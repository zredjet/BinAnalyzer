using System.CommandLine;
using System.Runtime.InteropServices;
using BinAnalyzer.Cli;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using BinAnalyzer.Tui;

// SIGPIPE無視（パイプ切断時のクラッシュ防止）
if (!OperatingSystem.IsWindows())
{
    IgnoreSigpipe();
}

[DllImport("libc", SetLastError = true)]
static extern nint signal(int signum, nint handler);

static void IgnoreSigpipe()
{
    const int SIGPIPE = 13;
    const nint SIG_IGN = 1;
    signal(SIGPIPE, SIG_IGN);
}

var fileArg = new Argument<string?>("file")
{
    Description = "解析対象のバイナリファイル（'-' でstdin）",
    Arity = ArgumentArity.ZeroOrOne,
};

var formatOption = new Option<FileInfo>("-f", "--format")
{
    Description = "フォーマット定義ファイル (.bdef.yaml)",
    Required = true,
};

var outputOption = new Option<string>("-o", "--output")
{
    Description = "出力形式 (tree, json, hexdump, html, map, csv, tsv, tui)",
    DefaultValueFactory = _ => "tree",
};

var colorOption = new Option<string>("--color")
{
    Description = "カラー出力 (auto, always, never)",
    DefaultValueFactory = _ => "auto",
};

var noValidateOption = new Option<bool>("--no-validate")
{
    Description = "フォーマット定義のバリデーションをスキップする",
};

var filterOption = new Option<string[]>("--filter")
{
    Description = "出力フィルタ（フィールドパスパターン、複数指定可）",
};

var onErrorOption = new Option<string>("--on-error")
{
    Description = "エラー時の動作 (stop, continue)",
    DefaultValueFactory = _ => "stop",
};

var stdinOption = new Option<bool>("--stdin")
{
    Description = "標準入力からバイナリデータを読み込む",
};

var quietOption = new Option<bool>("-q", "--quiet")
{
    Description = "デコード結果を出力せず、終了コードのみ返す",
};

var errorFormatOption = new Option<string>("--error-format")
{
    Description = "エラー出力形式 (text, json)",
    DefaultValueFactory = _ => "text",
};

var maxRepeatOption = new Option<int?>("--max-repeat")
{
    Description = "繰り返し回数のグローバル上限（フォーマット定義の repeat_max が優先）",
};

var rootCommand = new RootCommand("BinAnalyzer - 汎用バイナリ構造解析ツール")
{
    fileArg,
    formatOption,
    outputOption,
    colorOption,
    noValidateOption,
    filterOption,
    onErrorOption,
    stdinOption,
    quietOption,
    errorFormatOption,
    maxRepeatOption,
};

