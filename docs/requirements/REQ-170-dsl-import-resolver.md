# REQ-170: DSL インポート解決の抽象化（Web / WASM での imports 対応）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

`YamlFormatLoader.LoadFromString` は `imports:` を持つ定義を拒否し、`Load(path)` だけがファイルシステム経由でインポートを解決する。このため Web（WASM）では `imports` を使う 5 フォーマット（avi / heif / mp4 / webp / wav、および共通型ライブラリ REQ-155 を使うもの）が読めない。GUI モード（REQ-168）の Web ホストでも同じ制限が残っている。

インポートの取得手段（ファイル / HTTP / 埋め込み）をローダーから切り離せば、全ホストで同じフォーマット群を扱える。

## 機能要件

### 追加する機能

- [ ] `IImportResolver`（`Task<string?> ReadAsync(string basePath, string importPath)` 相当）を Core または Dsl に追加
- [ ] `YamlFormatLoader.LoadAsync(string yaml, string basePath, IImportResolver resolver)` を追加。循環インポート検出は既存ロジックを流用
- [ ] 既定実装 `FileImportResolver`（現在の挙動）と、Web 用 `HttpImportResolver`（`formats/` 配下を相対パスで取得）
- [ ] `HttpFormatCatalog`（Web）と `DirectoryFormatCatalog`（デスクトップ）を新 API に切り替え
- [ ] Web の `format-list.json` にインポートを含む定義を再掲載（現在除外されているものがあれば）

### 変更する既存機能

- [ ] `LoadFromString(yaml)` は互換維持（imports ありなら従来どおり例外）

### 変更しないもの（スコープ外）

- インポートの構文・マージ規則の変更

## 受入条件

1. [ ] `imports` を持つ定義を `HttpImportResolver` 経由で読み込み、`Load(path)` と同一の `FormatDefinition` になること
2. [ ] 循環インポートが従来どおり検出されること
3. [ ] Web GUI で `test.wav` / `test.mp4` を開いてデコードできること（bUnit + StubHttpHandler で共通型ライブラリの取得を検証）
4. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `IImportResolver` |
| BinAnalyzer.Dsl | `LoadAsync` + resolver 経由の解決 |
| BinAnalyzer.Web | `HttpImportResolver`、`HttpFormatCatalog` |
| BinAnalyzer.Gui.Desktop | `DirectoryFormatCatalog` |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — imports の解決手段を追記
- [ ] docs/architecture.md

---

## 設計メモ

### 設計方針
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
