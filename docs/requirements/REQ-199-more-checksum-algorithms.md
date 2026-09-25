# REQ-199: インターネットチェックサム・Ogg の CRC-32・32 ビットの合計のチェックサムを足す

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
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

- [x] **整数系のアルゴリズムを 3 つ足す**（`internet-checksum`・`crc32-ogg`・`sum32-be`）
  - `internet-checksum`: RFC 1071。16 ビットのビッグエンディアンの語の 1 の補数の和の 1 の補数。長さが奇数なら最後に 0 のバイトを足す。16 ビットのフィールドに指定する
  - `crc32-ogg`: 上の表の CRC-32。32 ビットのフィールドに指定する
  - `sum32-be`: ビッグエンディアンの uint32 の合計（2^32 で割った余り）。長さが 4 の倍数でなければ 0 で埋めて計算する。32 ビットのフィールドに指定する
- [x] ChecksumAlgorithms の一覧・Engine の計算・DSL リファレンスの対応アルゴリズムの表に足す（REQ-187 の同期のテストで固定されている）

### 変更する既存機能

- [x] **同梱定義で検証する**: pcap の IPv4 のヘッダのチェックサムと ICMP（v4）のチェックサム、ogg のページの CRC、otf の表のレコードのチェックサム（head 以外）。各定義の「対応していないもの」から外す

### 変更しないもの（スコープ外）

- TCP / UDP / ICMPv6 のチェックサム。擬似ヘッダ（送信元・宛先のアドレス、プロトコル番号、長さ）は連続したバイト列でなく、長さなどは計算した値なので、範囲だけでは表せない。擬似ヘッダを入力に足す仕組みは別の要望とする
- OTF の head の表のチェックサム（checksumAdjustment を 0 として計算する）と、ファイル全体の checksumAdjustment（0xB1B0AFBA から全体の合計を引いた値。「計算した値 = フィールド」の形でない）
- 転送時にチェックサムを 0 にする UDP（IPv4 の UDP の 0 は「チェックサム無し」）の扱い

## 受入条件

