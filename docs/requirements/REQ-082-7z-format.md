# REQ-082: 7zフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

7z（7-Zip）は広く使われるアーカイブフォーマットで、高い圧縮率（LZMA/LZMA2）とソリッド圧縮が特徴。Windows環境を中心に、Linux/macOSでも利用される。

ファイル先頭にSignature Header（32バイト）を持ち、next_header_offsetでメインヘッダ位置を参照する構造のため、seekを活用した解析が可能。ただし、7zの内部構造は可変長エンコーディング（7z独自のバイト列）を使用するため、詳細パースは現DSLの表現力を超える。Signature Headerとnext_headerへのseekにスコープを限定する。

7z構造:
- Signature Header（32バイト: signature 6バイト + version 2バイト + StartHeader CRC 4バイト + NextHeaderOffset 8バイト + NextHeaderSize 8バイト + NextHeaderCRC 4バイト）
- パックデータ（圧縮されたファイルデータ）
- Next Header（エンコードされたヘッダ情報、seekでアクセス）

## 機能要件

### 追加する機能

- [ ] `formats/7z.bdef.yaml` フォーマット定義
  - Signature Header
    - signature（bytes 6、expected [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]、"7z\xBC\xAF\x27\x1C"）
    - major_version（u8、メジャーバージョン、通常0）
    - minor_version（u8、マイナーバージョン、通常4）
    - start_header_crc（u32le、StartHeader部のCRC32）
    - next_header_offset（u64le、NextHeaderのオフセット — Signature Header末尾からの相対位置）
    - next_header_size（u64le、NextHeaderのサイズ）
    - next_header_crc（u32le、NextHeaderのCRC32）
  - Next Header（seek next_header_offset + 32 の位置にジャンプ）
    - header_data（bytes size next_header_size、description "7z encoded header - internal structure uses variable-length encoding"）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- エンコードされたヘッダの詳細解析（7z独自の可変長エンコーディング）
- ファイル一覧・ディレクトリ構造の取得
- 圧縮データの展開（LZMA, LZMA2, BZip2, PPMD等）
- ソリッド圧縮の対応
- 暗号化アーカイブ（AES-256）の対応
- マルチボリューム（分割アーカイブ）の対応
- CRC32の検証（start_header_crc, next_header_crc）

## 受入条件

1. [ ] `formats/7z.bdef.yaml` が存在すること
2. [ ] Signature Headerの6バイトシグネチャ（[0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]）が正しく検証されること
3. [ ] major_version, minor_version が正しく読み取れること
4. [ ] start_header_crc, next_header_offset, next_header_size, next_header_crc が正しく解析できること
5. [ ] next_header_offset + 32 の位置へのseekが正しく動作すること
6. [ ] header_dataがnext_header_sizeバイト分読み取れること
7. [ ] 可変長エンコーディングの制約がスコープ外として明記されていること
8. [ ] シグネチャヘッダ解析、next_headerへのseekが正しく動作すること
9. [ ] フォーマット定義がバリデーションに通ること
10. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットに7zを追記
- [ ] docs/architecture.md — formats/ ディレクトリに 7z.bdef.yaml を追記

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
