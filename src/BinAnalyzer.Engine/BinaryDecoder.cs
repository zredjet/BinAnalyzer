using System.IO.Compression;
using System.Text;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;

#pragma warning disable CA5350 // SHA-1 is used for checksum verification, not security
#pragma warning disable CA5351 // MD5 is used for checksum verification, not security

namespace BinAnalyzer.Engine;

public sealed class BinaryDecoder : IBinaryDecoder
{
    private readonly Stack<string> _pathStack = new();
    private ErrorMode _errorMode = ErrorMode.Stop;
    private List<DecodeError>? _errors;
    private int? _globalMaxRepeat;

    /// <summary>
    /// struct / switch の入れ子の深さ上限の既定値。1 段あたりのスタック消費は Debug ビルドで約 8 KB（Release でもその数分の一）あり、
    /// Windows のメインスレッドや スレッドプールのスタックは 1 MB なので、余裕を見て 64 にしている。実在のフォーマットで 20 段を超えることはまず無い。
    /// </summary>
    public const int DefaultMaxDepth = 64;

    /// <summary>0 バイトで成功した要素がこの数だけ続いたら繰り返しを打ち切る（REQ-197）。SQLite の列数の上限（32767）より大きい。</summary>
    public const int DefaultMaxZeroLengthElements = 65536;
    private int _maxZeroLengthElements = DefaultMaxZeroLengthElements;
    private int _maxDepth = DefaultMaxDepth;
    private int _depth;

    private string CurrentPath => _pathStack.Count == 0 ? "(root)" : string.Join(".", _pathStack.Reverse());

    public DecodedStruct Decode(ReadOnlyMemory<byte> data, FormatDefinition format, DecodeOptions? options = null)
    {
        _pathStack.Clear();
        _errorMode = ErrorMode.Stop;
        _errors = null;
        _globalMaxRepeat = options?.MaxRepeat;
        _maxDepth = options?.MaxDepth ?? DefaultMaxDepth;
        _maxZeroLengthElements = options?.MaxZeroLengthElements ?? DefaultMaxZeroLengthElements;
        _depth = 0;
        var context = new DecodeContext(data, options?.Endianness ?? format.Endianness);
        var rootStruct = format.Structs[format.RootStruct];
        return DecodeStruct(rootStruct, format, context, format.Name);
    }

    public DecodeResult DecodeWithRecovery(ReadOnlyMemory<byte> data, FormatDefinition format, ErrorMode errorMode, DecodeOptions? options = null)
    {
        _pathStack.Clear();
        _errorMode = errorMode;
        _errors = errorMode == ErrorMode.Continue ? new List<DecodeError>() : null;
        _globalMaxRepeat = options?.MaxRepeat;
        _maxDepth = options?.MaxDepth ?? DefaultMaxDepth;
        _maxZeroLengthElements = options?.MaxZeroLengthElements ?? DefaultMaxZeroLengthElements;
        _depth = 0;
        var context = new DecodeContext(data, options?.Endianness ?? format.Endianness);
        var rootStruct = format.Structs[format.RootStruct];
        var root = DecodeStruct(rootStruct, format, context, format.Name);
        return new DecodeResult
        {
            Root = root,
            Errors = _errors?.AsReadOnly() ?? (IReadOnlyList<DecodeError>)Array.Empty<DecodeError>(),
        };
    }

    /// <summary>
    /// エラー継続モードで読めなかったフィールドの名前を未定義にする。bitfield はエントリの名前も未定義にする（REQ-193）。
    /// しないと、外側のスコープや前の要素の同名の値が見えたまま後ろの式が評価され、再帰する定義では壊れた入力で指数的に展開する。
    /// </summary>
    private static void MarkFieldUndefined(FieldDefinition field, DecodeContext context)
    {
        context.MarkVariableUndefined(field.Name);
        if (field.BitfieldEntries is { } entries)
            foreach (var entry in entries)
                context.MarkVariableUndefined(entry.Name);
    }

    private void ProcessStateVariable(FieldDefinition field, DecodeContext context)
    {
        if (field.State is not { } stateName)
            return;

        // 条件評価（state_if がなければ常に更新）
        if (field.StateIf is not null && !ExpressionEvaluator.EvaluateAsBool(field.StateIf, context))
            return;

        // フィールドの値を状態変数に保存
        var value = context.GetVariable(field.Name);
        if (value is not null)
            context.SetStateVariable(stateName, value);
    }

    /// <summary>
    /// 入れ子（struct / switch フィールド）としての struct デコード。深さが <see cref="_maxDepth"/> を超えたらエラーにする。
    /// 再帰的なフォーマット（msgpack / CBOR / ISO BMFF 等）が壊れた入力で無限再帰しても、スタックオーバーフローで落ちる前に止める。
    /// </summary>
    private DecodedStruct DecodeNestedStruct(
        StructDefinition structDef,
        FormatDefinition format,
        DecodeContext context,
        string name,
        FieldType dslType)
    {
        if (_depth >= _maxDepth)
            throw new InvalidOperationException(
                $"Struct nesting deeper than {_maxDepth} levels at '{name}' (struct '{structDef.Name}'); the input is probably corrupt or the format recurses without consuming bytes");
        _depth++;
        try
        {
            return DecodeStruct(structDef, format, context, name, dslType);
        }
        finally
        {
            _depth--;
        }
    }

    private DecodedStruct DecodeStruct(
        StructDefinition structDef,
        FormatDefinition format,
        DecodeContext context,
        string name,
        FieldType? dslType = null)
    {
        // scope: isolated の struct は独自の変数のスコープを持つ（REQ-195）
        if (structDef.IsolatedScope)
            context.PushVariableScope();

        var hasEndiannessOverride = structDef.Endianness.HasValue;
        if (hasEndiannessOverride)
            context.PushEndiannessScope(structDef.Endianness!.Value);

        // 動的エンディアン式の評価
        var hasDynamicEndianness = false;
        if (structDef.EndiannessExpression is not null)
        {
            var result = ExpressionEvaluator.EvaluateAsString(
                structDef.EndiannessExpression, context);
            var endianness = result.ToLowerInvariant() switch
            {
                "little" or "le" => Endianness.Little,
                "big" or "be" => Endianness.Big,
                _ => throw new InvalidOperationException(
                    $"Dynamic endianness expression evaluated to invalid value: '{result}'"),
            };
            context.PushEndiannessScope(endianness);
            hasDynamicEndianness = true;
        }

        var startOffset = context.Position;
        var fields = structDef.Fields;
        var children = new List<DecodedNode>(fields.Count);

        var isBitstream = structDef.IsBitstream;
        if (isBitstream)
            context.EnterBitstreamMode(structDef.BitOrder ?? BitOrder.Msb);

        for (var i = 0; i < fields.Count; i++)
        {
            var node = DecodeField(fields[i], format, context, children, structDef);
            if (node is not null)
                children.Add(node);
        }

        if (isBitstream)
            context.ExitBitstreamMode();

        if (hasDynamicEndianness)
            context.PopScope();
        if (hasEndiannessOverride)
            context.PopScope();
        if (structDef.IsolatedScope)
            context.PopScope();

        // 文字列テーブル登録
        if (structDef.StringTableEncoding is { } stEncoding)
        {
            var structSize = context.Position - startOffset;
            context.RegisterStringTable(name, startOffset, structSize, stEncoding);
        }

        return new DecodedStruct
        {
            Name = name,
            StructType = structDef.Name,
            Offset = startOffset,
            // seek（restore 無し）で先頭より前へ戻った場合は負になるので 0 に丸める
            Size = Math.Max(0, context.Position - startOffset),
            Children = children,
            DslType = dslType,
            IsolatedScope = structDef.IsolatedScope,
        };
    }

