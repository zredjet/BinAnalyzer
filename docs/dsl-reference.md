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
| `gzip` | 可変 | gzip圧縮データ（展開して解析） |
| `bzip2` | 可変 | bzip2圧縮データ（展開して解析） |
| `lzma` | 可変 | LZMA圧縮データ（展開して解析、raw LZMA形式） |
| `zstd` / `zstandard` | 可変 | Zstandard圧縮データ（展開して解析） |
| `lz4` | 可変 | LZ4圧縮データ（展開して解析、LZ4 Frame Format） |
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

可変サイズのフィールド（`bytes`, `ascii`, `utf8`, `utf16le`, `utf16be`, `sjis`, `latin1`, `bitfield`, `zlib`, `deflate`, `gzip`, `bzip2`, `lzma`, `zstd`, `lz4`）にはサイズ指定が必要です:

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
- **メンバーアクセス**: `{struct.field}`, `{array[i].field}` — 構造体メンバーへのドットアクセス
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

#### `len(array_field)`

配列フィールドの要素数を返します。

```yaml
# 配列の要素数を後続フィールドで参照
- name: entries
  type: uint16
  repeat_count: "{num_entries}"
- name: last_index
  type: virtual
  value: "{len(entries) - 1}"
```

- 引数: 配列フィールド名（1つ）
- 戻り値: 配列の要素数（整数）
- 空配列の場合は0を返す
- 非配列フィールドを指定した場合はエラー

#### `count(array_field)`

`len()` のエイリアスです。可読性のために使い分けられます。

#### `min(array_field)`

数値配列フィールドの最小値を返します。

```yaml
- name: sizes
  type: uint32
  repeat_count: "{count}"
- name: min_size
  type: virtual
  value: "{min(sizes)}"
```

- 引数: 数値配列フィールド名（1つ）
- 戻り値: 配列内の最小値（整数）
- 空配列の場合はエラー
- 非数値配列の場合はエラー

#### `max(array_field)`

数値配列フィールドの最大値を返します。

```yaml
- name: sizes
  type: uint32
  repeat_count: "{count}"
- name: max_size
  type: virtual
  value: "{max(sizes)}"
```

- 引数: 数値配列フィールド名（1つ）
- 戻り値: 配列内の最大値（整数）
- 空配列の場合はエラー
- 非数値配列の場合はエラー

#### `sum(array_field)`

数値配列フィールドの合計値を返します。

```yaml
- name: sizes
  type: uint32
  repeat_count: "{count}"
- name: total_size
  type: virtual
  value: "{sum(sizes)}"
```

- 引数: 数値配列フィールド名（1つ）
- 戻り値: 配列内の合計値（整数）
- 空配列の場合は0を返す
- 非数値配列の場合はエラー

#### `substr(string_field, start, length)`

文字列フィールドから部分文字列を抽出します。マジック文字列の先頭一致判定などに使用します。

```yaml
# マジック文字列の先頭4文字を抽出して判定
- name: magic
  type: ascii
  size: "8"
- name: magic_prefix
  type: virtual
  value: "{substr(magic, 0, 4)}"
```

- 第1引数: 文字列フィールド名
- 第2引数: 開始位置（0始まり）
- 第3引数: 抽出する文字数
- 戻り値: 部分文字列（string）
- 開始位置が文字列長以上の場合は空文字列を返す
- 長さが残り文字数を超える場合は末尾までを返す（クランプ）
- 非文字列フィールドを指定した場合はエラー

#### `concat(value1, value2, ...)`

複数の値を文字列として結合します。数値は自動的に文字列に変換されます。

```yaml
# バージョン文字列の構築
- name: major
  type: uint8
- name: minor
  type: uint8
- name: version_str
  type: virtual
  value: "{concat(major, '.', minor)}"
```

- 引数: 2つ以上（可変長）
- 各引数を文字列に変換して結合
- 数値、文字列リテラル、フィールド参照を混在可能
- 戻り値: 結合された文字列（string）

#### `contains(string_field, search)`

文字列フィールドに指定した文字列が含まれるかを判定します。条件分岐での使用を想定しています。

```yaml
# 文字列フィールドに特定のパターンが含まれるか判定
- name: description
  type: ascii
  size: "{desc_length}"
- name: is_compressed
  type: virtual
  value: "{contains(description, 'compressed')}"
```

- 第1引数: 文字列フィールド名
- 第2引数: 検索文字列（文字列リテラルまたは文字列フィールド）
- 戻り値: 真偽値（bool）— 含む場合 true、含まない場合 false
- 非文字列フィールドを指定した場合はエラー

