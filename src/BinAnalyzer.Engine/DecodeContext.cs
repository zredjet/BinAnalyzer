using System.Buffers.Binary;
using System.Text;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Engine;

public sealed class DecodeContext
{
    private readonly ReadOnlyMemory<byte> _data;
    private int _position;
    private readonly Stack<Scope> _scopeStack = new();

    public DecodeContext(ReadOnlyMemory<byte> data, Endianness endianness)
    {
        _data = data;
        Endianness = endianness;
        // データ全体をカバーするルートスコープをプッシュ
        _scopeStack.Push(new Scope(0, data.Length));
    }

    public Endianness Endianness { get; }
    public int Position => _position;
    public int Remaining => CurrentScope.End - _position;
    public bool IsEof => _position >= CurrentScope.End;

    public ReadOnlyMemory<byte> SliceOriginal(int offset, int length) => _data.Slice(offset, length);

    private Scope CurrentScope => _scopeStack.Peek();

    public void PushScope(int size)
    {
        var end = _position + size;
        if (end > _data.Length)
            throw new InvalidOperationException(
                $"Cannot push scope of size {size} at position {_position}: would exceed data length {_data.Length}");
        _scopeStack.Push(new Scope(_position, end));
    }

    public void PopScope()
    {
        if (_scopeStack.Count <= 1)
            throw new InvalidOperationException("Cannot pop the root scope");
        var scope = _scopeStack.Pop();
        // スコープ末尾まで位置を進める（未読バイトを消費）
        _position = scope.End;
    }

    public void SetVariable(string name, object value)
    {
        CurrentScope.Variables[name] = value;
    }

    public object? GetVariable(string name)
    {
        foreach (var scope in _scopeStack)
        {
            if (scope.Variables.TryGetValue(name, out var value))
                return value;
        }
        return null;
    }

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

    private void EnsureAvailable(int count)
    {
        if (_position + count > CurrentScope.End)
            throw new InvalidOperationException(
                $"Cannot read {count} bytes at position 0x{_position:X}: only {CurrentScope.End - _position} bytes remaining in scope");
    }

    private sealed class Scope(int start, int end)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public Dictionary<string, object> Variables { get; } = new();
    }
}
