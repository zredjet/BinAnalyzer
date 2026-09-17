using System.Globalization;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Patching;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Presentation;

namespace BinAnalyzer.Cli;

/// <summary>
/// <c>patch</c> サブコマンド（REQ-164）: フィールドパスで指定した値を書き換え、別ファイルに出力する。
/// エンコードとチェックサム再計算は REQ-169 の <see cref="FieldEncoder"/> / <see cref="BinaryPatcher"/> を使う。
/// </summary>
internal static class PatchCommand
{
    public sealed record Options(
        string InputPath,
        FileInfo FormatFile,
        IReadOnlyList<string> Sets,
        string? OutputPath,
        bool DryRun,
        bool NoChecksum,
        bool NoValidate,
        string ErrorFormat);

    private sealed record ResolvedSet(string Path, string Input, DecodedNode Node, byte[] Before, byte[] After, string? Note);

    public static int Run(Options options)
    {
        var reporter = new CliErrorReporter(options.ErrorFormat);
        var readFromStdin = options.InputPath == "-";

        if (!readFromStdin && !File.Exists(options.InputPath))
        {
            reporter.ReportError($"ファイルが見つかりません: {options.InputPath}");
            return 1;
        }
        if (!options.FormatFile.Exists)
        {
            reporter.ReportError($"フォーマットファイルが見つかりません: {options.FormatFile.FullName}");
            return 1;
        }
        if (options.Sets.Count == 0)
        {
            reporter.ReportError("--set <フィールドパス>=<値> を 1 つ以上指定してください");
            return 1;
        }
        if (!options.DryRun && string.IsNullOrEmpty(options.OutputPath))
        {
            reporter.ReportError("出力ファイル（-o）を指定してください。書き込まずに確認するだけなら --dry-run を使います");
            return 1;
        }
        if (!options.DryRun && !readFromStdin && SamePath(options.InputPath, options.OutputPath!))
        {
            reporter.ReportError("出力ファイルが入力ファイルと同じです。元ファイルは変更しないので別のパスを指定してください");
            return 1;
        }

        // 1. フォーマット定義
        FormatDefinition format;
        try
        {
            format = new YamlFormatLoader().Load(options.FormatFile.FullName);
            if (!options.NoValidate)
            {
                var validation = FormatValidator.Validate(format);
                if (validation.Warnings.Any() || !validation.IsValid)
                    reporter.ReportValidationResult(validation);
                if (!validation.IsValid)
                    return 1;
            }
        }
        catch (Exception ex)
        {
            reporter.ReportError($"フォーマット定義の読み込みに失敗しました: {ex.Message}");
            return 1;
        }

        // 2. 入力を読み、元のレイアウトでデコードしてパスを解決する
        byte[] data;
        try
        {
            data = ReadInput(options.InputPath, readFromStdin);
        }
        catch (Exception ex)
        {
            reporter.ReportError($"入力の読み込みに失敗しました: {ex.Message}");
            return 1;
        }

        NodeIndex index;
        try
        {
            var decoded = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
            index = NodeIndex.Build(decoded.Root);
        }
        catch (Exception ex)
        {
            reporter.ReportError($"デコードに失敗しました: {ex.Message}");
            return 1;
        }

        var resolved = new List<ResolvedSet>();
        foreach (var set in options.Sets)
        {
            var eq = set.IndexOf('=');
            if (eq <= 0)
            {
                reporter.ReportError($"--set の形式が不正です（<フィールドパス>=<値>）: {set}");
                return 1;
            }
            var path = set[..eq].Trim();
            var input = set[(eq + 1)..];

            var node = index.ByPath(path);
            if (node is null)
            {
                reporter.ReportError($"フィールドが見つかりません: {path}" + Suggest(index, path));
                return 1;
            }
            var id = index.IdOf(node);
            var editability = FieldEditability.Of(index, id);
            if (!editability.CanEdit)
            {
                reporter.ReportError($"{path} は書き換えできません: {editability.Reason}");
                return 1;
            }

            var encodeInput = ResolveEnumLabel(node, input, format);
            var result = FieldEncoder.Instance.Encode(node, encodeInput);
            if (!result.IsSuccess)
            {
                reporter.ReportError($"{path} の値 '{input}' を書き込めません: {result.Error}");
                return 1;
            }

            var range = new ByteRange(node.Offset, node.Size);
            if (resolved.Any(r => new ByteRange(r.Node.Offset, r.Node.Size).Overlaps(range)))
            {
                reporter.ReportError($"{path} の範囲が他の --set と重なっています（同じフィールドを複数回指定していませんか）");
                return 1;
            }
            var before = data.AsSpan((int)node.Offset, (int)node.Size).ToArray();
            resolved.Add(new ResolvedSet(path, input, node, before, result.Bytes!, result.Note));
        }

        // 3. 適用（チェックサム再計算込み）
        var patches = resolved.Select(r => new BytePatch(r.Node.Offset, r.After)).ToList();
        PatchOutcome outcome;
        try
        {
            outcome = BinaryPatcher.Apply(data, format, patches, recalculateChecksums: !options.NoChecksum);
        }
        catch (Exception ex)
        {
            reporter.ReportError($"パッチの適用に失敗しました: {ex.Message}");
            return 1;
        }

        // 4. 変更内容の表示
        var prefix = options.DryRun ? "[dry-run] " : "";
        foreach (var r in resolved)
        {
            var kind = NodeDisplayText.TypeLabel(r.Node);
            Console.WriteLine($"{prefix}{r.Path} ({kind}) @0x{r.Node.Offset:X8} {r.Node.Size} B");
            Console.WriteLine($"  値:     {CurrentValue(r.Node)} → {r.Input.Trim()}{(r.Note is null ? "" : $"  ({r.Note})")}");
            Console.WriteLine($"  バイト: {FieldEncoder.FormatHex(Preview(r.Before))} → {FieldEncoder.FormatHex(Preview(r.After))}");
            if (index.KindOf(index.IdOf(r.Node)) == FieldKind.Len)
                Console.WriteLine("  注意:   長さ・個数を決めるフィールドです。後続フィールドのオフセットが変わる可能性があります");
        }
        if (outcome.ChecksumFixes.Count > 0)
        {
            Console.WriteLine($"{prefix}チェックサム再計算: {outcome.ChecksumFixes.Count} 件");
            foreach (var f in outcome.ChecksumFixes)
                Console.WriteLine($"  {f.Path} ({ChecksumAlgorithms.DisplayName(f.Algorithm)}) @0x{f.Range.Offset:X8}: {FieldEncoder.FormatHex(Preview(f.Before))} → {FieldEncoder.FormatHex(Preview(f.After))}");
        }
        else if (options.NoChecksum)
        {
            Console.WriteLine($"{prefix}チェックサム再計算: スキップ（--no-checksum）");
        }
        if (!outcome.Converged)
            Console.Error.WriteLine("警告: チェックサムの再計算が収束しませんでした。出力を再確認してください");

        // 5. 適用後の検証（警告のみ）
        try
        {
            var check = new BinaryDecoder().DecodeWithRecovery(outcome.Data, format, ErrorMode.Continue);
            var summary = ChecksumSummary.Compute(NodeIndex.Build(check.Root));
            if (check.Errors.Count > 0)
                Console.Error.WriteLine($"警告: 書き換え後のデコードで {check.Errors.Count} 件のエラーがあります（--on-error continue 相当で確認してください）");
            if (!summary.AllChecksumsValid)
                Console.Error.WriteLine($"警告: 書き換え後にチェックサム不一致が {summary.ChecksumTotal - summary.ChecksumValid} 件あります");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"警告: 書き換え後のデコードに失敗しました: {ex.Message}");
        }

