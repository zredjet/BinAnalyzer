# REQ-027: アライメント・パディングサポート

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットはフィールドのアライメント（境界揃え）を要求する。例えば:

- **PE/COFF（Windows実行ファイル）**: セクションは512バイト境界にアラインされる
- **ELF**: プログラムヘッダーのアライメント指定
- **BMP**: 各行が4バイト境界にパディングされる
- **WAV**: dataチャンクが偶数バイト境界に揃えられる場合がある
- **ZIP**: ローカルファイルヘッダー間のオプションパディング

現在のDSLではアライメントを表現できないため、手動で `size` 式を計算してパディングバイトを読み飛ばす必要があり、記述が煩雑でエラーが起きやすい。

## 機能要件

### 追加する機能

- [ ] `align` 属性（フィールドレベル）
  ```yaml
  - name: section_data
    type: bytes
    size: "{section_size}"
    align: 512   # 次のフィールドが512バイト境界から始まるようパディング
  ```
  - フィールドのデコード後、次のフィールドの読み取り位置を指定バイト境界に繰り上げ
  - パディングバイトは読み飛ばされる（デコード結果に含まない）
- [ ] `align` 属性（構造体レベル）
  ```yaml
  structs:
    section:
      align: 16  # この構造体の各インスタンスが16バイト境界から始まる
      fields:
        - name: data
          type: bytes
          size: "{size}"
  ```
  - 繰り返し（repeat/repeat_count）時に各要素の開始位置を揃える
- [ ] `padding` 属性（明示的パディング）
  ```yaml
  - name: _pad
    type: bytes
    padding: true
    size: "{4 - (offset % 4)}"
  ```
  - `padding: true` のフィールドは出力時にデフォルトで非表示
  - ツリー出力に `--show-padding` オプションで表示可能（将来課題）

### 変更する既存機能

- [ ] FieldDefinition: `Align` プロパティ追加
- [ ] StructDefinition: `Align` プロパティ追加
- [ ] BinaryDecoder: フィールド/構造体デコード後のアライメント処理
- [ ] YamlModels/YamlToIrMapper: `align`, `padding` のパース・マッピング
- [ ] FormatValidator: align値の妥当性チェック（正の整数、2の累乗推奨だが強制しない）

### 変更しないもの（スコープ外）

- パディングバイトの値検証（ゼロ埋めかどうかのチェック） — 将来課題
- `--show-padding` オプション — 将来課題
- ビットレベルのアライメント（バイト単位のみ） — 将来課題

## 受入条件

1. [ ] フィールドレベル `align` で次フィールドの開始位置がアラインされること
2. [ ] 構造体レベル `align` で各インスタンスの開始位置がアラインされること
3. [ ] すでにアライメント境界上にある場合、追加パディングが入らないこと
4. [ ] `padding: true` フィールドがツリー出力でデフォルト非表示になること
5. [ ] JSON出力にはパディングフィールドの情報が含まれること（完全性のため）
6. [ ] アライメント値が0以下の場合にバリデーションエラーになること
7. [ ] `size: remaining` とアライメントの組み合わせが正しく動作すること
8. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldDefinition/StructDefinition に Align 追加、FieldDefinition に Padding 追加 |
| BinAnalyzer.Dsl | YamlModels/YamlToIrMapper に align, padding のパース追加 |
| BinAnalyzer.Engine | BinaryDecoder にアライメントスキップ処理追加 |
| BinAnalyzer.Output | TreeOutputFormatter でパディングフィールドの非表示処理 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — align, padding 属性の説明追加
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

アライメントを **フィールドの属性** として実装する。フィールドデコード後に、DecodeContext の位置を次のアライメント境界まで進める。構造体レベルのアライメントは StructDefinition に属性を追加し、繰り返しデコード時に各要素の開始前にアライメントを適用する。

アライメント計算式:

```
padding = (align - (position % align)) % align
new_position = position + padding
```

`(position % align) == 0` の場合、padding = 0 で位置は変わらない。

### 構成

```
src/BinAnalyzer.Core/
├── Models/
│   ├── FieldDefinition.cs       # Align, IsPadding 追加
│   └── StructDefinition.cs      # Align 追加
src/BinAnalyzer.Dsl/
├── YamlModels/
│   └── YamlFormatModel.cs       # YamlFieldModel に align, padding 追加
│                                  YamlStructModel 導入（align対応）
└── YamlToIrMapper.cs            # align, padding のマッピング追加
src/BinAnalyzer.Engine/
├── DecodeContext.cs              # AlignTo メソッド追加
└── BinaryDecoder.cs             # フィールド後・構造体前のアライメント処理
src/BinAnalyzer.Output/
├── TreeOutputFormatter.cs       # IsPadding ノードのスキップ処理
├── JsonOutputFormatter.cs       # padding フラグを出力に含める
└── （その他フォーマッター）       # IsPadding の扱いは各フォーマッターで判断
```

