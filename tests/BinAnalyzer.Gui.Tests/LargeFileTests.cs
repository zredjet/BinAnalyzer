using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.Desktop;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-177: 閾値を超えるファイルは開く前に確認する。</summary>
public sealed class LargeFileTests : BunitContext
{
    private static GuiSession NewSession(long? threshold)
        => new(FakeFormatCatalog.WithPngAndBmp(), new FakeFileSource()) { LargeFileThreshold = threshold };

    [Fact]
    public async Task OverThreshold_BecomesPending_ConfirmOpens()
    {
        var session = NewSession(threshold: 10);
        var changed = 0;
        session.Changed += () => changed++;

        var result = await session.OpenAsync(new OpenedFile("big.png", TestPng.Bytes()));

        result.Should().BeNull();
        session.PendingLarge.Should().NotBeNull();
        session.PendingLarge!.File.Name.Should().Be("big.png");
        session.Documents.Should().BeEmpty();
        session.ShowPicker.Should().BeTrue();
        changed.Should().Be(1);

        var doc = await session.ConfirmLargeFileAsync();
        doc.Should().NotBeNull();
        session.PendingLarge.Should().BeNull();
        session.Active.Should().BeSameAs(doc);
        doc!.Format.File.Should().Be("png.bdef.yaml", "確認後は通常どおり拡張子で検出する");
    }

    [Fact]
    public async Task Cancel_DiscardsPending_AndKeepsActiveTab()
    {
        var (session, existing) = await SessionFactory.WithPngAsync();
        session.LargeFileThreshold = 10;

        (await session.OpenAsync(new OpenedFile("big.png", TestPng.Bytes()))).Should().BeNull();
        session.ShowPicker.Should().BeTrue();

        session.CancelLargeFile();
        session.PendingLarge.Should().BeNull();
        session.ShowPicker.Should().BeFalse();
        session.Active.Should().BeSameAs(existing);
        session.Documents.Should().ContainSingle();
    }

    [Fact]
    public async Task AtOrUnderThreshold_OrDisabled_OpensDirectly()
    {
        var size = TestPng.Bytes().Length;
        (await NewSession(threshold: size).OpenAsync(new OpenedFile("eq.png", TestPng.Bytes()))).Should().NotBeNull("閾値ちょうどは確認しない");
        (await NewSession(threshold: null).OpenAsync(new OpenedFile("off.png", TestPng.Bytes()))).Should().NotBeNull("null は無効");
    }

    [Fact]
    public async Task UnknownExtension_AfterConfirm_DoesNotAskAgainWhenFormatChosen()
    {
        var session = NewSession(threshold: 10);
        (await session.OpenAsync(new OpenedFile("mystery.bin", TestPng.Bytes()))).Should().BeNull();
        session.PendingLarge.Should().NotBeNull();

        (await session.ConfirmLargeFileAsync()).Should().BeNull("フォーマット未検出なので選択待ちになる");
        session.PendingLarge.Should().BeNull();
        session.PendingFile.Should().NotBeNull();

        var doc = await session.OpenPendingAsync("png.bdef.yaml");
        doc.Should().NotBeNull("大きさの確認は済んでいるので再確認しない");
        session.PendingLarge.Should().BeNull();
    }

    [Fact]
    public async Task FilePicker_ShowsConfirmation_ButtonsConfirmAndCancel()
    {
        var session = NewSession(threshold: 10);
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        await session.OpenAsync(new OpenedFile("big.png", TestPng.Bytes()));

        var cut = Render<FilePicker>();
        var dialog = cut.Find(".picker-large");
        dialog.TextContent.Should().Contain("big.png").And.Contain("大きい");
        var buttons = dialog.QuerySelectorAll("button");
        buttons.Select(b => b.TextContent).Should().Equal("開く", "キャンセル");

        buttons[1].Click();
        cut.WaitForAssertion(() => cut.FindAll(".picker-large").Should().BeEmpty());
        session.Documents.Should().BeEmpty();

        await session.OpenAsync(new OpenedFile("big.png", TestPng.Bytes()));
        cut.WaitForAssertion(() => cut.FindAll(".picker-large").Should().HaveCount(1));
        cut.Find(".picker-large button").Click();
        cut.WaitForAssertion(() => session.Documents.Should().ContainSingle());
    }

    [Theory]
    [InlineData(null, GuiSession.DefaultLargeFileThreshold)]
    [InlineData("", GuiSession.DefaultLargeFileThreshold)]
    [InlineData("abc", GuiSession.DefaultLargeFileThreshold)]
    [InlineData("-5", GuiSession.DefaultLargeFileThreshold)]
    [InlineData("0", null)]
    [InlineData("512", 512L * 1024 * 1024)]
    public void DesktopThreshold_ReadsEnvironment(string? value, long? expected)
    {
        var previous = Environment.GetEnvironmentVariable("BINANALYZER_GUI_LARGE_FILE_MB");
        try
        {
            Environment.SetEnvironmentVariable("BINANALYZER_GUI_LARGE_FILE_MB", value);
            GuiApp.LargeFileThresholdFromEnvironment().Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BINANALYZER_GUI_LARGE_FILE_MB", previous);
        }
    }
}
