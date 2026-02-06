# REQ-009: ZIPフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-002（ビットフィールド型: フラグフィールドで有用）、REQ-006（条件付きフィールド: オプションフィールドで有用） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

ZIPはアーカイブフォーマットとして最も普及しており、PKシグネチャで始まる複数のレコード型を持つ。ローカルファイルヘッダー、セントラルディレクトリ、エンドオブセントラルディレクトリ等、switchとrepeatの組み合わせが必要な構造であり、DSLの表現力の検証に適している。

ZIP構造:
- ローカルファイルヘッダー群（PK\x03\x04 + メタデータ + 圧縮データ）
- セントラルディレクトリ群（PK\x01\x02 + メタデータ）
- エンドオブセントラルディレクトリ（PK\x05\x06 + サマリー）

## 機能要件

### 追加する機能

- [ ] `formats/zip.bdef.yaml` の新規作成
- [ ] 対応するZIP構造:
  - ローカルファイルヘッダー
    - シグネチャ（PK\x03\x04）、バージョン、フラグ、圧縮方式
    - ファイル名長、拡張フィールド長、ファイル名、圧縮データ
  - セントラルディレクトリエントリ
    - シグネチャ（PK\x01\x02）、バージョン、フラグ、圧縮方式
    - CRC-32、圧縮サイズ、非圧縮サイズ、ファイル名
  - エンドオブセントラルディレクトリ
    - シグネチャ（PK\x05\x06）、エントリ数、ディレクトリサイズ、オフセット
  - PKシグネチャによるswitch分岐
- [ ] ZIP用のenum定義:
  - 圧縮方式（0=stored, 8=deflated, 14=LZMA等）
  - OS/バージョン
- [ ] エンディアン指定: `endianness: little`
- [ ] テストデータ生成: `ZipTestDataGenerator` の作成（無圧縮ZIPの最小構造）

### 変更する既存機能

- なし

### 変更しないもの（スコープ外）

- ZIP64拡張
- 暗号化ZIPの解析
- 圧縮データの展開
- スプリットアーカイブ

## 受入条件

1. [ ] `formats/zip.bdef.yaml` が `YamlFormatLoader` で正しく読み込めること
2. [ ] バリデーター（REQ-001実装済みの場合）でエラーが0件であること
3. [ ] 最小ZIPバイナリ（1ファイル、無圧縮）がデコードできること
4. [ ] 各シグネチャ（PK\x03\x04, PK\x01\x02, PK\x05\x06）が検証されること
5. [ ] ファイル名が正しくデコードされること
6. [ ] 圧縮方式のenum表示が正しいこと
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| BinAnalyzer.Integration.Tests | `ZipTestDataGenerator`, ZIPパーステスト追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 対応フォーマット一覧にZIPを追記

---

## 設計メモ

### 設計方針

フォーマット定義ファイル（`formats/zip.bdef.yaml`）の追加。ZIPの構造はPKシグネチャ（4バイト）による先頭判定でレコード種別をswitchする。repeat: eof で全レコードを順次デコード。

### DSL構造設計

```yaml
name: ZIP
endianness: little
root: zip

enums:
  compression_method:
    - value: 0
      label: stored
    - value: 8
      label: deflated
    - value: 14
      label: LZMA

structs:
  zip:
    - name: records
      type: struct
      struct: pk_record
      repeat: eof

  pk_record:
    - name: signature
      type: uint32
    - name: body
      type: switch
      size: remaining
      switch_on: "{signature}"
      cases:
        "0x04034B50": local_file_header      # PK\x03\x04
        "0x02014B50": central_directory_entry # PK\x01\x02
        "0x06054B50": end_of_central_dir     # PK\x05\x06
      default: raw_data

  local_file_header:
    - name: version_needed
      type: uint16
    - name: flags
      type: uint16
    - name: compression
      type: uint16
      enum: compression_method
    - name: mod_time
      type: uint16
    - name: mod_date
      type: uint16
    - name: crc32
      type: uint32
    - name: compressed_size
      type: uint32
    - name: uncompressed_size
      type: uint32
    - name: filename_length
      type: uint16
    - name: extra_length
      type: uint16
    - name: filename
      type: ascii
      size: "{filename_length}"
    - name: extra
      type: bytes
      size: "{extra_length}"
    - name: data
      type: bytes
      size: "{compressed_size}"

  central_directory_entry:
    - name: version_made_by
      type: uint16
    - name: version_needed
      type: uint16
    - name: flags
      type: uint16
    - name: compression
      type: uint16
      enum: compression_method
    - name: mod_time
      type: uint16
    - name: mod_date
      type: uint16
    - name: crc32
      type: uint32
    - name: compressed_size
      type: uint32
    - name: uncompressed_size
      type: uint32
    - name: filename_length
      type: uint16
    - name: extra_length
      type: uint16
    - name: comment_length
      type: uint16
    - name: disk_number_start
      type: uint16
    - name: internal_attributes
      type: uint16
    - name: external_attributes
      type: uint32
    - name: local_header_offset
      type: uint32
    - name: filename
      type: ascii
      size: "{filename_length}"
    - name: extra
      type: bytes
      size: "{extra_length}"
    - name: comment
      type: ascii
      size: "{comment_length}"

  end_of_central_dir:
    - name: disk_number
      type: uint16
    - name: start_disk
      type: uint16
    - name: entries_on_disk
      type: uint16
    - name: total_entries
      type: uint16
    - name: directory_size
      type: uint32
    - name: directory_offset
      type: uint32
    - name: comment_length
      type: uint16
    - name: comment
      type: ascii
      size: "{comment_length}"

  raw_data:
    - name: data
      type: bytes
      size: remaining
```

