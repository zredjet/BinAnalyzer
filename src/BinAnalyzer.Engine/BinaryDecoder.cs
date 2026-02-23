using System.IO.Compression;
using System.Text;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;

#pragma warning disable CA5350 // SHA-1 is used for checksum verification, not security
#pragma warning disable CA5351 // MD5 is used for checksum verification, not security

namespace BinAnalyzer.Engine;

public sealed class BinaryDecoder : IBinaryDecoder
{
    private readonly Stack<string> _pathStack = new();
    private ErrorMode _errorMode = ErrorMode.Stop;
    private List<DecodeError>? _errors;
    private int? _globalMaxRepeat;

    private string CurrentPath => _pathStack.Count == 0 ? "(root)" : string.Join(".", _pathStack.Reverse());

    public DecodedStruct Decode(ReadOnlyMemory<byte> data, FormatDefinition format, DecodeOptions? options = null)
    {
        _pathStack.Clear();
        _errorMode = ErrorMode.Stop;
        _errors = null;
        _globalMaxRepeat = options?.MaxRepeat;
        var context = new DecodeContext(data, format.Endianness);
        var rootStruct = format.Structs[format.RootStruct];
        return DecodeStruct(rootStruct, format, context, format.Name);
    }

    public DecodeResult DecodeWithRecovery(ReadOnlyMemory<byte> data, FormatDefinition format, ErrorMode errorMode, DecodeOptions? options = null)
    {
        _pathStack.Clear();
        _errorMode = errorMode;
        _errors = errorMode == ErrorMode.Continue ? new List<DecodeError>() : null;
        _globalMaxRepeat = options?.MaxRepeat;
        var context = new DecodeContext(data, format.Endianness);
        var rootStruct = format.Structs[format.RootStruct];
        var root = DecodeStruct(rootStruct, format, context, format.Name);
        return new DecodeResult
        {
            Root = root,
            Errors = _errors?.AsReadOnly() ?? (IReadOnlyList<DecodeError>)Array.Empty<DecodeError>(),
        };
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

    private DecodedStruct DecodeStruct(
        StructDefinition structDef,
        FormatDefinition format,
        DecodeContext context,
        string name)
    {
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
        var children = new List<DecodedNode>();

        var isBitstream = structDef.IsBitstream;
        if (isBitstream)
            context.EnterBitstreamMode(structDef.BitOrder ?? BitOrder.Msb);

        foreach (var field in structDef.Fields)
        {
            var node = DecodeField(field, format, context, children, structDef);
            if (node is not null)
                children.Add(node);
        }

        if (isBitstream)
            context.ExitBitstreamMode();

        if (hasDynamicEndianness)
            context.PopScope();
        if (hasEndiannessOverride)
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
            Size = context.Position - startOffset,
            Children = children,
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
            if (field.SeekExpression is not null && !perElementSeek)
            {
                var seekOffset = ResolveSeekOffset(field, context);
                if (field.SeekRestore)
                    savedPosition = context.SavePosition();
                context.Seek(seekOffset);
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
                if (savedPosition is { } pos)
                    context.RestorePosition(pos);
            }
        }
        catch (DecodeException dex)
        {
            if (_errorMode == ErrorMode.Continue)
            {
                _errors!.Add(new DecodeError(dex.Message, dex.Offset, dex.FieldPath, dex.FieldType));
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
            context.PushEndiannessScope(field.Endianness!.Value);

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
            FieldType.Float32 or FieldType.Float64 => DecodeFloatField(field, context),
            FieldType.Zlib or FieldType.Deflate or FieldType.Gzip
                or FieldType.Bzip2 or FieldType.Lzma or FieldType.Zstd or FieldType.Lz4
                => DecodeCompressedField(field, format, context),
            FieldType.Struct => DecodeStructField(field, format, context),
            FieldType.Switch => DecodeSwitchField(field, format, context),
            FieldType.Bitfield => DecodeBitfieldField(field, format, context),
            FieldType.Virtual => DecodeVirtualField(field, context),
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
        if (field.Checksum is not null && siblings is not null)
        {
            var (valid, expected) = VerifyChecksum(field.Checksum, value, siblings, context,
                checksumFieldOffset: offset, checksumFieldSize: size);
            checksumValid = valid;
            checksumExpected = expected;
            checksumAlgorithm = field.Checksum.Algorithm;
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
            StringTableValue = stringTableValue,
            Description = field.Description,
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
        if (field.Checksum is not null && siblings is not null)
        {
            var (valid, expected) = VerifyChecksum(field.Checksum, rawValue, siblings, context,
                checksumFieldOffset: byteOffset, checksumFieldSize: (bitSize + 7) / 8);
            checksumValid = valid;
            checksumExpected = expected;
            checksumAlgorithm = field.Checksum.Algorithm;
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
            StringTableValue = stringTableValue,
            Description = field.Description,
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
        if (field.Checksum is not null && siblings is not null
            && ChecksumAlgorithms.IsHashAlgorithm(field.Checksum.Algorithm))
        {
            var (v, hex) = VerifyHashChecksum(field.Checksum, bytes, siblings, context,
                checksumFieldOffset: offset, checksumFieldSize: size);
            checksumValid = v;
            checksumExpectedHex = hex;
            checksumAlgorithm = field.Checksum.Algorithm;
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
        };
    }

    private DecodedFloat DecodeFloatField(
        FieldDefinition field,
        DecodeContext context)
    {
        var offset = context.Position;
        var isSingle = field.Type == FieldType.Float32;
        double value = isSingle ? context.ReadFloat32() : context.ReadFloat64();
        var size = context.Position - offset;
        context.SetVariable(field.Name, value);

        return new DecodedFloat
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            Value = value,
            IsSinglePrecision = isSingle,
            Description = field.Description,
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
            decodedContent = DecodeStruct(structDef, format, innerContext, field.Name);
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
            result = DecodeStruct(structDef, format, context, field.Name);
            context.PopScope();
        }
        else if (resolvedArgs is not null)
        {
            context.PushVariableScope();
            BindTemplateArgs(resolvedArgs, context);
            result = DecodeStruct(structDef, format, context, field.Name);
            context.PopScope();
        }
        else
        {
            result = DecodeStruct(structDef, format, context, field.Name);
        }

        // メンバーアクセス用に構造体を辞書として登録
        context.SetVariable(field.Name, BuildStructDictionary(result));
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
            foreach (var switchCase in field.SwitchCases)
            {
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
            switchResult = DecodeStruct(structDef, format, context, field.Name);
            context.PopScope();
        }
        else
        {
            switchResult = DecodeStruct(structDef, format, context, field.Name);
        }

        // メンバーアクセス用に構造体を辞書として登録
        context.SetVariable(field.Name, BuildStructDictionary(switchResult));
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
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                    }

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
                        var element = DecodeElementWithScope(singleField, format, context, elementSize);
                        elements.Add(element);
                        consecutiveErrors = 0;

                        if (element is DecodedStruct)
                            PromoteDecodedValues(element, context);
                        SetPrevVariable(element, context);
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
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                    }

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
                        var element = DecodeElementWithScope(singleField, format, context, elementSize);
                        elements.Add(element);
                        consecutiveErrors = 0;

                        if (element is DecodedStruct)
                            PromoteDecodedValues(element, context);
                        SetPrevVariable(element, context);
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
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                    }

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
                    if (perElementSeek && field.SeekExpression is not null)
                    {
                        var seekOffset = ResolveSeekOffset(field, context);
                        if (field.SeekRestore)
                            elementSavedPos = context.SavePosition();
                        context.Seek(seekOffset);
                    }

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
                        var element = DecodeElementWithScope(singleField, format, context, elementSize);
                        elements.Add(element);
                        consecutiveErrors = 0;

                        if (element is DecodedStruct)
                            PromoteDecodedValues(element, context);
                        SetPrevVariable(element, context);
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
                    };
                    elements.Add(lpElement);

