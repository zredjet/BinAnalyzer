# REQ-195: 入れ子の struct に独自の変数のスコープを持たせる指定

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（REQ-190 の値の昇格の扱いに関わる） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

入れ子の struct の値は、次の 2 つの仕組みで親のスコープに届く。

1. **大きさの無い struct は親とスコープを共有する。** `size:` もエンディアンの指定もテンプレートの引数も無い struct は変数のスコープを作らないので、中のフィールドの値は親のスコープ（最も近い、変数を捕まえるスコープ）に束縛される
2. **繰り返しの要素の値の昇格は、入れ子の struct の子まで再帰する**（`PromoteDecodedValues`。配列の中は走査しない）。要素の中の struct の、そのまた中の struct の値まで、繰り返しを回しているスコープに書かれる

AVI の strh の fccType を、兄弟の strf から引く（dsl-reference の「兄弟スコープ参照」）など、どちらも意図した動きである。一方、**同じ struct が自分を入れ子にする再帰的な定義** では、入れ子の値が同じ名前で親の値を上書きする。REQ-188 のデータ・その他の見直しでは、これを避けるため 8 つの定義（cbor・dns・fat・msgpack・otf・parquet・protobuf・x509）で「エンディアンを書いた形」（`endianness:` + `fields:`）にして、スコープを作らせる回避策を使った。

- DNS の名前: 圧縮ポインタの先の名前（同じ `dns_name`）の `labels` と `is_pointer` が、ポインタを読んだ後で親の値を上書きし、`mail.example.com.` が `example.com.` になった
- CBOR / MessagePack: 入れ子の値の `major_type` / `format_byte` が親の値を上書きし、後ろの `if:` の判定を誤った（旧 MessagePack の定義のバグ）
- Parquet の Thrift の構造体: 入れ子の構造体の「種類」と「直前のフィールドの番号」が昇格して親の値を上書きするので、フィールドの後ろに書き直す virtual を置いた
- FAT のサブディレクトリ: 昇格を止めるために、1 個のサブディレクトリを `repeat_count` の配列として読んでいる

エンディアンの指定は変数のスコープのためのものではなく、読んで意図が分からない。また **昇格は再帰するので、スコープを作っても値は親に届く**（Parquet・FAT の追加の回避策はこのため）。

## 機能要件

### 追加する機能

- [x] **struct の定義に、独自の変数のスコープを持たせる指定を足す**（オブジェクトの形の `scope: isolated`）。指定した struct の中のフィールドの値は、その struct のスコープに束縛され、親のスコープに書かれない。外側の変数は今までどおり読める
- [x] **指定した struct の値は、繰り返しの要素の値の昇格でも親に届かない。** 昇格は指定した struct の中へ再帰しない（外からはメンバーアクセス `{child.field}` で引く）
- [x] 検証・JSON Schema・DSL の DTO で新しいキーを扱う

### 変更する既存機能

- [x] なし（指定しない struct の動作は変えない）

### 変更しないもの（スコープ外）

- エンディアンの形の struct・size 付きの struct の昇格の動作（既存の定義が、昇格で中の値を引いている可能性がある）
- 同梱定義の書き直し（回避策を新しい指定に置き換えるのは実装後の別の PR で。ゴールデンが変わらないことを確かめる）

## 受入条件

