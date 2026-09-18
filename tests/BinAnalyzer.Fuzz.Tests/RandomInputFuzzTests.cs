using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>REQ-160: ランダムバイト列を全フォーマット定義でデコードし、クラッシュ（想定外の例外）とハングが無いことを確認する。</summary>
[Trait("Category", "Fuzz")]
public sealed class RandomInputFuzzTests(ITestOutputHelper output)
{
    private const int InputsPerFormat = 40;

    public static IEnumerable<object[]> Formats => FormatCatalog.FormatFiles();

    [Theory]
    [MemberData(nameof(Formats))]
    public void RandomBytes_NeverCrashOrHang(string formatFile)
    {
        var format = FormatCatalog.Get(formatFile);
        var rng = FuzzConfig.RandomFor("random:" + formatFile);
        var failures = new List<string>();
        var stats = new Dictionary<OutcomeKind, int>();
        var n = InputsPerFormat * FuzzConfig.Multiplier;

        for (var i = 0; i < n; i++)
        {
            var data = i % 3 == 2 ? InputGenerator.Skewed(rng) : InputGenerator.Random(rng);
            foreach (var mode in new[] { ErrorMode.Stop, ErrorMode.Continue })
            {
                var outcome = FuzzRunner.Decode(format, data, mode);
                stats[outcome.Kind] = stats.GetValueOrDefault(outcome.Kind) + 1;
                if (outcome.IsCrash)
                    failures.Add(FuzzRunner.Describe(formatFile, outcome, data, $"random #{i}"));
                else if (outcome.Root is { } root)
                {
                    var problems = TreeInvariants.Check(root, data.Length);
                    if (problems.Count > 0)
                        failures.Add(FuzzRunner.Describe(formatFile, outcome, data, $"random #{i}") + "\ninvariants: " + string.Join("; ", problems.Take(5)));
                }
            }
        }

        output.WriteLine($"{formatFile}: {n} inputs × 2 modes — " + string.Join(", ", stats.Select(kv => $"{kv.Key}={kv.Value}")));
        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }

    /// <summary>空入力と 1 バイト入力（全 256 通り）。境界の典型で、全フォーマットで決定的に回す。</summary>
    [Theory]
    [MemberData(nameof(Formats))]
    public void EmptyAndSingleByte_NeverCrash(string formatFile)
    {
        var format = FormatCatalog.Get(formatFile);
        var failures = new List<string>();
        var inputs = new List<byte[]> { Array.Empty<byte>() };
        for (var b = 0; b < 256; b++)
            inputs.Add([(byte)b]);

        foreach (var data in inputs)
        {
            foreach (var mode in new[] { ErrorMode.Stop, ErrorMode.Continue })
            {
                var outcome = FuzzRunner.Decode(format, data, mode);
                if (outcome.IsCrash)
                    failures.Add(FuzzRunner.Describe(formatFile, outcome, data, data.Length == 0 ? "empty" : $"byte {data[0]:X2}"));
            }
        }

        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }
}
