# REQ-078: DNSパケットフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

DNS（Domain Name System）パケットはネットワーク解析・セキュリティ調査において頻繁に参照されるプロトコルフォーマット。pcapファイルと組み合わせてのネットワーク解析に有用。

ヘッダのflagsフィールドにbitfield、questions/answersセクションにrepeat_countを活用する例として適している。RFC 1035で定義される比較的シンプルな構造を持つ。

DNS構造:
- ヘッダ（12バイト固定: transaction_id, flags, 4つのカウントフィールド）
- Questionsセクション（qd_count個）
- Answersセクション（an_count個）— DNS名前圧縮があるためスコープ制限あり
- Authority/Additionalセクション — 同上

## 機能要件

### 追加する機能

- [ ] `formats/dns.bdef.yaml` フォーマット定義
  - ヘッダ（12バイト）
    - transaction_id（u16、トランザクションID）
    - flags（bitfield）
      - qr（bit 15、Query=0 / Response=1）
      - opcode（bits 14:11、標準クエリ=0, 逆クエリ=1, ステータス=2）
      - aa（bit 10、Authoritative Answer）
      - tc（bit 9、Truncation）
      - rd（bit 8、Recursion Desired）
      - ra（bit 7、Recursion Available）
      - z（bits 6:4、予約、常に0）
      - rcode（bits 3:0、応答コード: NoError=0, FormErr=1, ServFail=2, NXDomain=3, NotImp=4, Refused=5）
    - qd_count（u16、Questionセクションのエントリ数）
    - an_count（u16、Answerセクションのエントリ数）
    - ns_count（u16、Authorityセクションのエントリ数）
    - ar_count（u16、Additionalセクションのエントリ数）
  - Questions（repeat_count qd_count）
    - qname（bytes remaining、description "DNS name encoding - label length + label bytes, terminated by 0x00"）
    - qtype（u16、enum: A=1, NS=2, CNAME=5, SOA=6, PTR=12, MX=15, TXT=16, AAAA=28, SRV=33, ANY=255）
    - qclass（u16、enum: IN=1, CS=2, CH=3, HS=4, ANY=255）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- DNS名前圧縮（ポインタ 0xC0xx）の展開 — DSLの表現力を超えるため
- Answer/Authority/Additionalセクションの詳細解析（名前圧縮が必須のため）
- EDNS0（OPT レコード）の解析
- DNSSEC関連フィールドの解析
- TCPストリーム上のDNS（長さプレフィックス付き）の対応
- DNS over HTTPS / DNS over TLS

## 受入条件

1. [ ] `formats/dns.bdef.yaml` が存在すること
2. [ ] ヘッダの各フィールド（transaction_id, flags, カウントフィールド）が正しく解析できること
3. [ ] flagsのbitfield（qr, opcode, aa, tc, rd, ra, z, rcode）が正しく解析・表示されること
4. [ ] Questionsセクションがqd_count個分解析できること
5. [ ] qtypeがenumラベル付きで表示されること
6. [ ] qclassがenumラベル付きで表示されること
7. [ ] DNSヘッダ解析、flags bitfield表示が正しく動作すること
8. [ ] フォーマット定義がバリデーションに通ること
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにDNSを追記
- [ ] docs/architecture.md — formats/ ディレクトリに dns.bdef.yaml を追記

---

## 設計メモ

> 設計Phaseで記入する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
