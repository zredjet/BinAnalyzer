using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

public sealed class ComponentTests : BunitContext
{
    private async Task<(GuiSession Session, GuiDocument Doc)> SetupAsync(byte width = 1)
    {
        var (session, doc) = await SessionFactory.WithPngAsync(width);
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return (session, doc);
    }

    [Fact]
    public async Task HexView_SelectingNode_MarksExactlyItsBytes()
    {
        var (_, doc) = await SetupAsync();
        var cut = Render<HexView>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".row").Count.Should().Be(doc.Hex.RowCount);

        var width = doc.Index.ByPath("chunks[0].data.width")!;
        doc.Select(width);
        cut.Render();

        cut.FindAll(".b.sel").Count.Should().Be((int)width.Size);
        cut.FindAll(".a.sel").Count.Should().Be((int)width.Size);
        cut.FindAll(".b.sel").Should().AllSatisfy(e => e.GetAttribute("data-n").Should().Be(doc.Index.IdOf(width).ToString()));

        // struct 選択 → 子孫全バイト
        var chunk0 = doc.Index.ByPath("chunks[0]")!;
        doc.Select(chunk0);
        cut.Render();
        cut.FindAll(".b.sel").Count.Should().Be((int)chunk0.Size);
    }

    [Fact]
    public async Task HexView_CellsHaveSemanticClasses_AndGhosts()
    {
        var (_, doc) = await SetupAsync();
        var cut = Render<HexView>(p => p.Add(x => x.Document, doc));
        var first = cut.Find(".row .b");
        first.ClassList.Should().Contain("k-magic").And.Contain("fs");
        cut.FindAll(".row").First().QuerySelectorAll(".b")[7].ClassList.Should().Contain("fe");
        cut.FindAll(".row").First().QuerySelectorAll(".b")[8].ClassList.Should().Contain("k-len");
        cut.Find(".ghost").TextContent.Should().Contain("signature").And.Contain("✓");
    }

    [Fact]
    public async Task HexView_ClickingCell_SelectsNode()
    {
        var (_, doc) = await SetupAsync();
        var cut = Render<HexView>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".row").First().QuerySelectorAll(".b")[12].Click();
        doc.Selected!.Name.Should().Be("type");
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].type");
    }

    [Fact]
    public async Task HighlightStyle_RendersRuleForHoveredNode()
    {
        var (session, doc) = await SetupAsync();
        var cut = Render<HighlightStyle>();
        cut.FindAll("style").Should().BeEmpty();

        var id = doc.Index.IdOf(doc.Index.ByPath("chunks[0].type")!);
        doc.Hover(id);
        cut.WaitForAssertion(() => cut.Find("style").TextContent.Should().Contain($"[data-p*=\"/{id}/\"]").And.Contain($".node[data-n=\"{id}\"]"));

        doc.Hover(-1);
        cut.WaitForAssertion(() => cut.FindAll("style").Should().BeEmpty());
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task StructTree_SelectExpandsAncestors_ChevronTogglesCollapse()
    {
        var (_, doc) = await SetupAsync();
        doc.CollapseAll();
        var cut = Render<StructTree>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".node").Count.Should().Be(3, "折りたたみ時はルートとその直下のみ");

        var width = doc.Index.ByPath("chunks[0].data.width")!;
        doc.Select(width);
        cut.Render();
        var sel = cut.Find(".node.sel");
        sel.GetAttribute("data-n").Should().Be(doc.Index.IdOf(width).ToString());
        sel.TextContent.Should().Contain("width");

        // chunks[0] の chevron で折りたたむ → width は消える
        var chunk0Id = doc.Index.IdOf(doc.Index.ByPath("chunks[0]")!);
        cut.Find($".node[data-n=\"{chunk0Id}\"] .chev").Click();
        cut.FindAll(".node.sel").Should().BeEmpty();
        doc.IsExpanded(chunk0Id).Should().BeFalse();
    }

    [Fact]
    public async Task StructTree_ShowsEnumLabel_AndChecksumMark()
    {
        var (_, doc) = await SetupAsync();
        doc.ExpandAll();
        var cut = Render<StructTree>(p => p.Add(x => x.Document, doc));
        var colorType = doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.color_type")!);
        var node = cut.Find($".node[data-n=\"{colorType}\"]");
        node.QuerySelector(".lbl")!.TextContent.Should().Be("truecolor");
        var crc = doc.Index.IdOf(doc.Index.ByPath("chunks[0].crc")!);
        cut.Find($".node[data-n=\"{crc}\"] .ok").TextContent.Should().Be("✓");
    }

    [Fact]
    public async Task Breadcrumb_ShowsChain_AndClickSelectsAncestor()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        var cut = Render<BreadcrumbBar>(p => p.Add(x => x.Document, doc));
        cut.FindAll("button").Select(b => b.TextContent).Should().Equal("image.png", "chunks", "#0", "data", "width");

        cut.FindAll("button")[2].Click();
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0]");
    }

    [Fact]
    public async Task Inspector_RendersRows_ForInteger_Enum_Flags_Validation()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.color_type")!);
        var cut = Render<Inspector>(p => p.Add(x => x.Document, doc));
        cut.Find(".exp-head .path").TextContent.Should().Be("chunks[0].data.color_type");
        var dts = cut.FindAll("dt").Select(d => d.TextContent).ToList();
        dts.Should().Contain(["型", "位置", "生バイト", "値", "enum"]);
        cut.FindAll("dd")[0].TextContent.Should().Be("u8 (enum)");
        cut.FindAll("dd").Select(d => d.TextContent).Should().Contain("truecolor");

        doc.Select(doc.Index.ByPath("chunks[0].type")!);
        cut.Render();
        cut.Find(".flags").QuerySelectorAll("span").Should().HaveCount(4);

        doc.Select(doc.Index.ByPath("chunks[0].crc")!);
        cut.Render();
        cut.Find(".valid .ok").TextContent.Should().Be("✓");

        doc.Select(doc.Index.ByPath("signature")!);
        cut.Render();
        cut.Find(".kv .raw span").TextContent.Should().Be("89 50 4E 47 0D 0A 1A 0A");
    }

    [Fact]
    public async Task DefinitionView_HighlightsSelectedFieldLine()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        var cut = Render<DefinitionView>(p => p.Add(x => x.Document, doc));
        var cur = cut.Find(".yaml .line.cur");
        cur.TextContent.Trim().Should().StartWith("- name: width");
        cut.Find(".yaml .line.cur-struct").TextContent.Trim().Should().Be("ihdr:");
        cut.FindAll(".yaml .k").Should().NotBeEmpty();

        // 配列要素を選ぶと親配列フィールド（chunks）の定義行
        doc.Select(doc.Index.ByPath("chunks[1]")!);
        cut.Render();
        cut.Find(".yaml .line.cur").TextContent.Trim().Should().StartWith("- name: chunks");
    }

    [Fact]
    public async Task DiffView_ShowsRows_AndClickSelectsLeftNode()
    {
        var (session, left) = await SetupAsync(width: 1);
        var right = (await session.OpenAsync(new BinAnalyzer.Gui.Abstractions.OpenedFile("v2.png", TestPng.Bytes(width: 2))))!;
        session.Activate(left);
        session.Compare(right);

        var cut = Render<DiffView>();
        cut.Find(".diffsum").TextContent.Should().Contain("変更 2");
        var rows = cut.FindAll(".diffrow");
        rows.Should().HaveCount(2);
        rows[0].QuerySelector(".p")!.TextContent.Should().Be("chunks[0].data.width");
        rows[0].QuerySelector(".from")!.TextContent.Should().Be("1");
        rows[0].QuerySelector(".to")!.TextContent.Should().Be("2");
        rows[0].QuerySelector(".m")!.ClassList.Should().Contain("k-num");
        rows[1].QuerySelector(".m")!.ClassList.Should().Contain("k-crc");

        rows[0].Click();
        session.Active.Should().BeSameAs(left);
        left.Index.PathOf(left.Selected!).Should().Be("chunks[0].data.width");
    }

    [Fact]
    public async Task RightPane_TabsSwitchViews_DiffTabOnlyWithDiff()
    {
        var (session, doc) = await SetupAsync();
        var cut = Render<RightPane>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".ptab").Should().HaveCount(2);
        cut.FindAll(".tree").Should().HaveCount(1);

        cut.FindAll(".ptab")[1].Click();
        cut.WaitForAssertion(() => cut.FindAll(".yaml").Should().HaveCount(1));
        session.Pane.Should().Be(PaneKind.Definition);
    }

    [Fact]
    public async Task StructureMap_SegmentWidthsProportional_ClickSelects()
    {
        var (_, doc) = await SetupAsync();
        var cut = Render<StructureMapView>(p => p.Add(x => x.Document, doc));
        var segs = cut.FindAll(".seg");
        segs.Should().HaveCount(4);
        segs.Select(s => s.QuerySelector(".lab")!.TextContent).Should().Equal("signature", "IHDR", "IDAT", "IEND");
        segs[0].GetAttribute("style").Should().Contain("--w:8");
        segs[1].GetAttribute("style").Should().Contain("--w:25");

        segs[1].Click();
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0]");
        cut.Render();
        cut.Find(".seg.sel .lab").TextContent.Should().Be("IHDR");
        cut.FindAll(".sub.sel").Count.Should().Be(4, "選択した struct 直下の全バンド");
    }

    [Fact]
    public async Task CommandBar_FormatChange_Redecodes_SearchSelects()
    {
        var (session, doc) = await SetupAsync();
        var cut = Render<CommandBar>(p => p.Add(x => x.Document, doc));
        cut.FindAll("select")[0].Change("bmp.bdef.yaml");
        cut.WaitForAssertion(() => doc.Format.File.Should().Be("bmp.bdef.yaml"));

        cut.FindAll("select")[0].Change("png.bdef.yaml");
        cut.WaitForAssertion(() => doc.Format.File.Should().Be("png.bdef.yaml"));

        cut.FindAll("select")[1].Change("little");
        doc.EndianOverride.Should().Be(Core.Models.Endianness.Little);
        cut.FindAll("select")[1].Change("");
        doc.EndianOverride.Should().BeNull();

        var input = cut.Find("input[type=text]");
        input.Input("**.width");
        input.KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].data.width");
        cut.Find(".search .count").TextContent.Should().Be("1/1");
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task GuiShell_RendersAllRegions_ForActiveDocument()
    {
        var (session, doc) = await SetupAsync();
        var cut = Render<GuiShell>();
        cut.FindAll(".tab").Should().HaveCount(2, "ファイルタブ + 追加ボタン");
        cut.Find(".tab.active").TextContent.Should().Contain("image.png");
        cut.FindAll(".cmdbar, .crumb, .hexwrap, .infobar, .pane, .map, .status").Should().HaveCount(7);
        cut.Find(".infobar").ClassList.Should().Contain("ok");
        cut.Find(".infobar").TextContent.Should().Contain("チェックサム 3 / 3");
        cut.Find(".status").TextContent.Should().Contain("PNG").And.Contain("ビッグエンディアン");

        doc.Select(doc.Index.ByPath("chunks[0].crc")!);
        cut.WaitForAssertion(() => cut.Find(".status .sel-info").TextContent.Should().Contain("chunks[0].crc"));

        cut.Find(".tab.add").Click();
        cut.WaitForAssertion(() => cut.FindAll(".picker").Should().HaveCount(1));
        session.ShowPicker.Should().BeTrue();
    }

    [Fact]
    public async Task InfoBar_ShowsErrors_WhenChecksumInvalid()
    {
        var (_, doc) = await SetupAsync();
        var bytes = TestPng.Bytes();
        bytes[^1] ^= 0xFF; // IEND crc を壊す
        var broken = new GuiDocument("broken.png", bytes, doc.Format);
        var cut = Render<InfoBar>(p => p.Add(x => x.Document, broken));
        cut.Find(".infobar").ClassList.Should().Contain("bad");
        cut.Find(".infobar").TextContent.Should().Contain("チェックサム 2 / 3");
    }

    [Fact]
    public async Task ParameterlessComponents_RerenderOnSessionChange()
    {
        var (session, _) = await SetupAsync();
        var title = Render<TitleBar>();
        var nav = Render<NavRail>();
        title.FindAll(".tab").Should().HaveCount(2);

        var second = await session.OpenAsync(new BinAnalyzer.Gui.Abstractions.OpenedFile("second.png", TestPng.Bytes(width: 2)));
        second.Should().NotBeNull();

        title.WaitForAssertion(() =>
        {
            title.FindAll(".tab").Should().HaveCount(3);
            title.Find(".tab.active").TextContent.Should().Contain("second.png");
        });

        session.Compare(session.Documents[0]);
        nav.WaitForAssertion(() => nav.FindAll(".nav")[3].HasAttribute("disabled").Should().BeFalse());
        var diff = Render<DiffView>();
        diff.FindAll(".diffrow").Should().HaveCount(2);
        session.ClearDiff();
        diff.WaitForAssertion(() => diff.FindAll(".diffrow").Should().BeEmpty());
    }
}
