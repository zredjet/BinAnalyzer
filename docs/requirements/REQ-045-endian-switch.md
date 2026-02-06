# REQ-045: エンディアン切り替え

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のBinAnalyzerはフォーマット定義のトップレベルで指定した単一のエンディアンで全フィールドを読み取る。しかし実際のバイナリフォーマットでは、構造体やフィールド単位でエンディアンが異なる場合がある。

代表的な例:
- **TIFF**: ファイル先頭2バイト（"II"=リトル, "MM"=ビッグ）でエンディアンが決まる
- **PE**: DOSヘッダはリトルエンディアン固定だが、特定の構造体はビッグエンディアンの場合がある
- **ネットワークプロトコル**: ヘッダはビッグエンディアン（ネットワークバイトオーダー）、ペイロードはリトルエンディアン
- **組み込みファームウェア**: ARM（リトル）とネットワークスタック（ビッグ）が混在

## 機能要件

### 追加する機能

- [ ] 構造体レベルのエンディアン指定
  ```yaml
  structs:
    ifd_entry:
      endianness: little    # この構造体内のフィールドはリトルエンディアン
      fields:
        - name: tag
          type: u16
  ```
- [ ] フィールドレベルのエンディアン指定
  ```yaml
  - name: network_port
    type: u16
    endianness: big         # このフィールドだけビッグエンディアン
  ```
- [ ] エンディアンの優先順位: フィールド > 構造体 > トップレベル
- [ ] 指定可能な値: `big`, `little`

### 変更する既存機能

- [ ] `StructDefinition` — `Endianness?` プロパティ追加（null=親に従う）
- [ ] `FieldDefinition` — `Endianness?` プロパティ追加（null=親に従う）
- [ ] `DecodeContext` — エンディアンスタック管理、構造体スコープに連動
- [ ] `BinaryDecoder` — 構造体デコード時にエンディアンをpush/pop
- [ ] `YamlFormatModel` — 構造体・フィールドに endianness プロパティ追加
- [ ] `YamlToIrMapper` — endianness のパース

### 変更しないもの（スコープ外）

- ランタイムでのエンディアン動的切り替え（フィールド値に基づいて切り替え）— 将来課題
- ビットフィールド内のエンディアン指定

## 受入条件

1. [ ] 構造体レベルで endianness を指定できること
2. [ ] フィールドレベルで endianness を指定できること
3. [ ] フィールド > 構造体 > トップレベルの優先順位で解決されること
4. [ ] endianness 未指定の場合は親の設定を継承すること
5. [ ] リトルエンディアンとビッグエンディアンが混在するデータを正しくデコードできること
6. [ ] 既存のフォーマット定義（トップレベルのみ指定）に影響がないこと
7. [ ] DSLリファレンスに構造体・フィールドレベルの endianness の説明が追加されること
8. [ ] JSON Schemaに endianness プロパティが追加されること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | StructDefinition, FieldDefinition に Endianness? 追加 |
| BinAnalyzer.Dsl | YamlFormatModel に endianness 追加、YamlToIrMapper でパース |
| BinAnalyzer.Engine | DecodeContext にエンディアンスタック追加、BinaryDecoder で push/pop |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 構造体・フィールドレベルの endianness の説明、使用例
- [ ] docs/architecture.md — DecodeContext のエンディアンスタックの説明
- [ ] schemas/bdef.schema.json — endianness プロパティ追加

---

## 設計メモ

### 設計方針

エンディアンの解決は以下の優先順位に従う（高い方が優先）:

1. **フィールドレベル** (`FieldDefinition.Endianness`) -- 最優先。該当フィールドのみに適用。
2. **構造体レベル** (`StructDefinition.Endianness`) -- その構造体内の全フィールドに適用（フィールドレベルで上書き可能）。
3. **トップレベル** (`FormatDefinition.Endianness`) -- フォーマット全体のデフォルト。省略時は `big`。

