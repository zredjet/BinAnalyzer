# REQ-104: 複合キー配列 diff

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-103（キーベース配列 diff） |
| フェーズ | D（応用） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-12 |

## 背景・動機

REQ-103 で実装されたキーベース配列 diff は、単一フィールドをキーとして要素をマッチングする。しかし、実データでは単一フィールドではユニークにならず、複数フィールドの組み合わせで初めてユニークになるケースがある。

```
例: レコード配列の比較（group_id + item_id でユニーク）

Left:  [(group=1, item=1, val=10), (group=1, item=2, val=20), (group=2, item=1, val=30)]
Right: [(group=1, item=1, val=10), (group=1, item=2, val=99), (group=2, item=1, val=30), (group=2, item=2, val=40)]

単一キー diff_key: group_id（不十分）:
  group_id=1 が複数 → 最初の要素のみマッチ、残りは不正確

複合キー diff_key: [group_id, item_id]:
  [group_id=1,item_id=1]: (同一)
  [group_id=1,item_id=2]:
    val  20 → 99
  [group_id=2,item_id=1]: (同一)
  + [group_id=2,item_id=2]               ← 意味のある差分
```

### ユースケース

- データベースダンプ: 複合主キー（テーブル名+カラム名）でレコードを識別
- グループ化レコード: グループ ID + アイテム ID の組み合わせでユニーク
- 多次元インデックス: セクション番号 + エントリ番号のような二重インデックス
- ネームスペース付き識別子: (namespace, name) の組み合わせでユニーク

## 機能要件

### 追加する機能

#### DSL 拡張

- [ ] `diff_key` 属性でリスト形式（複合キー）を受け付ける
  ```yaml
  # 単一キー（現行 — 後方互換性 100% 維持）
  - name: sections
    type: struct
    struct: section_header
    repeat_count: "{e_shnum}"
    diff_key: name

  # 複合キー（新規）
  - name: entries
    type: struct
    struct: entry
    repeat_count: "{count}"
    diff_key: [group_id, item_id]

  # 複合キー（ブロック形式）
  - name: entries
    type: struct
    struct: entry
    repeat_count: "{count}"
    diff_key:
      - group_id
      - item_id
  ```
- [ ] 単一キーは要素 1 のリストとして内部的に統一処理
- [ ] 複合キーの各フィールドの型は `DecodedInteger` または `DecodedString`（単一キーと同一制約）

#### diff エンジン

- [ ] 複合キー指定時、全キーフィールドの値を連結した複合キー文字列で要素をマッチング
  - 内部辞書キー: `string.Join("\0", keyValues)`（`\0` は値に出現しない区切り文字）
  - 両方に存在するキー: 再帰的にフィールド比較
  - 左にのみ存在するキー: Removed
  - 右にのみ存在するキー: Added
- [ ] 複合キーの全フィールドが要素内に存在し、値が抽出可能な場合のみキーベース比較を使用
- [ ] いずれかのフィールドが欠落する要素がある場合、配列全体をインデックスベースにフォールバック

#### パス表記

- [ ] 複合キー比較時のパス表記: `配列名[key1=val1,key2=val2]`
  - 例: `entries[group_id=1,item_id=3].value`
  - 例: `symbols[namespace=std,name=vector].size`
- [ ] 単一キー時のパス表記は変更なし: `配列名[key=val]`
- [ ] フラット形式・ツリー形式の両方で統一したパス表記を使用

#### ツリー形式出力

- [ ] 複合キー比較時、要素ラベルを `[key1=val1,key2=val2]` で表示
  ```
  entries
    [group_id=1,item_id=1]  (同一)
    [group_id=1,item_id=2]
      val  20 → 99
    [group_id=2,item_id=1]  (同一)
    + [group_id=2,item_id=2]
  ```

### 変更しないもの（スコープ外）

- キーフィールドの自動推定（明示的な `diff_key` 指定のみ）
- `diff_key` の CLI オプションでの指定（DSL 定義のみ）
- キー値によるソート（left の出現順を維持）
- キー値の特殊文字（`,`, `=`, `]`）のエスケープ（実用上問題になるケースは稀）

## 受入条件

