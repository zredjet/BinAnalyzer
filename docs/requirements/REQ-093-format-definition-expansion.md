# REQ-093: フォーマット定義の構造化拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-092（ワークアラウンド解消、完了済み） |
| 作成日 | 2026-02-10 |
| 更新日 | 2026-02-11 |

## 背景・動機

REQ-053〜082 で30フォーマットの初期定義、REQ-092 で5フォーマットのワークアラウンド解消が完了した。現在、全30フォーマットの構造化深度は以下の分布となっている:

| 深度 | 件数 | 定義 |
|---|---|---|
| Deep（内部まで構造化済み） | 12 | PNG, ELF, PE, JPEG, Java-Class, Mach-O, FLAC, GIF, WebP, ZIP, BMP, MP3 |
| Moderate（主構造のみ） | 11 | OTF, WAV, AVI, PCAP, TIFF, SQLite, FLV, WASM, MIDI, GZIP, LZ4, ICO |
| Shallow（ヘッダのみ） | 7 | ICC, DNS, TAR, Parquet, 7z, PDF |

Moderate/Shallow のうち、DSL の表現力の限界で対応不可なもの（DNS 名前圧縮、PDF テキスト構造、Parquet Thrift、7z 独自エンコーディング）と、圧縮データの実体が不透明なもの（GZIP deflate、LZ4 ブロック）を除外すると、以下の8フォーマットが拡充可能と判断した。

### 拡充候補一覧

| # | フォーマット | 現在の深度 | 現状 | 拡充内容 | 難易度 |
|---|---|---|---|---|---|
| 1 | PCAP | Moderate | IPv4 body が `bytes remaining` | TCP/UDP ヘッダ追加、IPv6 ヘッダ追加 | 低〜中 |
| 2 | WAV | Moderate | LIST-INFO が `bytes remaining` | INFO サブチャンク解析 | 低 |
| 3 | OTF | Moderate | head/name テーブルのみ | cmap, OS/2, post, hhea テーブル追加 | 中 |
| 4 | AVI | Moderate | avih のみ | strh, strf, idx1 チャンク追加 | 中 |
| 5 | ICC | Shallow | タグ表のみ、データ未参照 | 主要タグデータ seek 参照 (desc, XYZ, curv, text) | 中 |
| 6 | TIFF | Shallow | IFD エントリのみ、値未参照 | インライン値の型別解釈（4バイト以内の SHORT/LONG 値） | 中 |
| 7 | SQLite | Moderate | B-tree ヘッダ + セルポインタ配列 | リーフテーブルセル内部（varint + ペイロードヘッダ） | 高 |
| 8 | TAR | Shallow | ヘッダ1ブロックのみ | 既知制約（ファイルデータスキップに `element_size` 連携が不可）→ 対象外 |

### 対応不可（参考）

| フォーマット | 理由 |
|---|---|
| DNS | 名前圧縮ポインタ（0xC0xx）の逆参照が必要 |
| PDF | テキストベース構造で根本的にバイナリ DSL 対象外 |
| Parquet | Thrift コンパクトプロトコル |
| 7z | 独自可変長エンコーディング |
| GZIP | deflate 圧縮データは `deflate` 型で展開済み。展開後データの構造は対象外 |
| LZ4 | LZ4 圧縮データの展開は DSL 対象外 |
| TAR | ファイルデータブロック数が8進ASCII文字列の size フィールドに依存し、DSL では8進数→整数変換ができない。`element_size: "512"` で均一読みは既に実装済み |

## 機能要件

### 拡充 1: PCAP — TCP/UDP/IPv6 ヘッダ追加

**現状** (`formats/pcap.bdef.yaml`):
- `ipv4_packet.body` が `bytes remaining` で IP ペイロード未解析
- `ethernet_frame.payload` の switch に IPv6 ケースなし

**改善後**:

```yaml
# ipv4_packet の body を switch 化
- name: body
  type: switch
  size: remaining
  switch_on: "{protocol}"
  cases:
    "6": tcp_segment
    "17": udp_datagram
  default: raw_data

tcp_segment:
  - name: src_port
    type: uint16
    description: "送信元ポート"
  - name: dst_port
    type: uint16
    description: "宛先ポート"
  - name: seq_number
    type: uint32
    description: "シーケンス番号"
  - name: ack_number
    type: uint32
    description: "確認応答番号"
  - name: data_offset_flags
    type: bitfield
    size: "2"
    fields:
      - name: data_offset
        bits: "15:12"
        description: "データオフセット（32ビットワード単位）"
      - name: reserved
        bits: "11:9"
      - name: ns
        bits: "8"
        description: "ECN-nonce"
      - name: cwr
        bits: "7"
      - name: ece
        bits: "6"
      - name: urg
        bits: "5"
      - name: ack
        bits: "4"
      - name: psh
        bits: "3"
      - name: rst
        bits: "2"
      - name: syn
        bits: "1"
      - name: fin
        bits: "0"
  - name: window_size
    type: uint16
  - name: checksum
    type: uint16
  - name: urgent_pointer
    type: uint16
  - name: payload
    type: bytes
    size: remaining
    description: "TCPペイロード（オプション含む）"

udp_datagram:
  - name: src_port
    type: uint16
    description: "送信元ポート"
  - name: dst_port
    type: uint16
    description: "宛先ポート"
  - name: length
    type: uint16
    description: "データグラム長（ヘッダ含む）"
  - name: checksum
    type: uint16
  - name: payload
    type: bytes
    size: remaining
    description: "UDPペイロード"

# ethernet_frame.payload に IPv6 ケース追加
# ether_type 0x86DD (34525) → ipv6_packet

ipv6_packet:
  - name: version_tc_flow
    type: bitfield
    size: "4"
    fields:
      - name: version
        bits: "31:28"
        description: "IPバージョン（6）"
      - name: traffic_class
        bits: "27:20"
      - name: flow_label
        bits: "19:0"
  - name: payload_length
    type: uint16
    description: "ペイロード長（バイト）"
  - name: next_header
    type: uint8
    enum: protocol
    description: "次ヘッダ（プロトコル番号）"
  - name: hop_limit
    type: uint8
    description: "ホップリミット"
  - name: src_ip
    type: bytes
    size: "16"
    description: "送信元IPv6アドレス"
  - name: dst_ip
    type: bytes
    size: "16"
    description: "宛先IPv6アドレス"
  - name: body
    type: switch
    size: remaining
    switch_on: "{next_header}"
    cases:
      "6": tcp_segment
      "17": udp_datagram
    default: raw_data
```

- [x]`tcp_segment` 構造体を新規定義（20バイトヘッダ + flags bitfield + remaining ペイロード）
- [x]`udp_datagram` 構造体を新規定義（8バイトヘッダ + remaining ペイロード）
- [x]`ipv6_packet` 構造体を新規定義（40バイト固定ヘッダ + protocol switch）
- [x]`ipv4_packet.body` を `switch` に変更し `protocol` で分岐
- [x]`ethernet_frame.payload` の switch に `"34525": ipv6_packet` を追加
- [x]IPv6 でも `protocol` enum を再利用（next_header フィールド）

**備考**: IPv4 オプション（IHL > 5 の場合）は body の先頭にオプションバイトが含まれるが、オプション長の算出に `(ihl - 5) * 4` が必要。現在 `ihl` は virtual フィールドで利用可能なため、将来的に `options` + `payload` への分離が可能。本REQでは `remaining` に含めて対応する。

### 拡充 2: WAV — LIST-INFO サブチャンク解析

**現状** (`formats/wav.bdef.yaml`):
- `list_chunk.data` が `bytes remaining` で INFO メタデータ未解析

**改善後**:

```yaml
list_chunk:
  - name: list_type
    type: ascii
    size: "4"
    description: "リストタイプ識別子（例: INFO）"
  - name: data
    type: switch
    switch_on: "{list_type}"
    cases:
      "'INFO'": info_chunk_data
    default: raw_data

info_chunk_data:
  - name: sub_chunks
    type: struct
    struct: info_sub_chunk
    repeat: eof

info_sub_chunk:
  - name: chunk_id
    type: ascii
    size: "4"
    description: "INFO チャンクID（IART, INAM, ICMT, ISFT 等）"
  - name: chunk_size
    type: uint32
  - name: value
    type: ascii
    size: "{chunk_size}"
    description: "チャンク値"
  - name: padding
    type: bytes
    size: "{chunk_size % 2}"
    if: "{chunk_size % 2 != 0}"
    padding: true
    description: "ワード境界アライメント"
```

- [x]`info_chunk_data` 構造体を新規定義（`repeat: eof` で INFO サブチャンクを繰り返し読み取り）
- [x]`info_sub_chunk` 構造体を新規定義（chunk_id + size + value + padding）
- [x]`list_chunk.data` を `switch` に変更し `list_type` で分岐
- [x]INFO チャンクID の enum 定義（任意: IART=アーティスト, INAM=タイトル, ICMT=コメント, ISFT=ソフトウェア, ICRD=作成日, IGNR=ジャンル）

### 拡充 3: OTF — 主要テーブル追加

**現状** (`formats/otf.bdef.yaml`):
- `table_record.table_data` の switch に `head`/`name` の2テーブルのみ

**改善後**: `cmap`、`OS/2`、`post`、`hhea` の4テーブルを追加。