#### `hex(value)`

整数値を `"0x"` プレフィックス付き16進文字列に変換します。識別子やマジックナンバーの表示に使用します。

```yaml
# 整数値を16進表示
- name: magic
  type: uint32
- name: magic_hex
  type: virtual
  value: "{hex(magic)}"
```

- 引数: 整数値（1つ）
- 戻り値: 文字列（`"0xFF"` 形式、大文字）
- `hex(0)` → `"0x0"`

#### `upper(str)`

文字列を大文字に変換します。

```yaml
- name: fourcc
  type: ascii
  size: "4"
- name: fourcc_upper
  type: virtual
  value: "{upper(fourcc)}"
```

- 引数: 文字列（1つ）
- 戻り値: 大文字変換された文字列
- 非文字列引数を指定した場合はエラー

#### `lower(str)`

文字列を小文字に変換します。

```yaml
- name: fourcc
  type: ascii
  size: "4"
- name: fourcc_lower
  type: virtual
  value: "{lower(fourcc)}"
```

- 引数: 文字列（1つ）
- 戻り値: 小文字変換された文字列
- 非文字列引数を指定した場合はエラー

#### `trim(str)`

文字列の先頭・末尾の空白文字とヌル文字を除去します。パディング付き固定長文字列の処理に使用します。

```yaml
- name: name_raw
  type: ascii
  size: "32"
- name: name
  type: virtual
  value: "{trim(name_raw)}"
```

- 引数: 文字列（1つ）
- 戻り値: 空白（スペース、タブ、CR、LF）とヌル文字を除去した文字列
- 非文字列引数を指定した場合はエラー

#### `popcount(value)`

整数値のセットされたビットの数（ポピュレーションカウント）を返します。フラグフィールドの有効フラグ数カウントに使用します。

```yaml
- name: flags
  type: uint32
- name: num_flags_set
  type: virtual
  value: "{popcount(flags)}"
```

- 引数: 整数値（1つ）
- 戻り値: セットされたビットの数（整数）
- `popcount(0)` → `0`, `popcount(0xFF)` → `8`

#### `abs(value)`

整数の絶対値を返します。

```yaml
- name: height
  type: int32
- name: actual_height
  type: virtual
  value: "{abs(height)}"
```

- 引数: 整数値（1つ）
- 戻り値: 絶対値（整数）
- `abs(0)` → `0`, `abs(-42)` → `42`, `abs(42)` → `42`

#### `log2(value)`

正整数の2を底とする対数（整数部）を返します。ページサイズのべき乗検証やアライメント計算に使用します。

```yaml
- name: page_size
  type: uint32
- name: page_bits
  type: virtual
  value: "{log2(page_size)}"
```

- 引数: 正整数（1つ）
- 戻り値: floor(log2(value))（整数）
- `log2(1)` → `0`, `log2(256)` → `8`
- 0以下の値を指定した場合はエラー

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

整数型フィールド（uint8/uint16/uint32/uint64等）にもフラグを適用できます。
この場合、デコード結果は各ビットの状態がツリー表示されます:

```yaml
flags:
  p_flags:
    bit_size: 32
    fields:
      - name: PF_X
        bit: 0
      - name: PF_W
        bit: 1
      - name: PF_R
        bit: 2

structs:
  phdr:
    - name: p_flags
      type: uint32
      flags: p_flags
```

式からの変数参照は整数値として解決されるため、ビット演算も利用できます:

```yaml
- name: is_executable
  type: virtual
  value: "{p_flags & 1}"
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

### 配列全体のサイズ指定（size + repeat）

繰り返しフィールドに `size` を指定すると、配列全体のバウンダリスコープが作成されます。繰り返しは指定サイズの範囲内で行われ、デコード後はスコープ終端まで位置が進みます。

```yaml
# 指定サイズ内でEOFまで繰り返し（MIDI トラックデータ等）
- name: events
  type: struct
  struct: midi_event
  size: "{length}"
  repeat: eof

# 式によるサイズ指定 + repeat_count
- name: entries
  type: struct
  struct: entry
  size: "{data_size}"
  repeat_count: "{count}"
