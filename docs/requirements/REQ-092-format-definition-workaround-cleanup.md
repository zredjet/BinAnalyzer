# REQ-092: フォーマット定義のワークアラウンド解消

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-086（LEB128/VLQ型）、REQ-087（repeat_while） |
| 作成日 | 2026-02-09 |
| 更新日 | 2026-02-10 |

## 背景・動機

REQ-083〜089 で追加した新機能（LEB128/VLQ型、repeat_while、length_prefixed、until_marker 等）および既存機能（repeat_count、switch）を活用して、現在のフォーマット定義でワークアラウンド（`bytes remaining` や `bytes "{size}"` による未構造化読み込み）になっている箇所を構造的な定義に改善する。

全30フォーマットを調査した結果、以下の5箇所が改善可能と判明した。

### 改善対象一覧

| # | フォーマット | 箇所 | 現状 | 改善に使う機能 |
|---|---|---|---|---|
| 1 | Mach-O | `build_version_body.tools_data` | `bytes remaining` | `repeat_count` + 構造体 |
| 2 | FLAC | `cuesheet_track.indices` | `bytes "{num_indices * 12}"` | `repeat_count` + 構造体 |
| 3 | FLV | `flv_tag.data` | `bytes "{data_size}"` | `switch` + タグ種別別構造体 |
| 4 | WASM | `section.section_data` | `bytes "{section_size}"` | `switch` + `uleb128` で主要セクション内部を構造化 |
| 5 | MIDI | `mtrk.data` | `bytes "{length}"` | `vlq` + `repeat_while` でイベント列を構造化 |

### 対応不可（参考）

以下はDSLの表現力の限界により対応不可。記録として残す。

| フォーマット | 箇所 | 理由 |
|---|---|---|
| Java Class | `attribute_info.info` | 定数プールのインデックス参照解決が必要（REQ-090 調査済み） |
| DNS | `payload` | 名前圧縮ポインタ（0xC0xx）の逆参照が必要 |
| PDF | `body` | テキストベース構造で根本的にバイナリDSL対象外 |
| Parquet | `row_groups` | Thrift コンパクトプロトコル（外部シリアライゼーション形式） |
| 7z | `header_data` | 独自可変長エンコーディング（LEB128/VLQ とは異なる方式） |

## 機能要件

### 改善 1: Mach-O — build_version_body のツールエントリ構造化

**現状** (`formats/macho.bdef.yaml:387-390`):
```yaml
build_version_body:
  - name: ntools
    type: uint32
  - name: tools_data
    type: bytes
    size: remaining
    description: "ビルドツールエントリ"
```

**改善後**:
```yaml
build_version_body:
  - name: platform
    type: uint32
    enum: platform
  - name: minos
    type: uint32
    description: "最小OSバージョン"
  - name: sdk
    type: uint32
    description: "SDKバージョン"
  - name: ntools
    type: uint32
    description: "ビルドツール数"
  - name: tools
    type: struct
    struct: build_tool_entry
    repeat_count: "{ntools}"

build_tool_entry:
  - name: tool
    type: uint32
    description: "ビルドツール種別（1=clang, 2=swift, 3=ld, 4=lld）"
  - name: version
    type: uint32
    description: "ツールバージョン（major.minor.patchをエンコード）"
```

- [x] `build_tool_entry` 構造体を新規定義
- [x] `tools_data` を `tools` に変更し `repeat_count: "{ntools}"` を指定
- [ ] ~~ビルドツール種別の enum 追加（任意）~~ — 見送り。ツール種別は4値のみで利用頻度が低い

### 改善 2: FLAC — cuesheet_track のインデックスポイント構造化

**現状** (`formats/flac.bdef.yaml:263-266`):
```yaml
- name: indices
  type: bytes
  size: "{num_indices * 12}"
  description: "インデックスポイント（各12バイト）"
```

**改善後**:
```yaml
- name: indices
  type: struct
  struct: cuesheet_index
  repeat_count: "{num_indices}"

cuesheet_index:
  - name: offset
    type: uint64
    description: "トラック先頭からのサンプルオフセット"
  - name: index_number
    type: uint8
    description: "インデックスポイント番号"
  - name: reserved
    type: bytes
    size: "3"
    padding: true
    description: "予約領域"
```

- [x] `cuesheet_index` 構造体を新規定義（offset: uint64, index_number: uint8, reserved: bytes 3）
- [x] `indices` を `repeat_count: "{num_indices}"` に変更

