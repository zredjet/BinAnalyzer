# REQ-046: PE（Windows EXE/DLL）フォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-044（オフセットジャンプ） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

PE（Portable Executable）はWindowsの実行ファイル（EXE）および動的リンクライブラリ（DLL）のフォーマット。リバースエンジニアリング、マルウェア解析、セキュリティ研究において最も重要なバイナリフォーマットの一つ。

DOS Header → PE Signature → COFF Header → Optional Header → Section Table という構造で、DOS HeaderのE_lfanew（オフセット0x3C）がPE Signatureの位置を指すため、オフセットジャンプ機能が必須。

## 機能要件

### 追加する機能

- [ ] `formats/pe.bdef.yaml` フォーマット定義
  - DOS Header（64バイト）
    - e_magic（"MZ" = 0x4D5A）
    - e_lfanew（PE Signatureへのオフセット、オフセット0x3C）
  - PE Signature（seekでe_lfanewの位置にジャンプ）
    - "PE\0\0" マジック
  - COFF File Header
    - Machine（enum: I386, AMD64, ARM, ARM64等）
    - NumberOfSections, TimeDateStamp, SizeOfOptionalHeader, Characteristics（flags）
  - Optional Header
    - Magic（PE32=0x10b, PE32+=0x20b）
    - AddressOfEntryPoint, ImageBase, SectionAlignment, FileAlignment
    - SizeOfImage, SizeOfHeaders
    - NumberOfRvaAndSizes
    - Data Directory配列（Export, Import, Resource, Exception, Security, BaseReloc, Debug, TLS, IAT等）
  - Section Table（NumberOfSections個）
    - Name（8バイトASCII）、VirtualSize, VirtualAddress, SizeOfRawData, PointerToRawData, Characteristics

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- Import/Export テーブルの詳細解析（RVA解決が必要）
- リソースセクションの解析
- .NET メタデータの解析
- デバッグ情報の解析

## 受入条件

1. [ ] `formats/pe.bdef.yaml` が存在すること
2. [ ] DOS Headerが正しく解析でき、e_lfanewが取得できること
3. [ ] e_lfanewへのseekでPE Signatureが正しく解析できること
4. [ ] COFF File Headerのフィールドが正しく解析できること
5. [ ] Optional Headerの主要フィールドが取得できること
6. [ ] Section Tableが正しい個数分解析できること
7. [ ] 32ビット（PE32）と64ビット（PE32+）の両方に対応すること
8. [ ] 有効なPEファイルでデコードエラーにならないこと
9. [ ] フォーマット定義がバリデーションに通ること
10. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにPEを追記
- [ ] docs/architecture.md — formats/ ディレクトリに pe.bdef.yaml を追記

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
