# REQ-189: 式の未定義の名前の検出

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-184（検証の診断の場所・候補の出し方）、REQ-186（同梱定義で警告 0 件のテスト） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

式（`if:` / `value:` / `size:` / `validate:` など）は、どこにも定義されていない名前を書いても検証で何も言われない。名前はデコード時に初めて引かれ、見つからないと `Variable 'xxx' not found in current scope` のデコードエラーになる。

REQ-188 の画像の見直しで入れた BMP のバグがこの形だった。

```yaml
- name: color_masks
  type: struct
  struct: color_masks
  if: "{header_size == 40 ? compression == 3 or compression == 6 : false}"
```

DSL に `false` のリテラルは無い（式の識別子はすべてフィールド参照になる）。`binanalyzer validate` は警告 0 件で、BITMAPINFOHEADER（40 バイト）のファイルは `false` の枝を通らないので正しく読めた。V4 / V5 のヘッダ（ImageMagick の既定の出力）では `false` が評価されてデコードエラーになった。テストデータが 40 バイトのヘッダだけだったため、テストでも見つからなかった（REQ-188 の「気づき・今後の課題」）。

条件の枝・まれにしか現れないレコード・特定の版だけのフィールドなど、テストデータが通らない式は多い。書き間違い（`compresion`）や、他の言語の感覚で書いた `true` / `false` は、静的に見つけられるものは検証で見つけたい。

## 機能要件

### 追加する機能

- [x] 新しい検証コード **VAL124（警告）**「式が定義のどこにも無い名前を参照している」。式の中の名前（`{name}`、`{name[i]}`、`{name.member}` の先頭の `name`）が、次のいずれでもなければ警告する
  - 定義全体（インポートした struct を含む）のいずれかのフィールドの名前（`virtual` / `switch` / `struct` を含む）
  - いずれかの bitfield のエントリの名前
  - いずれかのテンプレート struct のパラメータの名前
  - 特殊変数 `_index` / `_prev` / `remaining`
- [x] 名前が `true` / `false` のときは、DSL に真偽値のリテラルが無いことと、代わりに `1` / `0` を使うことをメッセージで示す
- [x] それ以外は、定義中の名前のうち近いもの（編集距離 2 以内、大文字小文字を無視。VAL123 と同じ規則）があれば「もしかして」の候補を示す
- [x] 新しい検証コード **VAL125（警告）**「式が組み込み関数に無い関数を呼んでいる」。デコード時の `Unknown function: 'xxx'` を事前に見つける。近い関数名があれば候補を示す
- [x] 検査する式: フィールドの `size` / `element_size` / `if` / `value` / `seek` / `seek_base` / `validate` / `switch_on` / `cases` の条件 / `repeat_count` / `repeat_until` / `repeat_while` / `repeat_max` / `repeat_error_limit` / `state_if` / struct 参照の引数（`struct: name(arg={expr})`）/ チェックサムの `range` / `ranges` の `offset` と `size`、struct の `endianness` の式
- [x] 組み込み関数の名前の一覧を Core に置く（`BuiltinFunctions.Names`）。検証器はこれを使う

### 変更する既存機能

- [x] `FormatValidator` — VAL124 / VAL125 を追加
- [x] VAL123 の候補の計算（`UnknownKeyScanner.Suggest`）を Core の共通の処理（`NameSuggestion`）に移し、VAL124 / VAL125 と共有する。VAL123 の結果は変えない
- [x] 同梱定義 — 検出されたもの（BMP の `false` 2 か所）を直す
- [x] docs/parser-design.md の検証コード一覧に VAL124 / VAL125、docs/dsl-reference.md の式の節に「真偽値のリテラルは無い」「未定義の名前は VAL124」を追記

### 変更しないもの（スコープ外）

- スコープを考えた厳密な判定（「この位置でこの名前が見えるか」）。下記の制限を参照
- メンバー名（`{header.size}` の `size`）の検査。先頭の名前だけを見る
- 状態変数（`{@name}`）の検査。`state:` で宣言されているかの検査は別要望の候補
- 関数の引数の個数・型の検査
- 真偽値のリテラル（`true` / `false`）を DSL に足すこと

