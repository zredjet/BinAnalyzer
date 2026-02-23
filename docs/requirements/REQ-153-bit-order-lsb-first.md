# REQ-153: ビットオーダー指定（LSB-first）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-23 |

## 背景・動機

現在のビットストリームモードはMSB-first（最上位ビットから読み取り）に固定されている。`BitReader` は各バイトの最上位ビットから順に読み取る。

しかし、一部のプロトコルやフォーマットではLSB-first（最下位ビットから読み取り）が使われている。

| フォーマット/プロトコル | ビットオーダー | 説明 |
|---|---|---|
| USB | LSB-first | パケットのビットフィールドはLSBから |
| Bluetooth | LSB-first | Link Layerパケット |
| DEFLATE | LSB-first | Huffmanコードのビット読み取り |
| GIF (LZW) | LSB-first | LZW圧縮データのビットパッキング |
| TIFF (PackBits) | MSB-first | 現在の動作で対応可能 |
| H.264/H.265 | MSB-first | 現在の動作で対応可能 |

## 機能要件

### 追加する機能

- [x] ビットストリーム構造体に `bit_order` プロパティを追加（`bit_order: lsb` / `bit_order: msb`）
- [x] LSB-first読み取りモード — 各バイトの最下位ビットから順に読み取る
- [x] デフォルトはMSB-first（後方互換性維持）

### 変更する既存機能

- [x] `BitReader` — LSB-firstモードの読み取りロジック追加
- [x] `DecodeContext.EnterBitstreamMode` — ビットオーダー引数の追加

### 変更しないもの（スコープ外）

- ビットフィールド（`type: bitfield`）のビット抽出 — バイトオーダーベースで動作しており対象外
- ビットストリーム以外のフィールドのビット表現 — 対象外

## 受入条件

1. [x] `bit_order: lsb` を指定したビットストリーム構造体がLSB-first順でビットを読み取ること
2. [x] `bit_order: msb` を指定した場合に既存のMSB-first動作であること
3. [x] `bit_order` 省略時にMSB-first（デフォルト）であること
4. [x] LSB-firstモードで複数バイトにまたがるフィールドが正しくデコードされること
5. [x] 不正な `bit_order` 値の指定時にバリデーションエラーが発生すること
6. [x] 既存のビットストリームテストが全て通過すること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition` — `BitOrder` プロパティ追加。`FormatValidator` — bit_order値の検証 |
| BinAnalyzer.Dsl | `YamlStructModel` — `bit_order` マッピング |
| BinAnalyzer.Engine | `BitReader` — LSB-firstモードの実装。`DecodeContext` — ビットオーダー引数の伝播 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — ビットストリームのbit_order説明追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- `BitOrder` enum（`Msb`, `Lsb`）を新設し、`StructDefinition.BitOrder?` として保持
- `BitReader` クラス内で `ReadBitsMsb` / `ReadBitsLsb` に分岐。MSB-first は既存ロジックそのまま
- `EnterBitstreamMode(BitOrder)` でビットオーダーを受け取り、`_bitstreamDepth == 0` の時のみ新しい `BitReader` を作成
- ネスト時（`_bitstreamDepth > 0`）は既存の `BitReader` を再利用するため、最初の `EnterBitstreamMode` のビットオーダーが適用される

### モデル変更

- `BitOrder.cs`（新規）: `Msb`, `Lsb` の2値enum
- `StructDefinition.BitOrder`: nullable。省略時 = null = MSB-first

### インタフェース変更

- `DecodeContext.EnterBitstreamMode(BitOrder bitOrder = BitOrder.Msb)` — デフォルト引数で後方互換維持

### 代替案

- フィールドレベルで `bit_order` を指定する案 → 構造体レベルの方が実用的（同一ビットストリーム内でビットオーダーが変わることは実用上ない）

### 懸念事項

- なし。シンプルな追加機能で既存動作への影響なし

---

## 実装メモ

### 実装中の設計変更

- 計画通り実装。設計変更なし

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitstreamParsingTests | `ParsesBitOrderLsb` | 1 |
| BitstreamParsingTests | `ParsesBitOrderMsb` | 2 |
| BitstreamParsingTests | `BitOrderDefaultIsNull` | 3 |
| BitstreamParsingTests | `ThrowsForInvalidBitOrder` | 5 |
| BitstreamParsingTests | `ThrowsForBitOrderOnNonBitstream` | 5 |
| BitstreamDecoderTests | `LsbFirstReadsLowBitsFirst` | 1 |
| BitstreamDecoderTests | `LsbFirstMultiByteField` | 4 |
| BitstreamDecoderTests | `MsbFirstExplicitSameAsDefault` | 2, 3 |
| BitstreamDecoderTests | `LsbFirstSequentialReads` | 1 |
| FormatValidatorTests | `VAL122_BitOrderOnNonBitstreamWarns` | 5 |
| FormatValidatorTests | `VAL122_BitOrderOnBitstreamNoWarning` | — |

### 気づき・今後の課題

- LSB-firstの `result` 構築は `result |= bits << totalBitsRead` で、MSB-firstの `result = (result << bitsToRead) | bits` とは異なる点に注意
- ネスト構造体が異なる `BitOrder` を持つ場合、最初の `EnterBitstreamMode` 時のビットオーダーが適用される（実用上問題なし）
