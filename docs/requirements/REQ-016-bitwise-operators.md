# REQ-016: ビット演算子の式サポート

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

バイナリフォーマットではビットマスクやシフト演算が頻出する。例えば:

- フラグフィールドの特定ビットを条件分岐に使用: `{flags & 0x80}`
- 上位/下位ニブルの分離: `{value >> 4}`, `{value & 0x0F}`
- ビット幅に基づくサイズ計算: `{bits << 3}`

現在の式パーサーは算術演算（+, -, *, /, %）と比較演算（==, !=, <, >, <=, >=）のみ対応しており、ビット演算が使えないためDSLの表現力に制約がある。

## 機能要件

### 追加する機能

- [ ] ビット演算子のトークン追加（ExpressionTokenizer）
  - `&`（ビットAND）
  - `|`（ビットOR）
  - `^`（ビットXOR）
  - `<<`（左シフト）
  - `>>`（右シフト）
- [ ] 文法規則への組み込み（ExpressionParser）
  - 優先順位: シフト > 加減算の間、ビットAND/XOR/OR > 比較演算の間（C言語準拠）
- [ ] 評価ロジックの追加（ExpressionEvaluator）
  - 整数型のオペランドに対してビット演算を実行
- [ ] DSL内の式での使用
  - `size: "{length & 0xFF}"`
  - `condition: "{flags & 0x80 != 0}"`
  - `switch_on: "{type >> 4}"`

### 変更する既存機能

- [ ] ExpressionTokenizer: 新トークン種別の追加
- [ ] ExpressionParser: 文法規則の拡張（演算子優先順位の追加）
- [ ] ExpressionEvaluator: 新しいBinaryOperatorの評価

### 変更しないもの（スコープ外）

- ビット反転（`~`）— 将来課題
- ビット代入演算子（`&=`, `|=` 等）— 式は読み取り専用

## 受入条件

1. [ ] `{value & 0xFF}` がビットAND演算として評価されること
2. [ ] `{value | 0x80}` がビットOR演算として評価されること
3. [ ] `{value ^ 0xFF}` がビットXOR演算として評価されること
4. [ ] `{value << 4}` が左シフト演算として評価されること
5. [ ] `{value >> 4}` が右シフト演算として評価されること
6. [ ] 演算子の優先順位がC言語準拠であること（`a + b & c` は `a + (b & c)` ではなく `(a + b) & c` ではない — 要検討）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | ExpressionTokenizer, ExpressionParser, ExpressionNode (BinaryOperator enum) |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | ExpressionEvaluator |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 式の演算子一覧更新
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要
- [ ] docs/parser-design.md — トークン種別、文法規則、演算子優先順位表の更新

---

## 設計メモ

### 設計方針

C言語の演算子優先順位に準拠する。既存のパーサー階層に新しい優先順位レベルを挿入する。

### トークン追加

`ExpressionTokenType` に以下を追加:
```csharp
Ampersand,      // &
Pipe,           // |
Caret,          // ^
LessLess,       // <<
GreaterGreater,  // >>
```

`ExpressionTokenizer` の `switch` 文に追加:
- `&` → Ampersand（`&&` は未サポート、`&` のみ）
- `|` → Pipe（`||` は未サポート、`|` のみ）
- `^` → Caret
- `<<` → LessLess（`<` の先読みで `<` なら LessLess、`=` なら LessThanOrEqual、それ以外は LessThan）
- `>>` → GreaterGreater（同様の先読み）

### BinaryOperator 追加

```csharp
BitwiseAnd,      // &
BitwiseOr,       // |
BitwiseXor,      // ^
LeftShift,       // <<
RightShift,      // >>
```

### 文法規則の変更

C言語準拠の優先順位（低→高）:

```
or_expr       → and_expr ("or" and_expr)*
and_expr      → bitor_expr ("and" bitor_expr)*
bitor_expr    → bitxor_expr ("|" bitxor_expr)*        ← 新規
bitxor_expr   → bitand_expr ("^" bitand_expr)*        ← 新規
bitand_expr   → compare_expr ("&" compare_expr)*      ← 新規
compare_expr  → shift_expr (("==" | "!="|...) shift_expr)?
shift_expr    → add_expr (("<<" | ">>") add_expr)*    ← 新規
add_expr      → mul_expr (("+" | "-") mul_expr)*
mul_expr      → unary_expr (("*" | "/" | "%") unary_expr)*
unary_expr    → ("-" | "not") unary_expr | primary
primary       → INTEGER | STRING | IDENTIFIER | "(" or_expr ")"
```

### ExpressionEvaluator 追加

```csharp
BinaryOperator.BitwiseAnd => ConvertToLong(left) & ConvertToLong(right),
BinaryOperator.BitwiseOr => ConvertToLong(left) | ConvertToLong(right),
BinaryOperator.BitwiseXor => ConvertToLong(left) ^ ConvertToLong(right),
BinaryOperator.LeftShift => ConvertToLong(left) << (int)ConvertToLong(right),
BinaryOperator.RightShift => ConvertToLong(left) >> (int)ConvertToLong(right),
```

### モデル変更

- `ExpressionTokenType`: 5トークン追加
- `BinaryOperator`: 5演算子追加
- `ExpressionParser`: 4メソッド追加（`ParseBitOrExpr`, `ParseBitXorExpr`, `ParseBitAndExpr`, `ParseShiftExpr`）

### インタフェース変更

なし。既存のpublicインタフェースに変更はない。

### 代替案

**案A: ビット演算子の優先順位をC言語と異なる順にする（不採用）**
- C言語の `&` が比較より低い優先順位は直感に反するとの議論もあるが、開発者の慣れを優先しC準拠とする。

### 懸念事項

1. **`<` / `>` との先読み衝突**: `<<` を追加するため `<` の字句解析で先読みが必要になる。現在の `<` は `<=` のみ先読みしているが、`<<` も加えて3分岐にする。同様に `>` は `>=` と `>>` の3分岐。

2. **論理ANDとビットAND**: `and` はキーワードで論理AND、`&` はビットAND。混同しにくいが、ドキュメントで明記する。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
