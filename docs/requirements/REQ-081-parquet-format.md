# REQ-081: Parquetフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

Apache Parquetは列指向のデータストレージフォーマットで、Apache Spark、Apache Arrow、Pandas等のビッグデータ / データサイエンスエコシステムで広く使用される。

ファイルの先頭と末尾に "PAR1" マジックナンバーを持ち、フッタにThriftエンコーディングされたメタデータが格納される構造が特徴。ただし、メタデータ部がApache Thriftのコンパクトプロトコルで直列化されているため、詳細パースは現DSLの表現力を超える。ヘッダマジックと基本構造の説明にスコープを限定する。

Parquet構造:
- ヘッダマジック（"PAR1"、4バイト）
- Row Groups（Column Chunksを含む — Thriftエンコーディング）
- フッタメタデータ（Thriftエンコーディング）
- フッタサイズ（4バイト）
- フッタマジック（"PAR1"、4バイト）

## 機能要件

### 追加する機能

- [ ] `formats/parquet.bdef.yaml` フォーマット定義
  - ヘッダマジック
    - magic（ascii 4、expected "PAR1"）
  - Row Groups
    - row_groups（bytes remaining、description "Row groups with column chunks - Thrift-encoded metadata requires specialized parsing"）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- Row Group / Column Chunk の詳細解析（Thriftエンコーディングが必要）
- フッタメタデータの解析（Thriftコンパクトプロトコル）
- フッタマジック（"PAR1"）のファイル末尾検証（現DSLでは末尾パース不可）
- ページヘッダ（Data Page, Dictionary Page等）の解析
- 列データの圧縮展開（Snappy, GZIP, LZ4, ZSTD等）
- エンコーディング（Plain, RLE, Delta等）のデコード
- 統計情報（min/max, null_count等）の解釈
- Parquet暗号化（Modular Encryption）への対応

## 受入条件

1. [ ] `formats/parquet.bdef.yaml` が存在すること
2. [ ] ヘッダマジック "PAR1" が正しく検証されること
3. [ ] row_groupsがbytesフィールドとしてdescription付きで読み取れること
4. [ ] Thriftエンコーディングの制約がスコープ外として明記されていること
5. [ ] ヘッダマジック検証、構造の説明が正しく動作すること
6. [ ] フォーマット定義がバリデーションに通ること
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにParquetを追記
- [ ] docs/architecture.md — formats/ ディレクトリに parquet.bdef.yaml を追記

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
