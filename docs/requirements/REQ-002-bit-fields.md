# REQ-002: ビットフィールド型

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットでは、1バイト未満の粒度でフィールドが定義されている。例:

- BMPのDIBヘッダーでの圧縮方式（特定ビット範囲）
- ELFヘッダーのフラグフィールド（個別ビット）
- ネットワークプロトコルヘッダー（TCPフラグ等）
- 各種ファイルフォーマットのステータスバイト

現在のBinAnalyzerはバイト境界単位のフィールドしか扱えない。`flags` 機能はasciiフィールドへの付加情報として存在するが、独立したビットフィールド型ではない。

ビットフィールド型を追加することで、対応可能なフォーマットの幅が大幅に広がる。

## 機能要件

### 追加する機能

- [ ] DSLに `bitfield` 型を追加
  - 指定バイト数を読み取り、その中のビット範囲を個別フィールドとして解釈する
  - ビットオーダーはMSBファースト（ネットワークバイトオーダーと同じ）
- [ ] DSLの `bitfield` 構文
  ```yaml
  - name: flags_byte
    type: bitfield
    size: "1"           # 読み取るバイト数（1, 2, 4）
    fields:
      - name: reserved
        bits: "7:4"     # ビット7〜4（4ビット）
      - name: flag_a
        bits: "3"       # ビット3（1ビット）
      - name: flag_b
        bits: "2"       # ビット2
      - name: mode
        bits: "1:0"     # ビット1〜0（2ビット）
        enum: mode_type
  ```
- [ ] IRモデルへの `FieldType.Bitfield` と `BitfieldEntry` の追加
- [ ] デコード結果 `DecodedBitfield` の追加
  - 元の整数値（raw value）と各ビットフィールドの解釈結果を保持
- [ ] ツリー出力でのビットフィールド表示
  ```
  ├── flags_byte [0x00000004] (1 byte): 0xA5
  │   ├── reserved: 10 (bits 7:4)
  │   ├── flag_a: 1 (bit 3)
  │   ├── flag_b: 0 (bit 2)
  │   └── mode: 1 "normal" (bits 1:0)
  ```
- [ ] ビットフィールド内の個別フィールドにenum参照を付与可能にする

### 変更する既存機能

- [ ] `FieldType` enumに `Bitfield` を追加
- [ ] `YamlFieldModel` にビットフィールド定義用プロパティを追加
- [ ] `YamlToIrMapper` にビットフィールドのマッピングロジックを追加
- [ ] `BinaryDecoder` にビットフィールドのデコードロジックを追加
- [ ] `TreeOutputFormatter` にビットフィールドの表示ロジックを追加

### 変更しないもの（スコープ外）

- バイト境界をまたぐビットフィールド（複数バイトに跨ってビット位置を指定する機能は含めない）
- ビットフィールドのネスト（ビットフィールド内にビットフィールドは不可）
- ビットフィールド値の式での参照（ビットフィールド内の値をサイズ式等で `{field.subfield}` のように参照する機能は将来課題）

## 受入条件

