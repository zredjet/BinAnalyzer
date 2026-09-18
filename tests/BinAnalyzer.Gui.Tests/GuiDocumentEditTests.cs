using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Patching;
using BinAnalyzer.Engine;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.State;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-169: 編集バッファ・チェックサム再計算・Undo / Redo・保存。</summary>
public sealed class GuiDocumentEditTests
{
    private static int WidthId(GuiDocument doc) => doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.width")!);

    [Fact]
    public async Task Preview_ShowsBytes_Dependencies_AndUnchanged()
    {
        var (_, doc) = await SessionFactory.WithPngAsync();
        var width = WidthId(doc);

        doc.EditText(width).Should().Be("1");
        var same = doc.PreviewEdit(width, "1");
        same.Editability.Kind.Should().Be(EditKind.Integer);
        same.Bytes.Should().Equal(new byte[] { 0, 0, 0, 1 });
        same.Unchanged.Should().BeTrue();
        same.CanApply.Should().BeFalse();
        same.Dependencies.Select(d => d.Path).Should().Equal("chunks[0].crc");

        var changed = doc.PreviewEdit(width, "0x102");
        changed.Bytes.Should().Equal(new byte[] { 0, 0, 1, 2 });
        changed.CanApply.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_OutOfRange_IsError_AndCannotApply()
    {
        var (_, doc) = await SessionFactory.WithPngAsync();
        var depth = doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.bit_depth")!);

        var preview = doc.PreviewEdit(depth, "300");
        preview.Bytes.Should().BeNull();
        preview.Error.Should().Contain("範囲外");
        preview.CanApply.Should().BeFalse();

        var outcome = doc.ApplyEdit(depth, "300");
        outcome.Success.Should().BeFalse();
        outcome.Error.Should().Contain("範囲外");
        doc.IsDirty.Should().BeFalse();
        doc.Data.Should().BeSameAs(doc.OriginalData);
    }

    [Fact]
    public async Task Apply_WritesBigEndian_RecalculatesCrc_AndUpdatesViews()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        var width = WidthId(doc);
        doc.Select(width);
        var events = 0;
        doc.Changed += () => events++;
        var dataEvents = 0;
        doc.DataChanged += () => dataEvents++;

        var outcome = doc.ApplyEdit(width, "2");

        outcome.Success.Should().BeTrue(outcome.Error);
        outcome.Record!.ChecksumFixes.Select(f => f.Path).Should().Equal("chunks[0].crc");
        doc.Data.Should().Equal(TestPng.Bytes(width: 2), "IHDR width と crc だけが変わり、結果は width=2 で生成した PNG と一致する");
        doc.OriginalData.Should().Equal(TestPng.Bytes(width: 1), "原本は不変");
        doc.Revision.Should().Be(1);
        events.Should().Be(1);
        dataEvents.Should().Be(1);

        // ツリー・インスペクター・ヘックスが更新されている
        ((DecodedInteger)doc.Selected!).Value.Should().Be(2);
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].data.width");
        doc.Summary.AllChecksumsValid.Should().BeTrue();
        doc.Hex.Build(1).Cells[3]!.Value.Value.Should().Be(2, "0x13 = IHDR width 最下位バイト");
        doc.IsDirty.Should().BeTrue();
        doc.CanUndo.Should().BeTrue();
        doc.CanRedo.Should().BeFalse();
        doc.IsFieldModified(width).Should().BeTrue();
        doc.LastEdit.Should().BeSameAs(outcome.Record);
        session.Active.Should().BeSameAs(doc);
    }

    [Fact]
    public async Task Apply_Enum_String_AndBytes()
    {
        var (_, doc) = await SessionFactory.WithPngAsync();

        var colorType = doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.color_type")!);
        doc.PreviewEdit(colorType, "6").Editability.Kind.Should().Be(EditKind.Enum);
        doc.ApplyEdit(colorType, "6").Success.Should().BeTrue();
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.color_type")!).EnumLabel.Should().Be("truecolor_alpha");
        doc.Summary.AllChecksumsValid.Should().BeTrue();

        var type = doc.Index.IdOf(doc.Index.ByPath("chunks[2].type")!);
        doc.PreviewEdit(type, "IEND").Editability.Kind.Should().Be(EditKind.String);
        doc.ApplyEdit(type, "IENX").Success.Should().BeTrue();
        ((DecodedString)doc.Index.ByPath("chunks[2].type")!).Value.Should().Be("IENX");
        doc.Summary.AllChecksumsValid.Should().BeTrue("IEND チャンクの crc も再計算される");

        var sig = doc.Index.IdOf(doc.Index.ByPath("signature")!);
        doc.PreviewEdit(sig, "89 50 4E 47 0D 0A 1A 0A").Editability.Kind.Should().Be(EditKind.Bytes);
        doc.ApplyEdit(sig, "89 50 4E 47 0D 0A 1A 0B").Success.Should().BeTrue();
        doc.Data[7].Should().Be(0x0B);
        doc.UndoCount.Should().Be(3);
    }

    [Fact]
    public async Task Undo_Redo_AndRevert_RestoreBytesAndTree()
    {
        var (_, doc) = await SessionFactory.WithPngAsync();
        var width = WidthId(doc);
        var height = doc.Index.IdOf(doc.Index.ByPath("chunks[0].data.height")!);
        doc.ApplyEdit(width, "2");
        doc.ApplyEdit(height, "3");
        doc.UndoCount.Should().Be(2);

        doc.Undo().Should().BeTrue();
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.height")!).Value.Should().Be(1);
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.width")!).Value.Should().Be(2);
        doc.Data.Should().Equal(TestPng.Bytes(width: 2));
        doc.CanRedo.Should().BeTrue();
        doc.LastEdit.Should().BeNull();

        doc.Undo().Should().BeTrue();
        doc.Data.Should().Equal(doc.OriginalData);
        doc.IsDirty.Should().BeFalse();
        doc.Summary.AllChecksumsValid.Should().BeTrue();
        doc.Undo().Should().BeFalse();

        doc.Redo().Should().BeTrue();
        doc.Data.Should().Equal(TestPng.Bytes(width: 2));
        doc.IsDirty.Should().BeTrue();
        doc.Redo().Should().BeTrue();
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.height")!).Value.Should().Be(3);
        doc.Redo().Should().BeFalse();

        // 「元に戻す」はフィールド単位で原本へ（履歴には積まれる）
        doc.RevertField(width).Should().NotBeNull();
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.width")!).Value.Should().Be(1);
        doc.Summary.AllChecksumsValid.Should().BeTrue();
        doc.IsFieldModified(width).Should().BeFalse();
        doc.RevertField(width).Should().BeNull("変更が無ければ何もしない");
        doc.RevertField(height).Should().NotBeNull();
        doc.Data.Should().Equal(doc.OriginalData);
        doc.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task Apply_ClearsRedoStack_AndNewEditAfterUndoIsDirty()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        var width = WidthId(doc);
        doc.ApplyEdit(width, "2");
        (await session.SaveAsync(doc)).Should().BeTrue();
        doc.IsDirty.Should().BeFalse();

        doc.Undo();
        doc.IsDirty.Should().BeTrue();
        doc.ApplyEdit(width, "5");
        doc.CanRedo.Should().BeFalse();
        doc.IsDirty.Should().BeTrue("保存時と同じ深さでも別の編集なら未保存");
    }

    [Fact]
    public async Task Save_PassesEditedBytes_AndSavedFileDecodesWithChange()
    {
        var files = new FakeFileSource();
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), files);
        var doc = (await session.OpenAsync(new OpenedFile("image.png", TestPng.Bytes(), "/data/image.png")))!;
        doc.ApplyEdit(WidthId(doc), "2");

        (await session.SaveAsync(doc)).Should().BeTrue();

        files.Saved.Should().ContainSingle();
        files.Saved[0].ChooseLocation.Should().BeFalse();
        files.Saved[0].File.FullPath.Should().Be("/data/image.png");
        files.Saved[0].Data.Should().Equal(TestPng.Bytes(width: 2));
        doc.IsDirty.Should().BeFalse();
        session.Message.Should().Contain("/data/image.png");

        // 保存したバイト列を元のフォーマットでデコードすると変更が反映され、チェックサムも正しい
        var root = new BinaryDecoder().Decode(files.Saved[0].Data, doc.Format.Definition);
        var index = Presentation.NodeIndex.Build(root);
        ((DecodedInteger)index.ByPath("chunks[0].data.width")!).Value.Should().Be(2);
        ((DecodedInteger)index.ByPath("chunks[0].crc")!).ChecksumValid.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAs_UpdatesNameAndPath_AndNoPathForcesChooser()
    {
        var files = new FakeFileSource { SaveAsPath = "/out/copy.png" };
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), files);
        var doc = (await session.OpenAsync(new OpenedFile("stdin.png", TestPng.Bytes())))!;
        doc.ApplyEdit(WidthId(doc), "2");

        (await session.SaveAsync(doc)).Should().BeTrue();
        files.Saved[0].ChooseLocation.Should().BeTrue("保存先が無いので選ばせる");
        doc.FullPath.Should().Be("/out/copy.png");
        doc.DisplayName.Should().Be("copy.png");
        doc.IsDirty.Should().BeFalse();

        files.CancelSave = true;
        doc.ApplyEdit(WidthId(doc), "3");
        (await session.SaveAsync(doc, chooseLocation: true)).Should().BeFalse();
        doc.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task Edit_InvalidatesDiffContainingDocument_AndSessionUndoRedoTargetActive()
    {
        var (session, left) = await SessionFactory.WithPngAsync(name: "a.png");
        var right = (await session.OpenAsync(new OpenedFile("b.png", TestPng.Bytes(width: 2))))!;
        session.Activate(left);
        session.Compare(right);
        session.Diff.Should().NotBeNull();

        left.ApplyEdit(WidthId(left), "2");
        session.Diff.Should().BeNull("編集でツリーが変わるので差分は無効");
        session.Pane.Should().Be(PaneKind.Structure);

        session.Undo();
        left.IsDirty.Should().BeFalse();
        session.Redo();
        left.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task Redecode_KeepsEditedBuffer()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        doc.ApplyEdit(WidthId(doc), "2");
        session.ChangeEndian(doc, Core.Models.Endianness.Little);
        doc.Data.Should().Equal(TestPng.Bytes(width: 2));
        session.ChangeEndian(doc, null);
        ((DecodedInteger)doc.Index.ByPath("chunks[0].data.width")!).Value.Should().Be(2);
        doc.IsDirty.Should().BeTrue();
    }
}
