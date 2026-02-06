using System.CommandLine;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Interfaces;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;

var fileArg = new Argument<FileInfo>("file")
{
    Description = "解析対象のバイナリファイル",
};

var formatOption = new Option<FileInfo>("-f", "--format")
{
    Description = "フォーマット定義ファイル (.bdef.yaml)",
    Required = true,
};

var outputOption = new Option<string>("-o", "--output")
{
    Description = "出力形式 (tree, json, hexdump, html, map)",
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

var rootCommand = new RootCommand("BinAnalyzer - 汎用バイナリ構造解析ツール")
{
    fileArg,
    formatOption,
    outputOption,
    colorOption,
    noValidateOption,
};

rootCommand.SetAction((parseResult) =>
{
    var file = parseResult.GetValue(fileArg)!;
    var formatFile = parseResult.GetValue(formatOption)!;
    var outputFormat = parseResult.GetValue(outputOption)!;
    var colorSetting = parseResult.GetValue(colorOption)!;
    var noValidate = parseResult.GetValue(noValidateOption);

    if (!file.Exists)
    {
        Console.Error.WriteLine($"エラー: ファイルが見つかりません: {file.FullName}");
        return 1;
    }

    if (!formatFile.Exists)
    {
        Console.Error.WriteLine($"エラー: フォーマットファイルが見つかりません: {formatFile.FullName}");
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

            foreach (var warning in validationResult.Warnings)
                Console.Error.WriteLine($"警告 [{warning.Code}]: {warning.Message}");

            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                    Console.Error.WriteLine($"エラー [{error.Code}]: {error.Message}");
                return 1;
            }
        }

        var data = File.ReadAllBytes(file.FullName);

        var decoder = new BinaryDecoder();
        var decoded = decoder.Decode(data, format);

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
                _ => new TreeOutputFormatter(colorMode),
            };
            output = formatter.Format(decoded);
        }

        Console.Write(output);
        return 0;
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

// diff サブコマンド
var file1Arg = new Argument<FileInfo>("file1")
{
    Description = "比較元のバイナリファイル",
};
var file2Arg = new Argument<FileInfo>("file2")
{
    Description = "比較先のバイナリファイル",
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

var diffCommand = new Command("diff", "2つのバイナリファイルの構造的差分を表示")
{
    file1Arg,
    file2Arg,
    diffFormatOption,
    diffColorOption,
};

diffCommand.SetAction((parseResult) =>
{
    var f1 = parseResult.GetValue(file1Arg)!;
    var f2 = parseResult.GetValue(file2Arg)!;
    var fmtFile = parseResult.GetValue(diffFormatOption)!;

    if (!f1.Exists)
    {
        Console.Error.WriteLine($"エラー: ファイルが見つかりません: {f1.FullName}");
        return 1;
    }
    if (!f2.Exists)
    {
        Console.Error.WriteLine($"エラー: ファイルが見つかりません: {f2.FullName}");
        return 1;
    }
    if (!fmtFile.Exists)
    {
        Console.Error.WriteLine($"エラー: フォーマットファイルが見つかりません: {fmtFile.FullName}");
        return 1;
    }

    try
    {
        var loader = new YamlFormatLoader();
        var format = loader.Load(fmtFile.FullName);

        var decoder = new BinaryDecoder();
        var decoded1 = decoder.Decode(File.ReadAllBytes(f1.FullName), format);
        var decoded2 = decoder.Decode(File.ReadAllBytes(f2.FullName), format);

        var diffResult = DiffEngine.Compare(decoded1, decoded2);
        var diffColorSetting = parseResult.GetValue(diffColorOption)!;
        var diffColorMode = diffColorSetting switch
        {
            "always" => ColorMode.Always,
            "never" => ColorMode.Never,
            _ => ColorMode.Auto,
        };
        var formatter = new DiffOutputFormatter(diffColorMode);
        Console.Write(formatter.Format(diffResult));
        return diffResult.HasDifferences ? 1 : 0;
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

return rootCommand.Parse(args).Invoke();
