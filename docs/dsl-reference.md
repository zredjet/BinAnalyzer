# DSLリファレンス

BinAnalyzerのフォーマット定義は `.bdef.yaml` 拡張子のYAMLファイルを使用します。

> **エディタ補完**: `schemas/bdef.schema.json` にJSON Schemaが提供されています。VS Code + YAML拡張で自動的にオートコンプリートとバリデーションが有効になります。

## トップレベル構造

```yaml
name: フォーマット名
endianness: big  # または "little" / "le" / "be"（デフォルト: big）
root: 構造体名

imports:
  - path: common.bdef.yaml  # 他ファイルの定義をインポート

enums:
  列挙型名:
    - value: 0
      label: ラベル名
      description: "説明（任意）"

flags:
  フラグ名:
    bit_size: 32
    fields:
      - name: フィールド名
        bit: 5
        set: "セット時の意味"
        clear: "クリア時の意味"

structs:
  構造体名:
    - name: フィールド名
      type: uint32
```

## フィールド型

| 型 | サイズ | 説明 |
|------|------|-------------|
| `uint8` / `u8` | 1バイト | 符号なし8ビット整数 |
| `uint16` / `u16` | 2バイト | 符号なし16ビット整数 |
| `uint32` / `u32` | 4バイト | 符号なし32ビット整数 |
| `uint64` / `u64` | 8バイト | 符号なし64ビット整数 |
| `int8` / `i8` | 1バイト | 符号付き8ビット整数 |
| `int16` / `i16` | 2バイト | 符号付き16ビット整数 |
| `int32` / `i32` | 4バイト | 符号付き32ビット整数 |
| `int64` / `i64` | 8バイト | 符号付き64ビット整数 |
| `float32` / `f32` | 4バイト | IEEE 754 単精度浮動小数点数 |
| `float64` / `f64` | 8バイト | IEEE 754 倍精度浮動小数点数 |
| `bytes` | 可変 | 生バイト列 |
| `ascii` | 可変 | ASCII文字列（サイズ指定必須） |
| `utf8` | 可変 | UTF-8文字列（サイズ指定必須） |
| `asciiz` | 可変 | ヌル終端ASCII文字列（サイズ指定不要） |
| `utf8z` | 可変 | ヌル終端UTF-8文字列（サイズ指定不要） |
| `zlib` | 可変 | zlib圧縮データ（展開して解析） |
| `deflate` | 可変 | deflate圧縮データ（展開して解析） |
| `utf16le` / `utf16-le` | 可変 | UTF-16LE文字列（サイズ指定必須） |
| `utf16be` / `utf16-be` | 可変 | UTF-16BE文字列（サイズ指定必須） |
| `sjis` / `shift_jis` / `shift-jis` | 可変 | Shift_JIS文字列（サイズ指定必須） |
| `latin1` / `iso-8859-1` | 可変 | Latin-1文字列（サイズ指定必須） |
| `uleb128` / `leb128u` | 可変 | 符号なしLEB128可変長整数（サイズ指定不要） |
| `sleb128` / `leb128s` | 可変 | 符号付きLEB128可変長整数（サイズ指定不要） |
| `vlq` | 可変 | VLQ（Variable-Length Quantity）可変長整数（サイズ指定不要） |
| `bitfield` | 可変 | ビットフィールド（個々のビット範囲を抽出） |
| `struct` | 可変 | ネスト構造体参照 |
| `switch` | 可変 | 条件付き構造体選択 |
| `virtual` | 0バイト | 計算フィールド（バイナリデータを読み取らず、式の評価結果を表示） |

## サイズ指定

可変サイズのフィールド（`bytes`, `ascii`, `utf8`, `utf16le`, `utf16be`, `sjis`, `latin1`, `bitfield`, `zlib`, `deflate`）にはサイズ指定が必要です:

```yaml
# 固定サイズ
- name: data
  type: bytes
  size: "8"

# 他フィールドを参照する式
- name: data
  type: bytes
  size: "{length}"

# 算術式
- name: payload
  type: bytes
  size: "{length - 4}"

# 現在のスコープ内の残りバイト数
- name: rest
  type: bytes
  size: remaining
```

## 式

式は `{...}` で囲み、以下をサポートします:

