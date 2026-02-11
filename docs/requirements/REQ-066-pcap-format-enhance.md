# REQ-066: PCAPフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-040で実装されたPCAPフォーマット定義（`formats/pcap.bdef.yaml`）では、link_typeのenum定義が不完全であり、パケットデータが未解析のraw bytesとして表示されるのみである。

ネットワークキャプチャの解析において、最も一般的なEthernet（link_type=1）フレームの構造解析は基本要件であり、さらにIPv4ヘッダの解析はプロトコル分析の起点として不可欠である。現状ではパケットデータの中身が全く見えないため、PCAPファイルの構造解析として実用性が限定的である。

また、link_typeにはRAW_IP(101)、LINUX_SLL(113)、IEEE802_11(105)等の主要なリンクレイヤータイプが未定義であり、対応するキャプチャファイルで数値のみが表示される。pcap_headerのmagic numberバリデーションも未実装のため、不正なファイルを検出できない。

本要望では、link_type enum拡充、Ethernetフレーム構造体追加、IPv4ヘッダ追加、magic numberバリデーションを行い、PCAPフォーマットの解析精度を大幅に向上させる。

## 機能要件

### 追加する機能

- [ ] link_type enum拡充
  - ETHERNET = 1
  - NULL = 0（BSD loopback）
  - RAW_IP = 101（LINKTYPE_RAW）
  - IEEE802_11 = 105（WiFi）
  - LOOP = 108（OpenBSD loopback）
  - LINUX_SLL = 113
  - LINUX_SLL2 = 276
- [ ] Ethernetフレーム構造体追加
  - link_type==1の場合にswitchで選択
  - dst_mac (bytes 6) — 宛先MACアドレス
  - src_mac (bytes 6) — 送信元MACアドレス
  - ether_type (u16 enum) — EtherType
  - payload (bytes remaining) — ペイロード
- [ ] ether_type enum定義
  - IPv4 = 0x0800
  - IPv6 = 0x86DD
  - ARP = 0x0806
  - VLAN = 0x8100
- [ ] IPv4ヘッダ構造体追加
  - version_ihl (bitfield) — version bits 7:4, ihl bits 3:0
  - tos (u8) — Type of Service
  - total_length (u16) — パケット全長
  - identification (u16) — 識別子
  - flags_fragment (bitfield) — flags bits 15:13, fragment_offset bits 12:0
  - ttl (u8) — Time to Live
  - protocol (u8 enum) — プロトコル番号
  - header_checksum (u16) — ヘッダチェックサム
  - src_ip (bytes 4) — 送信元IPアドレス
  - dst_ip (bytes 4) — 宛先IPアドレス
- [ ] protocol enum定義
  - ICMP = 1
  - TCP = 6
  - UDP = 17
- [ ] pcap_header magic numberバリデーション追加
  - `validate "{magic == 0xA1B2C3D4}"` でリトルエンディアンmagicを検証

### 変更する既存機能

- [ ] `formats/pcap.bdef.yaml` — link_type enum拡充、Ethernetフレーム構造体追加、IPv4ヘッダ追加、ether_type/protocol enum定義、magic numberバリデーション追加

### 変更しないもの（スコープ外）

- ビッグエンディアンPCAP（magic 0xD4C3B2A1）対応
- PCAPng（次世代PCAP）フォーマット対応
- IPv6ヘッダの詳細解析
- TCP/UDPヘッダの詳細解析
- ARPパケットの詳細解析
- VLAN（802.1Q）タグの詳細解析
- IPv4オプションフィールドの解析
- ナノ秒精度PCAP（magic 0xA1B23C4D）対応

## 受入条件

1. [ ] link_typeにETHERNET(1), NULL(0), RAW_IP(101), IEEE802_11(105), LOOP(108), LINUX_SLL(113), LINUX_SLL2(276)が定義されていること
2. [ ] link_type==1のパケットでEthernetフレーム（dst_mac, src_mac, ether_type, payload）がswitch解析可能であること
3. [ ] ether_type enumにIPv4(0x0800), IPv6(0x86DD), ARP(0x0806), VLAN(0x8100)が定義されていること
4. [ ] IPv4ヘッダがversion_ihl(bitfield), tos, total_length, identification, flags_fragment(bitfield), ttl, protocol(enum), header_checksum, src_ip, dst_ipとして解析可能であること
5. [ ] protocol enumにICMP(1), TCP(6), UDP(17)が定義されていること
6. [ ] pcap_headerのmagic numberに `validate "{magic == 0xA1B2C3D4}"` が追加されていること
7. [ ] 既存のPCAPヘッダ・パケットレコード解析に回帰がないこと
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なファイル

- [ ] formats/pcap.bdef.yaml — link_type enum拡充、Ethernetフレーム追加、IPv4ヘッダ追加、各enum定義、magic validate追加
- [ ] docs/architecture.md — フォーマット一覧の説明更新（PCAP Ethernet/IPv4パース対応を明記）

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
