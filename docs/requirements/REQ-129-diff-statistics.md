# REQ-129: diff統計サマリー

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | implemented |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-18 |

## 背景・動機

現在のdiff機能（REQ-101, REQ-103, REQ-104）は差分の詳細を一覧表示するが、「どの程度変わったのか」を定量的に把握する手段がない。大きなバイナリファイル（数百のチャンク・レコード）の比較では、まず全体像を掴んでから詳細を確認したい。

ファームウェア更新の検証、バイナリパッチの影響範囲確認、フォーマットバージョン間の差異の概要把握等のユースケースで統計情報が有用。

## 機能要件

### 追加する機能

- [x] diff実行時に統計サマリーを表示するオプション（`--summary` または `--stats`）
- [x] サマリーに含める情報
  - 変更フィールド数（Changed）
  - 追加フィールド数（Added）
  - 削除フィールド数（Removed）
  - 合計差分数
  - 一致率（変更なしフィールド数 / 全フィールド数）
- [x] サマリーのみ表示モード（詳細差分を省略して統計のみ出力）

### 変更する既存機能

- [x] diff サブコマンド — `--summary` / `--summary-only` オプションの追加
- [x] DiffSummaryFormatter 新設によるサマリー出力対応

### 変更しないもの（スコープ外）

- フィールドパス別の変更集計（「header配下で3件変更」等のグルーピング） — 将来課題
- バイトレベルの差分量（変更バイト数）の集計 — 将来課題

## 受入条件

1. [x] `--summary` オプション指定時に統計サマリーが表示されること
2. [x] Changed / Added / Removed の件数が正しいこと
3. [x] 差分なし（identical）の場合、件数が全て0で表示されること
4. [x] `--summary-only` 指定時は統計のみ表示、詳細差分は省略されること
5. [x] `--summary` なしの場合は既存動作と完全互換であること
6. [x] flat / tree 両方の出力形式でサマリーが動作すること
7. [x] 終了コードは既存仕様を維持（0: 同一、1: 差分あり）
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | DiffResultに統計集計メソッド追加（またはDiffStatisticsクラス新設） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | DiffEngineの結果から統計を集計するロジック |
| BinAnalyzer.Output | DiffOutputFormatter / DiffTreeOutputFormatterにサマリー出力追加 |
| BinAnalyzer.Cli | diffサブコマンドに `--summary` オプション追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — CLI使用法のdiffセクション更新

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- `DiffEngine` の全リーフ比較メソッドで `CompareContext` を通してフィールド数をカウント
- `DiffStatistics` レコード（Core）で Changed/Added/Removed/Unchanged を保持し、一致率を算出
- `DiffSummaryFormatter`（Output）でサマリー行をフォーマット
- CLI に `--summary`（詳細+サマリー）と `--summary-only`（サマリーのみ）を追加
- tree モードで `--summary` 指定時は内部的に `DiffEngine.Compare()` も実行して統計を取得

### モデル変更

- `DiffStatistics` レコード新設（Core/Diff）
- `DiffResult.Statistics` プロパティ追加
- `DiffEngine` 内部に `CompareContext` クラス導入、全 `Compare*` メソッドの引数を `List<DiffEntry>` → `CompareContext` に変更

### インタフェース変更

- `DiffEngine.Compare()` の戻り値 `DiffResult` に `Statistics` が常に設定される
- `DiffSummaryFormatter.Format(DiffStatistics)` メソッド新設

### 代替案

- `DiffResult.Entries` から事後的にカウント → 一致フィールド数が取れないため不採用
- `DiffTreeOutputFormatter` にも統計カウントを埋め込む → 複雑になるため、tree モードでは `DiffEngine.Compare()` を追加実行する方式を採用

### 懸念事項

- tree モードで `--summary` 指定時にデコード結果を2回走査する（tree表示 + DiffEngine.Compare）が、パフォーマンス影響は軽微

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- プラン通り実装。特に設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DiffStatisticsTests | Compare_IdenticalStructs_AllUnchanged | AC2, AC3 |
| DiffStatisticsTests | Compare_ChangedFields_CountsCorrectly | AC2 |
| DiffStatisticsTests | Compare_AddedFields_CountsCorrectly | AC2 |
| DiffStatisticsTests | Compare_RemovedFields_CountsCorrectly | AC2 |
| DiffStatisticsTests | Compare_MixedChanges_StatisticsCorrect | AC2 |
| DiffStatisticsTests | Compare_NestedStruct_CountsLeafFields | AC2 |
| DiffStatisticsTests | Compare_ArrayDiff_CountsElements | AC2 |
| DiffStatisticsTests | MatchRate_AllIdentical_Returns1 | AC3 |
| DiffStatisticsTests | MatchRate_NoFields_Returns1 | AC3 |
| DiffSummaryOutputTests | Summary_NoDifferences_ShowsZeroCounts | AC1, AC3 |
| DiffSummaryOutputTests | Summary_WithDifferences_ShowsCorrectCounts | AC1, AC2 |
| DiffSummaryOutputTests | Summary_ShowsMatchRate | AC1 |
| DiffSummaryOutputTests | SummaryOnly_OmitsDetailedDiff | AC4 |
| DiffSummaryOutputTests | SummaryWithDetails_ShowsBothOutputs | AC1 |
| DiffSummaryOutputTests | ExistingBehavior_NoSummaryFlag_Unchanged | AC5 |

### 気づき・今後の課題
