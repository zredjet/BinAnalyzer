using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-169: インスペクター編集 UI・タイトルバーの Undo / Redo / 保存・タブの未保存マーク。</summary>
public sealed class EditComponentTests : BunitContext
{
    private FakeFileSource _files = null!;

    private async Task<(GuiSession Session, GuiDocument Doc)> SetupAsync()
    {
        _files = new FakeFileSource();
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), _files);
        var doc = (await session.OpenAsync(new BinAnalyzer.Gui.Abstractions.OpenedFile("image.png", TestPng.Bytes(), "/data/image.png")))!;
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return (session, doc);
    }

    [Fact]
    public async Task Inspector_EditsInteger_PreviewsBytes_ListsChecksum_AndApplies()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        var cut = Render<Inspector>(p => p.Add(x => x.Document, doc));

        var input = cut.Find("input#gui-edit");
        input.GetAttribute("value").Should().Be("1");
        cut.Find(".preview").TextContent.Should().Contain("00 00 00 01").And.Contain("変更なし");
        cut.Find(".deps").TextContent.Should().Contain("chunks[0].crc").And.Contain("CRC-32");
        cut.Find("button.apply").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button.revert").HasAttribute("disabled").Should().BeTrue();

        input.Input("2");
        cut.Find(".preview").TextContent.Should().Contain("00 00 00 02");
        cut.Find(".preview").ClassList.Should().NotContain("err");
        cut.Find("button.apply").HasAttribute("disabled").Should().BeFalse();

        cut.Find("button.apply").Click();

        doc.Data.Should().Equal(TestPng.Bytes(width: 2));
        cut.Find(".affect.done").TextContent.Should().Contain("chunks[0].data.width = 2").And.Contain("chunks[0].crc");
        cut.Find(".exp-head .chip").TextContent.Should().Be("変更");
        cut.Find("input#gui-edit").GetAttribute("value").Should().Be("2");
        cut.Find("button.revert").HasAttribute("disabled").Should().BeFalse();

        cut.Find("button.revert").Click();
        doc.Data.Should().Equal(doc.OriginalData);
        cut.FindAll(".exp-head .chip").Should().BeEmpty();
    }

    [Fact]
    public async Task Inspector_OutOfRange_ShowsError_AndDisablesApply()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.bit_depth")!);
        var cut = Render<Inspector>(p => p.Add(x => x.Document, doc));

        cut.Find("input#gui-edit").Input("300");

        cut.Find(".preview").ClassList.Should().Contain("err");
        cut.Find(".preview").TextContent.Should().Contain("範囲外");
        cut.Find("button.apply").HasAttribute("disabled").Should().BeTrue();

        // Enter でも適用されない
        cut.Find("input#gui-edit").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        doc.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task Inspector_EnterApplies_EscapeResets_SelectionChangeResetsInput()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.height")!);
        var cut = Render<Inspector>(p => p.Add(x => x.Document, doc));

        cut.Find("input#gui-edit").Input("9");
        cut.Find("input#gui-edit").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.Find("input#gui-edit").GetAttribute("value").Should().Be("1");

        cut.Find("input#gui-edit").Input("7");
        cut.Find("input#gui-edit").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.height")!).Value.Should().Be(7);

        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        cut.Render();
        cut.Find("input#gui-edit").GetAttribute("value").Should().Be("1");
        cut.FindAll(".affect.done").Should().BeEmpty("選択が変わると結果表示は消える");
    }

    [Fact]
    public async Task Inspector_EnumUsesSelect_AndBytesUsesHex_AndNonEditableExplains()
    {
        var (_, doc) = await SetupAsync();
        doc.Select(doc.Index.ByPath("chunks[0].data.color_type")!);
        var cut = Render<Inspector>(p => p.Add(x => x.Document, doc));

        var select = cut.Find("select#gui-edit");
        select.QuerySelectorAll("option").Select(o => o.TextContent).Should().Contain("truecolor (2)");
        select.Change("6");
        cut.Find("button.apply").Click();
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.color_type")!).EnumLabel.Should().Be("truecolor_alpha");
        doc.Summary.AllChecksumsValid.Should().BeTrue();

        doc.Select(doc.Index.ByPath("signature")!);
        cut.Render();
        cut.Find("input#gui-edit").GetAttribute("value").Should().Be("89 50 4E 47 0D 0A 1A 0A");
        cut.Find("input#gui-edit").Input("89 50");
        cut.Find(".preview").TextContent.Should().Contain("8 バイト必要");

        doc.Select(doc.Index.ByPath("chunks[0]")!);
        cut.Render();
        cut.FindAll("#gui-edit").Should().BeEmpty();

        doc.Select(doc.Index.ByPath("chunks[1].data")!);
        cut.Render();
        cut.FindAll("#gui-edit").Should().BeEmpty("struct は編集不可");
        cut.FindAll("dt").Select(d => d.TextContent).Should().NotContain("編集", "構造体には理由行を出さない（子を編集する）");
    }

    [Fact]
    public async Task TitleBar_UndoRedoSave_ReflectDocumentState()
    {
        var (session, doc) = await SetupAsync();
        var cut = Render<TitleBar>();
        cut.Find(".cmd.undo").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".cmd.redo").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".cmd.save").HasAttribute("disabled").Should().BeTrue();
        cut.Find(".cmd.saveas").Should().NotBeNull();

        doc.ApplyEdit(doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.width")!), "2");
        cut.WaitForAssertion(() => cut.Find(".cmd.undo").HasAttribute("disabled").Should().BeFalse());
        cut.Find(".cmd.save").HasAttribute("disabled").Should().BeFalse();

        cut.Find(".cmd.undo").Click();
        doc.IsDirty.Should().BeFalse();
        cut.WaitForAssertion(() => cut.Find(".cmd.redo").HasAttribute("disabled").Should().BeFalse());
        cut.Find(".cmd.redo").Click();
        doc.IsDirty.Should().BeTrue();

        cut.Find(".cmd.save").Click();
        cut.WaitForAssertion(() => _files.Saved.Should().ContainSingle());
        _files.Saved[0].Data.Should().Equal(TestPng.Bytes(width: 2));
        cut.WaitForAssertion(() => doc.IsDirty.Should().BeFalse());
        session.Message.Should().Contain("保存しました");

        cut.WaitForAssertion(() => cut.Find(".cmd.saveas").Click());
        cut.WaitForAssertion(() => _files.Saved.Should().HaveCount(2));
        _files.Saved[1].ChooseLocation.Should().BeTrue();
    }

    [Fact]
    public async Task TitleBar_OnWeb_ShowsDownloadInsteadOfSaveAs()
    {
        var files = new FakeFileSource { SupportsNativePicker = false };
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), files);
        var doc = (await session.OpenAsync(new BinAnalyzer.Gui.Abstractions.OpenedFile("image.png", TestPng.Bytes())))!;
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<TitleBar>();
        cut.Find(".cmd.save").TextContent.Should().Contain("ダウンロード");
        cut.FindAll(".cmd.saveas").Should().BeEmpty();
        doc.Should().NotBeNull();
    }

    [Fact]
    public async Task TitleBar_MarksDirtyTab_AndGuiShellShortcutsUndoRedoSave()
    {
        var (session, doc) = await SetupAsync();
        var title = Render<TitleBar>();
        title.FindAll(".tab .dirty").Should().BeEmpty();

        var shell = Render<GuiShell>();
        doc.ApplyEdit(doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.width")!), "2");
        title.WaitForAssertion(() => title.Find(".tab.active .dirty").TextContent.Should().Be("●"));

        await shell.Instance.OnShortcut("undo");
        doc.IsDirty.Should().BeFalse();
        title.WaitForAssertion(() => title.FindAll(".tab .dirty").Should().BeEmpty());

        await shell.Instance.OnShortcut("redo");
        doc.IsDirty.Should().BeTrue();

        await shell.Instance.OnShortcut("save");
        _files.Saved.Should().ContainSingle();
        doc.IsDirty.Should().BeFalse();
        await shell.Instance.OnShortcut("save");
        _files.Saved.Should().ContainSingle("未変更なら保存しない");
        session.Active.Should().BeSameAs(doc);
    }
}