- **フィールド参照**: `{field_name}` — デコード済みフィールドの値
- **整数リテラル**: `{42}`, `{0xFF}`
- **文字列リテラル**: `{'IHDR'}`
- **算術演算**: `+`, `-`, `*`, `/`, `%`
- **比較演算**: `==`, `!=`, `<`, `<=`, `>`, `>=`
- **論理演算**: `and`, `or`, `not`
- **ビット演算**: `&`（AND）, `|`（OR）, `^`（XOR）, `<<`（左シフト）, `>>`（右シフト）
- **三項演算子**: `{condition ? true_value : false_value}` — 条件分岐式
- **配列インデックス**: `{array_field[index]}` — 配列要素へのアクセス
- **括弧**: `{(a + b) * c}`
- **関数呼び出し**: `{func_name(arg1, arg2, ...)}` — 組み込み関数の呼び出し

### 演算子優先順位（低い順）

| 優先度 | 演算子 |
|--------|--------|
| 0（最低） | `? :`（三項演算子） |
| 1 | `or` |
| 2 | `and` |
| 3 | `\|`（ビットOR） |
| 4 | `^`（ビットXOR） |
| 5 | `&`（ビットAND） |
| 6 | `==`, `!=`, `<`, `<=`, `>`, `>=` |
| 7 | `<<`, `>>` |
| 8 | `+`, `-` |
| 9 | `*`, `/`, `%` |
| 10（最高） | `-`（単項）, `not` |

三項演算子は右結合です。ネストも可能です: `{a ? b : c ? d : e}` は `{a ? b : (c ? d : e)}` と解釈されます。

### 組み込み関数

式内で関数呼び出し構文 `func_name(arg1, arg2, ...)` が使えます。

#### `until_marker(byte1, byte2, ...)`

指定したバイト列（マーカー）が現在位置以降で最初に出現する位置までのバイト数を返します。マーカーベースのストリーム終了検出に使用します。

```yaml
# JPEGエントロピーデータ: EOIマーカー(0xFF 0xD9)の直前まで
- name: compressed_data
  type: bytes
  size: "{until_marker(0xFF, 0xD9)}"
```

- 引数: 検索するバイト値を1つ以上カンマ区切りで指定（例: `0xFF, 0xD9`）
- 戻り値: 現在位置からマーカー先頭までのバイト数
- マーカーが見つからない場合: 現在のスコープ内の残りバイト数を返す（エラーにしない）
- マーカー自体はフィールドに含まれない（直前まで）
- 引数が0個の場合はエラー

#### `parse_int(field_name, base)`

ASCII/UTF-8文字列フィールドの値を指定した基数で整数に変換します。テキストエンコーディングされた数値フィールドの解析に使用します。

```yaml
# TARファイルサイズ（8進数ASCII文字列）
- name: file_size_octal
  type: ascii
  size: "12"
- name: file_size
  type: virtual
  value: "{parse_int(file_size_octal, 8)}"
```

- 第1引数: デコード済みの文字列フィールド名
- 第2引数: 基数（2, 8, 10, 16）
- ヌル終端文字（`\0`）と末尾の空白は自動的にトリミング
- 変換に失敗した場合は0を返す（エラーにしない）

## 列挙型（Enum）

整数値をラベルにマッピングします:

```yaml
enums:
  color_type:
    - value: 0
      label: grayscale
    - value: 2
      label: truecolor
      description: "RGBカラー"
```

フィールドからの参照:

```yaml
- name: color_type
  type: uint8
  enum: color_type
```

## フラグ

ビットレベルのフラグを定義します:

```yaml
flags:
  chunk_type_flags:
    bit_size: 32
    fields:
      - name: ancillary
        bit: 5            # ビット位置
        bit_size: 1       # ビット幅（デフォルト: 1、省略可）
        set: "yes"        # ビットがセットされている場合の表示
        clear: "no"       # ビットがクリアされている場合の表示
```

フラグはASCIIフィールドにも適用できます（例: PNGチャンクタイプ）:

```yaml
- name: type
  type: ascii
  size: "4"
  flags: chunk_type_flags
```

## 繰り返しモード

### データ末尾まで繰り返し

```yaml
- name: chunks
  type: struct
  struct: chunk
  repeat: eof
```

### N回繰り返し

```yaml
- name: items
  type: struct
  struct: item
  repeat_count: "{count}"
```

### 条件成立まで繰り返し

```yaml
- name: entries
  type: struct
  struct: entry
  repeat_until: "{type == 0}"
```

### 条件が真の間繰り返し

```yaml
- name: entries
  type: struct
  struct: entry
  repeat: while
  repeat_while: "{remaining >= 4}"
```