null（未指定）は「親に従う」を意味する。これにより既存のフォーマット定義に影響を与えない。

### モデル変更

**StructDefinition への `Endianness?` 追加:**

```csharp
// src/BinAnalyzer.Core/Models/StructDefinition.cs
public sealed class StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
    public int? Align { get; init; }

    /// <summary>この構造体内のフィールドに適用するエンディアン。nullの場合は親に従う。</summary>
    public Endianness? Endianness { get; init; }
}
```

**FieldDefinition への `Endianness?` 追加:**

```csharp
// src/BinAnalyzer.Core/Models/FieldDefinition.cs
public sealed class FieldDefinition
{
    // ...既存プロパティ...

    /// <summary>このフィールドのみに適用するエンディアン。nullの場合は構造体またはトップレベルに従う。</summary>
    public Endianness? Endianness { get; init; }
}
```

**YamlFormatModel の構造体定義の変更:**

現在 `YamlFormatModel.Structs` は `Dictionary<string, List<YamlFieldModel>>` であり、構造体レベルのメタデータ（`endianness`）を持てない。新たに `YamlStructModel` を導入し、フィールドリストとメタデータの両方を扱えるようにする。

```csharp
// src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs

// 新規クラス
public sealed class YamlStructModel
{
    [YamlMember(Alias = "endianness")]
    public string? Endianness { get; set; }

    [YamlMember(Alias = "align")]
    public int? Align { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlFieldModel> Fields { get; set; } = new();
}
```

`YamlFormatModel.Structs` の型は `Dictionary<string, YamlStructModel>` に変更する。

```csharp
// YamlFormatModel 内
[YamlMember(Alias = "structs")]
public Dictionary<string, YamlStructModel> Structs { get; set; } = new();
```

ただし、既存のYAMLファイルとの互換性を維持するため、YAMLデシリアライズ時に以下の2形式を両方サポートする必要がある:

- **従来形式（フィールドリスト直接指定）**: `structs: { main: [{ name: x, type: u8 }] }`
- **新形式（メタデータ付きオブジェクト）**: `structs: { main: { endianness: little, fields: [{ name: x, type: u8 }] } }`

これには `INodeDeserializer` カスタム実装、または `YamlFormatLoader` でデシリアライズ後に手動変換を行う方式が考えられる。実装の複雑さを考慮し、**`YamlStructModel` に統一し、従来形式はカスタムデシリアライザで `YamlStructModel` に変換する**方式を推奨する。

**YamlFieldModel への `endianness` 追加:**

```csharp
// src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs の YamlFieldModel 内
[YamlMember(Alias = "endianness")]
public string? Endianness { get; set; }
```

### DecodeContext の変更

**現在のエンディアン管理:**

`DecodeContext` はコンストラクタで受け取った `Endianness` を読み取り専用プロパティとして保持している（`public Endianness Endianness { get; }`）。全ての `ReadUInt16()`, `ReadUInt32()` 等がこのプロパティを参照してバイトオーダーを決定している。

**エンディアンスタックの設計:**

既存のスコープスタック (`_scopeStack`) の `Scope` クラスにエンディアン情報を追加する。これにより、スコープの push/pop に連動してエンディアンも自動管理される。

```csharp
// src/BinAnalyzer.Engine/DecodeContext.cs

private sealed class Scope(int start, int end, Endianness endianness)
{
    public int Start { get; } = start;
    public int End { get; } = end;
    public Endianness Endianness { get; } = endianness;
    public Dictionary<string, object> Variables { get; } = new();
}
```

**`Endianness` プロパティの変更:**

読み取り専用フィールドから、現在のスコープのエンディアンを返すプロパティに変更する。

```csharp
/// <summary>現在有効なエンディアン。スコープスタックの最上位から取得。</summary>
public Endianness Endianness => CurrentScope.Endianness;
```

**`ResolveEndianness` メソッドの設計:**

