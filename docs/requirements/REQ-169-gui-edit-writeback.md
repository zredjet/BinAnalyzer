# REQ-169: GUI での値の編集・書き戻し

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-164（patch コマンド）、REQ-168（GUI モード） |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI モード（REQ-168）は閲覧専用である。モックアップ（Prism）のインスペクターには「値の書き換え → 書き込みバイトのプレビュー → 適用 / 元に戻す」と「依存するチェックサムの再計算」が含まれており、これが「構造を見るバイナリエディタ」の核となる体験である。

エンジンは読み取り専用で、フィールド値のエンコード（値 → バイト列）と書き戻しの機構が無い。REQ-164（`patch` コマンド）がその基盤を定義しているため、本要望は REQ-164 の実装後に GUI 側を載せる。

## 機能要件

### 追加する機能

- [x] インスペクターの「値」行を編集可能にする（整数・浮動小数点・固定長文字列・固定長バイト列。enum は選択肢から選ぶ）
- [x] 入力中に「書き込みバイト」のプレビューを表示（エンディアン・サイズ・範囲外エラーを反映）
- [x] 「適用」でメモリ上のデータを書き換え、再デコードして全ビューを更新。「元に戻す」で直前の状態へ
- [x] 変更範囲を含むチェックサムフィールド（`checksum:` 定義）を自動再計算して書き戻す。再計算対象をインスペクターに事前表示
- [x] 変更があるタブにマーク（`●`）を表示し、「保存」「名前を付けて保存」（デスクトップ: ネイティブダイアログ、Web: ダウンロード）
- [x] 編集履歴（Undo / Redo、Ctrl+Z / Ctrl+Y）

### 変更する既存機能

- [x] `GuiDocument` に「編集バッファ」と「変更セット」を追加。`Data` は不変とし、編集後のバイト列を別に持つ
- [x] `IFileSource` に保存メソッド（`SaveAsync(OpenedFile, byte[], chooseLocation)`）を追加

### 変更しないもの（スコープ外）

- サイズが変わる編集（可変長文字列・配列要素の追加削除）
- 圧縮領域内の編集（再圧縮）
- `switch` の分岐キーを変えた場合の構造再構築以上のこと（再デコードで自然に追随する範囲のみ）

## 受入条件

1. [x] 整数フィールドの値を変更して適用すると、対応するバイトが正しいエンディアンで書き換わり、ツリー・ヘックス・インスペクターが更新されること
2. [x] 範囲外の値（`u8` に 300 等）はプレビューでエラーになり適用できないこと
3. [x] 変更範囲を含むチェックサムが再計算され、検証が ✓ のままであること（PNG の IHDR width 変更 → crc 再計算）
4. [x] 「元に戻す」「Undo/Redo」で元のバイト列と表示に戻ること
5. [x] 保存したファイルを CLI でデコードすると変更が反映されていること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 値エンコードのインタフェース（REQ-164 と共有） |
| BinAnalyzer.Engine | フィールドエンコーダ、チェックサム再計算（REQ-164 と共有） |
| BinAnalyzer.Presentation | 編集可否・依存チェックサムの導出 |
| BinAnalyzer.Gui | インスペクター編集 UI、編集バッファ、Undo/Redo、保存 |
| BinAnalyzer.Gui.Desktop / Web | `IFileSource.SaveAsync` |

### 変更が必要なドキュメント

- [x] docs/cli-usage.md — `### gui` に編集操作を追記
- [x] README.md — 機能一覧
- [x] docs/architecture.md — Core/Patching・Engine の書き戻し、GuiDocument の編集バッファ

---

## 設計メモ

### 設計方針

