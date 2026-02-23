# REQ-147: 追加フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | in_progress |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-22 |

## 背景・動機

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

- [ ] MKV/WebM (EBML) フォーマット定義
- [x] HEIF/HEIC フォーマット定義
- [ ] XZ フォーマット定義
- [x] MessagePack フォーマット定義
- [x] CBOR フォーマット定義

### 変更する既存機能

- [x] 変更なし — 新規 `.bdef.yaml` ファイルの追加のみ

### 変更しないもの（スコープ外）

- エンジンの機能追加 — 既存のDSL表現で記述可能な範囲
- ビットストリーム制御フローが必要なフォーマット（Brotli等） — REQ-145の完了後に対応
- ポインタベースの複雑な構造（Cap'n Proto, FlatBuffers） — REQ-137の完了後に対応

## 受入条件

1. [x] 各フォーマット定義がバリデーションエラーなしでロードできること（Phase 1: HEIF, MessagePack, CBOR 完了）
2. [x] 各フォーマットの実ファイルに対してデコードが成功すること（Phase 1: HEIF, MessagePack, CBOR 完了）
3. [x] ゴールデンファイルテストが追加されていること（Phase 1: HEIF, MessagePack, CBOR 完了）
4. [x] 各フォーマットのヘッダ・主要構造が正しくデコードされること（Phase 1: HEIF, MessagePack, CBOR 完了）
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