エンディアン解決ロジックは `BinaryDecoder` 側に配置し、DecodeContext にはエンディアン付きのスコープ操作メソッドを追加する。

```csharp
/// <summary>エンディアンを指定してスコープをプッシュする。</summary>
public void PushScope(int size, Endianness endianness)
{
    var end = _position + size;
    if (end > _data.Length)
        throw new InvalidOperationException(...);
    _scopeStack.Push(new Scope(_position, end, endianness));
}

/// <summary>サイズ制約なしでエンディアンのみ変更するスコープをプッシュする。</summary>
public void PushEndiannessScope(Endianness endianness)
{
    var current = CurrentScope;
    _scopeStack.Push(new Scope(current.Start, current.End, endianness));
}

/// <summary>一時的にエンディアンを切り替えて操作を実行する。</summary>
public T WithEndianness<T>(Endianness endianness, Func<T> action)
{
    PushEndiannessScope(endianness);
    try { return action(); }
    finally { PopEndiannessScope(); }
}

/// <summary>エンディアンスコープをポップする（位置は進めない）。</summary>
public void PopEndiannessScope()
{
    if (_scopeStack.Count <= 1)
        throw new InvalidOperationException("Cannot pop the root scope");
    _scopeStack.Pop();
    // 注: PopScope() と異なり、位置をスコープ末尾に進めない
}
```

既存の `PushScope(int size)` は現在のエンディアンを引き継ぐオーバーロードとして残す:

```csharp
public void PushScope(int size)
{
    PushScope(size, Endianness);
}
```

### BinaryDecoder の変更

**構造体デコード時のエンディアン push/pop:**

`DecodeStruct` で構造体にエンディアンが指定されている場合、エンディアンスコープをプッシュする。

```csharp
private DecodedStruct DecodeStruct(
    StructDefinition structDef, FormatDefinition format,
    DecodeContext context, string name)
{
    var startOffset = context.Position;
    var pushedEndianness = false;

    if (structDef.Endianness is { } endianness)
    {
        context.PushEndiannessScope(endianness);
        pushedEndianness = true;
    }

    try
    {
        var children = new List<DecodedNode>();
        foreach (var field in structDef.Fields)
        {
            var node = DecodeField(field, format, context, children);
            if (node is not null)
                children.Add(node);
        }

        return new DecodedStruct { ... };
    }
    finally
    {
        if (pushedEndianness)
            context.PopEndiannessScope();
    }
}
```

**フィールドデコード時のエンディアン一時切り替え:**

`DecodeSingleField` で、フィールドにエンディアンが指定されている場合のみ一時切り替えを行う。

```csharp
private DecodedNode DecodeSingleField(
    FieldDefinition field, FormatDefinition format,
    DecodeContext context, IReadOnlyList<DecodedNode>? siblings = null)
{
    if (field.Endianness is { } endianness)
    {
        return context.WithEndianness(endianness,
            () => DecodeSingleFieldCore(field, format, context, siblings));
    }

    return DecodeSingleFieldCore(field, format, context, siblings);
}
```

**ReadInteger 等でのエンディアン指定:**

`DecodeContext.ReadUInt16()` 等の既存メソッドは変更不要。これらは既に `Endianness` プロパティを参照しており、プロパティがスコープスタック経由で解決されるよう変更するだけで自動的に正しいエンディアンが使用される。

**`WithoutRepeat` メソッドへの `Endianness` 追加:**

```csharp
private static FieldDefinition WithoutRepeat(FieldDefinition field)
{
    return new FieldDefinition
    {
        // ...既存プロパティ...
        Endianness = field.Endianness,
    };
}
```

### YamlToIrMapper の変更

**endianness 文字列のパース:**

共通のパースヘルパーを追加する。トップレベルの既存パースロジックと統一する。