### モデル変更

**FieldDefinition に追加:**

```csharp
public sealed class FieldDefinition
{
    // 既存プロパティ...

    /// <summary>
    /// このフィールドのデコード後、次フィールドの開始位置を
    /// 指定バイト境界にアラインする。null = アライメントなし。
    /// </summary>
    public int? Align { get; init; }

    /// <summary>
    /// true の場合、このフィールドはパディングとして扱い、
    /// 出力フォーマッターでデフォルト非表示とする。
    /// </summary>
    public bool IsPadding { get; init; }
}
```

**StructDefinition に追加:**

```csharp
public sealed class StructDefinition
{
    public required string Name { get; init; }
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }

    /// <summary>
    /// この構造体のインスタンス開始位置を指定バイト境界にアラインする。
    /// 繰り返し（repeat/repeat_count）時に各要素の開始前に適用。
    /// null = アライメントなし。
    /// </summary>
    public int? Align { get; init; }
}
```

### YAMLモデル変更

**YamlFieldModel に追加:**

```csharp
public sealed class YamlFieldModel
{
    // 既存プロパティ...
    public int? Align { get; set; }
    public bool? Padding { get; set; }
}
```

**構造体のアライメント対応:**

現在の `Structs` は `Dictionary<string, List<YamlFieldModel>>` だが、構造体レベルのメタデータ（align）を持つため、ラッパーモデルが必要。

ただし、YAML互換性を維持するため、2つの記法をサポートする:

```yaml
# 記法1: 従来通り（フィールドリスト直接）
structs:
  simple:
    - name: x
      type: uint32

# 記法2: align 付き（オブジェクト形式）
structs:
  aligned_section:
    align: 512
    fields:
      - name: data
        type: bytes
        size: "{size}"
```

これにはYamlDotNetのカスタムデシリアライザが必要。YamlFormatModel の `Structs` の型を変更する:

```csharp
// 新規
public sealed class YamlStructModel
{
    public int? Align { get; set; }
    public List<YamlFieldModel> Fields { get; set; } = new();
}
```

`Structs` の型を `Dictionary<string, List<YamlFieldModel>>` から `Dictionary<string, YamlStructModel>` に変更し、カスタムデシリアライザで従来のリスト記法もパースできるようにする。

```csharp
// カスタムTypeConverter
public sealed class YamlStructModelConverter : IYamlTypeConverter
{
    // シーケンス（リスト）の場合: Fields にそのまま格納、Align = null
    // マッピング（オブジェクト）の場合: align と fields を読み取り
}
```

### YamlToIrMapper の変更

```csharp
private static StructDefinition MapStruct(string name, YamlStructModel yaml)
{
    return new StructDefinition
    {
        Name = name,
        Fields = yaml.Fields.Select(MapField).ToList(),
        Align = yaml.Align,  // 追加
    };
}

private static FieldDefinition MapField(YamlFieldModel yaml)
{
    // 既存マッピング...
    return new FieldDefinition
    {
        // 既存プロパティ...
        Align = yaml.Align,         // 追加
        IsPadding = yaml.Padding ?? false,  // 追加
    };
}
```

### DecodeContext の変更

```csharp
public sealed class DecodeContext
{
    // 既存メソッド...

    /// <summary>
    /// 現在の位置を指定バイト境界にアラインする。
    /// すでに境界上にある場合は何もしない。
    /// </summary>
    public int AlignTo(int alignment)
    {
        if (alignment <= 0)
            throw new ArgumentOutOfRangeException(nameof(alignment));
        var padding = (alignment - (_position % alignment)) % alignment;
        if (padding > 0)
        {
            if (_position + padding > CurrentScope.End)
                throw new InvalidOperationException(
                    $"Alignment to {alignment} bytes requires {padding} padding bytes, " +
                    $"but only {Remaining} bytes remaining in scope");
            _position += padding;
        }
        return padding;  // スキップしたバイト数を返す
    }
}
```

### BinaryDecoder の変更

**フィールドレベルアライメント — DecodeField 内:**

```csharp
private DecodedNode? DecodeField(
    FieldDefinition field, FormatDefinition format,
    DecodeContext context, IReadOnlyList<DecodedNode>? siblings = null)
{
    // 既存の条件・繰り返し処理...

    var node = DecodeSingleField(field, format, context, siblings);

    // フィールドデコード後のアライメント（追加）
    if (field.Align is { } align)
        context.AlignTo(align);

    return node;
}
```

**構造体レベルアライメント — DecodeRepeatedField 内:**

