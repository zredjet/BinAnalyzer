using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.Desktop;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-185: GUI でのフォーマット定義の診断（ステータスバーの件数・定義ビューの一覧・行の印）。</summary>
public sealed class DefinitionDiagnosticsTests : BunitContext
{
    // 本ファイルに未知キー（10 行目の expect）、インポート先に未知キー（8 行目の sizee）
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
              expect: [0x01]
            - name: hdr
              type: struct
              struct: shared_header
        """;

    private const string TypesYaml = """
        name: Types
        root: _types
        structs:
          _types: []
          shared_header:
            - name: kind
              type: uint8
              sizee: "1"
        """;

    private const string CleanYaml = """
        name: Clean
        root: main
        structs:
          main:
            - name: magic
              type: uint8
        """;

    private async Task<(GuiSession Session, GuiDocument Doc, FakeFormatCatalog Catalog)> SetupAsync()
    {
        var catalog = new FakeFormatCatalog()
            .Add("main.bdef.yaml", "Main", [".mn"], MainYaml)
            .Add("clean.bdef.yaml", "Clean", [".cl"], CleanYaml)
            .AddSource("common/types.bdef.yaml", TypesYaml);
        var session = new GuiSession(catalog, new FakeFileSource());
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var doc = await session.OpenAsync(new OpenedFile("a.mn", [0x01, 0x02]));
        return (session, doc!, catalog);
    }

    [Fact]
    public async Task StatusBar_ShowsWarningCount_AndOpensTheList()
    {
        var (session, doc, _) = await SetupAsync();
        var cut = Render<StatusBar>(p => p.Add(x => x.Document, doc));

        var button = cut.Find(".status .diag");
        button.TextContent.Should().Be("定義: 警告 2");
        button.ClassList.Should().Contain("warn");

        button.Click();

        session.Pane.Should().Be(PaneKind.Definition);
        session.DefinitionDiagnosticsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task StatusBar_NoDiagnostics_ShowsNothing()
    {
        var (session, doc, _) = await SetupAsync();
        await session.ChangeFormatAsync(doc, "clean.bdef.yaml");

        var cut = Render<StatusBar>(p => p.Add(x => x.Document, doc));

        cut.FindAll(".status .diag").Should().BeEmpty();
    }

    [Fact]
    public async Task StatusBar_Errors_ShownInRedWithBothCounts()
    {
        var catalog = new FakeFormatCatalog().Add("bad.bdef.yaml", "Bad", [".bd"], """
            name: Bad
            root: main
            structs:
              main:
                - name: nested
                  type: struct
                - name: x
                  type: uint8
                  expect: [0]
            """);
        var session = new GuiSession(catalog, new FakeFileSource());
        Services.AddSingleton(session);
        var doc = await session.OpenAsync(new OpenedFile("a.bd", [0x01]));

        var cut = Render<StatusBar>(p => p.Add(x => x.Document, doc));

        var button = cut.Find(".status .diag");
        button.TextContent.Should().Be("定義: エラー 1・警告 1");
        button.ClassList.Should().Contain("err");
        doc!.DecodeFailure.Should().BeNull("定義にエラーがあってもデコードは続ける（エラー継続モード）");
    }

    [Fact]
    public async Task DefinitionView_ListsDiagnostics_WhenOpen()
    {
        var (session, doc, _) = await SetupAsync();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.FindAll(".defdiag-item").Should().BeEmpty("閉じている間は一覧を出さない");
        cut.Find(".defdiag-head").TextContent.Should().Contain("警告 2");

        cut.Find(".defdiag-head").Click();

        session.DefinitionDiagnosticsOpen.Should().BeTrue();
        cut.Render();
        var items = cut.FindAll(".defdiag-item");
        items.Should().HaveCount(2);
        items[0].QuerySelector(".code")!.TextContent.Should().Be("VAL123");
        items[0].QuerySelector(".loc")!.TextContent.Should().Be("main.bdef.yaml:10");
        items[1].QuerySelector(".loc")!.TextContent.Should().Be("types.bdef.yaml:8");
    }

    [Fact]
    public async Task SelectingDiagnostic_InMainFile_HighlightsItsLine()
    {
        var (session, doc, _) = await SetupAsync();
        session.ShowDefinitionDiagnostics();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.FindAll(".defdiag-item")[0].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("main.bdef.yaml");
            var cur = cut.Find(".yaml .line.cur");
            cur.TextContent.Trim().Should().Be("expect: [0x01]");
            cur.Id.Should().Be("yaml-cur");
            cut.Find(".defdiag-item.cur .code").TextContent.Should().Be("VAL123");
        });
    }

    [Fact]
    public async Task SelectingDiagnostic_InImportedFile_ShowsThatFile()
    {
        var (session, doc, catalog) = await SetupAsync();
        session.ShowDefinitionDiagnostics();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.FindAll(".defdiag-item")[1].Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("types.bdef.yaml");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().Be("sizee: \"1\"");
        });
        catalog.SourceReads.Should().Contain("common/types.bdef.yaml");
    }

    [Fact]
    public async Task SelectingANodeAgain_ReturnsToTheNodesDefinition()
    {
        var (session, doc, _) = await SetupAsync();
        session.ShowDefinitionDiagnostics();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".defdiag-item")[1].Click();
        cut.WaitForAssertion(() => cut.Find(".defbar span").TextContent.Should().Be("types.bdef.yaml"));

        doc.Select(doc.Index.ByPath("magic")!);
        cut.Render();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".defbar span").TextContent.Should().Be("main.bdef.yaml");
            cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: magic");
            cut.FindAll(".defdiag-item.cur").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task LinesWithDiagnostics_AreMarked_InTheShownFile()
    {
        var (_, doc, _) = await SetupAsync();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));

        cut.WaitForAssertion(() =>
        {
            var marked = cut.FindAll(".yaml .line.diag-warn");
            marked.Should().ContainSingle("本ファイルの印は expect の行だけ（インポート先の診断は出さない）");
            marked[0].TextContent.Trim().Should().Be("expect: [0x01]");
            marked[0].GetAttribute("title").Should().StartWith("VAL123: ");
        });
    }

    [Fact]
    public async Task ChangingFormat_SwitchesDiagnostics()
    {
        var (session, doc, _) = await SetupAsync();
        doc.Format.Validation.Warnings.Should().HaveCount(2);

        await session.ChangeFormatAsync(doc, "clean.bdef.yaml");

        doc.Format.Validation.Diagnostics.Should().BeEmpty();
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".defdiag").Should().BeEmpty();
    }

    [Fact]
    public async Task BundledFormats_GuiDiagnosticsMatchCli()
    {
        var formatsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));
        var catalog = new DirectoryFormatCatalog([formatsDir]);
        var loader = new YamlFormatLoader();

        var entries = await catalog.ListAsync();
        entries.Should().HaveCountGreaterThan(30, "同梱定義を列挙できていること");
        foreach (var entry in entries)
        {
            var gui = (await catalog.LoadAsync(entry.File)).Validation.Diagnostics;
            var cli = FormatValidator.Validate(loader.Load(Path.Combine(formatsDir, entry.File))).Diagnostics;

            gui.Select(d => (d.Code, d.Severity, d.MessageWithLocation))
                .Should().Equal(cli.Select(d => (d.Code, d.Severity, d.MessageWithLocation)), entry.File);
        }
    }
}
