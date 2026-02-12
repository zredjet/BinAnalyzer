# REQ-103: キーベース配列 diff

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-101（構造化 diff） |
| フェーズ | D（応用） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-12 |

## 背景・動機

REQ-101 で実装された構造化 diff は、配列（repeat）要素をインデックスベースで比較する。つまり `left.Elements[i]` と `right.Elements[i]` を 1:1 で対応させる。

実データのバイナリフォーマットでは、構造体配列の各要素にユニーク ID（セクション番号、チャンク ID、エントリ ID 等）が含まれることが多い。この場合、要素の挿入・削除・並び替えが発生すると、インデックスがずれて以降の全要素が「変更」として報告される。

```
例: ELF セクションヘッダの比較

Left:  [section_0(NULL), section_1(.text), section_2(.data), section_3(.bss)]
Right: [section_0(NULL), section_1(.text), section_2(.rodata), section_3(.data), section_4(.bss)]

インデックスベース（現状）:
  ~ sections[2].name: ".data" → ".rodata"     ← .rodata の挿入なのに
  ~ sections[3].name: ".bss" → ".data"        ← 全部ずれて報告される
  + sections[4]: (added)

キーベース（diff_key: name）:
  + sections[name=.rodata]: (added)            ← 意味のある差分
  sections[name=.text]: (同一)
  sections[name=.data]: (同一)
  sections[name=.bss]: (同一)
```

ユニーク ID フィールドをキーとして要素をマッチングすることで、構造的に意味のある差分を出力できるようにする。

### ユースケース

- ELF: セクションヘッダを `sh_name`（文字列テーブル値）で比較 — セクションの追加・削除を検出
- PE: セクションテーブルを `name` で比較
- PNG: チャンクを `type`（"IHDR", "IDAT" 等）で比較
- MIDI: トラックをインデックスではなくトラック名で比較
- 汎用: レコード配列を ID フィールドで比較

## 機能要件

### 追加する機能

#### DSL 拡張

- [ ] フィールド定義に `diff_key` 属性を追加
  ```yaml
  - name: sections
    type: struct
    struct: section_header
    repeat_count: "{e_shnum}"
    diff_key: name            # 配列要素内の子フィールド名を指定
  ```
- [ ] `diff_key` は配列（repeat 系属性を持つ）フィールドでのみ有効
- [ ] `diff_key` に指定できるフィールドの型は `DecodedInteger` または `DecodedString`
  - 整数の場合: `Value.ToString()` を辞書キーとする
  - 文字列の場合: `Value` を辞書キーとする

#### diff エンジン

- [ ] `diff_key` が指定された配列では、要素をキー値でマッチングして比較する
  - 両方に存在するキー: 再帰的にフィールド比較
  - 左にのみ存在するキー: Removed
  - 右にのみ存在するキー: Added
- [ ] `diff_key` が未指定の配列は従来のインデックスベース比較（後方互換性 100%）

#### パス表記

- [ ] キーベース比較時のパス表記: `配列名[キーフィールド名=キー値]`
  - 例: `sections[name=.text].size`, `chunks[type=IHDR].data.width`
  - 整数キーの場合: `entries[id=42].value`
- [ ] フラット形式・ツリー形式の両方で統一したパス表記を使用

#### ツリー形式出力

- [ ] キーベース比較時、要素ラベルを `[キーフィールド名=キー値]` で表示
  ```
  sections
    [name=.text]  (同一)
    [name=.data]
      size  1024 → 2048
    + [name=.rodata]
    - [name=.debug_info]
  ```

### 変更しないもの（スコープ外）

- 複合キー（複数フィールドの組み合わせ）のサポート
- キーフィールドの自動推定（明示的な `diff_key` 指定のみ）
- `diff_key` の CLI オプションでの指定（DSL 定義のみ）
- キー値によるソート（left の出現順を維持）

## 受入条件

1. [ ] `diff_key` が指定された配列で、要素がキー値ベースでマッチングされること
2. [ ] キーマッチにより、追加・削除・変更が正しく検出されること
3. [ ] パス表記が `配列名[キーフィールド名=キー値].子フィールド` 形式であること
4. [ ] フラット形式（`--output flat`）でキーベースパスが表示されること
5. [ ] ツリー形式（`--output tree`）でキーベースラベルが表示されること
6. [ ] `diff_key` 未指定の配列は従来のインデックスベース比較のままであること（後方互換性）
7. [ ] `diff_key` に指定されたフィールドが存在しない要素がある場合、フォールバック動作すること
8. [ ] 同一キー値が複数存在する場合のエッジケースが処理されること
9. [ ] JSON Schema（`bdef.schema.json`）に `diff_key` が追加されていること
10. [ ] 単体テスト・統合テストが追加されていること
11. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なファイル

