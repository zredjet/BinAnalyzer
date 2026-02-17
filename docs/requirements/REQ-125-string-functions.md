# REQ-125: 文字列操作関数の追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-124 |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-02-17 |

## 背景・動機

PDF、ELF、PE等のフォーマットでは、文字列フィールドの値に基づいて後続の処理を分岐させたい場面がある。現在の式エンジンでは文字列の等値比較（`==`）は可能だが、部分一致や文字列加工ができない。

例えば:
- マジック文字列の先頭一致による判定（`substr()`）
- 複数フィールドを結合した識別子の構築（`concat()`）
- 文字列フィールドに特定パターンが含まれるかの判定（`contains()`）

## 機能要件

### 追加する機能

- [ ] `substr(string_field, start, length)` — 部分文字列の抽出
- [ ] `concat(value1, value2, ...)` — 複数の値を文字列として結合
- [ ] `contains(string_field, search)` — 文字列の包含判定（真偽値を返す）

### 変更する既存機能

- [ ] 式エバリュエーター — 新関数の評価ロジック追加（REQ-124の拡張と同じパターン）

### 変更しないもの（スコープ外）

- 正規表現マッチング — 将来課題
- 文字列のトリム・パディング操作 — 将来課題
- 大文字・小文字変換 — 将来課題

## 受入条件

1. [ ] `substr(field, 0, 4)` が先頭4文字を返すこと
2. [ ] `substr` の範囲外アクセスが安全にハンドリングされること（エラーまたはクランプ）
3. [ ] `concat(field1, field2)` が2つの値を文字列結合すること
4. [ ] `concat` が3つ以上の引数を受け付けること
5. [ ] `concat` が数値を文字列に変換して結合できること
6. [ ] `contains(field, "text")` が含む場合に真を返すこと
7. [ ] `contains(field, "text")` が含まない場合に偽を返すこと
8. [ ] 非文字列フィールドへの `substr`/`contains` 適用がエラーになること
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（FunctionCallノードは既存） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 式エバリュエーターに文字列関数の評価ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 関数一覧に追加
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

REQ-124で確立した組み込み関数パターン（`EvaluateFunction` の switch ディスパッチ + 個別の `Evaluate*` メソッド）をそのまま踏襲する。各関数は `IReadOnlyList<ExpressionNode>` と `DecodeContext` を受け取り、`object` を返す。

- `substr`: 範囲外アクセスはクランプ（例外を投げない）。PDF/ELFのマジック比較で安全に使えるようにする。
- `concat`: 可変長引数。`ToString()` で自動変換することで、数値と文字列の混在結合を自然にサポート。
- `contains`: `string.Contains()` を使用。戻り値は `bool` で、`if` 条件や三項演算子での使用を想定。

### モデル変更

なし。`ExpressionNode.FunctionCall` は既に任意の関数名と引数リストを表現できるため、Core への変更は不要。

### インタフェース変更

なし。`ExpressionEvaluator` の public API（`Evaluate`, `EvaluateAsLong`, `EvaluateAsString`, `EvaluateAsBool`）は変更なし。新関数は内部ディスパッチの拡張のみ。

### 代替案

- 文字列演算子（`+` で結合、`in` で包含判定）の追加も検討したが、既存の `+` が数値加算に使われているため、関数形式の方が明確。
- `startswith`/`endswith` も候補だったが、`substr` + `==` で表現可能なためスコープ外とした。

### 懸念事項

- `contains` の戻り値が `bool` であるため、`EvaluateAsBool` で受け取る必要がある。`ConvertToBool` は `bool` 型に対応済み。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

なし。計画通りの実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BuiltinFunctionTests | Substr_ReturnsSubstring | AC1 |
| BuiltinFunctionTests | Substr_StartBeyondLength_ReturnsEmpty | AC2 |
| BuiltinFunctionTests | Substr_LengthExceedsRemaining_ClampsToEnd | AC2 |
| BuiltinFunctionTests | Substr_NonString_Throws | AC8 |
| BuiltinFunctionTests | Concat_TwoStrings | AC3 |
| BuiltinFunctionTests | Concat_ThreeOrMoreArgs | AC4 |
| BuiltinFunctionTests | Concat_NumberToString | AC5 |
| BuiltinFunctionTests | Contains_Found_ReturnsTrue | AC6 |
| BuiltinFunctionTests | Contains_NotFound_ReturnsFalse | AC7 |
| BuiltinFunctionTests | Contains_NonString_Throws | AC8 |

### 気づき・今後の課題