    private DecodedNode? DecodeField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null,
        StructDefinition? parentStruct = null)
    {
        _pathStack.Push(field.Name);
        try
        {
            if (field.Condition is not null)
            {
                if (!ExpressionEvaluator.EvaluateAsBool(field.Condition, context))
                    return null;
            }

            // --- seek処理 ---
            // 要素ごと seek の判別: seek式が _index や IndexAccess を含む場合
            bool perElementSeek = field.SeekExpression is not null
                && field.Repeat is not RepeatMode.None
                && (UsesIterationContext(field.SeekExpression.Root)
                    || (field.SeekBaseExpression is not null
                        && UsesIterationContext(field.SeekBaseExpression.Root)));

            int? savedPosition = null;
            int? scopeDepthBeforeSeek = null;
            if (field.SeekExpression is not null && !perElementSeek)
            {
                var seekOffset = ResolveSeekOffset(field, context);
                if (field.SeekRestore)
                    savedPosition = context.SavePosition();
                context.Seek(seekOffset);
                scopeDepthBeforeSeek = EnterSeekBoundary(context, seekOffset);
            }
            try
            {
                // デコード前: state_default の初期化
                if (field.State is not null && field.StateDefault.HasValue
                    && !context.HasStateVariable(field.State))
                    context.SetStateVariable(field.State, field.StateDefault.Value);

                if (field.Repeat is not RepeatMode.None)
                    return DecodeRepeatedField(field, format, context, perElementSeek);

                var node = DecodeSingleField(field, format, context, siblings);

                // デコード後: 状態変数更新
                ProcessStateVariable(field, context);

                // カスタムバリデーション式
                if (field.ValidationExpression is not null)
                {
                    var passed = ExpressionEvaluator.EvaluateAsBool(field.ValidationExpression, context);
                    node = SetValidation(node, new Core.Decoded.ValidationInfo(passed, field.ValidationExpression.OriginalText));
                }

                // フィールドレベルアライメント
                if (field.Align is { } align)
                    context.AlignTo(align);

                // パディングフラグを伝搬
                if (field.IsPadding)
                    return SetPaddingFlag(node);

                return node;
            }
            finally
            {
                // 境界の外への seek で広げたスコープを戻す（REQ-191）
                if (scopeDepthBeforeSeek is { } depth)
                    context.PopScopesTo(depth);
                if (savedPosition is { } pos)
                    context.RestorePosition(pos);
            }
        }
        catch (DecodeException dex)
        {
            if (_errorMode == ErrorMode.Continue)
            {
                _errors!.Add(new DecodeError(dex.Message, dex.Offset, dex.FieldPath, dex.FieldType));
                MarkFieldUndefined(field, context);
                var posBeforeSkip = context.Position;
                TrySkipField(field, context, parentStruct?.ResyncMarker, parentStruct?.Align);
                var skipped = context.Position - posBeforeSkip;
                return new DecodedError
                {
                    Name = field.Name,
                    Offset = dex.Offset,
                    Size = 0,
                    SkippedBytes = skipped,
                    ErrorMessage = dex.Message,
                    FieldType = dex.FieldType,
                    DslType = field.Type,
                };
            }
            throw;
        }
        catch (Exception ex)
        {
            if (_errorMode == ErrorMode.Continue)
            {
                var errorOffset = context.Position;
                _errors!.Add(new DecodeError(ex.Message, errorOffset, CurrentPath, field.Type.ToString()));
                MarkFieldUndefined(field, context);
                var posBeforeSkip = context.Position;
                TrySkipField(field, context, parentStruct?.ResyncMarker, parentStruct?.Align);
                var skipped = context.Position - posBeforeSkip;
                return new DecodedError
                {
                    Name = field.Name,
                    Offset = errorOffset,
                    Size = 0,
                    SkippedBytes = skipped,
                    ErrorMessage = ex.Message,
                    FieldType = field.Type.ToString(),
                    DslType = field.Type,
                };
            }
            throw new DecodeException(
                ex.Message, context.Position, CurrentPath,
                field.Type.ToString(), inner: ex);
        }
        finally
        {
            _pathStack.Pop();
        }
    }

    private DecodedNode DecodeSingleField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null)
    {
        var hasEndiannessOverride = field.Endianness.HasValue;
        if (hasEndiannessOverride)
            context.PushEndiannessScope(field.Endianness!.Value, capturesVariables: false);

        var result = field.Type switch
        {
            FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64
                or FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64
                => DecodeIntegerField(field, format, context, siblings),
            FieldType.Bytes => DecodeBytesField(field, context, siblings),
            FieldType.Ascii => DecodeAsciiField(field, format, context),
            FieldType.Utf8 => DecodeStringField(field, context, Encoding.UTF8, "utf8"),
            FieldType.Utf16Le => DecodeStringField(field, context, Encoding.Unicode, "utf16le"),
            FieldType.Utf16Be => DecodeStringField(field, context, Encoding.BigEndianUnicode, "utf16be"),
            FieldType.ShiftJis => DecodeStringField(field, context, EncodingHelper.ShiftJis, "sjis"),
            FieldType.Latin1 => DecodeStringField(field, context, Encoding.Latin1, "latin1"),
            FieldType.AsciiZ => DecodeNullTerminatedStringField(field, context, Encoding.ASCII, "asciiz"),
            FieldType.Utf8Z => DecodeNullTerminatedStringField(field, context, Encoding.UTF8, "utf8z"),
            FieldType.Float16 or FieldType.Float32 or FieldType.Float64 => DecodeFloatField(field, context),
            var t when FieldTypeCategories.IsCompressed(t) => DecodeCompressedField(field, format, context),
            FieldType.Struct => DecodeStructField(field, format, context),
            FieldType.Switch => DecodeSwitchField(field, format, context),
            FieldType.Bitfield => DecodeBitfieldField(field, format, context),
            FieldType.Virtual => DecodeVirtualField(field, format, context),
            FieldType.ULeb128 or FieldType.SLeb128 or FieldType.Vlq
                => DecodeVariableLengthIntegerField(field, format, context),
            _ => throw new InvalidOperationException($"Unknown field type: {field.Type}"),
        };

        if (hasEndiannessOverride)
            context.PopScope();

        return result;
    }

    private DecodedNode DecodeIntegerField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null)
    {
        if (context.IsBitstreamMode)
            return DecodeBitstreamIntegerField(field, format, context, siblings);

        var offset = context.Position;
        long value = field.Type switch
        {
            FieldType.UInt8 => context.ReadUInt8(),
            FieldType.UInt16 => context.ReadUInt16(),
            FieldType.UInt32 => context.ReadUInt32(),
            FieldType.UInt64 => (long)context.ReadUInt64(),
            FieldType.Int8 => context.ReadInt8(),
            FieldType.Int16 => context.ReadInt16(),
            FieldType.Int32 => context.ReadInt32(),
            FieldType.Int64 => context.ReadInt64(),
            _ => throw new InvalidOperationException($"Not an integer type: {field.Type}"),
        };

        var size = context.Position - offset;
        context.SetVariable(field.Name, value);

        // flags展開（整数型フィールドのFlagsRef対応）
        if (field.FlagsRef is not null && format.Flags.TryGetValue(field.FlagsRef, out var flagsDef))
        {
            var flagStates = DecodeFlagValues((uint)value, flagsDef);
            return new DecodedFlags
            {
                Name = field.Name,
                Offset = offset,
                Size = size,
                RawValue = value,
                FlagStates = flagStates,
                Description = field.Description,
                DslType = field.Type,
            };
        }

        string? enumLabel = null;
        string? enumDesc = null;
        if (field.EnumRef is not null && format.Enums.TryGetValue(field.EnumRef, out var enumDef))
        {
            var entry = enumDef.FindByValue(value);
            if (entry is not null)
            {
                enumLabel = entry.Label;
                enumDesc = entry.Description;
            }
        }

        // チェックサム検証
        bool? checksumValid = null;
        long? checksumExpected = null;
        string? checksumAlgorithm = null;
        IReadOnlyList<ByteRange>? checksumCoverage = null;
        if (field.Checksum is not null && siblings is not null)
        {
            var (valid, expected, coverage) = VerifyChecksum(field.Checksum, value, siblings, context,
                checksumFieldOffset: offset, checksumFieldSize: size);
            checksumValid = valid;
            checksumExpected = expected;
            checksumAlgorithm = field.Checksum.Algorithm;
            checksumCoverage = coverage;
        }

        // 文字列テーブル参照
        string? stringTableValue = null;
        if (field.StringTableRef is not null)
        {
            stringTableValue = context.LookupString(field.StringTableRef, (int)value);
        }

        return new DecodedInteger
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            EnumLabel = enumLabel,
            EnumDescription = enumDesc,
            ChecksumValid = checksumValid,
            ChecksumExpected = checksumExpected,
            ChecksumAlgorithm = checksumAlgorithm,
            ChecksumCoverage = checksumCoverage,
            StringTableValue = stringTableValue,
            Description = field.Description,
            DslType = field.Type,
            Endianness = context.Endianness,
            EnumRef = field.EnumRef,
        };
    }

    private DecodedInteger DecodeBitstreamIntegerField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null)
    {
        var byteOffset = context.Position;
        var bitOffset = context.CurrentBitOffset ?? 0;
        var bitSize = ResolveSize(field, context);

        if (bitSize <= 0 || bitSize > 64)
            throw new InvalidOperationException(
                $"Bitstream field '{field.Name}' has invalid bit size {bitSize}. Must be 1–64.");

        var rawValue = context.ReadBitsAsLong(bitSize);

        // 符号拡張（signed 型の場合）
        if (IsSignedType(field.Type) && bitSize < 64)
        {
            var signBit = 1L << (bitSize - 1);
            if ((rawValue & signBit) != 0)
                rawValue |= ~((1L << bitSize) - 1);
        }

        context.SetVariable(field.Name, rawValue);

        string? enumLabel = null;
        string? enumDesc = null;
        if (field.EnumRef is not null && format.Enums.TryGetValue(field.EnumRef, out var enumDef))
        {
            var entry = enumDef.FindByValue(rawValue);
            if (entry is not null)
            {
                enumLabel = entry.Label;
                enumDesc = entry.Description;
            }
        }

        bool? checksumValid = null;
        long? checksumExpected = null;
        string? checksumAlgorithm = null;
        IReadOnlyList<ByteRange>? checksumCoverage = null;
        if (field.Checksum is not null && siblings is not null)
        {
            var (valid, expected, coverage) = VerifyChecksum(field.Checksum, rawValue, siblings, context,
                checksumFieldOffset: byteOffset, checksumFieldSize: (bitSize + 7) / 8);
            checksumValid = valid;
            checksumExpected = expected;
            checksumAlgorithm = field.Checksum.Algorithm;
            checksumCoverage = coverage;
        }

        string? stringTableValue = null;
        if (field.StringTableRef is not null)
        {
            stringTableValue = context.LookupString(field.StringTableRef, (int)rawValue);
        }

        return new DecodedInteger
        {
            Name = field.Name,
            Offset = byteOffset,
            BitOffset = bitOffset,
            Size = bitSize,
            Value = rawValue,
            EnumLabel = enumLabel,
            EnumDescription = enumDesc,
            ChecksumValid = checksumValid,
            ChecksumExpected = checksumExpected,
            ChecksumAlgorithm = checksumAlgorithm,
            ChecksumCoverage = checksumCoverage,
            StringTableValue = stringTableValue,
            Description = field.Description,
            DslType = field.Type,
            EnumRef = field.EnumRef,
        };
    }

    private static bool IsSignedType(FieldType type) =>
        type is FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64;

    private DecodedBytes DecodeBytesField(
        FieldDefinition field,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null)
    {
        var offset = context.Position;
        var size = ResolveSize(field, context);
        var bytes = context.ReadBytes(size);

        bool? valid = null;
        if (field.Expected is not null)
        {
            valid = bytes.Span.SequenceEqual(field.Expected);
        }

        bool? checksumValid = null;
        string? checksumExpectedHex = null;
        string? checksumAlgorithm = null;
        IReadOnlyList<ByteRange>? checksumCoverage = null;
        if (field.Checksum is not null && siblings is not null
            && ChecksumAlgorithms.IsHashAlgorithm(field.Checksum.Algorithm))
        {
            var (v, hex, coverage) = VerifyHashChecksum(field.Checksum, bytes, siblings, context,
                checksumFieldOffset: offset, checksumFieldSize: size);
            checksumValid = v;
            checksumExpectedHex = hex;
            checksumAlgorithm = field.Checksum.Algorithm;
            checksumCoverage = coverage;
        }

        return new DecodedBytes
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            RawBytes = bytes,
            ValidationPassed = valid,
            Description = field.Description,
            ChecksumValid = checksumValid,
            ChecksumExpectedHex = checksumExpectedHex,
            ChecksumAlgorithm = checksumAlgorithm,
            ChecksumCoverage = checksumCoverage,
            DslType = field.Type,
        };
    }

    private DecodedNode DecodeAsciiField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        var offset = context.Position;
        var size = ResolveSize(field, context);
        var value = context.ReadAscii(size);
        context.SetVariable(field.Name, value);

        IReadOnlyList<FlagState>? flagStates = null;
        if (field.FlagsRef is not null && format.Flags.TryGetValue(field.FlagsRef, out var flagsDef))
        {
            var rawSpan = context.SliceOriginal(offset, size).Span;
            uint rawValue = 0;
            for (var i = 0; i < rawSpan.Length && i < 4; i++)
                rawValue = (rawValue << 8) | rawSpan[i];
            flagStates = DecodeFlagValues(rawValue, flagsDef);
        }

        return new DecodedString
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            Encoding = "ascii",
            Flags = flagStates,
            Description = field.Description,
            DslType = field.Type,
        };
    }

    private DecodedString DecodeStringField(
        FieldDefinition field,
        DecodeContext context,
        Encoding encoding,
        string encodingName)
    {
        var offset = context.Position;
        var size = ResolveSize(field, context);
        var value = context.ReadString(size, encoding);
        context.SetVariable(field.Name, value);

        return new DecodedString
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            Encoding = encodingName,
            Description = field.Description,
            DslType = field.Type,
        };
    }

    private DecodedString DecodeNullTerminatedStringField(
        FieldDefinition field,
        DecodeContext context,
        Encoding encoding,
        string encodingName)
    {
        var offset = context.Position;
        var value = encoding == Encoding.ASCII
            ? context.ReadAsciiUntilNull()
            : context.ReadStringUntilNull(encoding);
        var size = context.Position - offset;
        context.SetVariable(field.Name, value);

        return new DecodedString
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            Encoding = encodingName,
            Description = field.Description,
            DslType = field.Type,
        };
    }

    private DecodedFloat DecodeFloatField(
        FieldDefinition field,
        DecodeContext context)
    {
        var offset = context.Position;
        var precision = field.Type switch
        {
            FieldType.Float16 => FloatPrecision.Half,
            FieldType.Float32 => FloatPrecision.Single,
            _ => FloatPrecision.Double,
        };
        double value = precision switch
        {
            FloatPrecision.Half => (double)context.ReadFloat16(),
            FloatPrecision.Single => context.ReadFloat32(),
            _ => context.ReadFloat64(),
        };
        var size = context.Position - offset;
        context.SetVariable(field.Name, value);

        return new DecodedFloat
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            Precision = precision,
            Description = field.Description,
            DslType = field.Type,
            Endianness = context.Endianness,
        };
    }

    private DecodedCompressed DecodeCompressedField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        var offset = context.Position;
        var compressedSize = ResolveSize(field, context);
        var compressedBytes = context.ReadBytes(compressedSize);
        var algorithm = field.Type switch
        {
            FieldType.Zlib => "zlib",
            FieldType.Deflate => "deflate",
            FieldType.Gzip => "gzip",
            FieldType.Bzip2 => "bzip2",
            FieldType.Lzma => "lzma",
            FieldType.Zstd => "zstd",
            FieldType.Lz4 => "lz4",
            _ => throw new InvalidOperationException($"Not a compression type: {field.Type}"),
        };

        byte[] decompressed;
        try
        {
            if (field.Type is FieldType.Zlib or FieldType.Deflate or FieldType.Gzip)
            {
                decompressed = DecompressBclStream(compressedBytes, field.Type);
            }
            else
            {
                decompressed = Compression.Decompressor.Decompress(compressedBytes, algorithm);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or IOException)
        {
            throw new DecodeException(
                $"Failed to decompress {algorithm} data: {ex.Message}",
                offset, CurrentPath, algorithm, inner: ex);
        }

        DecodedStruct? decodedContent = null;
        ReadOnlyMemory<byte>? rawDecompressed = decompressed;
        if (field.StructRef is not null && format.Structs.TryGetValue(field.StructRef, out var structDef))
        {
            var innerContext = new DecodeContext(decompressed, context.Endianness);
            decodedContent = DecodeStruct(structDef, format, innerContext, field.Name, FieldType.Struct);
            rawDecompressed = null;
        }

        return new DecodedCompressed
        {
            Name = field.Name,
            Offset = offset,
            Size = compressedSize,
            CompressedSize = compressedSize,
            DecompressedSize = decompressed.Length,
            Algorithm = algorithm,
            DecodedContent = decodedContent,
            RawDecompressed = rawDecompressed,
            Description = field.Description,
            DslType = field.Type,
        };
    }

    private static byte[] DecompressBclStream(ReadOnlyMemory<byte> compressedBytes, FieldType type)
    {
        using var input = new MemoryStream(compressedBytes.ToArray());
        using Stream decompressor = type switch
        {
            FieldType.Zlib => new ZLibStream(input, CompressionMode.Decompress),
            FieldType.Deflate => new DeflateStream(input, CompressionMode.Decompress),
            FieldType.Gzip => new GZipStream(input, CompressionMode.Decompress),
            _ => throw new InvalidOperationException($"Not a BCL compression type: {type}"),
        };
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }

    private DecodedStruct DecodeStructField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        if (field.StructRef is null)
            throw new InvalidOperationException($"Struct field '{field.Name}' has no struct reference");

        if (!format.Structs.TryGetValue(field.StructRef, out var structDef))
            throw new InvalidOperationException($"Struct '{field.StructRef}' not found");

        // テンプレート引数の解決
        bool hasTemplateArgs = structDef.Parameters.Count > 0;
        Dictionary<string, long>? resolvedArgs = null;
        if (hasTemplateArgs)
            resolvedArgs = ResolveTemplateArgs(structDef, field.StructArgs, context);

        // サイズ指定がある場合はバウンダリスコープ内でデコード
        // ビットストリームモード中はバイト単位スコープを使わない
        var hasSize = field.Size.HasValue || field.SizeExpression is not null || field.SizeRemaining;
        DecodedStruct result;
        if (hasSize && !context.IsBitstreamMode)
        {
            var size = ResolveSize(field, context);
            context.PushScope(size);
            if (resolvedArgs is not null)
                BindTemplateArgs(resolvedArgs, context);
            result = DecodeNestedStruct(structDef, format, context, field.Name, FieldType.Struct);
            context.PopScope();
        }
        else if (resolvedArgs is not null)
        {
            context.PushVariableScope();
            BindTemplateArgs(resolvedArgs, context);
            result = DecodeNestedStruct(structDef, format, context, field.Name, FieldType.Struct);
            context.PopScope();
        }
        else
        {
            result = DecodeNestedStruct(structDef, format, context, field.Name, FieldType.Struct);
        }

        // メンバーアクセス用に構造体ノードそのものを登録（式評価が子を名前で引く）
        context.SetVariable(field.Name, result);
        return result;
    }

    private DecodedNode DecodeSwitchField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        if (field.SwitchOn is null)
            throw new InvalidOperationException($"Switch field '{field.Name}' has no switch_on expression");

        var switchValue = ExpressionEvaluator.Evaluate(field.SwitchOn, context);

        // 一致するケースを検索
        string? matchedStructRef = null;
        if (field.SwitchCases is not null)
        {
            var cases = field.SwitchCases;
            for (var ci = 0; ci < cases.Count; ci++)
            {
                var switchCase = cases[ci];
                var caseValue = ExpressionEvaluator.Evaluate(switchCase.Condition, context);
                if (ValuesEqual(switchValue, caseValue))
                {
                    matchedStructRef = switchCase.StructRef;
                    break;
                }
            }
        }

        matchedStructRef ??= field.SwitchDefault;

        if (matchedStructRef is null)
            throw new InvalidOperationException(
                $"No matching case for switch value '{switchValue}' in field '{field.Name}'");

        if (!format.Structs.TryGetValue(matchedStructRef, out var structDef))
            throw new InvalidOperationException($"Struct '{matchedStructRef}' not found");

        // サイズ指定がある場合はバウンダリスコープ内でデコード
        // サイズ指定がない場合は内部structが消費したバイト数をそのまま採用
        // ビットストリームモード中はバイト単位スコープを使わない
        var hasSize = field.Size.HasValue || field.SizeExpression is not null || field.SizeRemaining;
        DecodedStruct switchResult;
        if (hasSize && !context.IsBitstreamMode)
        {
            var size = ResolveSize(field, context);
            context.PushScope(size);
            switchResult = DecodeNestedStruct(structDef, format, context, field.Name, FieldType.Switch);
            context.PopScope();
        }
        else
        {
            switchResult = DecodeNestedStruct(structDef, format, context, field.Name, FieldType.Switch);
        }

        // メンバーアクセス用に構造体を辞書として登録
        context.SetVariable(field.Name, switchResult);
        return switchResult;
    }

    private DecodedArray DecodeRepeatedField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        bool perElementSeek = false)
    {
        var startOffset = context.Position;
        var elements = new List<DecodedNode>();
        var singleField = perElementSeek ? WithoutRepeatAndSeek(field) : WithoutRepeat(field);

        // ビットストリームモードでは Size はビット幅（要素サイズ）なので、要素フィールドに引き継ぐ
        if (context.IsBitstreamMode && (field.Size.HasValue || field.SizeExpression is not null))
        {
            singleField = new FieldDefinition
            {
                Name = singleField.Name,
                Type = singleField.Type,
                Size = field.Size,
                SizeExpression = field.SizeExpression,
                SizeRemaining = singleField.SizeRemaining,
                EnumRef = singleField.EnumRef,
                FlagsRef = singleField.FlagsRef,
                StructRef = singleField.StructRef,
                StructArgs = singleField.StructArgs,
                Repeat = singleField.Repeat,
                SwitchOn = singleField.SwitchOn,
                SwitchCases = singleField.SwitchCases,
                SwitchDefault = singleField.SwitchDefault,
                BitfieldEntries = singleField.BitfieldEntries,
                Checksum = singleField.Checksum,
                Expected = singleField.Expected,
                Condition = singleField.Condition,
                Description = singleField.Description,
                Align = singleField.Align,
                IsPadding = singleField.IsPadding,
                ElementSize = singleField.ElementSize,
                ElementSizeExpression = singleField.ElementSizeExpression,
                ValueExpression = singleField.ValueExpression,
                SeekExpression = singleField.SeekExpression,
                SeekBaseExpression = singleField.SeekBaseExpression,
                SeekRestore = singleField.SeekRestore,
                Endianness = singleField.Endianness,
                ValidationExpression = singleField.ValidationExpression,
                StringTableRef = singleField.StringTableRef,
                DiffKey = singleField.DiffKey,
                State = singleField.State,
                StateIf = singleField.StateIf,
                StateDefault = singleField.StateDefault,
            };
        }

        var elementSize = ResolveElementSize(field, context);

        // --- ガード条件の解決（ループ前に1回だけ評価） ---
        long? repeatMax = field.RepeatMax is not null
            ? ExpressionEvaluator.EvaluateAsLong(field.RepeatMax, context)
            : _globalMaxRepeat;
        long? repeatErrorLimit = field.RepeatErrorLimit is not null
            ? ExpressionEvaluator.EvaluateAsLong(field.RepeatErrorLimit, context)
            : null;
        var consecutiveErrors = 0;
        var consecutiveZeroLength = 0;
        var truncated = false;
        string? truncationReason = null;

        // 配列全体の境界スコープ（size指定がある場合）
        // ビットストリームモード中はバイト単位スコープを使わない
        var hasBoundarySize = field.Size.HasValue || field.SizeExpression is not null || field.SizeRemaining;
        if (hasBoundarySize && !context.IsBitstreamMode)
        {
            var boundarySize = ResolveSize(field, context);
            context.PushScope(boundarySize);
        }

        // 構造体レベルアライメントの取得
        int? structAlign = null;
        StructDefinition? elementStructDef = null;
        if (field.StructRef is not null && format.Structs.TryGetValue(field.StructRef, out var repeatStructDef))
        {
            structAlign = repeatStructDef.Align;
            elementStructDef = repeatStructDef;
        }

        // 要素レベルエラー回復: 要素がstruct型で参照先structにResyncMarkerがある場合のみ有効
        var elementResyncMarker = (_errorMode == ErrorMode.Continue)
            ? elementStructDef?.ResyncMarker
            : null;

        switch (field.Repeat)
        {
            case RepeatMode.Count countMode:
            {
                var count = ExpressionEvaluator.EvaluateAsLong(countMode.CountExpression, context);
                // repeat_max によるカウントクランプ
                if (repeatMax.HasValue && count > repeatMax.Value)
                {
                    count = repeatMax.Value;
                    truncated = true;
                    truncationReason = $"repeat_max ({repeatMax.Value}) exceeded original count";
                }
                for (var i = 0; i < count; i++)
                {
                    context.SetVariable("_index", (long)i);

                    int? elementSavedPos = null;
                    int? elementScopeDepth = null;
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                        elementScopeDepth = EnterSeekBoundary(context, seekOffset);
                    }

                    try
                    {
                        if (structAlign is { } sa && i > 0 && !perElementSeek)
                            context.AlignTo(sa);

                        if (elementResyncMarker is not null)
                        {
                            if (!TryDecodeElementWithRecovery(singleField, format, context, elements, elementSize, elementResyncMarker, field.Type.ToString()))
                                break;
                            // 連続エラーカウンタ: DecodedError が追加された場合はインクリメント
                            if (elements.Count > 0 && elements[^1] is DecodedError)
                            {
                                consecutiveErrors++;
                                if (repeatErrorLimit.HasValue && consecutiveErrors >= repeatErrorLimit.Value)
                                {
                                    truncated = true;
                                    truncationReason = $"repeat_error_limit ({repeatErrorLimit.Value}) consecutive errors";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveErrors = 0;
                            }
                        }
                        else
                        {
                            var posBeforeElement = context.Position;
                            var element = DecodeElementWithScope(singleField, format, context, elementSize);
                            elements.Add(element);
                            consecutiveErrors = 0;

                            if (element is DecodedStruct)
                                PromoteDecodedValues(element, context);
                            SetPrevVariable(element, context);

                            // エラー継続モードで要素が 1 バイトも消費できなかった場合、count が壊れた値（数十億）だと
                            // 同じエラー要素を count 回積むだけで終わらない。位置が進まないエラー要素で打ち切る
                            if (!perElementSeek && context.Position == posBeforeElement && ContainsError(element))
                            {
                                truncated = true;
                                truncationReason = $"no progress at offset 0x{posBeforeElement:X} (element consumed 0 bytes)";
                                break;
                            }

                            // 0 バイトで成功する要素が壊れた要素数だけ続くと終わらないので、連続した数で打ち切る（REQ-197）。
                            // 要素ごとの seek は位置が要素ごとに決まるので対象外
                            if (!perElementSeek && context.Position == posBeforeElement)
                            {
                                if (++consecutiveZeroLength >= _maxZeroLengthElements)
                                {
                                    truncated = true;
                                    truncationReason = $"{_maxZeroLengthElements} consecutive elements consumed 0 bytes at offset 0x{posBeforeElement:X}";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveZeroLength = 0;
                            }
                        }
                    }
                    finally
                    {
                        // 境界の外への seek で広げたスコープを戻す（REQ-191）
                        if (elementScopeDepth is { } depth)
                            context.PopScopesTo(depth);
                    }

                    if (elementSavedPos is { } epos)
                        context.RestorePosition(epos);
                }
                break;
            }

            case RepeatMode.UntilEof:
            {
                var idx = 0;
                while (!context.IsEof)
                {
                    // repeat_max ガード
                    if (repeatMax.HasValue && idx >= repeatMax.Value)
                    {
                        truncated = true;
                        truncationReason = $"repeat_max ({repeatMax.Value}) reached";
                        break;
                    }

                    context.SetVariable("_index", (long)idx);

                    int? elementSavedPos = null;
                    int? elementScopeDepth = null;
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                        elementScopeDepth = EnterSeekBoundary(context, seekOffset);
                    }

                    try
                    {
                        if (structAlign is { } sa && idx > 0 && !perElementSeek)
                        {
                            context.AlignTo(sa);
                            if (context.IsEof) break;
                        }

                        if (elementResyncMarker is not null)
                        {
                            if (!TryDecodeElementWithRecovery(singleField, format, context, elements, elementSize, elementResyncMarker, field.Type.ToString()))
                                break;
                            if (elements.Count > 0 && elements[^1] is DecodedError)
                            {
                                consecutiveErrors++;
                                if (repeatErrorLimit.HasValue && consecutiveErrors >= repeatErrorLimit.Value)
                                {
                                    truncated = true;
                                    truncationReason = $"repeat_error_limit ({repeatErrorLimit.Value}) consecutive errors";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveErrors = 0;
                            }
                        }
                        else
                        {
                            var posBeforeElement = context.Position;
                            var element = DecodeElementWithScope(singleField, format, context, elementSize);
                            elements.Add(element);
                            consecutiveErrors = 0;

                            if (element is DecodedStruct)
                                PromoteDecodedValues(element, context);
                            SetPrevVariable(element, context);

                            // エラー継続モードで要素が 1 バイトも消費できなかった場合（末尾の端数バイト等）、
                            // 位置が進まず無限ループになるため打ち切る。
                            if (!perElementSeek && context.Position == posBeforeElement && ContainsError(element))
                            {
                                truncated = true;
                                truncationReason = $"no progress at offset 0x{posBeforeElement:X} (element consumed 0 bytes)";
                                break;
                            }

                            // 0 バイトで成功する要素が壊れた要素数だけ続くと終わらないので、連続した数で打ち切る（REQ-197）。
                            // 要素ごとの seek は位置が要素ごとに決まるので対象外
                            if (!perElementSeek && context.Position == posBeforeElement)
                            {
                                if (++consecutiveZeroLength >= _maxZeroLengthElements)
                                {
                                    truncated = true;
                                    truncationReason = $"{_maxZeroLengthElements} consecutive elements consumed 0 bytes at offset 0x{posBeforeElement:X}";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveZeroLength = 0;
                            }
                        }
                    }
                    finally
                    {
                        // 境界の外への seek で広げたスコープを戻す（REQ-191）
                        if (elementScopeDepth is { } depth)
                            context.PopScopesTo(depth);
                    }

                    if (elementSavedPos is { } epos)
                        context.RestorePosition(epos);

                    idx++;
                }
                break;
            }

            case RepeatMode.UntilValue untilMode:
            {
                var idx = 0;
                while (true)
                {
                    // repeat_max ガード
                    if (repeatMax.HasValue && idx >= repeatMax.Value)
                    {
                        truncated = true;
                        truncationReason = $"repeat_max ({repeatMax.Value}) reached";
                        break;
                    }

                    context.SetVariable("_index", (long)idx);

                    int? elementSavedPos = null;
                    int? elementScopeDepth = null;
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                        elementScopeDepth = EnterSeekBoundary(context, seekOffset);
                    }

                    try
                    {
                        if (structAlign is { } sa && idx > 0 && !perElementSeek)
                            context.AlignTo(sa);

                        if (elementResyncMarker is not null)
                        {
                            if (!TryDecodeElementWithRecovery(singleField, format, context, elements, elementSize, elementResyncMarker, field.Type.ToString()))
                                break;
                            if (elements.Count > 0 && elements[^1] is DecodedError)
                            {
                                consecutiveErrors++;
                                if (repeatErrorLimit.HasValue && consecutiveErrors >= repeatErrorLimit.Value)
                                {
                                    truncated = true;
                                    truncationReason = $"repeat_error_limit ({repeatErrorLimit.Value}) consecutive errors";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveErrors = 0;
                            }
                            // UntilValue条件はエラー回復時にはスキップ（条件評価できないため）
                        }
                        else
                        {
                            var posBeforeElement = context.Position;
                            var (element, conditionMet) = DecodeElementWithScopeAndCondition(
                                singleField, format, context, elementSize, untilMode.Condition);
                            elements.Add(element);
                            consecutiveErrors = 0;

                            if (element is DecodedStruct)
                                PromoteDecodedValues(element, context);
                            SetPrevVariable(element, context);

                            if (elementSavedPos is { } epos)
                                context.RestorePosition(epos);

                            if (conditionMet)
                                break;

                            // エラー継続モードで位置が進まないエラー要素は打ち切る（条件も評価できないので無限ループになる）
                            if (!perElementSeek && context.Position == posBeforeElement && ContainsError(element))
                            {
                                truncated = true;
                                truncationReason = $"no progress at offset 0x{posBeforeElement:X} (element consumed 0 bytes)";
                                break;
                            }

                            // 0 バイトで成功する要素が壊れた要素数だけ続くと終わらないので、連続した数で打ち切る（REQ-197）。
                            // 要素ごとの seek は位置が要素ごとに決まるので対象外
                            if (!perElementSeek && context.Position == posBeforeElement)
                            {
                                if (++consecutiveZeroLength >= _maxZeroLengthElements)
                                {
                                    truncated = true;
                                    truncationReason = $"{_maxZeroLengthElements} consecutive elements consumed 0 bytes at offset 0x{posBeforeElement:X}";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveZeroLength = 0;
                            }
                        }
                    }
                    finally
                    {
                        // 境界の外への seek で広げたスコープを戻す（REQ-191）
                        if (elementScopeDepth is { } depth)
                            context.PopScopesTo(depth);
                    }

                    if (context.IsEof)
                        break;
                    idx++;
                }
                break;
            }

            case RepeatMode.While whileMode:
            {
                var idx = 0;
                while (ExpressionEvaluator.EvaluateAsBool(whileMode.Condition, context))
                {
                    // repeat_max ガード
                    if (repeatMax.HasValue && idx >= repeatMax.Value)
                    {
                        truncated = true;
                        truncationReason = $"repeat_max ({repeatMax.Value}) reached";
                        break;
                    }

                    context.SetVariable("_index", (long)idx);

                    int? elementSavedPos = null;
                    int? elementScopeDepth = null;
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                        elementScopeDepth = EnterSeekBoundary(context, seekOffset);
                    }

                    try
                    {
                        if (structAlign is { } sa && idx > 0 && !perElementSeek)
                            context.AlignTo(sa);

                        if (elementResyncMarker is not null)
                        {
                            if (!TryDecodeElementWithRecovery(singleField, format, context, elements, elementSize, elementResyncMarker, field.Type.ToString()))
                                break;
                            if (elements.Count > 0 && elements[^1] is DecodedError)
                            {
                                consecutiveErrors++;
                                if (repeatErrorLimit.HasValue && consecutiveErrors >= repeatErrorLimit.Value)
                                {
                                    truncated = true;
                                    truncationReason = $"repeat_error_limit ({repeatErrorLimit.Value}) consecutive errors";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveErrors = 0;
                            }
                        }
                        else
                        {
                            var posBeforeElement = context.Position;
                            var element = DecodeElementWithScope(singleField, format, context, elementSize);
                            elements.Add(element);
                            consecutiveErrors = 0;

                            if (element is DecodedStruct)
                                PromoteDecodedValues(element, context);
                            SetPrevVariable(element, context);

                            // エラー継続モードで位置が進まないエラー要素は打ち切る（while 条件が真のままだと無限ループになる）
                            if (!perElementSeek && context.Position == posBeforeElement && ContainsError(element))
                            {
                                truncated = true;
                                truncationReason = $"no progress at offset 0x{posBeforeElement:X} (element consumed 0 bytes)";
                                break;
                            }

                            // 0 バイトで成功する要素が壊れた要素数だけ続くと終わらないので、連続した数で打ち切る（REQ-197）。
                            // 要素ごとの seek は位置が要素ごとに決まるので対象外
                            if (!perElementSeek && context.Position == posBeforeElement)
                            {
                                if (++consecutiveZeroLength >= _maxZeroLengthElements)
                                {
                                    truncated = true;
                                    truncationReason = $"{_maxZeroLengthElements} consecutive elements consumed 0 bytes at offset 0x{posBeforeElement:X}";
                                    break;
                                }
                            }
                            else
                            {
                                consecutiveZeroLength = 0;
                            }
                        }
                    }
                    finally
                    {
                        // 境界の外への seek で広げたスコープを戻す（REQ-191）
                        if (elementScopeDepth is { } depth)
                            context.PopScopesTo(depth);
                    }

                    if (elementSavedPos is { } epos)
                        context.RestorePosition(epos);

                    idx++;
                }
                break;
            }

            case RepeatMode.LengthPrefixed lp:
            {
                var idx = 0;
                while (!context.IsEof)
                {
                    // repeat_max ガード
                    if (repeatMax.HasValue && idx >= repeatMax.Value)
                    {
                        truncated = true;
                        truncationReason = $"repeat_max ({repeatMax.Value}) reached";
                        break;
                    }

                    context.SetVariable("_index", (long)idx);

                    var prefixValue = ReadLengthPrefix(context, lp.PrefixSize);
                    if (prefixValue == 0)
                        break;
                    var elementOffset = context.Position;
                    var bytes = context.ReadBytes((int)prefixValue);
                    var lpElement = new DecodedBytes
                    {
                        Name = field.Name,
                        Offset = elementOffset,
                        Size = (int)prefixValue,
                        RawBytes = bytes,
                        Description = field.Description,
                        DslType = field.Type,
                    };
                    elements.Add(lpElement);

                    // 前要素参照用に _prev 変数を登録
                    SetPrevVariable(lpElement, context);

                    idx++;
                }
                break;
            }
        }

        // 要素の値（昇格した値を含む）は、size の無い繰り返しと同じく外側のスコープに残す（REQ-190）。
        // _index / _prev はこの繰り返しのものなので捨てる（外側の繰り返しの _index / _prev を上書きしない）
        if (hasBoundarySize && !context.IsBitstreamMode)
            context.PopScopeCarryingVariables("_index", "_prev");

        var array = new DecodedArray
        {
            Name = field.Name,
            Offset = startOffset,
            Size = Math.Max(0, context.Position - startOffset),
            Elements = elements,
            DiffKey = field.DiffKey,
            Truncated = truncated,
            TruncationReason = truncationReason,
            DslType = field.Type,
        };

        // 配列を変数として登録（後続フィールドの式から要素・len/min/max/sum を参照可能にする）。
        // 要素の値の変換は式評価時に行う（NodeValues）
        if (NodeValues.IsValueArray(array))
            context.SetVariable(field.Name, array);

        return array;
    }

    private static void PromoteDecodedValues(DecodedNode node, DecodeContext context)
    {
        switch (node)
        {
            case DecodedInteger di:
                context.SetVariable(di.Name, di.Value);
                break;
            case DecodedString ds:
                context.SetVariable(ds.Name, ds.Value);
                break;
            case DecodedFloat df:
                context.SetVariable(df.Name, df.Value);
                break;
            case DecodedVirtual dv:
                context.SetVariable(dv.Name, dv.Value);
                break;
            case DecodedBitfield bf:
                context.SetVariable(bf.Name, bf.RawValue);
                var fields = bf.Fields;
                for (var i = 0; i < fields.Count; i++)
                    context.SetVariable(fields[i].Name, fields[i].Value);
                break;
            case DecodedStruct st:
                var children = st.Children;
                for (var i = 0; i < children.Count; i++)
                {
                    // 独自のスコープを持つ入れ子の struct の値は昇格しない（外からはメンバーアクセスで引く。REQ-195）。
                    // 要素そのもの（呼び出し元が渡した st）は、独自のスコープでも直下の値を昇格する
                    if (children[i] is DecodedStruct { IsolatedScope: true })
                        continue;
                    PromoteDecodedValues(children[i], context);
                }
                break;
            case DecodedArray arr:
                // スカラー配列は REQ-098 で既に処理済み。struct 配列の内部は走査しない
                break;
        }
    }

    private static void SetPrevVariable(DecodedNode element, DecodeContext context)
    {
        object? prevValue = element switch
        {
            DecodedInteger di => BoxCache.Box(di.Value),
            DecodedString ds => ds.Value,
            DecodedFloat df => df.Value,
            DecodedStruct st => st,
            _ => null,
        };
        if (prevValue is not null)
            context.SetVariable("_prev", prevValue);
    }

    private DecodedNode DecodeElementWithScope(
        FieldDefinition singleField,
        FormatDefinition format,
        DecodeContext context,
        int? elementSize)
    {
        if (elementSize is not { } size || context.IsBitstreamMode)
            return DecodeSingleField(singleField, format, context);

        context.PushScope(size);
        var element = DecodeSingleField(singleField, format, context);
        context.PopScope();
        return element;
    }

    private (DecodedNode element, bool conditionMet) DecodeElementWithScopeAndCondition(
        FieldDefinition singleField,
        FormatDefinition format,
        DecodeContext context,
        int? elementSize,
        Expression condition)
    {
        if (elementSize is not { } size || context.IsBitstreamMode)
        {
            var elem = DecodeSingleField(singleField, format, context);
            return (elem, EvaluateUntilCondition(elem, format, condition, context));
        }

        context.PushScope(size);
        var element = DecodeSingleField(singleField, format, context);
        // repeat_untilの条件はスコープ内で評価（要素内の変数を参照するため）
        var met = EvaluateUntilCondition(element, format, condition, context);
        context.PopScope();
        return (element, met);
    }

    /// <summary>
    /// repeat_until の条件を評価する。要素が独自の変数のスコープを持つ struct（<c>scope: isolated</c>・エンディアンの形・テンプレートの引数）なら、
    /// 要素の値はそのスコープと一緒に消えているので、一時的な変数のスコープに昇格と同じ範囲の値を入れてから評価する（REQ-196）。
    /// それ以外の要素の値は今のスコープに束縛されているので、そのまま評価する（従来どおり）。
    /// </summary>
    private static bool EvaluateUntilCondition(DecodedNode element, FormatDefinition format, Expression condition, DecodeContext context)
    {
        if (element is not DecodedStruct st
            || !format.Structs.TryGetValue(st.StructType, out var structDef)
            || !HasOwnVariableScope(structDef))
            return ExpressionEvaluator.EvaluateAsBool(condition, context);

        context.PushVariableScope();
        try
        {
            PromoteDecodedValues(element, context);
            return ExpressionEvaluator.EvaluateAsBool(condition, context);
        }
        finally
        {
            context.PopScope();
        }
    }

    /// <summary>デコードの間だけ変数を捕まえるスコープを持つ struct か（中の値は struct のデコードの後に見えない）。</summary>
    private static bool HasOwnVariableScope(StructDefinition structDef) =>
        structDef.IsolatedScope
        || structDef.Endianness.HasValue
        || structDef.EndiannessExpression is not null
        || structDef.Parameters.Count > 0;

    /// <summary>
    /// 要素デコードをエラー回復付きで実行する。成功時はtrue、マーカー見つからず終了時はfalse。
    /// </summary>
    private bool TryDecodeElementWithRecovery(
        FieldDefinition singleField,
        FormatDefinition format,
        DecodeContext context,
        List<DecodedNode> elements,
        int? elementSize,
        byte[] resyncMarker,
        string fieldType)
    {
        var savedPos = context.Position; // 要素開始位置を保存
        // 要素内のフィールドエラーを例外として伝搬させるため、一時的にStopモードに切り替える。
        // これにより内部のDecodeFieldのcatch→continueパスを迂回し、要素レベルで一括エラー処理する。
        var previousErrorMode = _errorMode;
        _errorMode = ErrorMode.Stop;
        try
        {
            var element = DecodeElementWithScope(singleField, format, context, elementSize);
            _errorMode = previousErrorMode;
            elements.Add(element);

            if (element is DecodedStruct)
                PromoteDecodedValues(element, context);
            SetPrevVariable(element, context);
            return true;
        }
        catch (Exception ex)
        {
            _errorMode = previousErrorMode;
            // 要素開始位置に復帰してからマーカーを前方スキャンする
            context.Seek(savedPos);
            var errorOffset = savedPos;
            var message = ex is DecodeException dex ? dex.Message : ex.Message;
            _errors!.Add(new DecodeError(message, errorOffset, CurrentPath, fieldType));
            var skipped = TryResyncToMarker(resyncMarker, context);
            elements.Add(new DecodedError
            {
                Name = $"[{elements.Count}]",
                Offset = errorOffset,
                Size = 0,
                SkippedBytes = skipped,
                ErrorMessage = message,
                FieldType = fieldType,
                DslType = singleField.Type,
            });
            return skipped > 0; // マーカー見つからず→ループ終了
        }
    }

    /// <summary>要素自身または直下の子にデコードエラーが含まれるか。</summary>
    /// <summary>
    /// 要素（の子孫）にデコードエラーが含まれるか。直下だけ見ると、入れ子の struct の奥で失敗した要素
    /// （msgpack の map_entry → key → format_byte など）を「正常だが 0 バイト」と誤認し、壊れた count のぶんだけ
    /// 同じ要素を積み続けてしまう（REQ-160 のファズで検出）。位置が進まなかった要素に対してだけ呼ばれるので再帰でよい。
    /// </summary>
    private static bool ContainsError(DecodedNode element)
    {
        switch (element)
        {
            case DecodedError:
                return true;
            case DecodedStruct s:
                for (var i = 0; i < s.Children.Count; i++)
                    if (ContainsError(s.Children[i])) return true;
                return false;
            case DecodedArray a:
                for (var i = 0; i < a.Elements.Count; i++)
                    if (ContainsError(a.Elements[i])) return true;
                return false;
            case DecodedCompressed { DecodedContent: { } content }:
                return ContainsError(content);
            default:
                return false;
        }
    }

    private static bool UsesIterationContext(ExpressionNode node) => node switch
    {
        ExpressionNode.FieldReference fr => fr.FieldName is "_index" or "_prev",
        ExpressionNode.IndexAccess => true,
        ExpressionNode.MemberAccess ma => UsesIterationContext(ma.Object),
        ExpressionNode.ElementAccess => true,
        ExpressionNode.BinaryOp bo => UsesIterationContext(bo.Left) || UsesIterationContext(bo.Right),
        ExpressionNode.UnaryOp uo => UsesIterationContext(uo.Operand),
        ExpressionNode.FunctionCall fc => fc.Arguments.Any(UsesIterationContext),
        ExpressionNode.Conditional c => UsesIterationContext(c.Condition)
            || UsesIterationContext(c.TrueExpr) || UsesIterationContext(c.FalseExpr),
        _ => false,
    };

    private static FieldDefinition WithoutRepeatAndSeek(FieldDefinition field)
    {
        return new FieldDefinition
        {
            Name = field.Name,
            Type = field.Type,
            Size = null,
            SizeExpression = null,
            SizeRemaining = false,
            EnumRef = field.EnumRef,
            FlagsRef = field.FlagsRef,
            StructRef = field.StructRef,
            StructArgs = field.StructArgs,
            Repeat = new RepeatMode.None(),
            SwitchOn = field.SwitchOn,
            SwitchCases = field.SwitchCases,
            SwitchDefault = field.SwitchDefault,
            BitfieldEntries = field.BitfieldEntries,
            Checksum = field.Checksum,
            Expected = field.Expected,
            Condition = field.Condition,
            Description = field.Description,
            Align = field.Align,
            IsPadding = field.IsPadding,
            ElementSize = field.ElementSize,
            ElementSizeExpression = field.ElementSizeExpression,
            ValueExpression = field.ValueExpression,
            SeekExpression = null, // per-element seek はループ側で処理
            SeekBaseExpression = null,
            SeekRestore = false,
            Endianness = field.Endianness,
            ValidationExpression = field.ValidationExpression,
            StringTableRef = field.StringTableRef,
            State = field.State,
            StateIf = field.StateIf,
            StateDefault = field.StateDefault,
        };
    }

    private static FieldDefinition WithoutRepeat(FieldDefinition field)
    {
        return new FieldDefinition
        {
            Name = field.Name,
            Type = field.Type,
            Size = null,
            SizeExpression = null,
            SizeRemaining = false,
            EnumRef = field.EnumRef,
            FlagsRef = field.FlagsRef,
            StructRef = field.StructRef,
            StructArgs = field.StructArgs,
            Repeat = new RepeatMode.None(),
            SwitchOn = field.SwitchOn,
            SwitchCases = field.SwitchCases,
            SwitchDefault = field.SwitchDefault,
            BitfieldEntries = field.BitfieldEntries,
            Checksum = field.Checksum,
            Expected = field.Expected,
            Condition = field.Condition,
            Description = field.Description,
            Align = field.Align,
            IsPadding = field.IsPadding,
            ElementSize = field.ElementSize,
            ElementSizeExpression = field.ElementSizeExpression,
            ValueExpression = field.ValueExpression,
            SeekExpression = field.SeekExpression,
            SeekBaseExpression = field.SeekBaseExpression,
            SeekRestore = field.SeekRestore,
            Endianness = field.Endianness,
            ValidationExpression = field.ValidationExpression,
            StringTableRef = field.StringTableRef,
            State = field.State,
            StateIf = field.StateIf,
            StateDefault = field.StateDefault,
        };
    }

    private static DecodedNode SetPaddingFlag(DecodedNode node) => node switch
    {
        DecodedBytes b => new DecodedBytes { Name = b.Name, Offset = b.Offset, Size = b.Size, RawBytes = b.RawBytes, ValidationPassed = b.ValidationPassed, Description = b.Description, IsPadding = true, Validation = b.Validation, ChecksumValid = b.ChecksumValid, ChecksumExpectedHex = b.ChecksumExpectedHex, ChecksumAlgorithm = b.ChecksumAlgorithm, ChecksumCoverage = b.ChecksumCoverage, DslType = b.DslType },
        DecodedInteger i => new DecodedInteger { Name = i.Name, Offset = i.Offset, Size = i.Size, BitOffset = i.BitOffset, Value = i.Value, EnumLabel = i.EnumLabel, EnumDescription = i.EnumDescription, ChecksumValid = i.ChecksumValid, ChecksumExpected = i.ChecksumExpected, ChecksumAlgorithm = i.ChecksumAlgorithm, ChecksumCoverage = i.ChecksumCoverage, StringTableValue = i.StringTableValue, Description = i.Description, IsPadding = true, Validation = i.Validation, DslType = i.DslType, Endianness = i.Endianness, EnumRef = i.EnumRef },
        DecodedString s => new DecodedString { Name = s.Name, Offset = s.Offset, Size = s.Size, Value = s.Value, Encoding = s.Encoding, Flags = s.Flags, Description = s.Description, IsPadding = true, Validation = s.Validation, DslType = s.DslType },
        DecodedFlags fl => new DecodedFlags { Name = fl.Name, Offset = fl.Offset, Size = fl.Size, RawValue = fl.RawValue, FlagStates = fl.FlagStates, Description = fl.Description, IsPadding = true, Validation = fl.Validation, DslType = fl.DslType },
        _ => node, // struct/array等はパディングとしてマークしない
    };

    private static DecodedNode SetValidation(DecodedNode node, Core.Decoded.ValidationInfo validation) => node switch
    {
        DecodedBytes b => new DecodedBytes { Name = b.Name, Offset = b.Offset, Size = b.Size, RawBytes = b.RawBytes, ValidationPassed = b.ValidationPassed, Description = b.Description, IsPadding = b.IsPadding, Validation = validation, ChecksumValid = b.ChecksumValid, ChecksumExpectedHex = b.ChecksumExpectedHex, ChecksumAlgorithm = b.ChecksumAlgorithm, ChecksumCoverage = b.ChecksumCoverage, DslType = b.DslType },
        DecodedInteger i => new DecodedInteger { Name = i.Name, Offset = i.Offset, Size = i.Size, BitOffset = i.BitOffset, Value = i.Value, EnumLabel = i.EnumLabel, EnumDescription = i.EnumDescription, ChecksumValid = i.ChecksumValid, ChecksumExpected = i.ChecksumExpected, ChecksumAlgorithm = i.ChecksumAlgorithm, ChecksumCoverage = i.ChecksumCoverage, StringTableValue = i.StringTableValue, Description = i.Description, IsPadding = i.IsPadding, Validation = validation, DslType = i.DslType, Endianness = i.Endianness, EnumRef = i.EnumRef },
        DecodedString s => new DecodedString { Name = s.Name, Offset = s.Offset, Size = s.Size, Value = s.Value, Encoding = s.Encoding, Flags = s.Flags, Description = s.Description, IsPadding = s.IsPadding, Validation = validation, DslType = s.DslType },
        DecodedFloat f => new DecodedFloat { Name = f.Name, Offset = f.Offset, Size = f.Size, Value = f.Value, Precision = f.Precision, Description = f.Description, IsPadding = f.IsPadding, Validation = validation, DslType = f.DslType, Endianness = f.Endianness },
        DecodedFlags fl => new DecodedFlags { Name = fl.Name, Offset = fl.Offset, Size = fl.Size, RawValue = fl.RawValue, FlagStates = fl.FlagStates, Description = fl.Description, IsPadding = fl.IsPadding, Validation = validation, DslType = fl.DslType },
        _ => node, // struct/array/bitfield等はバリデーションをサポートしない
    };

    /// <summary>
    /// エラー回復時にフィールドのスキップを試みる。固定サイズ型はスキップ、不明サイズは現位置維持。
    /// resyncMarkerがある場合は前方スキャンしてマーカー位置へSeek。
    /// structAlignがある場合は次のアライメント境界へSeek。
    /// 戻り値はスキップしたバイト数。
    /// </summary>
    private static int TrySkipField(FieldDefinition field, DecodeContext context,
        byte[]? resyncMarker = null, int? structAlign = null)
    {
        var posBeforeSkip = context.Position;

        var knownSize = field.Type switch
        {
            FieldType.UInt8 or FieldType.Int8 => 1,
            FieldType.UInt16 or FieldType.Int16 or FieldType.Float16 => 2,
            FieldType.UInt32 or FieldType.Int32 or FieldType.Float32 => 4,
            FieldType.UInt64 or FieldType.Int64 or FieldType.Float64 => 8,
            _ => (int?)null,
        };

        if (knownSize.HasValue && context.Position + knownSize.Value <= context.DataLength)
        {
            context.Seek(context.Position + knownSize.Value);
        }
        else if (field.Size.HasValue && context.Position + field.Size.Value <= context.DataLength)
        {
            context.Seek(context.Position + field.Size.Value);
        }
        else if (resyncMarker is not null && context.Remaining > 0)
        {
            var remaining = context.SliceOriginal(context.Position, context.Remaining).Span;
            var idx = remaining.IndexOf(resyncMarker.AsSpan());
            // idx==0 は現在位置なのでスキップして前方を探す（無限ループ防止）
            if (idx == 0 && remaining.Length > resyncMarker.Length)
            {
                var further = remaining.Slice(1).IndexOf(resyncMarker.AsSpan());
                idx = further >= 0 ? further + 1 : -1;
            }
            if (idx > 0)
            {
                var skipTo = context.Position + idx;
                context.Seek(skipTo);
            }
        }
        else if (structAlign is { } align && align > 1 && context.Remaining > 0)
        {
            var currentPos = context.Position;
            var nextBoundary = ((currentPos / align) + 1) * align;
            if (nextBoundary <= context.Position + context.Remaining)
                context.Seek(nextBoundary);
        }
        // else: unknown size, stay at current position

        return context.Position - posBeforeSkip;
    }

    /// <summary>
    /// resyncマーカーを前方スキャンし、見つかった位置へSeekする。
    /// 戻り値はスキップしたバイト数。見つからない場合はスコープ終端へ移動。
    /// </summary>
    private static int TryResyncToMarker(byte[] marker, DecodeContext context)
    {
        if (context.Remaining <= 0) return 0;
        var remaining = context.SliceOriginal(context.Position, context.Remaining).Span;
        var idx = remaining.IndexOf(marker.AsSpan());
        // idx==0 は現在位置なのでスキップして前方を探す（無限ループ防止）
        if (idx == 0 && remaining.Length > marker.Length)
        {
            var further = remaining.Slice(1).IndexOf(marker.AsSpan());
            idx = further >= 0 ? further + 1 : -1;
        }
        if (idx > 0)
        {
            var skipped = idx;
            context.Seek(context.Position + idx);
            return skipped;
        }
        // マーカー見つからず → スコープ終端
        var toEnd = context.Remaining;
        context.Seek(context.Position + toEnd);
        return toEnd;
    }

    /// <summary>
    /// 式の評価結果（long）をバイト数・オフセットとして使える int にする。
    /// 単純なキャストだと 0xFFFF_FFFF のような値が負の int に化け、負のサイズのスコープや逆走する位置を生む（REQ-160 のファズで検出）。
    /// </summary>
    private static int ToByteCount(long value, string what, string fieldName)
    {
        if (value < 0 || value > int.MaxValue)
            throw new InvalidOperationException(
                $"{what} of field '{fieldName}' evaluated to {value}, which is outside the supported range 0..{int.MaxValue}");
        return (int)value;
    }

    /// <summary>
    /// seek の行き先が今の境界（最も内側の size 付きのスコープ）の外なら、行き先を含む外側の境界までスコープを広げる（REQ-191）。
    /// 広げたときは、戻すための push 前のスコープの深さを返す（<see cref="DecodeContext.PopScopesTo"/> に渡す）。
    /// </summary>
    private static int? EnterSeekBoundary(DecodeContext context, int target)
    {
        var depth = context.ScopeDepth;
        return context.PushSeekBoundary(target) ? depth : null;
    }

    private static int ResolveSeekOffset(FieldDefinition field, DecodeContext context)
    {
        var seekOffset = ExpressionEvaluator.EvaluateAsLong(field.SeekExpression!, context);
        if (field.SeekBaseExpression is not null)
        {
            var seekBase = ExpressionEvaluator.EvaluateAsLong(field.SeekBaseExpression, context);
            seekOffset = seekBase + seekOffset;
        }
        return ToByteCount(seekOffset, "seek offset", field.Name);
    }

    private int ResolveSize(FieldDefinition field, DecodeContext context)
    {
        if (field.Size.HasValue)
            return field.Size.Value;

        if (field.SizeRemaining)
            return context.Remaining;

        if (field.SizeExpression is not null)
            return ToByteCount(ExpressionEvaluator.EvaluateAsLong(field.SizeExpression, context), "size", field.Name);

        throw new InvalidOperationException($"Field '{field.Name}' has no size specification");
    }

    private int? ResolveElementSize(FieldDefinition field, DecodeContext context)
    {
        if (field.ElementSize.HasValue)
            return field.ElementSize.Value;

        if (field.ElementSizeExpression is not null)
            return ToByteCount(ExpressionEvaluator.EvaluateAsLong(field.ElementSizeExpression, context), "element_size", field.Name);

        return null;
    }

    private static long ReadLengthPrefix(DecodeContext context, int prefixSize) =>
        prefixSize switch
        {
            1 => context.ReadUInt8(),
            2 => context.ReadUInt16(),
            4 => context.ReadUInt32(),
            3 => ReadUInt24(context),
            _ => throw new InvalidOperationException(
                $"Unsupported length prefix size: {prefixSize}. Must be 1-4."),
        };

    private static long ReadUInt24(DecodeContext context)
    {
        var bytes = context.ReadBytes(3);
        var span = bytes.Span;
        return context.Endianness == Endianness.Big
            ? (span[0] << 16) | (span[1] << 8) | span[2]
            : span[0] | (span[1] << 8) | (span[2] << 16);
    }

    private DecodedBitfield DecodeBitfieldField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        var offset = context.Position;
        var size = ResolveSize(field, context);

        long rawValue = size switch
        {
            1 => context.ReadUInt8(),
            2 => context.ReadUInt16(),
            4 => context.ReadUInt32(),
            8 => (long)context.ReadUInt64(),
            >= 3 and <= 7 => ReadBitfieldBytes(context, size),
            _ => throw new InvalidOperationException(
                $"Bitfield size must be 1-8 bytes, got {size}"),
        };

        context.SetVariable(field.Name, rawValue);

        var fields = new List<BitfieldValue>();
        if (field.BitfieldEntries is not null)
        {
            foreach (var entry in field.BitfieldEntries)
            {
                var bitWidth = entry.BitHigh - entry.BitLow + 1;
                // 64 ビットの幅は 1L << 64 が 1 に戻る（シフト量は 64 で割った余り）ので全ビット。上位ビットを符号拡張しないよう論理シフト
                var mask = bitWidth >= 64 ? -1L : (1L << bitWidth) - 1;
                var value = (rawValue >>> entry.BitLow) & mask;

                string? enumLabel = null;
                string? enumDesc = null;
                if (entry.EnumRef is not null &&
                    format.Enums.TryGetValue(entry.EnumRef, out var enumDef))
                {
                    var enumEntry = enumDef.FindByValue(value);
                    if (enumEntry is not null)
                    {
                        enumLabel = enumEntry.Label;
                        enumDesc = enumEntry.Description;
                    }
                }

                context.SetVariable(entry.Name, value);

                fields.Add(new BitfieldValue(
                    entry.Name, entry.BitHigh, entry.BitLow,
                    value, enumLabel, enumDesc));
            }
        }

        return new DecodedBitfield
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            RawValue = rawValue,
            Fields = fields,
            Description = field.Description,
            DslType = field.Type,
        };
    }

    private static long ReadBitfieldBytes(DecodeContext context, int size)
    {
        var bytes = context.ReadBytes(size);
        long value = 0;
        if (context.Endianness == Endianness.Big)
        {
            for (int i = 0; i < size; i++)
                value = (value << 8) | bytes.Span[i];
        }
        else
        {
            for (int i = size - 1; i >= 0; i--)
                value = (value << 8) | bytes.Span[i];
        }
        return value;
    }

    private DecodedVirtual DecodeVirtualField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        if (field.ValueExpression is null)
            throw new InvalidOperationException($"Virtual field '{field.Name}' has no value expression");

        var value = ExpressionEvaluator.Evaluate(field.ValueExpression, context);
        context.SetVariable(field.Name, value);

        // 整数の結果にだけ enum のラベルを付ける（真偽値・文字列には付けない。REQ-186）
        EnumEntry? enumEntry = null;
        if (field.EnumRef is not null && value is long or int
            && format.Enums.TryGetValue(field.EnumRef, out var enumDef))
            enumEntry = enumDef.FindByValue(Convert.ToInt64(value));

        return new DecodedVirtual
        {
            Name = field.Name,
            Offset = context.Position,
            Size = 0,
            Value = value,
            EnumLabel = enumEntry?.Label,
            EnumDescription = enumEntry?.Description,
            Description = field.Description,
            // padding: true の virtual は作業用の値としてツリーに出さない（REQ-192）
            IsPadding = field.IsPadding,
            BitOffset = context.IsBitstreamMode ? context.CurrentBitOffset : null,
            DslType = field.Type,
        };
    }

    private DecodedInteger DecodeVariableLengthIntegerField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        var offset = context.Position;
        long value = field.Type switch
        {
            FieldType.ULeb128 => (long)context.ReadULeb128(),
            FieldType.SLeb128 => context.ReadSLeb128(),
            FieldType.Vlq => (long)context.ReadVlq(),
            _ => throw new InvalidOperationException($"Not a variable-length integer type: {field.Type}"),
        };

        var size = context.Position - offset;
        context.SetVariable(field.Name, value);

        string? enumLabel = null;
        string? enumDesc = null;
        if (field.EnumRef is not null && format.Enums.TryGetValue(field.EnumRef, out var enumDef))
        {
            var entry = enumDef.FindByValue(value);
            if (entry is not null)
            {
                enumLabel = entry.Label;
                enumDesc = entry.Description;
            }
        }

        string? stringTableValue = null;
        if (field.StringTableRef is not null)
        {
            stringTableValue = context.LookupString(field.StringTableRef, (int)value);
        }

        return new DecodedInteger
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            EnumLabel = enumLabel,
            EnumDescription = enumDesc,
            StringTableValue = stringTableValue,
            Description = field.Description,
            DslType = field.Type,
            EnumRef = field.EnumRef,
        };
    }

    /// <summary>
    /// <c>fields:</c> 指定のチェックサム算出範囲。兄弟ノードの [Offset, Offset+Size) をそのまま使う。
    /// 見つからない名前は無視する（従来どおり）。
    /// </summary>
    private static List<ByteRange> ResolveChecksumFieldRanges(
        ChecksumSpec spec, IReadOnlyList<DecodedNode> siblings)
    {
        var ranges = new List<ByteRange>();
        foreach (var fieldName in spec.FieldNames)
        {
            var sibling = siblings.FirstOrDefault(n => n.Name == fieldName);
            if (sibling is null)
                continue;
            ranges.Add(new ByteRange(sibling.Offset, sibling.Size));
        }
        return ranges;
    }

    /// <summary><c>range:</c> / <c>ranges:</c> 指定の算出範囲。式を評価し、データ境界を超えていれば例外。</summary>
    private static List<ByteRange> ResolveChecksumExplicitRanges(
        IReadOnlyList<ChecksumRange> ranges, DecodeContext context)
    {
        var result = new List<ByteRange>(ranges.Count);
        foreach (var range in ranges)
        {
            var offsetValue = ExpressionEvaluator.EvaluateAsLong(range.OffsetExpression, context);
            var sizeValue = ExpressionEvaluator.EvaluateAsLong(range.SizeExpression, context);
            // long のまま検査する（int にキャストすると巨大な値が負に化けて検査をすり抜ける）
            if (offsetValue < 0 || sizeValue < 0 || offsetValue + sizeValue > context.DataLength)
                throw new DecodeException(
                    $"Checksum range [{offsetValue}..{offsetValue + sizeValue}) exceeds data boundary (data length: {context.DataLength})",
                    Math.Clamp(offsetValue, 0, context.DataLength), "(checksum)", "checksum");
            var offset = (int)offsetValue;
            var size = (int)sizeValue;

            result.Add(new ByteRange(offset, size));
        }
        return result;
    }

    /// <summary>チェックサム仕様から算出範囲を解決し、対象バイト列も返す。<c>exclude_self</c> はバイト列側でゼロ埋めする。</summary>
    private static (byte[] data, IReadOnlyList<ByteRange> coverage) CollectChecksumDataForSpec(
        ChecksumSpec spec, IReadOnlyList<DecodedNode> siblings,
        DecodeContext context,
        int checksumFieldOffset, int checksumFieldSize)
    {
        List<ByteRange> ranges;
        bool excludeSelf;
        if (spec.Range is not null)
        {
            ranges = ResolveChecksumExplicitRanges([spec.Range], context);
            excludeSelf = spec.ExcludeSelf;
        }
        else if (spec.Ranges is { Count: > 0 })
        {
            ranges = ResolveChecksumExplicitRanges(spec.Ranges, context);
            excludeSelf = spec.ExcludeSelf;
        }
        else
        {
            ranges = ResolveChecksumFieldRanges(spec, siblings);
            excludeSelf = false;
        }

        using var ms = new MemoryStream();
        foreach (var range in ranges)
        {
            var slice = context.SliceOriginal((int)range.Offset, (int)range.Size).ToArray();
            if (excludeSelf)
                ZeroFillOverlap(slice, (int)range.Offset, (int)range.Size, checksumFieldOffset, checksumFieldSize);
            ms.Write(slice);
        }
        return (ms.ToArray(), ranges);
    }

    private static void ZeroFillOverlap(
        byte[] slice, int sliceStart, int sliceSize,
        int fieldOffset, int fieldSize)
    {
        var overlapStart = Math.Max(sliceStart, fieldOffset);
        var overlapEnd = Math.Min(sliceStart + sliceSize, fieldOffset + fieldSize);
        if (overlapStart >= overlapEnd)
            return;

        var localStart = overlapStart - sliceStart;
        var localEnd = overlapEnd - sliceStart;
        Array.Clear(slice, localStart, localEnd - localStart);
    }

    /// <summary>
    /// 整数系チェックサムを検証する。計算の実装が無いアルゴリズム（未知の名前や、整数フィールドに指定したハッシュ系）は
    /// 検証しない（valid = null。以前は「常に一致」として ✓ を出していた。REQ-187）。
    /// </summary>
    private static (bool? valid, long? expected, IReadOnlyList<ByteRange> coverage) VerifyChecksum(
        ChecksumSpec spec, long actualValue,
        IReadOnlyList<DecodedNode> siblings, DecodeContext context,
        int checksumFieldOffset = 0, int checksumFieldSize = 0)
    {
        var (data, coverage) = CollectChecksumDataForSpec(spec, siblings, context,
            checksumFieldOffset, checksumFieldSize);
        if (!ChecksumCalculators.Integer.TryGetValue(spec.Algorithm, out var compute))
            return (null, null, coverage);

        var computed = compute(data);
        var valid = actualValue == computed;
        return (valid, valid ? null : computed, coverage);
    }

    /// <summary>ハッシュ系チェックサムを検証する。計算の実装が無ければ検証しない（valid = null）。</summary>
    private static (bool? valid, string? expectedHex, IReadOnlyList<ByteRange> coverage) VerifyHashChecksum(
        ChecksumSpec spec, ReadOnlyMemory<byte> actualBytes,
        IReadOnlyList<DecodedNode> siblings, DecodeContext context,
        int checksumFieldOffset = 0, int checksumFieldSize = 0)
    {
        var (data, coverage) = CollectChecksumDataForSpec(spec, siblings, context,
            checksumFieldOffset, checksumFieldSize);
        if (!ChecksumCalculators.Hash.TryGetValue(spec.Algorithm, out var compute))
            return (null, null, coverage);

        var computed = compute(data);
        var valid = actualBytes.Span.SequenceEqual(computed);
        return (valid, valid ? null : Convert.ToHexString(computed).ToLowerInvariant(), coverage);
    }

    /// <summary>
    /// テンプレートパラメータ定義と引数をマッチし、解決済み引数辞書を返す。
    /// </summary>
    private static Dictionary<string, long> ResolveTemplateArgs(
        StructDefinition structDef,
        IReadOnlyList<StructArgument>? args,
        DecodeContext context)
    {
        var resolved = new Dictionary<string, long>();
        var parameters = structDef.Parameters;

        if (args is not null && args.Count > 0)
        {
            // 名前付き引数かどうかを判定（最初の引数にParameterNameがあれば名前付き）
            var isNamed = args[0].ParameterName is not null;

            if (isNamed)
            {
                // 名前付き引数: 名前でマッチ
                foreach (var arg in args)
                {
                    if (arg.ParameterName is null)
                        throw new InvalidOperationException(
                            $"Cannot mix positional and named arguments in template struct '{structDef.Name}'");

                    var value = ResolveArgValue(arg, context);
                    resolved[arg.ParameterName] = value;
                }
            }
            else
            {
                // 位置引数: パラメータ定義順にマッチ
                for (var i = 0; i < args.Count; i++)
                {
                    if (i >= parameters.Count)
                        throw new InvalidOperationException(
                            $"Too many arguments for template struct '{structDef.Name}': " +
                            $"expected {parameters.Count}, got {args.Count}");

                    var value = ResolveArgValue(args[i], context);
                    resolved[parameters[i].Name] = value;
                }
            }
        }

        // デフォルト値の適用
        foreach (var param in parameters)
        {
            if (!resolved.ContainsKey(param.Name))
            {
                if (param.DefaultValue.HasValue)
                {
                    resolved[param.Name] = param.DefaultValue.Value;
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Required template parameter '{param.Name}' not specified for struct '{structDef.Name}'");
                }
            }
        }

        return resolved;
    }

    private static long ResolveArgValue(StructArgument arg, DecodeContext context)
    {
        if (arg.Value.HasValue)
            return arg.Value.Value;

        if (arg.Expression is not null)
            return ExpressionEvaluator.EvaluateAsLong(arg.Expression, context);

        throw new InvalidOperationException("Struct argument has neither value nor expression");
    }

    /// <summary>
    /// 解決済みテンプレート引数をコンテキストの変数として登録する。
    /// </summary>
    private static void BindTemplateArgs(Dictionary<string, long> resolvedArgs, DecodeContext context)
    {
        foreach (var (name, value) in resolvedArgs)
        {
            context.SetVariable(name, value);
        }
    }

    private static bool ValuesEqual(object left, object right)
    {
        if (left is string ls && right is string rs)
            return ls == rs;

        try
        {
            return Convert.ToInt64(left) == Convert.ToInt64(right);
        }
        catch
        {
            return Equals(left, right);
        }
    }

    private static IReadOnlyList<FlagState> DecodeFlagValues(uint rawValue, FlagsDefinition flagsDef)
    {
        var states = new List<FlagState>();
        foreach (var flagField in flagsDef.Fields)
        {
            var mask = ((1u << flagField.BitSize) - 1) << flagField.BitPosition;
            var isSet = (rawValue & mask) != 0;
            var meaning = isSet ? flagField.SetMeaning : flagField.ClearMeaning;
            states.Add(new FlagState(flagField.Name, isSet, flagField.BitPosition, meaning));
        }
        return states;
    }
}