| プロジェクト | ファイル | 変更内容 |
|---|---|---|
| BinAnalyzer.Core | `Models/FieldDefinition.cs` | `string? DiffKey` プロパティ追加 |
| BinAnalyzer.Core | `Decoded/DecodedArray.cs` | `string? DiffKey` プロパティ追加 |
| BinAnalyzer.Dsl | `YamlModels/YamlFormatModel.cs` | `YamlFieldModel` に `diff_key` プロパティ追加 |
| BinAnalyzer.Dsl | `DslParser.cs` | YAML → IR マッピングに `DiffKey` 追加 |
| BinAnalyzer.Engine | `BinaryDecoder.cs` | DecodedArray 生成時に `DiffKey` を伝搬 |
| BinAnalyzer.Engine | `DiffEngine.cs` | `CompareArrayByKey` メソッド追加、`CompareArray` 分岐 |
| BinAnalyzer.Output | `DiffOutputFormatter.cs` | パス表記の変更（影響軽微、DiffEntry のパスがエンジンで生成されるため） |
| BinAnalyzer.Output | `DiffTreeOutputFormatter.cs` | `FormatArray` のキーベース分岐、ラベル表記変更 |
| — | `schemas/bdef.schema.json` | `diff_key` フィールド定義追加 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — `diff_key` 属性の説明追記

---

## 設計メモ

> 設計Phaseで記入する。

### 設計方針

#### DiffKey 情報の伝搬経路

```
YAML (diff_key: "id")
  → YamlFieldModel.DiffKey
  → FieldDefinition.DiffKey
  → BinaryDecoder が DecodedArray 生成時にコピー
  → DecodedArray.DiffKey
  → DiffEngine が CompareArray 内で参照
```

DiffEngine は DecodedNode ツリーのみで完結し、FormatDefinition への依存を持たない。

#### キーベース比較アルゴリズム

```
CompareArrayByKey(left, right, path, keyField, entries):
  1. left.Elements から keyField の値を抽出し、Map<string, DecodedNode> を構築
  2. right.Elements から同様に Map を構築
  3. left の出現順に走査:
     - right にも同キーあり → CompareNodes で再帰比較
     - right になし → Removed
  4. right のみに存在するキー → Added（right の出現順）
  5. パス: "{path}[{keyField}={keyValue}]"
```

#### キー値抽出

```csharp
static string? ExtractKeyValue(DecodedNode element, string keyField)
{
    if (element is not DecodedStruct st) return null;
    var child = st.Children.FirstOrDefault(c => c.Name == keyField);
    return child switch
    {
        DecodedInteger i => i.Value.ToString(),
        DecodedString s  => s.Value,
        _                => null,  // 非対応型 → フォールバック
    };
}
```

#### フォールバック

以下の場合はインデックスベース比較にフォールバック:
- `DiffKey` が null（未指定）
- 要素が DecodedStruct でない（プリミティブ配列）
- キーフィールドが要素内に見つからない
- キー値が null（非対応型）

#### エッジケース: キー重複

同一配列内に同じキー値が複数存在する場合:
- 最初の要素を辞書に登録（後続は無視）
- ログ等での警告は検討するが、エラーにはしない

### モデル変更

| モデル | 追加 | 型 |
|---|---|---|
| `FieldDefinition` | `DiffKey` | `string?` |
| `DecodedArray` | `DiffKey` | `string?` |
| `YamlFieldModel` | `DiffKey` (alias: `diff_key`) | `string?` |

いずれも nullable の追加プロパティのみ。既存インタフェースへの破壊的変更なし。

### 代替案

| 案 | 不採用理由 |
|---|---|
| CLI オプションで `--diff-key sections=name` のように指定 | 配列ごとに異なるキーが必要で CLI が煩雑。フォーマット定義に紐づくべき情報 |
| FormatDefinition を DiffEngine に渡す | Engine が IR モデルの走査ロジックを持つことになり責務が増える |
| キーフィールドを自動推定（"id", "name" 等を探索） | フォーマットにより ID フィールド名が異なる。誤推定のリスクが高い |