rootCommand.SetAction((parseResult) =>
{
    var filePath = parseResult.GetValue(fileArg);
    var formatFile = parseResult.GetValue(formatOption)!;
    var outputFormat = parseResult.GetValue(outputOption)!;
    var colorSetting = parseResult.GetValue(colorOption)!;
    var noValidate = parseResult.GetValue(noValidateOption);
    var filterPatterns = parseResult.GetValue(filterOption);
    var onError = parseResult.GetValue(onErrorOption);
    var useStdin = parseResult.GetValue(stdinOption);
    var quiet = parseResult.GetValue(quietOption);
    var errorFormat = parseResult.GetValue(errorFormatOption)!;

    var maxRepeat = parseResult.GetValue(maxRepeatOption);

    var reporter = new CliErrorReporter(errorFormat);
    var readFromStdin = useStdin || filePath == "-";

    // 排他チェック
    if (useStdin && filePath is not null && filePath != "-")
    {
        reporter.ReportError("--stdin とファイルパスは同時に指定できません");
        return 1;
    }
    if (!readFromStdin && filePath is null)
    {
        reporter.ReportError("ファイルパスまたは --stdin が必要です");
        return 1;
    }
    if (!readFromStdin && !File.Exists(filePath!))
    {
        reporter.ReportError($"ファイルが見つかりません: {filePath}");
        return 1;
    }

    if (!formatFile.Exists)
    {
        reporter.ReportError($"フォーマットファイルが見つかりません: {formatFile.FullName}");
        return 1;
    }

    try
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(formatFile.FullName);

        // バリデーション
        if (!noValidate)
        {
            var validationResult = FormatValidator.Validate(format);

            if (validationResult.Warnings.Any() || !validationResult.IsValid)
                reporter.ReportValidationResult(validationResult);

            if (!validationResult.IsValid)
                return 1;
        }

        var data = ReadInputData(filePath, readFromStdin);
        var errorMode = onError == "continue" ? ErrorMode.Continue : ErrorMode.Stop;
        var decodeOptions = maxRepeat.HasValue ? new DecodeOptions { MaxRepeat = maxRepeat.Value } : null;

        var decoder = new BinaryDecoder();
        DecodeResult? decodeResult = null;
        DecodedStruct decoded;
        if (errorMode == ErrorMode.Continue)
        {
            decodeResult = decoder.DecodeWithRecovery(data, format, errorMode, decodeOptions);
            decoded = decodeResult.Root;
        }
        else
        {
            decoded = decoder.Decode(data, format, decodeOptions);
        }

        // フィルタ適用
        if (filterPatterns is { Length: > 0 })
        {
            var pathFilter = new PathFilter(filterPatterns);
            var filtered = NodeFilterHelper.FilterTree(decoded, pathFilter);
            if (filtered is null)
            {
                // マッチなし: 空出力で正常終了
                return 0;
            }
            decoded = filtered;
        }

        if (!quiet)
        {
            if (outputFormat == "tui")
            {
                var displayName = readFromStdin ? "<stdin>" : Path.GetFileName(filePath!);
                var tuiApp = new TuiApp();
                tuiApp.Run(decoded, data, displayName, formatFile.Name);
                return 0;
            }

            var colorMode = colorSetting switch
            {
                "always" => ColorMode.Always,
                "never" => ColorMode.Never,
                _ => ColorMode.Auto,
            };

            string output;
            if (outputFormat == "hexdump")
            {
                var hexFormatter = new HexDumpOutputFormatter(colorMode);
                output = hexFormatter.Format(decoded, data);
            }
            else if (outputFormat == "map")
            {
                var mapFormatter = new MapOutputFormatter(colorMode);
                output = mapFormatter.Format(decoded, data);
            }
            else
            {
                IOutputFormatter formatter = outputFormat switch
                {
                    "json" => new JsonOutputFormatter(),
                    "html" => new HtmlOutputFormatter(),
                    "csv" => new CsvOutputFormatter(),
                    "tsv" => new CsvOutputFormatter(useTsv: true),
                    _ => new TreeOutputFormatter(colorMode),
                };
                output = formatter.Format(decoded);
            }

            try
            {
                Console.Write(output);
            }
            catch (IOException)
            {
                // Broken pipe — 正常終了
                return 0;
            }
        }

        // エラーサマリー（quiet でも stderr に出力）
        if (decodeResult?.Errors is { Count: > 0 } errors)
        {
            reporter.ReportDecodeErrors(errors);
        }

        if (HasValidationFailures(decoded))
            return 2;

        return 0;
    }
    catch (DecodeException dex)
    {
        reporter.ReportDecodeException(dex);
        return 1;
    }
    catch (Exception ex)
    {
        reporter.ReportError(ex.Message);
        return 1;
    }
});

// diff サブコマンド
var file1Arg = new Argument<string>("file1")
{
    Description = "比較元のバイナリファイルまたはディレクトリ",
};
var file2Arg = new Argument<string>("file2")
{
    Description = "比較先のバイナリファイルまたはディレクトリ",
};
var diffFormatOption = new Option<FileInfo>("-f", "--format")
{
    Description = "フォーマット定義ファイル (.bdef.yaml)",
    Required = true,
};

var diffColorOption = new Option<string>("--color")
{
    Description = "カラー出力 (auto, always, never)",
    DefaultValueFactory = _ => "auto",
};

var diffOutputOption = new Option<string>("--output")
{
    Description = "出力形式 (flat, tree)",
    DefaultValueFactory = _ => "flat",
};

