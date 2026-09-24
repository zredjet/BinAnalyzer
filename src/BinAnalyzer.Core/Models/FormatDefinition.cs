namespace BinAnalyzer.Core.Models;

public sealed class FormatDefinition
{
    public required string Name { get; init; }
    public Endianness Endianness { get; init; } = Endianness.Big;
    public required IReadOnlyDictionary<string, EnumDefinition> Enums { get; init; }
    public required IReadOnlyDictionary<string, FlagsDefinition> Flags { get; init; }
    public required IReadOnlyDictionary<string, StructDefinition> Structs { get; init; }
    public required string RootStruct { get; init; }

    /// <summary>YAML にあった DSL の未知キー（読み飛ばしたもの）。検証器が VAL123 の警告にする（REQ-184）。</summary>
    public IReadOnlyList<DslUnknownKey> UnknownKeys { get; init; } = [];
}