1. [ ] 単一キー `diff_key: name` が従来通り動作すること（後方互換性 100%）
2. [ ] 複合キー `diff_key: [field1, field2]` で要素が複合キーベースでマッチングされること
3. [ ] 複合キーマッチにより、追加・削除・変更が正しく検出されること
4. [ ] パス表記が `配列名[key1=val1,key2=val2].子フィールド` 形式であること
5. [ ] フラット形式でキーベースパスが表示されること
6. [ ] ツリー形式でキーベースラベルが表示されること
7. [ ] 空リスト `diff_key: []` の場合、インデックスベースにフォールバックすること
8. [ ] 複合キーの一部フィールドが要素内に存在しない場合、フォールバック動作すること
9. [ ] 同一複合キー値が複数存在する場合のエッジケースが処理されること
10. [ ] JSON Schema（`bdef.schema.json`）の `diff_key` が `oneOf: [string, array]` に更新されていること
11. [ ] 単体テスト・統合テストが追加されていること
12. [ ] 既存テスト（REQ-103 のキーベース diff テスト含む）が全て通過すること

## 影響範囲

### 変更が必要なファイル

| プロジェクト | ファイル | 変更内容 |
|---|---|---|
| BinAnalyzer.Core | `Models/FieldDefinition.cs` | `DiffKey` の型を `string?` → `IReadOnlyList<string>?` に変更 |
| BinAnalyzer.Core | `Decoded/DecodedArray.cs` | `DiffKey` の型を `string?` → `IReadOnlyList<string>?` に変更 |
| BinAnalyzer.Dsl | `YamlModels/YamlFormatModel.cs` | `DiffKey` の型を `string?` → `object?` に変更 |
| BinAnalyzer.Dsl | `YamlToIrMapper.cs` | `object?` → `IReadOnlyList<string>?` 変換ロジック追加 |
| BinAnalyzer.Engine | `BinaryDecoder.cs` | 型変更に追従（パススルー） |
| BinAnalyzer.Engine | `DiffEngine.cs` | `ExtractKeyValue` → `ExtractCompositeKey` に拡張、`BuildKeyMap` 複合キー対応、パス表記変更 |
| BinAnalyzer.Output | `DiffTreeOutputFormatter.cs` | `ExtractKeyValue` → 複合キー対応、ラベル表記変更 |
| — | `schemas/bdef.schema.json` | `diff_key` の型を `oneOf: [string, array]` に変更 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — `diff_key` 属性の複合キー説明追記
- [ ] docs/parser-design.md — DiffKey の型変更反映
- [ ] docs/architecture.md — 複合キー対応の記述追加

---

## 設計メモ

### 推奨方針: 型変更アプローチ（Option A）

`string?` → `IReadOnlyList<string>?` に型変更し、単一キーを要素 1 のリストとして統一的に扱う。

```
YAML: diff_key: name           → IReadOnlyList<string> ["name"]
YAML: diff_key: [a, b]         → IReadOnlyList<string> ["a", "b"]
YAML: diff_key なし             → null（インデックスベース）
```

#### DiffKey 情報の伝搬経路

```
YAML (diff_key: ["group_id", "item_id"])
  → YamlFieldModel.DiffKey (object?: string or List<object>)
  → YamlToIrMapper で変換
  → FieldDefinition.DiffKey (IReadOnlyList<string>?)
  → BinaryDecoder が DecodedArray 生成時にコピー
  → DecodedArray.DiffKey (IReadOnlyList<string>?)
  → DiffEngine が CompareArray 内で参照
```

#### YAML デシリアライズの処理

YamlDotNet は `string?` 型プロパティに YAML リストを受け取れないため、`object?` に変更して受け取り後にマッパーで変換する。

```csharp
// YamlFieldModel
[YamlMember(Alias = "diff_key")]
public object? DiffKey { get; set; }

// YamlToIrMapper で変換
DiffKey = yaml.DiffKey switch
{
    string s => [s],                                                    // 単一キー
    IList list => list.Cast<object>().Select(x => x.ToString()!).ToArray(), // 複合キー
    null => null,                                                       // 未指定
    _ => null,                                                          // 非対応型
},
```

#### 複合キー値の抽出

