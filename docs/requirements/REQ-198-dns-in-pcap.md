# REQ-198: pcap の UDP のペイロードの DNS を分解する

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | REQ-194（`_offset`） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

formats/pcap.bdef.yaml は UDP のペイロードをバイト列のまま表示している（先頭コメントの「対応していないもの: アプリケーション層（DNS・HTTP・TLS など。ペイロードはバイト列）」）。キャプチャで最もよく見る DNS を、formats/dns.bdef.yaml と同じ形で分解したい。

REQ-188 の見直しの時点では、DNS の名前の圧縮ポインタ（「DNS のメッセージの先頭からの位置」）の基準を、pcap の UDP のペイロード（size 付きのスコープ）の中で式から求められなかった。REQ-194 で今の位置（`_offset`）が分かるようになったので、メッセージの先頭の位置を取っておき、ポインタに足せば読める。

dns.bdef.yaml をインポートして使えば定義を 2 つ持たずに済むが、今のままでは次の点で組み合わせられない。

- 圧縮ポインタを **ファイルの先頭からの位置** として seek している（`seek: "{pointer_offset}"`）。単独の DNS のファイルではメッセージの先頭が 0 なので正しいが、pcap の中では誤る
- 両方に `raw_data` という struct があり、インポートで名前が衝突する（エラー）

## 機能要件

### 追加する機能

- [ ] **pcap の UDP のペイロードを、ポートが 53（DNS）・5353（mDNS）・5355（LLMNR）なら DNS のメッセージとして分解する。** 送信元か宛先のどちらかのポートで判定する。ほかのポートは従来どおりバイト列
- [ ] pcap は formats/dns.bdef.yaml をインポートして、DNS のメッセージの struct を使う（定義を複製しない）

### 変更する既存機能

- [ ] **dns.bdef.yaml の圧縮ポインタを、メッセージの先頭からの位置として読む。** メッセージの先頭で `_offset` を取っておき、ポインタに足す。単独の DNS のファイルでは先頭が 0 なので、出力は変わらない
- [ ] インポートで衝突しないよう、dns.bdef.yaml か pcap.bdef.yaml の struct の名前を変える（`raw_data` など）

### 変更しないもの（スコープ外）

- TCP の上の DNS（2 バイトの長さの前置き、複数のセグメントにまたがるメッセージ）
- DNS 以外のアプリケーション層（HTTP・TLS・DHCP など）。別の要望とする
- UDP のペイロードが DNS として壊れているときの判別（ポートで決めるので、DNS でないデータはデコードエラーになる。エラー継続モードで続きを読めること）

## 受入条件

1. [ ] tcpdump が読める pcap / pcapng の UDP 53 番の問い合わせと応答で、質問・回答の名前（圧縮ポインタを含む）と RR のデータが、単独の DNS のファイルとして読んだときと同じになること
2. [ ] 同じメッセージを単独の DNS のファイルとして読んだ結果（ゴールデンを含む）が変わらないこと
3. [ ] 53 番以外の UDP のペイロードは、従来どおりバイト列であること
4. [ ] mDNS（5353）の応答のクラスの上位ビット（cache flush）が読めること
5. [ ] 同梱定義の検証の警告が 0 件のままであること（インポートで名前が衝突しないこと）
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| formats/dns.bdef.yaml | メッセージの先頭の位置、圧縮ポインタの seek、struct の名前 |
| formats/pcap.bdef.yaml | dns.bdef.yaml のインポート、UDP のペイロードの switch |
| tests/BinAnalyzer.Integration.Tests | pcap の中の DNS のテストと Generator |

### 変更が必要なドキュメント

- [ ] 両定義の先頭コメント（仕様・対応していないもの）
- [ ] docs/requirements/REQ-188（今後の課題から参照）

---

## 設計メモ

### 設計方針（案）

- dns.bdef.yaml のルート（`dns`）の先頭に `message_start: "{_offset}"`（`padding: true`）を置き、`dns_name` のポインタの seek を `{message_start + pointer_offset}` にする。`dns_name` は `scope: isolated` だが、外側の変数は読めるので `message_start` を引ける
- pcap の `udp_datagram` の `payload` を switch にし、`src_port` / `dst_port` が 53 / 5353 / 5355 なら `dns`、それ以外は従来のバイト列。switch に size を付けて UDP の長さで区切る（圧縮ポインタの seek はメッセージの中なので境界は広がらない）
- Web 版のインポートの解決（HttpImportResolver）でも読めることを確かめる

### 懸念事項

- インポートした定義の root（`dns`）は struct として使うだけで、pcap の root は変わらない
- dns.bdef.yaml を変えると、dns を単独で使うときの出力に作業用の値（`message_start`）が増える。`padding: true` なのでツリーには出ないが、JSON とゴールデンには出る
