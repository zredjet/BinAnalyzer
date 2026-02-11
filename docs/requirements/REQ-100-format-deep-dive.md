# REQ-100: 既存フォーマット深掘り

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-095（実ファイル検証の結果を踏まえて対象を確定）、REQ-096/097/098（DSL 拡張後に対象が広がる） |
| フェーズ | D（応用） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

REQ-093 で7フォーマットの構造化拡充を行い、現在の DSL で表現可能な範囲はほぼ網羅した。REQ-096〜099 の DSL 拡張が完了すると、さらに以下の深掘りが可能になる:

- REQ-096（parse_int）→ TAR のファイルデータブロック走査
- REQ-097（動的エンディアン）→ TIFF ビッグエンディアン対応、ELF LE/BE 統一定義
- REQ-098（配列 seek）→ SQLite 全セル走査、PE 全セクションデータ
- REQ-099（兄弟参照）→ AVI strf の内部構造化

加えて、REQ-095 の実ファイル検証で発見される問題に対する修正もこの REQ で対応する。

## 機能要件

### 拡充候補一覧

具体的な対象は REQ-095 の結果と DSL 拡張の進捗に応じて確定する。現時点の候補:

| フォーマット | 深掘り内容 | 依存 DSL 拡張 |
|---|---|---|
| TIFF | ビッグエンディアン対応 | REQ-097 |
| TIFF | RATIONAL 型タグの値 seek 参照 | なし（既存 seek で可能） |
| ELF | LE/BE 統一定義、DWARF セクション概要 | REQ-097 |
| TAR | ファイルデータブロック走査、複数エントリ対応 | REQ-096 |
| SQLite | cell_pointer_array による全セル走査 | REQ-098 |
| AVI | strf 内部構造化（BITMAPINFOHEADER / WAVEFORMATEX） | REQ-099 |
| PCAP | IPv4 オプション分離（IHL > 5 の場合） | なし |
| ICC | mluc タグ（v4 多言語 Unicode）、curv タグ（ガンマカーブ） | なし |
| OTF | cmap サブテーブル（Format 4/12）の内部解析 | REQ-098 |
| PE | セクションデータの seek 参照 | REQ-098 |

- [x] REQ-095 の結果に基づき対象フォーマットを確定 → PCAP, TIFF, ELF, AVI の4フォーマット
- [x] 各フォーマットの .bdef.yaml を拡充
- [x] 対応するテスト（合成データ）を追加

### 変更しないもの（スコープ外）

- 新規フォーマットの追加（別REQ）
- DSL 機能の追加（REQ-096〜099 で対応）

## 受入条件

1. [x] REQ-095 で発見された問題のうち、DSL で対応可能なものが修正されていること
2. [x] DSL 拡張を活用した深掘りが最低3フォーマット以上で実施されていること → 4フォーマット（PCAP, TIFF, ELF, AVI）
3. [x] 各拡充に対するテストが追加されていること → 10件の新規テスト
4. [x] 既存テストが全て通過すること → 768件全通過

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

フォーマット定義（.bdef.yaml）とテストのみの変更。

---

## 設計メモ

4フォーマットを対象に深掘りを実施。難易度順に PCAP → TIFF → ELF → AVI の順で実装。

### PCAP — IPv4/TCP オプション分離
- `ipv4_packet` に `options` フィールド（`if: "{ihl > 5}"`、`size: "{(ihl - 5) * 4}"`）を追加
- `tcp_segment` に `options` フィールド（`if: "{data_offset > 5}"`、`size: "{(data_offset - 5) * 4}"`）を追加
- 既存 DSL のみで完結。`data_offset` は bitfield サブフィールドとして SetVariable 済み

### TIFF — RATIONAL 型 seek 参照
- `ifd_entry` に `rational_value`（field_type==5）と `srational_value`（field_type==10）を追加
- `seek: "{value_offset}"` + `seek_restore: true` で外部値を参照し IFD 読み取りに影響なし
- 新規構造体 `rational_data`（uint32 × 2）、`srational_data`（int32 × 2）

### ELF — 動的エンディアン統一定義
- ルートレベルの `endianness: little` を削除
- `elf64_body`、`elf32_body` をマップ形式に変更し `endianness: "{ei_data == 1 ? 'little' : 'big'}"` を指定
- e_ident のフィールドは全て uint8/bytes のためエンディアン不問
- JSON スキーマ（`structDefinition.endianness`）の enum 制約を削除し式文字列も許容するよう更新

### AVI — strf 内部構造化（REQ-099 兄弟参照）
- `stream_format` を `switch` 化: `switch_on: "{fccType}"` で `bitmap_info_header` / `wave_format_ex` に分岐
- `fccType` は strh デコード後に `PromoteDecodedValues` で親スコープにプロモートされ、strf で参照可能
- 新規構造体 `bitmap_info_header`（BITMAPINFOHEADER 40B + extra）、`wave_format_ex`（WAVEFORMATEX 16B + extra）

---

## 実装メモ

### 実装中の設計変更

- JSON スキーマ `bdef.schema.json` の `structDefinition.endianness` から `enum` 制約を削除。REQ-097 の動的エンディアン式を許容するため。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| PcapParsingTests | PcapFormat_Ipv4Options_DecodesCorrectly | 2, 3 |
| PcapParsingTests | PcapFormat_TcpOptions_DecodesCorrectly | 2, 3 |
| PcapParsingTests | PcapFormat_NoOptions_StillWorks | 3, 4 |
| TiffParsingTests | TiffFormat_RationalTag_SeeksAndDecodesValue | 2, 3 |
| TiffParsingTests | TiffFormat_BigEndian_RationalTag_DecodesCorrectly | 2, 3 |
| ElfParsingTests | ElfFormat_BigEndian64_DecodesCorrectly | 2, 3 |
| ElfParsingTests | ElfFormat_BigEndian32_DecodesCorrectly | 2, 3 |
| ElfParsingTests | ElfFormat_LittleEndian_StillWorksAfterDynamicEndianness | 3, 4 |
| AviParsingTests | AviFormat_VideoStreamFormat_DecodesBitmapInfoHeader | 2, 3 |
| AviParsingTests | AviFormat_AudioStreamFormat_DecodesWaveFormatEx | 2, 3 |

### 気づき・今後の課題

- SRATIONAL テストは未追加（RATIONAL のみ検証）。実ファイルでの SRATIONAL 検証は今後の課題
- AVI の strf switch はデフォルトケース（vids/auds 以外）で raw_data にフォールバック
- PCAP の IPv4 options は bytes として取得するのみ。個々のオプション（NOP, Timestamp 等）の構造化解析は今後の拡張候補