1. [x] `internet-checksum` が RFC 1071 の例（`00 01 f2 03 f4 f5 f6 f7` の和 0xddf2、チェックサム 0x220d）と一致し、奇数の長さを 0 で埋めること
2. [x] `crc32-ogg` が、ffmpeg が書いた Ogg のページの CRC と一致すること（実装メモ参照。ffmpeg の muxer は libogg でなく libavformat）
3. [x] `sum32-be` が、fontTools が計算した表のチェックサム（macOS のシステムのフォント）と一致し、長さが 4 の倍数でない表を 0 で埋めて計算すること
4. [x] pcap の IPv4 のヘッダのチェックサム・ICMP のチェックサム、ogg のページの CRC、otf の表のチェックサムが、正しいファイルで ✓、1 バイト書き換えたファイルで ✗ になること
5. [x] DSL リファレンスの対応アルゴリズムの表と ChecksumAlgorithms・Engine の計算が一致すること（既存の同期のテスト）
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumAlgorithms` に 3 つ |
| BinAnalyzer.Engine | `ChecksumCalculators` に 3 つの計算（`SumChecksumCalculator`・`Crc32Calculator.ComputeOgg`） |
| formats/pcap・ogg・otf.bdef.yaml | `checksum:` の指定、先頭コメント |
| tests/BinAnalyzer.Engine.Tests / Integration.Tests / Dsl.Tests | アルゴリズムの値のテスト、定義の ✓ / ✗ のテスト、正しいチェックサムを書く Generator、アルゴリズム名の読み込み |
| testdata/golden | pcap・ogg・otf（先読みの作業用の値と検証の結果） |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 対応アルゴリズムの表（doc-sync）
- [x] schemas/bdef.schema.json — チェックサムのアルゴリズムの enum と説明

---

## 設計メモ

### 設計方針

- pcap の IPv4: ヘッダの先頭からの位置を `_offset` で求め、`range` のサイズを `ihl * 4` にして `exclude_self: true`
- ogg: ページの先頭からの位置を `_offset` で求め、`range` のサイズを `27 + num_segments + body_size` にして `exclude_self: true`（CRC のフィールドはヘッダの中にある）
- otf: 表のレコードの `checksum` に `range: {offset: "{offset}", size: "{length}"}`、`if` で head を除く（範囲の外の 0 埋めはアルゴリズムが行う）

（実装では ogg と otf の範囲をチェックサムの欄より後ろのフィールドで決められなかった。実装メモ参照）

### 懸念事項

- アルゴリズムの名前: 一般的な呼び名（`CRC-32/OGG` は reveng のカタログに無い）と、`crc32` との混同。パラメータを表示名に書く

---

## 実装メモ

### 実装中の設計変更

- **名前と表示名**: `internet-checksum`（Internet Checksum）・`crc32-ogg`（CRC-32/Ogg）・`sum32-be`（Sum-32/BE）。Ogg の CRC のパラメータ（多項式 0x04C11DB7・非反転・初期値 0・最後の XOR 無し）は reveng のカタログに名前が無いので、使う形式の名前を付けた。検査値（"123456789"）は 0x89A1897F。DSL リファレンスに、`crc32` との違いを含めて 3 つの計算の表を足した
- **チェックサムの範囲は、その欄を読んだ時点の変数で決まる**。ogg の CRC の範囲（ページ全体）はこの後ろのセグメント表、otf の表のチェックサムの範囲はこの後ろの offset / length で決まるので、設計方針の書き方では求められなかった。**`seek` + `seek_restore` で先に覗く作業用のフィールド**（`padding: true`。ツリーには出ない）を置いた。ogg は `peek_num_segments` と `peek_segment_table`、otf は `peek_offset` と `peek_length`。範囲の式を構造体の終わりに遅らせて評価するエンジンの変更も考えたが、既存のチェックサムの `_offset` などの意味が変わり、パッチの再計算（BinaryPatcher）にも関わるので採らなかった。JSON とゴールデンには先読みの値が増える
- **範囲の先頭は、チェックサムの欄を読んだ後の `_offset` から引いて求める**（IPv4 は `_offset - 12`、ICMP は `_offset - 4`、ogg は `_offset - 26`）。先頭の位置を取っておく作業用の値が要らない。ICMP の範囲の終わりは `remaining`（IPv4 の total_length で区切ったスコープの残り）
- **途中で切れたデータ**: IPv4 と ogg の範囲の大きさは、残りのバイト数で抑えた（範囲がデータの外に出るとデコードエラーになるため）。スナップ長で切れたパケット・途中で切れた Ogg のページは ✗ になる。otf は抑えていない（表がデータの外にあれば、表の中身の seek も元からデコードエラーになる）
- **ICMPv6 は検証しない**: 同じ `icmp_message` を ICMP と ICMPv6 で使っているので、`if:` が排他の同じ名前の 2 つの `checksum`（`version == 4` のときだけ検証）に分けた。TCP・UDP のチェックサムの説明に、擬似ヘッダを含むので検証しないことを書いた
- **otf の head**: `if:` が排他の 2 つの `checksum` に分け、head は検証しない（`ChecksumValid` が null）
- **Generator**: pcap の ICMP のエコーにチェックサムを書き（IPv4 のヘッダのチェックサムを書く処理を共通のヘルパーにした）、otf の `Sfnt` が表のチェックサムを書くようにした（head 以外）。ICMPv6 の見本（`CreateIcmpv6Pcap`）を足した。ogg の `CreateOpusOgg` は元から正しい CRC を書いている
- **ゴールデン**: 手で作った test.pcap（IPv4 のヘッダのチェックサムが 0）と test.ogg（CRC が 0）は ✗（`checksum_valid: false`）になった。test.otf は head だけなので検証しない。ゴールデンの入力は変えていない

### 照合

- **Ogg**: ffmpeg 9.0（Ogg の muxer は libavformat。libogg ではない）が書いた Opus・FLAC・Vorbis（ネイティブのエンコーダ）の 11 ページがすべて ✓。1 バイト書き換えたページだけ ✗。ffmpeg の Opus の最初のページを Engine のテストの値にした。手元に libogg で書くツール（oggenc）が無かった
- **OTF**: macOS のシステムのフォント 21 個（fontTools が「チェックサムが合わない」と判定する表を持つ 8 個を含む）で、表ごとの ✓ / ✗ が fontTools 4.63 の `sfnt.calcChecksum` の判定と一致した。TTC（ThonburiUI.ttc）の 32 の表も ✓。システムのフォント 254 個のうち 73 の表は、フォントに書かれた値が合わない（fontTools でも同じ）
- **pcap**: 手元の見本で、IPv4 のヘッダと ICMP の判定が tcpdump 4.99 の `-vvv` と一致した（tcpdump が `wrong icmp cksum 0 (->1927)` と出す ICMP は ✗ で、期待値 0x1927）。Generator の見本も tcpdump が誤りを出さないことを確かめた

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SumChecksumCalculatorTests | Internet_MatchesTheRfc1071Example | 1 |
| SumChecksumCalculatorTests | Internet_OfAnIpv4HeaderWithTheFieldZeroed_IsTheStoredChecksum | 1 |
| SumChecksumCalculatorTests | Internet_OddLength_PadsWithAZeroByte・Internet_CarriesTheOverflowIntoTheLowBits | 1 |
| Crc32OggCalculatorTests | CheckValue・DiffersFromTheZipCrc32・MatchesAPageWrittenByFfmpeg | 2 |
| SumChecksumCalculatorTests | Sum32_MatchesFontTools・Sum32_WrapsAround2To32・Sum32_LengthNotAMultipleOf4_PadsWithZeros | 3 |
| PcapParsingTests | PcapFormat_Ipv4HeaderAndIcmpChecksums_AreVerified | 4 |
| PcapParsingTests | PcapFormat_ChangedByte_FailsOnlyTheChecksumThatCoversIt | 4 |
| PcapParsingTests | PcapFormat_Icmpv6Checksum_IsNotVerified | スコープ外（ICMPv6） |
| OggParsingTests | OggFormat_PageCrc_IsVerified・OggFormat_PageCrc_FailsWhenABodyByteChanges | 4 |
| OtfParsingTests | OtfFormat_TableChecksums_AreVerifiedExceptHead・OtfFormat_TableChecksum_FailsWhenATableByteChanges | 3・4 |
| OtfParsingTests | OtfFormat_Collection_VerifiesTheSharedTablesInEachFont | 4 |
| ChecksumCalculatorsTests・ChecksumAlgorithmsTests・DocConsistency（既存） | 名前と計算と DSL リファレンスの表の一致 | 5 |
| ChecksumParsingTests | Load_NewAlgorithmNames_ParsesCorrectly（3 つの名前を追加） | 5 |