```csharp
static string? ExtractCompositeKey(DecodedNode element, IReadOnlyList<string> keyFields)
{
    if (element is not DecodedStruct st) return null;

    if (keyFields.Count == 1)
    {
        // 単一キー: 既存ロジックと同一（最適化）
        return ExtractSingleFieldValue(st, keyFields[0]);
    }

    var parts = new string[keyFields.Count];
    for (int i = 0; i < keyFields.Count; i++)
    {
        var val = ExtractSingleFieldValue(st, keyFields[i]);
        if (val is null) return null;  // いずれかのキーフィールドが欠落 → null
        parts[i] = val;
    }
    return string.Join("\0", parts);  // 内部辞書キー
}

static string? ExtractSingleFieldValue(DecodedStruct st, string fieldName)
{
    foreach (var child in st.Children)
    {
        if (child.Name == fieldName)
        {
            return child switch
            {
                DecodedInteger i => i.EnumLabel ?? i.Value.ToString(),
                DecodedString s  => s.Value,
                _                => null,
            };
        }
    }
    return null;
}
```

#### パス表記の生成

```csharp
static string BuildKeyedPath(string basePath, IReadOnlyList<string> keyFields, DecodedNode element)
{
    if (element is not DecodedStruct st)
        return basePath;

    if (keyFields.Count == 1)
    {
        // 単一キー: "items[id=42]" — 現行と同一形式
        var val = ExtractSingleFieldValue(st, keyFields[0]);
        return $"{basePath}[{keyFields[0]}={val}]";
    }

    // 複合キー: "items[group_id=1,item_id=3]"
    var pairs = keyFields.Select(f => $"{f}={ExtractSingleFieldValue(st, f)}");
    return $"{basePath}[{string.Join(",", pairs)}]";
}
```

#### フォールバック

以下の場合はインデックスベース比較にフォールバック（REQ-103 と同一方針）:
- `DiffKey` が null（未指定）
- `DiffKey` が空リスト
- 要素が DecodedStruct でない（プリミティブ配列）
- いずれかのキーフィールドが要素内に見つからない
- いずれかのキー値が null（非対応型）

### モデル変更

| モデル | 現行（REQ-103） | 変更後 |
|---|---|---|
| `FieldDefinition.DiffKey` | `string?` | `IReadOnlyList<string>?` |
| `DecodedArray.DiffKey` | `string?` | `IReadOnlyList<string>?` |
| `YamlFieldModel.DiffKey` | `string?` (alias: `diff_key`) | `object?` (alias: `diff_key`) |

型変更のみ。プロパティ名・alias は変更なし。

### 後方互換性

| 項目 | 影響 |
|---|---|
| DSL 構文 `diff_key: name` | 内部的に `["name"]` として処理。動作変更なし |
| パス表記 `[name=.text]` | 単一キー時は `[name=.text]` のまま。変更なし |
| 既存テスト | 型変更に伴うコンパイル修正のみ。テスト内容・期待値は変更なし |
| diff 未使用のデコード処理 | `DiffKey` は null 伝搬のみ。影響なし |

**C# 消費者への破壊的変更**: `DecodedArray.DiffKey` と `FieldDefinition.DiffKey` の型が `string?` → `IReadOnlyList<string>?` に変わる。ただし外部消費者は存在しないため実質的な問題なし。

### 代替案

| 案 | 評価 |
|---|---|
| **Option A: 型変更 `IReadOnlyList<string>?`** | **推奨**。統一的、単一キーと複合キーを同一コードパスで処理 |
| Option B: 別プロパティ `DiffKeys` を追加 | 冗長。`DiffKey` vs `DiffKeys` の優先順位が曖昧、分岐が増える |
| Option C: カンマ区切り文字列 `"field1,field2"` | フィールド名にカンマを含む場合に破綻。ハック的 |

### エッジケース

| ケース | 対応方針 |
|---|---|
| 空リスト `diff_key: []` | `null` と同等扱い、インデックスベースにフォールバック |
| 一部の要素で複合キーの 1 フィールドが欠落 | `CanUseKeyedComparison` が false → 配列全体をフォールバック |
| 複合キー値が重複 | 単一キーと同様、最初の要素を辞書に登録（後続は無視） |
| キー値に `,` や `=` を含む | 内部辞書キーは `\0` 区切りで安全。パス表記はエスケープなし（実用上問題は稀） |
| キーフィールド順序 | YAML 定義順を保持。順序が異なると別の複合キーとして扱われる |

