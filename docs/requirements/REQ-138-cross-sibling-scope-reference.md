# REQ-138: 兄弟フィールドのスコープ横断参照

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-137 |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

repeat内で複数の構造体が順番にデコードされる場合、ある要素のフィールド値が後続の別要素の型やサイズに影響するパターンがある。

### 典型例: AVIのストリームヘッダとストリームフォーマット

AVIフォーマットでは `strl` リスト内に `strh`（ストリームヘッダ）と `strf`（ストリームフォーマット）が連続する。`strf` の内部構造は `strh.fccType` の値（`'vids'` or `'auds'`）によって異なる。

現在のAVIフォーマット定義では、`stream_format` 構造体が `fccType` を参照できている。これは `strh` がデコードされた際に `fccType` が親スコープに変数として昇格（promote）されるためである。

```yaml
stream_format:
  - name: format_data
    type: switch
    size: remaining
    switch_on: "{fccType}"    # strh構造体のfccTypeを参照
    cases:
      "'vids'": bitmap_info_header
      "'auds'": wave_format_ex
```

しかし、この変数昇格は暗黙的であり、以下の制約がある。

1. **同名フィールドの衝突** — 複数の構造体で同名のフィールドがある場合、最後にデコードされた値で上書きされる
2. **明示的な参照パスがない** — どの構造体のフィールドを参照しているかが不明確
3. **repeat内の前要素からの参照** — `_index - 1` 番目の要素のフィールドを明示的に参照する手段がない

## 機能要件

### 追加する機能

- [x] repeat内の前要素のフィールドを明示的に参照する構文（例: `_prev.field` または `elements[_index - 1].field`） — `SetPrevVariable` で実装
- [x] 兄弟構造体のフィールドを名前空間付きで参照する構文（例: `strh.fccType`） — REQ-137で実装済み

### 変更する既存機能

- [x] `DecodeContext` — 構造体デコード結果を名前空間付きで変数登録 — REQ-137で実装済み（`SetVariable` + `BuildStructDictionary`）
- [x] `ExpressionEvaluator` — 名前空間付き変数の解決（REQ-137のメンバーアクセスと統合） — REQ-137で実装済み（`ResolveMemberAccess`）

### 変更しないもの（スコープ外）

- 既存の暗黙的な変数昇格の動作 — 後方互換性のため維持
- スコープスタックの基本構造 — 変更しない

## 受入条件

1. [x] repeat内で前の要素のフィールドを `_prev.field` または配列インデックス式で参照できること — `SetPrevVariable` ヘルパーで全5 repeat モードに対応
2. [x] 兄弟構造体の名前を使って `struct_name.field` で参照できること — REQ-137で実装済み（`DecodeStructField` が `SetVariable(field.Name, BuildStructDictionary(result))` で辞書登録、`ResolveMemberAccess` が解決）
3. [x] 名前空間付き参照と既存の暗黙的変数昇格が共存すること — REQ-137で実装済み（`PromoteDecodedValues` によるスカラー昇格と `BuildStructDictionary` による辞書登録が共存）
4. [x] 同名フィールドの衝突時、名前空間付き参照で曖昧さなく指定できること — REQ-137で実装済み（構造体名を明示すれば一意に解決可能）
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（式ASTはREQ-137で拡張済み） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DecodeContext` — 名前空間付き変数登録。`BinaryDecoder` — 構造体デコード後の変数登録拡張 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — `_prev` セクション追加
- [x] docs/architecture.md — 変更不要（既存のスコープスタック機構をそのまま利用）
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

REQ-137（構造体メンバーアクセス式）の実装により、本REQの機能要件の大部分は既に実現されている。

**REQ-137で実現済みの部分:**

- 兄弟構造体の名前空間参照（`{strh.fccType}`）— `DecodeStructField` が `SetVariable(field.Name, BuildStructDictionary(result))` で構造体デコード結果を辞書として変数登録し、`ResolveMemberAccess` がメンバーを解決する
- 名前空間参照と暗黙的変数昇格の共存 — `PromoteDecodedValues` によるスカラー昇格と `BuildStructDictionary` による辞書登録は独立して動作し、共存している
- 同名フィールド衝突の解消 — 構造体名を明示（`{strh.fccType}`）すれば一意に解決可能

**残る未実装機能:**

- repeat ループ内での前要素参照（`_prev`）のみが未実装
- `BinaryDecoder` の repeat ループ内で、直前要素のデコード結果を `_prev` 変数として登録する
- 式パーサー/AST変更は不要: `{_prev.fccType}` は既存の `FieldReference("_prev")` + `MemberAccess("fccType")` で処理可能
- スカラー要素の場合は `{_prev}` で直接値を取得可能

### モデル変更

なし。式ASTは既存の `FieldReference` + `MemberAccess` で `_prev.field` に対応可能。新しいノード型は不要。

### インタフェース変更

なし。`DecodeContext.SetVariable("_prev", value)` で十分。新しいインタフェースやメソッドの追加は不要。

### 代替案

| 案 | 概要 | メリット | デメリット |
|---|---|---|---|
| **案A: `_prev` のみ（推奨）** | 直前の1要素のみを `_prev` 変数として参照可能 | シンプル。実装コストが小さい。大半のユースケースをカバー | N個前の任意要素を参照できない |
| **案B: ループ中に配列を逐次登録** | `{field_name[_index - 1].member}` でN個前の任意要素を参照可能 | 柔軟。任意の過去要素にアクセス可能 | 配列の逐次登録によるパフォーマンス懸念。インデクサ式のAST拡張が必要 |

**推奨: 案A**。`_prev` で十分なユースケースが大半であり、実装がシンプル。案Bは需要が生じた時点で追加検討する。

### 懸念事項

- **`_prev` の初回要素問題** — ループの最初の要素（`_index == 0`）では前要素が存在しない。対応方針として以下の2案がある:
  - (a) `_prev` が未定義の場合は null として扱い、条件式（`if: "{_index > 0}"`）で保護する
  - (b) `_index == 0` の時は `_prev` を登録しない（未定義変数参照でエラー）
  - 案(a)が安全でDSL作者にとって扱いやすい

---

## 実装メモ

### 実装中の設計変更

- `_index == 0` 時の `_prev` は**登録しない**方式（案b）を採用。DSL作者は `if: "{_index > 0}"` でガードする。
- `SetPrevVariable` ヘルパーメソッドを `PromoteDecodedValues` の直後に配置。
- `UsesIterationContext` に `_prev` を追加し、per-element seek で `_prev` 参照が正しく動作するようにした。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| PrevVariableTests | RepeatCount_ScalarPrev | 1 — スカラー _prev 参照 |
| PrevVariableTests | RepeatCount_StructPrev_MemberAccess | 1 — 構造体 _prev.field メンバーアクセス |
| PrevVariableTests | PrevNotAvailableAtIndex0 | 1 — _index==0 で未定義エラー |
| PrevVariableTests | PrevWithIfGuard | 1 — if ガードで安全にスキップ |
| PrevVariableTests | RepeatUntilEof_Prev | 1 — repeat:eof での _prev 動作 |

### 気づき・今後の課題

- `||`/`&&` は短絡評価なし。`{_index == 0 || _prev.field != 0}` は右辺も評価されるため、ガードには `if` 条件または三項演算子を使用する必要がある。
