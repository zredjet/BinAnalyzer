# REQ-190: size 付きの繰り返しでも要素の値を外側のスコープに残す

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | approved |
| 優先度 | 中 |
| 依存 | なし（REQ-099 の兄弟スコープへの値の昇格の上で行う） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

`size:` と `repeat:`（`repeat_count` / `repeat_until` / `repeat_while` を含む）を一緒に指定したフィールドは、配列全体のバウンダリスコープの中で繰り返す（`BinaryDecoder.DecodeRepeatedField` の `context.PushScope(boundarySize)`）。要素ごとに `PromoteDecodedValues`（REQ-099）で昇格した値もこのスコープに入り、繰り返しの終わりの `PopScope` でまとめて捨てられる。

size の無い繰り返しでは、同じ値が外側の struct のスコープに届き、後ろのフィールドから `{name}` で参照できる。同梱定義もこれを使っている（PNG の sBIT / tRNS / bKGD は、前のチャンク IHDR の `color_type` で switch する）。

つまり **size を付けるかどうかで、繰り返しの後ろから見える変数が変わる**。size はバイト数の境界のために付けるもので、変数の見え方を変える意図は無い。dsl-reference.md の「配列全体のサイズ指定（size + repeat）」にも変数の扱いは書かれていない。

### 実例: ZIP の Zip64 拡張フィールド

REQ-188 のアーカイブの見直し（PR #18、ブランチ `req-188-archives`）で見つかった。Central Directory のヘッダのサイズ・位置が 0xFFFFFFFF のときは、拡張フィールド（`size: "{extra_length}"` + `repeat: eof`）の中の Zip64 拡張フィールド（ID 0x0001）の値を使う。素直に書くと次のようになるが、`zip64_compressed_size` が繰り返しの外に届かないのでデコードに失敗する。

```yaml
central_directory_header:
  # ...
  - name: extra
    type: struct
    struct: central_extra_field      # ID・長さ・switch（0x0001 → zip64_central_extra）
    size: "{extra_length}"
    repeat: eof
  - name: compressed_size_actual
    type: virtual
    value: "{compressed_size == 0xFFFFFFFF ? zip64_compressed_size : compressed_size}"
    # → Variable 'zip64_compressed_size' not found in current scope
```

PR #18 では、拡張フィールドの並びを `central_extra_fields` struct で包み（中は size の無い `repeat: eof`）、その中で virtual を計算して、外から `extra.compressed_size_actual` で引く形で避けた。構造が 1 段深くなり、ツリー表示にも `extra.fields` という余分な段が出る。

## 機能要件

### 変更する既存機能

- [ ] **size 付きの繰り返しの境界スコープを抜けるとき、その間に束縛された変数を外側のスコープに移す。** 対象は `size: N` / `size: "{式}"` / `size: remaining` のすべて。移すのは、昇格した要素の値、要素のフィールドの値（size の無い struct の要素は昇格ではなく直接束縛される）、エラー継続モードで失敗したフィールドの「未定義」の印。移し先は `SetVariable` と同じ（最も近い、変数を持つスコープ）で、size の無い繰り返しが直接書き込む先と一致する
- [ ] **`_index` / `_prev` は移さない。** 繰り返しの後ろでは、外側の繰り返しの `_index` / `_prev`（あれば）がそのまま見える。今の size 付きの繰り返しの動作を保つ（設計メモ参照）
- [ ] `DecodeContext` に、スコープを抜けて変数を外側に移すメソッドを足す
- [ ] docs/dsl-reference.md の「配列全体のサイズ指定」「兄弟スコープ参照」、docs/parser-design.md の値の昇格の説明、docs/architecture.md の DecodeContext の説明に、size 付きの繰り返しの変数の扱いを書く

### 変更しないもの（スコープ外）

