# REQ-070: TIFFフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-048で実装されたTIFFフォーマット定義（`formats/tiff.bdef.yaml`）は基本的なヘッダ構造とIFDエントリの解析に対応しているが、以下の点が不完全である。

1. **tiff_tag不完全**: タイル関連タグ（TileWidth, TileLength, TileOffsets, TileByteCounts）、JPEG関連タグ（JPEGInterchangeFormat, JPEGInterchangeFormatLength）、色空間関連タグ（YCbCrCoefficients, YCbCrSubSampling, YCbCrPositioning, ReferenceBlackWhite）、SubIFDsタグが未定義。タイルベースTIFFやJPEG圧縮TIFFの解析時にタグが数値のみで表示され可読性が低い。
2. **IFDチェーン未追跡**: Next IFD Offsetによる複数IFDの連鎖読み取りが実装されていない（スコープ外として維持するが、descriptionで説明を補足）。
3. **value/offset未解決**: IFDエントリのvalue_offsetフィールドが、値そのものなのかオフセットなのかの判別情報（total size <= 4 bytes）がdescriptionに記載されていない。
4. **compression enum不足**: JBIG、JPEG2000、WebP等の新しい圧縮方式がenum定義に含まれていない。
5. **Big-endian TIFF説明不足**: "MM"（ビッグエンディアン）TIFFに関する説明がdescriptionに不足している。

本要望では、既存の `tiff.bdef.yaml` のtag/compression enumを拡充し、descriptionの充実を図る。

## 機能要件

### 追加する機能

- [ ] tiff_tag enum拡充
  - TileWidth = 322
  - TileLength = 323
  - TileOffsets = 324
  - TileByteCounts = 325
  - SubIFDs = 330
  - JPEGInterchangeFormat = 513
  - JPEGInterchangeFormatLength = 514
  - YCbCrCoefficients = 529
  - YCbCrSubSampling = 530
  - YCbCrPositioning = 531
  - ReferenceBlackWhite = 532
- [ ] tiff_compression enum拡充
  - JBIG = 34661
  - JPEG2000 = 34712
  - WebP = 34892
- [ ] tiff_field_type_size virtual field追加
  - field_typeからバイトサイズを計算する説明をdescriptionに追加
  - 例: "BYTE=1, ASCII=1, SHORT=2, LONG=4, RATIONAL=8, ..." の対応表をdescriptionに記載
- [ ] IFD entryのvalue_offsetにdescription追加
  - description: "Value if total size (type_size * count) <= 4 bytes, otherwise offset to value data"
- [ ] Big-endian TIFF("MM")対応の説明をdescriptionに追記
  - byte_orderフィールドのdescriptionに "II = little-endian (Intel), MM = big-endian (Motorola)" と記載
  - 現在は"II"リトルエンディアンのみの実例が多いが、"MM"も有効であることを明記

### 変更する既存機能

- [ ] `formats/tiff.bdef.yaml` — tiff_tag/tiff_compression enum拡充、各フィールドのdescription改善

### 変更しないもの（スコープ外）

- IFDチェーンの自動走査（Next IFD Offsetによる繰り返しseek）
- Strip/Tileデータの実データ読み取り（オフセット先のピクセルデータ解析）
- Exif IFD / GPS IFDの再帰解析
- BigTIFF（8バイトオフセット）対応
- value_offsetの自動判別（total size <= 4の条件分岐）
- field_typeに基づくvalue_offsetの型解釈切り替え

## 受入条件

1. [ ] tiff_tag enumにTileWidth(322), TileLength(323), TileOffsets(324), TileByteCounts(325), SubIFDs(330), JPEGInterchangeFormat(513), JPEGInterchangeFormatLength(514), YCbCrCoefficients(529), YCbCrSubSampling(530), YCbCrPositioning(531), ReferenceBlackWhite(532)が追加されていること
2. [ ] tiff_compression enumにJBIG(34661), JPEG2000(34712), WebP(34892)が追加されていること
3. [ ] field_typeとバイトサイズの対応表がdescriptionに記載されていること
4. [ ] value_offsetフィールドに値/オフセットの判別方法がdescriptionとして記載されていること
5. [ ] byte_orderフィールドにII/MMの説明がdescriptionとして記載されていること
6. [ ] 既存のTIFFヘッダ・IFDエントリ解析に回帰がないこと
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

- [ ] formats/tiff.bdef.yaml — tiff_tag/tiff_compression enum拡充、description改善
- [ ] docs/architecture.md — フォーマット一覧の説明更新（TIFF tag/compression拡充を明記）

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
