using Bunit;
using BinAnalyzer.Web.Pages;
using BinAnalyzer.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Web.Tests;

public sealed class HomeComponentTests : BunitContext
{
    private const string FormatListJson = """
        [
          { "name": "PNG", "file": "png.bdef.yaml", "extensions": [".png"] },
          { "name": "JPEG", "file": "jpeg.bdef.yaml", "extensions": [".jpg", ".jpeg"] }
        ]
        """;

    private const string MinimalYaml = """
        name: Test
        endianness: big
        root: root
        structs:
          root:
            - name: magic
              type: u8
        """;

    private IRenderedComponent<Home> RenderHome(StubHttpHandler? handler = null)
    {
        handler ??= new StubHttpHandler();
        if (!handler.HasRegistration("formats/format-list.json"))
            handler.Register("formats/format-list.json", FormatListJson);

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new FormatService(http));
        JSInterop.Mode = JSRuntimeMode.Loose;

        return Render<Home>();
    }

    // ──────────────────────────────────────────────
    // C: 初期表示
    // ──────────────────────────────────────────────

    [Fact]
    public void C1_InitialRender_ShowsAllFormatsInDropdown()
    {
        var cut = RenderHome();

        var options = cut.FindAll("select option");
        options.Should().HaveCount(3); // placeholder + PNG + JPEG
        options[1].TextContent.Trim().Should().Be("PNG");
        options[2].TextContent.Trim().Should().Be("JPEG");
    }

    [Fact]
    public void C2_InitialRender_ShowsDropZonePlaceholder()
    {
        var cut = RenderHome();

        var dropZone = cut.Find(".drop-zone");
        dropZone.InnerHtml.Should().Contain("ファイルをドラッグ＆ドロップ");
    }

    [Fact]
    public void C3_InitialRender_DecodeButtonIsDisabled()
    {
        var cut = RenderHome();

        var button = cut.Find("button.btn-primary");
        button.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void C4_InitialRender_NoStatusMessageOrResultArea()
    {
        var cut = RenderHome();

        cut.FindAll(".status-message").Should().BeEmpty();
        cut.FindAll(".result-area").Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // D: ファイル選択
    // ──────────────────────────────────────────────

    [Fact]
    public void D1_FileSelected_ShowsFileNameAndSize()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x89], "test.bin"));

        cut.Find(".file-name").TextContent.Should().Be("test.bin");
        cut.Find(".file-size").TextContent.Should().Be("1 B");
    }

    [Fact]
    public void D2_PngFile_AutoSelectsFormat()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x89], "image.png"));

        var select = cut.Find("select");
        select.GetAttribute("value").Should().Be("png.bdef.yaml");
    }

    [Fact]
    public void D3_UnknownExtension_NoFormatAutoSelected()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x00], "data.xyz"));

        var select = cut.Find("select");
        select.GetAttribute("value").Should().BeEmpty();
    }

    [Fact]
    public async Task D4_OversizedFile_ShowsSizeError()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        var fakeFile = new FakeBrowserFile
        {
            Name = "huge.bin",
            Size = 200L * 1024 * 1024,
        };
        var args = new InputFileChangeEventArgs([fakeFile]);
        await cut.InvokeAsync(() => inputFile.Instance.OnChange.InvokeAsync(args));

        var error = cut.Find(".status-message.error");
        error.TextContent.Should().Contain("ファイルサイズが大きすぎます");
    }

    // ──────────────────────────────────────────────
    // E: ボタン状態制御
    // ──────────────────────────────────────────────

    [Fact]
    public void E1_NoFile_FormatSelected_ButtonDisabled()
    {
        var cut = RenderHome();

        // フォーマットを選択してもファイルがなければ disabled
        cut.Find("select").Change("png.bdef.yaml");

        cut.Find("button.btn-primary").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void E2_FileUploaded_NoFormat_ButtonDisabled()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        // .bin → 自動検出なし
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x00], "data.bin"));

        cut.Find("button.btn-primary").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void E3_FileUploaded_FormatSelected_ButtonEnabled()
    {
        var cut = RenderHome();
        var inputFile = cut.FindComponent<InputFile>();

        // .png → 自動検出あり → enabled
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x89], "image.png"));

        cut.Find("button.btn-primary").HasAttribute("disabled").Should().BeFalse();
    }

    // ──────────────────────────────────────────────
    // F: デコード実行
    // ──────────────────────────────────────────────

    [Fact]
    public void F1_DecodeSuccess_ShowsIframeSrcdoc()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);
        var cut = RenderHome(handler);

        // 1byte ファイルをアップロード（u8 デコード成功に必要）
        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        // デコード実行
        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll(".result-iframe").Count > 0);

        var iframe = cut.Find("iframe.result-iframe");
        iframe.GetAttribute("srcdoc").Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void F2_DecodeSuccess_ShowsJsonDownloadButton()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);
        var cut = RenderHome(handler);

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll("button.btn-secondary").Count > 0);

        var jsonBtn = cut.Find("button.btn-secondary");
        jsonBtn.TextContent.Should().Contain("JSON ダウンロード");
    }

    [Fact]
    public void F3_DecodeException_ShowsDecodeError()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);
        var cut = RenderHome(handler);

        // 空バイナリ → u8 読み込み失敗 → DecodeException
        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([], "image.png"));

        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll(".status-message.error").Count > 0);

        var error = cut.Find(".status-message.error");
        error.TextContent.Should().Contain("デコードエラー:");
    }

    [Fact]
    public void F4_GeneralException_ShowsGenericError()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", "not: valid: yaml: {{{");
        var cut = RenderHome(handler);

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll(".status-message.error").Count > 0);

        var error = cut.Find(".status-message.error");
        error.TextContent.Should().Contain("エラー:");
        error.TextContent.Should().NotContain("デコードエラー:");
        error.TextContent.Should().NotContain("フォーマット定義エラー:");
    }

    [Fact]
    public void F5_ValidationError_ShowsFormatDefinitionError()
    {
        const string invalidStructRefYaml = """
            name: Test
            endianness: big
            root: root
            structs:
              root:
                - name: child
                  type: struct
                  struct: nonexistent
            """;

        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", invalidStructRefYaml);
        var cut = RenderHome(handler);

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll(".status-message.error").Count > 0);

        var error = cut.Find(".status-message.error");
        error.TextContent.Should().Contain("フォーマット定義エラー:");
    }

    [Fact]
    public async Task F6_Decoding_ShowsSpinnerThenHides()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);
        handler.SetGate("formats/png.bdef.yaml");
        var cut = RenderHome(handler);

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        // デコード開始（HTTPブロック中）
        var decodeTask = cut.InvokeAsync(() => cut.Find("button.btn-primary").Click());

        // スピナーが表示されること
        cut.WaitForState(() => cut.FindAll(".spinner").Count > 0);
        cut.FindAll(".spinner").Should().HaveCount(1);

        // ゲート解放 → デコード完了
        handler.Release();
        await decodeTask;
        cut.WaitForState(() => cut.FindAll(".spinner").Count == 0);

        cut.FindAll(".spinner").Should().BeEmpty();
    }

    // ──────────────────────────────────────────────
    // G: ドラッグ&ドロップ
    // ──────────────────────────────────────────────

    [Fact]
    public void G1_DragOver_AddsDragOverClass()
    {
        var cut = RenderHome();

        var dropZone = cut.Find(".drop-zone");
        dropZone.TriggerEvent("ondragover", new DragEventArgs());

        cut.Find(".drop-zone").ClassList.Should().Contain("drag-over");
    }

    [Fact]
    public void G2_DragLeave_RemovesDragOverClass()
    {
        var cut = RenderHome();

        var dropZone = cut.Find(".drop-zone");
        dropZone.TriggerEvent("ondragover", new DragEventArgs());
        cut.Find(".drop-zone").ClassList.Should().Contain("drag-over");

        cut.Find(".drop-zone").TriggerEvent("ondragleave", new DragEventArgs());
        cut.Find(".drop-zone").ClassList.Should().NotContain("drag-over");
    }

    // ──────────────────────────────────────────────
    // H: JS interop
    // ──────────────────────────────────────────────

    [Fact]
    public void H1_JsonDownload_InvokesDownloadFile()
    {
        var handler = new StubHttpHandler();
        handler.Register("formats/format-list.json", FormatListJson);
        handler.Register("formats/png.bdef.yaml", MinimalYaml);
        var cut = RenderHome(handler);

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([0x42], "image.png"));

        cut.Find("button.btn-primary").Click();
        cut.WaitForState(() => cut.FindAll("button.btn-secondary").Count > 0);

        // JSON ダウンロードクリック
        cut.Find("button.btn-secondary").Click();

        var invocation = JSInterop.VerifyInvoke("downloadFile");
        invocation.Arguments.Should().HaveCount(3);
        invocation.Arguments[0].Should().Be("image.json"); // ファイル名
        invocation.Arguments[1].Should().Be("application/json"); // content-type
        invocation.Arguments[2].Should().NotBeNull(); // base64 data
    }
}
