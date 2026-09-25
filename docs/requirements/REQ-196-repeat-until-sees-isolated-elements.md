# REQ-196: 独自のスコープを持つ要素の値を repeat_until の条件から見えるようにする

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-195（独自のスコープの指定を足すと、同じ問題が指定した struct でも起きる） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

`repeat_until` の条件は要素をデコードした後に評価する（dsl-reference の「条件成立まで繰り返し」）。要素が **独自の変数のスコープを持つ struct**（エンディアンの形・テンプレートの引数・REQ-195 の指定）だと、要素の中の値はそのスコープに束縛され、スコープは struct のデコードの終わり（`DecodeStruct` の中）で閉じる。条件はその後に評価するので、**要素の値が見えず「変数が見つからない」エラーになる**。

REQ-188 の CBOR の見直しで、不定長の配列の要素（`cbor_value`、エンディアンの形）が break かどうかを `repeat_until: "{is_break == 1}"` で判定しようとしてこのエラーになった。値の昇格（`PromoteDecodedValues`）は条件の評価の後に行うので、昇格した値も間に合わない。CBOR と FAT のディレクトリは、繰り返しの前に既定値の virtual を置き、`repeat_while` で「前の要素の昇格した値」を見る形で避けた。

```yaml
# 回避策（formats/cbor.bdef.yaml）
- name: is_break
  type: virtual
  value: "{0}"
  padding: true
- name: items
  type: struct
  struct: cbor_value
  repeat_while: "{is_break == 0}"
```

要素の値で繰り返しを終える、という素直な書き方ができない。同じ struct を独自のスコープにしない（大きさの無い struct）と、今度は REQ-195 の上書きが起きる。

## 機能要件

### 変更する既存機能

- [x] **`repeat_until` の条件で、独自のスコープを持つ struct の要素の値を引けるようにする。** 要素の直下のフィールドの値（昇格する値と同じ範囲）が条件から見えること
- [x] `element_size` の要素（今でも要素のスコープの中で条件を評価している）の動作は変えない

### 変更しないもの（スコープ外）

- `repeat_while` の評価のタイミング（要素の前）
- 同梱定義の回避策の書き直し（実装後の別の PR で）

## 受入条件

1. [x] エンディアンの形の struct を要素にした `repeat_until` で、要素のフィールドの値を条件に使えること（CBOR の break と同じ形）
2. [x] テンプレートの引数を持つ struct の要素でも同じであること
3. [x] 大きさの無い struct・`element_size` の要素の `repeat_until` の動作が変わらないこと
4. [x] 条件で引けた値が、繰り返しの後ろ（昇格した値）と一致すること
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Engine | `DecodeElementWithScopeAndCondition`（`repeat_until` の要素のデコードと条件の評価） |
| tests/BinAnalyzer.Engine.Tests | `RepeatUntilOwnScopeElementTests` |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 「条件成立まで繰り返し」

---

## 設計メモ

### 設計方針

- **案 1: 条件を評価する前に、要素の値を一時的なスコープに入れる。** 要素のデコードの後、変数だけのスコープを push し、`PromoteDecodedValues(element)` でそこに書いてから条件を評価し、pop する。昇格と同じ値が見える
- **案 2: 昇格の後に条件を評価する。** 繰り返しの本体の順序を「デコード → 昇格 → 条件」にする。単純だが、昇格した値は繰り返しのスコープに残るので、条件が昇格しない値（配列など）を引く場合は案 1 と同じく見えない

### 懸念事項

- REQ-195 で「昇格しない struct」を足すと、案 1 の一時的なスコープにも値が入らない。REQ-195 の指定の struct は「要素の直下の値は昇格する、中の struct には入らない」ので、直下の値は見える

---

## 実装メモ

### 実装中の設計変更

- **案 1 を採り、一時的なスコープを使うのは「独自の変数のスコープを持つ struct」の要素だけにした。** 大きさの無い struct の要素の値は今のスコープに束縛されているので、従来どおりそのまま評価する。すべての要素で一時的なスコープに昇格すると、要素の中のエンディアンの形の struct の値（今は条件から見えない）が、要素の同じ名前の値を上書きして条件の結果が変わりうる（`UntilCondition_OfAPlainElement_StillUsesTheElementsOwnValue`）
- 独自のスコープを持つかは、要素の `DecodedStruct.StructType` から定義を引いて判定する（`HasOwnVariableScope`: `scope: isolated`・静的 / 動的なエンディアン・テンプレートの引数）。switch の要素は選ばれたケースの struct で判定する
- `element_size` の要素も同じ判定で、要素のスコープの中で評価する（大きさの無い struct の要素の動作は変わらない）
- 一時的なスコープは変数だけのオーバーレイ（`PushVariableScope`）で、pop しても位置は動かない。昇格は REQ-195 の規則のまま（要素の直下の値は入れ、入れ子の isolated の struct には入らない）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| RepeatUntilOwnScopeElementTests | UntilCondition_SeesTheValuesOfAnOwnScopeElement（scope: isolated・エンディアンの形） | 1, 4 |
| RepeatUntilOwnScopeElementTests | UntilCondition_SeesTheValuesOfATemplateElement | 2 |
| RepeatUntilOwnScopeElementTests | UntilCondition_WithElementSize_SeesTheValuesOfAnIsolatedElement | 3 |
| RepeatUntilOwnScopeElementTests | UntilCondition_OfAPlainElement_StillUsesTheElementsOwnValue | 3 |

### 気づき・今後の課題

- 同梱定義の回避策（CBOR の不定長の並び・FAT のディレクトリの、既定値の virtual + `repeat_while`）の置き換えは、スコープ外として別の PR で行う