        if (options.DryRun)
        {
            Console.WriteLine("[dry-run] ファイルは作成しません");
            return 0;
        }

        // 6. 出力
        try
        {
            File.WriteAllBytes(options.OutputPath!, outcome.Data);
        }
        catch (Exception ex)
        {
            reporter.ReportError($"出力ファイルの書き込みに失敗しました: {ex.Message}");
            return 1;
        }
        Console.WriteLine($"書き込みました: {options.OutputPath} ({outcome.Data.Length} バイト, {resolved.Count} フィールド{(outcome.ChecksumFixes.Count > 0 ? $" + チェックサム {outcome.ChecksumFixes.Count} 件" : "")})");
        return 0;
    }

    /// <summary>enum 参照付き整数で、数値として読めなければラベルとして解決する。</summary>
    private static string ResolveEnumLabel(DecodedNode node, string input, FormatDefinition format)
    {
        if (node is not DecodedInteger { EnumRef: { } enumRef } || FieldEncoder.TryParseInteger(input.Trim(), out _))
            return input;
        if (!format.Enums.TryGetValue(enumRef, out var def))
            return input;
        var entry = def.Entries.FirstOrDefault(e => string.Equals(e.Label, input.Trim(), StringComparison.Ordinal))
                 ?? def.Entries.FirstOrDefault(e => string.Equals(e.Label, input.Trim(), StringComparison.OrdinalIgnoreCase));
        return entry is null ? input : entry.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string CurrentValue(DecodedNode node) => node switch
    {
        DecodedInteger { EnumLabel: { } label } i => $"{FieldEncoder.Instance.InitialText(i)} ({label})",
        DecodedString s => $"\"{s.Value.TrimEnd('\0')}\"",
        _ => FieldEncoder.Instance.InitialText(node),
    };

    private static ReadOnlySpan<byte> Preview(byte[] bytes) => bytes.Length > 16 ? bytes.AsSpan(0, 16) : bytes.AsSpan();

    /// <summary>末尾の名前が一致するパスを候補として示す（例: <c>width</c> → <c>chunks[0].data.width</c>）。</summary>
    private static string Suggest(NodeIndex index, string path)
    {
        var leaf = path.Split('.', '[')[^1].Trim(']');
        if (leaf.Length == 0) return "";
        var candidates = new List<string>();
        for (var id = 1; id < index.Count && candidates.Count < 5; id++)
        {
            if (string.Equals(index.ById(id).Name, leaf, StringComparison.OrdinalIgnoreCase))
                candidates.Add(index.PathOf(id));
        }
        return candidates.Count == 0 ? "" : $"（候補: {string.Join(", ", candidates)}）";
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b),
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }
    }

    private static byte[] ReadInput(string path, bool stdin)
    {
        if (!stdin)
            return File.ReadAllBytes(path);
        using var ms = new MemoryStream();
        using var input = Console.OpenStandardInput();
        input.CopyTo(ms);
        return ms.ToArray();
    }
}
