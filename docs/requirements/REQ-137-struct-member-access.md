# REQ-137: 構造体メンバーアクセス式

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在の式システムでは、スカラ配列のインデックスアクセス（`array[i]`）はサポートされているが、構造体配列のメンバーアクセス（`array[i].field`）はサポートされていない。

この制約により、以下のようなパターンが表現できない。

```yaml
# 構造体配列のメンバー参照 — 現在不可
- name: sections
  type: struct
  struct: section_header
  repeat_count: "{num_sections}"

- name: section_data
  type: bytes
  size: "{sections[_index].raw_size}"
  seek: "{sections[_index].raw_offset}"
  repeat_count: "{num_sections}"
```

構造体の変数バインディングでは、各フィールドが個別にスコープに登録されるが、構造体をネストした場合の階層的なアクセスパスは提供されていない。

### 影響を受ける既存フォーマット定義

| フォーマット | パターン | 現状の制約 |
|---|---|---|
| PE | セクションヘッダ配列 → 各セクションデータの参照 | セクションヘッダのフィールドを後続フィールドから参照不可 |
| ELF | プログラムヘッダ配列 → セグメントデータの参照 | 同上 |
| ICC | タグテーブル → 各タグのoffset/sizeの参照 | 同上 |
| OTF | テーブルレコード配列 → 各テーブルのoffset/lengthの参照 | 同上 |
| SQLite | セルポインタは動作済み（スカラ配列のため） | 構造体配列では不可 |

## 機能要件

### 追加する機能

- [x] 式パーサーにドット記法（`array[i].field`）のサポートを追加
- [x] 構造体配列のデコード後、各要素のフィールドを階層的に変数登録する仕組み
- [x] ネスト構造体のメンバーアクセス（`parent.child.field`）
- [x] 配列とドットの組み合わせ（`array[i].nested.field`）

### 変更する既存機能

- [x] `ExpressionParser` — ドット記法のASTノード追加
- [x] `ExpressionEvaluator` — メンバーアクセスの評価ロジック追加
- [x] `BinaryDecoder` — 構造体デコード結果を辞書として変数登録

### 変更しないもの（スコープ外）

- スカラ配列のインデックスアクセス — 既存動作を維持
- スコープスタックの基本構造 — 変更しない
- 構造体内のフィールドから兄弟フィールドへの参照 — 既存動作を維持

## 受入条件

1. [x] `array[i].field` 式で構造体配列の要素のフィールド値を取得できること
2. [x] `array[0].field` のようなリテラルインデックスで参照できること
3. [x] `array[_index].field` のように変数インデックスで参照できること
4. [x] `parent.child.field` のようなネストメンバーアクセスが動作すること
5. [x] `array[i].nested.field` のような配列+ネストの組み合わせが動作すること
6. [x] 存在しないメンバー名を指定した場合、明確なエラーメッセージが返ること
7. [x] インデックスが範囲外の場合、明確なエラーメッセージが返ること
8. [x] 既存のスカラ配列インデックスアクセスが引き続き動作すること
9. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ExpressionNode` — メンバーアクセスノード追加。`ExpressionParser` — ドット記法パース |
| BinAnalyzer.Dsl | 変更なし（式はテキストとしてDSLから渡される） |
| BinAnalyzer.Engine | `ExpressionEvaluator` — メンバーアクセス評価。`DecodeContext` — 構造体変数の階層登録 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 式リファレンスにメンバーアクセス構文を追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

式パーサーに `.member` のドット記法を後置演算子として追加。構造体のデコード結果を `Dictionary<string, object>` として変数登録し、メンバーアクセスで辿れるようにする。`DecodeContext` の変更は不要（`SetVariable/GetVariable` は `object` を扱うため辞書もそのまま格納可能）。

### モデル変更

- `ExpressionTokenType.Dot` — 新トークン型
- `ExpressionNode.MemberAccess(ExpressionNode Object, string MemberName)` — 新ASTノード

### インタフェース変更

なし。既存の `DecodeContext.SetVariable(string, object)` をそのまま利用。

### 代替案

- 構造体メンバーを `parent__child` のようなフラット名で登録する案 → ネストが深い場合に名前衝突リスクあり。却下。

### 懸念事項

なし。

---

## 実装メモ

### 実装中の設計変更

なし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| MemberAccessExpressionTests | DotAccess_ParsesFieldReference | 4 |
| MemberAccessExpressionTests | DotAccess_AfterIndexAccess | 1, 2 |
| MemberAccessExpressionTests | DotAccess_ChainedDots | 4 |
| MemberAccessExpressionTests | DotAccess_IndexThenChainedDots | 5 |
| MemberAccessExpressionTests | DotAccess_InArithmetic | 1 |
| MemberAccessExpressionTests | DotAccess_MissingMemberName_Throws | 6 |
| MemberAccessEvaluationTests | MemberAccess_ResolvesStructField | 4 |
| MemberAccessEvaluationTests | MemberAccess_IndexThenMember | 1 |
| MemberAccessEvaluationTests | MemberAccess_ChainedDots | 4 |
| MemberAccessEvaluationTests | MemberAccess_NonDictThrows | 6 |
| MemberAccessEvaluationTests | MemberAccess_MissingMemberThrows | 6 |
| MemberAccessEvaluationTests | MemberAccess_IndexOutOfRangeThrows | 7 |
| MemberAccessEvaluationTests | MemberAccess_WithArithmetic | 1 |
| StructMemberAccessTests | StructField_RegisteredAsDictionary | 4 |
| StructMemberAccessTests | StructArray_ElementsAccessibleByMember | 1, 2 |
| StructMemberAccessTests | NestedStruct_DeepMemberAccess | 4, 5 |
| StructMemberAccessTests | StructArraySeek_WithMemberAccess | 1, 3 |
| StructMemberAccessTests | ExistingScalarArrayIndex_StillWorks | 8 |
| StructMemberAccessTests | SwitchField_RegisteredAsDictionary | 4 |

### 気づき・今後の課題

- `DecodeContext` は変更不要だった（`object` 型で辞書をそのまま格納可能）
- REQ-136（ポインタテーブルseek強化）の `seek_base` 実装で本機能を活用予定
