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
- **括弧**: `{(a + b) * c}`

### 演算子優先順位（低い順）

| 優先度 | 演算子 |
|--------|--------|
| 1（最低） | `or` |
| 2 | `and` |
| 3 | `\|`（ビットOR） |
| 4 | `^`（ビットXOR） |
| 5 | `&`（ビットAND） |
| 6 | `==`, `!=`, `<`, `<=`, `>`, `>=` |
| 7 | `<<`, `>>` |
| 8 | `+`, `-` |
| 9 | `*`, `/`, `%` |
| 10（最高） | `-`（単項）, `not` |

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

## ビットフィールド

`bitfield` 型は固定バイト列から個々のビット範囲を抽出します。`fields` にビット範囲とenumの対応を定義します。

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

### 構造体レベル

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
