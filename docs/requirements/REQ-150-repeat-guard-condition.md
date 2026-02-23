# REQ-150: repeatのガード条件

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-22 |

## 背景・動機

`repeat_count` による繰り返しでは、カウント値がバイナリデータから読み取られる。壊れたファイルや不正なデータでは、カウント値が異常に大きくなることがある（例: 破損したPNGでチャンク数が0xFFFFFFFF）。

現在のデコーダはカウント値をそのまま信用して繰り返すため、以下の問題が発生する。

1. **メモリ枯渇** — 数百万〜数十億回の繰り返し試行
2. **長時間のハング** — ユーザーに制御が返らない
3. **エラー回復の効果なし** — `on-error: continue` でも各要素のエラー回復後に次の要素を試行し続ける

### 対策の方針

`repeat_count` に上限ガード条件を設定し、カウント値が閾値を超えた場合やデコードエラーが連続した場合に繰り返しを打ち切る。

## 機能要件

### 追加する機能

- [x] `repeat_max` プロパティ — 繰り返し回数の上限を式で指定（`repeat_max: "1000"`）。カウントがこの値を超えたら打ち切り
- [x] `repeat_error_limit` プロパティ — 連続エラー回数の上限（`repeat_error_limit: "3"`）。連続して指定回数エラーが発生したら打ち切り
- [x] グローバルデフォルト値 — CLIオプション `--max-repeat N` でフォーマット定義に記載がない場合の上限を設定

### 変更する既存機能

- [x] `BinaryDecoder` — 繰り返しループ内のガード条件チェック追加
- [x] 打ち切り時は `DecodedArray` に打ち切り情報を付与（`Truncated = true`, `TruncationReason`）

### 変更しないもの（スコープ外）

- 正常なカウント値での繰り返しパフォーマンス — ガードチェックの影響を最小化

## 受入条件

