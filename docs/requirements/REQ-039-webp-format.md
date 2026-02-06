# REQ-039: WebPフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

WebPはGoogleが開発した画像フォーマットで、RIFFコンテナに基づく構造を持つ。WAVファイルと同じRIFF構造であるため、既存のWAV定義（imports機能）との共通部分を共有できる可能性がある。

VP8（非可逆）、VP8L（可逆）、VP8X（拡張）の3種類のチャンクタイプが主要構成要素。

## 機能要件

### 追加する機能

- [ ] `formats/webp.bdef.yaml` フォーマット定義
  - RIFFヘッダ（"RIFF" magic, filesize, "WEBP" form type）
  - チャンクの繰り返し（チャンクID 4バイト + size 4バイト + data）
  - switchによるチャンク種別の分岐
    - VP8: 非可逆圧縮データ（フレームヘッダの幅・高さ）
    - VP8L: 可逆圧縮データ（シグネチャ + 幅・高さ）
    - VP8X: 拡張ヘッダ（フラグ、キャンバス幅・高さ）
    - ALPH, ANIM, ANMF, ICCP, EXIF, XMP 等
  - チャンクデータの2バイトアライメント（パディング）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- VP8/VP8Lの圧縮画像データの展開
- アニメーションフレームの個別解析
- Exifメタデータの詳細解析

## 受入条件

1. [ ] `formats/webp.bdef.yaml` が存在すること
2. [ ] RIFFヘッダとWEBPフォームタイプが正しく解析できること
3. [ ] VP8/VP8L/VP8Xチャンクが識別・解析できること
4. [ ] VP8Xの拡張フラグが解析できること
5. [ ] 2バイトアライメントのパディングが正しく処理されること
6. [ ] 有効なWebPファイルでデコードエラーにならないこと
7. [ ] フォーマット定義がバリデーションに通ること
8. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにWebPを追記
- [ ] docs/architecture.md — formats/ ディレクトリに webp.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: WebP
endianness: little
root: webp
```

**主要struct:**

- **`webp`** (root) — ファイル全体
  - `riff_magic`: ascii, size=4 （"RIFF" を期待、expected でバリデーション）
  - `file_size`: u32
  - `webp_magic`: ascii, size=4 （"WEBP" を期待）
  - `chunks`: struct=webp_chunk, repeat=eof
- **`webp_chunk`** — 個別チャンク
  - `chunk_id`: ascii, size=4
  - `chunk_size`: u32
  - `data`: switch, size="{chunk_size}", switch_on="{chunk_id}"
  - `padding`: bytes, size="{chunk_size % 2}", padding=true （奇数サイズ時の1バイトパディング）
- **`vp8x_data`** — VP8X拡張ヘッダ
  - `flags`: bitfield, size=1 (reserved[2], animation[1], xmp[1], exif[1], alpha[1], icc[1], reserved[1])
  - `reserved`: bytes, size=3, padding=true
  - `canvas_width_b`: bytes, size=3 （24ビット幅 - 1）
  - `canvas_width_minus_one`: virtual, value="{...}" （3バイトからリトルエンディアン値を計算）
  - `canvas_height_b`: bytes, size=3 （24ビット高さ - 1）
  - `canvas_height_minus_one`: virtual, value="{...}"
- **`raw_data`** — 未対応チャンクの生データ

### enum定義

特になし。チャンクIDはascii文字列のためenumではなくswitchのcase値で分岐する。

### switchの使い方

`webp_chunk` の `data` フィールドで `switch_on: "{chunk_id}"` を使用。sizeは `"{chunk_size}"` でバウンダリスコープを設定し、チャンクデータ長を制限する。

```yaml
cases:
  "'VP8X'": vp8x_data
  "'VP8 '": raw_data   # VP8非可逆（内部解析はスコープ外）
  "'VP8L'": raw_data   # VP8L可逆（同上）
  "'ALPH'": raw_data
  "'ANIM'": raw_data
  "'ANMF'": raw_data
  "'ICCP'": raw_data
  "'EXIF'": raw_data
  "'XMP '": raw_data
default: raw_data
```

### 繰り返しの方式

チャンクの繰り返しには `repeat: eof` を使用。チャンク数が事前にわからないため、ファイル末尾まで読み進める。

### 注意点・制約

- **2バイトアライメント**: RIFFチャンクはデータ部が偶数バイト境界にアラインされる。`chunk_size` が奇数の場合、データ後に1バイトのパディングが入る。`padding` フィールドの size を `"{chunk_size % 2}"` で計算する（0 or 1）。
- **u24型が存在しない**: VP8Xのキャンバス幅・高さは24ビット値。bytes(3) で読み取り、virtualフィールドでバイトを結合して値を計算する。リトルエンディアンなので `{b0 | (b1 << 8) | (b2 << 16)}` の形。ただし、bytes(3)の個別バイトへのアクセス方法がDSLの制約になる可能性がある。個別バイトをu8x3で読む方式も検討する。
- **file_size**: RIFFヘッダの file_size はファイル全体サイズ - 8（RIFF magic + size フィールド自身を除く）。バリデーションには使わず表示のみ。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
