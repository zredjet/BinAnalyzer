# REQ-155: 共通定義ライブラリ（標準型ライブラリ）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-026 |
| 作成日 | 2026-02-23 |
| 更新日 | 2026-02-23 |

## 背景・動機

REQ-026 で `imports` 機構が実装済みであり、フォーマット定義間で構造体を共有できる。しかし、現在は各フォーマット定義が個別に共通パターン（FourCC、チャンクヘッダー、パディング等）を定義している。

標準的な構造体・型を「共通定義ライブラリ」として提供し、フォーマット定義作成時の定型作業を削減する。

## 機能要件

### 追加する機能

- [x] `formats/common/` ディレクトリに標準型ライブラリを配置
- [x] 共通チャンク構造（RIFF チャンク、IFF チャンク等）
- [x] 共通ヘッダーパターン（マジックバイト + バージョン等）
- [x] 一般的なデータ型（FourCC、UUID/GUID、タイムスタンプ等）
- [x] 既存フォーマット定義から共通構造体を抽出・リファクタリング

### 変更する既存機能

- [x] 既存フォーマット定義 — 共通ライブラリからの import に切り替え

### 変更しないもの（スコープ外）

- import 機構自体の拡張（REQ-026 の範囲）
- パッケージマネージャー的な外部ライブラリ取得機構

## 受入条件

1. [x] `formats/common/` に共通定義ファイルが存在すること
2. [x] 少なくとも3つの既存フォーマット定義が共通ライブラリを import して簡潔になること
3. [x] 共通ライブラリ使用前後でデコード結果が同一であること（ゴールデンテストで検証）
4. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし（既存 import 機構を利用） |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 共通ライブラリの利用方法追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- 共通ライブラリはファミリー別に分離: ISO BMFF (`isobmff.bdef.yaml`) と RIFF (`riff.bdef.yaml`)
- `raw_data` は両ファイルに重複するが、VAL109 警告回避のため意図的に分離
- 各共通ファイルには dummy root struct (`_isobmff_common` / `_riff_common`) を配置
- `container_box` は import 先の `iso_box` を参照 — マージ後の統合名前空間で解決

### モデル変更

変更なし。既存の import 機構をそのまま利用。

### インタフェース変更

変更なし。

### 代替案

- 全共通構造体を1ファイルにまとめる案 → RIFF フォーマットに ISO BMFF 固有 struct が混入し VAL109 警告が発生するため不採用
- description を共通ライブラリに残す案 → フォーマット固有の例示が含まれるため不採用。ゴールデンファイルを再生成で対応

### 懸念事項

- `_isobmff_common` / `_riff_common` dummy struct が import 先で VAL109（到達不可能）警告を出す可能性 → import 元ファイル単体の検証時のみの問題

---

## 実装メモ

### 実装中の設計変更

- フォーマット固有の `description` は共通ライブラリに含めない方針に変更。ゴールデンファイルを再生成して対応。デコード結果（値・構造）は同一。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Mp4ParsingTests | Mp4Format_LoadsWithoutErrors | AC-3, AC-4 |
| Mp4ParsingTests | Mp4Format_DecodesSuccessfully | AC-3, AC-4 |
| Mp4ParsingTests | Mp4Format_FtypBox_DecodesCorrectly | AC-3 |
| Mp4ParsingTests | Mp4Format_ContainerBoxes_Decoded | AC-3 |
| Mp4ParsingTests | Mp4Format_TreeOutput_ContainsExpectedElements | AC-3 |
| HeifParsingTests | HeifFormat_LoadsWithoutErrors | AC-3, AC-4 |
| HeifParsingTests | HeifFormat_DecodesSuccessfully | AC-3, AC-4 |
| HeifParsingTests | HeifFormat_FtypBox_DecodesCorrectly | AC-3 |
| HeifParsingTests | HeifFormat_MetaBox_DecodesCorrectly | AC-3 |
| HeifParsingTests | HeifFormat_TreeOutput_ContainsExpectedElements | AC-3 |

### 気づき・今後の課題

- 今回の共通化で5つのフォーマット定義（MP4, HEIF, WAV, AVI, WebP）が import を使用するようになった
- 今後、他のファミリー（例: IFF系）でも同様の共通ライブラリ抽出が可能
