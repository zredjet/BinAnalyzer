# REQ-035: GIFフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

GIF（Graphics Interchange Format）はアニメーション対応の画像フォーマット。ヘッダ、論理画面記述子、グローバルカラーテーブル、拡張ブロック、画像ブロックで構成されるブロックベースの逐次読み取り構造。

ブロック導入子（0x21=拡張, 0x2C=画像, 0x3B=終端）によるswitchと、サブブロックの繰り返し構造がDSLの良いテストケースとなる。

## 機能要件

### 追加する機能

- [ ] `formats/gif.bdef.yaml` フォーマット定義
  - ヘッダ（GIF87a/GIF89a シグネチャ）
  - 論理画面記述子（幅、高さ、パックドフィールド、背景色、ピクセルアスペクト比）
  - グローバルカラーテーブル（条件付き、サイズは2^(N+1)エントリ）
  - データブロックの繰り返し（拡張ブロック / 画像記述子 / トレーラ）
  - 拡張ブロック: グラフィック制御拡張、コメント拡張、アプリケーション拡張
  - 画像記述子 + ローカルカラーテーブル（条件付き）+ LZWデータ

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- LZW圧縮データの展開
- アニメーションフレームのレンダリング

## 受入条件

1. [ ] `formats/gif.bdef.yaml` が存在すること
2. [ ] GIF87a/GIF89a シグネチャが正しく解析できること
3. [ ] 論理画面記述子から幅・高さが抽出できること
4. [ ] グローバルカラーテーブルが条件付きで解析できること
5. [ ] 拡張ブロックと画像ブロックが識別・解析できること
6. [ ] 有効なGIFファイルでデコードエラーにならないこと
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

- [ ] README.md — 対応フォーマットにGIFを追記
- [ ] docs/architecture.md — formats/ ディレクトリに gif.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: GIF
endianness: little
root: gif

structs:
  gif                     — header + logical_screen_descriptor + global_color_table(条件付き) + blocks(repeat: eof)
  logical_screen_desc     — width(u16) + height(u16) + packed(bitfield,1) + bg_color_index(u8) + pixel_aspect_ratio(u8)
  color_table_entry       — red(u8) + green(u8) + blue(u8)
  block                   — introducer(u8) + switch分岐
  graphic_control_ext     — block_size(u8) + packed(bitfield,1) + delay_time(u16) + transparent_color_index(u8) + terminator(u8)
  comment_ext             — sub_blocks(bytes, remaining) ※サブブロック連鎖
  application_ext         — block_size(u8) + app_id(ascii,8) + auth_code(bytes,3) + sub_block_data(bytes, remaining)
  image_descriptor        — left(u16) + top(u16) + width(u16) + height(u16) + packed(bitfield,1)
                            + local_color_table(条件付き) + lzw_min_code_size(u8) + lzw_data(bytes, remaining)
  raw_data                — data(bytes, remaining)
```

### 主要struct

- **gif**: ルート構造体。header(bytes, 6, expected=GIF89a or GIF87a) + logical_screen_desc(struct) + global_color_table(bytes, 条件付き) + blocks(struct block, repeat: eof)。
- **logical_screen_desc**: width(u16) + height(u16) + packed(bitfield, 1バイト。global_color_table_flag:bit7, color_resolution:bits6-4, sort_flag:bit3, size_of_gct:bits2-0) + bg_color_index(u8) + pixel_aspect_ratio(u8)。
- **block**: introducer(u8) + switchでブロック種別を選択。0x21=拡張ブロック、0x2C=画像記述子、0x3B=トレーラ。
- **image_descriptor**: 画像の位置・サイズ・ローカルカラーテーブル情報。LZW最小コードサイズ(u8)の後、圧縮データのサブブロック連鎖が続く。

### switchの使い方

**ブロック導入子による一次分岐:**

```yaml
switch_on: "{introducer}"
cases:
  "0x2C": image_block        # 画像記述子
  "0x21": extension_block    # 拡張ブロック
  "0x3B": trailer            # トレーラ（終端）
