# REQ-122: ビット単位フィールド読み取り

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| フェーズ | D（エンジン） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-15 |

## 背景・動機

現在の BinAnalyzer はバイト境界でフィールドを読み取る。bitfield 型でビットレベルの値抽出は可能だが、バイト境界をまたぐ任意ビット幅のフィールドを連続して読み取ることはできない。一部のバイナリフォーマット（H.264 NAL, Huffman 符号化データ等）ではビットストリームとしての読み取りが必要。

### ユースケース

- H.264/H.265 のパラメータセット（SPS, PPS）のパース（Exp-Golomb 符号化）
- Huffman 符号化テーブルの読み取り
- パケットフォーマットのビットフィールドが連続する領域の解析
- 暗号プロトコルのビット単位フラグ読み取り

## 機能要件

### 追加する機能

- [ ] `mode: bitstream` 構造体属性でビットストリームモードを指定可能に
- [ ] bitstream 構造体内では `size` がビット単位として解釈される
- [ ] ビットストリームリーダーの実装（DecodeContext に `BitReader` 追加）
- [ ] バイト境界をまたぐフィールドの連続読み取り
- [ ] ビットオフセットの追跡と表示（`[0x0000000A:3]` 形式）
- [ ] bitstream 構造体の終了時に自動バイトアライン

### 対応するフィールド型

- 整数型のみ（uint8/16/32/64, int8/16/32/64）
- 型は結果の解釈（符号あり/なし）を決める。実際の読み取りビット幅は `size` で指定。

### 変更する既存機能

なし

### 変更しないもの（スコープ外）

- Exp-Golomb 等の可変長ビット符号化のネイティブサポート（DSL の式で対応可能）
- ビットストリーム全体を対象とした式評価
- 既存 `bitfield` 型の変更（パターンA用として共存）

## 受入条件

1. [ ] `mode: bitstream` 構造体内で `size` がビット単位として正しく解釈されること
2. [ ] バイト境界をまたぐフィールドが正しく読み取られること
3. [ ] ビットオフセットがデコード結果に反映されること
4. [ ] bitstream 構造体終了時にバイト境界へ自動アラインされること
5. [ ] 既存 `bitfield` 型に影響がないこと
6. [ ] 既存のバイト単位フィールド読み取りに影響がないこと（後方互換性）
7. [ ] 単体テストが追加されていること
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition` に `IsBitstream` プロパティ追加。`DecodedNode.BitOffset` プロパティ追加（ビットオフセット表示用） |
| BinAnalyzer.Dsl | `YamlStructModel` に `mode` 属性パース追加。`mode: bitstream` → `IsBitstream = true` |
| BinAnalyzer.Engine | `DecodeContext` に `BitReader` 内部クラス追加（ビット位置追跡、N ビット読み取り、バイトアライン）。`BinaryDecoder` で bitstream 構造体のスコープ処理 |
| BinAnalyzer.Output | `TreeOutputFormatter` でビットオフセット表示（`[0x0000000A:3]` 形式） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md
- [ ] docs/architecture.md
- [ ] CLAUDE.md
- [ ] README.md

---

## 設計メモ

### 設計方針

構造体レベル `mode: bitstream` を採用する。Construct の `BitStruct` と同じアプローチ。既存 `bitfield` 型と共存させる。

- bitstream 構造体内のフィールドは `size` がビット単位として解釈される
- フィールドの `type` は結果の解釈（符号あり/なし、幅）を決める。実際の読み取りビット幅は `size` で指定
- 構造体の終了時にバイト境界へ自動アライン

### 既存 bitfield 型との違い

2つのパターンは異なるユースケースに対応し、共存する。

| 観点 | bitfield 型（既存） | mode: bitstream（新規） |
|---|---|---|
| 単位 | バイトコンテナからビット抽出 | ビットストリームとしてN ビット連続読み取り |
| 指定方法 | フィールドの `type: bitfield` + `fields` でビット位置を範囲指定（`bits: "7:4"`） | 構造体の `mode: bitstream` + 各フィールドの `size` がビット幅 |
| 典型的ユースケース | フラグレジスタ、ヘッダの固定幅ビットフィールド | FLAC STREAMINFO、H.264 SPS 等のビットストリーム全体 |
| バイト境界 | コンテナはバイト単位で読み取る。境界をまたがない | フィールドがバイト境界を自由にまたぐ |
| 読み取り方式 | `ReadUInt8/16/32/64` → ビットマスク・シフト | `BitReader.ReadBits(n)` でN ビット順次読み取り |

### DSL 構文

`mode: bitstream` は struct レベル属性（`endianness:`, `align:`, `string_table:` と同列）として追加する。struct レベル属性がある場合、既存の DSL 規約により `fields:` キーが必須となる（新形式マッピング）。

```yaml
# 旧形式: struct レベル属性なし → フィールドリスト直接
streaminfo:
  - name: min_block_size
    type: uint16

