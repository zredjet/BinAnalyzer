using System.Text;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;

namespace BinAnalyzer.Engine;

public sealed class BinaryDecoder : IBinaryDecoder
{
    private readonly Stack<string> _pathStack = new();

    private string CurrentPath => _pathStack.Count == 0 ? "(root)" : string.Join(".", _pathStack.Reverse());

    public DecodedStruct Decode(ReadOnlyMemory<byte> data, FormatDefinition format)
    {
        _pathStack.Clear();
        var context = new DecodeContext(data, format.Endianness);
        var rootStruct = format.Structs[format.RootStruct];
        return DecodeStruct(rootStruct, format, context, format.Name);
    }

    private DecodedStruct DecodeStruct(
        StructDefinition structDef,
        FormatDefinition format,
        DecodeContext context,
        string name)
    {
        var startOffset = context.Position;
        var children = new List<DecodedNode>();

        foreach (var field in structDef.Fields)
        {
            var node = DecodeField(field, format, context, children);
            if (node is not null)
                children.Add(node);
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
        IReadOnlyList<DecodedNode>? siblings = null)
    {
        _pathStack.Push(field.Name);
        try
        {
            if (field.Condition is not null)
            {
                if (!ExpressionEvaluator.EvaluateAsBool(field.Condition, context))
                    return null;
            }

            if (field.Repeat is not RepeatMode.None)
                return DecodeRepeatedField(field, format, context);

            return DecodeSingleField(field, format, context, siblings);
        }
        catch (DecodeException)
        {
            throw;
        }
        catch (Exception ex)
        {
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
        return field.Type switch
        {
            FieldType.UInt8 or FieldType.UInt16 or FieldType.UInt32 or FieldType.UInt64
                or FieldType.Int8 or FieldType.Int16 or FieldType.Int32 or FieldType.Int64
                => DecodeIntegerField(field, format, context, siblings),
            FieldType.Bytes => DecodeBytesField(field, context),
            FieldType.Ascii => DecodeAsciiField(field, format, context),
            FieldType.Utf8 => DecodeStringField(field, context, Encoding.UTF8, "utf8"),
            FieldType.Utf16Le => DecodeStringField(field, context, Encoding.Unicode, "utf16le"),
            FieldType.Utf16Be => DecodeStringField(field, context, Encoding.BigEndianUnicode, "utf16be"),
            FieldType.ShiftJis => DecodeStringField(field, context, EncodingHelper.ShiftJis, "sjis"),
            FieldType.Latin1 => DecodeStringField(field, context, Encoding.Latin1, "latin1"),
            FieldType.Struct => DecodeStructField(field, format, context),
            FieldType.Switch => DecodeSwitchField(field, format, context),
            FieldType.Bitfield => DecodeBitfieldField(field, format, context),
            _ => throw new InvalidOperationException($"Unknown field type: {field.Type}"),
        };
    }

    private DecodedInteger DecodeIntegerField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context,
        IReadOnlyList<DecodedNode>? siblings = null)
    {
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
        if (field.Checksum is not null && siblings is not null)
        {
            var (valid, expected) = VerifyChecksum(field.Checksum, value, siblings, context);
            checksumValid = valid;
            checksumExpected = expected;
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
            Description = field.Description,
        };
    }

    private DecodedBytes DecodeBytesField(
        FieldDefinition field,
        DecodeContext context)
    {
        var offset = context.Position;
        var size = ResolveSize(field, context);
        var bytes = context.ReadBytes(size);

        bool? valid = null;
        if (field.Expected is not null)
        {
            valid = bytes.Span.SequenceEqual(field.Expected);
        }

        return new DecodedBytes
        {
            Name = field.Name,
            Offset = offset,
            Size = size,
            RawBytes = bytes,
            ValidationPassed = valid,
            Description = field.Description,
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

    private DecodedStruct DecodeStructField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        if (field.StructRef is null)
            throw new InvalidOperationException($"Struct field '{field.Name}' has no struct reference");

        if (!format.Structs.TryGetValue(field.StructRef, out var structDef))
            throw new InvalidOperationException($"Struct '{field.StructRef}' not found");

        return DecodeStruct(structDef, format, context, field.Name);
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
        var hasSize = field.Size.HasValue || field.SizeExpression is not null || field.SizeRemaining;
        if (hasSize)
        {
            var size = ResolveSize(field, context);
            context.PushScope(size);
            var result = DecodeStruct(structDef, format, context, field.Name);
            context.PopScope();
            return result;
        }
        else
        {
            return DecodeStruct(structDef, format, context, field.Name);
        }
    }

    private DecodedArray DecodeRepeatedField(
        FieldDefinition field,
        FormatDefinition format,
        DecodeContext context)
    {
        var startOffset = context.Position;
        var elements = new List<DecodedNode>();
        var singleField = WithoutRepeat(field);

        switch (field.Repeat)
        {
            case RepeatMode.Count countMode:
            {
                var count = ExpressionEvaluator.EvaluateAsLong(countMode.CountExpression, context);
                for (var i = 0; i < count; i++)
                {
                    var element = DecodeSingleField(singleField, format, context);
                    elements.Add(element);
                }
                break;
            }

            case RepeatMode.UntilEof:
            {
                while (!context.IsEof)
                {
                    var element = DecodeSingleField(singleField, format, context);
                    elements.Add(element);
                }
                break;
            }

            case RepeatMode.UntilValue untilMode:
            {
                while (true)
                {
                    var element = DecodeSingleField(singleField, format, context);
                    elements.Add(element);
                    if (ExpressionEvaluator.EvaluateAsBool(untilMode.Condition, context))
                        break;
                    if (context.IsEof)
                        break;
                }
                break;
            }
        }

        return new DecodedArray
        {
            Name = field.Name,
            Offset = startOffset,
            Size = context.Position - startOffset,
            Elements = elements,
        };
    }

    private static FieldDefinition WithoutRepeat(FieldDefinition field)
    {
        return new FieldDefinition
        {
            Name = field.Name,
            Type = field.Type,
            Size = field.Size,
            SizeExpression = field.SizeExpression,
            SizeRemaining = field.SizeRemaining,
            EnumRef = field.EnumRef,
            FlagsRef = field.FlagsRef,
            StructRef = field.StructRef,
            Repeat = new RepeatMode.None(),
            SwitchOn = field.SwitchOn,
            SwitchCases = field.SwitchCases,
            SwitchDefault = field.SwitchDefault,
            BitfieldEntries = field.BitfieldEntries,
            Checksum = field.Checksum,
            Expected = field.Expected,
            Condition = field.Condition,
            Description = field.Description,
        };
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
            _ => throw new InvalidOperationException(
                $"Bitfield size must be 1, 2, or 4 bytes, got {size}"),
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

    private static (bool valid, long? expected) VerifyChecksum(
        ChecksumSpec spec, long actualValue,
        IReadOnlyList<DecodedNode> siblings, DecodeContext context)
    {
        // 対象フィールドのバイト範囲を収集
        using var ms = new MemoryStream();
        foreach (var fieldName in spec.FieldNames)
        {
            var sibling = siblings.FirstOrDefault(n => n.Name == fieldName);
            if (sibling is null)
                continue;

            var slice = context.SliceOriginal((int)sibling.Offset, (int)sibling.Size);
            ms.Write(slice.Span);
        }

        var data = ms.ToArray();

        if (spec.Algorithm.Equals("crc32", StringComparison.OrdinalIgnoreCase))
        {
            var computed = Crc32Calculator.Compute(data);
            var expected = (long)computed;
            var valid = actualValue == expected;
            return (valid, valid ? null : expected);
        }

        // 未知のアルゴリズム: 検証スキップ
        return (true, null);
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
