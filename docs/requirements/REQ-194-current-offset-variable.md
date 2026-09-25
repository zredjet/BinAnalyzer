# REQ-194: 現在の読み取り位置を式で得る（`_offset`）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（REQ-044 の seek、REQ-191 の size 付きのスコープの外への seek の上で行う） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

式から「今どこを読んでいるか」（ファイルの先頭からの位置）を得る方法が無い。`remaining`（今のスコープの残りのバイト数）しか無いので、同じバイトを別の型で読み直す・先の 1 バイトを覗く・位置を記録してあとでポインタの基準にする、といったことに回避策が要る。

REQ-188 の見直しでは、ルートの先頭で `file_size: "{remaining}"` を取っておき、`seek: "{file_size - remaining}"` で今の位置を求める形を 6 つの定義で使った（flac・mp3・ogg・pdf・protobuf・x509）。この形は **大きさの無いスコープがファイルの末尾まで続くとき** にしか正しくない。size 付きのスコープの中では `remaining` がそのスコープの残りになるので使えず、次のことができなかった。

- **pcap の中の DNS**: DNS の名前の圧縮ポインタは「DNS のメッセージの先頭からの位置」。pcap の UDP のペイロード（size 付き）の中ではメッセージの先頭の位置が式で分からず、formats/pcap.bdef.yaml は DNS をバイト列のまま表示している（dns.bdef.yaml を import して使えない）
- **pcap の RAW（リンク層の種類 101）**: 先頭の 1 バイト（IP の版）を覗けないので、ip_packet が先頭のバイトを読んでから IPv4 / IPv6 に分ける形に組み替えた
- **PDF のオブジェクト**: endobj の後ろの空白でない文字を 4 バイトの先読みで調べるのに、ルートからの `file_end - remaining` に頼っている（PDF をほかの形式の中に入れると壊れる）
- **FAT のディレクトリのエントリ**: エントリの位置を `dir_base + _index * 32` と計算で求めている

## 機能要件

### 追加する機能

- [x] **特殊変数 `_offset`**: 式を評価する時点の読み取り位置（今のデータの先頭からのバイト数）。`seek:` の値と同じ基準（`seek: "{_offset}"` は今の位置）
- [x] 圧縮データの中（`zlib` などで展開した中身）では、展開した中身の先頭からの位置（`seek:` の基準と同じ）
- [x] ビットストリームモードの中では、バイト単位の位置（端数のビットは切り捨て）。ビット単位の位置は別の要望とする
- [x] 検証（REQ-189）で `_offset` を既知の名前として扱う（`_index` / `_prev` と同じ）

### 変更する既存機能

- [x] なし（新しい名前を足すだけ）

### 変更しないもの（スコープ外）

- 同梱定義の書き直し。`file_size - remaining` の回避策を `_offset` に置き換えるのは、実装後に別の PR で行う（ゴールデンが変わらないことを確かめて）
- pcap の中の DNS の分解（`_offset` を使って dns.bdef.yaml を import する）。別の要望とする
- フィールドの位置を後から引く `field.offset` の形（代替案を参照）

## 受入条件

1. [x] ルートの先頭で `_offset` が 0、いくつかのフィールドを読んだ後でそのバイト数になること
2. [x] size 付きのスコープ（struct・switch・size 付きの繰り返しの要素）の中でも、ファイルの先頭からの位置になること（`remaining` と違い、スコープに依らない）
3. [x] `seek: "{_offset}"` + `seek_restore: true` で、今の位置のバイトを別の型で読み直せること
4. [x] 要素ごとの seek（`_index` を使う seek）の中では、seek した後の位置になること
5. [x] 展開した圧縮データの中では、展開した中身の先頭からの位置になること
6. [x] 検証で `_offset` が未定義の名前（VAL124）として警告されないこと
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 既知の特殊変数の一覧（`ExpressionReferences`）に `_offset` |
| BinAnalyzer.Engine | 識別子の評価で `_offset` を `DecodeContext.ByteOffset` にする（ビットストリームモードの読みかけのバイトを考える） |
| tests/BinAnalyzer.Engine.Tests | `CurrentOffsetTests` |
| tests/BinAnalyzer.Core.Tests | `ExpressionNameValidationTests.VAL124_SpecialVariables_NoWarning` に `_offset` |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 「式」の特殊変数（`remaining` の隣）、VAL124 の説明
- [x] docs/parser-design.md — VAL124 の説明
- [x] docs/architecture.md — DecodeContext の説明（`ByteOffset`）

