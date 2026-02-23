# REQ-139: テンプレート構造体（パラメータ付きstruct）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

バイナリフォーマットには、同じ構造パターンが微妙なパラメータ違いで繰り返し出現するケースが多い。

### 典型例1: ASN.1 TLV（X.509）

X.509フォーマット定義では、Tag-Length-Value構造が何度も複製されている。Lengthのバイト数やTagの値が異なるだけで、構造自体は同一である。

### 典型例2: RIFFチャンク（AVI, WAV）

RIFFベースのフォーマットでは、4バイトID + 4バイト長 + データの構造が共通。ただしチャンク内のデータ構造がフォーマットごとに異なる。

### 典型例3: MP4 Box

MP4のBox構造（size + type + optional extended header + data）は全Boxで共通だが、データ部の構造がBox typeによって異なる。

現在は各バリエーションを個別のstruct定義として記述しており、構造の重複が発生している。

```yaml
# 望ましい記述
structs:
  tlv(tag_size=1, len_size=1):
    - name: tag
      type: uint8
      size: "{tag_size}"
    - name: length
      type: uint8
      size: "{len_size}"
    - name: value
      type: bytes
      size: "{length}"

  certificate_serial:
    - name: header
      type: struct
      struct: tlv(tag_size=1, len_size=2)
    - name: data
      ...
```

## 機能要件

### 追加する機能

- [x] 構造体定義にパラメータ宣言を追加（`struct_name(param1, param2=default)`）
- [x] 構造体参照時にパラメータを渡す構文（`struct: template_name(value1, value2)`）
- [x] パラメータを式内で変数として参照可能にする
- [x] デフォルト値の指定をサポート

### 変更する既存機能

- [x] `YamlToIrMapper` — パラメータ付きstruct定義のパース
- [x] `StructDefinition` — パラメータ定義の保持
- [x] `FieldDefinition` — struct参照時のパラメータ引数の保持
- [x] `BinaryDecoder` — 構造体デコード開始時にパラメータをスコープにバインド

### 変更しないもの（スコープ外）

- パラメータなし構造体の既存動作 — 完全互換
- テンプレートの再帰的なインスタンス化（テンプレートがテンプレートを呼ぶ） — 将来課題
- switch/cases内でのテンプレートパラメータの使用 — 将来課題

## 受入条件

1. [x] パラメータ付きstructを定義し、参照時にパラメータを渡してデコードできること
2. [x] パラメータが式内で変数として使用できること
3. [x] デフォルト値付きパラメータが省略時にデフォルト値を使用すること
4. [x] パラメータ不足の場合にバリデーションエラーが発生すること
5. [x] パラメータなしstructは既存通り動作すること（後方互換性）
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition` — パラメータリスト追加。`FieldDefinition` — struct参照引数追加。`FormatValidator` — パラメータ検証 |
| BinAnalyzer.Dsl | `YamlStructModel` — パラメータ定義パース。`YamlToIrMapper` — パラメータマッピング |
| BinAnalyzer.Engine | `BinaryDecoder` — デコード開始時のパラメータバインディング |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — テンプレートstruct構文の説明追加
- [x] docs/architecture.md — パラメータバインディングの説明追加
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- DSL層（`YamlToIrMapper`）で文字列パース。YAML辞書のキーとフィールドのstruct参照はプレーン文字列なのでYAML側の変更は不要
- `format.Structs` にはベース名で登録。パラメータ定義は `StructDefinition.Parameters` に格納
- 位置引数・名前付き引数・式引数の3種類をサポート
- パラメータのデフォルト値は `long?` 型（整数リテラルのみ）
- テンプレートパラメータが親スコープに漏れないよう、`PushVariableScope()` でオーバーレイスコープを作成

### モデル変更

- `TemplateParameter(string Name, long? DefaultValue)` — パラメータ定義
- `StructArgument(string? ParameterName, long? Value, Expression? Expression)` — 引数
- `StructDefinition.Parameters` — パラメータリスト追加
- `FieldDefinition.StructArgs` — struct参照引数追加

### インタフェース変更

なし（内部モデルの拡張のみ）

### 代替案

- YAMLモデルに専用プロパティを追加する案 → 却下（プレーン文字列で十分、YAML互換性を維持）
- 式引数をAST評価する案 → 採用（`ExpressionParser.Parse` で式を解析し、`ExpressionEvaluator` で実行時評価）

### 懸念事項

- 整数型フィールド（uint8等）はSizeExpressionを無視して固定バイト数を読む。テンプレートパラメータでサイズを制御するにはbytes型を使用する必要がある

---

## 実装メモ

### 実装中の設計変更

特になし

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| TemplateStructParsingTests | ParsesTemplateDefinition_WithDefaults | 1 |
| TemplateStructParsingTests | ParsesTemplateDefinition_WithoutDefaults | 1 |
| TemplateStructParsingTests | ParsesTemplateDefinition_MixedDefaults | 1 |
| TemplateStructParsingTests | ParsesTemplateRef_NamedArgs | 1 |
| TemplateStructParsingTests | ParsesTemplateRef_PositionalArgs | 1 |
| TemplateStructParsingTests | ParsesTemplateRef_ExpressionArgs | 2 |
| TemplateStructParsingTests | ParsesNonTemplate_Unchanged | 5 |
| TemplateStructTests | NamedArgs_DecodesCorrectly | 1, 2 |
| TemplateStructTests | PositionalArgs_DecodesCorrectly | 1, 2 |
| TemplateStructTests | DefaultValues_Applied | 3 |
| TemplateStructTests | PartialDefaults_MixArgs | 3 |
| TemplateStructTests | ExpressionArg_EvaluatedAtRuntime | 2 |
| TemplateStructTests | MissingRequiredParam_Throws | 4 |
| TemplateStructTests | WithRepeat_DecodesMultiple | 1 |
| TemplateStructTests | ParamUsedInSizeExpression | 2 |
| TemplateStructTests | VariableIsolation | 2 |
| TemplateStructTests | NonTemplate_UnchangedBehavior | 5 |
| TemplateValidationTests | VAL116_RequiredParamsWithoutArgs | 4 |
| TemplateValidationTests | VAL116_AllDefaultsNoArgs_NoWarning | 4 |

### 気づき・今後の課題

- 整数型フィールドは型に基づく固定サイズで読み取るため、テンプレートパラメータでサイズを変動させるにはbytes型を使用する必要がある
- switch/cases でのテンプレートパラメータ使用は将来課題
- 圧縮フィールドのstruct refでのテンプレート対応は将来課題
