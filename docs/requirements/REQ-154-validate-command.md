# REQ-154: binanalyzer validate コマンド

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-23 |
| 更新日 | 2026-02-23 |

## 背景・動機

フォーマット定義（.bdef.yaml）を作成・編集する際、現在はバイナリファイルを実際にデコードしてみるまで定義の誤りに気づけない。`FormatValidator` による静的検証（VALルール）は内部的に存在するが、CLI サブコマンドとして公開されていない。

フォーマット定義作成者のフィードバックループを短縮するため、定義ファイル単体での検証コマンドが必要である。

## 機能要件

### 追加する機能

- [x] `binanalyzer validate <フォーマットファイル>` サブコマンド
- [x] 全VALルールの検証結果を一覧表示（警告/エラーの区別）
- [x] 複数ファイル指定対応（`binanalyzer validate formats/*.bdef.yaml`）
- [x] 終了コード: エラー0件 → 0、エラーあり → 1（CI連携用）
- [x] `--format json` オプションで機械可読な出力

### 変更する既存機能

- [x] なし（既存の `FormatValidator` をそのまま活用）

### 変更しないもの（スコープ外）

- バイナリデータとの突合せ検証（それはデコード時の責務）
- 新しいVALルールの追加（別途対応）

## 受入条件

1. [x] `binanalyzer validate formats/png.bdef.yaml` で検証結果が表示されること
2. [x] エラーのあるフォーマット定義に対して適切なエラーメッセージと位置情報が出力されること
3. [x] 複数ファイルを指定した場合に全ファイルが検証されること
4. [x] エラーあり時に終了コード1が返ること
5. [x] `--format json` で JSON 形式の出力が得られること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（既存の `FormatValidator` を利用） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | `validate` サブコマンド追加 |

### 変更が必要なドキュメント

- [x] docs/cli-usage.md — validate コマンドの説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- 既存の `FormatValidator.Validate()` と `YamlFormatLoader.Load()` をそのまま活用し、新規のバリデーションロジックは追加しない
- validate コマンド専用の出力ロジックを `Program.cs` 内に実装（`CliErrorReporter` は変更しない）。理由: `CliErrorReporter` は stderr 出力前提だが、validate コマンドは結果を stdout に出力するのが自然なため
- JSON 出力は `System.Text.Json.Utf8JsonWriter` を使用し、既存の `CliErrorReporter` のパターンに倣う
- `schema` サブコマンドの実装パターンに準拠

### モデル変更

なし。既存の `ValidationResult` / `ValidationDiagnostic` をそのまま使用。

### インタフェース変更

なし。`Program.cs` に validate サブコマンドを追加するのみ。

### 代替案

- `CliErrorReporter` に validate 用メソッドを追加する案 → stdout/stderr の使い分けが複雑になるため不採用
- `--format` ではなく `-o` オプションを使う案 → ルートコマンドの `-o` と衝突するため `--format` を採用

### 懸念事項

- `--format` オプション名がルートコマンドの `-f, --format`（フォーマット定義ファイル指定）と名前が同じだが、System.CommandLine のサブコマンドスコープにより正しく分離される（`schema` コマンドの `-o` と同じパターン）

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- `--warnings-as-errors` オプションを追加。計画段階では CLI 構文に含まれていたが、受入条件には明記されていなかった。警告をエラー扱いにして終了コード1を返す CI 用途向け機能
- 複数ファイル時のみサマリー行を表示する仕様を追加（単一ファイル時は冗長なため省略）
- YAML パースエラーやファイル未存在エラーは `PARSE` コードで統一的に報告する設計とした

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ValidateCommandTests | ValidFormat_ExitCode0 | 1 |
| ValidateCommandTests | ValidFormat_OutputContainsOk | 1 |
| ValidateCommandTests | InvalidFormat_ExitCode1 | 2, 4 |
| ValidateCommandTests | InvalidFormat_OutputContainsErrorCode | 2 |
| ValidateCommandTests | MultipleFiles_AllValidated | 3 |
| ValidateCommandTests | MultipleFiles_OneInvalid_ExitCode1 | 3, 4 |
| ValidateCommandTests | NonexistentFile_ExitCode1 | 2, 4 |
| ValidateCommandTests | JsonFormat_ValidJson | 5 |
| ValidateCommandTests | JsonFormat_ContainsFileAndValid | 5 |
| ValidateCommandTests | WarningsAsErrors_WarningCausesExitCode1 | — |
| ValidateCommandTests | ParseError_ReportsGracefully | 2 |

### 気づき・今後の課題

- CLI テストで `dotnet run --project` を使うと、`dotnet test` の MSBuild ビルドサーバーとロック競合が発生し、テストがハングする問題を発見。`dotnet run --no-build` に変更して解決（ビルド評価オーバーヘッド: 2.35秒/回 → 0.56秒/回）
- PipelineTests と ValidateCommandTests を `[Collection("CliTests")]` で同一コレクションに配置し、xunit の並列実行による CLI プロセス競合を防止
- PipelineTests は stdin リダイレクトや broken pipe テストの都合上 `dotnet run --project`（ビルド付き）を維持しているが、将来的に `--no-build` 化を検討してもよい
