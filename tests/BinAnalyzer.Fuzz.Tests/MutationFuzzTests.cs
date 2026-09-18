using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>
/// REQ-160: 有効なサンプルにランダムな変異（バイト書き換え・ビット反転・境界値の注入・切り詰め・追記・複製・削除）を加えてデコードする。
/// 純ランダムより深い経路（ヘッダを通過した後の長さ・個数・オフセットの処理）に届く。
/// </summary>
[Trait("Category", "Fuzz")]
public sealed class MutationFuzzTests(ITestOutputHelper output)
{
    private const int MutationsPerSample = 120;

    public static IEnumerable<object[]> Samples => FormatCatalog.SampleCases();

    [Theory]
    [MemberData(nameof(Samples))]
    public void MutatedSamples_NeverCrashOrHang(string formatFile, int sampleIndex)
    {
        var format = FormatCatalog.Get(formatFile);
        var sample = FormatCatalog.Samples[formatFile][sampleIndex]();
        var rng = FuzzConfig.RandomFor($"mutate:{formatFile}#{sampleIndex}");
        var failures = new List<string>();
        var stats = new Dictionary<OutcomeKind, int>();
        var n = MutationsPerSample * FuzzConfig.Multiplier;

        for (var i = 0; i < n; i++)
        {
            // 変異を 1〜3 回重ねる
            var data = sample;
            var descriptions = new List<string>();
            var rounds = 1 + rng.Next(3);
            for (var r = 0; r < rounds; r++)
            {
                var (mutated, description) = InputGenerator.Mutate(rng, data);
                data = mutated;
                descriptions.Add(description);
            }
            var origin = $"mutation #{i} ({string.Join(" → ", descriptions)})";

            foreach (var mode in new[] { ErrorMode.Stop, ErrorMode.Continue })
            {
                var outcome = FuzzRunner.Decode(format, data, mode);
                stats[outcome.Kind] = stats.GetValueOrDefault(outcome.Kind) + 1;
                if (outcome.IsCrash)
                    failures.Add(FuzzRunner.Describe(formatFile, outcome, data, origin));
                else if (outcome.Root is { } root)
                {
                    var problems = TreeInvariants.Check(root, data.Length);
                    if (problems.Count > 0)
                        failures.Add(FuzzRunner.Describe(formatFile, outcome, data, origin) + "\ninvariants: " + string.Join("; ", problems.Take(5)));
                }
            }
        }

        output.WriteLine($"{formatFile} sample #{sampleIndex}: {n} mutations × 2 modes — " + string.Join(", ", stats.Select(kv => $"{kv.Key}={kv.Value}")));
        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }
}