### 改善 3: FLV — タグデータのタイプ別構造化

**現状** (`formats/flv.bdef.yaml:80-83`):
```yaml
- name: data
  type: bytes
  size: "{data_size}"
  description: "タグデータ"
```

**改善後**:
```yaml
- name: data
  type: switch
  size: "{data_size}"
  switch_on: "{tag_type}"
  cases:
    "8": audio_tag_data
    "9": video_tag_data
    "18": script_tag_data
  default: raw_data

audio_tag_data:
  - name: audio_header
    type: bitfield
    size: "1"
    fields:
      - name: sound_format
        bits: "7:4"
        description: "音声フォーマット（0=raw, 1=ADPCM, 2=MP3, 10=AAC, 11=Speex）"
      - name: sound_rate
        bits: "3:2"
        description: "サンプリングレート（0=5.5kHz, 1=11kHz, 2=22kHz, 3=44kHz）"
      - name: sound_size
        bits: "1"
        description: "サンプルサイズ（0=8bit, 1=16bit）"
      - name: sound_type
        bits: "0"
        description: "チャンネル（0=モノラル, 1=ステレオ）"
  - name: audio_data
    type: bytes
    size: remaining

video_tag_data:
  - name: video_header
    type: bitfield
    size: "1"
    fields:
      - name: frame_type
        bits: "7:4"
        description: "フレームタイプ（1=キー, 2=インター, 3=使い捨て, 4=生成キー, 5=ビデオ情報）"
      - name: codec_id
        bits: "3:0"
        description: "コーデックID（2=Sorenson H.263, 3=Screen Video, 4=VP6, 7=AVC/H.264）"
  - name: video_data
    type: bytes
    size: remaining

script_tag_data:
  - name: data
    type: bytes
    size: remaining
    description: "AMF0エンコードされたスクリプトデータ（onMetaData等）"
```

- [x] `audio_tag_data` 構造体を新規定義（audio header bitfield + 残りバイト）
- [x] `video_tag_data` 構造体を新規定義（video header bitfield + 残りバイト）
- [x] `script_tag_data` 構造体を新規定義
- [x] `data` を `switch` に変更し `tag_type` で分岐
- [x] `raw_data` 構造体を default フォールバックとして追加
- [ ] ~~音声フォーマット・フレームタイプ・コーデックIDの enum 追加（任意）~~ — 見送り。bitfield の description で値の意味を記載済み

### 改善 4: WASM — 主要セクション内部の構造化

**現状** (`formats/wasm.bdef.yaml:55-58`):
```yaml
- name: section_data
  type: bytes
  size: "{section_size}"
  description: "セクションデータ"
```

**改善後**:
```yaml
- name: section_data
  type: switch
  size: "{section_size}"
  switch_on: "{section_id}"
  cases:
    "0": custom_section_data
    "1": type_section_data
    "3": function_section_data
    "5": memory_section_data
    "7": export_section_data
  default: raw_data

type_section_data:
  - name: count
    type: uleb128
    description: "関数型エントリ数"
  - name: entries
    type: struct
    struct: func_type
    repeat_count: "{count}"

func_type:
  - name: form
    type: uint8
    description: "型形式（0x60 = func）"
    validate: "{form == 96}"
  - name: param_count
    type: uleb128
    description: "パラメータ数"
  - name: params
    type: uint8
    repeat_count: "{param_count}"
    description: "パラメータ型（0x7F=i32, 0x7E=i64, 0x7D=f32, 0x7C=f64）"
  - name: result_count
    type: uleb128
    description: "戻り値数"
  - name: results
    type: uint8
    repeat_count: "{result_count}"
    description: "戻り値型"

function_section_data:
  - name: count
    type: uleb128
  - name: type_indices
    type: uleb128
    repeat_count: "{count}"
    description: "関数がTypeセクションの何番目の型を使うかのインデックス"

memory_section_data:
  - name: count
    type: uleb128
    description: "メモリエントリ数"
  - name: entries
    type: struct
    struct: memory_type
    repeat_count: "{count}"

memory_type:
  - name: flags
    type: uleb128
    description: "リミットフラグ（0=最小のみ, 1=最小+最大）"
  - name: initial
    type: uleb128
    description: "初期ページ数（64KiB単位）"

export_section_data:
  - name: count
    type: uleb128
    description: "エクスポートエントリ数"
  - name: entries
    type: struct
    struct: export_entry
    repeat_count: "{count}"

export_entry:
  - name: name_len
    type: uleb128
  - name: name
    type: utf8
    size: "{name_len}"
    description: "エクスポート名"
  - name: kind
    type: uint8
    description: "エクスポート種別（0=function, 1=table, 2=memory, 3=global）"
  - name: index
    type: uleb128
    description: "エクスポート対象のインデックス"

custom_section_data:
  - name: name_len
    type: uleb128
  - name: name
    type: utf8
    size: "{name_len}"
    description: "カスタムセクション名"
  - name: data
    type: bytes
    size: remaining
    description: "カスタムセクションデータ"
```

