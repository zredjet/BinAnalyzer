# REQ-200: 半精度の浮動小数点数の型（float16）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

DSL の浮動小数点数の型は `float32` / `float64` だけで、IEEE 754 の binary16（半精度）が無い。REQ-188 の見直しで、CBOR の半精度の浮動小数点数（先頭のバイト 0xF9）を値として表示できず、符号・指数・仮数の bitfield で表示している（formats/cbor.bdef.yaml の「対応していないもの」）。RFC 8949 付録 A の例（`f9 3c 00` = 1.0、`f9 7b ff` = 65504.0、`f9 7c 00` = Infinity など）を値で確かめられない。

半精度は、機械学習のモデルの重み（safetensors・GGUF・ONNX）、glTF の頂点属性、OpenEXR の画素、GPU のテクスチャ（DDS / KTX）などでも使われる。

## 機能要件

### 追加する機能

- [x] **型 `float16`（別名 `f16`）**: 2 バイトの IEEE 754 binary16。エンディアンは他の数値型と同じく struct / フィールド / フォーマットの指定に従う。デコード結果は `DecodedFloat`（値は double。無限大・NaN・非正規化数を含む）
- [x] 式・出力（ツリー・JSON・CSV）・GUI / TUI の詳細で、`float32` / `float64` と同じく扱う
- [x] 型名の一覧（`FieldTypeNames`）・JSON Schema・検証・DSL リファレンスの型の表に足す

### 変更する既存機能

- [x] formats/cbor.bdef.yaml の半精度の浮動小数点数を `float16` で読み、値を表示する（符号・指数・仮数の bitfield は外した）

### 変更しないもの（スコープ外）

- bfloat16（上位 16 ビットの float32）などの別の 16 ビットの形式。別の要望とする
- ビットストリームモードの float（ビット単位の浮動小数点数）

## 受入条件

1. [x] `float16` が 1.0（0x3C00）・-2.0（0xC000）・65504.0（0x7BFF）・最小の非正規化数（0x0001 = 5.96e-8）・Infinity（0x7C00）・-Infinity・NaN（0x7E00）を読めること（ビッグエンディアン・リトルエンディアン）
2. [x] ツリー・JSON の出力、GUI / TUI の詳細で型が float16 と表示されること
3. [x] CBOR の RFC 8949 付録 A の半精度の例がすべて値で表示されること
4. [x] 値の編集（`FieldEncoder`）が float16 を扱うか、扱わないならその旨が表示されること
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldType.Float16`、`FieldTypeNames`、`DecodedFloat.Precision`（`IsSinglePrecision` の bool を置き換え）、`FieldEditRules` |
| BinAnalyzer.Dsl | 型名の変換 |
| BinAnalyzer.Engine | 読み取り（`System.Half`）、編集（`FieldEncoder`）、差分（`DiffEngine` の浮動小数点数の比較） |
| BinAnalyzer.Output / Presentation / Tui | 型名の表示 |
| schemas/bdef.schema.json | 型名 |
| formats/cbor.bdef.yaml | 半精度の値 |
| tests/BinAnalyzer.Engine / Integration / Presentation / Tui / Fuzz.Tests | 値・出力・表示・編集のテスト、CBOR の半精度の例の Generator |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — フィールド型の表
- [x] docs/parser-design.md・docs/architecture.md・docs/cli-usage.md（値の編集）・README.md

---

## 設計メモ

### 設計方針

- `DecodedFloat.IsSinglePrecision`（bool）は float16 を表せないので、バイト数（2 / 4 / 8）か `DslType` から型名を出す形にする。`IsSinglePrecision` を使っている表示・編集の箇所（Presentation・Tui・FieldEncoder・EditKind）を洗い出す
- .NET の `System.Half` で読み、double に広げる

### 懸念事項

- `DecodedFloat` の公開のプロパティを変えると、GUI の編集（Patching）に影響する

---

## 実装メモ

### 実装中の設計変更

- **`DecodedFloat.IsSinglePrecision` を `Precision`（`FloatPrecision.Half` / `Single` / `Double`。値はバイト数）に置き換えた**。型名は `FloatTypeName`（`float16` / `float32` / `float64`）で、JSON・CSV の型、Presentation / TUI の詳細、`NodeDisplayText.TypeLabel` は `DslType` が無いときこれを使う。編集の可否（`FieldEditRules`）はサイズを `(int)Precision` と比べる（以前の `IsSinglePrecision ? 4 : 8` と同じ検査）
- `Size` と `DslType` だけから精度を決める案（`IsSinglePrecision` を残したまま）は、`DslType` の無いノード（テストや差分で作るもの）で float16 を区別できないので採らなかった。GUI の編集（Patching）は `FieldEditRules` と `FieldEncoder` を通すので、公開のプロパティの変更は Core・Engine・Output・Presentation・Tui とテストの中で閉じた
- **編集**: `FieldEncoder` は float16 を `(Half)value` で丸めて書き戻す。有限の値が float16 の範囲（絶対値 65504）を超えて無限大になるときは「float16 の範囲外です」のエラー。`Infinity` / `-Infinity` / `NaN` の入力はそのまま書ける
- **式**: float16 の値は float32 / float64 と同じく double の変数になる（そのまま参照できる。浮動小数点数の比較・算術は元から対応していない）
- **CBOR**: 符号・指数・仮数の bitfield（`half_float`）を外し、単精度・倍精度と並ぶ `float16_value` にした。値だけで RFC 8949 付録 A の例が確かめられ、ビットは値から分かるので残さなかった
- **差分の NaN の扱いを直した**: ファズの決定性のテスト（同じ入力を 2 回デコードして `DiffEngine` で比べる）が、CBOR の NaN の見本で「違う」と判定した。`DiffEngine.CompareFloat` が `==` で比べていたため、同じ NaN 同士が違い、0.0 と -0.0 が同じになっていた（float32 / float64 でも起きる元からの誤り）。double のビットの並びで比べるようにした
- ゴールデン（test.cbor）は半精度の値を含まないので変わらない

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Float16DecoderTests | Decode_BigAndLittleEndian（1.0・-2.0・65504・最小の非正規化数・最小の正規化数・無限大・NaN など）・Decode_NegativeZero_KeepsTheSign | 1 |
| Float16DecoderTests | Decode_ProducesAHalfPrecisionNodeOf2Bytes（別名 `f16`）・Decode_ValueIsBoundAsAVariable・Decode_TooShort_IsAnError | 1・2 |
| Float16DecoderTests | Edit_IsAFloatEdit・Encode_BigAndLittle_AndRejectsOutOfRange | 4 |
| FloatDecoderTests | Diff_ComparesTheBitPattern | 実装中の設計変更（差分の NaN） |
| CborParsingTests | CborFormat_Rfc8949HalfFloatExamples_AreDecodedAsValues・CborFormat_Rfc8949Examples_MatchDiagnosticNotation（半精度の 2 つを値に） | 3 |
| CborParsingTests | CborFormat_HalfFloat_IsShownAsFloat16InTheOutputs（JSON・ツリー・CSV） | 2 |
| NodeDetailFormatterTests（Presentation） | Format_Float16_ReturnsFloat16Type | 2（GUI の詳細） |
| NodeDisplayTextTests | TypeLabel_Float_String_Flags_Array_UseDslType（`f16`・`float16` を追加） | 2 |
| NodeDetailFormatterTests（Tui） | Float_ShowsThePrecisionAsTheType | 2（TUI の詳細） |
| FormatCatalog（ファズ） | CBOR の見本に `CreateCborHalfFloatExamples` | 5 |
