using System.Buffers.Binary;
using System.Text;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Engine;

public sealed class DecodeContext
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _position;
    private readonly Stack<Scope> _scopeStack = new();
    // Pop したスコープを使い回す（変数辞書の容量も残るので、要素ごとのスコープ push で辞書を作り直さない）
    private readonly Stack<Scope> _scopePool = new();
    private readonly Endianness _defaultEndianness;
    private readonly Dictionary<string, (ReadOnlyMemory<byte> Data, StringTableEncoding Encoding)> _stringTables = new();
    private readonly Dictionary<string, object> _stateVariables = new();

    public DecodeContext(ReadOnlyMemory<byte> data, Endianness endianness)
    {
        _data = data;
        _defaultEndianness = endianness;
        // データ全体をカバーするルートスコープをプッシュ
        _scopeStack.Push(new Scope().Reset(0, data.Length, null, isOverlay: false, capturesVariables: true));
    }

    public Endianness Endianness
    {
        get
        {
            foreach (var scope in _scopeStack)
            {
                if (scope.ScopeEndianness.HasValue)
                    return scope.ScopeEndianness.Value;
            }
            return _defaultEndianness;
        }
    }
    public int Position => _position;
    public int DataLength => _data.Length;
    public int Remaining => CurrentScope.End - _position;

    /// <summary>
    /// 式の <c>_offset</c> の値（REQ-194）。ふつうは <see cref="Position"/> と同じ。ビットストリームモードで読みかけのバイトがあれば、
    /// そのバイトの位置（端数のビットは切り捨て）。<see cref="Position"/> は読みかけのバイトの次を指している。
    /// </summary>
    public int ByteOffset => _bitReader is { HasPartialByte: true } ? _position - 1 : _position;
    public bool IsEof => _position >= CurrentScope.End;

    public ReadOnlyMemory<byte> SliceOriginal(int offset, int length) => _data.Slice(offset, length);

    public void Seek(int absoluteOffset)
    {
        if (absoluteOffset < 0 || absoluteOffset > _data.Length)
            throw new InvalidOperationException(
                $"Seek offset {absoluteOffset} (0x{absoluteOffset:X}) is out of range: valid range is 0..{_data.Length} (0x{_data.Length:X})");
        _position = absoluteOffset;
    }

    public int SavePosition() => _position;

    public void RestorePosition(int savedPosition) => _position = savedPosition;

    private Scope CurrentScope => _scopeStack.Peek();

    public void PushScope(int size)
    {
        // 負のサイズ（式の評価結果が壊れている）を通すと End < Start のスコープができ、Pop で位置が逆走する
        if (size < 0)
            throw new InvalidOperationException($"Cannot push scope of negative size {size} at position {_position}");
        // int で足すと 0x7FFFFFFF 級のサイズで桁あふれして負になり、境界検査をすり抜ける
        var end = (long)_position + size;
        if (end > _data.Length)
            throw new InvalidOperationException(
                $"Cannot push scope of size {size} at position {_position}: would exceed data length {_data.Length}");
        _scopeStack.Push(Rent().Reset(_position, (int)end, null, isOverlay: false, capturesVariables: true));
    }

    /// <summary>
    /// 境界変更なし、エンディアンのみ切り替えるオーバーレイスコープをプッシュする。
    /// </summary>
    /// <param name="capturesVariables">
    /// false なら、このスコープの間に束縛された変数は外側（最も近い変数を持つスコープ）に書かれる。
    /// フィールド単位の <c>endianness:</c> はフィールドのデコード中だけ有効なので、値まで一緒に捨ててはいけない。
    /// </param>
    public void PushEndiannessScope(Endianness endianness, bool capturesVariables = true)
    {
        _scopeStack.Push(Rent().Reset(_position, CurrentScope.End, endianness, isOverlay: true, capturesVariables: capturesVariables));
    }

    /// <summary>
    /// 境界もエンディアンスも変更せず、変数スコープのみ作成するオーバーレイスコープをプッシュする。
    /// テンプレートパラメータの分離に使用。
    /// </summary>
    public void PushVariableScope()
    {
        _scopeStack.Push(Rent().Reset(_position, CurrentScope.End, null, isOverlay: true, capturesVariables: true));
    }

    /// <summary>
    /// seek の行き先 <paramref name="target"/> が今の境界（最も内側の size 付きのスコープ）の外なら、
    /// 行き先を含む最も内側の外側の境界（無ければファイル全体）と同じ範囲のオーバーレイスコープを push して true を返す（REQ-191）。
    /// 境界は [Start, End) で判定する（End ちょうどのバイトはその境界の外にある）。どの境界も含まない行き先（ファイルの終わり）と、
    /// 今の境界の中の行き先では何もしない（従来どおり）。
    /// 広げたスコープは変数を捕捉せず（フィールドの値はいつもの場所に束縛される）、エンディアンも変えない。
    /// 範囲は元の境界と同じ扱いなので、中の seek もこのスコープを基準に判定する。
    /// </summary>
    public bool PushSeekBoundary(int target)
    {
        var isCurrent = true;
        foreach (var scope in _scopeStack)
        {
            if (!scope.IsBoundary)
                continue;
            if (target >= scope.Start && target < scope.End)
            {
                if (isCurrent)
                    return false;
                _scopeStack.Push(Rent().Reset(scope.Start, scope.End, null, isOverlay: true, capturesVariables: false, isBoundary: true));
                return true;
            }
            isCurrent = false;
        }
        return false;
    }

    /// <summary>スコープの深さ（<see cref="PopScopesTo"/> で戻す位置の記録用）。</summary>
    public int ScopeDepth => _scopeStack.Count;

    /// <summary>スコープの深さが <paramref name="depth"/> になるまで Pop する（REQ-191。seek で広げたスコープを戻す）。</summary>
    public void PopScopesTo(int depth)
    {
        while (_scopeStack.Count > depth)
            PopScope();
    }

    private Scope Rent() => _scopePool.Count > 0 ? _scopePool.Pop() : new Scope();

    public void PopScope()
    {
        var scope = PopWithoutRecycling();
        scope.Variables.Clear();
        _scopePool.Push(scope);
    }

    /// <summary>
    /// スコープを抜け、その間に束縛された変数を外側（最も近い変数を持つスコープ）に移す（REQ-190）。
    /// size 付きの繰り返しの境界スコープ用。<paramref name="localNames"/> の変数（繰り返しの <c>_index</c> / <c>_prev</c>）は
    /// 移さずに捨てるので、外側の同名の変数（外側の繰り返しの <c>_index</c> 等）がそのまま見える。
    /// </summary>
    public void PopScopeCarryingVariables(params ReadOnlySpan<string> localNames)
    {
        var scope = PopWithoutRecycling();
        foreach (var (name, value) in scope.Variables)
        {
            if (!localNames.Contains(name))
                SetVariable(name, value);
        }
        scope.Variables.Clear();
        _scopePool.Push(scope);
    }

    private Scope PopWithoutRecycling()
    {
        if (_scopeStack.Count <= 1)
            throw new InvalidOperationException("Cannot pop the root scope");
        var scope = _scopeStack.Pop();
        // オーバーレイスコープの場合はpositionを進めない
        if (!scope.IsOverlay)
            _position = scope.End;
        return scope;
    }

    /// <summary>
    /// 現在の位置を指定バイト境界にアラインする。すでに境界上にある場合は何もしない。
    /// </summary>
    /// <returns>スキップしたパディングバイト数。</returns>
    public int AlignTo(int alignment)
    {
        if (alignment <= 0)
            throw new ArgumentOutOfRangeException(nameof(alignment),
                $"アライメント値は正の整数が必要です: {alignment}");
        var padding = (alignment - (_position % alignment)) % alignment;
        if (padding > 0)
        {
            if (_position + padding > CurrentScope.End)
                throw new InvalidOperationException(
                    $"アライメント {alignment} バイト境界に揃えるために {padding} バイトのパディングが必要ですが、" +
                    $"スコープ内の残りは {Remaining} バイトです");
            _position += padding;
        }
        return padding;
    }

    public void SetStateVariable(string name, object value)
        => _stateVariables[name] = value;

    public object? GetStateVariable(string name)
        => _stateVariables.TryGetValue(name, out var value) ? value : null;

    public bool HasStateVariable(string name)
        => _stateVariables.ContainsKey(name);

    public void RegisterStringTable(string name, int offset, int size, StringTableEncoding encoding)
    {
        _stringTables[name] = (_data.Slice(offset, size), encoding);
    }

    public string? LookupString(string tableName, int offset)
    {
        if (!_stringTables.TryGetValue(tableName, out var entry))
            return null;
        var (table, encoding) = entry;
        if (offset < 0 || offset >= table.Length)
            return null;

        var span = table.Span;

        if (encoding is StringTableEncoding.Utf16Le or StringTableEncoding.Utf16Be)
        {
            // UTF-16: 2バイト null (0x00, 0x00) 終端
            var end = offset;
            while (end + 1 < span.Length)
            {
                if (span[end] == 0 && span[end + 1] == 0)
                    break;
                end += 2;
            }
            var enc = encoding == StringTableEncoding.Utf16Le
                ? Encoding.Unicode
                : Encoding.BigEndianUnicode;
            return enc.GetString(span[offset..end]);
        }
        else
        {
            // ASCII / UTF-8: 1バイト null (0x00) 終端
            var end = offset;
            while (end < span.Length && span[end] != 0)
                end++;
            var enc = encoding == StringTableEncoding.Utf8
                ? Encoding.UTF8
                : Encoding.ASCII;
            return enc.GetString(span[offset..end]);
        }
    }

    /// <summary>整数値の束縛。小さな値はボックスを使い回す。</summary>
    public void SetVariable(string name, long value) => SetVariable(name, BoxCache.Box(value));

    public void SetVariable(string name, object value)
    {
        foreach (var scope in _scopeStack)
        {
            if (scope.CapturesVariables)
            {
                scope.Variables[name] = value;
                return;
            }
        }
        CurrentScope.Variables[name] = value;
    }

    public object? GetVariable(string name)
    {
        foreach (var scope in _scopeStack)
        {
            if (scope.Variables.TryGetValue(name, out var value))
                return ReferenceEquals(value, Undefined) ? null : value;
        }
        return null;
    }

    /// <summary>「未定義」を表す番人。外側のスコープに同名の変数があっても見えなくする。</summary>
    private static readonly object Undefined = new();

    /// <summary>
    /// フィールドのデコードに失敗したとき、その名前を「未定義」として束縛する（エラー継続モード用）。
    /// こうしないと、入れ子の struct で失敗したフィールドが外側の同名変数（例: 再帰する msgpack の <c>format_byte</c>）に
    /// フォールバックし、壊れた入力で無限再帰になる（REQ-160 のファズで検出）。
    /// </summary>
    public void MarkVariableUndefined(string name) => SetVariable(name, Undefined);

    public byte ReadUInt8()
    {
        EnsureAvailable(1);
        var value = _data.Span[_position];
        _position++;
        return value;
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var span = _data.Span.Slice(_position, 2);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt16BigEndian(span)
            : BinaryPrimitives.ReadUInt16LittleEndian(span);
        _position += 2;
        return value;
    }

    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        var span = _data.Span.Slice(_position, 4);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
        _position += 4;
        return value;
    }

    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        var span = _data.Span.Slice(_position, 8);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadUInt64BigEndian(span)
            : BinaryPrimitives.ReadUInt64LittleEndian(span);
        _position += 8;
        return value;
    }

    public sbyte ReadInt8()
    {
        EnsureAvailable(1);
        var value = (sbyte)_data.Span[_position];
        _position++;
        return value;
    }

    public short ReadInt16()
    {
        EnsureAvailable(2);
        var span = _data.Span.Slice(_position, 2);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt16BigEndian(span)
            : BinaryPrimitives.ReadInt16LittleEndian(span);
        _position += 2;
        return value;
    }

    public int ReadInt32()
    {
        EnsureAvailable(4);
        var span = _data.Span.Slice(_position, 4);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt32BigEndian(span)
            : BinaryPrimitives.ReadInt32LittleEndian(span);
        _position += 4;
        return value;
    }

    public long ReadInt64()
    {
        EnsureAvailable(8);
        var span = _data.Span.Slice(_position, 8);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadInt64BigEndian(span)
            : BinaryPrimitives.ReadInt64LittleEndian(span);
        _position += 8;
        return value;
    }

    public ReadOnlyMemory<byte> ReadBytes(int count)
    {
        EnsureAvailable(count);
        var result = _data.Slice(_position, count);
        _position += count;
        return result;
    }

    public float ReadFloat32()
    {
        EnsureAvailable(4);
        var span = _data.Span.Slice(_position, 4);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadSingleBigEndian(span)
            : BinaryPrimitives.ReadSingleLittleEndian(span);
        _position += 4;
        return value;
    }

    public double ReadFloat64()
    {
        EnsureAvailable(8);
        var span = _data.Span.Slice(_position, 8);
        var value = Endianness == Endianness.Big
            ? BinaryPrimitives.ReadDoubleBigEndian(span)
            : BinaryPrimitives.ReadDoubleLittleEndian(span);
        _position += 8;
        return value;
    }

    public ulong ReadULeb128()
    {
        ulong result = 0;
        int shift = 0;
        for (int i = 0; i < 10; i++)
        {
            EnsureAvailable(1);
            byte b = _data.Span[_position];
            _position++;
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
        }
        throw new InvalidOperationException(
            $"ULEB128 value exceeds 10 bytes at position 0x{_position:X}");
    }

    public long ReadSLeb128()
    {
        long result = 0;
        int shift = 0;
        byte b;
        for (int i = 0; i < 10; i++)
        {
            EnsureAvailable(1);
            b = _data.Span[_position];
            _position++;
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
            if ((b & 0x80) == 0)
            {
                // Sign extend if the sign bit (bit 6) of the last byte is set
                if (shift < 64 && (b & 0x40) != 0)
                    result |= -(1L << shift);
                return result;
            }
        }
        throw new InvalidOperationException(
            $"SLEB128 value exceeds 10 bytes at position 0x{_position:X}");
    }

    public ulong ReadVlq()
    {
        ulong result = 0;
        for (int i = 0; i < 10; i++)
        {
            EnsureAvailable(1);
            byte b = _data.Span[_position];
            _position++;
            result = (result << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0)
                return result;
        }
        throw new InvalidOperationException(
            $"VLQ value exceeds 10 bytes at position 0x{_position:X}");
    }

    public int FindMarker(ReadOnlySpan<byte> marker)
    {
        var searchSpan = _data.Span[_position..CurrentScope.End];
        var index = searchSpan.IndexOf(marker);
        return index >= 0 ? _position + index : -1;
    }

    public string ReadAsciiUntilNull()
    {
        var start = _position;
        while (_position < CurrentScope.End && _data.Span[_position] != 0)
            _position++;
        var value = Encoding.ASCII.GetString(_data.Span[start.._position]);
        if (_position < CurrentScope.End)
            _position++; // consume NUL
        return value;
    }

    public string ReadStringUntilNull(Encoding encoding)
    {
        var start = _position;
        while (_position < CurrentScope.End && _data.Span[_position] != 0)
            _position++;
        var value = encoding.GetString(_data.Span[start.._position]);
        if (_position < CurrentScope.End)
            _position++; // consume NUL
        return value;
    }

    public string ReadAscii(int count) => ReadString(count, Encoding.ASCII);

    public string ReadUtf8(int count) => ReadString(count, Encoding.UTF8);

    public string ReadString(int count, Encoding encoding)
    {
        EnsureAvailable(count);
        var result = encoding.GetString(_data.Span.Slice(_position, count));
        _position += count;
        return result;
    }

    // --- Bitstream mode ---
    private BitReader? _bitReader;
    private int _bitstreamDepth;

    public bool IsBitstreamMode => _bitReader is not null;
    public int? CurrentBitOffset => _bitReader?.BitPosition;

    public void EnterBitstreamMode(BitOrder bitOrder = BitOrder.Msb)
    {
        if (_bitstreamDepth == 0)
            _bitReader = new BitReader(this, bitOrder);
        _bitstreamDepth++;
    }

    public void ExitBitstreamMode()
    {
        _bitstreamDepth--;
        if (_bitstreamDepth == 0)
        {
            _bitReader!.AlignToByte();
            _bitReader = null;
        }
    }

    public long ReadBitsAsLong(int bitCount)
    {
        return _bitReader!.ReadBits(bitCount);
    }

    private void EnsureAvailable(int count)
    {
        if (count < 0)
            throw new InvalidOperationException($"Cannot read a negative number of bytes ({count}) at position 0x{_position:X}");
        if ((long)_position + count > CurrentScope.End)
            throw new InvalidOperationException(
                $"Cannot read {count} bytes at position 0x{_position:X}: only {CurrentScope.End - _position} bytes remaining in scope");
    }

    /// <summary>スコープ。Pop 後に使い回すため可変（<see cref="Reset"/>）。</summary>
    private sealed class Scope
    {
        public int Start { get; private set; }
        public int End { get; private set; }
        public Endianness? ScopeEndianness { get; private set; }
        public bool IsOverlay { get; private set; }
        /// <summary>false のとき <see cref="SetVariable"/> はこのスコープを素通りして外側に書く。</summary>
        public bool CapturesVariables { get; private set; }
        /// <summary>
        /// 読み取りの境界を決めるスコープか（size 付きのスコープ・ルート・seek で広げたスコープ）。
        /// エンディアン・変数だけのオーバーレイは親の境界をそのまま使うので false（Start が境界の先頭ではない）。
        /// </summary>
        public bool IsBoundary { get; private set; }
        public Dictionary<string, object> Variables { get; } = new();

        public Scope Reset(int start, int end, Endianness? endianness, bool isOverlay, bool capturesVariables, bool? isBoundary = null)
        {
            Start = start;
            End = end;
            ScopeEndianness = endianness;
            IsOverlay = isOverlay;
            CapturesVariables = capturesVariables;
            IsBoundary = isBoundary ?? !isOverlay;
            return this;
        }
    }

    private sealed class BitReader
    {
        private readonly DecodeContext _context;
        private readonly BitOrder _bitOrder;
        private int _bitPosition;   // 0–7: 現在バイト内の次に読み取るビット位置
        private byte _currentByte;
        private bool _hasByte;

        public BitReader(DecodeContext context, BitOrder bitOrder = BitOrder.Msb)
        {
            _context = context;
            _bitOrder = bitOrder;
        }

        public int BitPosition => _bitPosition;

        /// <summary>読みかけのバイトがあるか（次のビットを今のバイトから読む）。</summary>
        public bool HasPartialByte => _hasByte;

        public long ReadBits(int count)
        {
            if (count <= 0 || count > 64)
                throw new InvalidOperationException(
                    $"Bit read count must be 1–64, got {count}");

            return _bitOrder == BitOrder.Lsb ? ReadBitsLsb(count) : ReadBitsMsb(count);
        }

        private long ReadBitsMsb(int count)
        {
            long result = 0;
            var remaining = count;

            while (remaining > 0)
            {
                if (!_hasByte)
                {
                    _currentByte = _context.ReadUInt8();
                    _bitPosition = 0;
                    _hasByte = true;
                }

                var availableInByte = 8 - _bitPosition;
                var bitsToRead = Math.Min(remaining, availableInByte);

                // Extract bitsToRead bits from _currentByte starting at _bitPosition (MSB-first)
                var shift = availableInByte - bitsToRead;
                var mask = (1 << bitsToRead) - 1;
                var bits = (_currentByte >> shift) & mask;

                result = (result << bitsToRead) | (uint)bits;
                _bitPosition += bitsToRead;
                remaining -= bitsToRead;

                if (_bitPosition >= 8)
                {
                    _hasByte = false;
                    _bitPosition = 0;
                }
            }

            return result;
        }

        private long ReadBitsLsb(int count)
        {
            long result = 0;
            var totalBitsRead = 0;
            var remaining = count;

            while (remaining > 0)
            {
                if (!_hasByte)
                {
                    _currentByte = _context.ReadUInt8();
                    _bitPosition = 0;
                    _hasByte = true;
                }

                var availableInByte = 8 - _bitPosition;
                var bitsToRead = Math.Min(remaining, availableInByte);

                // Extract bitsToRead bits from _currentByte starting at _bitPosition (LSB-first)
                var mask = (1 << bitsToRead) - 1;
                var bits = (_currentByte >> _bitPosition) & mask;

                result |= (long)(uint)bits << totalBitsRead;
                _bitPosition += bitsToRead;
                totalBitsRead += bitsToRead;
                remaining -= bitsToRead;

                if (_bitPosition >= 8)
                {
                    _hasByte = false;
                    _bitPosition = 0;
                }
            }

            return result;
        }

        public void AlignToByte()
        {
            if (_hasByte && _bitPosition > 0)
            {
                // Discard remaining bits in current byte
                _hasByte = false;
                _bitPosition = 0;
            }
        }
    }
}
