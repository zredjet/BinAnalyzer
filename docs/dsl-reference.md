# DSLリファレンス

BinAnalyzerのフォーマット定義は `.bdef.yaml` 拡張子のYAMLファイルを使用します。

## トップレベル構造

```yaml
name: フォーマット名
endianness: big  # または "little"（デフォルト: big）
root: 構造体名

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
| `bytes` | 可変 | 生バイト列 |
| `ascii` | 可変 | ASCII文字列 |
| `utf8` | 可変 | UTF-8文字列 |
| `struct` | 可変 | ネスト構造体参照 |
| `switch` | 可変 | 条件付き構造体選択 |

## サイズ指定

可変サイズのフィールド（`bytes`, `ascii`, `utf8`, `struct`, `switch`）にはサイズ指定が必要です:

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
- **括弧**: `{(a + b) * c}`

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
        bit: 5
        set: "yes"
        clear: "no"
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

## バリデーション

バイトフィールドに期待値を指定できます:

```yaml
- name: signature
  type: bytes
  size: "8"
  expected: [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
```

出力にはバリデーション結果としてチェックマーク（✓）またはバツ印（✗）が表示されます。