### 既知の制限

エンジンは入れ子の struct をデコードすると、その中の値を親のスコープへ昇格する（`PromoteDecodedValues`）。そのため、ある式から見える名前は struct の参照関係とデコードの順序（条件・switch の分岐）で決まり、静的には決めにくい。本要望は **定義全体の名前の集合** で近似する。

- 見つけられるもの: 定義のどこにも無い名前（書き間違い、`true` / `false`、削除したフィールドへの参照の残り）
- 見つけられないもの: 定義のどこかにはあるが、その式からは見えない名前（別の struct の同名でないフィールドを参照している、まだデコードされていない後ろのフィールドを参照している、その枝では定義されない名前を参照している）。これらは従来どおりデコード時のエラーになる

## 受入条件

1. [x] 背景の BMP の定義（`: false`）を `validate` にかけると、`color_masks` の `if` について VAL124 の警告が 1 件出て、メッセージに `false` と「1 / 0 を使う」旨が含まれ、位置はそのフィールドの行であること
2. [x] 定義のどこにも無い名前（例 `compresion`）で VAL124 が出て、近い名前 `compression` が候補として示されること
3. [x] 他の struct のフィールド名・bitfield のエントリ名・テンプレートのパラメータ名・`_index` / `_prev` / `remaining` の参照には VAL124 が出ないこと（昇格による参照を誤検知しない）
4. [x] 機能要件に挙げた各キーの式の中の未定義の名前が検出されること（配列の添字・メンバーアクセスの先頭・関数の引数・三項演算子の各枝の中を含む）
5. [x] 組み込み関数に無い関数名で VAL125 が出て、近い関数名が候補として示されること。組み込み関数の呼び出しには出ないこと
6. [x] Core の組み込み関数の一覧と、エンジンが評価できる関数・dsl-reference.md の組み込み関数の見出しが一致すること
7. [x] 同梱の全定義（`formats/*.bdef.yaml`）で警告が 0 件のままであること（`BundledDefinitionValidationTests`）
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `BuiltinFunctions`、`NameSuggestion`、`ExpressionReferences`（名前の集合・フィールドの式・式の中の参照）。`FormatValidator.ValidateExpressionNames` が VAL124 / VAL125 を出す |
| BinAnalyzer.Dsl | `UnknownKeyScanner.Suggest` が `NameSuggestion` を使う |
| BinAnalyzer.Engine | 変更なし（組み込み関数の一覧との一致をテストで確かめる） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし（既存の診断の表示に加わる） |
| formats/bmp.bdef.yaml | `: false` → `: 0` |

### 変更が必要なドキュメント

