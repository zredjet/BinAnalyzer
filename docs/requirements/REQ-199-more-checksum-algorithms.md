# REQ-199: インターネットチェックサム・Ogg の CRC-32・32 ビットの合計のチェックサムを足す

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | REQ-187（チェックサムのアルゴリズムの一覧）、REQ-194（`_offset`: 範囲の先頭の位置を求める） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

REQ-188 の見直しで、チェックサムのアルゴリズムが無いために検証できないものが 3 つの定義に残った（各定義の「対応していないもの」）。

| 定義 | チェックサム | アルゴリズム |
|---|---|---|
| pcap | IPv4 のヘッダのチェックサム・ICMP のチェックサム（TCP / UDP / ICMPv6 は擬似ヘッダを含む） | インターネットチェックサム（RFC 1071: 16 ビットの 1 の補数の和の 1 の補数） |
| ogg | ページの CRC | CRC-32（多項式 0x04C11DB7、初期値 0、入力・出力を反転しない、最後の XOR 無し） |
| otf | 表のレコードのチェックサム | ビッグエンディアンの uint32 の合計（2^32 で割った余り。表の長さが 4 の倍数でなければ 0 で埋める） |

どれも範囲（`checksum:` の `range` / `ranges`）と `exclude_self` の既存の仕組みに、アルゴリズムを足すだけで検証できる。範囲の先頭は、REQ-194 の `_offset` で取っておいた位置で書ける。

## 機能要件

### 追加する機能

- [ ] **整数系のアルゴリズムを 3 つ足す**（名前は設計で決める。案: `internet-checksum`・`crc32-ogg`・`sum32-be`）
  - `internet-checksum`: RFC 1071。16 ビットのビッグエンディアンの語の 1 の補数の和の 1 の補数。長さが奇数なら最後に 0 のバイトを足す。16 ビットのフィールドに指定する
  - `crc32-ogg`: 上の表の CRC-32。32 ビットのフィールドに指定する
  - `sum32-be`: ビッグエンディアンの uint32 の合計（2^32 で割った余り）。長さが 4 の倍数でなければ 0 で埋めて計算する。32 ビットのフィールドに指定する
- [ ] ChecksumAlgorithms の一覧・Engine の計算・DSL リファレンスの対応アルゴリズムの表に足す（REQ-187 の同期のテストで固定されている）

### 変更する既存機能

- [ ] **同梱定義で検証する**: pcap の IPv4 のヘッダのチェックサムと ICMP（v4）のチェックサム、ogg のページの CRC、otf の表のレコードのチェックサム（head 以外）。各定義の「対応していないもの」から外す

### 変更しないもの（スコープ外）

- TCP / UDP / ICMPv6 のチェックサム。擬似ヘッダ（送信元・宛先のアドレス、プロトコル番号、長さ）は連続したバイト列でなく、長さなどは計算した値なので、範囲だけでは表せない。擬似ヘッダを入力に足す仕組みは別の要望とする
- OTF の head の表のチェックサム（checksumAdjustment を 0 として計算する）と、ファイル全体の checksumAdjustment（0xB1B0AFBA から全体の合計を引いた値。「計算した値 = フィールド」の形でない）
- 転送時にチェックサムを 0 にする UDP（IPv4 の UDP の 0 は「チェックサム無し」）の扱い

## 受入条件

1. [ ] `internet-checksum` が RFC 1071 の例（`00 01 f2 03 f4 f5 f6 f7` の和 0xddf2、チェックサム 0x220d）と一致し、奇数の長さを 0 で埋めること
2. [ ] `crc32-ogg` が、libogg が書いた Ogg のページ（ffmpeg の出力）の CRC と一致すること
3. [ ] `sum32-be` が、fontTools が計算した表のチェックサム（macOS のシステムのフォント）と一致し、長さが 4 の倍数でない表を 0 で埋めて計算すること
4. [ ] pcap の IPv4 のヘッダのチェックサム・ICMP のチェックサム、ogg のページの CRC、otf の表のチェックサムが、正しいファイルで ✓、1 バイト書き換えたファイルで ✗ になること
5. [ ] DSL リファレンスの対応アルゴリズムの表と ChecksumAlgorithms・Engine の計算が一致すること（既存の同期のテスト）
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumAlgorithms` に 3 つ |
| BinAnalyzer.Engine | `ChecksumCalculators` に 3 つの計算 |
| formats/pcap・ogg・otf.bdef.yaml | `checksum:` の指定、先頭コメント |
| tests/BinAnalyzer.Engine.Tests / Integration.Tests | アルゴリズムの値のテスト、定義の ✓ / ✗ のテスト |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 対応アルゴリズムの表（doc-sync）
- [ ] schemas/bdef.schema.json — チェックサムのアルゴリズムの enum と説明

---

## 設計メモ

### 設計方針（案）

- pcap の IPv4: ヘッダの先頭（`ip_packet` の先頭）で `_offset` を取っておき、`range: {offset: "{header_start}", size: "{ihl * 4}"}` + `exclude_self: true`
- ogg: ページの先頭で `_offset` を取っておき、`range: {offset: "{page_start}", size: "{27 + num_segments + body_size}"}` + `exclude_self: true`（CRC のフィールドはヘッダの中にある）
- otf: 表のレコードの `checksum` に `range: {offset: "{offset}", size: "{length}"}`、`if` で head を除く（範囲の外の 0 埋めはアルゴリズムが行う）

### 懸念事項

- アルゴリズムの名前: 一般的な呼び名（`CRC-32/OGG` は reveng のカタログに無い）と、`crc32` との混同。パラメータを表示名に書く
