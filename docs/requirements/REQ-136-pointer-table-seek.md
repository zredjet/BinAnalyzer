# REQ-136: ポインタテーブルによる反復seekの強化

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-137 |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

スカラ配列に対するインデックスアクセスを使った反復seekは既にサポートされている（SQLiteフォーマット定義の `seek: "{cell_pointer_array[_index]}"` で実証済み）。

しかし、以下のようなケースでは対応できない。

### 1. 構造体配列のメンバーアクセス

オフセットテーブルが構造体配列の場合、メンバーフィールドへのアクセスができない。

```yaml
# 現在は不可能
- name: entries
  type: struct
  struct: table_entry
  repeat_count: "{header.count}"
  seek: "{offset_table[_index].data_offset}"  # 構造体メンバーアクセス不可
```

### 2. 相対オフセットseek

現在のseekは絶対オフセットのみ。多くのバイナリフォーマットではセクション先頭からの相対オフセットを使用する。

```yaml
# 現在は式で手動計算が必要
seek: "{section_start + entry_offset}"
```

### 影響を受けるフォーマット

| フォーマット | パターン | 現状 |
|---|---|---|
| SQLite | スカラ配列のポインタテーブル | 動作済み |
| ICC | タグテーブル（構造体配列）→ 各タグのオフセット | 構造体メンバー参照不可のため制約あり |
| OTF | テーブルディレクトリ → 各テーブルのオフセット | 同上 |
| PE | セクションヘッダ → RawDataOffset | 同上 |

## 機能要件

### 追加する機能

- [x] 構造体配列のメンバーアクセスをseek式で使用可能にする（REQ-137で実装済み）
- [x] `seek_base` プロパティ — 相対オフセットの基準位置を指定する仕組み（`seek_base: "{section_start}"` + `seek: "{offset}"` → 絶対位置 = `section_start + offset`）

### 変更する既存機能

- [x] `ExpressionEvaluator` — 構造体メンバーアクセスの評価（REQ-137で対応済み）
- [x] `BinaryDecoder` — `seek_base` プロパティの処理追加

### 変更しないもの（スコープ外）

- スカラ配列に対する既存のseek動作 — 変更なし
- `seek_restore` の動作 — 変更なし

## 受入条件

1. [x] 構造体配列のメンバーフィールドをseek式で参照できること（REQ-137完了）
2. [x] `seek_base` を指定した場合、`seek_base + seek` の値が絶対オフセットとして使用されること
3. [x] `seek_base` なしの場合は既存動作（seekが絶対オフセット）を維持すること
4. [ ] ICC/OTFのフォーマット定義を改善し、構造体テーブル経由のseekを使用できること
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` — `SeekBaseExpression` プロパティ追加、`FormatValidator` — VAL015追加 |
| BinAnalyzer.Dsl | `YamlFieldModel` — `seek_base` マッピング追加 |
| BinAnalyzer.Engine | `BinaryDecoder` — `ResolveSeekOffset` ヘルパー追加、seek計算時のベースオフセット処理 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — seek_baseの説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- `seek_base` は `SeekBaseExpression` として `FieldDefinition` に追加し、`seek` と同様に式で指定可能とする
- 実効オフセット計算は `ResolveSeekOffset` ヘルパーに集約し、5箇所のseek処理を統一
- `perElementSeek` 判定を拡張し、`seek_base` に `_index` が含まれる場合もper-element seekとして処理
- VAL015バリデーションで `seek_base` 単独指定をエラーとして検出

### モデル変更

- `FieldDefinition.SeekBaseExpression: Expression?` — seek基準オフセット式

### インタフェース変更

なし（DecodeContext, ExpressionEvaluator等は変更不要）

### 代替案

- `seek` 式内で `+` 演算子による手動結合 — 機能的に同等だが、`seek_base` を分離することで意図が明確になり、DSL記述が簡潔になる

### 懸念事項

なし

---

## 実装メモ

### 実装中の設計変更

なし

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SeekTests | SeekBase_AddsBaseToSeekOffset | 受入条件2 |
| SeekTests | SeekBase_WithFieldReferences | 受入条件2 |
| SeekTests | SeekBase_WithSeekRestore | 受入条件2 |
| SeekTests | SeekBase_PreservesAbsoluteSeek | 受入条件3 |
| SeekTests | SeekBase_WithPerElementSeek | 受入条件2 |
| SeekTests | SeekBase_ZeroBase | 受入条件2, 3 |
| FormatValidatorTests | VAL015_SeekBaseWithoutSeek_ReportsError | バリデーション |
| FormatValidatorTests | SeekBaseWithSeek_NoValidationError | バリデーション |
| SeekParsingTests | ParsesSeekBase | DSLパース |
| SeekParsingTests | ParsesWithoutSeekBase | DSLパース |

### 気づき・今後の課題

- 受入条件4（ICC/OTFフォーマット定義改善）は後続作業として別途対応
