# REQ-040: PCAPフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

PCAP（Packet Capture）はネットワークパケットキャプチャの標準フォーマット。tcpdump, Wireshark等のツールで広く使用される。

グローバルヘッダ + パケットヘッダ/データの繰り返しというシンプルな構造で、BinAnalyzerの繰り返し機能で定義可能。ネットワーク解析やセキュリティ調査に有用。

マジックナンバー（0xA1B2C3D4 or 0xD4C3B2A1）でエンディアンが決まるが、当面はビッグエンディアン（ネイティブバイトオーダー）固定で対応する。

## 機能要件

### 追加する機能

- [ ] `formats/pcap.bdef.yaml` フォーマット定義
  - グローバルヘッダ（24バイト）
    - magic number（0xA1B2C3D4）
    - バージョン（major, minor）
    - タイムゾーンオフセット、シグフィグ（通常0）
    - snaplen（最大パケット長）
    - link type（データリンクタイプ、enum）
  - パケットレコードの繰り返し（repeat: until_eof）
    - パケットヘッダ（タイムスタンプ秒・マイクロ秒、キャプチャ長、元パケット長）
    - パケットデータ（キャプチャ長分のraw bytes）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- PcapNG（次世代フォーマット）の対応
- リトルエンディアンPCAP（マジック 0xD4C3B2A1）の自動判定
- パケット内容（Ethernet/IP/TCP等）の解析
- ナノ秒精度PCAP（マジック 0xA1B23C4D）

## 受入条件

1. [ ] `formats/pcap.bdef.yaml` が存在すること
2. [ ] グローバルヘッダが正しく解析できること
3. [ ] パケットレコードが繰り返し解析できること
4. [ ] 各パケットのタイムスタンプとデータ長が抽出できること
5. [ ] 有効なPCAPファイルでデコードエラーにならないこと
6. [ ] フォーマット定義がバリデーションに通ること
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

### 変更が必要なドキュメント

- [ ] README.md — 対応フォーマットにPCAPを追記
- [ ] docs/architecture.md — formats/ ディレクトリに pcap.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: PCAP
endianness: little
root: pcap
```

**主要struct:**

- **`pcap`** (root) — ファイル全体
  - `header`: struct=pcap_global_header
  - `packets`: struct=pcap_packet, repeat=eof
- **`pcap_global_header`** — グローバルヘッダ（24バイト）
  - `magic`: bytes, size=4, expected=[0xD4,0xC3,0xB2,0xA1]
  - `version_major`: u16
  - `version_minor`: u16
  - `thiszone`: i32
  - `sigfigs`: u32
  - `snaplen`: u32
  - `network`: u32, enum=link_type
- **`pcap_packet`** — パケットレコード
  - `ts_sec`: u32
  - `ts_usec`: u32
  - `incl_len`: u32
  - `orig_len`: u32
  - `data`: bytes, size="{incl_len}"

### enum定義

- **`link_type`**: 1=LINKTYPE_ETHERNET, 101=LINKTYPE_RAW, 113=LINKTYPE_LINUX_SLL, 228=LINKTYPE_IPV4, 229=LINKTYPE_IPV6

### switchの使い方

switchは使用しない。パケットレコードは全て同一構造（ヘッダ+データ）で、データリンクタイプによる分岐はスコープ外。

### 繰り返しの方式

パケットレコードの繰り返しに `repeat: eof` を使用。パケット数はヘッダに記録されないため、ファイル末尾まで読み進める。

### 注意点・制約

- **エンディアン**: PCAP のマジックナンバー 0xA1B2C3D4 はファイルのネイティブバイトオーダーを示す。x86環境で作成されたファイルはリトルエンディアンが大多数のため、`endianness: little` とし、マジックバイト列は [0xD4,0xC3,0xB2,0xA1]（リトルエンディアンでのバイト順）で定義する。
- **ビッグエンディアンPCAP**: マジックが [0xA1,0xB2,0xC3,0xD4] のファイルには対応しない。エンディアン自動判定はDSLの機能外であり、スコープ外とする。
- **PcapNG非対応**: 新世代のpcapngフォーマット（マジック 0x0A0D0D0A）は別フォーマット定義が必要。
- **パケットデータ内部**: Ethernet/IP/TCPヘッダの解析は行わず、`data` は生bytesとして出力する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