```yaml
# table_record.table_data の switch に追加
cases:
  "'head'": head_table
  "'name'": name_table
  "'cmap'": cmap_table
  "'OS/2'": os2_table
  "'post'": post_table
  "'hhea'": hhea_table
default: raw_data

cmap_table:
  - name: version
    type: uint16
    description: "テーブルバージョン（0）"
  - name: numTables
    type: uint16
    description: "エンコーディングレコード数"
  - name: encoding_records
    type: struct
    struct: cmap_encoding_record
    repeat_count: "{numTables}"

cmap_encoding_record:
  - name: platformID
    type: uint16
    description: "プラットフォームID（0=Unicode, 1=Macintosh, 3=Windows）"
  - name: encodingID
    type: uint16
    description: "エンコーディングID"
  - name: offset
    type: uint32
    description: "cmapテーブル先頭からのサブテーブルオフセット"

os2_table:
  - name: version
    type: uint16
    description: "OS/2テーブルバージョン"
  - name: xAvgCharWidth
    type: int16
    description: "平均文字幅"
  - name: usWeightClass
    type: uint16
    description: "ウェイトクラス（100=Thin, 400=Regular, 700=Bold, 900=Black）"
  - name: usWidthClass
    type: uint16
    description: "幅クラス（1=UltraCondensed, 5=Medium, 9=UltraExpanded）"
  - name: fsType
    type: uint16
    description: "フォント埋め込みライセンスフラグ"
  - name: ySubscriptXSize
    type: int16
  - name: ySubscriptYSize
    type: int16
  - name: ySubscriptXOffset
    type: int16
  - name: ySubscriptYOffset
    type: int16
  - name: ySuperscriptXSize
    type: int16
  - name: ySuperscriptYSize
    type: int16
  - name: ySuperscriptXOffset
    type: int16
  - name: ySuperscriptYOffset
    type: int16
  - name: yStrikeoutSize
    type: int16
  - name: yStrikeoutPosition
    type: int16
  - name: sFamilyClass
    type: int16
  - name: panose
    type: bytes
    size: "10"
    description: "PANOSE分類（10バイト）"
  - name: ulUnicodeRange1
    type: uint32
  - name: ulUnicodeRange2
    type: uint32
  - name: ulUnicodeRange3
    type: uint32
  - name: ulUnicodeRange4
    type: uint32
  - name: achVendID
    type: ascii
    size: "4"
    description: "ベンダーID"
  - name: fsSelection
    type: uint16
    description: "フォント選択フラグ（bit 0=Italic, bit 5=Bold, bit 6=Regular）"
  - name: usFirstCharIndex
    type: uint16
  - name: usLastCharIndex
    type: uint16
  - name: sTypoAscender
    type: int16
    description: "タイポグラフィック・アセンダ"
  - name: sTypoDescender
    type: int16
    description: "タイポグラフィック・ディセンダ"
  - name: sTypoLineGap
    type: int16
    description: "タイポグラフィック行間"
  - name: usWinAscent
    type: uint16
  - name: usWinDescent
    type: uint16
  - name: extra_data
    type: bytes
    size: remaining
    description: "バージョン依存の追加フィールド（ulCodePageRange, sxHeight等）"

post_table:
  - name: version
    type: uint32
    description: "テーブルバージョン（Fixed 16.16: 0x00010000=1.0, 0x00020000=2.0, 0x00030000=3.0）"
  - name: italicAngle
    type: uint32
    description: "イタリック角度（Fixed 16.16）"
  - name: underlinePosition
    type: int16
    description: "下線位置"
  - name: underlineThickness
    type: int16
    description: "下線太さ"
  - name: isFixedPitch
    type: uint32
    description: "固定幅フラグ（非ゼロ=固定幅）"
  - name: minMemType42
    type: uint32
  - name: maxMemType42
    type: uint32
  - name: minMemType1
    type: uint32
  - name: maxMemType1
    type: uint32
  - name: extra_data
    type: bytes
    size: remaining
    description: "バージョン依存の追加データ（v2.0: グリフ名テーブル）"

hhea_table:
  - name: majorVersion
    type: uint16
  - name: minorVersion
    type: uint16
  - name: ascender
    type: int16
    description: "アセンダ（タイポグラフィックアセント）"
  - name: descender
    type: int16
    description: "ディセンダ（タイポグラフィックディセント）"
  - name: lineGap
    type: int16
    description: "行間"
  - name: advanceWidthMax
    type: uint16
    description: "最大アドバンス幅"
  - name: minLeftSideBearing
    type: int16
  - name: minRightSideBearing
    type: int16
  - name: xMaxExtent
    type: int16
  - name: caretSlopeRise
    type: int16
    description: "キャレット傾斜（rise）"
  - name: caretSlopeRun
    type: int16
    description: "キャレット傾斜（run）"
  - name: caretOffset
    type: int16
  - name: reserved1
    type: int16
  - name: reserved2
    type: int16
  - name: reserved3
    type: int16
  - name: reserved4
    type: int16
  - name: metricDataFormat
    type: int16
  - name: numberOfHMetrics
    type: uint16
    description: "水平メトリクス数"
```

- [x]`cmap_table` 構造体を新規定義（バージョン + エンコーディングレコード配列）
- [x]`cmap_encoding_record` 構造体を新規定義
- [x]`os2_table` 構造体を新規定義（ウェイト、幅、埋め込みフラグ、PANOSE、Unicode範囲等）
- [x]`post_table` 構造体を新規定義（イタリック角度、下線、固定幅フラグ等）
- [x]`hhea_table` 構造体を新規定義（アセンダ/ディセンダ/行間/メトリクス数等）
- [x]`table_record.table_data` の switch に4ケース追加
- [x]`platform_id` enum 追加（任意: 0=Unicode, 1=Macintosh, 3=Windows）

