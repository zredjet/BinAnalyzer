# REQ-058: JPEGフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のjpeg.bdef.yaml（REQ-034で導入）では、DQT（量子化テーブル）やDHT（ハフマンテーブル）がgeneric_segmentとして扱われており、内部構造が未解析のままである。また、APP0（JFIF）の詳細パース（バージョン、密度情報等）が行われておらず、APP1（EXIF）も未解析である。jpeg_marker enumも主要マーカーのみで、DRI、COM、APP2-APP15が含まれていない。

JPEG画像のメタデータ解析やデバッグにおいて、量子化テーブル・ハフマンテーブルの構造やJFIFパラメータの確認は一般的なユースケースであり、これらの詳細解析は実用上の価値が高い。

## 機能要件

### 追加する機能

- [ ] DQT（量子化テーブル）パース
  - `precision_and_table_id`: uint8 bitfield（precision bits 7:4, table_id bits 3:0）
  - `table_data`: bytes, size = precision == 0 ? 64 : 128（8bit精度なら64バイト、16bit精度なら128バイト）
- [ ] DHT（ハフマンテーブル）パース
  - `class_and_id`: uint8 bitfield（table_class bits 7:4, table_id bits 3:0）
  - `num_codes`: bytes, size=16（各コード長のシンボル数、16バイト固定）
  - `values`: bytes, size=remaining（ハフマン値テーブル、バウンダリスコープ内の残りバイト）
- [ ] APP0（JFIF）パース
  - `identifier`: ascii, size=5（"JFIF\0" を期待）
  - `version_major`: u8
  - `version_minor`: u8
  - `density_units`: u8, enum（0=no units, 1=dots per inch, 2=dots per cm）
  - `x_density`: u16
  - `y_density`: u16
  - `thumbnail_width`: u8
  - `thumbnail_height`: u8
- [ ] DRI（リスタートインターバル）パース
  - `restart_interval`: uint16
- [ ] jpeg_marker enum拡充
  - DRI = 0xFFDD
  - COM = 0xFFFE
  - APP2 = 0xFFE2 ~ APP15 = 0xFFEF

### 変更する既存機能

- [ ] `formats/jpeg.bdef.yaml` — DQT/DHT/APP0/DRIの各switch caseを追加、jpeg_marker enum拡充
- [ ] `docs/architecture.md` — フォーマット一覧の更新（既存JPEGエントリの説明拡充）

### 変更しないもの（スコープ外）

- APP1（Exif）の詳細解析（TIFF構造のためオフセットジャンプが必要）
- エントロピー符号化データの解析
- プログレッシブJPEG固有のSOS解析
- 量子化テーブル・ハフマンテーブルの意味的な妥当性検証

## 受入条件

1. [ ] DQTセグメントがswitch caseに追加され、precision/table_id/table_dataが個別フィールドとして解析されること
2. [ ] DHTセグメントがswitch caseに追加され、class_and_id/num_codes/valuesが個別フィールドとして解析されること
3. [ ] APP0セグメントがJFIF構造として詳細解析され、バージョン・密度情報が抽出できること
4. [ ] DRIセグメントがswitch caseに追加され、restart_intervalが解析されること
5. [ ] jpeg_marker enumにDRI(0xDD), COM(0xFE), APP2-APP15が追加されていること
6. [ ] 有効なJPEGファイルで拡充後もデコードエラーにならないこと
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

- [ ] `formats/jpeg.bdef.yaml` — DQT/DHT/APP0/DRIセグメント構造追加、enum拡充
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新

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
