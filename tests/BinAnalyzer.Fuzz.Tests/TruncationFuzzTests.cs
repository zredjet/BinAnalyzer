using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>REQ-160: 有効なバイナリを 1 バイトずつ短くしてデコードし、クラッシュしないことを確認する。</summary>
[Trait("Category", "Fuzz")]
public sealed class TruncationFuzzTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Samples => FormatCatalog.SampleCases();

    [Fact]
    public void EveryFormat_HasAtLeastOneSample()
    {
        FormatCatalog.SamplesMissing().Should().BeEmpty("切り詰め・変異ファズは全フォーマットに有効なサンプルが要る");
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryPrefix_NeverCrashOrHang(string formatFile, int sampleIndex)
    {
        var format = FormatCatalog.Get(formatFile);
        var sample = FormatCatalog.Samples[formatFile][sampleIndex]();
        var failures = new List<string>();
        var stopErrors = 0;

        // 完全なサンプルは Stop モードで成功するはず（サンプル自体の健全性）
        var full = FuzzRunner.Decode(format, sample, ErrorMode.Stop);
        full.Kind.Should().Be(OutcomeKind.Ok, FuzzRunner.Describe(formatFile, full, sample, $"sample #{sampleIndex} (full)"));

        // 長いサンプルは全プレフィックスだと多いので、末尾寄りは全部・手前は間引く
        var step = sample.Length > 2048 ? sample.Length / 1024 : 1;
        for (var length = sample.Length - 1; length >= 0; length -= length > 256 ? step : 1)
        {
            var data = sample[..length];
            foreach (var mode in new[] { ErrorMode.Stop, ErrorMode.Continue })
            {
                var outcome = FuzzRunner.Decode(format, data, mode);
                if (outcome.IsCrash)
                    failures.Add(FuzzRunner.Describe(formatFile, outcome, data, $"sample #{sampleIndex} truncated to {length}"));
                else if (outcome.Root is { } root)
                {
                    var problems = TreeInvariants.Check(root, data.Length);
                    if (problems.Count > 0)
                        failures.Add(FuzzRunner.Describe(formatFile, outcome, data, $"sample #{sampleIndex} truncated to {length}") + "\ninvariants: " + string.Join("; ", problems.Take(5)));
                }
                if (mode == ErrorMode.Stop && outcome.Kind == OutcomeKind.DecodeError)
                    stopErrors++;
            }
        }

        output.WriteLine($"{formatFile} sample #{sampleIndex}: {sample.Length} B, Stop-mode DecodeException on {stopErrors} prefixes");
        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }
}