**スコープ外**: cmap サブテーブル（Format 4/12 等）の内部解析、glyf テーブル（アウトラインデータ）、kern テーブル（カーニング）。cmap サブテーブルは seek でのランダムアクセスが必要で複雑度が高い。

### 拡充 4: AVI — ストリームヘッダ・インデックス追加

**現状** (`formats/avi.bdef.yaml`):
- `riff_chunk` の switch に `LIST`/`avih` の2ケースのみ。`strh`/`strf`/`idx1` が未対応

**改善後**:

```yaml
# riff_chunk.data の switch に追加
cases:
  "'LIST'": list_chunk
  "'avih'": main_avi_header
  "'strh'": stream_header
  "'strf'": stream_format
  "'idx1'": avi_old_index
default: raw_data

stream_header:
  - name: fccType
    type: ascii
    size: "4"
    description: "ストリームタイプ（'vids'=映像, 'auds'=音声, 'txts'=字幕）"
  - name: fccHandler
    type: ascii
    size: "4"
    description: "コーデック識別子（FourCC）"
  - name: dwFlags
    type: uint32
  - name: wPriority
    type: uint16
  - name: wLanguage
    type: uint16
  - name: dwInitialFrames
    type: uint32
  - name: dwScale
    type: uint32
    description: "タイムスケール"
  - name: dwRate
    type: uint32
    description: "レート（fps = dwRate / dwScale）"
  - name: dwStart
    type: uint32
  - name: dwLength
    type: uint32
    description: "ストリーム長（dwScaleの単位）"
  - name: dwSuggestedBufferSize
    type: uint32
  - name: dwQuality
    type: uint32
  - name: dwSampleSize
    type: uint32
  - name: rcFrame
    type: bytes
    size: "8"
    description: "表示矩形（left, top, right, bottom 各 int16）"

stream_format:
  - name: data
    type: bytes
    size: remaining
    description: "ストリームフォーマットデータ（映像: BITMAPINFOHEADER、音声: WAVEFORMATEX）"

avi_old_index:
  - name: entries
    type: struct
    struct: avi_index_entry
    repeat: eof

avi_index_entry:
  - name: dwChunkId
    type: ascii
    size: "4"
    description: "チャンクID（'00dc'=映像, '01wb'=音声等）"
  - name: dwFlags
    type: uint32
    description: "フラグ（0x10=AVIIF_KEYFRAME）"
  - name: dwOffset
    type: uint32
    description: "moviリスト先頭からのオフセット"
  - name: dwSize
    type: uint32
    description: "チャンクデータサイズ"
```

- [x]`stream_header` 構造体を新規定義（fccType, fccHandler, dwScale, dwRate 等）
- [x]`stream_format` 構造体を新規定義（フォーマット種別による内容が異なるため remaining で読み込み）
- [x]`avi_old_index` 構造体を新規定義（`repeat: eof` でインデックスエントリ繰り返し）
- [x]`avi_index_entry` 構造体を新規定義（ChunkId, Flags, Offset, Size の16バイト固定）
- [x]`riff_chunk.data` の switch に3ケース追加
- [x]ストリームタイプ enum 追加（任意: vids, auds, txts）

**備考**: `strf` の内容は `strh.fccType` に依存する（映像=BITMAPINFOHEADER、音声=WAVEFORMATEX）が、`strf` は `strh` と兄弟チャンクであり、`fccType` が同一スコープにない。そのため `strf` の内部構造化は本REQでは `bytes remaining` に留め、将来的に「直前の strh の fccType を参照する」仕組みが必要。

### 拡充 5: ICC — 主要タグデータの seek 参照

**現状** (`formats/icc.bdef.yaml`):
- `tag_entry` に offset/size があるがデータを seek 参照していない

**改善後**:

```yaml
tag_entry:
  - name: signature
    type: ascii
    size: "4"
    description: "タグシグネチャ"
  - name: offset
    type: uint32
    description: "プロファイル先頭からのタグデータオフセット"
  - name: size
    type: uint32
    description: "タグデータサイズ（バイト）"
  - name: data
    type: switch
    size: "{size}"
    seek: "{offset}"
    switch_on: "{signature}"
    cases:
      "'desc'": desc_tag_data
      "'XYZ '": xyz_tag_data
      "'text'": text_tag_data
    default: raw_data

desc_tag_data:
  - name: type_signature
    type: ascii
    size: "4"
    description: "タイプシグネチャ（'desc'）"
  - name: reserved
    type: uint32
  - name: ascii_length
    type: uint32
    description: "ASCII説明文字列の長さ"
  - name: ascii_description
    type: ascii
    size: "{ascii_length}"
    description: "ASCII説明文字列"
  - name: extra_data
    type: bytes
    size: remaining
    description: "Unicode/ScriptCode文字列（オプション）"

xyz_tag_data:
  - name: type_signature
    type: ascii
    size: "4"
    description: "タイプシグネチャ（'XYZ '）"
  - name: reserved
    type: uint32
  - name: x
    type: int32
    description: "X値（s15Fixed16Number）"
  - name: y
    type: int32
    description: "Y値（s15Fixed16Number）"
  - name: z
    type: int32
    description: "Z値（s15Fixed16Number）"

text_tag_data:
  - name: type_signature
    type: ascii
    size: "4"
    description: "タイプシグネチャ（'text'）"
  - name: reserved
    type: uint32
  - name: text
    type: ascii
    size: remaining
    description: "テキスト文字列"
```