- **REQ-164（`patch` コマンド）の基盤部分を本要望で先行実装する。** 値のエンコード（`FieldEncoder`）とパッチ適用＋チェックサム再計算（`BinaryPatcher`）を Core / Engine に置き、GUI はそれを呼ぶだけにする。CLI の `patch` サブコマンド自体は REQ-164 の範囲として残す。
- **編集に必要なメタデータをデコード結果ノードに持たせる。** 既存の `DecodedInteger` は値とサイズしか持たず、符号・エンディアン・enum 名が分からない。フォーマット定義を再走査してフィールド定義を探す案は switch / テンプレート / 配列で壊れやすいので、エンジンがデコード時に `DslType`（DSL の型）、`Endianness`（実効エンディアン）、`EnumRef` をノードに記録する。REQ-171 の `TypeName` はこの `DslType` から導出できる。
- **チェックサムの依存関係は「算出対象バイト範囲」で判定する。** `ChecksumSpec` の `fields:` / `range:` / `ranges:` を再評価するのではなく、エンジンが検証時に実際に読んだ範囲を `ChecksumCoverage`（`ByteRange` の列）としてノードに残す。GUI はこれと編集範囲の重なりだけで「再計算対象」を事前表示できる（式評価不要）。
- **再計算はエンジンの検証結果を流用する。** パッチ適用後に再デコードすると、影響を受けたチェックサムは `ChecksumValid=false` かつ `ChecksumExpected`（整数）/ `ChecksumExpectedHex`（ハッシュ）に正しい値が入る。`BinaryPatcher` はそれを該当フィールドのサイズ・エンディアンで書き戻し、変更範囲を広げてもう一度デコードする。チェックサムが別のチェックサムの範囲に含まれる（入れ子）場合も、変化が無くなるまで（上限 8 回）反復するだけで収束する。アルゴリズムごとの計算コードを二重に持たない。
- **編集バッファは copy-on-write。** `GuiDocument.Data` は「現在デコード・表示しているバイト列」で、編集のたびに新しい配列に差し替える（配列を書き換えない）。読み込んだ原本は `OriginalData` に保持し、「元に戻す」はここから該当範囲を戻す。ヘックス / インスペクター / ステータスバーは従来どおり `Data` を読むだけで編集後の状態を映す。
- **Undo / Redo は「バイト書き込みの束」で表現する。** 1 回の適用 = フィールド本体の書き込み＋再計算したチェックサムの書き込みを 1 つの `EditRecord` にまとめ、`Before` / `After` を持つ。Undo は `Before` を、Redo は `After` を書き戻して再デコードする。保存済みかどうかはスタック先頭のレコード同一性で判定する（深さだけだと「保存 → Undo → 別の編集」を見逃す）。
- **キーボードショートカットは JS 側で捕捉して .NET に渡す。** `gui.js` が `document` の keydown で Ctrl+Z / Ctrl+Y（Ctrl+Shift+Z）/ Ctrl+S を拾い、`DotNetObjectReference` 経由で `GuiShell` に通知する。入力欄にフォーカスがある間はブラウザ標準の undo を邪魔しないよう無視する。

### モデル変更

- `DecodedNode.DslType: FieldType?` — DSL の型。エンジンが整数・浮動小数点・文字列・バイト列・可変長整数・フラグ・ビットフィールドに設定する。
- `DecodedInteger.Endianness: Endianness?` / `DecodedInteger.EnumRef: string?` / `DecodedInteger.ChecksumCoverage: IReadOnlyList<ByteRange>?`
- `DecodedFloat.Endianness: Endianness?`
- `DecodedBytes.ChecksumCoverage: IReadOnlyList<ByteRange>?`
- Core `Patching/`: `ByteRange(Offset, Size)`、`BytePatch(Offset, Bytes)`、`FieldEncodeResult`、`IFieldEncoder`

### インタフェース変更

- Core: `IFieldEncoder.Encode(DecodedNode node, string input) → FieldEncodeResult`
- Engine: `FieldEncoder`（`IFieldEncoder` 実装、`InitialText(node)` で編集欄の初期文字列も返す）、`BinaryPatcher.Apply(data, format, options, patches) → PatchOutcome(Data, ChecksumFixes)`
- Presentation: `FieldEditability.Of(index, id) → EditabilityInfo(Kind, Reason)`、`ChecksumDependencies.Covering(index, range) → IReadOnlyList<ChecksumDependency>`
- Gui: `IFileSource.SaveAsync(OpenedFile file, byte[] data, bool chooseLocation) → Task<OpenedFile?>`（null はキャンセル）。`GuiDocument.PreviewEdit / ApplyEdit / RevertField / Undo / Redo / IsDirty / OriginalData`、`GuiSession.SaveAsync(doc, chooseLocation) / Undo / Redo`
- Web: `BrowserFileSource` が `IJSRuntime` を受け取り、`downloadFile` でダウンロードする

