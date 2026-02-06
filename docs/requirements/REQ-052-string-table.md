# REQ-052: 文字列テーブル参照

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-044（オフセットジャンプ） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

ELFの `.strtab`（文字列テーブル）やPEのインポート名テーブルでは、構造体フィールドにオフセット値が格納され、そのオフセットが別の領域の文字列を参照する構造が使われる。

現在のBinAnalyzerでは、オフセット値は数値として表示されるのみで、実際の文字列を引くことができない。`string_table` 機能により、オフセット値から参照先の文字列を自動解決し、解析結果の可読性を大幅に向上させる。

この機能はオフセットジャンプ（REQ-044）の上に構築される特殊化された参照パターン。

## 機能要件

### 追加する機能

- [ ] DSLに `string_table` プロパティを追加
  ```yaml
  structs:
    symtab_entry:
      fields:
        - name: st_name
          type: u32
          string_table: ".strtab"   # 参照先テーブルの名前
  ```
- [ ] 文字列テーブルの定義方法
  ```yaml
  structs:
    strtab:
      string_table: true            # この構造体を文字列テーブルとして登録
      fields:
        - name: data
          type: bytes
          size: remaining
  ```
- [ ] `string_table` の動作仕様
  - フィールド値（整数）をテーブル内のオフセットとして解釈
  - テーブル先頭からのオフセット位置のNULL終端文字列を読み取る
  - 解決された文字列をDecodedIntegerにラベルとして付与（enumラベルと同様の表示）
- [ ] デコード順序の考慮
  - 文字列テーブルが先にデコードされている必要がある
  - テーブルが未デコードの場合はオフセット値のみ表示（エラーにしない）

### 変更する既存機能

- [ ] `FieldDefinition` — `StringTableRef` プロパティ追加
- [ ] `StructDefinition` — `IsStringTable` プロパティ追加
- [ ] `DecodeContext` — デコード済み文字列テーブルの保持
- [ ] `BinaryDecoder` — 文字列テーブル登録、参照解決
- [ ] `YamlFormatModel` — `string_table` プロパティ追加
- [ ] `YamlToIrMapper` — string_table のパース
- [ ] `DecodedInteger` — 文字列テーブルから解決されたラベルの表示

### 変更しないもの（スコープ外）

- Unicode文字列テーブル（UTF-16等）— 将来課題
- 文字列テーブルの動的オフセット解決（テーブル自体のオフセットが動的な場合）
- 複数文字列テーブルの自動選択

## 受入条件

1. [ ] `string_table` プロパティで文字列テーブルを参照できること
2. [ ] 整数フィールドの値がテーブル内のオフセットとして解釈されること
3. [ ] 解決された文字列がラベルとして表示されること
4. [ ] テーブルが未デコードの場合にエラーにならないこと（オフセット値のみ表示）
5. [ ] 不正なオフセット（テーブル範囲外）で適切なエラーまたは警告が表示されること
6. [ ] tree/json/html の各出力形式で文字列が表示されること
7. [ ] DSLリファレンスに string_table の説明が追加されること
8. [ ] JSON Schemaに string_table プロパティが追加されること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldDefinition に StringTableRef、StructDefinition に IsStringTable 追加 |
| BinAnalyzer.Dsl | YamlFormatModel に string_table 追加、YamlToIrMapper でパース |
| BinAnalyzer.Engine | DecodeContext に文字列テーブル保持、BinaryDecoder に参照解決 |
| BinAnalyzer.Output | 変更なし（既存のenumラベル表示ロジックを再利用） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — string_table の説明、使用例
- [ ] docs/architecture.md — 文字列テーブル参照の仕組みの説明
- [ ] schemas/bdef.schema.json — string_table プロパティ追加

---

## 設計メモ

### 1. StructDefinition の変更（BinAnalyzer.Core）

`StructDefinition` に `IsStringTable` プロパティを追加する。