default: raw_data
```

**拡張ブロック内のラベルによる二次分岐:**

extension_block内でlabel(u8)を読み、さらにswitchする。

```yaml
switch_on: "{label}"
cases:
  "0xF9": graphic_control_ext
  "0xFE": comment_ext
  "0xFF": application_ext
default: generic_ext
```

### enumの使い方

- **block_introducer**: 0x2C=Image, 0x21=Extension, 0x3B=Trailer
- **extension_label**: 0xF9=Graphic Control, 0xFE=Comment, 0xFF=Application, 0x01=Plain Text
- **disposal_method**: 0=None, 1=Do Not Dispose, 2=Restore to Background, 3=Restore to Previous（graphic_control_extのbitfieldで使用）

### bitfieldの使い方

**logical_screen_descのpacked byte:**

```yaml
- name: packed
  type: bitfield
  size: "1"
  fields:
    - name: global_color_table_flag
      bits: "7"
    - name: color_resolution
      bits: "6:4"
    - name: sort_flag
      bits: "3"
    - name: size_of_global_color_table
      bits: "2:0"
```

**graphic_control_extのpacked byte:**

```yaml
- name: packed
  type: bitfield
  size: "1"
  fields:
    - name: reserved
      bits: "7:5"
    - name: disposal_method
      bits: "4:2"
      enum: disposal_method
    - name: user_input_flag
      bits: "1"
    - name: transparent_color_flag
      bits: "0"
```

### 繰り返しの方式

- **blocks**: `repeat: eof` でファイル末尾までブロックを繰り返す。トレーラ(0x3B)到達後、通常はファイル末尾となるため自然終了。
- **global_color_table**: 条件付きフィールド。`if: "{global_color_table_flag == 1}"` で存在判定。サイズは `3 * (1 << (size_of_global_color_table + 1))` だが、DSLでは `<<` 演算子が使えるため `{3 * (1 << (size_of_global_color_table + 1))}` で計算可能。bytes型で一括読み取りする。
- **local_color_table**: image_descriptor内で同様に条件付き。
- **サブブロック連鎖**: GIFのサブブロック（size(u8) + data[size]、size=0で終端）は、理想的にはrepeat_until: "{size == 0}"で対応できるが、サブブロック構造体内のsizeフィールドを参照する必要がある。簡易的にはremainingでraw bytesとして一括読み取りし、サブブロック連鎖の詳細解析はスコープ外とする。

### 注意点・制約

1. **グローバルカラーテーブルのサイズ計算**: `3 * (1 << (size_of_global_color_table + 1))` の式でサイズを算出する。bitfieldのサブフィールド値を式で参照できるか確認が必要。参照できない場合、packedバイトをu8として読み取り、ビット演算 `{(packed & 0x07)}` でサイズ値を抽出する代替策を用いる。
2. **サブブロック連鎖の限界**: サブブロック（size + data の繰り返し、size=0で終端）は、switch内のバウンダリスコープでremainingとして一括読み取りするのが現実的。サブブロック単位の詳細解析は、repeat_untilとelement_sizeの組み合わせが必要になり複雑。
3. **拡張ブロックの構造**: 拡張ブロックはintroducer(0x21) + label(u8) + ブロック本体 + サブブロック連鎖の構造。switchのsizeを指定せず、各case構造体内でremainingまで読み取る形にするか、拡張ブロック全体のサイズが事前に不明なため工夫が必要。switchにsizeを指定しない方式を採用する。
4. **GIF87a vs GIF89a**: ヘッダの6バイトでバージョン判別可能。GIF87aは拡張ブロックを持たないが、DSL定義としては統一的に扱い、拡張ブロックが存在しなければ自然にスキップされる。
5. **トレーラ後のデータ**: 一部のGIFファイルはトレーラ後に余分なデータを持つことがある。repeat: eofで読み続けるとエラーになる可能性があるが、実用上は問題にならないことが多い。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