var summaryOption = new Option<bool>("--summary")
{
    Description = "詳細差分の末尾に統計サマリーを追加表示",
};

var summaryOnlyOption = new Option<bool>("--summary-only")
{
    Description = "統計サマリーのみ表示（詳細差分を省略）",
};

var diffCommand = new Command("diff", "2つのバイナリファイルの構造的差分を表示")
{
    file1Arg,
    file2Arg,
    diffFormatOption,
    diffColorOption,
    diffOutputOption,
    summaryOption,
    summaryOnlyOption,
};

diffCommand.SetAction((parseResult) =>
{
    var f1 = parseResult.GetValue(file1Arg)!;
    var f2 = parseResult.GetValue(file2Arg)!;
    var fmtFile = parseResult.GetValue(diffFormatOption)!;

    if (!fmtFile.Exists)
    {
        Console.Error.WriteLine($"エラー: フォーマットファイルが見つかりません: {fmtFile.FullName}");
        return 1;
    }

    var isDir1 = Directory.Exists(f1);
    var isDir2 = Directory.Exists(f2);

    if (isDir1 != isDir2)
    {
        Console.Error.WriteLine("エラー: ファイルとディレクトリを混在して指定できません");
        return 1;
    }

    var diffColorSetting = parseResult.GetValue(diffColorOption)!;
    var diffColorMode = diffColorSetting switch
    {
        "always" => ColorMode.Always,
        "never" => ColorMode.Never,
        _ => ColorMode.Auto,
    };

    try
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(fmtFile.FullName);

        if (isDir1 && isDir2)
        {
            // === バッチモード ===
            var leftFiles = Directory.GetFiles(f1).Select(Path.GetFileName).ToHashSet()!;
            var rightFiles = Directory.GetFiles(f2).Select(Path.GetFileName).ToHashSet()!;

            var commonFiles = leftFiles.Intersect(rightFiles).OrderBy(f => f).ToList();
            var leftOnly = leftFiles.Except(rightFiles).OrderBy(f => f).ToList();
            var rightOnly = rightFiles.Except(leftFiles).OrderBy(f => f).ToList();

            var decoder = new BinaryDecoder();
            var entries = new List<BatchDiffFileEntry>();

            foreach (var fileName in commonFiles)
            {
                try
                {
                    var decoded1 = decoder.Decode(File.ReadAllBytes(Path.Combine(f1, fileName!)), format);
                    var decoded2 = decoder.Decode(File.ReadAllBytes(Path.Combine(f2, fileName!)), format);
                    var diffResult = DiffEngine.Compare(decoded1, decoded2);

                    entries.Add(new BatchDiffFileEntry
                    {
                        FileName = fileName!,
                        Statistics = diffResult.Statistics,
                        HasDifferences = diffResult.HasDifferences,
                    });
                }
                catch (Exception ex)
                {
                    entries.Add(new BatchDiffFileEntry
                    {
                        FileName = fileName!,
                        HasError = true,
                        ErrorMessage = ex.Message,
                    });
                }
            }

            var batchResult = new BatchDiffResult
            {
                FileEntries = entries,
                LeftOnlyFiles = leftOnly!,
                RightOnlyFiles = rightOnly!,
            };

            var batchFormatter = new BatchDiffSummaryFormatter(diffColorMode);
            Console.Write(batchFormatter.Format(batchResult));

            return batchResult.HasDifferences ? 1 : 0;
        }
        else
        {
            // === 既存ファイルモード ===
            if (!File.Exists(f1))
            {
                Console.Error.WriteLine($"エラー: ファイルが見つかりません: {f1}");
                return 1;
            }
            if (!File.Exists(f2))
            {
                Console.Error.WriteLine($"エラー: ファイルが見つかりません: {f2}");
                return 1;
            }

            var decoder = new BinaryDecoder();
            var decoded1 = decoder.Decode(File.ReadAllBytes(f1), format);
            var decoded2 = decoder.Decode(File.ReadAllBytes(f2), format);

            var outputFormat = parseResult.GetValue(diffOutputOption)!;
            var showSummary = parseResult.GetValue(summaryOption);
            var showSummaryOnly = parseResult.GetValue(summaryOnlyOption);

            if (showSummaryOnly)
            {
                var diffResult = DiffEngine.Compare(decoded1, decoded2);
                var summaryFormatter = new DiffSummaryFormatter(diffColorMode);
                Console.Write(summaryFormatter.Format(diffResult.Statistics!));
                return diffResult.HasDifferences ? 1 : 0;
            }
            else if (outputFormat == "tree")
            {
                var treeFormatter = new DiffTreeOutputFormatter(diffColorMode);
                Console.Write(treeFormatter.Format(decoded1, decoded2));
                var hasDiff = treeFormatter.HasDifferences;

                if (showSummary)
                {
                    var diffResult = DiffEngine.Compare(decoded1, decoded2);
                    var summaryFormatter = new DiffSummaryFormatter(diffColorMode);
                    Console.Write(summaryFormatter.Format(diffResult.Statistics!));
                }

                return hasDiff ? 1 : 0;
            }
            else
            {
                var diffResult = DiffEngine.Compare(decoded1, decoded2);
                var formatter = new DiffOutputFormatter(diffColorMode);
                Console.Write(formatter.Format(diffResult));

                if (showSummary)
                {
                    var summaryFormatter = new DiffSummaryFormatter(diffColorMode);
                    Console.Write(summaryFormatter.Format(diffResult.Statistics!));
                }

                return diffResult.HasDifferences ? 1 : 0;
            }
        }
    }
    catch (DecodeException dex)
    {
        Console.Error.Write(dex.FormatMessage());
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"エラー: {ex.Message}");
        return 1;
    }
});