```

- `size` は配列全体の境界を設定（各要素ではなく配列全体に適用）
- `element_size` は各要素の境界を設定（既存機能）
- 両方を同時に指定可能（直交する概念）
- `repeat: eof` の場合、`size` 境界内の EOF で繰り返し終了
- `repeat_while` の場合、`remaining` は `size` 境界内の残りバイト数を参照
- 繰り返し終了後、`size` の末尾まで未読バイトを自動スキップ

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

### 繰り返し内の特殊変数（`_prev`）

繰り返しブロック内では `_prev` 変数が自動的に設定されます。
直前のイテレーションでデコードされた要素の値を保持します。

- スカラー要素の場合: `{_prev}` で直接値を取得
- 構造体要素の場合: `{_prev.field_name}` でメンバーアクセス
- 最初の要素（`_index == 0`）では未定義 — `if: "{_index > 0}"` でガードしてください

```yaml
# 前要素の値に基づく条件分岐
- name: records
  type: struct
  struct: record
  repeat: eof

# record 構造体内:
- name: value
  type: uint8
- name: delta
  type: virtual
  value: "{_prev.value}"
  if: "{_index > 0}"
```

### 繰り返しのガード条件（repeat_max / repeat_error_limit）

壊れたファイルや不正なデータにより繰り返し回数が異常に大きくなる場合、メモリ枯渇やハングを防ぐためにガード条件を設定できます。

#### repeat_max — 繰り返し回数上限

```yaml
- name: entries
  type: struct
  struct: entry
  repeat_count: "{num_entries}"
  repeat_max: "1000"           # 最大1000要素で打ち切り
```

- 式で指定可能（`repeat_max: "{max_entries}"`）
- 全繰り返しモード（`repeat_count`, `repeat: eof`, `repeat_until`, `repeat: while`, `repeat: length_prefixed`）に適用
- `repeat_count` ではカウント値がクランプされる。他のモードではループ冒頭で要素数をチェック
- 式はループ前に1回だけ評価される

#### repeat_error_limit — 連続エラー上限

```yaml
- name: chunks
  type: struct
  struct: chunk
  repeat: eof
  repeat_error_limit: "3"      # 連続3回エラーで打ち切り
```

- `on-error: continue` モードでのエラー回復時に有効
- 連続エラーカウンタはデコード成功時にリセットされる
- 式はループ前に1回だけ評価される

#### CLIグローバルデフォルト

`--max-repeat N` オプションでフォーマット定義に `repeat_max` が指定されていない場合のデフォルト上限を設定できます。フィールドレベルの `repeat_max` はCLIオプションより優先されます。

```bash
dotnet run --project src/BinAnalyzer.Cli -- broken.bin -f formats/png.bdef.yaml --max-repeat 10000
```

#### 打ち切り時の出力

ガード条件により打ち切られた配列は、出力に `(truncated)` と理由が表示されます。JSON出力では `"truncated": true` と `"truncation_reason"` プロパティが追加されます。

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

### メンバーアクセス

構造体フィールドのメンバーにドット記法でアクセスできます:

- `{header.width}` — 構造体フィールドのメンバー
- `{entries[_index].offset}` — 構造体配列の要素メンバー
- `{parent.child.value}` — ネスト構造体のメンバー

```yaml
# 構造体テーブルのメンバー参照
- name: sections
  type: struct
  struct: section_header
  repeat_count: "{num_sections}"

# 構造体配列のメンバーをseekに使用
- name: section_data
  type: bytes
  size: "{sections[_index].raw_size}"
  seek: "{sections[_index].raw_offset}"
  repeat_count: "{num_sections}"
  seek_restore: true
```

- 構造体型フィールド、switch型フィールドのデコード結果にメンバーアクセス可能
- 構造体配列の要素にもインデックス＋ドットの組み合わせでアクセス可能
- ドットチェーン（`a.b.c`）でネスト構造体の深いメンバーにもアクセス可能
- 存在しないメンバー名を指定した場合はエラー

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

## テンプレート構造体（パラメータ付きstruct）

構造体定義にパラメータを宣言し、参照時に引数を渡すことで、同じ構造パターンをパラメータ違いで再利用できます。TLV、RIFFチャンク、MP4 Box等の汎用パターンに有用です。

### パラメータ付き定義

構造体名の後に `(param1=default1, param2=default2)` 形式でパラメータを宣言します。デフォルト値を省略すると必須パラメータになります。

```yaml
structs:
  # デフォルト値付きパラメータ
  data_block(prefix_size=1, payload_size=4):
    - name: prefix
      type: bytes
      size: "{prefix_size}"
    - name: payload
      type: bytes
      size: "{payload_size}"

  # 必須パラメータ（デフォルトなし）
  record(size):
    - name: data
      type: bytes
      size: "{size}"

  # 混在（必須 + デフォルト付き）
  chunk(id_size, data_size=4):
    - name: id
      type: bytes
      size: "{id_size}"
    - name: data
      type: bytes
      size: "{data_size}"
