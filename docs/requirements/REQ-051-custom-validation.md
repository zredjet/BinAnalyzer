# REQ-051: カスタムバリデーション式

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のBinAnalyzerは `expected` プロパティで固定値の検証ができるが、フィールド値に対する動的な制約条件（範囲チェック、他フィールドとの関係等）は表現できない。

`validate` プロパティで式を指定し、デコード後に条件を評価することで:
- 「widthは1以上65535以下」
- 「data_sizeはfile_sizeからheader_sizeを引いた値以下」
- 「checksumフィールドの値が計算値と一致」

といった制約をフォーマット定義に宣言的に記述できる。既存の式システム（ExpressionParser/ExpressionEvaluator）をそのまま活用でき、実装コストが低い。

## 機能要件

### 追加する機能

- [ ] DSLに `validate` プロパティを追加
  ```yaml
  - name: width
    type: u16
    validate: "{width > 0 && width <= 65535}"
  ```
- [ ] `validate` の動作仕様
  - フィールドデコード後に式を評価
  - 式の結果が真（非ゼロ）なら検証成功、偽（ゼロ）なら検証失敗
  - 検証失敗はエラーではなく警告として扱う（デコードは継続）
  - 検証結果はDecodedNodeに付与（出力フォーマッターで ✓/✗ 表示）
- [ ] `validate` に式を指定可能（比較、論理演算、算術、フィールド参照）
  ```yaml
  validate: "{width > 0}"                    # 範囲チェック
  validate: "{data_size <= file_size - 12}"  # 他フィールドとの関係
  validate: "{type == 1 || type == 2}"       # 許容値リスト
  ```
- [ ] 検証結果の表示
  - tree出力: 成功=✓（緑）、失敗=✗（赤）+ 式テキスト
  - json出力: `_validation` オブジェクト（result, expression）
  - html出力: 成功/失敗のスタイル適用

### 変更する既存機能

- [ ] `FieldDefinition` — `ValidationExpression` プロパティ追加
- [ ] `DecodedNode` — `ValidationResult?` プロパティ追加（Valid/Invalid + 式テキスト）
- [ ] `BinaryDecoder` — フィールドデコード後に validate 式を評価
- [ ] `YamlFormatModel` — `validate` プロパティ追加
- [ ] `YamlToIrMapper` — validate 式のパース
- [ ] 出力フォーマッター — 検証結果の表示

### 変更しないもの（スコープ外）

- 検証失敗時のデコード中断（検証は常に警告レベル）
- 検証メッセージのカスタマイズ（`validate_message` プロパティ）— 将来課題
- 構造体レベルの検証（フィールド横断的な整合性チェック）— 将来課題

## 受入条件

1. [ ] `validate` プロパティで式を指定できること
2. [ ] 式が真の場合に検証成功としてマークされること
3. [ ] 式が偽の場合に検証失敗としてマークされること（デコードは継続）
4. [ ] tree出力で ✓/✗ が表示されること
5. [ ] json出力で検証結果が含まれること
6. [ ] 他フィールドの値を参照する式が動作すること
7. [ ] validate 未指定のフィールドに影響がないこと
8. [ ] DSLリファレンスに validate の説明が追加されること
9. [ ] JSON Schemaに validate プロパティが追加されること
10. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldDefinition に ValidationExpression 追加、DecodedNode に ValidationResult 追加 |
| BinAnalyzer.Dsl | YamlFormatModel に validate 追加、YamlToIrMapper で式パース |
| BinAnalyzer.Engine | BinaryDecoder にバリデーション評価ロジック追加 |
| BinAnalyzer.Output | 全フォーマッターに検証結果の表示追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — validate プロパティの説明、使用例
- [ ] docs/architecture.md — バリデーション式の評価フローの説明
- [ ] schemas/bdef.schema.json — validate プロパティ追加

---

## 設計メモ

### 1. FieldDefinition の変更（BinAnalyzer.Core）

`FieldDefinition` に `ValidationExpression` プロパティを追加する。

```csharp
// FieldDefinition に追加
/// <summary>フィールドデコード後に評価するバリデーション式。結果が真なら検証成功。</summary>
public Expression? ValidationExpression { get; init; }
```

既存の `Expected`（固定バイト値の検証）は `DecodedBytes.ValidationPassed` で結果を格納しているが、今回の `validate` は式ベースで全フィールド型に適用可能という違いがある。

### 2. ValidationResult の定義と DecodedNode への追加（BinAnalyzer.Core）

**案A（採用）:** `DecodedNode` 基底クラスにプロパティを追加する。

```csharp
// DecodedNode に追加
/// <summary>カスタムバリデーションの結果。validate未指定の場合はnull。</summary>
public ValidationResult? Validation { get; init; }
```

```csharp
// BinAnalyzer.Core/Decoded/ValidationResult.cs を新規作成
namespace BinAnalyzer.Core.Decoded;

/// <summary>カスタムバリデーション式の評価結果。</summary>
/// <param name="IsValid">式の評価結果が真ならtrue。</param>
/// <param name="Expression">評価した式のテキスト表現（出力表示用）。</param>
public record ValidationResult(bool IsValid, string Expression);
```

**案Bを不採用とする理由:** 全ノードサブタイプ（DecodedInteger, DecodedString, DecodedBytes, DecodedFloat, DecodedBitfield, DecodedVirtual 等）に個別追加すると変更箇所が多く、将来のサブタイプ追加時に漏れるリスクがある。基底クラスに追加することで一元管理できる。

