# REQ-043: FLACフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし（第2弾でREQ-044に依存） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

FLAC（Free Lossless Audio Codec）はオープンソースの可逆音声圧縮フォーマット。"fLaC" マジック + METADATAブロック連鎖 + オーディオフレームで構成される。

METADATAブロックは「最終ブロックフラグ（1bit）+ ブロックタイプ（7bit）+ データ長（24bit）+ data」の繰り返しで、ビットフィールドの実用的なテストケースとなる。

第1弾ではMETADATAブロック（STREAMINFO, VORBIS_COMMENT等）の解析に限定する。第2弾でオーディオフレームヘッダの解析に対応する。

## 機能要件

### 追加する機能

- [ ] `formats/flac.bdef.yaml` フォーマット定義（第1弾）
  - マジック（"fLaC" 4バイト）
  - METADATAブロックの繰り返し（最終ブロックフラグで終端）
    - ブロックヘッダ（is_last 1bit, block_type 7bit, length 24bit）— bitfield使用
    - switchによるブロックタイプの分岐
      - STREAMINFO（0）: サンプルレート、チャンネル数、ビット深度、総サンプル数、MD5シグネチャ
      - PADDING（1）: パディングバイト列
      - APPLICATION（2）: アプリケーションID + データ
      - SEEKTABLE（3）: シークポイントの配列
      - VORBIS_COMMENT（4）: ベンダー文字列 + コメント配列
      - PICTURE（6）: 画像タイプ、MIMEタイプ、説明、画像データ

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- オーディオフレームのデコード
- FLAC圧縮データの展開
- Ogg FLACコンテナ

## 受入条件

1. [ ] `formats/flac.bdef.yaml` が存在すること
2. [ ] "fLaC" マジックが正しく解析できること
3. [ ] STREAMINFOブロックからサンプルレート、チャンネル数、ビット深度が抽出できること
4. [ ] METADATAブロックが最終ブロックフラグに基づいて正しく繰り返し解析できること
5. [ ] VORBIS_COMMENTブロックが解析できること
6. [ ] 有効なFLACファイルでデコードエラーにならないこと
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

- [ ] README.md — 対応フォーマットにFLACを追記
- [ ] docs/architecture.md — formats/ ディレクトリに flac.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: FLAC
endianness: big
root: flac
```

**主要struct:**

- **`flac`** (root) — ファイル全体
  - `magic`: ascii, size=4 （"fLaC" を期待、bytes expected=[0x66,0x4C,0x61,0x43] でも可）
  - `metadata_blocks`: struct=metadata_block, repeat_until="{is_last == 1}"
- **`metadata_block`** — メタデータブロック
  - `header_byte`: u8
  - `is_last`: virtual, value="{header_byte >> 7}"
  - `block_type`: virtual, value="{header_byte & 0x7F}"
  - `length_b0`: u8
  - `length_b1`: u8
  - `length_b2`: u8
  - `length`: virtual, value="{(length_b0 << 16) | (length_b1 << 8) | length_b2}"
  - `data`: switch, size="{length}", switch_on="{block_type}"
- **`streaminfo`** (block_type=0) — ストリーム情報
  - `min_block_size`: u16
  - `max_block_size`: u16
  - `min_frame_size_b0`: u8, `min_frame_size_b1`: u8, `min_frame_size_b2`: u8
  - `min_frame_size`: virtual （3バイトから計算）
  - `max_frame_size_b0`: u8, `max_frame_size_b1`: u8, `max_frame_size_b2`: u8
  - `max_frame_size`: virtual （3バイトから計算）
  - `stream_info_bits`: bytes, size=8 （sample_rate:20bit, channels:3bit, bps:5bit, total_samples:36bit のパック領域。ビットが跨るため個別抽出が困難）
  - `md5`: bytes, size=16
- **`padding_block`** (block_type=1) — パディング
  - `padding`: bytes, size=remaining
- **`application_block`** (block_type=2) — アプリケーション
  - `app_id`: bytes, size=4
  - `data`: bytes, size=remaining
- **`seektable_block`** (block_type=3) — シークテーブル
  - `seekpoints`: struct=seekpoint, repeat=eof （バウンダリスコープ内のEOFまで）
- **`seekpoint`** — シークポイント（18バイト固定）
  - `sample_number`: u64
  - `offset`: u64
  - `num_samples`: u16
- **`vorbis_comment_block`** (block_type=4) — Vorbisコメント
  - `data`: bytes, size=remaining （リトルエンディアン制約のため生bytesとして扱う。詳細は注意点参照）
- **`picture_block`** (block_type=6) — 画像
  - `picture_type`: u32, enum=picture_type
  - `mime_length`: u32
  - `mime`: ascii, size="{mime_length}"
  - `desc_length`: u32
  - `description`: utf8, size="{desc_length}"
  - `width`: u32
  - `height`: u32
  - `depth`: u32
  - `colors`: u32
  - `data_length`: u32
  - `data`: bytes, size="{data_length}"

### enum定義

- **`block_type_enum`**: 0=STREAMINFO, 1=PADDING, 2=APPLICATION, 3=SEEKTABLE, 4=VORBIS_COMMENT, 5=CUESHEET, 6=PICTURE（virtualフィールドに直接enumを付けられない場合は表示用のみ）
- **`picture_type`**: 0=Other, 1=File_Icon_32x32, 2=Other_File_Icon, 3=Front_Cover, 4=Back_Cover, etc.

### switchの使い方

`metadata_block` の `data` フィールドで `switch_on: "{block_type}"` を使用。size="{length}" でバウンダリスコープを設定する。

```yaml
cases:
  "0": streaminfo
  "1": padding_block
  "2": application_block
  "3": seektable_block
  "4": vorbis_comment_block
  "6": picture_block
