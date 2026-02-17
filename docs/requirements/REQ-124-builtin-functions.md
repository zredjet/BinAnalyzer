# REQ-124: 組み込み関数の追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-17 |

## 背景・動機

現在の式エンジンが提供する関数は `parse_int()` と `until_marker()` の2つのみ。フォーマット定義で配列の要素数参照や集計値の計算が必要な場面で、DSLの表現力が不足している。

例えば:
- 配列フィールドの要素数を後続フィールドの条件に使いたい（`len()`）
- サンプルサイズテーブルの最大値・合計値を参照したい（`max()`, `sum()`）
- 値の範囲チェックに最小・最大を使いたい（`min()`, `max()`）

組み込み関数の拡充は、他の多くの要望（フォーマット定義の拡充、import実用化等）の土台となる。

## 機能要件

### 追加する機能

- [x] `len(array_field)` — 配列フィールドの要素数を返す
- [x] `min(array_field)` — 配列フィールドの最小値を返す（数値配列）
- [x] `max(array_field)` — 配列フィールドの最大値を返す（数値配列）
- [x] `sum(array_field)` — 配列フィールドの合計値を返す（数値配列）
- [x] `count(array_field)` — `len()` のエイリアス（可読性向上のため）

### 変更する既存機能

- [x] 式パーサー — 関数呼び出しの解析（既存の `parse_int` パターンを拡張）
- [x] 式エバリュエーター — 新関数の評価ロジック追加

### 変更しないもの（スコープ外）

- ラムダ式・高階関数（`filter()`, `map()` 等） — 将来課題
- ユーザー定義関数 — 将来課題
- 構造体配列の特定フィールドへのアクセス（`sum(entries.size)` のようなドット記法） — 将来課題

## 受入条件

1. [x] `len(array)` が配列の要素数を正しく返すこと
2. [x] `min(array)`, `max(array)` が数値配列の最小値・最大値を返すこと
3. [x] `sum(array)` が数値配列の合計値を返すこと
4. [x] `count(array)` が `len(array)` と同じ結果を返すこと
5. [x] 空配列に対する各関数の挙動が定義されていること（len=0, min/max/sum=エラーまたは0）
6. [x] 非配列フィールドへの適用がエラーになること
7. [x] 非数値配列への `min`/`max`/`sum` 適用がエラーになること
8. [x] 既存の `parse_int()`, `until_marker()` が引き続き動作すること
9. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 関数ASTノードの追加（既存のFunctionCall拡張） |
| BinAnalyzer.Dsl | 変更なし（式パーサーはEngine側） |
| BinAnalyzer.Engine | 式エバリュエーターに新関数の評価ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 関数一覧に追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- パーサー・ASTは変更不要。既存の `FunctionCall` ノードは汎用的で、関数名と引数リストを保持するため新関数にそのまま対応。
- `ExpressionEvaluator.EvaluateFunction()` のswitch文に新関数を追加するのみ。
- 配列フィールドはスコープに `List<object>` として格納済み（スカラー配列）で、`IndexAccess` で既にアクセスできる。この仕組みを活用。

### モデル変更

なし。`ExpressionNode.FunctionCall` は汎用的で変更不要。

### インタフェース変更

なし。`ExpressionEvaluator` の公開APIに変更なし。

### 代替案

- LINQ使用（`list.Min()` 等）: 要素の型変換制御が難しいため、明示的ループを採用。
- 個別メソッド（`EvaluateMin`, `EvaluateMax`, `EvaluateSum`）: 共通処理が多いため `EvaluateMinMaxSum` に統合。

### 懸念事項

- 構造体配列の特定フィールドへのアクセス（`sum(entries.size)` のようなドット記法）は将来課題。現状はスカラー配列のみ対応。

---

## 実装メモ

### 実装中の設計変更

なし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BuiltinFunctionTests | Len_ReturnsArrayLength | 受入条件1 |
| BuiltinFunctionTests | Len_EmptyArray_ReturnsZero | 受入条件5 |
| BuiltinFunctionTests | Len_NonArray_Throws | 受入条件6 |
| BuiltinFunctionTests | Count_ReturnsArrayLength | 受入条件4 |
| BuiltinFunctionTests | Min_ReturnsMinValue | 受入条件2 |
| BuiltinFunctionTests | Min_SingleElement_ReturnsThatElement | 受入条件2 |
| BuiltinFunctionTests | Min_EmptyArray_Throws | 受入条件5 |
| BuiltinFunctionTests | Min_NonArray_Throws | 受入条件6 |
| BuiltinFunctionTests | Max_ReturnsMaxValue | 受入条件2 |
| BuiltinFunctionTests | Max_SingleElement_ReturnsThatElement | 受入条件2 |
| BuiltinFunctionTests | Max_EmptyArray_Throws | 受入条件5 |
| BuiltinFunctionTests | Sum_ReturnsSumOfValues | 受入条件3 |
| BuiltinFunctionTests | Sum_EmptyArray_ReturnsZero | 受入条件5 |
| BuiltinFunctionTests | Sum_SingleElement_ReturnsThatElement | 受入条件3 |
| BuiltinFunctionTests | Sum_NonArray_Throws | 受入条件6 |
| BuiltinFunctionTests | Min_StringArray_Throws | 受入条件7 |
| BuiltinFunctionTests | Max_StringArray_Throws | 受入条件7 |
| BuiltinFunctionTests | Sum_StringArray_Throws | 受入条件7 |
| BuiltinFunctionTests | Len_InArithmeticExpression_Works | 受入条件1 |
| BuiltinFunctionTests | ExistingFunctions_StillWork | 受入条件8 |

### 気づき・今後の課題

- `count()` は `len()` の完全なエイリアスとして実装。同一メソッド `EvaluateLen` を呼び出す。
- `sum(empty_array)` は 0L を返す設計（数学的な空和の慣例に従う）。`min`/`max` は空配列でエラー（未定義のため）。
