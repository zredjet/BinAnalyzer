# REQ-034: JPEGフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

JPEGは最も普及した画像フォーマットの一つ。マーカーベースのシーケンシャル構造を持ち、BinAnalyzerの現在の機能（switch、式、繰り返し）で定義可能。

SOI（Start of Image）から始まり、APPn/DQT/DHT/SOF/SOS等のマーカーセグメントが連鎖する構造は、switchフィールドの実用的なテストケースとなる。

## 機能要件

### 追加する機能

- [ ] `formats/jpeg.bdef.yaml` フォーマット定義
  - SOI マーカー（0xFFD8）
  - マーカーセグメントの繰り返し（マーカーID 2バイト + length 2バイト + data）
  - switchによるマーカー種別の分岐（APP0/JFIF, APP1/Exif, DQT, DHT, SOF0, SOS, EOI）
  - SOF0セグメント内のフレームパラメータ（幅、高さ、コンポーネント数）
  - SOS以降の画像データはraw bytesとして扱う

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- エントロピー符号化データの解析
- Exifメタデータの詳細解析（TIFF構造のため、オフセットジャンプが必要）
- プログレッシブJPEGの複数スキャン解析
- JPEG 2000

## 受入条件

1. [ ] `formats/jpeg.bdef.yaml` が存在すること
2. [ ] SOIマーカーが正しく解析できること
3. [ ] 主要マーカーセグメント（APP0, DQT, DHT, SOF0, SOS, EOI）が識別できること
4. [ ] SOF0から画像の幅・高さが抽出できること
5. [ ] 有効なJPEGファイルでデコードエラーにならないこと
6. [ ] フォーマット定義がバリデーションに通ること
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] README.md — 対応フォーマットにJPEGを追記
- [ ] docs/architecture.md — formats/ ディレクトリに jpeg.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: JPEG
endianness: big
root: jpeg

structs:
  jpeg           — SOIマーカー + セグメントの繰り返し + 画像データ末尾
  marker_segment — marker_id(u8, 常に0xFF) + marker_type(u8) + switch分岐
  app0_segment   — length(u16) + identifier(ascii,5) + version等
  sof0_segment   — length(u16) + precision(u8) + height(u16) + width(u16)
                   + num_components(u8) + components(repeat_count)
  sof0_component — component_id(u8) + sampling_factors(u8) + qt_table_id(u8)
  sos_segment    — length(u16) + sos_header + image_data(bytes, remaining)
  generic_segment — length(u16) + data(bytes, {length - 2})
  raw_data       — data(bytes, remaining)
```

### 主要struct

- **jpeg**: ルート構造体。soi(bytes, 2, expected=[0xFF, 0xD8]) + segments(struct marker_segment, repeat: eof)。
- **marker_segment**: marker_id(u8) でFF固定 + marker_type(u8) でセグメント種別を識別。marker_typeに対するswitchで各セグメント構造体を選択。EOI(0xD9)の場合はlength無し、SOS(0xDA)の場合はSOS用構造体へ分岐。
- **sof0_segment**: length(u16) + precision(u8) + height(u16) + width(u16) + num_components(u8) + components配列(repeat_count: {num_components}, struct: sof0_component)。
- **sos_segment**: length(u16) + sos_data(bytes, {length - 2}) + image_data(bytes, remaining)。SOSセグメントのヘッダ後の圧縮データはEOIマーカーまで続くが、バイトストリーム内の0xFFエスケープの解析はスコープ外のため、remaining bytesで末尾まで一括読み取りする。

### switchの使い方

marker_typeの値でswitchする。PNGのchunk typeパターン（ascii文字列でswitch）と異なり、u8の整数値でswitchする。

```yaml
switch_on: "{marker_type}"
cases:
  "0xE0": app0_segment    # APP0 (JFIF)
  "0xE1": app1_segment    # APP1 (Exif)
  "0xDB": dqt_segment     # DQT
  "0xC4": dht_segment     # DHT
  "0xC0": sof0_segment    # SOF0 (Baseline DCT)
  "0xDA": sos_segment     # SOS (Start of Scan)
  "0xD9": eoi_marker      # EOI (End of Image) — データ無し
default: generic_segment
```

### enumの使い方

marker_type用のenumを定義し、主要マーカー種別にラベルを付与する。

```yaml
enums:
  jpeg_marker:
    - value: 0xC0
      label: SOF0
      description: "Baseline DCT"
    - value: 0xDA
      label: SOS
    - value: 0xD9
      label: EOI
    - value: 0xDB
      label: DQT
    - value: 0xC4
      label: DHT
    - value: 0xE0
      label: APP0
    - value: 0xE1
      label: APP1
```

### 繰り返しの方式

- **segments**: `repeat: eof` でマーカーセグメントをファイル末尾まで繰り返す。SOSセグメント内でremainingを使い圧縮データ+EOIまで一括読み取りするため、SOS到達後は実質的にファイル末尾まで消費され、繰り返しが自然終了する。
- **sof0_component**: `repeat_count: "{num_components}"` でコンポーネント情報をN回繰り返す。

### 注意点・制約

1. **SOSセグメント以降の処理**: SOS以降は圧縮画像データが続き、その中に0xFFバイトが出現する（スタッフィングバイト）。マーカー解析を続けるにはバイトストリームのスタッフィング解除が必要だが、これはDSLの範囲外。そのためSOS構造体内でremainingを使い、SOS以降の全データ（圧縮データ + EOIマーカー含む）を一括raw bytesとして読み取る設計とする。
2. **EOIマーカー**: SOS内でremainingで読み取るため、EOIは個別のセグメントとしてはデコードされない。switchのcasesにはEOIを定義するが、SOS前に異常終了するファイルへの対応として残す。
3. **マーカーのlengthフィールド**: JPEGのlengthフィールドはlength自身の2バイトを含む。データ部のサイズは `{length - 2}` で計算する。switchのsizeに `{length - 2}` を指定する。ただしlengthフィールド自体はswitch外（marker_segment内）に配置するか、各case構造体内に含めるか検討が必要。各case構造体内にlengthを含める方が、switchのsize指定が不要になり自然。
4. **marker_segmentの構造の選択肢**: marker_typeの後にlengthが来るセグメント（APP0, SOF0等）と来ないセグメント（SOI, EOI）がある。SOIはルート構造体で固定読み取りし、EOIはSOS内で吸収するため、marker_segmentに来るセグメントは基本的にlengthを持つ。switchの外にlengthを配置し、switchのsizeを `{length - 2}` とするのが最もシンプル。
5. **Exif (APP1)**: TIFF構造のためオフセットジャンプが必要で完全な解析は不可。raw bytesとして読み取る。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
