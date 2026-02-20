# REQ-132: WebAssembly版

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-21 |

## 背景・動機

BinAnalyzerの機能をブラウザ上で利用できるようにする。CLIツールのインストールなしに、Webブラウザでバイナリファイルをドラッグ&ドロップして構造解析できる体験を提供する。

### ユースケース

- インストール不要のクイック解析（チーム内での共有・デモ）
- 非エンジニア向けのバイナリ構造確認ツール
- 教育用途（バイナリフォーマットの学習）

## 機能要件

### 追加する機能

- [x] Blazor WebAssemblyプロジェクトの作成
- [x] ファイルアップロード（ドラッグ&ドロップ / ファイル選択）
- [x] フォーマット定義の選択（組み込みフォーマット一覧から）
- [x] デコード結果のツリー表示（HTML出力フォーマッターの活用）
- [x] JSON出力のダウンロード

### 変更する既存機能

なし（新規プロジェクトの追加）

### 変更しないもの（スコープ外）

- サーバーサイド処理 — 全てクライアントサイド（WASM）で完結
- カスタムフォーマット定義のアップロード — 初期リリースでは組み込みのみ
- diff機能のWeb UI化 — 将来課題
- 大容量ファイル対応（ブラウザのメモリ制約あり）

## 受入条件

1. [x] Blazor WebAssemblyアプリケーションがビルドできること
2. [x] ブラウザでバイナリファイルをアップロードしてデコードできること
3. [x] 組み込みフォーマット定義から選択してデコードできること
4. [x] デコード結果がツリー形式でブラウザ上に表示されること
5. [x] 全ての処理がクライアントサイドで完結すること（サーバー通信なし）
6. [x] 主要ブラウザ（Chrome, Firefox, Safari, Edge）で動作すること
7. [x] 既存のCLI/テストに影響がないこと
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| **BinAnalyzer.Web（新規）** | Blazor WebAssemblyプロジェクト |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — Webコンポーネントの追記
- [x] CLAUDE.md — プロジェクト構成にWeb追記
- [x] README.md — Web版の使用法追加

---

## 設計メモ

### 設計方針

- Blazor WebAssembly Standalone（サーバーなし、静的サイトとしてデプロイ可能）
- フォーマット定義ファイル（35個の `.bdef.yaml`）は `wwwroot/formats/` に配置し `HttpClient` で取得
- デコード結果表示は `HtmlOutputFormatter` の出力を iframe srcdoc で埋め込み
- JSON出力ダウンロードは `JsonOutputFormatter` + JavaScript Blob URL

### モデル変更

なし。既存の Core / Dsl / Engine / Output / Compression は全て純粋マネージドC#で WASM 互換。

### インタフェース変更

なし。`IFormatLoader.LoadFromString(string yaml)` を使用してファイルシステムなしでフォーマット定義をロード。

### 代替案

- サーバーサイド Blazor → 静的デプロイ不可のため不採用
- JavaScript + REST API → .NETコード再利用できないため不採用

### 懸念事項

- YamlDotNet のトリミング問題 → `PublishTrimmed=false` で回避
- 大容量ファイルのメモリ制約 → 100MB上限を設定

---

## 実装メモ

### 実装中の設計変更

なし。計画通り実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FormatServiceTests | GetFormatListAsync_ReturnsAllFormats | 3 |
| FormatServiceTests | GetFormatListAsync_CachesResult | 3 |
| FormatServiceTests | DetectFormat_MatchesExtension | 3 |
| FormatServiceTests | DetectFormat_CaseInsensitive | 3 |
| FormatServiceTests | DetectFormat_MultipleExtensions_MatchesAny | 3 |
| FormatServiceTests | DetectFormat_NoMatch_ReturnsNull | 3 |
| FormatServiceTests | LoadFormatAsync_ReturnsFormatDefinition | 3 |
| FormatServiceTests | LoadFormatAsync_CachesResult | 3 |
| FormatFileSizeTests | Bytes_Range (4件) | 4 |
| FormatFileSizeTests | Kilobytes_Range (4件) | 4 |
| FormatFileSizeTests | Megabytes_Range (3件) | 4 |
| FormatFileSizeTests | Gigabytes_Range (2件) | 4 |

新規テスト21件 + 既存テスト全1,040件が全通過することで受入条件7-8を確認。

### 気づき・今後の課題

- カスタムフォーマット定義のアップロード対応（テキストエリアでYAML直接入力）
- エラー回復モード（`--on-error continue` 相当）のUI対応
- ファイルサイズが大きい場合のプログレス表示
- PWA対応（オフライン利用）
