# REQ-097: 動的エンディアン切り替え

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-095（実ファイル検証で TIFF BE ファイルの需要を確認後が望ましい） |
| フェーズ | B（軽量 DSL 拡張） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

TIFF フォーマットはファイル先頭の `byte_order` フィールド（`"II"` = リトルエンディアン、`"MM"` = ビッグエンディアン）によってバイトオーダーが動的に決まる。現在の DSL ではエンディアンは静的指定（トップレベル / 構造体レベル / フィールドレベル）のみで、実行時の値に基づく切り替えができない。

そのため TIFF の定義には `validate: "{byte_order == 'II'}"` が入っており、ビッグエンディアン TIFF は解析不可となっている。

TIFF 以外にも、エンディアンが動的に決まるフォーマットは存在する（例: ELF の `ei_data`、Mach-O のマジックバイト `0xFEEDFACE` vs `0xCEFAEDFE`）。

## 機能要件

### 追加する機能

- [x] 構造体レベルの `endianness` に式を指定可能にする
  ```yaml
  structs:
    tiff_body:
      endianness: "{byte_order == 'II' ? 'little' : 'big'}"
      fields:
        - name: magic
          type: uint16
  ```
  または、より単純な条件マッピング構文:
  ```yaml
  structs:
    tiff_body:
      endianness_switch:
        field: byte_order
        cases:
          "'II'": little
          "'MM'": big
      fields:
        - ...
  ```
- [x] エンディアン式の評価タイミング: 構造体のデコード開始時に1回評価し、以降のフィールドに適用
- [x] 子構造体への伝播: 動的に決定されたエンディアンが子構造体に継承されること
- [x] TIFF フォーマット定義から `validate: "{byte_order == 'II'}"` を除去し、LE/BE 両対応にする

### 変更しないもの（スコープ外）

- フィールド単位の動的エンディアン（構造体単位で十分）
- 同一構造体内でのエンディアン動的切り替え（フィールドごとに変わるケースは既存のフィールドレベル `endianness` で対応）

## 受入条件

1. [x] 構造体の `endianness` にフィールド値を参照する式（または条件マッピング）が指定できること
2. [x] リトルエンディアン TIFF が正しくデコードできること（既存動作の維持）
3. [x] ビッグエンディアン TIFF が正しくデコードできること
4. [x] 子構造体に動的エンディアンが正しく継承されること
5. [ ] ELF の `ei_data` による LE/BE 切り替えが定義可能であること（任意）
6. [x] FormatValidator で式の妥当性が検証されること
7. [x] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | StructDefinition に動的エンディアン式を追加 |
| BinAnalyzer.Dsl | YamlToIrMapper でエンディアン式をパース |
| BinAnalyzer.Engine | BinaryDecoder で構造体デコード開始時にエンディアン式を評価 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

---

## 設計メモ

### 式エンジンに三項演算子を追加

- `ExpressionTokenType` に `Question`, `Colon` を追加
- `ExpressionNode.Conditional` レコードを追加
- `ExpressionParser` に `ParseTernaryExpr` を最低優先順位として追加（右結合）
- `ExpressionEvaluator` で短絡評価を実装

### 動的エンディアンの実装方式

- `StructDefinition.EndiannessExpression` を追加（既存の `Endianness` と相互排他）
- YamlToIrMapper で `{` で始まる endianness 値を式としてパース
- BinaryDecoder.DecodeStruct で静的エンディアンの後に動的エンディアンスコープをプッシュ
- 式は構造体デコード開始時に1回だけ評価、参照先フィールドは親スコープに存在する必要がある

### TIFF フォーマット定義の構造変更

- `tiff` ルートに `byte_order`（ascii、エンディアン非依存）を直接配置
- 残りのヘッダフィールドを `tiff_body` 構造体に移動
- `tiff_body` に `endianness: "{byte_order == 'II' ? 'little' : 'big'}"` を設定
- `ifd0` を `tiff_body` の子に移動（動的エンディアンが自動継承）
- トップレベルの `endianness: little` を削除

---

## 実装メモ

### 実装中の設計変更

- `DynamicEndianness_InvalidResult_Throws` テストで `InvalidOperationException` を期待していたが、実際には `DecodeException` でラップされるため `Exception` に変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| TernaryExpressionTests | Ternary_Parse_TrueCondition | 1 |
| TernaryExpressionTests | Ternary_Parse_FalseCondition | 1 |
| TernaryExpressionTests | Ternary_NestedTernary_RightAssociative | 1 |
| TernaryExpressionTests | Ternary_InParentheses | 1 |
| TernaryExpressionTests | Ternary_MissingColon_Throws | 1 |
| TernaryExpressionTests | Ternary_IntegerResult | 1 |
| TernaryExpressionTests | Ternary_WithFieldReference | 1 |
| DynamicEndiannessTests | DynamicEndianness_EvaluatesToLittle | 2 |
| DynamicEndiannessTests | DynamicEndianness_EvaluatesToBig | 3 |
| DynamicEndiannessTests | DynamicEndianness_InheritsToChildStruct | 4 |
| DynamicEndiannessTests | DynamicEndianness_InvalidResult_Throws | 6 |
| EndiannessParsingTests | ParsesDynamicEndianness_Expression | 1 |
| EndiannessParsingTests | ParsesDynamicEndianness_StaticStillWorks | 1 |
| TiffParsingTests | TiffFormat_BigEndian_DecodesCorrectly | 3 |
| TiffParsingTests | TiffFormat_BigEndian_IfdEntry_DecodesCorrectly | 3 |

### 気づき・今後の課題

- `endianness_switch` 構文（条件マッピング）は三項演算子で代替可能なため未実装。必要に応じて将来追加。
- ELF の `ei_data` 対応は TIFF と同じパターンで定義可能（受入条件5は任意のため未検証）。