- size 付きの struct / switch（繰り返しでないもの）のスコープ。従来どおり中の値は外に出さず、外からは REQ-137 のメンバーアクセス（`a.b`）で引く
- size の無い繰り返しが `_index` / `_prev` を外側に漏らすこと（「気づき・今後の課題」参照）。`PrevVariableTests.RepeatCount_ScalarPrev` が「繰り返しの後の `_prev` は最後の要素」を確かめているので、変えるなら別の要望で決める
- `element_size` の要素ごとのスコープ。struct の要素は従来どおり昇格で値が出る
- ビットストリームモードの繰り返し。size はビット幅で、境界スコープを作らないので変わらない
- DSL の構文。新しいキーは足さない
- `formats/zip.bdef.yaml` の書き直し。回避策は PR #18 にあり、main にはまだ無い。PR #18 のマージ後に別の PR で行う（実装メモ参照）

## 受入条件

1. [ ] size 付きの繰り返し（`repeat: eof` / `repeat_count` / `repeat_while` / `repeat_until`）の要素の中の値（要素の size 付きの switch の中の値を含む）を、繰り返しの後ろのフィールドが `{name}` で参照できること（背景の ZIP と同じ形の定義）
2. [ ] 同じ定義から size を外した繰り返しと同じ値になること
3. [ ] 外側に同名の変数があると、size の無い繰り返しと同じく最後の要素の値で上書きされること
4. [ ] 外側の繰り返しの要素の中に size 付きの繰り返しがあっても、その後ろの `_index` / `_prev` が外側の繰り返しの値であること
5. [ ] size 付きの繰り返しが入れ子になっていても、内側の値が外側の繰り返しを経て外に届くこと（要素の配列の中身は昇格しないため、昇格だけでは届かない形）
6. [ ] 移った値は最も近い変数のスコープ（size 付きの struct など）までで、その外には出ないこと。外からはメンバーアクセスで引けること
7. [ ] スカラー要素の size 付きの繰り返しの名前は、繰り返しの後も配列全体を指すこと（`{bytes[1]}`）
8. [ ] 同梱定義のゴールデンファイルが変わらないこと
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DecodeContext.PopScopeCarryingVariables`、`BinaryDecoder.DecodeRepeatedField` の境界スコープの Pop をこれに変える |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| tests/BinAnalyzer.Engine.Tests | size 付きの繰り返しの変数のテスト、`DecodeContext` のテスト |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 「配列全体のサイズ指定」「兄弟スコープ参照」
- [ ] docs/parser-design.md — 兄弟スコープへの値の昇格、実装済みの拡張の表
- [ ] docs/architecture.md — DecodeContext の説明
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **境界スコープを抜けるときに、その変数を外側へ移す。** `DecodeContext.PopScopeCarryingVariables(params ReadOnlySpan<string> localNames)` を足し、`DecodeRepeatedField` の境界スコープの `PopScope()` をこれに変える。スコープを Pop した後、その変数を 1 つずつ `SetVariable` し直すので、移し先は `SetVariable` と同じ規則（最も近い、変数を持つスコープ）になり、size の無い繰り返しが直接書き込む先と一致する
- **繰り返しの間の読み出しは今と同じ。** 繰り返しの間は、要素の値が境界スコープにあって外側の同名の変数を隠す。size の無い繰り返しは外側を直接上書きするが、読み出しの結果は同じ。変わるのは繰り返しの後ろから見える値だけ
- **`_index` / `_prev` は移さない。** この 2 つは繰り返しの進み具合を表す値で、繰り返しの外では意味が無い。要素の中に size 付きの繰り返しがある形で、その後ろで外側の繰り返しの `_index` を使う定義がある。PR #18 の ZIP の `local_file` は、`header`（中に size 付きの `extra` の繰り返し）の後ろで `central_directory.entries[_index]` を使って data と data_descriptor を読む。移すと `_index` が拡張フィールドの番号に変わり、別のエントリのサイズで読んでしまう。今の size 付きの繰り返しは `_index` / `_prev` を捨てているので、それを保つ
- **同名の変数の上書き（shadowing）。** 移すと、外側の同名の変数は最後の要素の値で上書きされる。size の無い繰り返しと同じで、dsl-reference.md の兄弟スコープ参照の「同名のフィールドは後続の要素で上書き」の規則どおり。同梱定義の size 付きの繰り返しで、要素の中のフィールド名を繰り返しの後ろで読む箇所が無いことを確かめた（下表。PR #18 の定義を含む）。そのため同梱定義のデコード結果は変わらない見込みで、ゴールデンファイルで確かめる

  | 定義 | フィールド | 要素の中の名前 | 繰り返しの後ろ |
  |---|---|---|---|
  | midi | `mtrk.events`（`size: "{length}"`） | `delta_time`・`status`・`meta_length` 等 | mtrk には無い。次の mtrk は自分の `length` を読んでから使う |
  | mp3 | `mp3.frames`（`size: "{tag_size}"`） | `frame_id`・`frame_size`・`frame_flags` | MPEG フレームヘッダ（`raw` 等。要素と同じ名前を読む前に自分で束縛する） |
  | gzip（PR #18） | `gzip.extra`（`size: "{extra_length}"`） | `subfield_id`・`length` | `original_name` 以降は要素の名前を読まない |
  | zip（PR #18） | `local_file_header.extra`（`size: "{extra_length}"`） | `header_id`・`data_size`・`zip64_*` 等 | local_file_header の最後のフィールド。`local_file` は `_index` だけを読む |

- **既存テストへの影響。** `SizeRepeatCoexistenceTests`（境界と位置）は変数を見ない。`SiblingReferenceTests`（要素の間の昇格）と `PrevVariableTests` は size の無い繰り返し。どれも変わらない見込み

### モデル変更

- なし

### インタフェース変更

- `DecodeContext.PopScopeCarryingVariables(params ReadOnlySpan<string> localNames)` を追加（public。`DecodeContext` は既に public）。既存のメソッドの変更なし
- DSL の動作の変更: size 付きの繰り返しの後ろで、要素の中の名前が参照できるようになる。今まで参照できなかった（デコードエラーか、外側の同名の変数を読んでいた）ので、同じ名前で外側の変数を読んでいた定義は、最後の要素の値を読むように変わる（同梱定義には無い）

### 代替案

- **境界スコープを「変数を捕捉しないスコープ」にする。** フィールド単位の `endianness:` と同じ `capturesVariables: false` で Push すれば 1 行で済み、size の無い繰り返しと完全に同じになる。しかし `_index` / `_prev` も外側に書かれ、外側の繰り返しの `_index` を上書きする（上記の ZIP の `local_file` が壊れる）。不採用
- **昇格した値（`PromoteDecodedValues` の結果）だけを移す。** size の無い struct の要素は、中のフィールドが昇格ではなく直接（境界スコープに）束縛される。昇格の記録だけでは size の無い繰り返しと一致しない。スコープの変数を丸ごと移す方が単純で一致する。不採用
- **値を外に出すかを選ぶ DSL キー（`export:` など）を足す。** size の有無で変数の見え方が変わるのは意図した仕様ではなく、選ばせる理由が無い。DSL・JSON Schema・DTO の変更も要る。不採用
- **size 付きの struct / switch も同じく移す。** これらは REQ-137 のメンバーアクセスで外から引く設計で、移すと外側の変数を広く上書きする。影響が大きいので本要望では変えない

### 懸念事項

- **前の要素の値が残る。** 移した値は外側のスコープに残るので、外側の繰り返しの次の要素からも前の要素の値が見える（size の無い繰り返しと同じ性質）。ZIP の Zip64 のように「ヘッダが 0xFFFFFFFF のときだけ拡張フィールドの値を読む」形では、壊れたファイル（0xFFFFFFFF なのに Zip64 拡張フィールドが無い）で、前のエントリの値を黙って使うことがある。PR #18 の回避策（size 付きの struct で包む）はこの場合にエラーになる。ZIP を書き直すときに、どちらを取るか決める
- **性能。** 繰り返し 1 回あたり、境界スコープの変数（名前の種類の数。要素の数ではない）を 1 回列挙して外側に書くだけ。スコープの辞書は Pop 後に使い回す（REQ-180）ので、割り当ても増えない

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
