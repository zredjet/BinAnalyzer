# REQ-006: 条件付きフィールド

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットでは、先行フィールドの値に応じて後続フィールドの存在/不在が変わる。例:

- PNGの `PLTE` チャンク: `color_type` が indexed (3) の場合のみ必須
- BMPの拡張ヘッダー: ヘッダーサイズに応じてフィールドの有無が変わる
- 各種プロトコル: フラグビットに応じてオプションフィールドが現れる

現在のBinAnalyzerでは `switch` で分岐先structを切り替えることで部分的に対応できるが、単一フィールドの条件付き有無を表現するには冗長。`if` 属性による直接的な条件指定が望ましい。

## 機能要件

### 追加する機能

- [ ] DSLに `if` フィールド属性を追加
  ```yaml
  structs:
    extended_header:
      - name: version
        type: uint8
      - name: extra_flags
        type: uint16
        if: "{version >= 2}"
      - name: legacy_field
        type: uint8
        if: "{version == 1}"
  ```
- [ ] IRモデルの `FieldDefinition` に `Condition` プロパティ（`Expression?`）を追加
- [ ] `BinaryDecoder` で条件評価を行い、false の場合フィールドをスキップ
- [ ] スキップされたフィールドはデコード結果に含めない（バイトも読み取らない）

### 変更する既存機能

- [ ] `FieldDefinition`: `Condition` プロパティの追加
- [ ] `YamlFieldModel`: `if` プロパティの追加
- [ ] `YamlToIrMapper.MapField`: `if` 式のパース
- [ ] `BinaryDecoder.DecodeField`: 条件評価によるフィールドスキップ

### 変更しないもの（スコープ外）

- `else` / `else if` のチェーン（switchで代替可能）
- 条件付きstruct全体のスキップ（フィールドレベルのみ対応）
- 条件式の結果型の静的検証（ランタイム評価のみ）

## 受入条件