### 代替案

- フォーマット定義からフィールド定義を逆引きして符号・エンディアンを得る案 — switch の分岐や配列要素、テンプレート引数で同じパスが別定義に対応するため見送り。
- チェックサム再計算をアルゴリズム別に GUI 側で実装する案 — 17 アルゴリズム分の分岐と `exclude_self` / `ranges:` の再現が重複するので、再デコードで得た期待値を書き戻す方式にした。
- 編集バッファをヘックスビューの直接編集にも開放する案 — 本要望はインスペクター経由の「意味を分かった上での編集」に絞る（スコープ外）。

### 懸念事項

- チェックサムの `fields:` 参照範囲が編集範囲を含むかの判定は、デコード結果のオフセットで行える（IR の式評価は不要）。
- 再デコードで構造が変わる編集（length を変える等）は「後続の全オフセットが再計算される」ことを警告表示する。
- 固定長文字列は「エンコード後のバイト数 ≤ フィールドサイズ」を許し、不足分は 0x00 で埋める。超過はエラー。
- 圧縮ストリーム内のノード（`NodeIndex.IsInFileSpace` が false）とビットストリームフィールド、可変長整数（LEB128 / VLQ）、NUL 終端文字列は編集不可として理由を表示する。

---

## 実装メモ

### 実装中の設計変更