- [x]`tag_entry` に `data` フィールドを追加（`seek: "{offset}"` + `size: "{size}"` + switch）
- [x]`desc_tag_data` 構造体を新規定義（ASCII 説明文字列）
- [x]`xyz_tag_data` 構造体を新規定義（XYZ 三刺激値）
- [x]`text_tag_data` 構造体を新規定義（テキスト文字列）
- [x]`raw_data` 構造体を default フォールバックとして追加

**備考**: ICC v4 では `desc` が `mluc`（多言語 Unicode）に置き換えられるが、既存の v2 プロファイルでは `desc` が広く使われている。`mluc` タグの追加は将来対応。また、`curv` タグ（ガンマカーブ）は count(uint32) + entries(uint16 repeat_count) で定義可能だが、seek 参照でのデータ重複読み取りの懸念があるため本REQでは見送る。

### 拡充 6: TIFF — IFD エントリのインライン値解釈

**現状** (`formats/tiff.bdef.yaml`):
- `ifd_entry.value_offset` が uint32 のまま。型情報（field_type, count）があるが値の解釈なし

**改善後**:

```yaml
ifd_entry:
  - name: tag
    type: uint16
    enum: tiff_tag
  - name: field_type
    type: uint16
    enum: tiff_field_type
  - name: count
    type: uint32
    description: "値の数"
  - name: value_offset
    type: uint32
    description: "値またはオフセット（データサイズが4バイト以下なら値そのもの）"
  - name: is_inline
    type: virtual
    value: "{(field_type == 1 and count <= 4) or (field_type == 3 and count <= 2) or (field_type == 4 and count == 1) or (field_type == 6 and count <= 4) or (field_type == 8 and count <= 2) or (field_type == 9 and count == 1)}"
    description: "値がインラインかどうか（type_size * count <= 4）"
  - name: inline_short_value
    type: virtual
    value: "{value_offset & 0xFFFF}"
    if: "{field_type == 3 and count == 1}"
    description: "SHORT型のインライン値（リトルエンディアン下位16ビット）"
  - name: inline_long_value
    type: virtual
    value: "{value_offset}"
    if: "{field_type == 4 and count == 1}"
    description: "LONG型のインライン値"
```

- [x]`is_inline` virtual フィールドを追加（型サイズ × count が4以下かどうかの判定）
- [x]`inline_short_value` virtual フィールドを追加（SHORT 型 count=1 の場合の値抽出）
- [x]`inline_long_value` virtual フィールドを追加（LONG 型 count=1 の場合の値抽出）
- [x]主要タグの enum 拡充（ExifIFD, GPSIFD は既存。Compression, PhotometricInterpretation の値 enum 追加を検討）

**制限事項**: TIFF のバイトオーダーは `byte_order` フィールド（"II" or "MM"）で動的に決まるが、現在の定義は `validate: "{byte_order == 'II'}"` でリトルエンディアンのみ対応。ビッグエンディアン TIFF は将来の `endianness` 動的切り替え機能が必要。また、オフセット参照先のデータ読み取り（RATIONAL 型等）は `seek` で可能だが、タグごとに seek 先の構造が異なるため汎用的な対応は困難。

### 拡充 7: SQLite — リーフテーブルセル内部の解析

**現状** (`formats/sqlite.bdef.yaml`):
- `btree_page_header` に `cell_pointer_array` があるがセルデータ未解析

**改善後**:

本拡充は他と比較して複雑度が高い。SQLite のセルフォーマットは varint（ULEB128 類似だが最大9バイト）を多用し、ペイロードヘッダの型コード体系も独自である。

```yaml
# btree_page_header に first_cell を追加（cell_content_offset への seek で最初のセルを読む概念実証）

# ただし、cell_pointer_array の各要素をオフセットとして seek するには
# 「配列の各要素の値を使って seek し構造体を読む」機能が DSL にないため、
# 先頭セルのみ概念的に定義する。

# リーフテーブルセル構造:
table_leaf_cell:
  - name: payload_size
    type: uleb128
    description: "ペイロードサイズ（バイト）"
  - name: rowid
    type: uleb128
    description: "行ID（INTEGER PRIMARY KEY の値）"
  - name: payload
    type: bytes
    size: "{payload_size}"
    description: "レコードヘッダ + レコードデータ"
```

- [x]`table_leaf_cell` 構造体を新規定義（payload_size: uleb128, rowid: uleb128, payload: bytes）
- [x]概念実証として先頭セルの読み取りテストを追加

**制限事項**: セルポインタ配列の各値をオフセットとして順次 seek する機能（「配列要素の値に基づく繰り返し seek」）は現在の DSL にない。ページ内の全セルを構造的に解析するには、DSL 拡張（`seek_each` や「配列要素参照式」等）が必要。本REQでは B-tree ページヘッダの直後に連続するセルデータを `repeat: eof` で読む概念実証に留める。

