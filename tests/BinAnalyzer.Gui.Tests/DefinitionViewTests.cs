using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-172: 定義ビューはパーサーが記録した行（SourceLine）を強調し、インポート先の定義はそのファイルを表示する。</summary>
public sealed class DefinitionViewTests : BunitContext
{
    private const string MainYaml = """
        name: Main
        endianness: big
        root: main
        imports:
          - path: common/types.bdef.yaml
        structs:
          main:
            - name: magic
              type: uint8
            - name: hdr
              type: struct
              struct: shared_header
            - name: tail
              type: uint8
        """;

    private const string TypesYaml = """
        name: Types
        root: _types
        structs:
          _types: []
          # 共通ヘッダ
          shared_header:
            - name: kind
              type: uint8
            - name: length
              type: uint16
        """;

    private async Task<(GuiSession Session, GuiDocument Doc, FakeFormatCatalog Catalog)> SetupAsync()
    {
        var catalog = new FakeFormatCatalog()
            .Add("main.bdef.yaml", "Main", [".mn"], MainYaml)
            .AddSource("common/types.bdef.yaml", TypesYaml);
        var session = new GuiSession(catalog, new FakeFileSource());
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var doc = await session.OpenAsync(new OpenedFile("a.mn", [0x01, 0x02, 0x00, 0x10, 0x03]));
        return (session, doc!, catalog);
    }

    [Fact]
    public async Task FieldInMainFile_HighlightsParserLine()
    {
        var (_, doc, catalog) = await SetupAsync();
        doc.Select(doc.Index.ByPath("tail")!);
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("main.bdef.yaml");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: tail");
            cut.Find(".yaml .line.cur-struct").TextContent.Trim().Should().Be("main:");
        });
        catalog.SourceReads.Should().BeEmpty("本ファイル内の定義ではインポート先を読まない");
    }

    [Fact]
    public async Task FieldInImportedStruct_ShowsImportedFile_AndItsLine()
    {
        var (_, doc, catalog) = await SetupAsync();
        doc.Select(doc.Index.ByPath("hdr.length")!);
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("types.bdef.yaml");
            cut.Find(".defbar .hint").TextContent.Should().Contain("インポート定義");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: length");
            cut.Find(".yaml .line.cur-struct").TextContent.Trim().Should().Be("shared_header:");
        });
        catalog.SourceReads.Should().ContainSingle().Which.Should().Be("common/types.bdef.yaml");

        // 本ファイルのフィールドに戻ると本ファイルを表示する
        doc.Select(doc.Index.ByPath("magic")!);
        cut.Render();
        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("main.bdef.yaml");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: magic");
        });
    }

    [Fact]
    public async Task StructNodeSelected_HighlightsStructLine_InItsFile()
    {
        var (_, doc, _) = await SetupAsync();
        doc.Select(doc.Index.ByPath("hdr")!);
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        // hdr は main の中のフィールドなので、main.bdef.yaml の "- name: hdr" 行
        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("main.bdef.yaml");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: hdr");
        });
    }
}
