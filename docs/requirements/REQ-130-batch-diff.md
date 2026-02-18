# REQ-130: バッチdiff

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-129 |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-18 |

## 背景・動機

現在のdiffは2つのファイルの1対1比較のみ。ファームウェア更新の検証、リグレッションテスト、バイナリリソースの一括検査等では、ディレクトリ内の複数ファイルをまとめて比較し、レポートを出力したいニーズがある。

REQ-129（diff統計サマリー）と組み合わせることで、多数のファイルの差分概要を一覧化できる。

## 機能要件

### 追加する機能

- [x] ディレクトリ指定によるバッチdiff（`diff <dir1> <dir2> -f <format>`）
- [x] ファイル名マッチングによるペアリング（同名ファイル同士を比較）
- [x] バッチ結果のサマリーレポート出力
  - ファイルごとの差分有無
  - ファイルごとのChanged / Added / Removed件数
  - 片方にのみ存在するファイルの一覧
- [x] 終了コード: 0=全ファイル同一、1=1つ以上差分あり

### 変更する既存機能

- [x] diff サブコマンド — ディレクトリ引数の受け付け

### 変更しないもの（スコープ外）

- 再帰的なサブディレクトリ走査 — 将来課題
- ファイル名パターンによるフィルタリング — 将来課題
- HTML形式のバッチレポート出力 — 将来課題
- 異なるフォーマット定義の混在（全ファイル同一フォーマット前提）

## 受入条件

1. [x] 2つのディレクトリを指定してバッチdiffが実行できること
2. [x] 同名ファイル同士が正しくペアリングされて比較されること
3. [x] 片方にのみ存在するファイルがレポートに含まれること
4. [x] 全ファイル同一の場合に終了コード0が返ること
5. [x] 1つ以上差分がある場合に終了コード1が返ること
6. [x] 空ディレクトリの指定が適切にハンドリングされること
7. [x] 既存の2ファイル比較（非ディレクトリ引数）が引き続き動作すること
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `BatchDiffResult` / `BatchDiffFileEntry` モデル追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし（ペアリング・反復実行はCLI側で実装） |
| BinAnalyzer.Output | `BatchDiffSummaryFormatter` 追加 |
| BinAnalyzer.Cli | diffサブコマンドの `Argument<string>` 化、バッチモード分岐追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — CLI使用法のdiffセクション更新
- [x] docs/cli-usage.md — diff セクションにバッチモード記載追加

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- diff サブコマンドの引数を `Argument<FileInfo>` → `Argument<string>` に変更し、ファイル/ディレクトリ両方を受け付ける
- 両方ディレクトリならバッチモード、両方ファイルなら既存の1対1 diff、混在はエラー
- バッチモード時は `--output`, `--summary`, `--summary-only` を無視し、バッチレポートを出力
- ファイルペアリングは `Directory.GetFiles()` の非再帰取得 + ファイル名突合
- 各ファイルのデコードエラーは個別にキャッチし、バッチ全体を中断しない

### モデル変更

- `BatchDiffResult` — バッチ比較結果全体（FileEntries, LeftOnlyFiles, RightOnlyFiles）
- `BatchDiffFileEntry` — ファイルごとの比較結果（FileName, Statistics, HasDifferences, HasError, ErrorMessage）
- REQ-129 の `DiffStatistics` をファイルごとの統計に再利用

### インタフェース変更

- `BatchDiffSummaryFormatter.Format(BatchDiffResult)` — バッチレポート文字列を生成

### 代替案

- ペアリング・反復実行を Engine 層に置く案も検討したが、CLI 固有のロジック（ディレクトリ判定、ファイル一覧取得）が多いため CLI 側に実装

### 懸念事項

- 大量ファイル時のメモリ使用量（将来的にストリーミング出力を検討）

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- 当初 Engine 層にバッチ制御ロジックを置く予定だったが、CLI 側に実装（ディレクトリ判定・ファイル一覧取得がCLI固有のため）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BatchDiffResultTests | HasDifferences_AllIdentical_ReturnsFalse | AC4 |
| BatchDiffResultTests | HasDifferences_FileDiffExists_ReturnsTrue | AC5 |
| BatchDiffResultTests | HasDifferences_LeftOnlyExists_ReturnsTrue | AC3,AC5 |
| BatchDiffResultTests | HasDifferences_RightOnlyExists_ReturnsTrue | AC3,AC5 |
| BatchDiffResultTests | HasDifferences_ErrorExists_ReturnsTrue | AC5 |
| BatchDiffOutputTests | BatchSummary_AllIdentical_ShowsAllIdenticalMessage | AC4 |
| BatchDiffOutputTests | BatchSummary_WithDifferences_ShowsDiffCounts | AC2 |
| BatchDiffOutputTests | BatchSummary_LeftOnlyFiles_ShowsList | AC3 |
| BatchDiffOutputTests | BatchSummary_RightOnlyFiles_ShowsList | AC3 |
| BatchDiffOutputTests | BatchSummary_ErrorFile_ShowsErrorStatus | — |
| BatchDiffOutputTests | BatchSummary_MixedResults_ShowsCorrectTotals | AC2,AC3 |
| BatchDiffOutputTests | BatchSummary_EmptyDirectories_HandlesGracefully | AC6 |

### 気づき・今後の課題

- 再帰的サブディレクトリ走査の対応（将来）
- ファイル名パターンフィルタリング（将来）
- 大量ファイル時のストリーミング出力（将来）
