# REQ-067: WebPフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-039で実装されたWebPフォーマット定義（`formats/webp.bdef.yaml`）はVP8X（拡張ヘッダ）チャンクのみが構造解析されており、VP8（非可逆圧縮）、VP8L（可逆圧縮）、ALPH（アルファチャンネル）、ANIM/ANMF（アニメーション）、ICCP（ICCプロファイル）、EXIF（メタデータ）等のチャンクは全てraw_dataとして表示されるのみである。

WebPは現代のウェブで広く使用される画像フォーマットであり、VP8/VP8Lの基本ヘッダ情報（フレームタイプ、画像サイズ）やアニメーション情報（背景色、ループカウント、フレーム位置・サイズ・持続時間）は画像解析において重要な情報である。ALPHチャンクの圧縮方式やフィルタリング方式の表示も透過画像の構造理解に有用である。

本要望では、各主要チャンクの内部構造を定義し、switchのcasesに追加することで、WebPフォーマットの解析結果の情報量と可読性を大幅に向上させる。

## 機能要件

### 追加する機能

- [ ] VP8チャンクパース
  - frame_tag (bytes 3, bitfield) — frame_type bit 0, version bits 3:1, show_frame bit 4, partition_size bits 23:5
  - start_code (bytes 3) — 期待値 [0x9D, 0x01, 0x2A]
  - width_and_scale (u16 le) — 幅とスケール情報（下位14bitが幅、上位2bitがスケール）
  - height_and_scale (u16 le) — 高さとスケール情報（下位14bitが高さ、上位2bitがスケール）
- [ ] VP8Lチャンクパース
  - signature (u8) — 期待値 0x2F
  - data (bytes remaining) — VP8L losslessビットストリーム（descriptionに説明を記載）
- [ ] ALPHチャンクパース
  - flags (u8, bitfield) — preprocessing bits 5:4, filtering bits 3:2, compression bits 1:0
  - data (bytes remaining) — アルファチャンネルデータ
- [ ] ANIMチャンクパース
  - background_color (u32) — 背景色（BGRA）
  - loop_count (u16) — ループ回数（0=無限ループ）
- [ ] ANMFチャンクパース
  - frame_x (u32, 24bit packed) — フレームX座標
  - frame_y (u32, 24bit packed) — フレームY座標
  - width_minus_one (u32, 24bit) — フレーム幅-1
  - height_minus_one (u32, 24bit) — フレーム高さ-1
  - duration (u32, 24bit) — フレーム表示時間（ミリ秒）
  - flags (u8, bitfield) — ブレンド方法、廃棄方法等
- [ ] ICCPチャンクパース
  - icc_data (bytes remaining) — ICCプロファイルデータ
- [ ] EXIFチャンクパース
  - exif_data (bytes remaining) — EXIFメタデータ

### 変更する既存機能

- [ ] `formats/webp.bdef.yaml` — switchのcasesにVP8, VP8L, ALPH, ANIM, ANMF, ICCP, EXIFチャンクを追加

### 変更しないもの（スコープ外）

- VP8ビットストリームの詳細解析（マクロブロック、パーティション等）
- VP8Lビットストリームの詳細解析（エントロピー符号化等）
- ALPHデータの展開・解析
- ICCプロファイルデータの内部構造解析
- EXIFメタデータのTIFF IFD構造解析
- XMPチャンクの解析
- ANMFフレーム内の画像データ（VP8/VP8L）の再帰的解析

## 受入条件

1. [ ] VP8チャンクがswitch casesに追加され、frame_tag(bitfield), start_code, width_and_scale, height_and_scaleが個別フィールドとして表示されること
2. [ ] VP8Lチャンクがswitch casesに追加され、signature, dataが表示されること
3. [ ] ALPHチャンクがswitch casesに追加され、flags(bitfield: preprocessing, filtering, compression), dataが表示されること
4. [ ] ANIMチャンクがswitch casesに追加され、background_color, loop_countが表示されること
5. [ ] ANMFチャンクがswitch casesに追加され、frame_x, frame_y, width_minus_one, height_minus_one, duration, flagsが表示されること
6. [ ] ICCP, EXIFチャンクがswitch casesに追加され、データがbytes表示されること
7. [ ] 既存のVP8Xチャンク解析に回帰がないこと
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] formats/webp.bdef.yaml — VP8/VP8L/ALPH/ANIM/ANMF/ICCP/EXIFチャンク構造体追加、switch cases拡充
- [ ] docs/architecture.md — フォーマット一覧の説明更新（WebP対応チャンク数の記載）

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
