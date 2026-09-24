# REQ-147: 追加フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-09-24 |

## 背景・動機

> **現況（2026-09-24）**: HEIF/HEIC・MessagePack・CBOR を追加済みで、同梱の定義は 38 種（`formats/*.bdef.yaml`）。残りは MKV/WebM と XZ の 2 件。下の「現在35」は作成時点の数。
>
> **完了（2026-09-24）**: Phase 2 で MKV/WebM（`mkv.bdef.yaml`）と XZ（`xz.bdef.yaml`）を追加し、同梱の定義は 40 種になった。

現在35のフォーマット定義が存在する。エンジン機能は成熟しており、新しいフォーマット定義の追加にエンジン変更は原則不要である。カバレッジの拡大は、ツールの実用性を直接向上させる。

### 追加候補フォーマット

| カテゴリ | フォーマット | 特徴・難易度 |
|---|---|---|
| コンテナ | MKV/WebM (Matroska/EBML) | EBMLベースの可変長タグ構造。uleb128活用 |
| 画像 | HEIF/HEIC | ISO Base Media File Format (MP4派生)。既存MP4定義を活用可能 |
| 圧縮 | XZ | ストリームヘッダ + ブロック + インデックス。CRC-32/CRC-64検証 |
| 圧縮 | Brotli | ウィンドウサイズ + メタブロック。ビットストリーム解析必要 |
| シリアライズ | FlatBuffers | オフセットベースの構造。vtable + データテーブル |
| シリアライズ | Cap'n Proto | セグメントテーブル + ポインタベースの構造 |
| シリアライズ | MessagePack | タグ + データのTLV構造。可変長整数 |
| シリアライズ | CBOR (RFC 8949) | TLV構造。MessagePack類似だがIETF標準 |
| ファームウェア | UEFI Firmware Volume | EFI_FV_HEADER + EFI_FFS_FILE_HEADER の階層構造 |
| ゲーム | Unity AssetBundle | ヘッダ + シリアライズされたアセットデータ |

## 機能要件

### 追加する機能

- [x] MKV/WebM (EBML) フォーマット定義
- [x] HEIF/HEIC フォーマット定義
- [x] XZ フォーマット定義
- [x] MessagePack フォーマット定義
- [x] CBOR フォーマット定義

### 変更する既存機能

- [x] 変更なし — 新規 `.bdef.yaml` ファイルの追加のみ

### 変更しないもの（スコープ外）

- エンジンの機能追加 — 既存のDSL表現で記述可能な範囲
- ビットストリーム制御フローが必要なフォーマット（Brotli等） — REQ-145の完了後に対応
- ポインタベースの複雑な構造（Cap'n Proto, FlatBuffers） — REQ-137の完了後に対応

## 受入条件

1. [x] 各フォーマット定義がバリデーションエラーなしでロードできること（Phase 1: HEIF, MessagePack, CBOR / Phase 2: MKV/WebM, XZ 完了）
2. [x] 各フォーマットの実ファイルに対してデコードが成功すること（Phase 1: HEIF, MessagePack, CBOR / Phase 2: MKV/WebM, XZ 完了）
3. [x] ゴールデンファイルテストが追加されていること（Phase 1: HEIF, MessagePack, CBOR / Phase 2: MKV/WebM, XZ 完了）
4. [x] 各フォーマットのヘッダ・主要構造が正しくデコードされること（Phase 1: HEIF, MessagePack, CBOR / Phase 2: MKV/WebM, XZ 完了）
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [x] formats/ — 新規フォーマット定義ファイル追加（heif, msgpack, cbor）
- [x] tests/ — ゴールデンファイルテスト追加（heif, msgpack, cbor）
- [x] tests/testdata/ — テスト用バイナリファイル追加（heif, msgpack, cbor）

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 対応フォーマットリストの更新（38種に更新済み）

---

## 設計メモ

### 設計方針

Phase 1 として簡単な3フォーマット（HEIF, MessagePack, CBOR）を先行実装。MKV/WebM と XZ は後続フェーズとする。

- **HEIF/HEIC**: ISO Base Media File Format (ISOBMFF) ベース。既存 `mp4.bdef.yaml` と同じボックス構造パターン。HEIF固有ボックス（meta FullBox、pitm、iloc、iinf/infe、iprp）を追加。
- **MessagePack**: first-byte dispatch の TLV 構造。先頭1バイトで型とサイズエンコーディングが決まる。switch の default に fixtype_value を配置し、if 条件分岐でビットパターンを判定。
- **CBOR (RFC 8949)**: 先頭バイトの上位3ビットが major type、下位5ビットが additional info。virtual フィールドでビット分離し、major type で switch 分岐。

### モデル変更

なし。既存の DSL 機能（switch, if, virtual, repeat_count, repeat: eof）で全て表現可能。

### インタフェース変更

なし。

### 代替案

- HEIF は MP4 定義を拡張する案もあったが、ファイル識別とボックスセットが異なるため独立定義とした。
- MessagePack の fixtype は switch の range case がない制約のため、default + if 分岐で対処。

### 懸念事項

- MessagePack の fixtype 分岐: DSL の switch_on は等値比較のみで範囲条件はサポートしていない。fixtype_value 内の `if` 条件分岐（`format_byte >= 0x80 && format_byte <= 0x8f` 等）で対処。
- CBOR の不定長エンコーディング: additional_info=31 の不定長パターンは break byte (0xff) までの繰り返し。テストデータでは定長のみ使用し、不定長は `raw_data` にフォールバック。
- HEIF の iloc バージョン差異: iloc box は version 0/1/2 で構造が異なる。テストデータは version 0 の最小構造で対応。