条件は各イテレーション開始**前**に評価されます。条件が偽になった時点で繰り返しを終了し、その要素はデコードしません。`repeat_until` との違いは以下の通りです:

- `repeat_until`: 要素をデコードした**後**に条件評価。条件が真になった要素も配列に含まれる
- `repeat_while`: 要素をデコードする**前**に条件評価。条件が偽なら要素をデコードしない

条件が最初から偽の場合、空配列（要素0個）が返ります。

`repeat_while:` は単独でも使用でき、その場合 `repeat: while` は省略可能です:

```yaml
- name: entries
  type: struct
  struct: entry
  repeat_while: "{remaining >= 4}"
```

### 長さプレフィックス付き繰り返し（length_prefixed）

各要素の先頭Nバイトがデータサイズを示し、サイズ0で繰り返しが終了するパターンです。GIFのサブブロックなど、多くのバイナリフォーマットで使われます。

```yaml
- name: sub_blocks
  type: bytes
  repeat: length_prefixed
  length_prefix_size: 1   # プレフィックスのバイト数（デフォルト: 1、1〜4対応）
```

動作:
1. プレフィックス（Nバイト）を読み取る
2. プレフィックスの値が0なら繰り返し終了（終端マーカーは配列に含まれない）
3. プレフィックスの値分のバイト列を読み取り、配列要素として追加
4. 1に戻る

- `length_prefix_size` はプレフィックスのバイト数（1〜4、省略時は1）
- プレフィックスのエンディアンは現在のスコープに従う
- 各要素は `bytes` 型としてデコードされる（サイズはプレフィックスから自動決定）
- `size` 指定は不要（プレフィックスがサイズを決定するため）
- データ末尾（EOF）に達した場合も安全に終了する

```
# GIF sub-block構造の例:
[size: 1B][data: size B][size: 1B][data: size B]...[0x00]
```

### 要素サイズ指定（element_size）

繰り返しフィールドで `element_size` を指定すると、各要素のデコード時にバウンダリスコープが設定されます。要素のフィールドが `element_size` より小さい場合、残りバイトは自動的にスキップされます。

```yaml
# 固定サイズレコードの繰り返し（各要素40バイト）
- name: sections
  type: struct
  struct: section_header
  repeat_count: "{num_sections}"
  element_size: "{section_size}"
```

- 固定値（`element_size: "40"`）と式（`element_size: "{entry_size}"`）の両方に対応
- 要素のデコード後、`element_size` の末尾まで未読バイトを自動スキップ
- 要素のデコードが `element_size` を超えた場合はデコードエラー
- `repeat`, `repeat_count`, `repeat_until` のいずれとも組み合わせ可能

### 繰り返し内の特殊変数（`_index`）

繰り返しブロック内では `_index` 変数が自動的に設定されます。現在のイテレーションインデックス（0始まり）を保持します。

```yaml
- name: entries
  type: struct
  struct: entry
  repeat_count: "{count}"
  seek: "{offsets[_index]}"    # _index番目の要素のオフセットにseek
  seek_restore: true
```

### 配列インデックスアクセス

繰り返しフィールドのデコード済み値に `[index]` でアクセスできます。`_index` と組み合わせることで、ポインタテーブルに基づく要素ごとのseekが可能です。

```yaml
# ポインタテーブルを先に読み取り
- name: cell_pointers
  type: uint16
  repeat_count: "{num_cells}"

# 各ポインタが指す位置のデータを読み取り
- name: cells
  type: struct
  struct: cell_data
  repeat_count: "{num_cells}"
  seek: "{cell_pointers[_index]}"
  seek_restore: true
```

- インデックスには整数リテラル、フィールド参照、任意の式が使用可能
- 配列範囲外のインデックスはエラー

### 兄弟スコープ参照

繰り返し（`repeat`/`repeat_count`/`repeat: eof` 等）内で構造体をデコードすると、各要素のスカラーフィールド値（整数、文字列、浮動小数点、virtual、bitfieldサブフィールド）が自動的に親スコープに昇格されます。これにより、前の要素でデコードした値を後続の要素から参照できます。

```yaml
# AVIの例: strlリスト内のチャンク繰り返し
- name: sub_chunks
  type: struct
  struct: riff_chunk
  repeat: eof
  # strh チャンクの fccType ("vids"/"auds") が親スコープに昇格
  # → 続く strf チャンク内の switch で {fccType} として参照可能

# strf 内の switch
- name: format_data
  type: switch
  switch_on: "{fccType}"       # 兄弟 strh の fccType を参照
  cases:
    "'vids'": bitmap_info_header
    "'auds'": wave_format_ex
```