default: raw_data
```

### 繰り返しの方式

メタデータブロックの繰り返しに `repeat_until: "{is_last == 1}"` を使用。各ブロックの先頭バイトの最上位ビット（is_last）が1になったブロックを最後に繰り返しを終了する。repeat_until は条件が真になった要素を含めて終了するため、最終ブロックも正しくデコードされる。

シークテーブル内の seekpoint は `repeat: eof` でバウンダリスコープ（size=length で設定済み）内のEOFまで繰り返す。各 seekpoint は18バイト固定なので、length / 18 個のエントリが読み取られる。

### 注意点・制約

- **u24型が存在しない**: ブロックヘッダの length（24ビット）と STREAMINFO の min/max_frame_size（24ビット）は、3個の u8 として読み取り virtual フィールドでビットシフト演算を使って結合する: `{(b0 << 16) | (b1 << 8) | b2}`。
- **STREAMINFO のパック領域**: sample_rate(20bit) + channels(3bit) + bits_per_sample(5bit) + total_samples(36bit) の計64ビットはバイト境界を跨いでパックされている。bitfield の size=8 で読み取り可能だが、36ビットの total_samples がビット範囲指定で正しく扱えるかはDSLの実装依存。第1弾では stream_info_bits を生bytes(8) として出力し、個別フィールド抽出は将来課題とする。
- **VORBIS_COMMENT のリトルエンディアン問題**: FLAC全体は big-endian だが、VORBIS_COMMENT ブロック内部の長さフィールド（vendor_length, comment_count, 各comment length）はリトルエンディアンで格納される（Vorbis仕様由来の歴史的経緯）。現在のDSLではフィールド単位のエンディアン指定ができないため（REQ-045で対応予定）、VORBIS_COMMENT の内部構造を正しくデコードできない。そのため第1弾では `data: bytes, size=remaining` として生バイト出力とする。REQ-045（per-field endianness）実装後に構造的デコードに対応する。
- **is_last と block_type の抽出**: 先頭バイトを u8 で読み、virtual でビット演算（`>> 7` と `& 0x7F`）を行う方式を採用。bitfield (size=1) でも可能だが、block_type を switch_on で参照する必要があるため、virtual の方が式参照との互換性が高い。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