```csharp
private static Endianness? ParseOptionalEndianness(string? value)
{
    return value?.ToLowerInvariant() switch
    {
        "big" or "be" => Endianness.Big,
        "little" or "le" => Endianness.Little,
        null => null,
        _ => throw new InvalidOperationException($"Unknown endianness: {value}"),
    };
}
```

**MapStructs の変更:**

```csharp
private static IReadOnlyDictionary<string, StructDefinition> MapStructs(
    Dictionary<string, YamlStructModel> yamlStructs)
{
    var result = new Dictionary<string, StructDefinition>();
    foreach (var (name, model) in yamlStructs)
    {
        result[name] = new StructDefinition
        {
            Name = name,
            Fields = model.Fields.Select(MapField).ToList(),
            Align = model.Align,
            Endianness = ParseOptionalEndianness(model.Endianness),
        };
    }
    return result;
}
```

**MapField の変更:**

```csharp
private static FieldDefinition MapField(YamlFieldModel yaml)
{
    // ...既存処理...
    return new FieldDefinition
    {
        // ...既存プロパティ...
        Endianness = ParseOptionalEndianness(yaml.Endianness),
    };
}
```

### FormatValidator の変更

新しいバリデーションルール `VAL011` を追加する。

```csharp
/// <summary>VAL011: endianness の値が不正</summary>
private static void ValidateEndianness(
    FieldDefinition field, string structName,
    List<ValidationDiagnostic> diagnostics)
{
    // IR レベルでは Endianness? 型なので、パース時に不正値はエラーになる。
    // バリデーターでは追加チェック不要（型安全）。
}
```

実際にはIRモデルの `Endianness?` 型はenum値かnullのみ取りうるため、FormatValidator 側での追加バリデーションは不要。不正な文字列値のバリデーションは `YamlToIrMapper.ParseOptionalEndianness` でパース時にエラーとなる。

ただし、バリデーター側で以下の警告を追加することを検討する:

- **VAL111**: 構造体レベルのエンディアンがトップレベルと同一（冗長指定の警告）
- **VAL112**: フィールドレベルのエンディアンが構造体レベルと同一（冗長指定の警告）

冗長指定の警告は優先度低とし、初回実装ではスキップ可能。

### JSON Schema 変更

`bdef.schema.json` に以下の変更を加える。

**構造体定義の変更:**

現在の構造体定義（`structs` の `additionalProperties`）はフィールド配列を直接受け取る形式。これをオブジェクト形式（`endianness` + `fields`）に変更し、`oneOf` で従来のフィールド配列形式も許容する。

```json
"structs": {
  "type": "object",
  "description": "構造体の定義。キーが構造体名。",
  "additionalProperties": {
    "oneOf": [
      {
        "type": "array",
        "description": "構造体のフィールドリスト（簡略形式）",
        "items": { "$ref": "#/$defs/field" }
      },
      {
        "$ref": "#/$defs/structDefinition"
      }
    ]
  }
}
```

新しい `$defs/structDefinition`:

```json
"structDefinition": {
  "type": "object",
  "description": "メタデータ付き構造体定義",
  "required": ["fields"],
  "additionalProperties": false,
  "properties": {
    "endianness": {
      "type": "string",
      "enum": ["big", "little", "be", "le"],
      "description": "この構造体内のフィールドに適用するバイトオーダー。省略時は親に従う。"
    },
    "align": {
      "type": "integer",
      "minimum": 1,
      "description": "繰り返し時の各インスタンスのアライメント（バイト単位）。"
    },
    "fields": {
      "type": "array",
      "description": "構造体のフィールドリスト",
      "items": { "$ref": "#/$defs/field" }
    }
  }
}
```

**フィールド定義への `endianness` 追加:**

```json
"endianness": {
  "type": "string",
  "enum": ["big", "little", "be", "le"],
  "description": "このフィールドのみに適用するバイトオーダー。省略時は構造体またはトップレベルに従う。"
}
```

### テスト計画