- 新しい DSL 構文は不要 — 既存の `{variable_name}` 参照をそのまま利用
- 同名のフィールドは後続の要素で上書き（通常の変数セマンティクス）

## Switch（条件分岐構造体）

フィールド値に基づいて構造体を選択します:

```yaml
- name: data
  type: switch
  size: "{length}"
  switch_on: "{type}"
  cases:
    "'IHDR'": ihdr
    "'tEXt'": text_chunk
  default: raw_data
```

### バウンダリスコープ

`size` を指定すると、switch 内部にバウンダリスコープが設定されます。選択された構造体はそのサイズ範囲内でデコードされ、構造体内の `size: remaining` はスコープ内の残りバイト数を参照します。

### seek との組み合わせ

`seek` と `size` を組み合わせることで、オフセット先のデータを型に応じて構造体選択できます。`seek_restore: true` を指定すると、デコード後に元の位置に復帰します。

```yaml
# ICCプロファイルのタグデータ参照（offset先のデータをsignatureで分岐）
- name: data
  type: switch
  size: "{size}"
  seek: "{offset}"
  seek_restore: true
  switch_on: "{signature}"
  cases:
    "'desc'": desc_tag_data
    "'XYZ '": xyz_tag_data
  default: raw_data
```

## 条件フィールド

`if` を指定すると、式の評価結果が真の場合のみフィールドをデコードします。偽の場合はスキップされます。

```yaml
- name: version
  type: uint8
- name: extra_data
  type: uint16
  if: "{version >= 2}"      # version が2以上の場合のみデコード
```

## チェックサム

整数フィールドに `checksum` を指定すると、指定フィールドのバイト列からチェックサムを計算し、フィールド値と照合します。

```yaml
- name: type
  type: ascii
  size: "4"
- name: data
  type: bytes
  size: "{length}"
- name: crc
  type: uint32
  checksum:
    algorithm: crc32        # 現在はCRC-32のみ対応
    fields: [type, data]    # チェックサム計算対象のフィールド名リスト
```

出力にはチェックマーク（✓）またはバツ印（✗）と期待値が表示されます。

## 可変長整数（LEB128 / VLQ）

`uleb128`、`sleb128`、`vlq` は可変長エンコーディングされた整数です。サイズ指定は不要で、バイト列のMSB（最上位ビット）を見て自動的に終端を検出します。

```yaml
# WASM section size（符号なしLEB128）
- name: section_size
  type: uleb128

# 符号付きLEB128
- name: offset
  type: sleb128

# MIDI delta time（VLQ）
- name: delta_time
  type: vlq
```

### エンコーディング仕様

| 型 | エンコーディング | バイト順序 | 用途 |
|------|------|------|------|
| `uleb128` | 各バイトの下位7bitをリトルエンディアン順にシフト結合。MSB=0で終了 | リトルエンディアン | WASM, DWARF, Android DEX |
| `sleb128` | uleb128と同様 + 最終バイトのbit6で符号拡張 | リトルエンディアン | WASM（符号付き値）, DWARF |
| `vlq` | 各バイトの下位7bitをビッグエンディアン順にシフト結合。MSB=0で終了 | ビッグエンディアン | MIDI, Git packfile |

- 最大10バイト（64ビット値に対応）。超過時はデコードエラー
- 整数型として扱われるため、`enum` 参照や `string_table` 参照、式での変数参照が可能

## ビットフィールド

`bitfield` 型は固定バイト列（1〜8バイト）から個々のビット範囲を抽出します。`fields` にビット範囲とenumの対応を定義します。

```yaml
- name: flags
  type: bitfield
  size: "1"
  fields:
    - name: reserved
      bits: "7:4"           # ビット7〜4（範囲指定）
    - name: flag_a
      bits: "3"             # ビット3のみ（単一ビット）
    - name: mode
      bits: "1:0"           # ビット1〜0
      enum: mode_type       # enumラベルの適用（任意）
```

ビット範囲は `"高:低"` 形式で、単一ビットの場合はビット番号のみ指定します。

## バリデーション

バイトフィールドに期待値を指定できます:

```yaml
- name: signature
  type: bytes
  size: "8"
  expected: [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
```