                    // 前要素参照用に _prev 変数を登録
                    SetPrevVariable(lpElement, context);

                    idx++;
                }
                break;
            }
        }

        if (hasBoundarySize && !context.IsBitstreamMode)
            context.PopScope();

        // 配列要素値を変数として登録（後続フィールドの式から参照可能にする）
        var elementValues = new List<object>();
        foreach (var element in elements)
        {
            object? val = element switch
            {
                DecodedInteger di => (object)di.Value,
                DecodedString ds => ds.Value,
                DecodedFloat df => df.Value,
                DecodedStruct st => BuildStructDictionary(st),
                _ => null,
            };
            if (val is not null)
                elementValues.Add(val);
        }
        if (elementValues.Count == elements.Count && elements.Count > 0)
            context.SetVariable(field.Name, elementValues);

        return new DecodedArray
        {
            Name = field.Name,
            Offset = startOffset,
            Size = context.Position - startOffset,
            Elements = elements,
            DiffKey = field.DiffKey,
            Truncated = truncated,
            TruncationReason = truncationReason,
        };
    }

    private static Dictionary<string, object> BuildStructDictionary(DecodedStruct st)
    {
        var dict = new Dictionary<string, object>();
        foreach (var child in st.Children)
        {
            switch (child)
            {
                case DecodedInteger di:
                    dict[di.Name] = di.Value;
                    break;
                case DecodedString ds:
                    dict[ds.Name] = ds.Value;
                    break;
                case DecodedFloat df:
                    dict[df.Name] = df.Value;
                    break;
                case DecodedVirtual dv:
                    dict[dv.Name] = dv.Value;
                    break;
                case DecodedBitfield bf:
                    dict[bf.Name] = bf.RawValue;
                    foreach (var f in bf.Fields)
                        dict[f.Name] = f.Value;
                    break;
                case DecodedStruct nested:
                    dict[nested.Name] = BuildStructDictionary(nested);
                    break;
                case DecodedArray arr:
                    var arrayValues = BuildArrayValues(arr);
                    if (arrayValues is not null)
                        dict[arr.Name] = arrayValues;
                    break;
            }
        }
        return dict;
    }

    private static List<object>? BuildArrayValues(DecodedArray arr)
    {
        var values = new List<object>();
        foreach (var elem in arr.Elements)
        {
            object? val = elem switch
            {
                DecodedInteger di => (object)di.Value,
                DecodedString ds => ds.Value,
                DecodedFloat df => df.Value,
                DecodedStruct s => BuildStructDictionary(s),
                _ => null,
            };
            if (val is null) return null;
            values.Add(val);
        }
        return values.Count > 0 ? values : null;
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
                foreach (var field in bf.Fields)
                    context.SetVariable(field.Name, field.Value);
                break;
            case DecodedStruct st:
                foreach (var child in st.Children)
                    PromoteDecodedValues(child, context);
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
            DecodedInteger di => (object)di.Value,
            DecodedString ds => ds.Value,
            DecodedFloat df => df.Value,
            DecodedStruct st => BuildStructDictionary(st),
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
            return (elem, ExpressionEvaluator.EvaluateAsBool(condition, context));
        }

        context.PushScope(size);
        var element = DecodeSingleField(singleField, format, context);
        // repeat_untilの条件はスコープ内で評価（要素内の変数を参照するため）
        var met = ExpressionEvaluator.EvaluateAsBool(condition, context);
        context.PopScope();
        return (element, met);
    }

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
            });
            return skipped > 0; // マーカー見つからず→ループ終了
        }
    }

    private static bool UsesIterationContext(ExpressionNode node) => node switch
    {
        ExpressionNode.FieldReference fr => fr.FieldName is "_index" or "_prev",
        ExpressionNode.IndexAccess => true,
        ExpressionNode.MemberAccess ma => UsesIterationContext(ma.Object),
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
        DecodedBytes b => new DecodedBytes { Name = b.Name, Offset = b.Offset, Size = b.Size, RawBytes = b.RawBytes, ValidationPassed = b.ValidationPassed, Description = b.Description, IsPadding = true, Validation = b.Validation, ChecksumValid = b.ChecksumValid, ChecksumExpectedHex = b.ChecksumExpectedHex, ChecksumAlgorithm = b.ChecksumAlgorithm },
        DecodedInteger i => new DecodedInteger { Name = i.Name, Offset = i.Offset, Size = i.Size, Value = i.Value, EnumLabel = i.EnumLabel, EnumDescription = i.EnumDescription, ChecksumValid = i.ChecksumValid, ChecksumExpected = i.ChecksumExpected, ChecksumAlgorithm = i.ChecksumAlgorithm, StringTableValue = i.StringTableValue, Description = i.Description, IsPadding = true, Validation = i.Validation },
        DecodedString s => new DecodedString { Name = s.Name, Offset = s.Offset, Size = s.Size, Value = s.Value, Encoding = s.Encoding, Flags = s.Flags, Description = s.Description, IsPadding = true, Validation = s.Validation },
        DecodedFlags fl => new DecodedFlags { Name = fl.Name, Offset = fl.Offset, Size = fl.Size, RawValue = fl.RawValue, FlagStates = fl.FlagStates, Description = fl.Description, IsPadding = true, Validation = fl.Validation },
        _ => node, // struct/array等はパディングとしてマークしない
    };

    private static DecodedNode SetValidation(DecodedNode node, Core.Decoded.ValidationInfo validation) => node switch
    {
        DecodedBytes b => new DecodedBytes { Name = b.Name, Offset = b.Offset, Size = b.Size, RawBytes = b.RawBytes, ValidationPassed = b.ValidationPassed, Description = b.Description, IsPadding = b.IsPadding, Validation = validation, ChecksumValid = b.ChecksumValid, ChecksumExpectedHex = b.ChecksumExpectedHex, ChecksumAlgorithm = b.ChecksumAlgorithm },
        DecodedInteger i => new DecodedInteger { Name = i.Name, Offset = i.Offset, Size = i.Size, Value = i.Value, EnumLabel = i.EnumLabel, EnumDescription = i.EnumDescription, ChecksumValid = i.ChecksumValid, ChecksumExpected = i.ChecksumExpected, ChecksumAlgorithm = i.ChecksumAlgorithm, StringTableValue = i.StringTableValue, Description = i.Description, IsPadding = i.IsPadding, Validation = validation },
        DecodedString s => new DecodedString { Name = s.Name, Offset = s.Offset, Size = s.Size, Value = s.Value, Encoding = s.Encoding, Flags = s.Flags, Description = s.Description, IsPadding = s.IsPadding, Validation = validation },
        DecodedFloat f => new DecodedFloat { Name = f.Name, Offset = f.Offset, Size = f.Size, Value = f.Value, IsSinglePrecision = f.IsSinglePrecision, Description = f.Description, IsPadding = f.IsPadding, Validation = validation },
        DecodedFlags fl => new DecodedFlags { Name = fl.Name, Offset = fl.Offset, Size = fl.Size, RawValue = fl.RawValue, FlagStates = fl.FlagStates, Description = fl.Description, IsPadding = fl.IsPadding, Validation = validation },
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
            FieldType.UInt16 or FieldType.Int16 => 2,
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

    private static int ResolveSeekOffset(FieldDefinition field, DecodeContext context)
    {
        var seekOffset = (int)ExpressionEvaluator.EvaluateAsLong(field.SeekExpression!, context);
        if (field.SeekBaseExpression is not null)
        {
            var seekBase = (int)ExpressionEvaluator.EvaluateAsLong(field.SeekBaseExpression, context);
            seekOffset = seekBase + seekOffset;
        }
        return seekOffset;
    }

    private int ResolveSize(FieldDefinition field, DecodeContext context)
    {
        if (field.Size.HasValue)
            return field.Size.Value;

        if (field.SizeRemaining)
            return context.Remaining;

        if (field.SizeExpression is not null)
            return (int)ExpressionEvaluator.EvaluateAsLong(field.SizeExpression, context);

        throw new InvalidOperationException($"Field '{field.Name}' has no size specification");
    }

    private int? ResolveElementSize(FieldDefinition field, DecodeContext context)
    {
        if (field.ElementSize.HasValue)
            return field.ElementSize.Value;

        if (field.ElementSizeExpression is not null)
            return (int)ExpressionEvaluator.EvaluateAsLong(field.ElementSizeExpression, context);

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
                var mask = (1L << bitWidth) - 1;
                var value = (rawValue >> entry.BitLow) & mask;

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
        DecodeContext context)
    {
        if (field.ValueExpression is null)
            throw new InvalidOperationException($"Virtual field '{field.Name}' has no value expression");

        var value = ExpressionEvaluator.Evaluate(field.ValueExpression, context);
        context.SetVariable(field.Name, value);

        return new DecodedVirtual
        {
            Name = field.Name,
            Offset = context.Position,
            Size = 0,
            Value = value,
            Description = field.Description,
            BitOffset = context.IsBitstreamMode ? context.CurrentBitOffset : null,
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
        };
    }

    private static byte[] CollectChecksumData(
        ChecksumSpec spec, IReadOnlyList<DecodedNode> siblings, DecodeContext context)
    {
        using var ms = new MemoryStream();
        foreach (var fieldName in spec.FieldNames)
        {
            var sibling = siblings.FirstOrDefault(n => n.Name == fieldName);
            if (sibling is null)
                continue;

            var slice = context.SliceOriginal((int)sibling.Offset, (int)sibling.Size);
            ms.Write(slice.Span);
        }
        return ms.ToArray();
    }

    private static byte[] CollectChecksumRangeData(
        IReadOnlyList<ChecksumRange> ranges, bool excludeSelf,
        int checksumFieldOffset, int checksumFieldSize,
        DecodeContext context)
    {
        using var ms = new MemoryStream();
        foreach (var range in ranges)
        {
            var offset = (int)ExpressionEvaluator.EvaluateAsLong(range.OffsetExpression, context);
            var size = (int)ExpressionEvaluator.EvaluateAsLong(range.SizeExpression, context);

            if (offset < 0 || offset + size > context.DataLength)
                throw new DecodeException(
                    $"Checksum range [{offset}..{offset + size}) exceeds data boundary (data length: {context.DataLength})",
                    offset, "(checksum)", "checksum");

            var slice = context.SliceOriginal(offset, size).ToArray();

            if (excludeSelf)
                ZeroFillOverlap(slice, offset, size, checksumFieldOffset, checksumFieldSize);

            ms.Write(slice);
        }
        return ms.ToArray();
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

    private static byte[] CollectChecksumDataForSpec(
        ChecksumSpec spec, IReadOnlyList<DecodedNode> siblings,
        DecodeContext context,
        int checksumFieldOffset, int checksumFieldSize)
    {
        if (spec.Range is not null)
            return CollectChecksumRangeData(
                [spec.Range], spec.ExcludeSelf,
                checksumFieldOffset, checksumFieldSize, context);

        if (spec.Ranges is { Count: > 0 })
            return CollectChecksumRangeData(
                spec.Ranges, spec.ExcludeSelf,
                checksumFieldOffset, checksumFieldSize, context);

        return CollectChecksumData(spec, siblings, context);
    }

    private static (bool valid, long? expected) VerifyChecksum(
        ChecksumSpec spec, long actualValue,
        IReadOnlyList<DecodedNode> siblings, DecodeContext context,
        int checksumFieldOffset = 0, int checksumFieldSize = 0)
    {
        var data = CollectChecksumDataForSpec(spec, siblings, context,
            checksumFieldOffset, checksumFieldSize);

        long computed = spec.Algorithm.ToLowerInvariant() switch
        {
            "crc32" => Crc32Calculator.Compute(data),
            "crc16-ccitt" => Crc16Calculator.ComputeCcitt(data),
            "crc16-ibm" => Crc16Calculator.ComputeIbm(data),
            "adler32" => Adler32Calculator.Compute(data),
            "crc8" => Crc8Calculator.ComputeSmbus(data),
            "crc8-maxim" => Crc8Calculator.ComputeMaxim(data),
            "crc8-cdma2000" => Crc8Calculator.ComputeCdma2000(data),
            "crc64-ecma" => (long)Crc64Calculator.ComputeEcma(data),
            "xxhash32" => XxHashCalculator.ComputeXxHash32(data),
            "xxhash64" => (long)XxHashCalculator.ComputeXxHash64(data),
            "fletcher16" => FletcherCalculator.ComputeFletcher16(data),
            "fletcher32" => FletcherCalculator.ComputeFletcher32(data),
            _ => actualValue, // 未知: 常にvalid
        };

        var valid = actualValue == computed;
        return (valid, valid ? null : computed);
    }

    private static (bool valid, string? expectedHex) VerifyHashChecksum(
        ChecksumSpec spec, ReadOnlyMemory<byte> actualBytes,
        IReadOnlyList<DecodedNode> siblings, DecodeContext context,
        int checksumFieldOffset = 0, int checksumFieldSize = 0)
    {
        var data = CollectChecksumDataForSpec(spec, siblings, context,
            checksumFieldOffset, checksumFieldSize);

        byte[] computed = spec.Algorithm.ToLowerInvariant() switch
        {
            "md5" => HashCalculator.ComputeMd5(data),
            "sha1" => HashCalculator.ComputeSha1(data),
            "sha256" => HashCalculator.ComputeSha256(data),
            "sha384" => HashCalculator.ComputeSha384(data),
            "sha512" => HashCalculator.ComputeSha512(data),
            _ => actualBytes.ToArray(), // 未知: 常にvalid
        };

        var valid = actualBytes.Span.SequenceEqual(computed);
        return (valid, valid ? null : Convert.ToHexString(computed).ToLowerInvariant());
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
