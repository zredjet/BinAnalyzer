# REQ-141: エラー回復の精度向上（マジックバイト再同期）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在の `on-error: continue` モードでは、デコードエラー発生時に以下の回復戦略をとる。

1. 固定サイズ型の場合 — フィールドサイズ分をスキップ
2. 可変サイズ型の場合 — 現在位置に留まる

この戦略では、壊れたフィールドの後に有効なデータが続いている場合でも、位置がずれて以降のデコードが全て失敗するケースが多い。

### 改善が必要なケース

| ケース | 現状の動作 | 望ましい動作 |
|---|---|---|
| 配列要素の途中で破損 | 次の要素もずれて失敗 | 次のマジックバイトまでスキップ |
| チャンク構造の破損 | 以降の全チャンクが解析不能 | 次のチャンクヘッダを検出して再同期 |
| 圧縮データの展開失敗 | 残りバイトが不定 | sizeフィールドで指定されたバイト数をスキップ |

### 影響を受けるフォーマット

チャンクベースのフォーマット（PNG, RIFF/AVI, MP4, MIDI等）やレコード配列型のフォーマット（PE, ELF, ZIP等）で特に有効。

## 機能要件

### 追加する機能

- [x] 再同期マーカーの定義（DSL）— 構造体レベルで `resync_marker: [byte_pattern]` を指定可能にする
- [x] マーカーベースの再同期 — エラー発生時に指定されたバイトパターンを前方スキャンして位置を再同期する
- [x] アライメントベースの再同期 — 構造体に `align` が指定されている場合、次のアライメント境界までスキップする
- [x] エラーノードにスキップしたバイト数を記録する

### 変更する既存機能

- [x] `BinaryDecoder.TrySkipField` — 再同期戦略の選択ロジック追加
- [x] `DecodeException` / `DecodedError` — スキップバイト数の記録

### 変更しないもの（スコープ外）

- `on-error: stop` モードの動作 — 変更なし
- 正常デコード時のパフォーマンス — 再同期ロジックはエラー時のみ実行

## 受入条件

1. [x] `resync_marker` を指定した構造体でエラー発生時、マーカーまでスキップしてデコードが再開されること
2. [x] アライメント指定のある構造体でエラー発生時、次のアライメント境界から再開されること
3. [x] `DecodedError` ノードにスキップしたバイト数が記録されること
4. [x] マーカーが見つからない場合、スコープ終端までスキップすること
5. [x] 正常デコード時のパフォーマンスが劣化しないこと
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition` — `ResyncMarker` プロパティ追加。`DecodedError` — `SkippedBytes` 追加 |
| BinAnalyzer.Dsl | `YamlStructModel` — `resync_marker` マッピング |
| BinAnalyzer.Engine | `BinaryDecoder` — 再同期ロジック |
| BinAnalyzer.Output | エラーノード出力にスキップバイト数を表示 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — `resync_marker` の説明追加
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

- `TryDecodeElementWithRecovery` で要素デコード前の位置を保存し、失敗時に復帰してからマーカースキャンを行う設計に変更。部分デコードで位置がずれた場合でもマーカーを確実に検出できる。
- `TryDecodeElementWithRecovery` 内で一時的に `ErrorMode.Stop` に切り替え、要素内のフィールドエラーを例外として伝搬させる設計に変更。`ErrorMode.Continue` のままだと内部の `DecodeField` がエラーを吸収し、壊れた要素が `DecodedStruct`（エラー子ノード付き）として返されてしまうため。
- マーカーが現在位置（idx==0）にある場合、無限ループを防ぐためさらに前方を検索するロジックを `TrySkipField` と `TryResyncToMarker` の両方に追加。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ResyncRecoveryTests | ResyncMarker_SkipsToNextMarker | 1 |
| ResyncRecoveryTests | ResyncMarker_InRepeatedField | 1 |
| ResyncRecoveryTests | ResyncMarker_NotFound_SkipsToScopeEnd | 4 |
| ResyncRecoveryTests | AlignmentResync_SkipsToNextBoundary | 2 |
| ResyncRecoveryTests | SkippedBytes_RecordedInDecodedError | 3 |
| ResyncRecoveryTests | ResyncMarker_YamlParsing | 1 |
| ResyncRecoveryTests | NoResyncMarker_ExistingBehavior | 6 |
| ResyncRecoveryTests | NormalDecode_NoPerformanceImpact | 5 |
| ResyncRecoveryTests | ResyncMarker_AtCurrentPos_SkipsForward | 1 |

### 気づき・今後の課題

- 固定サイズ型フィールドのみで構成された構造体では、データ内容が壊れていてもバイト数が足りればデコードが「成功」する。エラー回復が機能するのは、可変サイズフィールド（`size: "{expr}"`）やバイト不足が発生するケースに限られる。`expect` によるマジックバイト検証を強制エラーとして扱う（バリデーション失敗時に例外を投げる）オプションがあれば、より多くの破損パターンを検出できる可能性がある。
