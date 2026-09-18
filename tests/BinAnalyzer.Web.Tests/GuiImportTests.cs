using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using BinAnalyzer.Web.Services;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Web.Tests;

/// <summary>
/// REQ-170 受入条件 3: Web GUI（HttpFormatCatalog）で imports を持つ WAV / MP4 を開いてデコードできる。
/// 共通型ライブラリは StubHttpHandler 経由で取得されることを検証する。
/// </summary>
public sealed class GuiImportTests : BunitContext
{
    private const string FormatListJson = """
        [
          { "name": "WAV", "file": "wav.bdef.yaml", "extensions": [".wav"] },
          { "name": "MP4", "file": "mp4.bdef.yaml", "extensions": [".mp4", ".m4a"] }
        ]
        """;

    private static string RealFormat(string relative)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "formats", relative));

    private (GuiSession session, StubHttpHandler handler) CreateSession()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/wav.bdef.yaml", RealFormat("wav.bdef.yaml"));
        handler.Register("formats/mp4.bdef.yaml", RealFormat("mp4.bdef.yaml"));
        handler.Register("formats/common/riff.bdef.yaml", RealFormat("common/riff.bdef.yaml"));
        handler.Register("formats/common/isobmff.bdef.yaml", RealFormat("common/isobmff.bdef.yaml"));

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var session = new GuiSession(new HttpFormatCatalog(new FormatService(http)), new BrowserFileSource(JSInterop.JSRuntime));
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return (session, handler);
    }

    /// <summary>48 バイトの最小 PCM WAV（RIFF/WAVE + fmt + data）。</summary>
    private static byte[] TestWav()
    {
        var ms = new MemoryStream();
        void Ascii(string s) => ms.Write(System.Text.Encoding.ASCII.GetBytes(s));
        void U16(ushort v) => ms.Write(BitConverter.GetBytes(v));
        void U32(uint v) => ms.Write(BitConverter.GetBytes(v));
        Ascii("RIFF"); U32(40); Ascii("WAVE");
        Ascii("fmt "); U32(16);
        U16(1); U16(1); U32(8000); U32(8000); U16(1); U16(8);
        Ascii("data"); U32(4); ms.Write([0x80, 0x80, 0x80, 0x80]);
        return ms.ToArray();
    }

    /// <summary>最小 MP4（ftyp + moov{free} + mdat）。ftyp_box / container_box は common/isobmff 由来。</summary>
    private static byte[] TestMp4()
    {
        var ms = new MemoryStream();
        void Ascii(string s) => ms.Write(System.Text.Encoding.ASCII.GetBytes(s));
        void U32(uint v) => ms.Write([(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);
        U32(24); Ascii("ftyp"); Ascii("isom"); U32(0x200); Ascii("isom"); Ascii("mp41");
        U32(16); Ascii("moov"); U32(8); Ascii("free");
        U32(12); Ascii("mdat"); ms.Write([1, 2, 3, 4]);
        return ms.ToArray();
    }

    [Fact]
    public async Task OpenWav_ResolvesRiffLibraryOverHttp_AndDecodes()
    {
        var (session, handler) = CreateSession();

        var doc = await session.OpenAsync(new OpenedFile("test.wav", TestWav()));

        session.Message.Should().BeNull();
        doc.Should().NotBeNull();
        doc!.DecodeFailure.Should().BeNull();
        doc.Errors.Should().BeEmpty();
        doc.Format.Definition.Structs.Should().ContainKey("raw_data");
        doc.Index.ByPath("chunks[0].data.audio_format").Should().NotBeNull();
        doc.Index.ByPath("chunks[1].data.data").Should().NotBeNull();
        handler.RequestedUrls.Should().Contain("formats/common/riff.bdef.yaml");

        var cut = Render<GuiShell>();
        cut.Find(".gui-root").Should().NotBeNull();
        cut.Markup.Should().Contain("test.wav");
    }

    [Fact]
    public async Task OpenMp4_ResolvesIsobmffLibraryOverHttp_AndDecodes()
    {
        var (session, handler) = CreateSession();

        var doc = await session.OpenAsync(new OpenedFile("test.mp4", TestMp4()));

        session.Message.Should().BeNull();
        doc.Should().NotBeNull();
        doc!.DecodeFailure.Should().BeNull();
        doc.Errors.Should().BeEmpty();
        doc.Format.Definition.Structs.Should().ContainKey("ftyp_box").And.ContainKey("container_box");
        doc.Index.ByPath("boxes[0].box_data.major_brand").Should().NotBeNull();
        doc.Index.ByPath("boxes[1].box_data.children[0]").Should().NotBeNull();
        handler.RequestedUrls.Should().Contain("formats/common/isobmff.bdef.yaml");

        var cut = Render<GuiShell>();
        cut.Markup.Should().Contain("test.mp4");
    }

    [Fact]
    public async Task OpenWav_WhenLibraryMissing_ReportsResolvedUrl()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/wav.bdef.yaml", RealFormat("wav.bdef.yaml"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var session = new GuiSession(new HttpFormatCatalog(new FormatService(http)), new BrowserFileSource(JSInterop.JSRuntime));

        var doc = await session.OpenAsync(new OpenedFile("test.wav", TestWav()));

        doc.Should().BeNull();
        session.PendingFile.Should().NotBeNull();
        session.Message.Should().Contain("formats/common/riff.bdef.yaml");
    }
}