1. [x] `repeat_max` を超えた場合に繰り返しが打ち切られること
2. [x] `repeat_error_limit` を超えた連続エラーで繰り返しが打ち切られること
3. [x] 打ち切られた配列の `Truncated` が `true` であること
4. [x] `--max-repeat` CLIオプションがデフォルト上限として機能すること
5. [x] `repeat_max` がフォーマット定義に指定されている場合、CLIオプションより優先されること
6. [x] 通常の繰り返し（上限未到達）の動作が変わらないこと
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過 — 1,262テスト）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` — `RepeatMax`, `RepeatErrorLimit` プロパティ追加。`DecodedArray` — `Truncated` プロパティ追加 |
| BinAnalyzer.Dsl | `YamlFieldModel` — `repeat_max`, `repeat_error_limit` マッピング |
| BinAnalyzer.Engine | `BinaryDecoder` — ガード条件チェック、打ち切りロジック |
| BinAnalyzer.Output | 打ち切り配列の表示（"... truncated" 等） |
| BinAnalyzer.Cli | `--max-repeat` オプション追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — `repeat_max`, `repeat_error_limit` の説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要
- [x] docs/cli-usage.md — `--max-repeat` オプションの説明追加

---

## 設計メモ

### 設計方針

- **グローバルオプションの伝達**: `BinaryDecoder` のインスタンスフィールド `_globalMaxRepeat` に保持。既存の `_errorMode` と同パターン。`DecodeContext` にはポリシーを入れない。
- **`repeat_max` の型**: `Expression?`（FieldDefinition上）。`repeat_max: "{max_entries}"` のような動的指定に対応。CLIグローバルデフォルトは `int?`。フィールドレベルがグローバルに優先。
- **`repeat_error_limit` の型**: `Expression?`。連続エラーカウンタはデコード成功時にリセット。
- **ガードチェックの挿入位置**: 5つのRepeatMode（Count, UntilEof, UntilValue, While, LengthPrefixed）全てに適用。Count モードでは count 値のクランプ + ループ内チェック、他モードではループ冒頭で要素数チェック。`repeat_max` の式はループ前に1回だけ評価。
- **打ち切り情報**: `DecodedArray` に `Truncated`（bool）と `TruncationReason`（string?）を追加。出力フォーマッタで表示。

### モデル変更

| モデル | 追加プロパティ |
|---|---|
| `FieldDefinition` | `Expression? RepeatMax`, `Expression? RepeatErrorLimit` |
| `DecodedArray` | `bool Truncated`, `string? TruncationReason` |
| `DecodeOptions`（新規） | `int? MaxRepeat` |
| `YamlFieldModel` | `string? RepeatMax`, `string? RepeatErrorLimit` |

### インタフェース変更

| インタフェース | 変更内容 |
|---|---|
| `IBinaryDecoder.Decode` | `DecodeOptions? options = null` 引数追加 |
| `BinaryDecoder.DecodeWithRecovery` | `DecodeOptions? options = null` 引数追加 |

### 代替案

| 案 | 却下理由 |
|---|---|
| `DecodeContext` にグローバルmax渡し | Context はデータ/状態コンテナ。ポリシーを混ぜると責務が曖昧に |
| `repeat_max` を `int?` にする | 要件が「式で指定」。動的上限 `"{max_entries}"` が必要 |
| `TruncatedDecodedArray` サブクラスにする | 全フォーマッタのパターンマッチに影響。プロパティ追加の方が低コスト |

### 懸念事項

| 懸念 | 対策 |
|---|---|
| `repeat_max` の式を毎イテレーション評価するとコスト増 | ループ前に1回だけ評価し `long?` に格納 |
| `FieldDefinition` コピー箇所への追加漏れリスク | ガードロジックは元の `field` から読むため `singleField` コピーには不要 |
| ゴールデンファイルテストへの影響 | `Truncated=false` がデフォルト。JSON出力は `Truncated==true` 時のみ書き出し → 既存ゴールデンファイルに影響なし |

---

## 実装メモ

### 実装中の設計変更

- `repeat: eof` もガード対象に含めた。壊れたファイルで極小要素が大量に生成される可能性があるため、全RepeatModeに一律適用。
- `IBinaryDecoder.Decode` にも `DecodeOptions?` 引数を追加（当初はインタフェース変更なしの方針だったが、オプション引数のデフォルト値でインタフェース互換性は維持）。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| RepeatGuardTests | RepeatMax_Count_TruncatesAtLimit | AC1, AC3 |
| RepeatGuardTests | RepeatMax_UntilEof_TruncatesAtLimit | AC1, AC3 |
| RepeatGuardTests | RepeatMax_While_TruncatesAtLimit | AC1, AC3 |
| RepeatGuardTests | RepeatMax_UntilValue_TruncatesAtLimit | AC1, AC3 |
| RepeatGuardTests | RepeatMax_LengthPrefixed_TruncatesAtLimit | AC1, AC3 |
| RepeatGuardTests | RepeatErrorLimit_TruncatesOnConsecutiveErrors | AC2, AC3 |
| RepeatGuardTests | RepeatErrorLimit_ResetsOnSuccess | AC6 |
| RepeatGuardTests | GlobalMaxRepeat_AppliesAsDefault | AC4 |
| RepeatGuardTests | FieldRepeatMax_OverridesGlobal | AC5 |
| RepeatGuardTests | NoGuard_NormalBehaviorUnchanged | AC6 |
| RepeatGuardTests | RepeatMax_Expression_Dynamic | AC1 |
| RepeatGuardTests | TruncationReason_Count_IncludesOriginalCount | AC3 |
| RepeatGuardTests | TruncationReason_UntilEof_IncludesReached | AC3 |
| RepeatGuardParsingTests | ParsesRepeatMax_Constant | AC1 |
| RepeatGuardParsingTests | ParsesRepeatMax_Expression | AC1 |
| RepeatGuardParsingTests | ParsesRepeatErrorLimit | AC2 |
| RepeatGuardParsingTests | ParsesBothRepeatMaxAndErrorLimit | AC1, AC2 |
| RepeatGuardParsingTests | WithoutRepeatGuard_FieldsAreNull | AC6 |
| RepeatGuardParsingTests | RepeatMax_WithCountRepeat | AC1 |
| RepeatGuardValidationTests | VAL120_RepeatMaxOnNonRepeatField_ReportsWarning | — |
| RepeatGuardValidationTests | VAL120_RepeatMaxOnRepeatField_NoWarning | — |
| RepeatGuardValidationTests | VAL121_RepeatErrorLimitOnNonRepeatField_ReportsWarning | — |
| RepeatGuardValidationTests | VAL121_RepeatErrorLimitOnRepeatField_NoWarning | — |
| RepeatGuardValidationTests | BothGuardsOnNonRepeatField_ReportsBothWarnings | — |
| RepeatGuardValidationTests | NoGuard_NoWarnings | AC6 |

### 気づき・今後の課題

- `repeat_error_limit` は `ErrorMode.Stop` では実質無効（例外が先に伝播するため）。ドキュメントにこの制約を明記済み。
- 将来的に `repeat_max` のデフォルト値をフォーマット定義レベル（`defaults:` セクション）で指定可能にする拡張が考えられる。
