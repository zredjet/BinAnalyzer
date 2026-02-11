# REQ-072: PDFフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-033で実装されたPDFフォーマット定義（`formats/pdf.bdef.yaml`）はヘッダ13バイト程度のみのスタブ状態であり、PDFファイルの基本構造に対する情報が不足している。

PDFはテキストベースのフォーマットであるため、バイナリフィールドレベルの解析には根本的な限界がある。オブジェクト定義（`1 0 obj ... endobj`）、クロスリファレンステーブル（`xref`）、トレーラ（`trailer`）はいずれもテキストベースの構文であり、BinAnalyzerのバイナリ構造解析エンジンでは対応できない。ファイル末尾からの逆方向パース（startxref位置の検出）も、シーケンシャル読み取りベースのエンジンでは実現不可能である。

本要望では、これらの制約を明確にしたうえで、バイナリ解析として意味のあるヘッダ部分の改善と、スコープの説明をdescription/コメントで明記する。

## 機能要件

### 追加する機能

- [ ] PDF header改善
  - version: ascii, size: 8, description: "e.g. %PDF-1.7"
  - binary_comment: bytes, size: 5, description: "Binary marker: % followed by 4 bytes > 127, indicates binary content"
- [ ] body追加
  - body: bytes, size: remaining, description: "PDF body: objects, streams, xref, trailer. Text-based structure not suitable for binary field-level parsing."
- [ ] スコープの明確化（description/コメント）
  - PDFはテキストベースフォーマットであり、バイナリ構造解析の対象範囲は限定的であることをYAMLファイル内のdescription/コメントで明記
  - ファイル末尾のxref/trailer解析はスコープ外（逆方向パースが必要）であることを記載
  - ストリーム（stream...endstream）のバイナリ部分もスコープ外（テキストパースが先行して必要）であることを記載

### 変更する既存機能

- [ ] `formats/pdf.bdef.yaml` — ヘッダフィールドの改善（version/binary_comment分離）、bodyフィールド追加、スコープ説明のdescription/コメント追記

### 変更しないもの（スコープ外）

- PDFオブジェクトの解析（間接オブジェクト `n 0 obj ... endobj` のテキストパース）
- クロスリファレンステーブルの解析（`xref` セクションのテキストパース）
- トレーラの解析（`trailer << ... >>` のテキストパース）
- startxrefの検出（ファイル末尾からの逆方向検索）
- ストリームデータの解析（`stream ... endstream` 内のバイナリデータ）
- 暗号化PDFの対応
- リニアライズドPDFの最適化構造解析
- ページコンテンツの解析

## 受入条件

1. [ ] PDFヘッダがversion（ascii 8バイト）とbinary_comment（bytes 5バイト）に分離されて解析されること
2. [ ] bodyフィールドがbytes remainingとして定義され、descriptionにテキストベース構造である旨が記載されていること
3. [ ] PDFがテキストベースフォーマットでありバイナリ解析の対象範囲が限定的であることがdescription/コメントに明記されていること
4. [ ] xref/trailer/stream解析がスコープ外であることがdescription/コメントに記載されていること
5. [ ] 既存のPDFヘッダ解析に回帰がないこと
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] formats/pdf.bdef.yaml — ヘッダ構造改善、bodyフィールド追加、スコープ説明のdescription/コメント追記
- [ ] docs/architecture.md — フォーマット一覧の説明更新（PDFスコープの限定を明記）

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