# 新形式: struct レベル属性あり → fields: キー必須
vorbis_comment_block:
  endianness: little
  fields:
    - name: vendor_length
      type: uint32

# bitstream: 新形式と同じパターン
flac_streaminfo_bits:
  mode: bitstream
  fields:
    - name: sample_rate
      type: uint32
      size: "20"
```

**パーサーへの影響**: `StructNodeDeserializer` は変更不要。`SequenceStart` → 旧形式、`MappingStart` → YamlDotNet デフォルト処理で `mode` を含むマッピング形式をそのまま処理できる。

### DSL 例: FLAC STREAMINFO

#### 現在の実装（workaround）

現在の `flac.bdef.yaml` では、ビットストリーム領域を以下のように処理している:

```yaml
# 24ビット値: uint8 × 3 + virtual でシフト合成（冗長）
- name: min_frame_size_b0
  type: uint8
- name: min_frame_size_b1
  type: uint8
- name: min_frame_size_b2
  type: uint8
- name: min_frame_size
  type: virtual
  value: "{(min_frame_size_b0 << 16) | (min_frame_size_b1 << 8) | min_frame_size_b2}"

# ビットストリーム領域: 8バイト bitfield でビット範囲指定
- name: sample_rate_channels_bps_samples
  type: bitfield
  size: "8"
  fields:
    - name: sample_rate
      bits: "63:44"     # 20ビット
    - name: channels
      bits: "43:41"     # 3ビット
    - name: bps
      bits: "40:36"     # 5ビット
    - name: total_samples
      bits: "35:0"      # 36ビット
```

#### mode: bitstream を使った場合

bitstream 構造体内では `bytes` 等のバイト単位型は使えないため、ビットストリーム部分と非ビットストリーム部分（MD5 等）は別構造体に分ける。

```yaml
structs:
  # 親構造体: bitstream 部分 + バイト単位部分を組み合わせ
  streaminfo:
    - name: info
      type: struct
      struct: streaminfo_bits
    - name: md5
      type: bytes
      size: "16"
      description: "非圧縮オーディオデータのMD5シグネチャ"

  # bitstream 構造体: 全フィールドがビット単位
  streaminfo_bits:
    mode: bitstream
    fields:
      - name: min_block_size
        type: uint16
        size: "16"
        description: "最小ブロックサイズ（サンプル数）"
      - name: max_block_size
        type: uint16
        size: "16"
        description: "最大ブロックサイズ（サンプル数）"
      - name: min_frame_size
        type: uint32
        size: "24"          # 24ビット → virtual 不要
        description: "最小フレームサイズ（バイト、0=不明）"
      - name: max_frame_size
        type: uint32
        size: "24"
        description: "最大フレームサイズ（バイト、0=不明）"
      - name: sample_rate
        type: uint32
        size: "20"          # 20ビット、バイト境界をまたぐ
        description: "サンプルレート（Hz）"
      - name: channels
        type: uint8
        size: "3"
        description: "チャンネル数 - 1（実値は+1）"
      - name: bits_per_sample
        type: uint8
        size: "5"
        description: "ビット/サンプル - 1（実値は+1）"
      - name: total_samples
        type: uint64
        size: "36"          # 36ビット、バイト境界をまたぐ
        description: "総サンプル数（0=不明）"
      # 合計: 16+16+24+24+20+3+5+36 = 144ビット = 18バイト（バイト境界に一致）