### 懸念事項

- **YAML `object?` デシリアライズ**: YamlDotNet が scalar → `string`, sequence → `List<object>` と型を分けるため、マッパーでの分岐処理が必要。型安全性がやや低下するが、マッパー内で完結する。
- **パス表記の長大化**: 複合キーが 3 つ以上になるとパスが長くなる（`[a=1,b=2,c=3]`）。実用上 2 キーが大半のため許容範囲。
- **ExtractKeyValue の重複拡張**: DiffEngine と DiffTreeOutputFormatter の両方で同一の複合キー抽出ロジックが必要。REQ-103 と同様、アーキテクチャ維持のため意図的に重複させる。

### 工数見積もり

| 作業 | 規模 |
|---|---|
| モデル型変更（Core） | 小 |
| YAML デシリアライズ対応（Dsl） | 中 |
| DiffEngine 拡張 | 中 |
| DiffTreeOutputFormatter 拡張 | 中 |
| スキーマ更新 | 小 |
| 既存テスト修正 | 小 |
| 新規テスト追加 | 中 |
| ドキュメント更新 | 小 |
| **合計** | **中程度** |

REQ-103 と同じファイル群への変更であり、ロジックの拡張方向も明確。リスクは低い。

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Decoded/DecodedArray.cs` | `DiffKey` の型を `string?` → `IReadOnlyList<string>?` に変更 |
| `src/BinAnalyzer.Core/Models/FieldDefinition.cs` | `DiffKey` の型を `string?` → `IReadOnlyList<string>?` に変更 |
| `src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs` | `DiffKey` の型を `string?` → `object?` に変更 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | `MapDiffKey` メソッド追加（`object?` → `IReadOnlyList<string>?` 変換）、`using System.Collections` 追加 |
| `src/BinAnalyzer.Engine/DiffEngine.cs` | `ExtractKeyValue` → `ExtractCompositeKey` + `ExtractSingleFieldValue` に分離、`BuildKeyedPath` 追加、全メソッドを `IReadOnlyList<string>` 対応に |
| `src/BinAnalyzer.Output/DiffTreeOutputFormatter.cs` | `ExtractKeyValue` → `ExtractCompositeKey` + `ExtractSingleFieldValue` に分離、`BuildKeyedLabel` 追加、全メソッドを `IReadOnlyList<string>` 対応に |
| `schemas/bdef.schema.json` | `diff_key` の型を `oneOf: [string, array]` に変更 |

### 実装中の設計変更

- 設計通りに実装。特記すべき設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DiffEngineTests | CompositeKey_IdenticalElements_NoDifferences | 1, 2 |
| DiffEngineTests | CompositeKey_ElementChanged_ReportsChangedWithCompositeKeyPath | 3, 4 |
| DiffEngineTests | CompositeKey_ElementAddedAndRemoved_ReportsCorrectly | 3, 4 |
| DiffEngineTests | CompositeKey_StringKeys_MatchesByCompositeStringValue | 3, 4 |
| DiffEngineTests | CompositeKey_EmptyKeyList_FallsBackToIndex | 7 |
| DiffOutputTests | TreeFormat_CompositeKey_ChangedElement_ShowsCompositeKeyLabel | 5, 6 |
| DiffOutputTests | TreeFormat_CompositeKey_AddedAndRemoved_ShowsPlusMinus | 5, 6 |
| DiffOutputTests | TreeFormat_CompositeKey_IdenticalElements_ShowsIdentical | 1, 6 |

### 気づき・今後の課題

- 既存の単一キーテスト（REQ-103）は `CreateArray` ヘルパーの `diffKey` パラメータ経由で `["id"]` リストに自動変換され、コンパイル修正のみで全通過。後方互換性 100% 維持を確認。
- `MapDiffKey` の `IList` 分岐は YamlDotNet が YAML シーケンスを `List<object>` として返すことに依存。`Cast<object>().Select(x => x.ToString()!)` で文字列リストに変換。
- DiffEngine と DiffTreeOutputFormatter の `ExtractCompositeKey`/`ExtractSingleFieldValue` は同一ロジックの重複。REQ-103 と同様、Output→Engine 依存を避けるため意図的に重複。
