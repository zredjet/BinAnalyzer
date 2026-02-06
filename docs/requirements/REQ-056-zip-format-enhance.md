# REQ-056: ZIPフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-009で実装されたZIPフォーマット定義（`formats/zip.bdef.yaml`）はローカルファイルヘッダー、セントラルディレクトリ、エンドオブセントラルディレクトリの基本構造に対応しているが、general_purpose_flagsが数値のみの表示、extra fieldが未解析、compression_methodのenum値が不完全な状態にある。

general_purpose_flagsは暗号化状態（bit 0）、Data Descriptorの有無（bit 3）、UTF-8エンコーディング（bit 11）などZIPアーカイブの重要な属性を示すフィールドだが、個別ビットの解読ができない。compression_methodもDeflate(8)とStored(0)のみの定義で、Deflate64、BZIP2、LZMA、ZSTD等の近年使用される圧縮方式が不足している。また、version_made_by/version_neededの上位バイトはアーカイブ作成OSを示すが、未定義のままである。

本要望では、既存の `zip.bdef.yaml` のフラグ・enum定義を拡充し、ZIP解析の可読性と情報量を向上させる。

## 機能要件

### 追加する機能

- [ ] general_purpose_flags のbitfield化
  - bit 0: 暗号化（encrypted）
  - bit 3: データディスクリプタ（data_descriptor）
  - bit 11: UTF-8（utf8_encoding）
- [ ] compression_method enum拡充
  - Deflate64 = 9
  - BZIP2 = 12
  - LZMA = 14
  - ZSTD = 93
  - XZ = 95
- [ ] Data Descriptor対応の説明追記
  - bit 3が立っている場合、compressed_dataの後にcrc32(4bytes)/compressed_size(4bytes)/uncompressed_size(4bytes)が配置される構造のdescriptionを該当フィールドに追記
  - 実際のData Descriptorパースは構造上の制約から対応困難なため、説明コメントによる情報提供とする
- [ ] version_made_by / version_needed の上位バイトをenum化
  - OS情報: MS-DOS = 0, Unix = 3, OS/2 = 6, macOS = 19

### 変更する既存機能

- [ ] `formats/zip.bdef.yaml` — flagsフィールドをbitfieldに変更、compression_method enumにエントリ追加、version関連フィールドにdescription追記

### 変更しないもの（スコープ外）

- Data Descriptorの実際のパース（compressed_sizeが0の場合のストリーム解析が必要）
- ZIP64拡張対応
- 暗号化ZIPの解析
- extra fieldの詳細パース（個別のextra field IDごとの構造定義）
- 圧縮データの展開

## 受入条件

1. [ ] general_purpose_flagsがbitfieldとして表示され、暗号化(bit 0)、データディスクリプタ(bit 3)、UTF-8(bit 11)の各ビットが個別に識別可能であること
2. [ ] compression_methodにDeflate64(9), BZIP2(12), LZMA(14), ZSTD(93), XZ(95)が追加されていること
3. [ ] version_made_by/version_neededのOS情報としてMS-DOS(0), Unix(3), OS/2(6), macOS(19)が定義されていること
4. [ ] Data Descriptorに関する説明がdescriptionとして記載されていること
5. [ ] 既存のZIP解析機能（ローカルファイルヘッダー、セントラルディレクトリ、エンドオブセントラルディレクトリ）に回帰がないこと
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

- [ ] formats/zip.bdef.yaml — general_purpose_flagsのbitfield化、compression_method拡充、version enum追加
- [ ] docs/architecture.md — フォーマット一覧の説明更新（ZIP bitfield/enum拡充を明記）

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