- [x] `type_section_data` 構造体: `uleb128` カウント + `func_type` 繰り返し
- [x] `function_section_data` 構造体: `uleb128` カウント + type index 繰り返し
- [x] `memory_section_data` 構造体: `uleb128` カウント + `memory_type` 繰り返し
- [x] `export_section_data` 構造体: `uleb128` カウント + `export_entry` 繰り返し
- [x] `custom_section_data` 構造体: 名前 + 残りデータ
- [x] `section_data` を `switch` に変更し `section_id` で分岐
- [x] `raw_data` 構造体を default フォールバックとして追加
- [ ] ~~value type の enum 追加（任意: 0x7F=i32, 0x7E=i64, 0x7D=f32, 0x7C=f64）~~ — 見送り
- [ ] ~~export kind の enum 追加（任意）~~ — 見送り

**スコープ外**: import section, global section, element section, code section, data section, table section は内部構造が複雑なため本REQでは対象外とする。

### 改善 5: MIDI — トラックデータのイベント列構造化

**現状** (`formats/midi.bdef.yaml:58-61`):
```yaml
- name: data
  type: bytes
  size: "{length}"
  description: "MIDIイベント..."
```

**改善後**:
```yaml
- name: events
  type: struct
  struct: midi_event
  size: "{length}"
  repeat: eof

midi_event:
  - name: delta_time
    type: vlq
    description: "デルタタイム（VLQエンコード）"
  - name: status
    type: uint8
    description: "ステータスバイト"
  - name: event_data
    type: switch
    switch_on: "{status >> 4}"
    cases:
      "8": note_off_params
      "9": note_on_params
      "10": aftertouch_params
      "11": control_change_params
      "14": pitch_bend_params
    default: raw_event_data
```

- [x] `midi_event` 構造体: `vlq` デルタタイム + ステータスバイト + switch でイベント種別分岐
- [x] チャンネルイベント構造体: `two_param_event`（2バイト）、`one_param_event`（1バイト）
- [x] システムイベント: `system_event_data` → `meta_event_body` / `sysex_event_body` のネスト switch
- [x] switch-as-boundary パターンで `mtrk.data` → `track_data` に変更（`size: "{length}"` + `switch_on: "1"` で境界スコープ作成、内部で `repeat: eof`）

**制限事項**: MIDIのランニングステータス（前イベントと同じステータスの場合にステータスバイトを省略する）には対応しない。ランニングステータスを使用するMIDIファイルでは、2番目以降のイベントでパースエラーが発生する可能性がある。ランニングステータスの処理にはステートフルなデコーディングが必要であり、現在のDSLの表現力を超える。

**メタイベント（0xFF）とSysEx（0xF0/0xF7）**: メタイベントは `0xFF` + type(1byte) + length(VLQ) + data(bytes) の構造であり、VLQ型で length をパースできる。SysEx も同様に length(VLQ) + data の構造。これらは switch ケースとして追加可能。

### 変更しないもの（スコープ外）

- 対応不可フォーマットの `bytes remaining`（Java Class, DNS, PDF, Parquet, 7z）
- switch の `default: raw_data` による未知ケースのフォールバック（正常な設計）
- 圧縮データ・画像データ等の本質的にバイナリブロブである `bytes remaining`（WebP の VP8L/ALPH/ICCP/EXIF、FLAC の padding_block/application_block、WAV の data_chunk 等）

## 受入条件

1. [x] Mach-O: build_version のツールエントリが構造化されること
2. [x] FLAC: cuesheet のインデックスポイントが構造化されること
3. [x] FLV: audio/video タグのヘッダが構造化されること
4. [x] WASM: type/function/memory/export/custom セクションの内部が構造化されること
5. [x] MIDI: デルタタイム + ステータスバイトが構造化されること
6. [x] 各フォーマットの既存統合テストが全通過すること
7. [x] 新たに追加した構造に対するテストケースが存在すること（7件追加）
8. [x] `dotnet test` 全通過（666 passed, 0 failed）

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

