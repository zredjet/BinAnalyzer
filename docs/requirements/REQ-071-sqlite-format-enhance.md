# REQ-071: SQLiteフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-049で実装されたSQLiteフォーマット定義（`formats/sqlite.bdef.yaml`）はファイルヘッダ（100バイト）と第1ページのB-treeページヘッダの基本構造に対応しているが、以下の点が不完全である。

1. **セル/レコード未パース**: B-treeページヘッダの後に続くcell pointer arrayが未解析で、セルの位置情報が取得できない。セルポインタ配列はページ内のレコード位置を示す重要な構造体である。
2. **validate不足**: SQLiteファイルヘッダには固定値であるべきフィールド（max_embedded_payload_fraction=64、min_embedded_payload_fraction=32、leaf_payload_fraction=32等）が存在するが、validateが設定されておらず、壊れたデータベースファイルの検出ができない。
3. **description不足**: page_sizeの特殊値（1は65536を意味する）、file_change_counterの用途、reserved_for_expansionの制約（ゼロであること）等の重要な補足情報がdescriptionに記載されていない。
4. **enum不足**: write_version/read_versionの値（1=legacy、2=WAL）がenum化されておらず、数値のみの表示で可読性が低い。

本要望では、既存の `sqlite.bdef.yaml` にcell pointer arrayの追加、validate/description/enumの充実を行う。

## 機能要件

### 追加する機能

- [ ] cell_pointer_array追加
  - btree_page_headerの後にuint16のrepeat_count("{number_of_cells}")配列
  - 各ポインタはページ内のセルコンテンツ領域へのオフセット
- [ ] database_header validate追加
  - validate: `"{max_embedded_payload_fraction == 64}"`
  - validate: `"{min_embedded_payload_fraction == 32}"`
  - validate: `"{leaf_payload_fraction == 32}"`
- [ ] page_size description改善
  - description: "Page size in bytes. Value of 1 means 65536. Must be a power of 2 between 512 and 65536."
- [ ] schema_format_number validate追加
  - validate: `"{schema_format_number >= 1 and schema_format_number <= 4}"`
- [ ] write_version/read_version enum追加
  - 1 = legacy（ロールバックジャーナル）
  - 2 = WAL（Write-Ahead Logging）
- [ ] file_change_counter description改善
  - description: "Incremented on each transaction commit. Used to detect database modifications."
- [ ] reserved_for_expansion description改善
  - description: "Reserved for expansion. Must be zero."

### 変更する既存機能

- [ ] `formats/sqlite.bdef.yaml` — cell_pointer_array追加、validate追加、description改善、write/read version enum追加

### 変更しないもの（スコープ外）

- セルコンテンツ（レコード本体）の解析（varintエンコーディングが必要）
- B-treeページの再帰走査（interior pageのchild pointer追跡）
- Overflow pageの追跡
- WAL（Write-Ahead Logging）ファイルの解析
- ジャーナルファイルの解析
- 削除レコードの復旧
- フリーリストページチェーンの走査

## 受入条件

1. [ ] cell_pointer_arrayがbtree_page_headerの後にuint16配列（repeat_count: number_of_cells）として追加されていること
2. [ ] max_embedded_payload_fraction, min_embedded_payload_fraction, leaf_payload_fractionにvalidateが設定されていること
3. [ ] page_sizeフィールドに "1 means 65536" の説明がdescriptionに記載されていること
4. [ ] schema_format_numberにvalidate（1以上4以下）が設定されていること
5. [ ] write_version/read_versionにenum（1=legacy, 2=WAL）が設定されていること
6. [ ] file_change_counterにトランザクションコミット時のインクリメントについてdescriptionが記載されていること
7. [ ] reserved_for_expansionに "Must be zero" のdescriptionが記載されていること
8. [ ] 既存のSQLiteヘッダ・B-treeページヘッダ解析に回帰がないこと
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

### 変更が必要なファイル

- [ ] formats/sqlite.bdef.yaml — cell_pointer_array追加、validate/description/enum充実
- [ ] docs/architecture.md — フォーマット一覧の説明更新（SQLite cell pointer array対応を明記）

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