### 懸念事項

- **パス表記の変更**: キーベース時のパス `items[id=42]` は既存の `items[0]` と互換性がない。diff 結果をプログラム的に消費している場合に影響する可能性がある。ただし現状は人間向け表示のみのため問題なし。
- **キー値に特殊文字**: キー値に `]` や `=` が含まれる場合のエスケープ。初期実装ではエスケープなし（実用上問題になるケースは稀）。
- **大量要素の性能**: Dictionary ベースのマッチングのため O(n) で処理可能。問題なし。

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Decoded/DecodedArray.cs` | `string? DiffKey` プロパティ追加 |
| `src/BinAnalyzer.Core/Models/FieldDefinition.cs` | `string? DiffKey` プロパティ追加 |
| `src/BinAnalyzer.Dsl/YamlModels/YamlFormatModel.cs` | `YamlFieldModel` に `diff_key` プロパティ追加 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | `DiffKey = yaml.DiffKey` マッピング追加 |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | DecodedArray 生成時に `DiffKey = field.DiffKey` 追加 |
| `src/BinAnalyzer.Engine/DiffEngine.cs` | `CompareArrayByKey`, `CanUseKeyedComparison`, `BuildKeyMap`, `ExtractKeyValue` メソッド追加 |
| `src/BinAnalyzer.Output/DiffTreeOutputFormatter.cs` | `FormatArrayByKey`, `CanUseKeyedFormat`, `ExtractKeyValue` メソッド追加、`AllElementsIdentical` キーベース対応 |
| `schemas/bdef.schema.json` | `diff_key` フィールド定義追加 |

### 実装中の設計変更

- **EnumLabel 対応**: `ExtractKeyValue` で整数キーの場合、`EnumLabel` があればそれをキー値として使用するようにした。これにより `enum` 付きフィールド（例: チャンクタイプのラベル）でもキーとして機能する。設計メモの疑似コードでは `i.Value.ToString()` のみだったが、`i.EnumLabel ?? i.Value.ToString()` に変更。
- **フォールバック判定の一括化**: 設計メモでは「キーフィールドが見つからない場合」を個別に判定する想定だったが、`CanUseKeyedComparison` メソッドで全要素のキー抽出可能性を事前検証する方式に統一。全要素でキー抽出できない場合はインデックスベースにフォールバック。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DiffEngineTests | KeyedArray_IdenticalElements_NoDifferences | 1, 6 |
| DiffEngineTests | KeyedArray_ElementRemoved_ReportsRemoved | 2, 3 |
| DiffEngineTests | KeyedArray_ElementAdded_ReportsAdded | 2, 3 |
| DiffEngineTests | KeyedArray_ElementChanged_ReportsChangedWithKeyPath | 2, 3 |
| DiffEngineTests | KeyedArray_InsertionAndDeletion_MatchesByKey | 1, 2, 3 |
| DiffEngineTests | KeyedArray_StringKey_MatchesByStringValue | 1, 2, 3 |
| DiffEngineTests | KeyedArray_NoDiffKey_FallsBackToIndexComparison | 6 |
| DiffOutputTests | TreeFormat_KeyedArray_IdenticalElements_ShowsIdentical | 5 |
| DiffOutputTests | TreeFormat_KeyedArray_ChangedElement_ShowsKeyAndArrow | 4, 5 |
| DiffOutputTests | TreeFormat_KeyedArray_AddedAndRemoved_ShowsPlusMinus | 4, 5 |

### 気づき・今後の課題

- **DiffTreeOutputFormatter と DiffEngine の ExtractKeyValue 重複**: 両方に同じロジックの `ExtractKeyValue` が存在する。Core に共通ユーティリティとして切り出すことも可能だが、Output が Engine に依存しない現在のアーキテクチャを維持するため、意図的に重複させている。
- **複合キーの需要**: 単一フィールドのキーでは不十分なケースが将来出てくる可能性がある（例: 2つのフィールドの組み合わせでユニークになる場合）。現時点ではスコープ外としたが、`diff_key: ["field1", "field2"]` のような拡張は設計上可能。
- **キー値の表示順序**: left の出現順 → right のみの要素の順で出力している。要素数が多い場合に「Added がどこに挿入されたか」が直感的にわかりにくい可能性がある。将来的にマージ順序表示を検討してもよい。