- [x] `formats/macho.bdef.yaml` — build_tool_entry 構造体追加
- [x] `formats/flac.bdef.yaml` — cuesheet_index 構造体追加
- [x] `formats/flv.bdef.yaml` — audio/video/script タグ構造体追加、switch 化
- [x] `formats/wasm.bdef.yaml` — セクション内部構造体群追加、switch 化
- [x] `formats/midi.bdef.yaml` — midi_event 構造体群追加、switch-as-boundary パターン適用

### 変更が必要なテスト

- [x] MachoParsingTests: build_version ツールエントリの構造検証（+1テスト）
- [x] FlacParsingTests: cuesheet インデックスポイントの構造検証（+1テスト）
- [x] FlvParsingTests: audio/video タグヘッダの構造検証（+2テスト）
- [x] WasmParsingTests: type/export セクション内部の構造検証（+2テスト）
- [x] MidiParsingTests: End of Track メタイベントの構造検証（+1テスト、既存1テスト修正）

---

## 設計メモ

### 設計方針

本REQはエンジン・コア変更なし、`.bdef.yaml` とテストのみの変更。既存の DSL 機能（`repeat_count`、`switch`+`size`、`uleb128`/`vlq`、`repeat: eof`、`bitfield`）を組み合わせて5つのワークアラウンドを解消する。

### 3つのパターン

改善対象は以下の3パターンに分類できる:

| パターン | 使用箇所 | 仕組み |
|---|---|---|
| **repeat_count 置換** | Mach-O (build_tool_entry), FLAC (cuesheet_index) | `bytes` を固定長構造体 × カウントに置換。最も単純 |
| **switch による型別分岐** | FLV (tag_type), WASM (section_id) | `bytes` を `switch` + `size` に変更し、値に応じた構造体にディスパッチ。`default: raw_data` で未知ケースをフォールバック |
| **switch-as-boundary** | MIDI (track_data) | `size` と `repeat: eof` を同一フィールドに指定できないDSL制約を回避するため、`switch` で size 境界スコープを作り、内部で `repeat: eof` する。MP3 の `id3v2_body` (`formats/mp3.bdef.yaml:75-81`) が前例 |

### MIDI のネスト switch 設計

MIDI イベントは2段階の switch で分岐する:

1. **第1段階**: `status >> 4` でイベントカテゴリを分岐
   - `8`〜`11`, `14` → `two_param_event`（2バイトパラメータ）
   - `12`〜`13` → `one_param_event`（1バイトパラメータ）
   - `15` → `system_event_data`（システムメッセージ）
2. **第2段階** (`system_event_data` 内): `status` の完全値で分岐
   - `255` (0xFF) → `meta_event_body`（type + vlq length + data）
   - `240` (0xF0), `247` (0xF7) → `sysex_event_body`（vlq length + data）
   - その他 → `raw_event_data`（remaining 消費）

### 既存テストデータとの互換性検証

各改善で既存テストデータがそのまま新構造体で正しくデコードされることを事前に確認:

| フォーマット | 既存テストデータ | 互換性 |
|---|---|---|
| Mach-O | LC_UUID のみ使用 → build_version 未使用 | 影響なし |
| FLAC | STREAMINFO のみ使用 → cuesheet 未使用 | 影響なし |
| FLV | audio tag, data=0xAA(1byte) → audio_tag_data の bitfield が 0xAA を正常解釈、audio_data=0bytes | 互換 |
| WASM | type section, data=[0x01, 0x60, 0x00, 0x00] → type_section_data: count=1, func_type(form=0x60, params=0, results=0) = 4bytes | 完全互換 |
| MIDI | track length=1, data=[0x00] → **非互換**。End of Track イベントに変更必要 | テストデータ要修正 |

### 代替案

- **MIDI: `repeat_while` による条件付き繰り返し** — End of Track (0xFF 0x2F 0x00) をガードに使う案。ただし VLQ デルタタイム読み取り前に「次のバイトをピークする」機能がDSLにないため不採用。`repeat: eof` + switch-as-boundary が最も自然。

### 懸念事項