```csharp
// StructDefinition に追加
/// <summary>この構造体を文字列テーブルとして登録する。デコード時にraw bytesがDecodeContextに保持される。</summary>
public bool IsStringTable { get; init; }
```

### 2. FieldDefinition の変更（BinAnalyzer.Core）

`FieldDefinition` に `StringTableRef` プロパティを追加する。

```csharp
// FieldDefinition に追加
/// <summary>参照先の文字列テーブル名。整数フィールドの値をテーブル内オフセットとして解釈する。</summary>
public string? StringTableRef { get; init; }
```

### 3. DecodeContext の変更（BinAnalyzer.Engine）

デコード済み文字列テーブルを名前付きで保持する機能を追加する。

```csharp
// DecodeContext に追加
private readonly Dictionary<string, ReadOnlyMemory<byte>> _stringTables = new();

/// <summary>デコードされた構造体のraw bytesを文字列テーブルとして登録する。</summary>
public void RegisterStringTable(string name, ReadOnlyMemory<byte> data)
{
    _stringTables[name] = data;
}

/// <summary>文字列テーブルから指定オフセットのNULL終端文字列を解決する。</summary>
/// <returns>解決された文字列。テーブル未登録またはオフセット範囲外の場合はnull。</returns>
public string? ResolveString(string tableName, long offset)
{
    if (!_stringTables.TryGetValue(tableName, out var tableData))
        return null;

    if (offset < 0 || offset >= tableData.Length)
        return null;

    // NULL終端文字列を読み取る
    var span = tableData.Span[(int)offset..];
    var nullIndex = span.IndexOf((byte)0);
    var length = nullIndex >= 0 ? nullIndex : span.Length;
    return System.Text.Encoding.UTF8.GetString(span[..length]);
}
```

**テーブル名の規則:** 構造体インスタンスの名前（フィールド名）をテーブル名として使用する。例えばフィールド名 `.strtab` で `string_table: true` の構造体を参照した場合、テーブル名は `".strtab"` となる。

### 4. BinaryDecoder の変更（BinAnalyzer.Engine）

#### 4a. 文字列テーブルの登録

`DecodeStruct` メソッドで、`IsStringTable=true` の構造体をデコード後、そのraw bytesを `DecodeContext` に登録する。

```csharp
// DecodeStruct の末尾、return の前に追加
if (structDef.IsStringTable)
{
    var tableBytes = context.SliceOriginal((int)startOffset, (int)(context.Position - startOffset));
    context.RegisterStringTable(name, tableBytes);
}
```

**注意:** `SliceOriginal` は元のデータからスライスを取得するため、構造体全体のraw bytesが登録される。テーブル内のオフセットは構造体先頭からの相対位置として解釈する。ただし、構造体にヘッダーフィールド（例: テーブルサイズ）が含まれる場合、オフセット計算がずれる可能性がある。

**代替案（推奨）:** 構造体の最初の `bytes` 型フィールドのデータのみを登録する方式。より正確だが、構造体に `bytes` フィールドが1つだけという制約が暗黙に必要になる。実装時に判断する。最もシンプルな実装として、まず構造体全体を登録し、ELF `.strtab` 等の実際のユースケースで問題があれば改善する。

#### 4b. 文字列テーブル参照の解決

`DecodeIntegerField` メソッドで、`StringTableRef` が指定されているフィールドのデコード後に文字列を解決する。

```csharp
// DecodeIntegerField の EnumLabel 解決ロジックの後に追加
string? stringTableLabel = null;
if (field.StringTableRef is not null)
{
    stringTableLabel = context.ResolveString(field.StringTableRef, value);
}
```

**EnumLabel との統合:** 文字列テーブルから解決した文字列を `DecodedInteger.EnumLabel` に設定する。これにより、既存の TreeOutputFormatter / JsonOutputFormatter の enum ラベル表示ロジックをそのまま再利用できる。`EnumRef` と `StringTableRef` の両方が指定された場合は `EnumRef` を優先する。