```

### 参照時の引数指定

#### 名前付き引数

```yaml
- name: entry
  type: struct
  struct: data_block(prefix_size=2, payload_size=8)
```

#### 位置引数

```yaml
- name: entry
  type: struct
  struct: data_block(2, 8)
```

#### 式引数

フィールドの値を引数として渡す場合は式を使用します:

```yaml
- name: header_size
  type: uint8
- name: entry
  type: struct
  struct: "record(size={header_size})"
```

### デフォルト値の適用

引数を省略した場合、パラメータのデフォルト値が使用されます。全パラメータにデフォルト値がある場合は引数なしで参照できます:

```yaml
- name: entry
  type: struct
  struct: data_block   # prefix_size=1, payload_size=4（デフォルト）
```

デフォルト値のない必須パラメータを省略するとランタイムエラーになります。

### パラメータのスコープ

テンプレートパラメータは構造体内の変数スコープに設定され、式内で `{param_name}` として参照できます。パラメータは親スコープには漏れません。

### 制約

- パラメータのデフォルト値は整数リテラルのみ
- switch/cases 内でのテンプレートパラメータ使用は未対応
- テンプレートの再帰的なインスタンス化は未対応

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

フィールドに `checksum` を指定すると、指定フィールドのバイト列からチェックサムを計算し、フィールド値と照合します。

### 対応アルゴリズム

| アルゴリズム | カテゴリ | 出力サイズ | 対応フィールド型 |
|---|---|---|---|
| `crc32` | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 |
| `crc16-ccitt` | 整数系 | 2 bytes (16-bit) | uint16 等の整数型 |
| `crc16-ibm` | 整数系 | 2 bytes (16-bit) | uint16 等の整数型 |
| `adler32` | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 |
| `crc8` | 整数系 | 1 byte (8-bit) | uint8 等の整数型 |
| `crc8-maxim` | 整数系 | 1 byte (8-bit) | uint8 等の整数型 |
| `crc8-cdma2000` | 整数系 | 1 byte (8-bit) | uint8 等の整数型 |
| `crc64-ecma` | 整数系 | 8 bytes (64-bit) | uint64 |
| `xxhash32` | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 |
| `xxhash64` | 整数系 | 8 bytes (64-bit) | uint64 |
| `fletcher16` | 整数系 | 2 bytes (16-bit) | uint16 等の整数型 |
| `fletcher32` | 整数系 | 4 bytes (32-bit) | uint32 等の整数型 |
| `md5` | ハッシュ系 | 16 bytes | bytes |
| `sha1` | ハッシュ系 | 20 bytes | bytes |
| `sha256` | ハッシュ系 | 32 bytes | bytes |
| `sha384` | ハッシュ系 | 48 bytes | bytes |
| `sha512` | ハッシュ系 | 64 bytes | bytes |

整数系アルゴリズムは整数型フィールドに、ハッシュ系アルゴリズムは bytes 型フィールドに指定します。

### 整数系（CRC-32 / CRC-16 / Adler-32）

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
    algorithm: crc32
    fields: [type, data]    # チェックサム計算対象のフィールド名リスト

- name: header_crc
  type: uint16
  checksum:
    algorithm: crc16-ccitt
    fields: [magic, version, flags]
```

### ハッシュ系（MD5 / SHA-1 / SHA-256）

```yaml
- name: content_hash
  type: bytes
  size: "16"
  checksum:
    algorithm: md5
    fields: [content]

- name: digest
  type: bytes
  size: "32"
  checksum:
    algorithm: sha256
    fields: [header, payload]
```

出力にはチェックマーク（✓）またはバツ印（✗）とアルゴリズム名が表示されます。

### バイト範囲指定

`fields` の代わりに `range` または `ranges` でバイトオフセット+サイズを指定できます。PE/ELF等のバイナリフォーマットでフィールド境界と一致しない範囲のチェックサムを検証する場合に使用します。

```yaml
# 単一範囲: offset=0 から 256 バイト
- name: crc
  type: uint32
  checksum:
    algorithm: crc32
    range:
      offset: "0"
      size: "256"

# 複数範囲の連結
- name: crc
  type: uint32
  checksum:
    algorithm: crc32
    ranges:
      - offset: "0"
        size: "{header_size}"
      - offset: "{data_offset}"
        size: "{data_size}"

# チェックサムフィールド自身をゼロクリアして計算（PE checksum等）
- name: checksum
  type: uint32
  checksum:
    algorithm: crc32
    range:
      offset: "0"
      size: "{file_size}"
    exclude_self: true
```