- **MIDI ランニングステータス**: 実際の MIDI ファイルでは広く使われるが、DSLでステートフルなデコーディングを表現できないため非対応。ランニングステータスを含むファイルでは2番目以降のイベントでパースエラーとなる。
- **MIDI `raw_event_data`**: 未知のシステムイベント（0xF1-0xF6 等）は `remaining` で残りバイトを消費するため、そこでトラックの解析が実質終了する。ただし正常な MIDI ファイルではこれらのイベントはまず出現しない。

---

## 実装メモ

### 実装順序

難易度の低い順に5ステップで実施。各ステップ完了後に `dotnet test` で回帰テストを確認した。

1. **Mach-O** — 固定8バイト構造体の追加のみ。最も単純。
2. **FLAC** — 固定12バイト構造体の追加のみ。Mach-Oと同等。
3. **FLV** — switch 追加 + bitfield 構造体。中程度。
4. **WASM** — 複数の uleb128 ベース構造体。中〜高。
5. **MIDI** — VLQ + ネスト switch + switch-as-boundary。最も複雑。

### 変更ファイル一覧

| ファイル | 操作 | 変更内容 |
|---|---|---|
| `formats/macho.bdef.yaml` | 修正 | `tools_data` → `tools` (repeat_count)、`build_tool_entry` 構造体追加 |
| `formats/flac.bdef.yaml` | 修正 | `indices` → struct repeat_count、`cuesheet_index` 構造体追加 |
| `formats/flv.bdef.yaml` | 修正 | `data` → switch、`audio_tag_data`/`video_tag_data`/`script_tag_data`/`raw_data` 構造体追加 |
| `formats/wasm.bdef.yaml` | 修正 | `section_data` → switch、8構造体追加（type/func_type/function/memory/memory_type/export/export_entry/custom/raw_data） |
| `formats/midi.bdef.yaml` | 修正 | `data` → `track_data` (switch-as-boundary)、8構造体追加（midi_event_list/midi_event/two_param_event/one_param_event/system_event_data/meta_event_body/sysex_event_body/raw_event_data/raw_data） |
| `tests/.../MachoTestDataGenerator.cs` | 修正 | `CreateMacho64WithBuildVersion()` 追加（64bytes, ntools=1） |
| `tests/.../MachoParsingTests.cs` | 修正 | `MachoFormat_BuildVersion_ToolEntryDecodesCorrectly` テスト追加 |
| `tests/.../FlacTestDataGenerator.cs` | 修正 | `CreateFlacWithCuesheet()` 追加（490bytes, 1 track + 1 index） |
| `tests/.../FlacParsingTests.cs` | 修正 | `FlacFormat_CuesheetIndex_DecodesCorrectly` テスト追加 |
| `tests/.../FlvTestDataGenerator.cs` | 修正 | `CreateFlvWithVideoTag()` 追加（29bytes, video tag type=9） |
| `tests/.../FlvParsingTests.cs` | 修正 | `FlvFormat_AudioTagHeader_DecodesCorrectly`、`FlvFormat_VideoTagHeader_DecodesCorrectly` テスト追加 |
| `tests/.../WasmTestDataGenerator.cs` | 修正 | `CreateWasmWithExportSection()` 追加（24bytes, type + export sections） |
| `tests/.../WasmParsingTests.cs` | 修正 | `WasmFormat_TypeSection_DecodesStructuredContent`、`WasmFormat_ExportSection_DecodesCorrectly` テスト追加 |
| `tests/.../MidiTestDataGenerator.cs` | 修正 | `CreateMinimalMidi()` を修正（23→26bytes, End of Track イベント） |
| `tests/.../MidiParsingTests.cs` | 修正 | `MidiFormat_Track_DecodesCorrectly` 修正、`MidiFormat_EndOfTrackEvent_DecodesCorrectly` テスト追加 |

### 実装中の設計変更

1. **MIDI フィールド名**: 要件定義では `mtrk.data` → `events` と記載していたが、switch-as-boundary パターンを採用したため `track_data`（switch フィールド）→ `midi_event_list`（内部構造体）→ `events`（repeat: eof の配列）という3層構造になった。これは MP3 の `id3v2_body` → `id3v2_frames` と同じパターン。

