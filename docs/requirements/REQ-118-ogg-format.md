# REQ-118: OGG/Vorbis フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| フェーズ | D（フォーマット） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-15 |

## 背景・動機

OGG はオープンソースのメディアコンテナフォーマットで、Vorbis（音声）や Opus（音声）、Theora（動画）等のコーデックを格納する。ページベースの構造で、BinAnalyzer の DSL で表現可能。

### ユースケース

- OGG ファイルのページ構造（キャプチャパターン、シリアル番号、シーケンス番号）の確認
- Vorbis ヘッダ（Identification, Comment, Setup）の解析
- ストリーム情報（チャンネル数、サンプルレート等）の確認
- 複数論理ストリームの識別

## 機能要件

### 追加する機能

- [x] `formats/ogg.bdef.yaml` の作成
- [x] OGG ページヘッダ（マジック "OggS"、バージョン、フラグ、granule position、シリアル番号、ページシーケンス、CRC-32、セグメントテーブル）の定義
- [x] Vorbis Identification ヘッダの基本情報
- [x] ページの repeat_until eof パース

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- Vorbis 音声データのデコード
- Opus / Theora ヘッダの詳細解析
- ストリーム再構成

## 受入条件

1. [x] `formats/ogg.bdef.yaml` が作成されていること
2. [x] OGG ページヘッダが正しくデコードされること
3. [x] Vorbis Identification ヘッダの基本情報が表示されること
4. [x] 複数ページが連続してパースされること
5. [x] 実際の OGG ファイルでデコードが成功すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

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

`until_marker` + `switch` + `size:` 方式でページデータの境界を検出する。`until_marker(0x4F, 0x67, 0x67, 0x53)` で次の "OggS" マーカーまでのバイト数を取得し、`switch` の `size:` で境界スコープを作成。最終ページはマーカー未発見で `remaining` にフォールバック。

### モデル変更

なし。C# コードの変更は不要。

### インタフェース変更

なし。

### 代替案

- `sum()` 関数をエンジンに追加してセグメントテーブルの合計を計算する案 → DSL 変更が不要な `until_marker` 方式を採用
- `bitfield` 型で header_type のフラグを抽出する案 → `uint8` + `virtual` の方がシンプルで実績あり

### 懸念事項

- ページデータ内に "OggS" バイト列が偶然含まれる場合、`until_marker` が誤検出する可能性あり（実用上は極めて稀）
- Vorbis Comment/Setup ヘッダは raw bytes として表示（Identification のみ詳細解析）

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `formats/ogg.bdef.yaml` | 新規 | OGG/Vorbis フォーマット定義 |
| `tests/BinAnalyzer.Integration.Tests/OggTestDataGenerator.cs` | 新規 | 最小 2 ページ OGG テストデータ生成 |
| `tests/BinAnalyzer.Integration.Tests/RealFileFixture.cs` | 変更 | `EnsureFile("test.ogg", ...)` 追加 |
| `tests/BinAnalyzer.Integration.Tests/GoldenFileTests.cs` | 変更 | `[InlineData("ogg", ...)]` 追加 |
| `tests/BinAnalyzer.Integration.Tests/RealFileValidationTests.cs` | 変更 | `[InlineData("ogg.bdef.yaml", ...)]` 追加 |
| `testdata/golden/ogg.json` | 新規 | ゴールデンファイル（自動生成） |

### 実装中の設計変更

なし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| GoldenFileTests | `DecodeResult_MatchesGoldenFile("ogg", ...)` | AC1, AC2, AC3, AC4 |
| RealFileValidationTests | `RealFile_DecodesSuccessfully("ogg.bdef.yaml", ...)` | AC5, AC6 |

### 気づき・今後の課題

- Opus ヘッダ (`OpusHead`) の詳細解析は別要件で対応可能
- Theora ヘッダの解析も同様に `switch_on` の条件分岐で追加可能
- CRC-32 の検証は現在スコープ外（validate 式では CRC 計算関数が未サポート）