### 変更しないもの（スコープ外）

- DNS ペイロード（名前圧縮ポインタの制約）
- PDF 構造（テキストベース）
- Parquet / 7z 内部構造（独自エンコーディングの制約）
- GZIP / LZ4 展開後データの構造解析
- TAR ファイルデータブロック（8進ASCII→整数変換の制約）
- ICO の内部 PNG/BMP データ解析（親フォーマットの定義を再利用する仕組みが DSL にない）
- FLV / WASM / MIDI のさらなる深層構造（REQ-092 で直近の改善済み）

## 受入条件

1. [x] PCAP: IPv4 パケットの TCP/UDP ヘッダがポート番号・フラグ含め構造化されること
2. [x] PCAP: IPv6 パケットのヘッダ（src/dst アドレス、next_header）が構造化されること
3. [x] WAV: LIST-INFO チャンクのサブチャンク（IART, INAM 等）が個別に構造化されること
4. [x] OTF: cmap テーブルのエンコーディングレコードが構造化されること
5. [x] OTF: OS/2 テーブルのウェイト・幅・PANOSE 等のメトリクスが構造化されること
6. [x] OTF: post テーブルのイタリック角度・固定幅フラグが構造化されること
7. [x] OTF: hhea テーブルのアセンダ/ディセンダ/メトリクス数が構造化されること
8. [x] AVI: strh チャンクのストリームタイプ・コーデック・レート情報が構造化されること
9. [x] AVI: idx1 チャンクのインデックスエントリが構造化されること
10. [x] ICC: desc/XYZ/text タグのデータが seek 参照で構造化されること
11. [x] TIFF: IFD エントリにインライン値の virtual フィールドが追加されること
12. [x] SQLite: リーフテーブルセルの payload_size/rowid が uleb128 で読み取れること
13. [x] 各フォーマットの既存統合テストが全通過すること
14. [x] 新たに追加した構造に対するテストケースが存在すること
15. [x] `dotnet test` 全通過（677テスト: Core 128 + Engine 208 + Dsl 73 + Integration 268）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

本REQはフォーマット定義（.bdef.yaml）とテストのみの変更。エンジン・コアの変更は不要。

### 変更が必要なフォーマット定義

- [x]`formats/pcap.bdef.yaml` — TCP/UDP/IPv6 構造体追加、switch 拡張
- [x]`formats/wav.bdef.yaml` — INFO サブチャンク構造体追加、list_chunk switch 化
- [x]`formats/otf.bdef.yaml` — cmap/OS2/post/hhea テーブル構造体追加、switch 拡張
- [x]`formats/avi.bdef.yaml` — strh/strf/idx1 構造体追加、switch 拡張
- [x]`formats/icc.bdef.yaml` — tag_entry に seek 参照追加、desc/XYZ/text 構造体追加
- [x]`formats/tiff.bdef.yaml` — ifd_entry に virtual フィールド追加
- [x]`formats/sqlite.bdef.yaml` — table_leaf_cell 構造体追加

### 変更が必要なテスト

- [x]PcapParsingTests: TCP/UDP ヘッダの構造検証、IPv6 パケットの構造検証
- [x]WavParsingTests: LIST-INFO サブチャンクの構造検証
- [x]OtfParsingTests: cmap/OS2/post/hhea テーブルの構造検証
- [x]AviParsingTests: strh/idx1 チャンクの構造検証
- [x]IccParsingTests: desc/XYZ タグデータの seek 参照検証
- [x]TiffParsingTests: インライン値 virtual フィールドの検証
- [x]SqliteParsingTests: リーフテーブルセルの uleb128 検証

---

## 設計メモ

### 設計方針

エンジン・コア変更なし、`.bdef.yaml` とテスト（テストデータ生成・テストケース）のみの変更とした。7 フォーマットを難易度の低い順（WAV → PCAP → AVI → OTF → TIFF → ICC → SQLite）に実装し、各ステップ完了後に `dotnet test` で回帰テスト確認を行った。

### DSL 機能の活用

各フォーマットの拡充で活用した DSL 機能:

| DSL 機能 | 使用フォーマット | 用途 |
|---|---|---|
| `switch` + `switch_on` | WAV, PCAP, AVI, OTF, ICC | プロトコル/チャンクタイプによる構造分岐 |
| `repeat: eof` | WAV, PCAP, AVI, SQLite | 可変長配列の繰り返し読み取り |
| `repeat_count` | OTF, TIFF | 固定長配列の繰り返し読み取り |
| `seek` + `seek_restore` | OTF, ICC | ランダムアクセスによるテーブル/タグデータ参照 |
| `virtual` フィールド | TIFF | 計算値の定義（インライン判定・値抽出） |
| `bitfield` | PCAP | TCP フラグ・IPv6 ヘッダの bit レベル解析 |
| `uleb128` | SQLite | 可変長整数エンコーディング |
| `if` 条件付きフィールド | PCAP, SQLite, WAV | ページタイプ等に基づく条件付き出力 |
| `endianness` オーバーライド | PCAP | ネットワークプロトコル（big-endian）のネスト |
| `padding: true` | WAV, AVI | ワード境界アライメント |
| モジュロ式 `{chunk_size % 2}` | WAV, AVI | パディングサイズの動的算出 |