2. **MIDI チャンネルイベント構造体の統合**: 要件定義では `note_off_params`、`note_on_params`、`aftertouch_params` 等の個別構造体を想定していたが、パラメータ数が同じ（1バイト or 2バイト）イベント同士は内部構造が同一であるため、`two_param_event`（2パラメータ: Note On/Off, Aftertouch, Control Change, Pitch Bend）と `one_param_event`（1パラメータ: Program Change, Channel Aftertouch）の2構造体に統合した。個別名にしてもフィールド名が `param1`/`param2` では区別の意味が薄いため。

3. **FLV テストの child index 修正**: 当初 `flv_tag` の `data` フィールドを children[9] と仮定していたが、`data_size` virtual フィールドも children に含まれるため children[10] が正しかった。テスト実行で即座に発見し修正。

4. **WASM の `func_type` に validate 追加**: 要件定義にはなかったが、`form` フィールドに `validate: "{form == 96}"` を追加し、0x60 マーカーの検証を行うようにした。不正な WASM バイナリの早期検出に寄与する。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| MachoParsingTests | MachoFormat_BuildVersion_ToolEntryDecodesCorrectly | AC-1: ツールエントリ構造化 |
| FlacParsingTests | FlacFormat_CuesheetIndex_DecodesCorrectly | AC-2: インデックスポイント構造化 |
| FlvParsingTests | FlvFormat_AudioTagHeader_DecodesCorrectly | AC-3: audio タグヘッダ構造化 |
| FlvParsingTests | FlvFormat_VideoTagHeader_DecodesCorrectly | AC-3: video タグヘッダ構造化 |
| WasmParsingTests | WasmFormat_TypeSection_DecodesStructuredContent | AC-4: type セクション内部構造化 |
| WasmParsingTests | WasmFormat_ExportSection_DecodesCorrectly | AC-4: export セクション内部構造化 |
| MidiParsingTests | MidiFormat_EndOfTrackEvent_DecodesCorrectly | AC-5: メタイベント構造化 |

### テスト結果

```
BinAnalyzer.Core.Tests:        128 passed
BinAnalyzer.Dsl.Tests:          73 passed
BinAnalyzer.Engine.Tests:      208 passed
BinAnalyzer.Integration.Tests: 257 passed (+7 from 250 pre-REQ baseline)
合計: 666 passed, 0 failed (AC-8 充足)
```

### 気づき・今後の課題

- **switch-as-boundary パターンの汎用性**: MP3 の `id3v2_body` で初めて使われたこのパターンが、MIDI でも有効であることが確認できた。`size` + `repeat: eof` を同一フィールドに直接指定できない DSL の制約は、本パターンで実用上問題なく回避できる。ただし `switch_on: "1"` + `cases: "1": ...` という記法は意図が分かりにくいため、将来的に `scope` や `boundary` のような専用構文を検討する余地がある。

- **MIDI ランニングステータスの実用的影響**: 現代の MIDI ファイル作成ツール（DAW、シーケンサー等）の多くはランニングステータスを使用する。本REQの構造化はランニングステータスなしのファイルでのみ完全に動作する。ランニングステータス対応には「直前のステータスバイトを記憶し、先頭ビットが0の場合に省略されたとみなす」というステートフルロジックが必要であり、現在の DSL の宣言的アプローチでは表現できない。対応するには DSL に `state` 変数や `peek` 機能の追加が必要。

- **WASM のスコープ外セクション**: import(2), table(4), global(6), start(8), element(9), code(10), data(11) セクションは `raw_data` にフォールバックしている。特に code セクション（関数本体のバイトコード）は WASM デバッグで有用だが、命令セットの解析が膨大な作業になるため別REQとすべき。

- **FLV の Enhanced FLV (ExFLV)**: 近年の FLV 拡張仕様ではタグデータの先頭バイトの解釈が変わる（例: AV1/HEVC コーデック対応）。現在の bitfield 定義はクラシック FLV に基づいているため、ExFLV ファイルでは `video_header` の `codec_id` 値が仕様外の値になる可能性がある。enum + validate での対応を将来的に検討。

- **FLAC cuesheet の lead-in track**: FLAC 仕様では最終トラック（トラック番号170, 0xAA）が CD のリードアウトを表す特殊トラックである。num_indices=0 であるべきだが、現在の定義では検証していない。validate 式の追加候補。

- **テストデータの網羅性**: 各フォーマットの新構造体に対して最小限の正常系テストのみ追加した。異常系（不正なマーカー値、サイズ不一致等）のテストは既存のエンジンレベルのエラーハンドリングテストでカバーされているが、フォーマット固有の異常系テストは追加の余地がある。
