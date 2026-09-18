using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>
/// REQ-160: 入力に依らず成り立つべき性質（プロパティ）を、生成した入力で確認する。
/// <list type="bullet">
/// <item>Stop モードの例外は <c>DecodeException</c> だけ。Continue モードは例外を投げない（<see cref="FuzzRunner"/> の分類で担保）</item>
/// <item>同じ入力を同じデコーダで 2 回デコードしても結果は同じ（デコーダにおかしな状態が残らない）</item>
/// <item>Stop モードで成功した入力は、Continue モードでも同じ木になりエラーは 0 件</item>
/// <item>Stop モードが失敗した入力でも、Continue モードは木を返しエラーを 1 件以上記録する</item>
/// <item>どんな木でも出力フォーマッタ（tree / JSON）は例外を投げない</item>
/// </list>
/// </summary>
[Trait("Category", "Fuzz")]
public sealed class DecodePropertyTests
{
    private const int InputsPerFormat = 25;

    public static IEnumerable<object[]> Formats => FormatCatalog.FormatFiles();

    private static IEnumerable<(byte[] Data, string Origin)> Inputs(string formatFile, Random rng, int count)
    {
        // 有効なサンプル（あれば）→ その変異 → 純ランダム、の順に混ぜる
        if (FormatCatalog.Samples.TryGetValue(formatFile, out var generators))
        {
            var samples = generators.Select(g => g()).ToList();
            for (var i = 0; i < samples.Count; i++)
                yield return (samples[i], $"sample #{i}");
            for (var i = 0; i < count; i++)
            {
                var (data, description) = InputGenerator.Mutate(rng, samples[rng.Next(samples.Count)]);
                yield return (data, $"mutation #{i} ({description})");
            }
        }
        for (var i = 0; i < count; i++)
            yield return (InputGenerator.Random(rng, 1024), $"random #{i}");
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void Decode_IsDeterministic_AndModesAgree(string formatFile)
    {
        var format = FormatCatalog.Get(formatFile);
        var rng = FuzzConfig.RandomFor("property:" + formatFile);
        var failures = new List<string>();

        foreach (var (data, origin) in Inputs(formatFile, rng, InputsPerFormat * FuzzConfig.Multiplier))
        {
            // 同じデコーダ instance を 4 回使い回す（状態が残らないことの検証）。
            // タイムアウトした呼び出しはスレッドが走り続けて instance を触るので、その入力はそこで打ち切る
            var decoder = new BinaryDecoder();
            var runs = new List<DecodeOutcome>(4);
            foreach (var mode in new[] { ErrorMode.Stop, ErrorMode.Continue, ErrorMode.Stop, ErrorMode.Continue })
            {
                var outcome = FuzzRunner.Decode(format, data, mode, decoder: decoder);
                runs.Add(outcome);
                if (outcome.IsCrash)
                {
                    failures.Add(FuzzRunner.Describe(formatFile, outcome, data, origin));
                    break;
                }
            }
            if (runs.Count < 4)
                continue;
            var (stop1, cont1, stop2, cont2) = (runs[0], runs[1], runs[2], runs[3]);

            // 決定性
            if (stop1.Kind != stop2.Kind)
                failures.Add($"{formatFile} {origin}: Stop mode outcome differs between runs ({stop1.Kind} vs {stop2.Kind})");
            else if (stop1.Root is { } r1 && stop2.Root is { } r2 && DiffEngine.Compare(r1, r2).HasDifferences)
                failures.Add($"{formatFile} {origin}: Stop mode tree differs between runs");
            if (cont1.Errors.Count != cont2.Errors.Count || DiffEngine.Compare(cont1.Root!, cont2.Root!).HasDifferences)
                failures.Add($"{formatFile} {origin}: Continue mode result differs between runs");

            // モード間の整合
            if (stop1.Kind == OutcomeKind.Ok)
            {
                if (cont1.Errors.Count != 0)
                    failures.Add($"{formatFile} {origin}: Stop succeeded but Continue recorded {cont1.Errors.Count} error(s): {cont1.Errors[0].Message}");
                else if (DiffEngine.Compare(stop1.Root!, cont1.Root!).HasDifferences)
                    failures.Add($"{formatFile} {origin}: Stop and Continue trees differ for a valid input");
            }
            else if (cont1.Errors.Count == 0)
            {
                failures.Add($"{formatFile} {origin}: Stop failed ({stop1.Exception!.Message}) but Continue recorded no error");
            }
        }

        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void OutputFormatters_NeverThrow_OnAnyDecodedTree(string formatFile)
    {
        var format = FormatCatalog.Get(formatFile);
        var rng = FuzzConfig.RandomFor("output:" + formatFile);
        var tree = new TreeOutputFormatter();
        var json = new JsonOutputFormatter();
        var failures = new List<string>();

        foreach (var (data, origin) in Inputs(formatFile, rng, InputsPerFormat * FuzzConfig.Multiplier))
        {
            var outcome = FuzzRunner.Decode(format, data, ErrorMode.Continue);
            if (outcome.Root is not { } root)
                continue;
            try
            {
                tree.Format(root).Should().NotBeNull();
                json.Format(root).Should().NotBeNull();
            }
            catch (Exception ex)
            {
                failures.Add($"{formatFile} {origin}: {ex.GetType().Name}: {ex.Message}\ninput base64: {Convert.ToBase64String(data)}");
            }
        }

        failures.Should().BeEmpty(string.Join("\n\n", failures.Take(3)));
    }
}