- [x] docs/parser-design.md — 検証コード一覧に VAL124 / VAL125
- [x] docs/dsl-reference.md — 式の節に真偽値と未定義の名前の扱い
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **定義全体の名前の集合で判定する。** 変数の束縛はエンジンの実行時の動作（スコープスタック・値の昇格・テンプレート引数の束縛・繰り返しの `_index` / `_prev`）で決まる。昇格により、ある struct の式が兄弟の struct の中のフィールドを参照するのは正しい使い方で、同梱定義でも多い。参照関係とデコード順を静的に追うのは、switch の分岐・`if`・`seek` を考えると誤検知か見逃しのどちらかが大きくなる。定義全体の集合なら誤検知は 0 で、「どこにも無い名前」という最も分かりやすい誤りを確実に見つけられる
- **集合に入れるもの。** エンジンが `SetVariable` する名前と一致させる: フィールド名（全型。`BinaryDecoder` はデコードしたフィールドの名前で束縛する）、bitfield のエントリ名（`DecodeBitfield` と昇格）、テンプレートのパラメータ名（`BindTemplateArgs`）、`_index` / `_prev`（繰り返し）、`remaining`（`ResolveField` の特別扱い）。flags のフィールド名は変数として束縛されないので入れない
- **式の木を歩いて名前を集める。** `FieldReference` と `IndexAccess` の配列名が名前、`FunctionCall` の名前が関数名。`MemberAccess` は対象の式だけを歩き（先頭の名前は検査される）、メンバー名は見ない。`StateReference`（`@name`）は見ない
- **1 つの式の同じ名前は 1 件にまとめる。** `{foo + foo}` で 2 件出さない。別の式（別のキー）なら別の診断
- **場所。** struct 名とフィールド名を診断に入れ、既存の `WithLocation` がフィールドの行を付ける。struct の `endianness` はフィールド名なし（struct の行）
- **候補。** VAL123 の候補計算（大文字小文字を無視した Levenshtein、2 以内、同点は先のもの）を Core の `NameSuggestion` に移して共有する。VAL124 の候補は定義中の名前（定義順）、VAL125 は組み込み関数の名前
- **組み込み関数の一覧は Core に置く。** Core は Engine に依存できないので、評価器の `switch` とは別に名前の一覧を持つ。ずれないように、Engine のテストで「一覧の各名前が評価器で `Unknown function` にならない」こと、Integration のテストで「dsl-reference.md の組み込み関数の見出しと一覧が一致する」ことを確かめる

### モデル変更

なし（IR の変更なし）。

### インタフェース変更

- 新しい診断コード VAL124 / VAL125（警告）。メッセージ例:
  - `フィールド 'color_masks' の if の式 '{header_size == 40 ? ... : false}' が未定義の名前 'false' を参照しています。DSL に真偽値のリテラルはありません。1（真）/ 0（偽）を使ってください`
  - `フィールド 'x' の size の式 '{compresion}' が未定義の名前 'compresion' を参照しています。定義中のどのフィールド・bitfield のエントリ・テンプレートのパラメータにもありません（もしかして 'compression'?）`
  - `フィールド 'x' の value の式 '{lenn(a)}' が組み込み関数に無い関数 'lenn' を呼んでいます（もしかして 'len'?）`
- 追加の public 型: `BinAnalyzer.Core.Expressions.BuiltinFunctions`、`BinAnalyzer.Core.Validation.NameSuggestion`

### 代替案

- **struct ごとに見える名前を計算する（参照関係とデコード順から）。** ある struct の式から見えるのは、自分より前のフィールド・親や祖先のスコープの名前・昇格された兄弟の中身。switch の分岐や `if` で束縛されるかどうかが変わり、ルートから複数の経路で到達する struct は経路ごとに見える名前が違う。正確にやると抽象解釈に近くなり、近似すると誤検知が出る。同梱定義で警告 0 件を保つ（REQ-186）には誤検知が許されないので、不採用。将来、「後ろのフィールドの参照」のような明確な部分だけを別の要望で足す余地は残す
- **`true` / `false` をリテラルとして DSL に足す。** 書き手の直感には合うが、本要望は検出が目的で、DSL の拡張は別の判断になる（`1` / `0` で表現できる）。不採用（スコープ外）
- **未定義の名前をエラーにする。** 利用者の定義が次の版で読めなくなる。また近似なので、将来スコープ判定を足したときに扱いを変えにくい。REQ-184 と同じく警告にする
- **関数名の検査を VAL124 にまとめる。** 名前の種類が違い、候補の集合も違うので、コードを分けた方が絞り込みやすい

### 懸念事項

- 利用者の定義で、実行時にだけ束縛される名前（エンジンが将来足す特殊変数など）を参照していると誤検知になる。特殊変数を足すときは `ExpressionReferences.SpecialVariables` も更新する（テストで `_index` / `_prev` / `remaining` を固定する）
- 組み込み関数を足すときは Core の一覧・評価器・dsl-reference.md の 3 か所を更新する必要がある。テストでずれを検出する
- 背景の BMP の修正は REQ-188 のアーカイブの PR（#18、未マージ）にも同じ変更が入っている。同じ行を同じ内容に変えるので、マージは衝突しない

