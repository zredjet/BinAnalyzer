# REQ-111: MP4/ISO BMFF フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| フェーズ | D（フォーマット） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-14 |

## 背景・動機

MP4（ISO Base Media File Format / MPEG-4 Part 12）は動画・音声コンテナとして最も普及しているバイナリフォーマットの一つ。box（atom）構造の再帰的なツリーで構成され、BinAnalyzer の DSL で表現可能な構造。フォーマット定義の充実度はツールの実用性に直結する。

### ユースケース

- MP4 ファイルの構造解析（ftyp, moov, mdat 等の box 構造）
- メタデータ（udta, ilst）の確認
- トラック情報（trak, mdia, minf）の確認
- 動画/音声コーデック情報の確認

## 機能要件

### 追加する機能

- [x] `formats/mp4.bdef.yaml` の作成
- [x] ftyp, moov, mvhd, trak, mdia, minf, stbl 等の主要 box の定義
- [x] box の再帰的パース（size + type ヘッダ → ペイロード）
- [x] 主要 box タイプの enum 定義

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- 全 box タイプの網羅的な定義（主要なもののみ）
- メディアデータ（mdat）の内部デコード
- フラグメント MP4（fMP4）対応

## 受入条件

1. [x] `formats/mp4.bdef.yaml` が作成されていること
2. [x] 主要な box 構造（ftyp, moov, mvhd, trak, mdia, stbl）が正しくデコードされること
3. [x] box のネスト構造がツリー表示で確認できること
4. [x] 実際の MP4 ファイルでデコードが成功すること
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

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

AVI フォーマット定義（`formats/avi.bdef.yaml`）のパターンを転用。MP4 の box 構造（`size(4B) + type(4B) + data`）は AVI の RIFF チャンク（`id(4B) + size(4B) + data`）と構造的に類似しており、同じ再帰パターンで実現。

主要な違い:
- **エンディアン**: ビッグエンディアン（AVI はリトルエンディアン）
- **ヘッダ順序**: サイズが先、タイプが後（AVI は逆）
- **FullBox パターン**: MP4 固有。version(1B) + flags(3B) ヘッダを持つ box がある
- **extended_size**: `box_size == 1` の場合、uint64 で実サイズを格納（大容量ファイル対応）
- **box_size == 0**: ファイル末尾まで（`remaining` で対応）

### 変更ファイル一覧

| ファイル | 変更 | 内容 |
|---|---|---|
| `formats/mp4.bdef.yaml` | 新規 | MP4/ISO BMFF フォーマット定義 |
| `tests/BinAnalyzer.Integration.Tests/Mp4TestDataGenerator.cs` | 新規 | 最小 MP4 テストデータ生成 |
| `tests/BinAnalyzer.Integration.Tests/RealFileFixture.cs` | 変更 | `EnsureFile("test.mp4", ...)` 追加 |
| `tests/BinAnalyzer.Integration.Tests/GoldenFileTests.cs` | 変更 | `[InlineData("mp4", ...)]` 追加 |
| `tests/BinAnalyzer.Integration.Tests/RealFileValidationTests.cs` | 変更 | `[InlineData("mp4.bdef.yaml", ...)]` 追加 |
| `testdata/golden/mp4.json` | 新規 | ゴールデンファイル |

### 対応 box 一覧

| box type | 種別 | 説明 |
|---|---|---|
| ftyp | Leaf | ファイルタイプ・ブランド |
| moov | Container | ムービーメタデータコンテナ |
| mvhd | FullBox | ムービーヘッダ（timescale, duration 等） |
| trak | Container | トラックコンテナ |
| tkhd | FullBox | トラックヘッダ（track_id, duration 等） |
| mdia | Container | メディアコンテナ |
| mdhd | FullBox | メディアヘッダ（timescale, duration, language） |
| hdlr | FullBox | ハンドラタイプ（"vide", "soun" 等） |
| minf | Container | メディア情報コンテナ |
| vmhd | FullBox | ビデオメディアヘッダ |
| smhd | FullBox | サウンドメディアヘッダ |
| dinf | Container | データ情報コンテナ |
| dref | FullBox | データ参照 |
| stbl | Container | サンプルテーブルコンテナ |
| stsd | FullBox | サンプル記述 |
| stts | FullBox | Time-to-Sample テーブル |
| stsc | FullBox | Sample-to-Chunk テーブル |
| stsz | FullBox | サンプルサイズテーブル |
| stco | FullBox | チャンクオフセットテーブル |
| edts | Container | 編集リストコンテナ |
| elst | FullBox | 編集リスト |
| udta | Container | ユーザーデータ |
| mdat | Leaf | メディアデータ（raw） |
| free/skip | Leaf | フリースペース（raw） |

### モデル変更

なし。C# プロジェクトコード（Core/Dsl/Engine/Output/Cli）の変更は不要。

### インタフェース変更

なし。

### 代替案

なし。

### 懸念事項

- `box_size == 0`（ファイル末尾まで）は `remaining` で対応。通常 mdat のみで使用される。
- extended_size（box_size == 1）は条件付きフィールドで対応。テストデータでは未使用。
- FullBox の version 分岐（v0=32bit, v1=64bit）は `if` 条件で対応。テストデータは v0 のみ。

---

## 実装メモ

### 実装中の設計変更

- `RealFileValidationTests` の `minChildren` を 2 → 1 に変更。ルート構造 `mp4_file` は `boxes` フィールド1つのみで Children.Count=1 となるため。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| GoldenFileTests | DecodeResult_MatchesGoldenFile("mp4", ...) | 受入条件 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully("mp4.bdef.yaml", ...) | 受入条件 4, 5 |

### 気づき・今後の課題

- フラグメント MP4（fMP4）の対応（sidx, moof, mfhd 等）は将来の要件として検討。
- co64（64bit チャンクオフセット）の対応は大容量ファイル解析で必要になる可能性あり。
- ilst（iTunes メタデータ）等の拡張 box は需要に応じて追加可能。