rootCommand.Subcommands.Add(diffCommand);

// schema サブコマンド
var schemaFormatFileArg = new Argument<FileInfo>("format-file")
{
    Description = "フォーマット定義ファイル (.bdef.yaml)",
};

var schemaOutputOption = new Option<string>("-o", "--output")
{
    Description = "出力形式 (mermaid, dot, graphviz)",
    DefaultValueFactory = _ => "mermaid",
};

var schemaCommand = new Command("schema", "フォーマット定義のIR構造図を出力")
{
    schemaFormatFileArg,
    schemaOutputOption,
};

schemaCommand.SetAction((parseResult) =>
{
    var fmtFile = parseResult.GetValue(schemaFormatFileArg)!;
    var outputFormat = parseResult.GetValue(schemaOutputOption)!;

    if (!fmtFile.Exists)
    {
        Console.Error.WriteLine($"エラー: フォーマットファイルが見つかりません: {fmtFile.FullName}");
        return 1;
    }

    try
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(fmtFile.FullName);

        // バリデーション
        var validationResult = FormatValidator.Validate(format);

        foreach (var warning in validationResult.Warnings)
            Console.Error.WriteLine($"警告 [{warning.Code}]: {warning.Message}");

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
                Console.Error.WriteLine($"エラー [{error.Code}]: {error.Message}");
            return 1;
        }

        ISchemaFormatter formatter = outputFormat switch
        {
            "dot" or "graphviz" => new DotSchemaFormatter(),
            _ => new MermaidSchemaFormatter(),
        };

        Console.Write(formatter.Format(format));
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"エラー: {ex.Message}");
        return 1;
    }
});

rootCommand.Subcommands.Add(schemaCommand);

return rootCommand.Parse(args).Invoke();

// --- ヘルパー関数 ---

static byte[] ReadInputData(string? filePath, bool readFromStdin)
{
    if (readFromStdin)
    {
        using var ms = new MemoryStream();
        using var stdin = Console.OpenStandardInput();
        stdin.CopyTo(ms);
        return ms.ToArray();
    }
    return File.ReadAllBytes(filePath!);
}

static bool HasValidationFailures(DecodedNode node)
{
    if (node.Validation is { Passed: false })
        return true;
    if (node is DecodedStruct s)
        return s.Children.Any(HasValidationFailures);
    if (node is DecodedArray a)
        return a.Elements.Any(HasValidationFailures);
    if (node is DecodedCompressed c && c.DecodedContent is not null)
        return HasValidationFailures(c.DecodedContent);
    return false;
}
