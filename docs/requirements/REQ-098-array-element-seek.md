# REQ-098: 配列要素値による繰り返し seek

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-095（実ファイル検証で需要確認後が望ましい） |
| フェーズ | C（中量 DSL 拡張） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

多くのバイナリフォーマットは「オフセットテーブル + データ本体」のパターンを持つ:

- **SQLite**: `cell_pointer_array`（uint16 配列）の各値がセルデータへのオフセット
- **PE**: セクションテーブルの `PointerToRawData` が各セクションデータへのオフセット
- **ELF**: プログラムヘッダの `p_offset` がセグメントデータへのオフセット
- **OTF**: テーブルレコードの `offset` が各テーブルデータへのオフセット（現在は seek_restore で個別対応）
- **ICC**: タグエントリの `offset` がタグデータへのオフセット（同上）

現在の DSL では配列要素の値を使って繰り返し seek する汎用的な仕組みがなく、OTF/ICC では switch + seek_restore で個別対応している。配列の各要素をオフセットとして構造体を読む汎用機能があれば、これらのパターンを統一的に表現できる。

## 機能要件

### 追加する機能

- [x] 繰り返しフィールドで「前の配列フィールドの各要素値」を seek オフセットとして使用する仕組み
  ```yaml
  # 案2（採用）: 繰り返し内で配列インデックス参照
  - name: cells
    type: struct
    struct: table_leaf_cell
    repeat_count: "{number_of_cells}"
    seek: "{cell_pointers[_index]}"   # _index は繰り返しの現在インデックス
    seek_restore: true
  ```
- [x] 各イテレーションで seek → デコード → seek_restore のサイクルが自動的に行われること
- [x] seek 先のデータサイズの指定方法（固定サイズ / 次のオフセットとの差分 / remaining）

### 変更しないもの（スコープ外）

- 配列要素の値に基づく条件分岐（switch で対応可能）
- 多次元配列のインデックス参照
- 配列のスライス操作

## 受入条件

1. [x] 配列フィールドの各要素値をオフセットとして、対応する構造体を繰り返しデコードできること
2. [x] SQLite の cell_pointer_array を使った全セル走査が定義できること
3. [x] 各イテレーションの seek/restore が正しく動作すること
4. ~~[ ] FormatValidator で参照先配列フィールドの存在が検証されること~~ → スコープ外（式エンジンベースの汎用アプローチのため、静的検証は不要）
5. [x] 既存の seek + seek_restore パターン（OTF/ICC）が引き続き動作すること
6. [x] 既存テストが全て通過すること（752件全パス）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 式エンジンに `LeftBracket`/`RightBracket` トークン、`IndexAccess` ノード、パーサー拡張 |
| BinAnalyzer.Dsl | 変更なし（既存の `seek`/`seek_restore` プロパティを再利用） |
| BinAnalyzer.Engine | `IndexAccess` 評価、`_index` 変数設定、配列要素値保存、要素ごと seek |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

---

## 設計メモ

### 方針: 案2（汎用式ベース）を採用

案1（`seek_from` 専用プロパティ）は簡潔だが用途が限定的。案2（式エンジン拡張）を採用した理由:

- 既存の `seek` / `seek_restore` プロパティを再利用（新 YAML プロパティ不要、DSL パーサー変更不要）
- `field[expr]` は seek 以外（size, if, value 等）でも使える汎用機能
- `_index` は配列インデックス参照以外にもイテレーション番号ベースの条件分岐等に有用
- 式エンジンの拡張パターンは REQ-097（三項演算子）と同じで確立済み

### 後方互換の担保

`UsesIterationContext()` ヘルパーで seek 式の AST を走査し、`_index` 参照または `IndexAccess` ノードを含む場合のみ要素ごと seek とする。それ以外は従来通り一括 seek。

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `src/BinAnalyzer.Core/Expressions/ExpressionToken.cs` | 修正 | `LeftBracket`, `RightBracket` 追加 |
| `src/BinAnalyzer.Core/Expressions/ExpressionTokenizer.cs` | 修正 | `[`, `]` トークン化 |
| `src/BinAnalyzer.Core/Expressions/ExpressionNode.cs` | 修正 | `IndexAccess` レコード追加 |
| `src/BinAnalyzer.Core/Expressions/ExpressionParser.cs` | 修正 | `identifier[expr]` パース |
| `src/BinAnalyzer.Engine/ExpressionEvaluator.cs` | 修正 | `IndexAccess` 評価 + `ResolveIndexAccess` |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | 修正 | `_index` 変数、配列値保存、`UsesIterationContext`、要素ごと seek |
| `formats/sqlite.bdef.yaml` | 修正 | cells を `seek: "{cell_pointer_array[_index]}"` に変更 |

### 実装中の設計変更

- DSL パーサー（BinAnalyzer.Dsl）の変更は不要だった。既存の `seek`/`seek_restore` YAML プロパティをそのまま利用でき、式文字列 `"{cell_pointer_array[_index]}"` は `ExpressionParser.Parse()` で自動的にパースされる。
- 受入条件4（FormatValidator での静的検証）はスコープ外とした。式エンジンベースの汎用アプローチでは、`field[expr]` の参照先が配列変数かどうかは実行時にしか判定できないため。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| IndexAccessExpressionTests | IndexAccess_Parse_LiteralIndex | 式パース |
| IndexAccessExpressionTests | IndexAccess_Parse_FieldRefIndex | 式パース |
| IndexAccessExpressionTests | IndexAccess_Parse_ExpressionIndex | 式パース |
| IndexAccessExpressionTests | IndexAccess_Parse_InArithmetic | 式パース |
| IndexAccessExpressionTests | IndexAccess_Parse_UnmatchedBracket_Throws | エラーハンドリング |
| ArrayIndexEvaluatorTests | IndexAccess_ReturnsElementValue | 1 |
| ArrayIndexEvaluatorTests | IndexAccess_WithFieldReferenceIndex | 1 |
| ArrayIndexEvaluatorTests | IndexAccess_OutOfRange_Throws | エラーハンドリング |
| ArrayIndexEvaluatorTests | IndexAccess_NotAnArray_Throws | エラーハンドリング |
| ArrayIndexEvaluatorTests | IndexAccess_NegativeIndex_Throws | エラーハンドリング |
| ArrayElementSeekTests | RepeatCount_SetsIndexVariable | 1 |
| ArrayElementSeekTests | RepeatCount_StoresArrayValues | 1 |
| ArrayElementSeekTests | PerElementSeek_WithIndexAccess | 1, 3 |
| ArrayElementSeekTests | PerElementSeek_WithSeekRestore | 3 |
| ArrayElementSeekTests | PerElementSeek_StructElements | 1 |
| ArrayElementSeekTests | ExistingSeekPlusRepeat_Unchanged | 5 |
| ArrayElementSeekTests | NestedRepeat_IndexShadowing | 1 |

### 気づき・今後の課題

- `_index` 変数は全繰り返しモード（Count, UntilEof, UntilValue, While, LengthPrefixed）で設定される。既存式に `_index` 参照はないため動作影響なし。
- 配列要素値の変数保存は全要素がスカラー（integer/string/float）の場合のみ。struct 配列等では `List<object>` に登録しない。
- ネストした繰り返しでは内側の `_index` が外側をシャドウする。スコープスタックではなく `SetVariable` の上書きで実現しているが、内側ループ終了後に外側ループが `_index` を再設定するため問題ない。
