# REQ-079: LZ4 Frameフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

LZ4は高速圧縮アルゴリズムで、データベース（ClickHouse, ZFS等）、ゲームエンジン、リアルタイムデータ処理で広く利用される。LZ4 Frame Format（LZ4F）はストリーミング対応のフレームフォーマット。

シンプルなヘッダ構造でbitfieldを活用するフォーマット定義の例として適している。Frame Descriptorのフラグフィールドに条件付きフィールド（content_size, dict_id）が連動する構造を持つ。

LZ4 Frame構造:
- Magic Number（4バイト: 0x184D2204）
- Frame Descriptor（FLG + BD + オプションフィールド + Header Checksum）
- Data Blocks（block_size + data の繰り返し、block_size=0で終了）
- Content Checksum（オプション）

## 機能要件

### 追加する機能

- [ ] `formats/lz4.bdef.yaml` フォーマット定義
  - Magic Number
    - magic（u32le、expected 0x184D2204）
  - Frame Descriptor
    - flg（bitfield）
      - version（bits 7:6、常に01）
      - b_independence（bit 5、ブロック独立フラグ）
      - b_checksum（bit 4、ブロックチェックサムフラグ）
      - content_size（bit 3、コンテンツサイズフィールド有無）
      - content_checksum（bit 2、コンテンツチェックサムフラグ）
      - dict_id（bit 0、辞書IDフィールド有無）
    - bd（bitfield）
      - block_max_size（bits 6:4、最大ブロックサイズ: 4=64KB, 5=256KB, 6=1MB, 7=4MB）
    - content_size_value（u64le、if flg.content_size = 1、元データサイズ）
    - dict_id_value（u32le、if flg.dict_id = 1、辞書ID）
    - header_checksum（u8、ヘッダチェックサム: XXH32の2ndバイト）
  - Data Blocks（repeat until block_size = 0）
    - block_size（u32le、最上位ビット=1は非圧縮、0は圧縮）
    - data（bytes size block_size、ブロックデータ）
  - Content Checksum（if flg.content_checksum = 1）
    - checksum（u32le、XXH32チェックサム）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- LZ4ブロックデータの展開・解凍
- Skippable Frames（マジック 0x184D2A50〜0x184D2A5F）の対応
- Legacy Frame Format（マジック 0x184C2102）の対応
- ブロックチェックサム / コンテンツチェックサムの検証
- 辞書圧縮の対応

## 受入条件

1. [ ] `formats/lz4.bdef.yaml` が存在すること
2. [ ] Magic Number（0x184D2204）が正しく検証されること
3. [ ] Frame DescriptorのFLG bitfield（version, b_independence, b_checksum, content_size, content_checksum, dict_id）が正しく解析・表示されること
4. [ ] BD bitfield（block_max_size）が正しく解析・表示されること
5. [ ] content_size_valueがflg.content_sizeに応じて条件付きで解析されること
6. [ ] header_checksumが読み取れること
7. [ ] フレームヘッダ解析、bitfield表示が正しく動作すること
8. [ ] フォーマット定義がバリデーションに通ること
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにLZ4を追記
- [ ] docs/architecture.md — formats/ ディレクトリに lz4.bdef.yaml を追記

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