**既存の `ValidationPassed` との関係:** `DecodedNode.ValidationPassed` は `expected` プロパティ（固定バイト値照合）の結果に使用されている。名前が紛らわしくなるため、新プロパティは `Validation`（`ValidationResult?` 型）として区別する。将来的に `ValidationPassed` を `Validation` に統合することも検討可能だが、今回のスコープ外とする。

### 3. BinaryDecoder の変更（BinAnalyzer.Engine）

`DecodeField` メソッドで、`DecodeSingleField` の呼び出し後にバリデーション式を評価する。

```csharp
// DecodeField 内、node = DecodeSingleField(...) の後に追加
if (field.ValidationExpression is not null)
{
    var isValid = ExpressionEvaluator.EvaluateAsBool(field.ValidationExpression, context);
    node = SetValidationResult(node, new ValidationResult(isValid, field.ValidationExpression.OriginalText));
}
```

**SetValidationResult ヘルパーメソッド:** `DecodedNode` は `init` プロパティのため、既存ノードの `Validation` を後から設定できない。対応策として以下のいずれかを採用する:

- **案1（推奨）:** `Validation` を `{ get; set; }` にする。init-only ではなく通常のセッターにすることで、デコード後に設定可能にする。`DecodedNode` の他のプロパティ（`Name`, `Offset`, `Size`）は `init` のままとし、`Validation` のみ `set` にすることで、バリデーション結果の後付けという意図を明確にする。
- **案2:** `SetPaddingFlag` と同様に、各サブタイプごとに新しいインスタンスを生成してコピーする。変更箇所が多くなるため非推奨。

**評価タイミング:** フィールドデコード後（`context.SetVariable` 済み）に評価する。これにより、自分自身の値を `{field_name}` で参照できる。

**繰り返しフィールドの扱い:** `DecodeRepeatedField` 内の各要素デコード後にもバリデーションを適用する。各要素の `DecodedNode` に個別の `ValidationResult` を設定する。

### 4. Expression に OriginalText を追加（BinAnalyzer.Core）

出力時にバリデーション式のテキストを表示するため、`Expression` クラスに元のテキストを保持するプロパティが必要になる。

```csharp
// Expression に追加（もしくは ValidationResult 生成時に YamlFieldModel.Validate の文字列を渡す）
public string OriginalText { get; init; } = "";
```

`ExpressionParser.Parse()` で元のテキストを `OriginalText` に設定する。既に `Expression` が元テキストを保持している場合はそれを利用する。保持していない場合は、`FieldDefinition` にバリデーション式の元テキストを `string? ValidationExpressionText` として別途保持する方式でもよい。

### 5. YamlFormatModel の変更（BinAnalyzer.Dsl）

`YamlFieldModel` に `validate` プロパティを追加する。

```csharp
// YamlFieldModel に追加
[YamlMember(Alias = "validate")]
public string? Validate { get; set; }
```

### 6. YamlToIrMapper の変更（BinAnalyzer.Dsl）

`MapField` メソッドで `validate` をパースする。

```csharp
// MapField の FieldDefinition 初期化に追加
ValidationExpression = yaml.Validate is not null ? ExpressionParser.Parse(yaml.Validate) : null,
```

### 7. 出力フォーマッターの変更（BinAnalyzer.Output）

全フォーマッターの各ノード表示メソッドに、`Validation` が非null の場合の表示を追加する。

**TreeOutputFormatter:**
各 Format メソッド（FormatInteger, FormatBytes, FormatString, FormatFloat, FormatVirtual 等）の末尾（`sb.AppendLine()` の直前）にバリデーション結果を追加する。共通ヘルパーメソッドを作成する。

```csharp
private void AppendValidation(StringBuilder sb, DecodedNode node)
{
    if (node.Validation is not { } v) return;
    sb.Append("  ");
    if (v.IsValid)
        sb.Append(C($"✓ validate: {v.Expression}", AnsiColors.Green));
    else
        sb.Append(C($"✗ validate: {v.Expression}", AnsiColors.Red));
}
```

**JsonOutputFormatter:**
`WriteCommonProperties` メソッドにバリデーション結果の出力を追加する。

```csharp
// WriteCommonProperties の末尾に追加
if (node.Validation is { } validation)
{
    writer.WritePropertyName("_validation");
    writer.WriteStartObject();
    writer.WriteBoolean("valid", validation.IsValid);
    writer.WriteString("expression", validation.Expression);
    writer.WriteEndObject();
}
```

**HtmlOutputFormatter:** 成功時は緑色のチェックマーク、失敗時は赤色のバツ印を表示する。CSS クラス `validation-pass` / `validation-fail` を適用する。

**CsvOutputFormatter:** `validation` 列を追加するか、既存の出力形式に注釈として含めるかを実装時に判断する。

### 8. 設計判断

- **バリデーション失敗はデコード停止しない:** 要件通り、バリデーションは常に警告レベルとする。`ErrorMode` とは独立した機能である。`ErrorMode.Stop` であってもバリデーション失敗でデコードは停止しない。
- **バリデーション式のエラー（式自体の評価失敗）:** 式内で参照した変数が存在しない等の場合は、`ValidationResult(false, expression)` として失敗扱いにするか、例外をスローするかの判断が必要。推奨: try-catch で囲み、評価エラー時は `ValidationResult(false, expression + " (evaluation error)")` とする。デコードは継続する。
- **`WithoutRepeat` メソッドの更新:** `FieldDefinition` のコピーを作成している `WithoutRepeat` に `ValidationExpression` を追加する必要がある。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