`offset` と `size` には式（`{変数名}` や `{変数 + 4}` 等）を使用できます。デコード時に評価されます。

**排他制約:**
- `fields` と `range`/`ranges` は同時に指定できません（VAL018）
- `range` と `ranges` は同時に指定できません（VAL019）
- `exclude_self` は `range`/`ranges` 指定時のみ有効です（VAL119: 警告）

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

## ビットストリーム（mode: bitstream）

`mode: bitstream` を構造体に指定すると、その構造体内のフィールドの `size` はビット単位として解釈されます。バイト境界をまたぐ任意ビット幅のフィールドを連続して読み取ることができます。

```yaml
structs:
  streaminfo_bits:
    mode: bitstream
    fields:
      - name: sample_rate
        type: uint32
        size: "20"          # 20ビット
      - name: channels
        type: uint8
        size: "3"           # 3ビット
      - name: bps
        type: uint8
        size: "5"           # 5ビット
      - name: total_samples
        type: uint64
        size: "36"          # 36ビット
```

### ビットオーダー（bit_order）

デフォルトではビットはMSB-first（最上位ビットから読み取り）で処理されますが、`bit_order: lsb` を指定するとLSB-first（最下位ビットから読み取り）に切り替わります。USB、Bluetooth、DEFLATE、GIF (LZW) 等のフォーマットではLSB-firstが必要です。

```yaml
structs:
  # MSB-first（デフォルト）
  h264_nalu:
    mode: bitstream
    fields:
      - name: forbidden_zero_bit
        type: uint8
        size: "1"
      - name: nal_ref_idc
        type: uint8
        size: "2"
      - name: nal_unit_type
        type: uint8
        size: "5"

  # LSB-first
  usb_packet:
    mode: bitstream
    bit_order: lsb
    fields:
      - name: pid
        type: uint8
        size: "4"
      - name: addr
        type: uint8
        size: "7"
      - name: endp
        type: uint8
        size: "4"

  # 明示的MSB-first
  explicit_msb:
    mode: bitstream
    bit_order: msb
    fields:
      - name: value
        type: uint8
        size: "8"
```

- `bit_order: msb` — MSB-first（デフォルト、省略可）
- `bit_order: lsb` — LSB-first（最下位ビットから読み取り）
- `bit_order` は `mode: bitstream` と併用必須。非ビットストリーム構造体に指定するとバリデーション警告（VAL122）
- ネストしたビットストリーム構造体では、最初の `EnterBitstreamMode` 時のビットオーダーが適用される

### 制約

- **対応するフィールド型**: 整数型（`uint8`/`uint16`/`uint32`/`uint64`/`int8`/`int16`/`int32`/`int64`）、`virtual`、`switch`、`struct`。`bytes`、`ascii` 等のバイト単位型は使用不可
- **`size` の範囲**: 1〜64 ビット
- **ビットオーダー**: デフォルトはMSB-first。`bit_order: lsb` でLSB-firstに変更可能
- **`type` の役割**: 結果の解釈（符号あり/なし）を決定する。実際の読み取りビット幅は `size` で指定
- **非対応**: `repeat: eof`、`repeat: while`、`bytes` 型、文字列型はビットストリーム内で使用不可

### 制御フロー

bitstream 構造体内で以下の制御フローが使用できます。

#### `if` 条件分岐

条件によりビットフィールドの読み取りをスキップできます。

```yaml
mode: bitstream
fields:
  - name: has_extension
    type: uint8
    size: "1"
  - name: extension_data
    type: uint16
    size: "12"
    if: "{has_extension == 1}"
  - name: main_data
    type: uint8
    size: "3"
```

#### `virtual` フィールド

ビット値から計算フィールドを生成します。ビットを消費しません。

```yaml
mode: bitstream
fields:
  - name: channels_minus1
    type: uint8
    size: "3"
  - name: channels
    type: virtual
    value: "{channels_minus1 + 1}"
```

#### `switch` 構造体切り替え

ビット値に基づいて異なるビットストリーム構造体に切り替えます。切り替え先もビットストリーム構造体にすることを推奨します。

```yaml
structs:
  header:
    mode: bitstream
    fields:
      - name: type_id
        type: uint8
        size: "2"
      - name: body
        type: switch
        switch_on: "{type_id}"
        cases:
          "0": type_a_bits
          "1": type_b_bits
        default: type_a_bits

  type_a_bits:
    mode: bitstream
    fields:
      - name: value
        type: uint8
        size: "6"

  type_b_bits:
    mode: bitstream
    fields:
      - name: high
        type: uint8
        size: "3"
      - name: low
        type: uint8
        size: "3"
```

