# REQ-063: BMPフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-007で実装されたBMPフォーマット定義（`formats/bmp.bdef.yaml`）はBITMAPINFOHEADER（header_size=40）固定であり、Windows 95以降で広く使用されるBITMAPV4HEADER（header_size=108）やWindows 98/2000以降のBITMAPV5HEADER（header_size=124）に未対応である。

V4ヘッダはICCカラーマネジメント用のカラーマスクやガンマ値を含み、V5ヘッダはさらにレンダリングインテントやICCプロファイルデータへの参照を持つ。また、現在のcompression enumは基本値のみであり、BI_ALPHABITFIELDSやBI_JPEG/BI_PNG等が未定義。さらに、8bit以下のBMP画像で必須となるカラーテーブルが未パースのため、インデックスカラー画像の構造を正しく表示できない。

本要望では、ヘッダサイズによるswitch分岐、V4/V5ヘッダ構造体の追加、compression enum拡充、カラーテーブルの条件パースを行い、BMPフォーマットの解析精度を向上させる。

## 機能要件

### 追加する機能

- [ ] ヘッダサイズによるswitch分岐
  - header_size読み取り後に `switch_on(header_size)` で情報ヘッダを分岐
  - 40 = BITMAPINFOHEADER（既存構造体）
  - 108 = BITMAPV4HEADER
  - 124 = BITMAPV5HEADER
- [ ] BITMAPV4HEADER構造体追加
  - BITMAPINFOHEADERの全フィールド（width, height, planes, bit_count, compression, image_size, x_pixels_per_meter, y_pixels_per_meter, colors_used, colors_important）
  - red_mask (u32) — 赤チャンネルのビットマスク
  - green_mask (u32) — 緑チャンネルのビットマスク
  - blue_mask (u32) — 青チャンネルのビットマスク
  - alpha_mask (u32) — アルファチャンネルのビットマスク
  - cs_type (u32) — 色空間タイプ
  - endpoints (bytes 36) — CIEXYZTRIPLE構造体（3x3座標）
  - gamma_red (u32) — 赤チャンネルのガンマ値
  - gamma_green (u32) — 緑チャンネルのガンマ値
  - gamma_blue (u32) — 青チャンネルのガンマ値
- [ ] BITMAPV5HEADER構造体追加
  - BITMAPV4HEADERの全フィールド
  - intent (u32 enum) — レンダリングインテント
  - profile_data (u32) — ICCプロファイルデータのオフセット
  - profile_size (u32) — ICCプロファイルデータのサイズ
  - reserved (u32) — 予約領域
- [ ] compression enum拡充
  - BI_ALPHABITFIELDS = 6
  - BI_JPEG = 4
  - BI_PNG = 5
- [ ] カラーテーブルの条件パース
  - if条件 `bit_count <= 8` でcolor_tableを追加
  - repeat_countで色数分のエントリを繰り返しデコード
  - 各エントリ: blue (u8), green (u8), red (u8), reserved (u8)

### 変更する既存機能

- [ ] `formats/bmp.bdef.yaml` — ヘッダサイズによるswitch分岐追加、V4/V5構造体追加、compression enum拡充、カラーテーブルの条件パース追加

### 変更しないもの（スコープ外）

- ピクセルデータの詳細解析（圧縮展開、ビット深度別パース）
- OS/2 BMP（BITMAPCOREHEADER, header_size=12）対応
- ICCプロファイルデータ本体の解析
- CIEXYZTRIPLE構造体の個別フィールド分解

## 受入条件

1. [ ] header_size=40のBMPファイルがBITMAPINFOHEADERとしてswitch解析可能であること
2. [ ] header_size=108のBMPファイルがBITMAPV4HEADERとしてswitch解析可能であること（カラーマスク、ガンマ値が個別フィールド表示）
3. [ ] header_size=124のBMPファイルがBITMAPV5HEADERとしてswitch解析可能であること（intent、profile_data等が個別フィールド表示）
4. [ ] compression enumにBI_ALPHABITFIELDS(6), BI_JPEG(4), BI_PNG(5)が追加されていること
5. [ ] bit_count<=8のBMPファイルでカラーテーブルが条件パースされ、各エントリのblue/green/red/reservedが表示されること
6. [ ] 既存のBITMAPINFOHEADER解析に回帰がないこと
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] formats/bmp.bdef.yaml — ヘッダswitch分岐追加、V4/V5構造体追加、compression enum拡充、カラーテーブル条件パース追加
- [ ] docs/architecture.md — フォーマット一覧の説明更新（BMP V4/V5ヘッダ対応を明記）

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