- `IFileSource.SaveAsync` は要件案の `(OpenedFile, byte[])` に `chooseLocation` を足した。「保存」と「名前を付けて保存」を同じメソッドで扱い、保存先が無い（標準入力から開いた）場合も `GuiSession` 側で true にして選ばせる。戻り値は保存後の `OpenedFile`（名前・パス）で、キャンセルは null。
- 要件の「`Data` は不変」は「配列インスタンスを書き換えない」と解釈した。`Data` を現在のバイト列にしておかないと、ヘックス / インスペクター / ステータスバーなど既存の全ビューを書き換える必要があり、新しいビューが誤って原本を読む事故も起きやすい。原本は `OriginalData` に分けた。
- チェックサム欄そのものをユーザーが書き換えた場合、`BinaryPatcher` はその値を尊重して再計算しない（`exclude_self` 付き `range:` では自分の範囲が算出対象に含まれるため、放置すると手入力が上書きされる）。
- 適用後の結果表示（「適用しました: … チェックサム再計算: …」）は `GuiDocument.LastEdit` に持たせ、選択変更・Undo / Redo で消す。
- 編集中の値欄は `Inspector` が `SelectedId` と `Revision` の組で初期化タイミングを判定する。入力途中でホバー等の再描画が走っても文字列を失わない。
- Undo / Redo / 保存 / 名前を付けて保存のボタンは当初コマンドバーに置いたが、1360px 幅で検索ボックスが右ペインに重なった（コマンドバーは既に幅いっぱい）ので、タイトルバー右側（タブの隣の空き領域）に移した。Web 版（Blazor WASM）で実際に PNG を開き、`width` の編集 → 適用 → crc 再計算 → Undo ボタン / Ctrl+Y / Ctrl+Z → enum セレクト表示までブラウザで確認済み。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Engine.Tests / FieldEncoderTests | `Encode_Integer_RespectsSizeSignAndEndianness`（10 ケース）、`Encode_Float32_BigAndLittle`、`Encode_Float64_LittleEndian`、`Encode_FixedString_PadsWithNul_AndRejectsOverflow`、`Encode_Utf16String_UsesEncodingByteLength`、`Encode_Bytes_ParsesHexWithSeparators_AndRequiresExactLength`、`InitialText_ReflectsCurrentValue` | 1 |
| Engine.Tests / FieldEncoderTests | `Encode_Integer_OutOfRange_Fails`（8 ケース）、`Encode_Integer_Unparseable_Fails`（5 ケース）、`CanEncode_RejectsVariableLength_Bitstream_AndMissingMetadata` | 2 |
| Engine.Tests / BinaryPatcherTests | `Decoder_RecordsChecksumCoverage_ForFieldsMode` / `ForRangeMode`、`Apply_UpdatesBytes_AndRecalculatesCoveringChecksum`、`Apply_NestedChecksums_IterateUntilStable`、`Apply_HashChecksum_BytesFieldIsRewritten`、`Apply_LittleEndianChecksumField_UsesFieldEndianness`、`Apply_LeavesUnrelatedInvalidChecksumAlone`、`Apply_ExplicitPatchOnChecksumField_IsNotOverwritten`、`Apply_WithoutRecalculation_LeavesChecksumStale`、`Apply_PatchOutOfBounds_Throws`、`Apply_NoPatches_ReturnsCopy` | 1, 3 |
| Presentation.Tests / EditabilityTests | `Of_ClassifiesNodes_AndExplainsWhyNot`、`Of_FlagsLengthFields_AsLayoutShifting`、`Covering_ReturnsChecksumsWhoseCoverageOverlaps`、`Covering_ExcludesRequestedNode` | 1, 3 |
| Gui.Tests / GuiDocumentEditTests | `Preview_ShowsBytes_Dependencies_AndUnchanged`、`Apply_WritesBigEndian_RecalculatesCrc_AndUpdatesViews`（結果が `TestPng.Bytes(width: 2)` と完全一致）、`Apply_Enum_String_AndBytes`、`Redecode_KeepsEditedBuffer` | 1, 3 |
| Gui.Tests / GuiDocumentEditTests | `Preview_OutOfRange_IsError_AndCannotApply` | 2 |
| Gui.Tests / GuiDocumentEditTests | `Undo_Redo_AndRevert_RestoreBytesAndTree`、`Apply_ClearsRedoStack_AndNewEditAfterUndoIsDirty`、`Edit_InvalidatesDiffContainingDocument_AndSessionUndoRedoTargetActive` | 4 |
| Gui.Tests / GuiDocumentEditTests | `Save_PassesEditedBytes_AndSavedFileDecodesWithChange`、`SaveAs_UpdatesNameAndPath_AndNoPathForcesChooser` | 5 |
| Gui.Tests / EditComponentTests（bUnit） | `Inspector_EditsInteger_PreviewsBytes_ListsChecksum_AndApplies`、`Inspector_OutOfRange_ShowsError_AndDisablesApply`、`Inspector_EnterApplies_EscapeResets_SelectionChangeResetsInput`、`Inspector_EnumUsesSelect_AndBytesUsesHex_AndNonEditableExplains`、`TitleBar_UndoRedoSave_ReflectDocumentState`、`TitleBar_OnWeb_ShowsDownloadInsteadOfSaveAs`、`TitleBar_MarksDirtyTab_AndGuiShellShortcutsUndoRedoSave` | 1, 2, 3, 4, 5（UI） |
| Cli.Tests / PatchedFileDecodeTests | `PatchedPng_DecodesWithCli_WidthChangedAndCrcValid`（`BinaryPatcher` で保存した PNG を `-o json` でデコード） | 5 |
| 全体 | `dotnet test` 1,570 件通過 | 6 |

### 気づき・今後の課題

- REQ-164（`patch` コマンド）は `FieldEncoder` + `BinaryPatcher` をそのまま使えば CLI 側は引数解析だけになる。`FieldEncoder` は enum ラベル（`color_type=truecolor`）を受け付けないので、CLI で欲しければ `EnumDefinition` を渡す口を足す。
- REQ-171（`TypeName`）は `DecodedNode.DslType` から導出できる。`NodeDisplayText.TypeLabel` を `DslType` 優先に変えるだけでよい。
- `DecodedFlags` / `DecodedBitfield` は編集不可のまま（ビット単位のトグル UI は別要望）。
- タブを閉じる際に未保存の確認ダイアログは出していない（GUI にモーダルの仕組みが無い）。`●` マークとステータスで気づけるが、REQ-174 以降で共通ダイアログを入れる際に対応する。
- 圧縮ストリーム内の編集（REQ-175 の後続）は、`DecodedCompressed.RawDecompressed` に対して同じ `BinaryPatcher` を当てて再圧縮する形で載せられる。