#### `repeat_count` 繰り返し

ビットフィールドを指定回数繰り返し読み取ります。

```yaml
mode: bitstream
fields:
  - name: count
    type: uint8
    size: "4"
  - name: values
    type: uint8
    size: "4"
    repeat: count
    repeat_count: "{count}"
```

#### ネスト構造体

ビットストリーム構造体内から別のビットストリーム構造体を参照すると、ビット位置が引き継がれます（バイトアラインは行われません）。

```yaml
structs:
  parent:
    mode: bitstream
    fields:
      - name: header
        type: uint8
        size: "4"
      - name: nested
        type: struct
        struct: child_bits

  child_bits:
    mode: bitstream
    fields:
      - name: value
        type: uint8
        size: "4"
```

### 自動バイトアライン

bitstream 構造体の終了時、読み取り位置は自動的に次のバイト境界にアラインされます。合計ビット数がバイト境界に一致しない場合、余りビットは読み飛ばされます。

### 符号拡張

`type` が符号付き整数型（`int8`/`int16`/`int32`/`int64`）の場合、読み取ったビット列のMSBを見て符号拡張が行われます。例えば、5ビットの `11111` は `int8` として `-1` に解釈されます。

### 式参照

bitstream 構造体内でもフィールド参照（`{field_name}`）は通常通り使用できます。先行フィールドの値に基づいて後続フィールドのビット幅を動的に決定することも可能です。

```yaml
- name: width
  type: uint8
  size: "4"
- name: value
  type: uint32
  size: "{width}"    # width の値をビット幅として使用
```

### 出力表示

bitstream フィールドのオフセットは `[0x0000000A:3]` 形式（バイトオフセット:ビットオフセット）で表示され、サイズは `(20 bits)` のようにビット単位で表示されます。

### bitfield 型との違い

| 観点 | `bitfield` 型（既存） | `mode: bitstream`（新規） |
|---|---|---|
| 単位 | バイトコンテナからビット範囲を抽出 | ビットストリームとしてNビット連続読み取り |
| 指定方法 | フィールドの `type: bitfield` + `fields` でビット位置を範囲指定 | 構造体の `mode: bitstream` + 各フィールドの `size` がビット幅 |
| バイト境界 | コンテナはバイト単位で読み取り。境界をまたがない | フィールドがバイト境界を自由にまたぐ |
| 典型的ユースケース | フラグレジスタ、ヘッダの固定幅ビットフィールド | FLAC STREAMINFO、H.264 SPS 等のビットストリーム |

### 使用例: FLAC STREAMINFO

```yaml
structs:
  streaminfo:
    - name: info
      type: struct
      struct: streaminfo_bits
    - name: md5
      type: bytes
      size: "16"

  streaminfo_bits:
    mode: bitstream
    fields:
      - name: min_block_size
        type: uint16
        size: "16"
      - name: max_block_size
        type: uint16
        size: "16"
      - name: min_frame_size
        type: uint32
        size: "24"
      - name: max_frame_size
        type: uint32
        size: "24"
      - name: sample_rate
        type: uint32
        size: "20"
      - name: channels
        type: uint8
        size: "3"
      - name: bits_per_sample
        type: uint8
        size: "5"
      - name: total_samples
        type: uint64
        size: "36"
      # 合計: 16+16+24+24+20+3+5+36 = 144ビット = 18バイト
```

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

圧縮型（`zlib`, `deflate`, `gzip`, `bzip2`, `lzma`, `zstd`, `lz4`）は圧縮データを展開します。`struct` を指定すると、展開後のデータを構造体としてネスト解析できます。

```yaml
- name: compressed_data
  type: zlib
  size: "{compressed_size}"
  struct: inner_format      # 展開後のデータを構造体として解析

- name: raw_compressed
  type: deflate
  size: remaining           # structを省略すると生バイト列として出力

- name: gzip_payload
  type: gzip
  size: "{gz_size}"
  struct: inner_data

- name: bz2_section
  type: bzip2
  size: "{bz2_size}"

- name: lzma_data
  type: lzma
  size: "{lzma_size}"

- name: zstd_block
  type: zstd                # "zstandard" もエイリアスとして使用可能
  size: "{block_size}"
  struct: block_content

- name: lz4_frame
  type: lz4
  size: remaining
```

`struct` を省略した場合、展開後の生バイト列が出力されます。

`gzip` は BCL の `GZipStream` を使用します。`bzip2`, `lzma`, `zstd`, `lz4` は外部ライブラリ（BinAnalyzer.Compression）を使用します。`lzma` は raw LZMA 形式（5バイトプロパティ + 8バイトサイズ + 圧縮データ）、`lz4` は LZ4 Frame Format を対象とします。

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