---

## 設計メモ

### 設計方針

- **変数として束縛せず、識別子の評価のときに `DecodeContext.Position` を返す**（`remaining` と同じ扱い）。フィールドを読むたびに変数を書き換えると、全フィールドに費用がかかる
- 名前は既存の特殊変数（`_index` / `_prev`）に合わせて `_offset`。フィールドの名前と衝突しにくい

### 代替案

- **`offset(field)` 関数・`field._offset` のメンバー**: 読んだフィールドの位置を後から引ける（ポインタの基準によく使う）。ノードは位置を持っているので作れるが、変数の値ではなくノードを引く仕組みが要る。今の位置が分かれば、フィールドの直前に `_offset` の virtual を置けば同じことができるので、まず `_offset` だけにする
- **`remaining` をファイル全体の残りにする変種（`file_remaining`）**: `file_size - file_remaining` で位置になるが、1 つの値で済むものを 2 つにする理由が無い

### 懸念事項

- seek の境界を広げたスコープ（REQ-191）の中でも位置は同じ基準なので、特別な扱いは要らない見込み
- 位置の値は `int`。今のデータ長の上限（2 GB）の中に収まる

---

## 実装メモ

### 実装中の設計変更

- **ビットストリームモードの位置は `DecodeContext.ByteOffset` で求める。** ビットストリームモードでは、最初のビットを読むときにバイトを 1 つ読み進める（`DecodeContext.Position` は読みかけのバイトの次を指す）。`BitReader.HasPartialByte` が真なら `Position - 1`（読みかけのバイト）、偽なら `Position` を返す。要件どおり端数のビットは切り捨てた位置になる
- **`_offset` を使う seek は要素ごとの seek にしない。** 要素ごとに seek するかは、seek の式が繰り返しの文脈（`_index` / `_prev` / 配列の添字）を使うかで決めている（`UsesIterationContext`）。`_offset` は「式を評価した時点の位置」で、繰り返しの前に 1 回評価すれば足りるので、判定に入れなかった
- 同じ名前のフィールドを定義しても、`remaining` と同じく特殊変数が優先される（定義の側で `_offset` という名前を使うことは想定しない）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| CurrentOffsetTests | Offset_IsZeroAtTheStartAndAdvancesWithTheFields | 1 |
| CurrentOffsetTests | Offset_InsideSizedScope_IsFromTheStartOfTheFile | 2 |
| CurrentOffsetTests | SeekToOffset_RereadsTheSameBytesAsAnotherType | 3 |
| CurrentOffsetTests | Offset_InPerElementSeek_IsThePositionAfterTheSeek | 4 |
| CurrentOffsetTests | Offset_InsideDecompressedData_IsFromTheStartOfTheContent | 5 |
| CurrentOffsetTests | Offset_InBitstreamMode_IsTheByteBeingRead | 機能要件（ビットストリーム） |
| ExpressionNameValidationTests | VAL124_SpecialVariables_NoWarning（`_offset` を追加） | 6 |

### 気づき・今後の課題

- 同梱定義の `file_size - remaining` の回避策（flac・mp3・ogg・pdf・protobuf・x509）は `_offset` に置き換えた（PR: 同梱定義の置き換え。作業用の `file_size` / `value_offset` が無くなった以外、全サンプルの出力は同じ）。pcap の中の DNS の分解は別の要望とする