### 設計方針（Phase 2: MKV/WebM・XZ、2026-09-24）

- **XZ**: Block Header は圧縮後サイズを省略できる（`xz -T1` など）ので、先頭から順に読むだけでは圧縮データの終わりが分からない。ルートの先頭で `{remaining}`（= ファイルサイズ）を取り、`seek` + `seek_restore` で末尾の Stream Footer → Index を先に読み、Index の Unpadded Size から各 Block の圧縮データのサイズを求める。可変長整数は ULEB128 と同じ形式なので `uleb128` 型を使う。CRC32 は Stream Header・Block Header・Index・Stream Footer の 4 か所すべてを検証する（Block Header と Index は本体を struct にまとめて `fields` で、Stream Footer は対象が CRC の後ろにあるので末尾からの `range` で指定）。Block 末尾の Check（CRC32 / CRC64 / SHA-256）は展開後のデータに対する値なので、表示のみ
- **MKV/WebM**: EBML の要素は ID・サイズとも可変長整数（VINT、先頭バイトの上位の 0 の数 + 1 が長さ）で、DSL に専用の型は無い。先頭 1 バイトから長さを三項演算子で求め、残りバイトは長さごとの switch（`be1`〜`be7`）で読み、値を virtual で組み立てる。長さ 1 のとき（大半）は残りバイトの switch を `if` で読まない。データの型は ID で決まるので、ID の switch で master（子要素の繰り返し）/ 符号なし整数 / 符号付き整数 / 浮動小数点 / ASCII / UTF-8 / 日付 / SimpleBlock / バイナリに振り分ける（主要な 100 要素。表に無い ID はバイナリ）。サイズの値ビットがすべて 1（サイズ不定。ライブ配信の Segment / Cluster）のときはスコープの残りすべてをデータとする

### モデル変更（Phase 2）

- **式の拡張**: メンバーをたどった先の配列への添字（`{index.body.records[_index].unpadded_size}`）を書けるようにした。以前は配列名に直接付く添字（`{records[_index]}`）しか書けず、パーサーは「配列名 `[` 添字 `]`」の後にしか `.メンバー` を続けられなかった。後置の `.メンバー` と `[添字]` を任意の順で続けられるようにし、式の結果（配列）への添字として `ElementAccess` ノードを足した（既存の `IndexAccess` はそのまま）
- **format-list.json**: GUI / Web が拡張子からフォーマットを選ぶ一覧に cbor・msgpack が漏れていた（Phase 1 の追加時）。今回の 2 つと合わせて 4 件を足し、定義ファイルとの一致・拡張子の重複をテストで固定した

### 代替案（Phase 2）

- **EBML の VINT を型として DSL に足す（`type: ebml_vint`）。** 定義は短くなるが、エンジンに EBML 専用の型が増える。既存の機能（switch・virtual・三項演算子）で書けたので見送った
- **XZ の Block を Block Header の圧縮後サイズで読む。** xz の既定（マルチスレッド）ではサイズが書かれるが、`-T1` や他の実装では省略されるので、Index から求める方式にした

---

---

## 実装メモ

### 実装中の設計変更

特になし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| GoldenFileTests | DecodeResult_MatchesGoldenFile("heif", ...) | 1, 2, 3 |
| GoldenFileTests | DecodeResult_MatchesGoldenFile("msgpack", ...) | 1, 2, 3 |
| GoldenFileTests | DecodeResult_MatchesGoldenFile("cbor", ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully("heif.bdef.yaml", ...) | 1, 2, 4 |
| RealFileValidationTests | RealFile_DecodesSuccessfully("msgpack.bdef.yaml", ...) | 1, 2, 4 |
| RealFileValidationTests | RealFile_DecodesSuccessfully("cbor.bdef.yaml", ...) | 1, 2, 4 |

### 気づき・今後の課題

- MKV/WebM (EBML) と XZ は Phase 2 で対応予定
- MessagePack の fixtype 分岐が DSL の range case サポートにより簡潔になる可能性あり（将来の DSL 拡張候補）

### Phase 2 の実装メモ（MKV/WebM・XZ）

- 確認に使ったファイル: XZ は `xz` で作った CRC32 / CRC64 / SHA-256 / チェックなし / マルチスレッドの 3 Block / `-T1`（サイズなし）/ BCJ_x86 + LZMA2 の 7 種、MKV/WebM は `ffmpeg` で作った WebM（VP8 + Opus）と MKV（FFV1、UTF-8 のタイトル）。すべてデコードエラーなし、XZ の CRC32 はすべて ✓
- テストデータの Generator は、XZ は LZMA2 の非圧縮チャンクで本物として展開できる .xz を組み立てる（Python の `lzma` と `xz -t` で確認）。WebM はサイズ不定の Segment（8 バイトの VINT）・3 / 5 バイトの整数・UTF-8 のタイトル・SimpleBlock を含む（`ffprobe` で vp8 16x16 と認識されることを確認）
- 最初の Generator は Cluster をサイズ不定にしていたが、仕様上サイズ不定にできるのは親もサイズ不定の場合なので（`ffprobe` が警告）、Segment をサイズ不定にした
- 追加したテスト: `XzParsingTests`（5 件）、`MkvParsingTests`（4 件）、`FormatListConsistencyTests`（2 件）、式の拡張（Core 3 件・Engine 2 件）、実ファイル・ゴールデン・ファズへの登録
- 今後の課題: XZ の複数ストリームの連結とストリームパディング、LZMA2 の展開（Check の検証）、EBML の CRC-32 要素（親のデータの CRC）の検証、MKV の Block のレーシングの展開

