# REQ-149: 式関数の追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在の式システムでは以下の関数が利用可能である。

- `until_marker()`, `parse_int()`, `len()`, `count()`, `min()`, `max()`, `sum()`, `substr()`, `concat()`, `contains()`

フォーマット定義の記述中に「あと少し関数があれば仮想フィールドで表現できるのに」というケースが存在する。

### 追加が有用な関数の例

| 関数 | 用途 | 具体的なフォーマット例 |
|---|---|---|
| `hex(int)` | 整数を16進文字列に変換 | 識別子の表示（"0x4D5A" 等） |
| `upper(str)` / `lower(str)` | 文字列の大小変換 | FourCCの正規化 |
| `trim(str)` | 空白・null除去 | パディング付き固定長文字列 |
| `popcount(int)` | セットビット数カウント | フラグフィールドの有効フラグ数 |
| `clz(int)` | 先頭ゼロビット数 | アライメント計算 |
| `abs(int)` | 絶対値 | 符号付き高さ等の正規化 |
| `log2(int)` | 対数（2底） | ページサイズのべき乗検証 |

## 機能要件

### 追加する機能

- [x] `hex(value)` — 整数を `"0x"` プレフィックス付き16進文字列に変換
- [x] `upper(str)` — 文字列を大文字に変換
- [x] `lower(str)` — 文字列を小文字に変換
- [x] `trim(str)` — 先頭・末尾の空白とnull文字を除去
- [x] `popcount(value)` — セットされたビットの数を返す
- [x] `abs(value)` — 絶対値を返す
- [x] `log2(value)` — 2を底とする対数（整数部）を返す

### 変更する既存機能

- [x] `ExpressionEvaluator` — 各関数の評価ロジック追加
- [x] `ExpressionParser` — 新しい関数名の認識（既存の関数呼び出し構文で対応可能）

### 変更しないもの（スコープ外）

- ユーザー定義関数 — スコープ外
- 関数のオーバーロード — スコープ外
- 浮動小数点を返す関数 — `log2` は整数部のみ返す

## 受入条件

1. [x] `hex(255)` が `"0xFF"` を返すこと
2. [x] `upper("abc")` が `"ABC"` を返すこと
3. [x] `lower("ABC")` が `"abc"` を返すこと
4. [x] `trim("  hello\0\0")` が `"hello"` を返すこと
5. [x] `popcount(0xFF)` が `8` を返すこと
6. [x] `abs(-42)` が `42` を返すこと
7. [x] `log2(256)` が `8` を返すこと
8. [x] 未知の関数名が指定された場合にエラーメッセージが返ること
9. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（関数名はパーサーで既にサポート済みの構文） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `ExpressionEvaluator` — 各関数の評価実装追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 関数リファレンスに新規関数を追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

パーサー（`ExpressionParser`）は汎用的に任意の識別子+`(`を関数呼び出しとして認識するため、パーサー変更は不要。`ExpressionEvaluator.EvaluateFunction`のswitch式に7ケースを追加し、各関数をprivate staticメソッドとして実装する。既存パターン（引数チェック→評価→変換→結果返却）に従う。

`popcount`と`log2`には`System.Numerics.BitOperations`を使用。

### モデル変更

なし。`ExpressionNode.FunctionCall`は既にfunc名と引数リストを持つ汎用型。

### インタフェース変更

なし。公開APIに変更なし。

### 代替案

- 文字列関数（upper/lower/trim）で非文字列引数を自動ToString変換する案 → 型安全のためエラーとする方針を採用。
- `hex`の出力形式として`"FF"`（プレフィックスなし）案 → `"0xFF"`形式の方が明示的で読みやすいため採用。

### 懸念事項

- `abs(long.MinValue)` はOverflowExceptionになるが、long.MinValueが実際に現れるケースは極めて稀のためそのまま伝播。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

計画通り。設計変更なし。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BuiltinFunctionTests | Hex_Returns0xFF | AC-1 |
| BuiltinFunctionTests | Hex_Zero_Returns0x0 | AC-1 |
| BuiltinFunctionTests | Hex_LargeValue | AC-1 |
| BuiltinFunctionTests | Upper_ConvertsToUppercase | AC-2 |
| BuiltinFunctionTests | Upper_NonString_Throws | AC-2 |
| BuiltinFunctionTests | Lower_ConvertsToLowercase | AC-3 |
| BuiltinFunctionTests | Lower_NonString_Throws | AC-3 |
| BuiltinFunctionTests | Trim_RemovesWhitespaceAndNull | AC-4 |
| BuiltinFunctionTests | Trim_AllNulls_ReturnsEmpty | AC-4 |
| BuiltinFunctionTests | Trim_NonString_Throws | AC-4 |
| BuiltinFunctionTests | Popcount_0xFF_Returns8 | AC-5 |
| BuiltinFunctionTests | Popcount_Zero_ReturnsZero | AC-5 |
| BuiltinFunctionTests | Popcount_One_ReturnsOne | AC-5 |
| BuiltinFunctionTests | Abs_Negative_ReturnsPositive | AC-6 |
| BuiltinFunctionTests | Abs_Positive_ReturnsSame | AC-6 |
| BuiltinFunctionTests | Abs_Zero_ReturnsZero | AC-6 |
| BuiltinFunctionTests | Log2_256_Returns8 | AC-7 |
| BuiltinFunctionTests | Log2_1_Returns0 | AC-7 |
| BuiltinFunctionTests | Log2_Zero_Throws | AC-7 |
| BuiltinFunctionTests | Log2_Negative_Throws | AC-7 |
| BuiltinFunctionTests | UnknownFunction_Throws | AC-8 |

### 気づき・今後の課題

- `clz(int)`（先頭ゼロビット数）は要件のテーブルに記載があるが、追加対象の7関数には含まれていないため未実装。必要に応じて別途追加可能。
