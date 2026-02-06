# REQ-018: float32 / float64 型の追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

多くのバイナリフォーマットにはIEEE 754浮動小数点数が含まれる。例:

- WAV（IEEE Float音声データ）
- 3Dモデルフォーマット（頂点座標、法線ベクトル）
- 科学データフォーマット（HDF5, FITS）
- ゲームのセーブデータ
- GPSデータ（経度・緯度）

現在のDSLには整数型のみが定義されており、浮動小数点フィールドはバイト列として読み取るしかない。基本的なデータ型として対応すべきである。

## 機能要件

### 追加する機能

- [ ] `FieldType.Float32` 型の追加（IEEE 754 単精度浮動小数点数、4バイト）
- [ ] `FieldType.Float64` 型の追加（IEEE 754 倍精度浮動小数点数、8バイト）
- [ ] デコード結果モデル `DecodedFloat` の追加
  - `double Value` — デコードされた浮動小数点値
  - `bool IsSinglePrecision` — float32の場合true
- [ ] エンディアン対応（ビッグ/リトルエンディアン両対応）
- [ ] DSLでの使用
  ```yaml
  - name: x_coordinate
    type: float32
  - name: precise_value
    type: float64
  ```

### 変更する既存機能

- [ ] FieldType enum: Float32, Float64 の追加
- [ ] DecodeContext: ReadFloat32(), ReadFloat64() メソッドの追加
- [ ] BinaryDecoder: 浮動小数点フィールドのデコード処理追加
- [ ] YamlToIrMapper: `float32` / `f32` / `float64` / `f64` 型名のマッピング追加
- [ ] TreeOutputFormatter: 浮動小数点値の表示（小数点以下の適切な桁数）
- [ ] JsonOutputFormatter: 浮動小数点値のJSON出力

### 変更しないもの（スコープ外）

- half精度（float16） — 将来課題
- 固定小数点型 — 将来課題
- 浮動小数点値の変数バインディング（式内での浮動小数点演算） — 将来課題
- NaN/Infinity の特別な表示 — 標準的な文字列表現で十分

## 受入条件

1. [ ] `float32` 型フィールドが4バイトからIEEE 754単精度としてデコードされること
2. [ ] `float64` 型フィールドが8バイトからIEEE 754倍精度としてデコードされること
3. [ ] ビッグエンディアン/リトルエンディアン両方で正しくデコードされること
4. [ ] ツリー出力で浮動小数点値が適切に表示されること
5. [ ] JSON出力で浮動小数点値が数値として出力されること
6. [ ] 特殊値（NaN, +Infinity, -Infinity, -0.0）が正しく処理されること
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldType enum, DecodedFloat モデル追加 |
| BinAnalyzer.Dsl | YamlToIrMapper に型マッピング追加 |
| BinAnalyzer.Engine | DecodeContext, BinaryDecoder にデコード処理追加 |
| BinAnalyzer.Output | TreeOutputFormatter, JsonOutputFormatter に表示処理追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型一覧に float32, float64 追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要
- [ ] docs/parser-design.md — フィールド型マッピング表の更新

---

## 設計メモ

### 設計方針

既存の整数デコードパターン（`DecodeIntegerField`）に倣い、`DecodedFloat` モデルを新設する。`BinaryPrimitives` の `ReadSingleBigEndian` / `ReadDoubleBigEndian` 等を使用。変数バインディングは `double` 型で行う（式評価での浮動小数点演算はスコープ外だが、変数としては保持）。

### モデル変更

**FieldType enum に追加:**
```csharp
Float32,
Float64,
```

**DecodedFloat（新規）:**
```csharp
// src/BinAnalyzer.Core/Decoded/DecodedFloat.cs
public sealed class DecodedFloat : DecodedNode
{
    public required double Value { get; init; }
    public required bool IsSinglePrecision { get; init; }
}
```

### DecodeContext への追加

```csharp
public float ReadFloat32()
{
    EnsureAvailable(4);
    var span = _data.Span.Slice(_position, 4);
    var value = Endianness == Endianness.Big
        ? BinaryPrimitives.ReadSingleBigEndian(span)
        : BinaryPrimitives.ReadSingleLittleEndian(span);
    _position += 4;
    return value;
}

public double ReadFloat64()
{
    EnsureAvailable(8);
    var span = _data.Span.Slice(_position, 8);
    var value = Endianness == Endianness.Big
        ? BinaryPrimitives.ReadDoubleBigEndian(span)
        : BinaryPrimitives.ReadDoubleLittleEndian(span);
    _position += 8;
    return value;
}
```

### BinaryDecoder の変更

`DecodeSingleField` の switch に追加:
```csharp
FieldType.Float32 or FieldType.Float64 => DecodeFloatField(field, context),
```

新規メソッド:
```csharp
private DecodedFloat DecodeFloatField(FieldDefinition field, DecodeContext context)
{
    var offset = context.Position;
    var isSingle = field.Type == FieldType.Float32;
    double value = isSingle ? context.ReadFloat32() : context.ReadFloat64();
    var size = isSingle ? 4 : 8;
    context.SetVariable(field.Name, value);
    return new DecodedFloat { Name = field.Name, Offset = offset, Size = size,
        Value = value, IsSinglePrecision = isSingle, Description = field.Description };
}
```

### 出力フォーマッターの変更

**TreeOutputFormatter:**
```csharp
case DecodedFloat floatNode:
    FormatFloat(sb, floatNode, prefix);
    break;
```
表示: `field_name: 3.14159` （`G` フォーマット、有効桁数自動）

**JsonOutputFormatter:**
```csharp
writer.WriteNumber("value", node.Value);
```
NaN/Infinity は JSON仕様上 `null` にフォールバック、または文字列として出力。

### YamlToIrMapper の変更

```
"float32" | "f32" → FieldType.Float32
"float64" | "f64" → FieldType.Float64
```

### DiffEngine の変更

`CompareNodes` に `DecodedFloat` 同士の比較を追加。浮動小数点の等値比較は `==` で行う（ビット完全一致）。

### インタフェース変更

なし。`IOutputFormatter.Format(DecodedStruct)` は変更不要。

### 代替案

**案A: DecodedInteger に統合する（不採用）**
- `DecodedInteger.Value` は `long` 型であり、浮動小数点値の精度を保てない。専用モデルが必要。

### 懸念事項

1. **NaN/Infinity のJSON出力**: `System.Text.Json` は `NaN` / `Infinity` をデフォルトで拒否する。`JsonNumberHandling.AllowNamedFloatingPointLiterals` を設定するか、文字列としてフォールバックする。

2. **変数バインディングの型**: `double` を `SetVariable` に格納するが、`ConvertToLong` は `double` を扱えない。float変数を整数式で使用するとエラーになる。これは意図的な制約とし、ドキュメントに明記する。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