1. [ ] `if` 属性付きフィールドが定義できること
2. [ ] 条件がtrueの場合、フィールドが通常通りデコードされること
3. [ ] 条件がfalseの場合、フィールドがスキップされ、バイトが読み取られないこと
4. [ ] スキップされたフィールドがデコード結果ツリーに含まれないこと
5. [ ] スキップされたフィールドの後続フィールドが正しいオフセットでデコードされること
6. [ ] 条件式で先行フィールドの値を参照できること
7. [ ] ツリー出力に影響がないこと（スキップされたフィールドは表示されない）
8. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` に `Condition` プロパティ追加 |
| BinAnalyzer.Dsl | `YamlFieldModel` に `if` 追加、マッピング |
| BinAnalyzer.Engine | `BinaryDecoder.DecodeField` に条件評価ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — `if` 属性の構文ドキュメント
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

`FieldDefinition` に `Condition`（`Expression?`）を追加し、デコード時に条件を評価してフィールドの読み取り/スキップを制御する。

既存の式システム（`ExpressionParser`, `ExpressionEvaluator`）をそのまま活用。新しい演算子や関数の追加は不要（比較演算子 `==`, `>=`, `!=` 等は既に対応済み）。

### モデル変更

**FieldDefinition への追加**:
```csharp
/// <summary>条件式。falseの場合フィールドをスキップする。</summary>
public Expression? Condition { get; init; }
```

#### Dsl/YamlModels/ への追加

**YamlFieldModel への追加**:
```csharp
[YamlMember(Alias = "if")]
public string? If { get; set; }
```

### インタフェース変更

既存インタフェースへの変更なし。

**BinaryDecoder の変更**:
- `DecodeField` メソッド内（repeatの前）で条件を評価
- falseの場合は `null` を返し、`DecodeStruct` のchildrenに追加しない

```csharp
private DecodedNode? DecodeField(
    FieldDefinition field, FormatDefinition format, DecodeContext context)
{
    // 条件付きフィールドの評価
    if (field.Condition is not null)
    {
        if (!ExpressionEvaluator.EvaluateAsBool(field.Condition, context))
            return null;  // スキップ
    }

    if (field.Repeat is not RepeatMode.None)
        return DecodeRepeatedField(field, format, context);

    return DecodeSingleField(field, format, context);
}
```

`DecodeStruct` 側の変更:
```csharp
foreach (var field in structDef.Fields)
{
    var node = DecodeField(field, format, context);
    if (node is not null)      // ← null チェック追加
        children.Add(node);
}
```

**戻り値の型変更**: `DecodeField` の戻り値を `DecodedNode` → `DecodedNode?` に変更。

**YamlToIrMapper の変更**:
- `MapField` に `if` プロパティのパース追加

```csharp
Condition = yaml.If is not null ? ExpressionParser.Parse(yaml.If) : null,
```

### WithoutRepeat への追加

```csharp
Condition = field.Condition,  // コピー対象に追加
```

### 代替案

**案A: スキップされたフィールドを結果に含める（値なしとして）（不採用）**
- 理由: 存在しないフィールドを結果に含めると、ツリー出力やJSON出力で「存在しないが表示される」フィールドの扱いが複雑化する。シンプルに除外する方が一貫性がある。

**案B: 条件式の否定をサポートする unless 属性（不採用）**
- 理由: `if: "{version != 1}"` で否定は表現可能。専用構文は不要。

### 懸念事項

1. **変数バインディングへの影響**: スキップされたフィールドは変数にバインドされない。後続フィールドの式で当該フィールド名を参照すると実行時エラーになる。これはDSL設計者の責任範囲とする（同じif条件で保護するか、スキップされるフィールドを参照しない設計にする）。

2. **repeat内の条件付きフィールド**: repeat対象のフィールドにifが付いている場合の動作。RepeatMode.Count(N) では条件がfalseでもカウントは消費される（N回ループし、条件falseならスキップ）。UntilEofでは条件falseの要素が結果から除外される。この動作で問題ないか、実装時に検証する。

3. **CRC検証（REQ-005）との相互作用**: checksum.fieldsで指定されたフィールドがifによりスキップされた場合、CRC計算の対象範囲が変わる。これは特殊ケースであり、実装時に考慮が必要。ただし、CRC対象フィールドにifを付けるユースケースは稀。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `DecodeField` の戻り値を `DecodedNode` → `DecodedNode?` に変更。
- `DecodeStruct` で null チェックを追加して、条件falseのフィールドをchildrenから除外。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Models/FieldDefinition.cs` | `Condition` プロパティ追加 |
| `src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs` | `If` プロパティ追加 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | `Condition` マッピング追加 |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `DecodeField` に条件評価、`WithoutRepeat` に `Condition` コピー |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ConditionalFieldTests | Decode_ConditionTrue_FieldIsIncluded | AC-2 |
| ConditionalFieldTests | Decode_ConditionFalse_FieldIsSkipped | AC-3, AC-4 |
| ConditionalFieldTests | Decode_SkippedField_BytesNotConsumed | AC-3, AC-5 |
| ConditionalFieldTests | Decode_MultipleConditionalFields_IndependentEvaluation | AC-6 |
| ConditionalFieldParsingTests | Load_FieldWithIf_ParsesCondition | AC-1 |
| ConditionalFieldParsingTests | Load_FieldWithoutIf_HasNullCondition | AC-1 |

### 受入条件の充足状況

- [x] AC-1: `if` 属性付きフィールドが定義できる
- [x] AC-2: 条件がtrueの場合、フィールドが通常通りデコードされる
- [x] AC-3: 条件がfalseの場合、フィールドがスキップされ、バイトが読み取られない
- [x] AC-4: スキップされたフィールドがデコード結果ツリーに含まれない
- [x] AC-5: スキップされたフィールドの後続フィールドが正しいオフセットでデコードされる
- [x] AC-6: 条件式で先行フィールドの値を参照できる
- [x] AC-7: ツリー出力に影響がない（スキップされたフィールドは表示されない）
- [x] AC-8: 既存テスト全て通過（156テスト合格）

### 気づき・今後の課題

- repeat内の条件付きフィールドの動作は未テスト。repeat対象のstructの子フィールドにifを付ける場合は問題ないが、repeat対象フィールド自体にifを付ける場合の動作は未検証。
