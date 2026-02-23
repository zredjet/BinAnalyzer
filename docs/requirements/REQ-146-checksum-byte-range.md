# REQ-146: チェックサム範囲指定（バイトオフセットベース）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在のチェックサム機能では、検証対象をフィールド名のリスト（`checksum.field_names`）で指定する。対象フィールドの生バイトを連結してチェックサムを計算する仕組みである。

しかし、以下のケースではフィールド名指定では不十分である。

### バイト範囲指定が必要なケース

| フォーマット | チェックサム仕様 | 現状の制約 |
|---|---|---|
| PNG | CRCはチャンクタイプ(4バイト) + データ全体を対象 | フィールドが分割されている場合に対象を集めにくい |
| PE | チェックサムはOptional Header内の特定範囲を除外して計算 | 「除外範囲」の表現ができない |
| ZIP | CRC-32はファイルデータのみ対象 | 圧縮後データが単一フィールドなら可能だが、チャンク分割時に不可 |
| ELF | セクションチェックサムはセクションのバイト範囲で指定 | オフセット+サイズの組がフィールド名と対応しない |

### 現在の制約まとめ

1. **バイトオフセット+長さでの範囲指定ができない**
2. **除外範囲の指定ができない**（チェックサムフィールド自身をゼロクリアして計算するパターン）
3. **非隣接の複数バイト範囲の連結ができない**

## 機能要件

### 追加する機能

- [x] `checksum.range` — バイトオフセットと長さで検証対象を指定（`range: { offset: "{expr}", size: "{expr}" }`）
- [x] `checksum.ranges` — 複数のバイト範囲を配列で指定
- [x] `checksum.exclude_self` — チェックサムフィールド自身の位置をゼロクリアして計算するオプション

### 変更する既存機能

- [x] `BinaryDecoder` のチェックサム計算ロジック — 範囲指定時のバイト収集方法変更
- [x] `ChecksumDefinition`（IR） — 範囲指定プロパティの追加

### 変更しないもの（スコープ外）

- `checksum.field_names` による既存のフィールド名指定 — 変更なし
- チェックサムアルゴリズムの追加 — REQ-151で対応

## 受入条件

1. [x] `checksum.range` でバイトオフセット+サイズを指定してチェックサムが計算されること
2. [x] `checksum.ranges` で複数範囲を指定して連結チェックサムが計算されること
3. [x] `checksum.exclude_self` 指定時にフィールド自身のバイト位置がゼロ埋めで計算されること
4. [x] `field_names` と `range`/`ranges` が同時指定された場合にバリデーションエラーになること
5. [x] 範囲がファイル境界を超える場合にエラーメッセージが返ること
6. [x] 既存の `field_names` 指定チェックサムが引き続き動作すること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumDefinition` — `Range`/`Ranges`/`ExcludeSelf` プロパティ追加。`FormatValidator` — 排他制約の検証 |
| BinAnalyzer.Dsl | `YamlFieldModel` — checksum内のrange/ranges/exclude_selfマッピング |
| BinAnalyzer.Engine | `BinaryDecoder` — 範囲ベースのバイト収集ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — チェックサムの範囲指定構文の説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- `ChecksumSpec` に `Range`/`Ranges`/`ExcludeSelf` プロパティを追加。`FieldNames` は `required` のまま維持し、range モード時は空リスト。
- `CollectChecksumRangeData` で範囲ベースのバイト収集、`ZeroFillOverlap` で exclude_self のゼロ埋めを実装。
- `CollectChecksumDataForSpec` ディスパッチャーが range/ranges/fields モードを判別。
- `VerifyChecksum`/`VerifyHashChecksum` にデフォルト引数 `checksumFieldOffset`/`checksumFieldSize` を追加し、既存呼び出し元との互換性を維持。

### モデル変更

- `ChecksumRange` 新規クラス（Core）: `OffsetExpression` + `SizeExpression`
- `ChecksumSpec` 拡張: `Range`, `Ranges`, `ExcludeSelf` プロパティ追加
- `YamlChecksumRangeModel` 新規クラス（DSL）: YAML の range オブジェクトに対応
- `YamlChecksumModel` 拡張: `Range`, `Ranges`, `ExcludeSelf` プロパティ追加

### インタフェース変更

- `VerifyChecksum` / `VerifyHashChecksum` にオプション引数追加（既存呼び出し元は変更不要）

### 代替案

- `FieldNames` を `required` でなくして null 許容にする案 → 既存コードへの影響が大きいため却下

### 懸念事項

- ビットストリームモードでの exclude_self: ビット単位フィールドのバイト位置は近似値。実用上の問題は少ないが、将来的にビットストリーム+range の組み合わせが必要になった場合は再検討が必要。

---

## 実装メモ

### 実装中の設計変更

- 計画通りに実装。特記すべき設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ChecksumParsingTests | Load_ChecksumWithRange_ParsesCorrectly | AC1 |
| ChecksumParsingTests | Load_ChecksumWithRanges_ParsesCorrectly | AC2 |
| ChecksumParsingTests | Load_ChecksumWithExcludeSelf_ParsesCorrectly | AC3 |
| ChecksumParsingTests | Load_ChecksumWithExcludeSelfDefault_IsFalse | AC3 |
| FormatValidatorTests | VAL018_FieldNamesAndRange_ReportsError | AC4 |
| FormatValidatorTests | VAL018_FieldNamesAndRanges_ReportsError | AC4 |
| FormatValidatorTests | VAL019_RangeAndRanges_ReportsError | AC4 |
| FormatValidatorTests | VAL119_ExcludeSelfWithoutRange_ReportsWarning | — |
| FormatValidatorTests | ChecksumWithRangeOnly_NoError | — |
| FormatValidatorTests | ChecksumWithFieldsOnly_StillValid | AC6 |
| ChecksumDecoderTests | Decode_ChecksumWithRange_ComputesCorrectly | AC1 |
| ChecksumDecoderTests | Decode_ChecksumWithRanges_ConcatenatesRanges | AC2 |
| ChecksumDecoderTests | Decode_ChecksumWithExcludeSelf_ZeroFillsOwnBytes | AC3 |
| ChecksumDecoderTests | Decode_ChecksumRangeExceedsBoundary_ThrowsError | AC5 |
| ChecksumDecoderTests | Decode_ChecksumWithRangeExpression_EvaluatesAtDecodeTime | AC1 |
| ChecksumDecoderTests | Decode_HashChecksum_WithRange_ComputesCorrectly | AC1 |
| ChecksumDecoderTests | Decode_ExistingFieldNamesChecksum_StillWorks | AC6 |

### 気づき・今後の課題

- 現在の `exclude_self` はチェックサムフィールド自身のみゼロ埋め。将来的に任意の除外範囲（`exclude_ranges`）が必要になる可能性がある。
