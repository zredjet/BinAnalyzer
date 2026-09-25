# REQ-200: 半精度の浮動小数点数の型（float16）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

DSL の浮動小数点数の型は `float32` / `float64` だけで、IEEE 754 の binary16（半精度）が無い。REQ-188 の見直しで、CBOR の半精度の浮動小数点数（先頭のバイト 0xF9）を値として表示できず、符号・指数・仮数の bitfield で表示している（formats/cbor.bdef.yaml の「対応していないもの」）。RFC 8949 付録 A の例（`f9 3c 00` = 1.0、`f9 7b ff` = 65504.0、`f9 7c 00` = Infinity など）を値で確かめられない。

半精度は、機械学習のモデルの重み（safetensors・GGUF・ONNX）、glTF の頂点属性、OpenEXR の画素、GPU のテクスチャ（DDS / KTX）などでも使われる。

## 機能要件

### 追加する機能

- [ ] **型 `float16`（別名 `f16`）**: 2 バイトの IEEE 754 binary16。エンディアンは他の数値型と同じく struct / フィールド / フォーマットの指定に従う。デコード結果は `DecodedFloat`（値は double。無限大・NaN・非正規化数を含む）
- [ ] 式・出力（ツリー・JSON・CSV）・GUI / TUI の詳細で、`float32` / `float64` と同じく扱う
- [ ] 型名の一覧（`FieldTypeNames`）・JSON Schema・検証・DSL リファレンスの型の表に足す

### 変更する既存機能

- [ ] formats/cbor.bdef.yaml の半精度の浮動小数点数を `float16` で読み、値を表示する（符号・指数・仮数の bitfield は外すか残すかを設計で決める）

### 変更しないもの（スコープ外）

- bfloat16（上位 16 ビットの float32）などの別の 16 ビットの形式。別の要望とする
- ビットストリームモードの float（ビット単位の浮動小数点数）

## 受入条件

1. [ ] `float16` が 1.0（0x3C00）・-2.0（0xC000）・65504.0（0x7BFF）・最小の非正規化数（0x0001 = 5.96e-8）・Infinity（0x7C00）・-Infinity・NaN（0x7E00）を読めること（ビッグエンディアン・リトルエンディアン）
2. [ ] ツリー・JSON の出力、GUI / TUI の詳細で型が float16 と表示されること
3. [ ] CBOR の RFC 8949 付録 A の半精度の例がすべて値で表示されること
4. [ ] 値の編集（`FieldEncoder`）が float16 を扱うか、扱わないならその旨が表示されること
5. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldType.Float16`、`FieldTypeNames`、`DecodedFloat` の精度（今は `IsSinglePrecision` の bool） |
| BinAnalyzer.Dsl | 型名の変換 |
| BinAnalyzer.Engine | 読み取り（`System.Half`）、編集（`FieldEncoder`） |
| BinAnalyzer.Output / Presentation / Tui | 型名の表示 |
| schemas/bdef.schema.json | 型名 |
| formats/cbor.bdef.yaml | 半精度の値 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型の表

---

## 設計メモ

### 設計方針（案）

- `DecodedFloat.IsSinglePrecision`（bool）は float16 を表せないので、バイト数（2 / 4 / 8）か `DslType` から型名を出す形にする。`IsSinglePrecision` を使っている表示・編集の箇所（Presentation・Tui・FieldEncoder・EditKind）を洗い出す
- .NET の `System.Half` で読み、double に広げる

### 懸念事項

- `DecodedFloat` の公開のプロパティを変えると、GUI の編集（Patching）に影響する