```

**改善点**:
- 24ビット値の `uint8` × 3 + `virtual` シフト合成 → `size: "24"` の1フィールドに
- 8バイト `bitfield` のビット範囲指定 → 各フィールドが独立した型と `size` を持つ
- フィールド名がそのまま意味を持つ（`sample_rate_channels_bps_samples` → `sample_rate`, `channels`, ...）

### モデル変更

#### Core — StructDefinition

```csharp
public sealed class StructDefinition
{
    // 既存プロパティ（省略）
    public bool IsBitstream { get; init; }  // 新規追加
}
```

#### Core — DecodedNode

```csharp
public abstract class DecodedNode
{
    // 既存プロパティ（省略）
    public int? BitOffset { get; init; }  // 新規追加。null = バイト境界（既存互換）
}
```

- `BitOffset`: 0–7 の範囲。バイトオフセット `Offset` 内のビット位置を表す。
- 通常のバイト単位フィールドでは `null`（既存の表示に影響しない）。
- bitstream 構造体内のフィールドでは 0–7 の値を持つ。

#### Dsl — YamlStructModel

```csharp
public sealed class YamlStructModel
{
    // 既存プロパティ（省略）
    [YamlMember(Alias = "mode")]
    public string? Mode { get; set; }  // 新規追加。"bitstream" をサポート
}
```

#### Dsl — StructNodeDeserializer

変更不要。`mode: bitstream` 付きの struct はマッピング形式（`MappingStart`）であり、既存のフォールバック処理（YamlDotNet デフォルト）で `YamlStructModel` にデシリアライズされる。

#### Dsl — YamlToIrMapper

`MapStructs` メソッドで以下を追加:

1. `structModel.Mode == "bitstream"` → `IsBitstream = true` にマッピング
2. `structModel.Mode` が `null` でも `"bitstream"` でもない場合 → パースエラーを報告
3. bitstream 構造体のフィールド型バリデーション: `IsBitstream` 構造体内のフィールドが整数型以外の場合、パースエラーを報告

### BitReader の内部設計

`DecodeContext` に内部クラス `BitReader` を追加する。既存のバイト読み取りメソッド群とは独立して動作し、bitstream 構造体のスコープ内でのみ使用される。

```
BitReader
├── コンストラクタ(DecodeContext context)
├── ReadBits(int count) → long     // N ビット読み取り（1–64）
├── BitPosition → int              // 現在バイト内のビット位置（0–7）
├── TotalBitsRead → long           // 読み取り済みビット総数
├── AlignToByte()                  // 次のバイト境界まで進める
└── Remaining → long               // 残りビット数（スコープ内）
```

**ReadBits のアルゴリズム（MSB-first）**:

1. 現在のバイトから読み取り可能なビット数を計算
2. 必要なビット数を満たすまでバイトを順次消費
3. 各バイトの未消費ビットを上位から取り出す
4. 結果を `long` に詰めて返す

```
例: BitPosition=3 の状態で ReadBits(10) を実行
  バイト0: ビット[2:0] の 3 ビットを取得（残り 7 ビット必要）
  バイト1: ビット[7:1] の 7 ビットを取得
  → 合計 10 ビット。BitPosition=1 になる
```

**DecodeContext との統合**:

```
DecodeContext
├── 既存メソッド群（変更なし）
├── EnterBitstreamMode()    // BitReader を生成、bitstream スコープ開始
├── ExitBitstreamMode()     // AlignToByte → BitReader を破棄、Position を更新
├── ReadBitsAsLong(int n)   // BitReader.ReadBits(n) のラッパー
├── IsBitstreamMode → bool  // BitReader が存在するか
└── CurrentBitOffset → int? // BitReader?.BitPosition
```

- `EnterBitstreamMode()` は `BinaryDecoder.DecodeStruct` で `IsBitstream` 構造体を検出した際に呼ばれる
- `ExitBitstreamMode()` は構造体のフィールド処理完了後に呼ばれ、自動アラインを行う
- bitstream モード中、`BinaryDecoder` のフィールド読み取りは `ReadBitsAsLong(size)` に分岐する

### BinaryDecoder の変更箇所

#### DecodeStruct メソッド

```
DecodeStruct(structDef, format, context):
    if structDef.IsBitstream:
        context.EnterBitstreamMode()

    // 既存: フィールドを順次デコード
    foreach field in structDef.Fields:
        child = DecodeField(field, format, context)  // 分岐は内部で
        children.Add(child)

    if structDef.IsBitstream:
        context.ExitBitstreamMode()  // バイトアライン + モード終了