出力にはバリデーション結果としてチェックマーク（✓）またはバツ印（✗）が表示されます。

## 説明テキスト

任意のフィールドに `description` を指定できます。フォーマット定義の自己文書化に利用します。

```yaml
- name: magic
  type: bytes
  size: "8"
  description: "PNGファイルのマジックバイト"
```

## ヌル終端文字列

`asciiz` と `utf8z` はヌル文字（`0x00`）で終端される文字列です。サイズ指定は不要で、ヌル文字まで自動的に読み取ります。

```yaml
- name: filename
  type: asciiz

- name: description
  type: utf8z
```

## 圧縮データ

`zlib` と `deflate` は圧縮データを展開します。`struct` を指定すると、展開後のデータを構造体としてネスト解析できます。

```yaml
- name: compressed_data
  type: zlib
  size: "{compressed_size}"
  struct: inner_format      # 展開後のデータを構造体として解析

- name: raw_compressed
  type: deflate
  size: remaining           # structを省略すると生バイト列として出力
```

`struct` を省略した場合、展開後の生バイト列が出力されます。

## インポート

他のフォーマット定義ファイルから構造体、列挙型、フラグの定義をインポートできます。共通定義の再利用に便利です。

```yaml
name: MyFormat
root: main
imports:
  - path: common_types.bdef.yaml
  - path: enums/colors.bdef.yaml
structs:
  main:
    - name: header
      type: struct
      struct: common_header  # common_types.bdef.yamlで定義
```

- パスはインポート元ファイルからの相対パス
- インポートは再帰的に解決（推移的インポート対応）
- 循環インポートはエラー
- 同名の定義が複数ファイルに存在する場合はエラー（名前衝突禁止）

## 計算フィールド（Virtual）

`virtual` 型はバイナリデータを読み取らず、式の評価結果を表示する計算フィールドです。サイズは常に0バイトです。

```yaml
- name: pixel_count
  type: virtual
  value: "{width * height}"
  description: "総ピクセル数"

- name: compression_ratio
  type: virtual
  value: "{compressed_size * 100 / original_size}"
```

- `value` プロパティで式を指定（必須）
- 既存の式システム（フィールド参照、算術、比較等）をそのまま利用可能
- ツリー出力では `= 値` 形式で表示され、計算値であることが視覚的に区別できる
- `if` と組み合わせて条件付き計算フィールドを定義可能

### 条件付き virtual フィールド

```yaml
# TIFFのIFDエントリ: 型とカウントに応じたインライン値解釈
- name: is_inline
  type: virtual
  value: "{(field_type == 3 and count <= 2) or (field_type == 4 and count == 1)}"
  description: "値がインラインかどうか"
- name: inline_short_value
  type: virtual
  value: "{value_offset & 0xFFFF}"
  if: "{field_type == 3 and count == 1}"
  description: "SHORT型のインライン値"
```

**注意**: `or`/`and` を含む論理式は `bool` 型を返します（`long` ではない）。テスト等で評価結果を比較する際は型に注意してください。

## オフセットジャンプ（seek）

`seek` を指定すると、フィールドのデコード前に指定された絶対オフセットにジャンプします。ポインタベースのフォーマット（PE/ELF/ZIP/TIFF等）の解析に有用です。

### 基本的な使い方

```yaml
# 固定オフセットにジャンプして読み取り
- name: pe_header
  type: struct
  struct: pe_header
  seek: "0x3C"
```

### フィールド値によるオフセット指定

```yaml
- name: e_lfanew
  type: uint32
  description: "PEヘッダへのオフセット"
- name: pe_signature
  type: uint32
  seek: "{e_lfanew}"
```

### 算術式によるオフセット指定

```yaml
- name: header_offset
  type: uint32
- name: data
  type: bytes
  size: "16"
  seek: "{header_offset + 4}"
```

### seek_restore（位置復帰）

`seek_restore: true` を指定すると、seekしたフィールドのデコード後に元の読み取り位置に復帰します。これにより、シーケンシャルな読み取りを中断せずにポインタ先のデータを参照できます。

```yaml
- name: name_offset
  type: uint32
- name: name
  type: asciiz
  seek: "{name_offset}"
  seek_restore: true       # デコード後、name_offsetの次の位置に復帰
- name: next_field
  type: uint32             # name_offset直後から続けて読み取り
```