---

## 実装メモ

### 実装中の設計変更

- **診断を出す処理は `FormatValidator.cs` に置いた。** 当初は検査全体を別クラス（`ExpressionNameValidator`）にするつもりだったが、REQ-183 のドキュメント整合性テスト（`DocConsistency.ExtractValidatorCodes`）は `FormatValidator.cs` のソースの `Error("VALnnn"` / `Warning("VALnnn"` から検証コードを集める。別ファイルに置くと parser-design.md の VAL124 / VAL125 が「実装に無い」ことになるので、名前の集合・フィールドの式の列挙・式の木の走査を `ExpressionReferences`（internal）に分け、`Warning("VAL124", …)` / `Warning("VAL125", …)` は `FormatValidator.ValidateExpressionNames` で出す
- **dsl-reference.md の組み込み関数の節の終わり。** 節の中のコード例に YAML のコメント（`# …`）があるので、節の終わりは `## ` / `### ` の見出しで判定する（`# ` では切らない）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ExpressionNameValidationTests（Core） | VAL124_FalseLiteral_WarnsWithOneZeroHint / VAL124_TrueFalseAnyCase_WarnsWithOneZeroHint / VAL124_FieldNamedFalse_NoWarning | 1 |
| ExpressionNameValidationTests（Core） | VAL124_Typo_SuggestsClosestName / VAL124_NothingClose_NoSuggestion | 2 |
| ExpressionNameValidationTests（Core） | VAL124_NameDefinedInAnotherStruct_NoWarning / VAL124_BitfieldEntryName_NoWarning / VAL124_TemplateParameter_NoWarning / VAL124_SpecialVariables_NoWarning / VAL124_StateReference_NoWarning / VAL124_MemberName_IsNotChecked | 3 |
| ExpressionNameValidationTests（Core） | VAL124_EachExpressionKey_IsChecked（18 キー）/ VAL124_StructEndianness_IsChecked / VAL124_NestedPositions_AreChecked（10 通り）/ VAL124_SameNameTwiceInOneExpression_ReportedOnce | 4 |
| ExpressionNameValidationTests（Core） | VAL125_UnknownFunction_WarnsWithSuggestion / VAL125_BuiltinFunctions_NoWarning / VAL125_FunctionNameIsNotAVariable | 5 |
| BuiltinFunctionTests（Engine） | EveryBuiltinFunctionName_IsKnownToEvaluator | 6 |
| DocumentationConsistencyTests（Integration） | DslReference_BuiltinFunctions_MatchCoreList / DslReference_BuiltinFunctions_DetectsFunctionAddedWithoutDocUpdate / DslReference_BuiltinFunctions_DetectsDocumentedFunctionMissingFromList | 6 |
| ExpressionNameIntegrationTests（Integration） | BmpFalseLiteral_IsReportedAtFieldLine / BmpFalseLiteral_FixedWithZero_HasNoDiagnostics | 1 |
| BundledDefinitionValidationTests（Integration、既存） | BundledFormat_HasNoDiagnostics（BMP の `: false` を戻すと VAL124 で失敗することを確認した） | 7 |

検証の結果（2026-09-24）: 同梱の `formats/*.bdef.yaml` で検出されたのは BMP の `: false` の 2 件だけで、誤検知は 0 件。`formats/common/*.bdef.yaml` を単体で検証しても VAL124 / VAL125 は 0 件。未マージの REQ-188 のアーカイブの PR（#18）の定義（zip / 7z / tar / lz4 / gzip の書き直しを含む）でも警告 0 件。`dotnet test` は 2,271 件すべて通過。

### 気づき・今後の課題

- 状態変数（`{@name}`）が、どのフィールドの `state:` でも宣言されていないことの検査（VAL124 と同じ仕組みでできる）
- 後ろのフィールドの参照（同じ struct の中で、まだデコードしていないフィールドを参照している）は、昇格を考えても静的に見つけられる。誤検知の無い部分から段階的にスコープ判定を足せる