```csharp
// DecodedInteger 生成時
EnumLabel = enumLabel ?? stringTableLabel,
EnumDescription = enumDesc,
```

### 5. YamlFormatModel の変更（BinAnalyzer.Dsl）

**YamlFieldModel に追加:**

```csharp
// YamlFieldModel に追加
[YamlMember(Alias = "string_table")]
public string? StringTable { get; set; }
```

**注意:** `string_table` プロパティは構造体レベル（`YamlFormatModel.Structs` の値）とフィールドレベル（`YamlFieldModel`）の2箇所で異なる意味を持つ。

- 構造体レベル: `string_table: true` → この構造体はテーブルである
- フィールドレベル: `string_table: ".strtab"` → このフィールドはテーブルを参照する

現在の `YamlFormatModel.Structs` は `Dictionary<string, List<YamlFieldModel>>` 型であり、構造体レベルのメタデータを持てない。構造体レベルの `string_table` を実現するには以下の変更が必要:

**案A（推奨）:** `YamlStructModel` を新規作成する。

```csharp
public sealed class YamlStructModel
{
    [YamlMember(Alias = "string_table")]
    public bool? StringTable { get; set; }

    [YamlMember(Alias = "align")]
    public int? Align { get; set; }

    [YamlMember(Alias = "fields")]
    public List<YamlFieldModel> Fields { get; set; } = new();
}
```

`YamlFormatModel.Structs` の型を `Dictionary<string, YamlStructModel>` に変更する。ただし、この変更は既存の YAML 構文（構造体を直接フィールドリストとして定義）との後方互換性を壊す。

**案B:** 既存構文との互換を維持するため、YAMLのカスタムデシリアライザーで、リスト形式とオブジェクト形式の両方をサポートする。実装コストが高い。

**案C（段階的導入）:** 初回実装では構造体レベルの `string_table: true` は導入せず、フィールドレベルの `string_table` のみ実装する。テーブルの登録は「`bytes` 型 + `size: remaining` + 特定の命名規則」のフィールドを検出して自動登録するか、フィールドレベルの `register_string_table: true` プロパティで明示的に登録する方式とする。

### 6. YamlToIrMapper の変更（BinAnalyzer.Dsl）

`MapField` で `StringTableRef` を設定する。

```csharp
// MapField の FieldDefinition 初期化に追加
StringTableRef = yaml.StringTable,
```

構造体レベルの `IsStringTable` は、`MapStructs` で `YamlStructModel` から設定する（案Aの場合）。

### 7. WithoutRepeat メソッドの更新（BinAnalyzer.Engine）

`BinaryDecoder.WithoutRepeat` に `StringTableRef` を追加する。

```csharp
StringTableRef = field.StringTableRef,
```

### 8. 懸念事項と対策

**デコード順序依存性:** 文字列テーブルは参照元より先にデコードされている必要がある。YAMLでの構造体定義順序がデコード順序を決定するため、ユーザーがテーブルを先に配置する必要がある。テーブルが未登録の場合は `ResolveString` が null を返し、ラベルなしの数値として表示される（エラーにはしない）。ドキュメントでこの制約を明記する。

**NULL終端文字列の読み取り:** `ResolveString` メソッドで `IndexOf((byte)0)` を使用し、NULL終端を検出する。テーブル末尾にNULLがない場合はテーブル末尾までを文字列とする。エンコーディングはUTF-8を固定とし、UTF-16等は将来課題とする。

**オフセット値の解釈:** `value` はテーブルの先頭からの相対バイトオフセットとして解釈する。テーブル登録時のバイト列の先頭がオフセット0に対応する。

**出力フォーマッターの変更:** `EnumLabel` を再利用するため、出力フォーマッターへの変更は不要。TreeOutputFormatter の `FormatInteger` は既に `EnumLabel` をマゼンタ色で表示する。ただし、文字列テーブル由来であることを区別したい場合は、将来的に `DecodedInteger` に `StringTableResolved` フラグを追加し、表示を変えることも検討する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