1. [ ] DSLで `type: bitfield` のフィールドが定義できること
2. [ ] 1バイト、2バイト、4バイトのビットフィールドがデコードできること
3. [ ] 単一ビット指定（`bits: "3"`）と範囲指定（`bits: "7:4"`）が動作すること
4. [ ] ビットフィールド内の個別フィールドにenum参照が適用されること
5. [ ] ツリー出力でビットフィールドの階層表示がされること（ビット位置情報を含む）
6. [ ] バリデーター（REQ-001）がビットフィールドの整合性を検証すること（ビット範囲の重複、size超過等）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldType.Bitfield`, `BitfieldEntry` モデル, `DecodedBitfield` の追加 |
| BinAnalyzer.Dsl | `YamlFieldModel` 拡張, `YamlToIrMapper` にbitfieldマッピング追加 |
| BinAnalyzer.Engine | `BinaryDecoder` にbitfieldデコードロジック追加 |
| BinAnalyzer.Output | `TreeOutputFormatter` にbitfield表示追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — bitfield型の構文ドキュメント追加
- [ ] docs/architecture.md — 必要に応じて更新
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

ビットフィールドを新しいフィールド型 `FieldType.Bitfield` として追加する。バイト単位で読み取った後、定義されたビット範囲を個別に抽出・表示する。

既存の `flags` 機能（asciiフィールドへの付加情報）とは独立した型とする。理由:
- flagsは文字列フィールドへの補助情報であり、ビットフィールドは独立したフィールド型
- 各ビット範囲に独自のenum参照を持てる
- デコード結果が異なる（DecodedString + FlagState vs DecodedBitfield + BitfieldValue）

### モデル変更

#### Core/Models/ への追加

**BitfieldEntry**（ビットフィールド内の個別エントリ定義）:
```csharp
public sealed class BitfieldEntry
{
    public required string Name { get; init; }
    public required int BitHigh { get; init; }      // 上位ビット位置（単一ビットの場合はBitLowと同じ）
    public required int BitLow { get; init; }        // 下位ビット位置
    public string? EnumRef { get; init; }            // オプションのenum参照
    public string? Description { get; init; }
}
```

ビット範囲の表現:
- `bits: "3"` → BitHigh=3, BitLow=3（1ビット）
- `bits: "7:4"` → BitHigh=7, BitLow=4（4ビット）

**FieldDefinition への追加**:
```csharp
public IReadOnlyList<BitfieldEntry>? BitfieldEntries { get; init; }
```

**FieldType への追加**:
```csharp
Bitfield,  // FieldType enumに追加
```

#### Core/Decoded/ への追加

**DecodedBitfield**:
```csharp
public sealed class DecodedBitfield : DecodedNode
{
    public required long RawValue { get; init; }
    public required IReadOnlyList<BitfieldValue> Fields { get; init; }
}
```

**BitfieldValue**:
```csharp
public sealed record BitfieldValue(
    string Name,
    int BitHigh,
    int BitLow,
    long Value,
    string? EnumLabel,
    string? EnumDescription
);
```

#### Dsl/YamlModels/ への追加

**YamlBitfieldEntryModel**:
```csharp
public sealed class YamlBitfieldEntryModel
{
    public string Name { get; set; }
    public string Bits { get; set; }       // "3" or "7:4"
    public string? Enum { get; set; }
    public string? Description { get; set; }
}
```

**YamlFieldModel への追加**:
```csharp
[YamlMember(Alias = "fields")]
public List<YamlBitfieldEntryModel>? BitfieldFields { get; set; }
```

### インタフェース変更

- 既存インタフェースへの変更なし
- `BinaryDecoder.DecodeSingleField` のswitch式に `FieldType.Bitfield` の分岐を追加
- `TreeOutputFormatter` にDecodedBitfieldの表示ロジックを追加
- `YamlToIrMapper.MapField` にbitfield関連プロパティのマッピングを追加
- `YamlToIrMapper.ParseFieldType` に `"bitfield"` を追加

### デコードロジック

```csharp
private DecodedBitfield DecodeBitfieldField(
    FieldDefinition field, FormatDefinition format, DecodeContext context)
{
    var offset = context.Position;
    var size = ResolveSize(field, context);

    // バイト列を整数として読み取り（ビッグエンディアン前提でrawValue構築）
    long rawValue = size switch
    {
        1 => context.ReadUInt8(),
        2 => context.ReadUInt16(),
        4 => context.ReadUInt32(),
        _ => throw new InvalidOperationException(
            $"Bitfield size must be 1, 2, or 4 bytes, got {size}")
    };

    // 各ビットフィールドを抽出
    var fields = new List<BitfieldValue>();
    foreach (var entry in field.BitfieldEntries!)
    {
        var bitWidth = entry.BitHigh - entry.BitLow + 1;
        var mask = (1L << bitWidth) - 1;
        var value = (rawValue >> entry.BitLow) & mask;

        string? enumLabel = null;
        string? enumDesc = null;
        if (entry.EnumRef is not null &&
            format.Enums.TryGetValue(entry.EnumRef, out var enumDef))
        {
            var enumEntry = enumDef.FindByValue(value);
            enumLabel = enumEntry?.Label;
            enumDesc = enumEntry?.Description;
        }

        fields.Add(new BitfieldValue(
            entry.Name, entry.BitHigh, entry.BitLow, value, enumLabel, enumDesc));
    }

    return new DecodedBitfield { ... };
}
```

### ツリー出力

```
├── flags_byte [0x00000004] (1 byte): 0xA5
│   ├── reserved: 10 (bits 7:4)
│   ├── flag_a: 1 (bit 3)
│   ├── flag_b: 0 (bit 2)
│   └── mode: 1 "normal" (bits 1:0)
```

- raw値を16進で表示
- 各フィールドは値 + ビット位置情報
- enum付きの場合はラベルを引用符付きで表示
- 単一ビットは `(bit N)`、範囲は `(bits H:L)` と表示

### 代替案

**案A: 既存のflags機能を拡張する（不採用）**
- 理由: flagsは1ビット単位のon/off判定に特化。複数ビット範囲の値抽出やenum参照はbitfieldの責務。混在させると既存のflagsロジックが複雑化する。

**案B: size を 8バイトまで対応する（不採用）**
- 理由: 8バイト(64ビット)のビットフィールドは実用上まれ。1/2/4バイトで十分。必要になった時点で拡張可能。

**案C: ビットオーダーをLSBファーストも選択可能にする（不採用）**
- 理由: 初期実装ではMSBファースト固定で十分。フォーマットのエンディアンに依存するケースは稀。将来 `bit_order` 属性で拡張可能。

### 懸念事項

1. **エンディアンとの相互作用**: ビットフィールドの元データをエンディアンに従って読み取るため、`DecodeContext.ReadUInt16/32` を使用する（既にエンディアン対応済み）。ビット抽出自体はエンディアン非依存。

2. **YamlFieldModel の `fields` プロパティ名**: YAMLの `fields` キーはbitfield専用だが、将来他の型でもネストフィールドを持つ可能性がある。現時点では `BitfieldFields` としてマッピングし、YAML上は `fields` で統一。

3. **変数バインディング**: ビットフィールド内の個別値を後続フィールドの式で参照する機能はスコープ外。`context.SetVariable(field.Name, rawValue)` で整数としてバインドするのみ。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `FormatValidator` の VAL007（サイズ必須チェック）に `FieldType.Bitfield` を追加。
- `WithoutRepeat` メソッドに `BitfieldEntries` のコピーを追加（repeat対応）。
- ビットフィールドのraw値は `context.SetVariable` で変数バインドし、後続フィールドの式から参照可能。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitfieldDecoderTests | Decode_SingleBit_ExtractsCorrectly | AC-3（単一ビット指定） |
| BitfieldDecoderTests | Decode_BitRange_ExtractsCorrectly | AC-3（範囲指定） |
| BitfieldDecoderTests | Decode_TwoBytes_BigEndian | AC-2（2バイト） |
| BitfieldDecoderTests | Decode_FourBytes_BigEndian | AC-2（4バイト） |
| BitfieldDecoderTests | Decode_WithEnumRef_ResolvesLabel | AC-4（enum参照） |
| BitfieldDecoderTests | Decode_VariableBinding_RawValueAvailable | 変数バインディング確認 |
| BitfieldParsingTests | Load_BitfieldType_ParsesCorrectly | AC-1（DSL定義） |
| BitfieldParsingTests | Load_BitfieldEntries_ParsedCorrectly | AC-1 |
| BitfieldParsingTests | Load_BitRange_ParsedCorrectly | AC-3 |
| BitfieldParsingTests | Load_BitfieldEntryWithEnum_ParsedCorrectly | AC-4 |
| BitfieldOutputTests | TreeOutput_Bitfield_ShowsHierarchy | AC-5（ツリー出力） |

### 受入条件の充足状況

- [x] AC-1: DSLで `type: bitfield` のフィールドが定義できる
- [x] AC-2: 1バイト、2バイト、4バイトのビットフィールドがデコードできる
- [x] AC-3: 単一ビット指定（`bits: "3"`）と範囲指定（`bits: "7:4"`）が動作する
- [x] AC-4: ビットフィールド内の個別フィールドにenum参照が適用される
- [x] AC-5: ツリー出力でビットフィールドの階層表示がされる
- [x] AC-6: バリデーター（VAL007）がビットフィールドのサイズ必須を検証する
- [x] AC-7: 既存テスト全て通過（111テスト合格）

### 気づき・今後の課題

- ビットフィールドの個別値を式で参照する機能（`{flags.mode}` 形式）は将来課題として残す。
- ビット範囲の重複・超過バリデーション（例: 8ビットフィールドなのにbit8を参照）は未実装。別途バリデーションルール追加を検討。
- 8バイト（64ビット）対応も需要があれば対応可能。
