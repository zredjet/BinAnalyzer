# REQ-054: PNGフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

既存の `formats/png.bdef.yaml` はIHDR、IDAT、IEND、tEXtといった基本チャンクのみ対応しており、PNG仕様で定義されている重要なチャンク（PLTE、gAMA、cHRM、tRNS等）が未解析のままraw bytesとして表示される。

特にPLTE（パレット）チャンクはインデックスカラーPNG画像で必須であり、gAMA（ガンマ補正）やcHRM（色度情報）はカラーマネジメントの理解に重要。tRNS（透過情報）はアルファチャンネルを持たないPNGでの透過表現に使用される。また、テキスト系チャンク（zTXt、iTXt）はメタデータ保持に広く使われているが、現在は未対応。

本要望では、これらの主要チャンクをswitchのcasesに追加し、内部構造を定義することで、PNG解析結果の情報量と可読性を大幅に向上させる。

## 機能要件

### 追加する機能

- [ ] PLTEチャンク追加
  - R(uint8), G(uint8), B(uint8)の3バイト組を `repeat_count(chunk_length / 3)` で繰り返し
  - パレットエントリ数はチャンク長を3で割った値
- [ ] gAMAチャンク追加
  - gamma: uint32（ガンマ値 x 100000）
- [ ] cHRMチャンク追加
  - 8つのuint32フィールド: white_point_x, white_point_y, red_x, red_y, green_x, green_y, blue_x, blue_y
  - 各値はCIE xy色度座標 x 100000
- [ ] tRNSチャンク追加
  - color_typeによりフォーマットが異なるが、汎用的にraw bytes（`size: remaining`）で対応
- [ ] zTXtチャンク追加
  - keyword: asciiz（NULL終端文字列）
  - compression_method: uint8
  - compressed_data: bytes（`size: remaining`、zlib圧縮テキスト）
- [ ] iTXtチャンク追加
  - keyword: asciiz（NULL終端文字列）
  - compression_flag: uint8
  - compression_method: uint8
  - language_tag: asciiz
  - translated_keyword: asciiz
  - text: bytes（`size: remaining`）
- [ ] bKGDチャンク追加
  - raw bytes（`size: remaining`、color_typeにより内容が異なる）
- [ ] sBITチャンク追加
  - raw bytes（`size: remaining`、color_typeにより内容が異なる）

### 変更する既存機能

- [ ] `formats/png.bdef.yaml` — switchのcasesに上記チャンクを追加
- [ ] text_chunk（tEXt）の改善: keywordをasciizで分離パースし、keyword/textを別フィールドとして表示

### 変更しないもの（スコープ外）

- tRNSのcolor_type別の詳細パース（将来的にswitch分岐で対応可能）
- bKGD/sBITのcolor_type別の詳細パース
- zTXtのzlib圧縮データの展開表示
- アニメーションPNG（APNG）関連チャンク（acTL, fcTL, fdAT）
- iCCP（ICCプロファイル）チャンク

## 受入条件

1. [ ] PLTEチャンクがswitch casesに追加され、R/G/Bエントリが繰り返しデコードされること
2. [ ] gAMAチャンクがswitch casesに追加され、gamma値がuint32として表示されること
3. [ ] cHRMチャンクがswitch casesに追加され、8つの色度値が個別フィールドとして表示されること
4. [ ] tRNS、bKGD、sBITチャンクがswitch casesに追加され、raw bytesとして表示されること
5. [ ] zTXtチャンクがswitch casesに追加され、keyword/compression_method/compressed_dataが分離表示されること
6. [ ] iTXtチャンクがswitch casesに追加され、keyword/flags/language/textが分離表示されること
7. [ ] tEXtチャンクのkeywordがasciizで分離パースされること
8. [ ] 既存のIHDR/IDAT/IENDチャンクの解析に回帰がないこと
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なファイル

- [ ] formats/png.bdef.yaml — 各チャンクの構造体追加、switch cases拡充、tEXt改善
- [ ] docs/architecture.md — フォーマット一覧の説明更新（対応チャンク数の記載）

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
