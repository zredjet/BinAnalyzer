# REQ-134: Web UIコンポーネントのbUnitテスト

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-132 |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

REQ-132で追加したBlazer WebAssembly版（BinAnalyzer.Web）のHomeコンポーネントには29個のロジックパスがある。現在のテスト（FormatServiceTests + FormatFileSizeTests: 21件）はサービス層と純粋ロジックの9パスをカバーしているが、残りの20パスはコンポーネントのレンダリング・イベント処理・状態遷移に関するもので、bUnitなしではテストできない。

パスカバー率を31%（9/29）から100%（29/29）に引き上げるため、bUnitを導入してコンポーネントテストを追加する。

### カバレッジギャップの内訳

| カテゴリ | パス数 | 現状カバー | 未カバー |
|---|---|---|---|
| 純粋ロジック（FormatFileSize） | 4 | 4 | 0 |
| サービス層（FormatService） | 5 | 5 | 0 |
| 初期表示 | 4 | 0 | 4 |
| ファイル選択 | 4 | 0 | 4 |
| ボタン状態制御 | 3 | 0 | 3 |
| デコード実行 | 6 | 0 | 6 |
| ドラッグ&ドロップ | 2 | 0 | 2 |
| JS interop | 1 | 0 | 1 |
| **合計** | **29** | **9** | **20** |

## 機能要件

### 追加する機能

- [x] BinAnalyzer.Web.Tests に bUnit パッケージを追加
- [x] Home コンポーネントの初期表示テスト（C1〜C4: 4件）
- [x] ファイル選択の振る舞いテスト（D1〜D4: 4件）
- [x] デコードボタンの状態制御テスト（E1〜E3: 3件）
- [x] デコード実行テスト — 成功・各種エラー（F1〜F6: 6件）
- [x] ドラッグ&ドロップ CSS 制御テスト（G1〜G2: 2件）
- [x] JS interop 呼び出し検証テスト（H1: 1件）

### 変更する既存機能

なし。テストの追加のみ。

### 変更しないもの（スコープ外）

- BinAnalyzer.Web の本体コード変更 — テストのためのリファクタリングは行わない
- 他プロジェクトのテスト変更
- ブラウザ上の E2E テスト（Playwright 等）— bUnit のコンポーネント単体テストのみ

## 受入条件

1. [x] bUnit パッケージが BinAnalyzer.Web.Tests に追加されていること
2. [x] 初期表示テスト: フォーマットドロップダウンに全フォーマットが表示されること（C1）
3. [x] 初期表示テスト: ドロップゾーンにプレースホルダーテキストが表示されること（C2）
4. [x] 初期表示テスト: デコードボタンが disabled であること（C3）
5. [x] 初期表示テスト: エラー表示・結果エリアが非表示であること（C4）
6. [x] ファイル選択テスト: ファイル名・サイズが表示されること（D1）
7. [x] ファイル選択テスト: 拡張子でフォーマットが自動選択されること（D2）
8. [x] ファイル選択テスト: 未知拡張子ではフォーマット未選択のままであること（D3）
9. [x] ファイル選択テスト: 100MB超ファイルでエラーメッセージが表示されること（D4）
10. [x] ボタン状態テスト: ファイルなし＋フォーマット選択済→disabled（E1）
11. [x] ボタン状態テスト: ファイルあり＋フォーマット未選択→disabled（E2）
12. [x] ボタン状態テスト: ファイルあり＋フォーマット選択済→enabled（E3）
13. [x] デコードテスト: 成功時に iframe srcdoc が設定されること（F1）
14. [x] デコードテスト: 成功時に JSON ダウンロードボタンが表示されること（F2）
15. [x] デコードテスト: DecodeException 発生時にエラーメッセージが表示されること（F3）
16. [x] デコードテスト: 一般例外発生時にエラーメッセージが表示されること（F4）
17. [x] デコードテスト: バリデーションエラー時にエラーメッセージが表示されること（F5）
18. [x] デコードテスト: デコード中にスピナーが表示されること（F6）
19. [x] ドラッグテスト: dragover で drag-over クラスが付与されること（G1）
20. [x] ドラッグテスト: dragleave で drag-over クラスが除去されること（G2）
21. [x] JS interop テスト: JSON ダウンロード時に downloadFile が正しい引数で呼ばれること（H1）
22. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Compression | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| BinAnalyzer.Tui | 変更なし |
| BinAnalyzer.Web | 変更なし |
| **BinAnalyzer.Web.Tests** | bUnit パッケージ追加、テストクラス追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- bUnit v2 API を使用: `BunitContext`（`TestContext` は v2 で非推奨）、`Render<T>()`（`RenderComponent<T>()` は v2 で廃止）
- StubHttpHandler に非同期ゲート機構（`SetGate`/`Release`）と `HasRegistration` メソッドを追加（後方互換）
- D4テスト用に `FakeBrowserFile`（`IBrowserFile` スタブ）を新規作成

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| HomeComponentTests | C1_InitialRender_ShowsAllFormatsInDropdown | 受入条件2 |
| HomeComponentTests | C2_InitialRender_ShowsDropZonePlaceholder | 受入条件3 |
| HomeComponentTests | C3_InitialRender_DecodeButtonIsDisabled | 受入条件4 |
| HomeComponentTests | C4_InitialRender_NoStatusMessageOrResultArea | 受入条件5 |
| HomeComponentTests | D1_FileSelected_ShowsFileNameAndSize | 受入条件6 |
| HomeComponentTests | D2_PngFile_AutoSelectsFormat | 受入条件7 |
| HomeComponentTests | D3_UnknownExtension_NoFormatAutoSelected | 受入条件8 |
| HomeComponentTests | D4_OversizedFile_ShowsSizeError | 受入条件9 |
| HomeComponentTests | E1_NoFile_FormatSelected_ButtonDisabled | 受入条件10 |
| HomeComponentTests | E2_FileUploaded_NoFormat_ButtonDisabled | 受入条件11 |
| HomeComponentTests | E3_FileUploaded_FormatSelected_ButtonEnabled | 受入条件12 |
| HomeComponentTests | F1_DecodeSuccess_ShowsIframeSrcdoc | 受入条件13 |
| HomeComponentTests | F2_DecodeSuccess_ShowsJsonDownloadButton | 受入条件14 |
| HomeComponentTests | F3_DecodeException_ShowsDecodeError | 受入条件15 |
| HomeComponentTests | F4_GeneralException_ShowsGenericError | 受入条件16 |
| HomeComponentTests | F5_ValidationError_ShowsFormatDefinitionError | 受入条件17 |
| HomeComponentTests | F6_Decoding_ShowsSpinnerThenHides | 受入条件18 |
| HomeComponentTests | G1_DragOver_AddsDragOverClass | 受入条件19 |
| HomeComponentTests | G2_DragLeave_RemovesDragOverClass | 受入条件20 |
| HomeComponentTests | H1_JsonDownload_InvokesDownloadFile | 受入条件21 |

### 気づき・今後の課題