### テストデータ設計

`ZipTestDataGenerator.CreateMinimalZip()`:
- 1ファイル（"hello.txt"、無圧縮、内容 "Hello"）
- Local file header + file data + Central directory entry + End of central directory
- CRC-32は正しい値を計算して埋め込む

### 代替案

なし。

### 懸念事項

1. **switchの `size: remaining` の問題**: `pk_record` のbodyにsizeを指定する必要があるが、レコード全体のサイズは先読みしないと分からない。`size: remaining` を使用すると、最初のレコードが残り全バイトを消費してしまう。**これは設計上の課題**であり、以下の対策が必要:
   - local_file_headerとcentral_directory_entryはフィールドの合計サイズで自動的に消費量が決まるため、switchのsizeバウンダリがなくてもデコード可能かもしれない。ただし現在のswitch実装は必ずsizeを要求する。
   - **対策案**: switch型でsize指定なしを許容し、内部structのデコード結果のサイズをそのまま採用する機能拡張が必要になる可能性がある。実装時に検証する。

2. **Data Descriptor**: ZIP仕様ではフラグのbit 3が立っている場合、compressed_sizeとcrc32がlocal file headerではなくデータの後にData Descriptorとして配置される。この場合、`size: "{compressed_size}"` が0になる。初期実装では非対応（Data Descriptorなしのzipのみ対応）。

---

## 実装メモ

### 実装中の設計変更

- **switchでsize指定なしを許容する機能拡張**: 設計メモの懸念事項1で指摘されていた通り、`size: remaining`だと最初のレコードが全バイトを消費する問題が発生。`BinaryDecoder.DecodeSwitchField`を修正し、size指定がない場合はバウンダリスコープを使わず、内部structが消費したバイト数をそのまま採用するようにした。
- DSL定義の`pk_record.body`から`size`属性を削除。既存のPNG/WAV/BMP定義（sizeあり）は影響なし。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `DecodeSwitchField`でsize指定なしswitchをサポート |
| `formats/zip.bdef.yaml` | ZIPフォーマット定義（新規） |
| `tests/.../ZipTestDataGenerator.cs` | 最小ZIPバイナリ生成（新規） |
| `tests/.../ZipParsingTests.cs` | ZIP統合テスト6件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ZipParsingTests | ZipFormat_LoadsWithoutErrors | AC-1, AC-2 |
| ZipParsingTests | ZipFormat_DecodesMinimalZip | AC-3 |
| ZipParsingTests | ZipFormat_LocalFileHeader_DecodesCorrectly | AC-4, AC-5, AC-6 |
| ZipParsingTests | ZipFormat_CentralDirectory_DecodesCorrectly | AC-4, AC-5 |
| ZipParsingTests | ZipFormat_EndOfCentralDir_DecodesCorrectly | AC-4 |
| ZipParsingTests | ZipFormat_TreeOutput_ContainsExpectedElements | AC-5, AC-6 |

### 受入条件の充足状況

- [x] AC-1: `formats/zip.bdef.yaml` が正しく読み込める
- [x] AC-2: バリデーターでエラー0件
- [x] AC-3: 最小ZIPバイナリがデコードできる
- [x] AC-4: 各シグネチャ（PK\x03\x04, PK\x01\x02, PK\x05\x06）が検証される
- [x] AC-5: ファイル名が正しくデコードされる
- [x] AC-6: 圧縮方式のenum表示が正しい（stored等）
- [x] AC-7: 既存テスト全て通過（181テスト合格）

### 気づき・今後の課題

- **switchのsize省略機能**: この拡張により、ZIPのようなTLV（Tag-Length-Value）構造でsizeバウンダリなしのswitchが使えるようになった。PNGのようにsizeで区切られるswitchとの使い分けが可能。
- Data Descriptorフラグ（bit 3）のサポートは未対応。実際のZIPファイルではData Descriptorが使われることがあり、その場合`compressed_size`が0になるため`data`フィールドが空になる。
- ZIP64拡張は未対応。