### seek_base（相対オフセット基準）

`seek_base` を指定すると、`seek` の値が相対オフセットとして解釈され、
実効オフセット = `seek_base` + `seek` にジャンプします。

```yaml
- name: section_start
  type: uint32
- name: entry_offset
  type: uint32
- name: data
  type: bytes
  size: "16"
  seek_base: "{section_start}"
  seek: "{entry_offset}"
  seek_restore: true
  # 実効オフセット = section_start + entry_offset
```

- `seek_base` は式で指定（フィールド参照、算術式が使用可能）
- `seek_base` は `seek` と組み合わせて使用（`seek` なしで `seek_base` を指定するとバリデーションエラー VAL015）
- `seek_base` なしの場合は既存動作（`seek` が絶対オフセット）を維持
- 繰り返しフィールドで `_index` と組み合わせて使用可能

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

構造体をオブジェクト形式（`endianness`/`align`/`string_table`/`mode`/`fields` キー）で定義します。旧形式（フィールドリスト直接）との混在も可能です。

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

#### エンコーディング指定

`string_table` にオブジェクト形式で `encoding` を指定すると、UTF-8 や UTF-16LE/BE でエンコードされた文字列テーブルを扱えます。

```yaml
structs:
  # UTF-8 文字列テーブル
  utf8_strtab:
    string_table:
      encoding: utf8
    fields:
      - name: data
        type: bytes
        size: remaining

  # UTF-16LE 文字列テーブル（Windows PE リソース等）
  utf16_strtab:
    string_table:
      encoding: utf16le
    fields:
      - name: data
        type: bytes
        size: remaining
```

| エンコーディング | null 終端 | 説明 |
|---|---|---|
| `ascii`（デフォルト） | 1 バイト (0x00) | ASCII 文字列。`string_table: true` と同等 |
| `utf8` | 1 バイト (0x00) | UTF-8 文字列。マルチバイト文字対応 |
| `utf16le` | 2 バイト (0x00, 0x00) | UTF-16 リトルエンディアン |
| `utf16be` | 2 バイト (0x00, 0x00) | UTF-16 ビッグエンディアン |

- `encoding` を省略した場合のデフォルトは `ascii`
- `string_table: true` は後方互換で `ascii` として動作
- UTF-16 の null 終端は 2 バイト (0x00, 0x00)。1 バイトの 0x00 は UTF-16 文字の一部として出現しうるため区別される

### 文字列テーブルの参照

整数フィールドに `string_table` で参照先テーブル名を指定すると、整数値をオフセットとして文字列を解決します。

```yaml
- name: name_offset
  type: uint32
  string_table: strtab   # strtab テーブルの offset 位置の文字列を解決
```

- 文字列テーブルはヌル終端文字列として解釈（エンコーディングに応じた終端バイトを使用）
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

## エラー回復マーカー（resync_marker）

構造体定義に `resync_marker` を指定すると、`on-error: continue` モードでのエラー回復が改善されます。繰り返しフィールドの要素デコードに失敗した場合、マーカーバイトパターンを前方スキャンして次の要素の開始位置を特定し、デコードを再開します。

チャンクベース（PNG, MIDI, MP4等）やレコード配列型（PE, ELF, ZIP等）のフォーマットで、壊れた要素以降の連鎖的なデコード失敗を防ぎます。

### 基本的な使い方

```yaml
structs:
  midi_track:
    resync_marker: [0x4D, 0x54, 0x72, 0x6B]  # "MTrk"
    fields:
      - name: magic
        type: ascii
        size: 4
        expect: "MTrk"
      - name: length
        type: uint32
      - name: data
        type: bytes
        size: "{length}"
```

### 動作

- 繰り返し要素のデコードに失敗すると、残りデータ内でマーカーパターンを前方スキャンします
- マーカーが見つかった場合、その位置から次の要素のデコードを再開します
- マーカーが見つからない場合、スコープ終端までスキップしてループを終了します
- マーカーが現在位置にある場合は無限ループを防ぐため、さらに前方を探します
- スキップしたバイト数は `DecodedError` ノードの `skipped_bytes` に記録されます

### align との関係

構造体に `align` が指定されていてマーカーが未定義の場合、エラー時に次のアライメント境界までスキップします。`resync_marker` と `align` の両方がある場合はマーカーが優先されます。

## Diff キー（diff_key）