```

#### DecodeField メソッド（整数フィールド）

bitstream モード中は既存の `ReadUInt8/16/32/64` ではなく `ReadBitsAsLong(size)` を使用。`size` の解釈がバイト→ビットに変わる。

```
DecodeIntegerField(field, format, context):
    if context.IsBitstreamMode:
        bitSize = ResolveSize(field, context)  // size がビット単位
        rawValue = context.ReadBitsAsLong(bitSize)
        // 符号拡張: type が int8/16/32/64 の場合、MSB を見て拡張
        offset = context.Position  // BitReader 開始前のバイト位置
        bitOffset = context.CurrentBitOffset
        → DecodedInteger { Offset=offset, BitOffset=bitOffset, ... }
    else:
        // 既存のバイト単位読み取り（変更なし）
```

#### 符号拡張

`type` が符号付き（int8/16/32/64）の場合、読み取った `bitSize` ビットの MSB を見て符号拡張する。

```
SignExtend(value, bitSize):
    if bitSize >= 64: return value  // そのまま
    signBit = 1L << (bitSize - 1)
    if (value & signBit) != 0:
        value |= ~((1L << bitSize) - 1)  // 上位ビットを1埋め
    return value
```

### インタフェース変更

- 既存の public インタフェース変更なし
- `DecodeContext` に内部メソッド追加（`EnterBitstreamMode`, `ExitBitstreamMode`, `ReadBitsAsLong`, `IsBitstreamMode`, `CurrentBitOffset`）
- `BinaryDecoder` 内部の分岐追加のみ

### Output — ビットオフセット表示

`TreeOutputFormatter` のオフセット表示を拡張する。

```
既存:  [0x0000000A] (4 bytes)
新規:  [0x0000000A:3] (20 bits)   ← BitOffset=3, Size はビット単位
```

**表示ルール**:
- `BitOffset` が `null` → 既存表示（変更なし）
- `BitOffset` が 0–7 → `[0x{Offset:X8}:{BitOffset}]` 形式
- サイズ単位: bitstream 内のフィールドは `(N bits)` 表記、通常フィールドは `(N bytes)` 表記

`DecodedNode.Size` の解釈: bitstream 内のフィールドでは `Size` がビット数を表す。`BitOffset != null` を判定条件とする。

### 代替案

1. **フィールドレベル `bit_size` 属性** — Kaitai Struct 方式。フィールドごとに `bit_size` を指定。冗長になりがちで、ビットストリーム全体の構造が表現しにくい。
2. **全アドレスをビット単位に変更** — Hachoir 方式。エンジン全体の改修が必要で過大。
3. **フィールドレベル + 構造体レベルの併用** — 010 Editor 方式。柔軟だが複雑度が増す。

### 懸念事項と暫定方針

#### 1. 式評価（変数バインディング）

**懸念**: bitstream 構造体内で `{field_name}` による式参照が必要な場合、変数バインディングはどうなるか。

**暫定方針**: **変更不要**。既存の `context.SetVariable(field.Name, value)` の仕組みはそのまま使える。bitstream フィールドのデコード値（long）を通常通り変数に設定する。変数解決はスコープスタックを辿るため、bitstream 構造体のスコープ内で正しく動作する。ビットオフセットは変数解決に影響しない（変数は名前→値のマッピングであり、オフセット情報は持たない）。

#### 2. エンディアン（ビットオーダー）

**懸念**: ビットストリームのビットオーダー（MSB-first / LSB-first）の扱い。

**暫定方針**: **デフォルト MSB-first**（ビッグエンディアン・ビットオーダー）。理由:
- FLAC, H.264, MPEG, JPEG 等の主要フォーマットは MSB-first
- Kaitai Struct のデフォルトも MSB-first（`bit-endian: be`）
- 初期実装は MSB-first のみをサポート
- LSB-first が必要になった場合、`bit_endian: le` 属性を `mode: bitstream` と併用する形で拡張可能（将来課題）

#### 3. ネスト（bitstream 内の struct 参照）

**懸念**: bitstream 構造体内で通常の struct を `struct_ref` で参照した場合の挙動。

**暫定方針**: **初期実装では禁止**。bitstream 構造体内で `type: struct` フィールドを検出した場合、パースエラーとする。理由:
- 通常の struct はバイト単位読み取りを前提とする
- bitstream 内でバイト単位 struct に切り替えるには、自動アライン→通常モード→bitstream 復帰 の制御が必要で複雑
- 実フォーマットでこのパターンが必要になった時点で設計・実装する（YAGNI）

**許可する型**: 整数型（uint8/16/32/64, int8/16/32/64）のみ。bitfield, bytes, string, struct, switch 等は bitstream 内では使用不可。

#### 4. size: remaining の扱い

**懸念**（新規）: bitstream 構造体内で `size: remaining` を使用した場合の解釈。

**暫定方針**: bitstream 内での `size: remaining` は残りビット数を返す。`DecodeContext` のスコープ管理が既にバイト単位の remaining を持っているため、bitstream モードでは `remaining_bytes * 8 - bits_read_in_current_byte` に変換する。

#### 5. repeat の扱い

**懸念**（新規）: bitstream 構造体内のフィールドに `repeat` を指定した場合の挙動。

**暫定方針**: `repeat: count` のみサポート。`repeat: until_eof` は bitstream スコープの残りビットが 0 になるまで繰り返す。`repeat: until_value` / `repeat: while` は通常通り式評価で判定。各要素の `size` はビット単位。

### エッジケースと制約

| ケース | 挙動 |
|---|---|
| `size` が 0 | エラー（0 ビット読み取りは無意味） |
| `size` が 64 超過 | エラー（long に収まらない） |
| bitstream 構造体内で整数型以外を使用 | パースエラー |
| bitstream 構造体内で `type: struct` を使用 | パースエラー |
| bitstream 構造体のフィールド合計ビット数がバイト境界に一致しない | 構造体終了時に自動アライン（余りビットは読み飛ばし） |
| bitstream 構造体のスコープバイト数を超えて読み取り | デコードエラー（既存の EOF 検出と同様） |

---

## 実装メモ

### 実装中の設計変更

設計通りに実装。特に設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitstreamParsingTests | Load_BitstreamMode_SetsIsBitstreamTrue | 1 |
| BitstreamParsingTests | Load_StructWithoutMode_IsBitstreamIsFalse | 6 |
| BitstreamParsingTests | Load_BitstreamFields_ParsedCorrectly | 1 |
| BitstreamParsingTests | Load_InvalidMode_ThrowsException | 1 |
| BitstreamParsingTests | Load_BitstreamWithNonIntegerField_ThrowsException | 1 |
| BitstreamDecoderTests | Decode_ByteAligned_16plus16 | 1 |
| BitstreamDecoderTests | Decode_CrossByteBoundary_20plus12 | 2 |
| BitstreamDecoderTests | Decode_SmallBitWidths_3plus5 | 1 |
| BitstreamDecoderTests | Decode_SignedInteger_SignExtension | 1 |
| BitstreamDecoderTests | Decode_AutoByteAlign_OnStructExit | 4 |
| BitstreamDecoderTests | Decode_FlacStreaminfoPattern | 2 |
| BitstreamDecoderTests | Decode_VariableReference_InBitstreamFields | 1 |
| BitstreamDecoderTests | Decode_SingleBit | 1 |
| BitstreamDecoderTests | Decode_SignedPositiveValue | 1 |
| BitstreamDecoderTests | Decode_BitOffset_TrackedCorrectly | 3 |

### 気づき・今後の課題

- LSB-first ビットオーダーは未実装。`bit_endian: le` 属性で将来拡張可能。
- bitstream 内の `type: struct` ネストは禁止（設計通り）。フィールド型バリデーションで整数型のみ許可。