```csharp
private DecodedArray DecodeRepeatedField(...)
{
    // ...
    switch (field.Repeat)
    {
        case RepeatMode.Count countMode:
        {
            var count = ExpressionEvaluator.EvaluateAsLong(...);
            for (var i = 0; i < count; i++)
            {
                // 構造体アライメント（追加）
                if (structDef?.Align is { } align && i > 0)
                    context.AlignTo(align);

                var element = DecodeSingleField(singleField, format, context);
                elements.Add(element);
            }
            break;
        }
        // UntilEof, UntilValue も同様...
    }
}
```

構造体アライメントの適用タイミング: 最初の要素（i=0）ではスキップし、2番目以降の要素で前の要素との間のパディングをスキップする。これはPE/ELFのセクションアライメントの一般的なセマンティクスに合致する。

### 出力フォーマッターの変更

**TreeOutputFormatter:**

```csharp
// DecodedNodeのフィルタ
private bool ShouldShowNode(DecodedNode node)
{
    // IsPadding は DecodedNode レベルでは持てないため、
    // フィールド名の先頭が "_" かつ padding マーカーで判断する
    // → 代替案: DecodedNode に IsPadding を持たせる
}
```

ここで設計判断が必要: パディングの非表示情報をどう伝搬するか。

**採用案: DecodedNode に IsPadding を追加**

```csharp
public abstract class DecodedNode
{
    // 既存プロパティ...
    public bool IsPadding { get; init; }  // 追加
}
```

BinaryDecoder でフィールドデコード時に `field.IsPadding` を `DecodedNode.IsPadding` にコピー。TreeOutputFormatter で `IsPadding == true` のノードをスキップ。JsonOutputFormatter では `"padding": true` として出力（完全性のため）。

### FormatValidator の変更

```csharp
// 新規バリデーション（VAL008）
// align 値が正の整数であることを検証
if (field.Align is { } align && align <= 0)
    errors.Add(new ValidationError("VAL008",
        $"Field '{field.Name}' in struct '{structName}': " +
        $"align must be a positive integer, got {align}"));

// 新規バリデーション（VAL009）
// 構造体 align 値が正の整数であることを検証
if (structDef.Align is { } sAlign && sAlign <= 0)
    errors.Add(new ValidationError("VAL009",
        $"Struct '{structDef.Name}': " +
        $"align must be a positive integer, got {sAlign}"));
```

### インタフェース変更

- `DecodedNode` に `IsPadding` プロパティ追加（全派生クラスで利用可能）
- 破壊的変更: なし（init プロパティのデフォルト値は false）

### 代替案

**案A: align を式（Expression）で指定（不採用）**
- `align: "{block_size}"` のように動的にアライメントを決定する方式。
- 不採用理由: 実用上、アライメントは固定値がほとんど（2, 4, 8, 16, 512 等）。式対応は複雑さに対してユースケースが少ない。将来必要になった場合に拡張可能な構造にしておく（`int?` を `Expression?` に変えるだけ）。

**案B: 暗黙的パディング（align のみ、padding 属性なし）（不採用）**
- `align` だけで対応し、明示的な `padding` フィールドは不要とする方式。
- 不採用理由: `align` は「次フィールドの開始位置を揃える」定型パターンに最適だが、BMP行パディングのような「データに依存する可変パディング」は `size` 式で計算する必要がある。その場合にパディングフィールドを出力から隠す手段が必要。

**案C: struct の YAML 記法を変えず、`_align` 特殊フィールド名で対応（不採用）**
- `- name: _align` `  type: uint8` `  align: 4` のような疑似フィールドで構造体アライメントを表現。
- 不採用理由: セマンティクスが不明瞭。構造体レベルのメタデータは構造体定義に直接持つのが自然。

### 懸念事項

1. **YAMLの後方互換性**: `Structs` の型を `Dictionary<string, List<YamlFieldModel>>` から `Dictionary<string, YamlStructModel>` に変更するため、カスタムデシリアライザで従来のリスト記法（`struct_name: [fields...]`）を引き続きパースできるようにする必要がある。YamlDotNetの `IYamlTypeConverter` で対応可能だが、テストで両方の記法を検証すること。

2. **スコープとアライメントの相互作用**: switch の `PushScope` / `PopScope` でバウンダリスコープが設定されている場合、アライメントによるスキップがスコープ境界を超えないことを保証する必要がある。`AlignTo` メソッドで `Remaining` チェックを行うことで対応。

3. **繰り返しモード `eof` とアライメント**: `repeat: eof` + 構造体 `align` の組み合わせでは、アライメントパディング後に `IsEof` を再チェックする必要がある。パディング後にデータが残っていなければループ終了とする。

4. **`size: remaining` との組み合わせ**: フィールドの `size: remaining` はスコープ内の残りバイト数。直前のフィールドに `align` がある場合、アライメントスキップ後の `Remaining` が使われるため、自然に正しく動作する。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