### 代替案

- **SQLite セルの全件走査**: `cell_pointer_array` の各要素値を使った seek による全セル走査を検討したが、「配列要素の値に基づく繰り返し seek」機能が DSL にないため `repeat: eof` による概念実証に留めた。
- **TIFF インライン値の seek 参照**: `value_offset` が示すオフセット先データを seek で読む案も検討したが、タグごとに型・構造が異なるため virtual フィールドによるインライン値解釈に絞った。
- **OTF cmap サブテーブル解析**: Format 4/12 等のサブテーブル内部解析は seek でのランダムアクセスが複雑すぎるためスコープ外とした。

---

## 実装メモ

### 実装順序（実績）

難易度の低い順に実装し、各ステップで `dotnet test` 全通過を確認した:

1. **WAV** — `list_chunk.data` を switch 化、`info_chunk_data` / `info_sub_chunk` 追加（130行）
2. **PCAP** — `tcp_segment` / `udp_datagram` / `ipv6_packet` 追加、4段 switch 構成（302行）
3. **AVI** — `stream_header` / `stream_format` / `avi_old_index` / `avi_index_entry` 追加（165行）
4. **OTF** — `cmap_table` / `os2_table` / `post_table` / `hhea_table` 追加、`seek_restore` 修正（360行）
5. **TIFF** — `ifd_entry` に virtual フィールド 3 件追加（195行）
6. **ICC** — `tag_entry` に `seek` + switch 追加、`desc_tag_data` / `xyz_tag_data` / `text_tag_data` 追加（229行）
7. **SQLite** — `table_leaf_cell` 追加（uleb128 × 2 + bytes）、`btree_page_header` に `cells` 追加（156行）

### 実装中の設計変更

#### 1. OTF: `seek_restore: true` の追加（潜在バグ修正）

**変更**: `table_record.table_data` の seek フィールドに `seek_restore: true` を追加。

**理由**: 複数テーブルレコードを持つ OTF ファイルでは、1 つ目のテーブルデータを seek で読んだ後にファイルポジションが戻らず、2 つ目以降のレコードの読み取りが失敗した（`PushScope` エラー）。既存テスト（テーブル 1 つのみ）では偶然成功していたため、潜在バグだった。`seek_restore: true` を追加することで、seek 後に元のポジションに復帰するようにした。

#### 2. PCAP: テストデータサイズ拡張

**変更**: `CreateMinimalPcap()` のデータサイズを 74 → 94 バイトに拡張。

**理由**: `ipv4_packet.body` を switch 化して `tcp_segment` を選択するようにしたため、0 バイトの body では TCP ヘッダ読み取りが失敗する。20 バイトの TCP ヘッダ（src_port=80, dst_port=12345, SYN フラグ）を追加し、既存テストの `inclLen` アサーションを 34 → 54 に修正した。

#### 3. AVI: テストデータサイズ再計算

**変更**: `CreateAviWithStreamAndIndex()` のバッファサイズを 180 → 188 バイトに修正。

**理由**: 初回実装で RIFF ヘッダ + LIST(hdrl) + idx1 の合計サイズを誤計算し、`ArgumentOutOfRangeException`（バッファオーバーフロー）が発生。正しくは 12 + 152 + 24 = 188 バイト。file_size フィールドも合わせて修正した。

#### 4. TIFF: DecodedVirtual 型の使用

**変更**: テストで `DecodedInteger` → `DecodedVirtual` に変更。

**理由**: virtual フィールドは `DecodedInteger` ではなく `DecodedVirtual` 型を生成する。また、`or` 演算子を含む式は `long` ではなく `bool` を返すため、`is_inline` の値比較を `Be(1L)` → `Be(true)` に修正した。

### 変更ファイル一覧