1. [x] 指定した struct を再帰させた定義で、入れ子の値が親の同じ名前の値を上書きしないこと（DNS の名前と同じ形: ラベル + 自分と同じ struct の入れ子 + 親の値を使う virtual）
2. [x] 指定した struct の中から、外側のスコープの変数を読めること
3. [x] 指定した struct を要素に持つ繰り返しで、昇格が指定した struct の中へ入らないこと（要素の直下の値は今までどおり昇格する）
4. [x] 外からメンバーアクセスで指定した struct の値を引けること
5. [x] 指定しない struct の動作（スコープの共有・昇格の再帰）が変わらないこと（ゴールデンが変わらない）
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `StructDefinition.IsolatedScope`、`DecodedStruct.IsolatedScope` |
| BinAnalyzer.Dsl | `YamlStructModel.Scope`（`scope:`）、変換と値の検査（`isolated` 以外は読み込みエラー） |
| BinAnalyzer.Engine | `DecodeStruct` で変数のスコープを push、`PromoteDecodedValues` が指定した struct に入らない |
| schemas/bdef.schema.json | 新しいキー |
| tests/BinAnalyzer.Engine.Tests | `IsolatedScopeTests` |
| tests/BinAnalyzer.Dsl.Tests | `IsolatedScopeParsingTests` |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 「変数のスコープ（scope: isolated）」、「兄弟スコープ参照」（昇格が入らない場合）
- [x] docs/format-authoring.md — 「再帰する struct」

---

## 設計メモ

### 設計方針

- スコープは `PushVariableScope`（テンプレートの引数と同じ、変数だけのオーバーレイ）で作る
- 昇格で入らないようにするため、デコード結果の `DecodedStruct` に `IsolatedScope` の印を持たせる（`PromoteDecodedValues` は定義を持たないので、ノードの側で分かるようにした）

### 代替案

- **エンディアンの形の struct を、常に独自のスコープ + 昇格なしにする**: 今の回避策がそのまま正しい意味になるが、エンディアンを切り替えるためだけにオブジェクトの形にした既存の定義（Mach-O・pcap・TIFF など）の昇格が止まり、動作が変わる。不採用
- **昇格の再帰をやめる**: 兄弟スコープ参照（AVI の fccType など）が壊れる。不採用

### 懸念事項

- 名前は `scope: isolated` にした。値を取るキーにして、`mode: bitstream` と同じく今後ほかのスコープの種類を足せるようにした

---

## 実装メモ

### 実装中の設計変更

- **スコープは `DecodeStruct` の先頭で push し、終わりで pop する**（エンディアンのスコープより外側）。中のエンディアンのスコープも変数を捕まえるが、どちらもこの struct の中で閉じるので、値が親に届かない点は同じ
- **昇格は「入れ子の isolated の struct に入らない」とした。** `PromoteDecodedValues` に渡した要素そのもの（呼び出し元の繰り返しの要素）は、isolated でも直下の値を昇格する。要素の子（とその中）の `DecodedStruct` が isolated なら、そこで止める。switch のケースの struct も `DecodedStruct` なので同じ
- 値の検査は `mode:` と同じく読み込み時に行う（`isolated` 以外は `InvalidOperationException`）。未知のキーの検査（VAL123）は DTO の別名から自動で既知になる

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| IsolatedScopeTests | Isolated_RecursiveStruct_KeepsItsOwnValues | 1 |
| IsolatedScopeTests | NotIsolated_RecursiveStruct_IsOverwrittenByTheChild | 5 |
| IsolatedScopeTests | Isolated_ReadsOuterVariables_AndMemberAccessReachesItsValues | 2, 4 |
| IsolatedScopeTests | Isolated_ValueIsNotBoundInTheParentScope | 1 |
| IsolatedScopeTests | Promotion_DoesNotEnterNestedIsolatedStructs | 3 |
| IsolatedScopeTests | Promotion_OfAnIsolatedElement_KeepsItsDirectValues | 3 |
| IsolatedScopeTests | Isolated_SwitchCase_IsNotEnteredByPromotion | 3 |
| IsolatedScopeParsingTests | Load_ScopeIsolated_SetsIsolatedScope / Load_UnknownScope_ThrowsException | 機能要件（DSL） |

### 気づき・今後の課題

- 同梱定義の回避策（cbor・dns・fat・msgpack・otf・parquet・protobuf・x509 のエンディアンの形、Parquet の書き直しの virtual、FAT の 1 個の配列）の置き換えは、スコープ外として別の PR で行う
- REQ-196（独自のスコープの要素の値を repeat_until の条件から見る）は、`scope: isolated` の要素でも同じ問題が起きる