| テストカテゴリ | テスト内容 | 対応する受入条件 |
|---|---|---|
| DSLパース | 構造体レベル endianness のパース（big/little/be/le） | AC-1 |
| DSLパース | フィールドレベル endianness のパース | AC-2 |
| DSLパース | endianness 未指定時に null になること | AC-4 |
| DSLパース | 不正な endianness 値でエラーが発生すること | -- |
| DSLパース | 従来形式（フィールドリスト直接指定）が引き続き動作すること | AC-6 |
| デコード | 構造体レベル endianness でフィールドが正しくデコードされること | AC-1, AC-5 |
| デコード | フィールドレベル endianness でフィールドが正しくデコードされること | AC-2, AC-5 |
| デコード | フィールド > 構造体 > トップレベルの優先順位 | AC-3 |
| デコード | ネスト構造体でのエンディアン継承 | AC-4 |
| デコード | 同一バイナリ内でビッグ/リトルが混在するケース | AC-5 |
| バリデーション | 既存のバリデーションルールへの影響なし | AC-9 |
| 統合テスト | 既存フォーマット（png, bmp, wav, zip, elf, pdf）が引き続き正常動作 | AC-6, AC-9 |
| Schema | JSON Schema で endianness 指定が許可されること | AC-8 |

### 懸念事項

**ビットフィールドとエンディアンの関係:**

ビットフィールド（`bitfield` 型）の内部はビット単位の操作であり、エンディアンは基底バイト列の読み取り時にのみ影響する。現在の `DecodeBitfieldField` は `ReadUInt8()`/`ReadUInt16()`/`ReadUInt32()` でバイト列を整数値に変換した後にビット操作を行うため、エンディアンの切り替えはこの整数読み取り部分に自然に適用される。REQ-045のスコープ外として明記されているとおり、ビットフィールド内部の個別ビットに対するエンディアン指定は行わない。

**ネスト構造体でのエンディアン継承:**

構造体Aが `endianness: little` を持ち、内部にネストされた構造体B（endianness未指定）がある場合、構造体Bのフィールドは構造体Aのリトルエンディアンを継承する。これはスコープスタック方式により自然に実現される。`PushEndiannessScope` は新しいスコープとして親のEnd境界を引き継ぎつつエンディアンを変更する。構造体Bが自身のエンディアンを持つ場合はさらに上書きされる。

**既存フォーマット定義への影響がないこと:**

- `StructDefinition.Endianness` と `FieldDefinition.Endianness` はどちらも nullable で、デフォルトは null。
- null の場合は何も push しないため、既存の動作（トップレベルのエンディアンのみ使用）が維持される。
- YAMLデシリアライズの互換性は `YamlStructModel` 導入時にカスタムデシリアライザで担保する。
- 全ての既存統合テストの通過を確認する。

**`DecodeCompressedField` でのエンディアン伝搬:**

圧縮フィールドの展開データを `DecodeStruct` する際、新しい `DecodeContext` が作成される（L342: `new DecodeContext(decompressed, format.Endianness)`）。この箇所は `format.Endianness`（トップレベル）を使用しているが、圧縮フィールドが構造体レベルのエンディアン指定の中にある場合、現在のコンテキストのエンディアンを引き継ぐべきか検討が必要。推奨: `context.Endianness` を渡すように変更する。

**スコープスタックの整合性:**

`PushEndiannessScope` / `PopEndiannessScope` と既存の `PushScope` / `PopScope` が混在する場合の整合性に注意が必要。エンディアンスコープは位置を進めないため、`PopEndiannessScope` は `PopScope` と異なる挙動を持つ。実装時には、スコープの種類（サイズスコープ vs エンディアンスコープ）をフラグで区別し、ポップ時に正しい処理を行うことを検討する。あるいは、Scope クラスに `isEndianOnlyScope` フラグを追加し、`PopScope` 内で分岐する方が安全。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