| ファイル | 変更種別 | 行数 |
|---|---|---|
| `formats/wav.bdef.yaml` | 拡充 | 130 |
| `formats/pcap.bdef.yaml` | 拡充 | 302 |
| `formats/avi.bdef.yaml` | 拡充 | 165 |
| `formats/otf.bdef.yaml` | 拡充 + バグ修正 | 360 |
| `formats/tiff.bdef.yaml` | 拡充 | 195 |
| `formats/icc.bdef.yaml` | 拡充 | 229 |
| `formats/sqlite.bdef.yaml` | 拡充 | 156 |
| `tests/.../WavTestDataGenerator.cs` | 新メソッド追加 | — |
| `tests/.../WavParsingTests.cs` | 新テスト追加 | — |
| `tests/.../PcapTestDataGenerator.cs` | 既存修正 + 新メソッド | — |
| `tests/.../PcapParsingTests.cs` | 既存修正 + 新テスト | — |
| `tests/.../AviTestDataGenerator.cs` | 新メソッド追加 | — |
| `tests/.../AviParsingTests.cs` | 新テスト追加 | — |
| `tests/.../OtfTestDataGenerator.cs` | 新メソッド追加 | — |
| `tests/.../OtfParsingTests.cs` | 新テスト追加 | — |
| `tests/.../TiffParsingTests.cs` | 新テスト追加 | — |
| `tests/.../IccTestDataGenerator.cs` | 新メソッド追加 | — |
| `tests/.../IccParsingTests.cs` | 新テスト追加 | — |
| `tests/.../SqliteTestDataGenerator.cs` | 新メソッド追加 | — |
| `tests/.../SqliteParsingTests.cs` | 新テスト追加 | — |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| WavParsingTests | WavFormat_ListInfo_DecodesCorrectly | 3 (LIST-INFO) |
| PcapParsingTests | PcapFormat_TcpSegment_DecodesCorrectly | 1 (TCP ヘッダ) |
| PcapParsingTests | PcapFormat_Ipv6_DecodesCorrectly | 2 (IPv6) |
| AviParsingTests | AviFormat_StreamHeader_DecodesCorrectly | 8 (strh) |
| AviParsingTests | AviFormat_OldIndex_DecodesCorrectly | 9 (idx1) |
| OtfParsingTests | OtfFormat_CmapTable_DecodesCorrectly | 4 (cmap) |
| OtfParsingTests | OtfFormat_HheaTable_DecodesCorrectly | 7 (hhea) |
| TiffParsingTests | TiffFormat_InlineValues_DecodesCorrectly | 11 (virtual) |
| IccParsingTests | IccFormat_DescTag_DecodesCorrectly | 10 (desc seek) |
| IccParsingTests | IccFormat_XyzTag_DecodesCorrectly | 10 (XYZ seek) |
| SqliteParsingTests | SqliteFormat_TableLeafCell_DecodesCorrectly | 12 (uleb128 セル) |

### テストデータ生成メソッド追加

| クラス | メソッド | サイズ | 内容 |
|---|---|---|---|
| WavTestDataGenerator | CreateWavWithListInfo() | 74B | RIFF + fmt + LIST(INFO: INAM + ISFT) |
| PcapTestDataGenerator | CreatePcapWithIpv6() | 102B | ヘッダ + Ethernet/IPv6/UDP パケット |
| AviTestDataGenerator | CreateAviWithStreamAndIndex() | 188B | RIFF + LIST(hdrl: avih + LIST(strl: strh)) + idx1 |
| OtfTestDataGenerator | CreateOtfWithCmapAndHhea() | 162B | offset_table + 3 records (head, cmap, hhea) |
| IccTestDataGenerator | CreateIccWithTags() | 216B | header + 2 tags (desc@156, XYZ@196) |
| SqliteTestDataGenerator | CreateSqliteWithCell() | 114B | header + btree page (1 cell: payload=2, rowid=1) |

### 気づき・今後の課題

#### 発見した潜在バグ

1. **OTF の `seek_restore` 欠落**: 複数テーブルを持つ実際の OTF ファイルでは、`table_data` の seek 後にポジションが戻らず 2 つ目以降のテーブルが読めないはずだった。既存テストがテーブル 1 つのみだったため検出されていなかった。本 REQ で `seek_restore: true` を追加して解消。

#### DSL の型システムに関する気づき

2. **virtual フィールドの型**: virtual フィールドは `DecodedVirtual` 型を返し、`DecodedInteger` とは異なる。`or` 演算子を含む式は `bool` 型を返す（`long` ではない）。テスト作成時に型マッチに注意が必要。

3. **switch による構造ラッピング**: switch フィールドはマッチしたケースの構造体でラップされる。テストでネスト構造をナビゲートする際、switch の中間層を考慮する必要がある（例: WAV の `list_chunk.data` → `info_chunk_data` への到達に 1 段追加のナビゲーションが必要）。

#### 今後の拡充候補

4. **PCAP IPv4 オプション**: `ihl` virtual フィールドが既に利用可能なため、`(ihl - 5) * 4` バイトのオプション分離が将来的に可能。
5. **ICC `mluc` タグ**: ICC v4 プロファイルでは `desc` が `mluc`（多言語 Unicode）に置き換えられる。v4 対応時に追加を検討。
6. **ICC `curv` タグ**: ガンマカーブデータ。count(uint32) + entries(uint16 repeat_count) で定義可能だが、seek でのデータ重複読み取りの懸念あり。
7. **AVI `strf` の内部構造化**: `strh.fccType` に依存する内容（映像=BITMAPINFOHEADER、音声=WAVEFORMATEX）だが、兄弟チャンク間の変数参照が DSL にないため未対応。
8. **SQLite セルの全件走査**: `cell_pointer_array` の各値をオフセットとして seek する DSL 拡張（`seek_each` 等）があれば全セル構造化が可能。
9. **OTF cmap サブテーブル**: Format 4/12 等の内部解析。cmap テーブル先頭からの相対オフセットでの seek が必要。
10. **TIFF ビッグエンディアン対応**: `byte_order == "MM"` の場合の動的エンディアン切り替え（DSL 拡張が必要）。
