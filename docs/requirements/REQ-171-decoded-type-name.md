# REQ-171: デコード結果ノードに DSL 型名を保持する

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-169（`DecodedNode.DslType`） |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

`DecodedInteger` 等は値とサイズしか持たず、DSL の型名（`uint32` / `int16` / `uleb128` 等）や符号を保持していない。そのため `NodeDisplayText.TypeLabel`（REQ-167）は `int32` のようなサイズ由来の近似ラベルしか出せず、GUI インスペクター・ステータスバー・TUI 詳細の「型」表示が不正確になる（REQ-167 / REQ-168 の課題）。

## 機能要件

### 追加する機能

- [x] `DecodedNode` に `TypeName: string?`（DSL の `type:` 文字列）を追加。エンジンが全ノード種別で設定（`DslType` から導出）
- [x] `NodeDisplayText.TypeLabel` は `TypeName` があればそれを優先し、無ければ従来の近似
- [x] JSON 出力（`JsonOutputFormatter`）に `type` を出す（既に出しているなら整合を確認）

### 変更する既存機能

- [x] `DecodedInteger` 等の生成箇所（`BinaryDecoder`）で `TypeName` を設定

### 変更しないもの（スコープ外）

- Decoded モデルへの符号・ビット幅以外のメタデータ追加

## 受入条件

1. [x] `uint32` / `int16` / `float32` / `ascii` / `bytes` の各フィールドで `TypeName` が DSL の型名と一致すること
2. [x] `TypeLabel` が `u32` / `i16` 等の正確なラベルを返すこと（既存の `TypeLabel_*` テストを更新）
3. [x] ゴールデンファイル（JSON 出力）が更新されること
4. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodedNode.TypeName` |
| BinAnalyzer.Engine | 設定箇所 |
| BinAnalyzer.Presentation | `TypeLabel` |
| BinAnalyzer.Output | JSON 出力（必要なら） |

### 変更が必要なドキュメント

- [x] docs/architecture.md — デコード結果ツリー

---

## 設計メモ

### 設計方針

- **`TypeName` は `DslType` から導出する。** REQ-169 で `DecodedNode.DslType: FieldType?` をエンジンが設定するようになったので、文字列を別途持たず `TypeName => FieldTypeNames.ToDslName(DslType)` の計算プロパティにする。型名と `FieldType` が食い違う余地が無く、ノード生成箇所の変更は「`DslType` を渡す」だけで済む。
- **DSL 型名の対応表を Core に置く。** `FieldTypeNames.ToDslName`（正規名 1 つ）/ `TryParse`（`u32` / `f32` / `shift_jis` などの別名も受理）/ `ShortLabel`（表示用 `u32` / `i16` / `f32`）。Dsl の `YamlToIrMapper.ParseFieldType` は `TryParse` に委譲し、別名の一覧を 1 か所にまとめる。`TypeName` は別名で書かれていても正規名（`u8` → `uint8`）になる。
- **全ノード種別で `DslType` を設定する。** REQ-169 ではスカラー系だけだったものを struct（`struct`）、switch の結果（`switch`）、配列（要素フィールドの型）、bitfield、圧縮（`zlib` 等）、virtual、エラーにも広げる。`DecodeStruct` に `dslType` 引数を足し、呼び出し元（struct フィールド / switch / 圧縮展開内容）が渡す。ルート構造体は DSL のフィールドではないので null。
- **`TypeLabel` は `DslType` があれば正確なラベル、無ければ従来の推定。** 整数 `u32` / `i16`、浮動小数点 `f32` / `f64`、文字列 `ascii[4]`（NUL 終端は `asciiz[5]`）、可変長整数 `uleb128`、フラグ `u32 (flags)`、配列 `struct[3]` / `u8[16]`、ビットストリーム `u8:3bit`。手で組んだノード（テスト・古いコード）では従来どおり `int32` 等にフォールバックするので互換を保つ。enum は `EnumLabel` が無くても `EnumRef` があれば `(enum)` を付ける。
- **JSON 出力は `_type`（ノード種別）と別に `type`（DSL 型名）を出す。** `_type: "integer"` は構造の種類、`type: "uint32"` は DSL の宣言で、役割が違うので両方残す。null のノード（ルート等）では省略。
- **`NodeDetailFormatter` の `Type` 行も `TypeName` を優先する。** TUI の詳細ペインでも `integer` ではなく `uint32` と出る。

### モデル変更

- `DecodedNode.TypeName: string?`（計算プロパティ）
- Core `Models/FieldTypeNames`

### インタフェース変更

- `BinaryDecoder.DecodeStruct(..., FieldType? dslType = null)`（private）
- JSON 出力に `type` キー追加（ゴールデンファイル 36 件更新）

### 代替案

- `TypeName` を `init` プロパティとしてエンジンが文字列を設定する案（要件書の原案）— `DslType` と二重管理になり、別名（`u32`）をそのまま持つか正規化するかも決めないといけない。導出にした。
- `TypeLabel` を DSL 正規名（`uint32`）で出す案 — インスペクターの型欄・ステータスバーは幅が限られるので、受入条件どおり短縮形（`u32`）にした。詳細行（`NodeDetailFormatter`）と JSON は正規名。

### 懸念事項

- 既存のゴールデンファイルは全て変わる（`type` キーが増える）。`UPDATE_GOLDEN=1` で再生成した。
- GUI インスペクターの型欄は `int8 (enum)` → `u8 (enum)` のように表示が変わる。

---

## 実装メモ

### 実装中の設計変更

- `DecodedError` には既存の `FieldType: string?` があるが、そのままにして `DslType` も併せて設定した（JSON の `field_type` は互換のため維持）。
- 配列（`DecodedArray`）の `TypeName` は要素フィールドの型（`repeat` 付きフィールドの `type:` そのもの）。`TypeLabel` は `struct[3]` のように要素数を添える。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Engine.Tests / TypeNameTests | `Decode_SetsTypeName_ForEveryNodeKind`（uint32 / int16 / float32 / ascii / bytes / uleb128 / u8 / asciiz / bitfield / struct / switch / 配列 / virtual の 13 ケース）、`Decode_AliasTypeName_IsCanonical_AndRootHasNone`、`Decode_CompressedNode_KeepsCompressionTypeName` | 1 |
| Engine.Tests / TypeNameTests | `FieldTypeNames_RoundTrip_AllTypes` | 1 |
| Presentation.Tests / NodeDisplayTextTests | `TypeLabel_Integer_WithDslType_IsExact`（6 ケース）、`TypeLabel_EnumRefWithoutLabel_StillMarksEnum`、`TypeLabel_Float_String_Flags_Array_UseDslType`、`TypeLabel_Integer_WithoutDslType_FallsBackToSize`（既存を改名） | 2 |
| Gui.Tests / ComponentTests | `Inspector_RendersRows_ForInteger_Enum_Flags_Validation`（`int8 (enum)` → `u8 (enum)` に更新） | 2 |
| Integration.Tests / GoldenFileTests | 36 フォーマットのゴールデンを `type` 付きで再生成 | 3 |
| 全体 | `dotnet test` 1,600 件通過 | 4 |

### 気づき・今後の課題

- `DecodedFlags` / `DecodedBitfield` の `TypeLabel` は `u32 (flags)` / `bitfield8` のまま。bitfield の `size:` はバイト数なので `bitfield{bits}` 表記は据え置いた。
- REQ-169 の編集可否判定（`FieldEditRules`）は `DslType` を見ているので、本要望で struct / switch / 配列にも `DslType` が付いたが、それらは種別で先に弾かれるため挙動は変わらない。