- `seek` は絶対オフセット（ファイル先頭からのバイト位置）を式で指定
- `seek_restore` は `seek` と組み合わせて使用（`seek` なしで `seek_restore` を指定するとバリデーションエラー VAL011）
- `seek` は `if`（条件フィールド）と組み合わせ可能
- `seek` は `repeat`（繰り返し）と組み合わせ可能

## エンディアン切り替え

構造体レベルまたはフィールドレベルでエンディアンを上書きできます。優先順位: フィールド > 構造体 > トップレベル。

### 構造体レベル（静的）

```yaml
structs:
  le_header:
    endianness: little
    fields:
      - name: magic
        type: uint32
      - name: version
        type: uint16
```

構造体をオブジェクト形式（`endianness`/`align`/`fields` キー）で定義します。旧形式（フィールドリスト直接）との混在も可能です。

### 構造体レベル（動的）

構造体の `endianness` に式を指定すると、デコード時に動的にエンディアンを決定できます。式は `'little'` または `'big'` を返す必要があります。

```yaml
structs:
  tiff:
    - name: byte_order
      type: ascii
      size: "2"
    - name: body
      type: struct
      struct: tiff_body

  tiff_body:
    endianness: "{byte_order == 'II' ? 'little' : 'big'}"
    fields:
      - name: magic
        type: uint16
```

三項演算子と組み合わせて、先行フィールドの値に基づくエンディアン切り替えが可能です。動的エンディアンは子構造体にも継承されます。

### フィールドレベル

```yaml
structs:
  mixed:
    - name: be_value
      type: uint32
    - name: le_value
      type: uint32
      endianness: little
```

### ネスト構造体の継承

子構造体にエンディアン指定がない場合、親構造体のエンディアンを継承します。

```yaml
structs:
  parent:
    endianness: little
    fields:
      - name: child
        type: struct
        struct: child_struct
  child_struct:
    # endianness 未指定 → parent の little を継承
    - name: value
      type: uint16
```

## 文字列テーブル参照（string_table）

ELF `.strtab` のような文字列テーブルを定義し、整数フィールドからオフセットで文字列を参照できます。

### 文字列テーブルの定義

構造体に `string_table: true` を指定すると、そのデコード結果のバイト列が文字列テーブルとして登録されます。

```yaml
structs:
  strtab:
    string_table: true
    fields:
      - name: data
        type: bytes
        size: remaining
```

### 文字列テーブルの参照

整数フィールドに `string_table` で参照先テーブル名を指定すると、整数値をオフセットとして文字列を解決します。

```yaml
- name: name_offset
  type: uint32
  string_table: strtab   # strtab テーブルの offset 位置の文字列を解決
```

- 文字列テーブルはヌル終端文字列（ASCII）として解釈
- テーブルは参照元より前にデコードされている必要あり
- 出力には `→ "解決済み文字列"` が表示される

## カスタムバリデーション（validate）

フィールドに `validate` を指定すると、デコード後に式を評価し、結果を ✓/✗ で表示します。

```yaml
- name: magic
  type: uint16
  validate: "{magic == 42}"

- name: byte_order
  type: ascii
  size: "2"
  validate: "{byte_order == 'II'}"
```

- 式はフィールドのデコード後に評価される（フィールド自身の値を参照可能）
- 既存の式システム（比較、算術、論理演算）をそのまま利用可能
- ツリー出力では ✓（緑）/ ✗（赤）で表示
- JSON出力では `_validation` オブジェクト（`passed`, `expression`）

## アライメントとパディング

### フィールドレベルアライメント

フィールドの `align` を指定すると、そのフィールドのデコード後に次のフィールドの開始位置が指定バイト境界にアラインされます。

```yaml
- name: tag
  type: uint8
  align: 4       # デコード後、次フィールドを4バイト境界に揃える
- name: value
  type: uint32   # 4バイト境界から開始
```

### 構造体レベルアライメント

構造体定義（IR）に `align` を指定すると、繰り返し（`repeat`）時に各要素の開始位置がアラインされます（最初の要素は除く）。

```yaml
# 例: 各エントリを512バイト境界にアラインするテーブル
- name: entries
  type: struct
  struct: entry       # entry構造体のAlignが設定されている場合、
  repeat_count: "{n}" # 2番目以降の要素が境界に揃えられる
```

### パディングフィールド

`padding: true` を指定すると、そのフィールドはツリー出力で非表示になります。予約領域やアライメント用のダミーバイトに使用します。

```yaml
- name: reserved
  type: bytes
  size: "3"
  padding: true   # ツリー出力で非表示
```
