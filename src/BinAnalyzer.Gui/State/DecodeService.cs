using System.Diagnostics;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Gui.State;

public sealed record DecodeOutcome(DecodedStruct Root, IReadOnlyList<DecodeError> Errors, TimeSpan Elapsed, string? Failure);

/// <summary>部分的に壊れたファイルでも描画できるよう、エラー継続モードでデコードし所要時間を計測する。</summary>
public static class DecodeService
{
    public static DecodeOutcome Decode(byte[] data, FormatDefinition format, Endianness? endianOverride)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var options = new DecodeOptions { Endianness = endianOverride };
            var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue, options);
            sw.Stop();
            return new DecodeOutcome(result.Root, result.Errors, sw.Elapsed, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var empty = new DecodedStruct
            {
                Name = format.Name,
                StructType = format.RootStruct,
                Offset = 0,
                Size = data.Length,
                Children = [],
            };
            return new DecodeOutcome(empty, [], sw.Elapsed, ex.Message);
        }
    }
}