配列（repeat 系属性を持つフィールド）に `diff_key` を指定すると、`binanalyzer diff` コマンドでの比較時に、インデックスではなく指定フィールドの値をキーとして要素をマッチングします。単一フィールドキーと複合キー（複数フィールド）の両方に対応しています。

### 単一キー

```yaml
- name: sections
  type: struct
  struct: section_header
  repeat_count: "{num_sections}"
  diff_key: name              # 各要素の name フィールドをキーとして比較
```

### 複合キー

複数フィールドの組み合わせでユニークになる場合、リスト形式で複合キーを指定できます:

```yaml
# インライン形式
- name: entries
  type: struct
  struct: entry
  repeat_count: "{count}"
  diff_key: [group_id, item_id]

# ブロック形式
- name: entries
  type: struct
  struct: entry
  repeat_count: "{count}"
  diff_key:
    - group_id
    - item_id
```

単一キーは内部的に要素 1 のリストとして統一処理されます。

### 動作

- **diff_key 未指定**: 従来のインデックスベース比較（`[0]` vs `[0]`, `[1]` vs `[1]`, ...）
- **単一キー指定**: キーベース比較（`[name=.text]` vs `[name=.text]`, ...）
- **複合キー指定**: 複合キーベース比較（`[group_id=1,item_id=3]` vs `[group_id=1,item_id=3]`, ...）

キーベース比較では、要素の挿入・削除・並び替えがあっても、同じキー値を持つ要素同士が正しくマッチングされます。

### 使用例

#### 単一キー

```
$ binanalyzer diff old.elf new.elf -f formats/elf.bdef.yaml

sections
  [name=.text]  (同一)
  [name=.data]
    size  1024 → 2048
  + [name=.rodata]
  - [name=.debug_info]
```

#### 複合キー

```
$ binanalyzer diff old.bin new.bin -f formats/record.bdef.yaml

entries
  [group_id=1,item_id=1]  (同一)
  [group_id=1,item_id=2]
    val  20 → 99
  [group_id=2,item_id=1]  (同一)
  + [group_id=2,item_id=2]
```

### 制約

- `diff_key` に指定するフィールドの型は整数（`DecodedInteger`）または文字列（`DecodedString`）のみ対応
- 配列要素が構造体（`DecodedStruct`）でない場合は使用不可（インデックスベースにフォールバック）
- 指定フィールドが要素内に存在しない場合もインデックスベースにフォールバック
- 空リスト `diff_key: []` の場合、インデックスベースにフォールバック
- diff 以外の処理（デコード、ツリー表示、hexdump 等）には影響しない

## ステートフル変数

一部のバイナリフォーマットでは、前のレコードの状態が次のレコードのデコードに影響する「ステートフル」なパースが必要です（例: MIDIランニングステータス）。

ステートフル変数は、繰り返しイテレーションやネスト構造体を跨いで値を保持する仕組みです。通常のフィールド変数（スコープPopで消える）とは異なり、デコードセッション全体で永続します。

### フィールドプロパティ

| プロパティ | 型 | 説明 |
|---|---|---|
| `state` | string | デコードした値を保存する状態変数名 |
| `state_if` | string (式) | 状態変数を更新する条件式。省略時は常に更新 |
| `state_default` | integer | 状態変数の初期値。まだ設定されていない場合に使用 |

### 式内での参照

状態変数は式内で `@state_name` として参照します。通常のフィールド変数 `{field_name}` との名前衝突を防ぎ、状態変数であることが明示されます。

```yaml
# 状態変数を設定
- name: status
  type: uint8
  state: "running_status"

# 状態変数を参照
- name: effective
  type: virtual
  value: "{@running_status}"
```

### 動作タイミング

1. **デコード前**: `state_default` が指定されかつ未初期化の場合、初期値をセット
2. **デコード後**: `state_if` が未指定 or true の場合、フィールドの値を状態変数に保存

### 使用例: MIDI ランニングステータス

```yaml
midi_event:
  fields:
    - name: status_or_data
      type: uint8
      state: "running_status"
      state_if: "{status_or_data & 0x80}"
      state_default: 0

    - name: effective_status
      type: virtual
      value: "{status_or_data & 0x80 ? status_or_data : @running_status}"
```

- `status_or_data` のbit 7がセット → 新しいステータスバイト → `@running_status` を更新
- bit 7がクリア → ランニングステータス → 保存済みの `@running_status` を使用

### バリデーション

| コード | 重要度 | 内容 |
|---|---|---|
| VAL017 | Error | `state_if` を指定するには `state` が必要 |
| VAL117 | Warning | `state_default` を指定するには `state` が必要 |
