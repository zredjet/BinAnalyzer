# REQ-191: size 付きのスコープの中から範囲の外へ seek したフィールドを読めるようにする

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（REQ-044 の seek、REQ-190 の size 付きの繰り返しのスコープの上で行う） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

`seek:` は絶対オフセット（ファイル先頭からの位置）へ飛ぶ（REQ-044）。`DecodeContext.Seek` はファイル全体（0〜データ長）の範囲しか検査しないが、読み取り（`EnsureAvailable`）は今のスコープの終わり（`CurrentScope.End`）までしか許さない。そのため、**size 付きのスコープ（`size:` を持つ struct / switch、`size:` 付きの繰り返し）の中から、その範囲の外へ seek して読むと失敗する**。

```
Cannot read 1 bytes at position 0x2A0: only -1080 bytes remaining in scope
```

size は「この領域の大きさ」を表すために付けるもので、ポインタで別の場所を引けなくする意図は無い。dsl-reference.md の seek の説明（「絶対オフセット（ファイル先頭からのバイト位置）を式で指定」）にもこの制約は書かれていない。

### 実例: ELF のシンボル名（REQ-188）

REQ-188 の実行形式の見直し（PR #21、ブランチ `req-188-executables`）で見つかった。ELF のセクションの中身は `size: "{sh_size}"` の switch で種類ごとに読むのが素直だが、シンボル表の各シンボルの名前は **別のセクション（文字列表）** にあり、`seek: "{string_table_offset + st_name}"` で引く。switch の size のスコープの外なので読めない。

PR #21 では switch の `size:` を外し、各ケースが自分で `sh_size` から大きさを決める形（`repeat_count: "{sh_size / 24}"`、`size: "{sh_size}"` など 6 か所）で避けた。同じ問題は次にもある。

- PE のインポート名・エクスポート名（ディレクトリと別のセクションにあるとき。PR #21 は同じセクションにある前提で引く）
- Mach-O のユニバーサルバイナリのスライス（size 付き）の中で、ロードコマンド（size 付き）からシンボル表・コード署名を引くとき（PR #21 はロードコマンドを読み終えてから、値の昇格で得た位置へ seek している）

## 機能要件

### 変更する既存機能

- [x] **seek の行き先が今の境界の外なら、そのフィールドは行き先を含む外側の境界の中で読む。** 今の境界 = 最も内側の size 付きのスコープ（無ければファイル全体）。行き先を含む外側の境界 = 行き先を含む最も内側の size 付きのスコープ（無ければファイル全体）。境界は [先頭, 終わり) で判定する（終わりちょうどのバイトは境界の外で、次の領域にある）
- [x] **広げるのはそのフィールドのデコードの間だけ。** フィールドの後ろでは元の境界に戻る。`seek_restore: true` なら位置も戻る。`seek_restore` が無ければ位置はフィールドの後ろのまま（元の境界の外なので、その後ろの読み取りは従来どおり失敗する）
- [x] **要素ごとの seek（`_index` を使う seek）も要素ごとに同じく扱う**（`repeat_count` / `repeat: eof` / `repeat_until` / `repeat_while`）
- [x] **広げた境界の中では、`remaining`・`size: remaining`・`repeat: eof`・NUL 終端の文字列・`until_marker` が広げた境界の終わりを使う**
- [x] **変数とエンディアンは変えない。** seek したフィールドの値は今までと同じスコープに束縛され、エンディアンは今の struct のまま
- [x] **行き先が今の境界の中なら何もしない**（従来どおり）
- [x] `DecodeContext` に、境界を広げるメソッド（`PushSeekBoundary`）と、スコープの深さを戻すメソッド（`ScopeDepth` / `PopScopesTo`）を足す
- [x] docs/dsl-reference.md の seek の説明、docs/architecture.md の DecodeContext の説明に書く

### 変更しないもの（スコープ外）

- DSL の構文。新しいキー（`seek_scope:` など）は足さない（代替案を参照）
- seek の無いフィールドの境界。size 付きのスコープの中で、seek せずに範囲の外を読むことは従来どおりできない
- size 付きの struct / switch を seek で範囲の外に置くこと。`PushScope` は親の終わりではなくデータ長しか検査しないので、今でもできる（本要望の前から動く）
- ビットストリームモード。size はビット幅で、境界スコープを作らない
- 同梱定義の書き直し。ELF の回避策（PR #21）は、PR #21 と本要望（PR #22）の両方がマージされた後に別の PR で外した（実装メモ参照）

## 受入条件

1. [x] size 付きの switch の中から範囲の外（後ろ）へ seek した NUL 終端の文字列を読めて、seek_restore で戻った後のスコープの中の続きも正しく読めること（ELF のシンボル名と同じ形）
2. [x] 要素ごとの seek（`_index`）でも、各要素を範囲の外から読めること
3. [x] 広げた境界がファイル全体なら、seek したフィールドの `size: remaining` はファイルの末尾までになること
4. [x] 行き先が今の境界の中なら、`size: remaining` は従来どおり今の境界の残りであること
5. [x] size 付きのスコープが入れ子のとき、内側の外で外側の中への seek は、外側の境界までしか広げないこと（Mach-O のスライスと同じ形）
6. [x] seek したフィールドの値が今までと同じく束縛され（後ろの virtual・外からのメンバーアクセスで引ける）、エンディアンも今の struct のままであること
7. [x] seek_restore が無いと、位置は元の境界の外のままで、その後ろの読み取りは従来どおり失敗すること
8. [x] エラー継続モードで seek したフィールドの読み取りが失敗しても、境界が元に戻り、スコープの中の続きを正しく読めること
9. [x] 同梱定義のゴールデンファイルが変わらないこと
10. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DecodeContext.PushSeekBoundary` / `ScopeDepth` / `PopScopesTo`、スコープの `IsBoundary`。`BinaryDecoder.DecodeField` と `DecodeRepeatedField` の 4 つの繰り返しで、seek の後に境界を広げ、フィールド（要素）の後に戻す |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| tests/BinAnalyzer.Engine.Tests | `SeekBoundaryTests` |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 「オフセットジャンプ（seek）」に「size 付きのスコープの外への seek」
- [x] docs/architecture.md — DecodeContext の説明
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **行き先が今の境界の外のときだけ、行き先を含む外側の境界まで広げる。** seek の直後に `DecodeContext.PushSeekBoundary(target)` を呼ぶ。スコープのスタックを内側から見て、最初の境界（size 付きのスコープ・ルート）が行き先を含めば何もしない。含まなければ、行き先を含む次の境界と同じ範囲のオーバーレイスコープを push する。フィールド（要素）の後に `PopScopesTo(push 前の深さ)` で戻す
- **「最も近い、行き先を含む境界」まで。ファイル全体ではない。** Mach-O のスライスの中では、スライスの中の位置はスライスの先頭からの相対で、`remaining` もスライスの残りであってほしい。ルートまで広げると、スライスの末尾より後ろ（次のスライス）まで読めてしまう
- **境界の判定は [先頭, 終わり)。** 終わりちょうどのバイトは境界の外にある（次の領域の先頭）。ELF のテストで、文字列表がシンボル表の直後にあると、最初の名前の位置が switch の終わりちょうどになる。両端を含めると広げずに空の文字列を読んでしまう。ただし、どの境界も含まない行き先（ファイルの終わり）では広げない
- **広げたスコープはオーバーレイで、変数を捕捉しない。** Pop しても位置を動かさない（seek_restore の有無の動作を変えない）。変数は `SetVariable` がこのスコープを素通りして今までと同じ場所に書く。エンディアンは持たないので、今の struct のエンディアンがそのまま使われる
- **広げたスコープも「境界」として扱う。** スコープに `IsBoundary`（size 付きのスコープ・ルート・広げたスコープで true、エンディアン・変数だけのオーバーレイで false）を足す。オーバーレイの Start は push した位置で境界の先頭ではないので、判定から外す。広げたスコープの中の seek は、広げた境界を基準に判定する
- **戻すのは finally で。** 単一のフィールドは `DecodeField` の既存の finally（seek_restore の位置の復帰）の前で戻す。要素ごとの seek は、繰り返しの各回の本体を try/finally で包む（本体の中の `break` と例外でも戻す）。`repeat_until` は要素の後に `IsEof` を見るので、その前に戻す。戻すときは push 前の深さまで Pop するので、seek したフィールドの中で例外のために Pop されなかったスコープも一緒に片付く
- **互換性。** 行き先が今の境界の中なら動作は変わらない。変わるのは今の境界の外への seek で、次の 2 通り。(1) 行き先が今の境界の終わり以降: 今までは読み取りが失敗した（NUL 終端の文字列・`size: remaining`・`repeat: eof` は空になった）。(2) 行き先が今の境界の先頭より前: 今までも今の境界の終わりまでは読めたが、`remaining` が今の境界の終わりまでだった。どちらも、同梱定義のテストとゴールデン・ファズで 1 回も起きないことを確かめた（実装メモ参照）

### モデル変更

- なし

### インタフェース変更

- `DecodeContext.PushSeekBoundary(int)` / `ScopeDepth` / `PopScopesTo(int)` を追加（public。`DecodeContext` は既に public）。既存のメソッドの変更なし
- DSL の動作の変更: size 付きのスコープの中から範囲の外へ seek したフィールドが読める。今まで失敗していた（または空になっていた）ので、それを前提にした定義は無いはず（同梱定義には無い）

### 代替案

- **`seek_scope: file` のようなキーを足し、指定したときだけ広げる。** 範囲の外への seek は今まで失敗するだけだったので、黙って許して困る定義が無い。キーを知らないと同じ回避策を書くことになり、DSL・JSON Schema・検証・DTO の変更も要る。不採用
- **seek したら常にファイル全体まで広げる。** 行き先が今の境界の中でも `remaining` が変わり、今の定義（ICC のタグ・BMP の画素データなど、seek + `size: remaining`）の動作が変わりうる。Mach-O のスライスのように、外側の境界の中に閉じてほしい場合にも合わない。不採用
- **読み取りの境界の検査を、スコープの終わりではなくデータ長にする。** size 付きのスコープの「ここで終わる」が効かなくなり、`remaining` と `repeat: eof` の意味が壊れる。不採用
- **境界を両端を含む [先頭, 終わり] で判定する。** 行き先が今の境界の終わりちょうどのとき広げず、次の領域の先頭のデータを読めない（上記の ELF のテスト）。不採用

### 懸念事項

- **seek_restore の無い seek。** 位置は広げた境界の中（元の境界の外）に残る。元の境界の後ろの読み取りは失敗し、元の境界の Pop で位置は元の境界の終わりに戻る。今までと同じ動作で、seek_restore を付け忘れた定義の誤りを隠さない
- **ファズ。** 広げるのは実在の外側の境界（ファイル全体を超えない）までで、seek の行き先は今までどおり 0〜データ長に限られる。繰り返しの回数・入れ子の深さの上限も変わらないので、読む範囲が広がる以外の変化は無い。既存のファズのテストで確かめる

---

## 実装メモ

### 実装中の設計変更

- **境界の判定を [先頭, 終わり] から [先頭, 終わり) に変えた。** 最初は「今の境界の中（両端を含む）なら何もしない」とした。すると、行き先が今の境界の終わりちょうどのとき（ELF と同じ形のテストで、文字列表がシンボル表の直後にある）に広げず、最初のシンボル名が空文字列になった。終わりちょうどのバイトは境界の外にあるので [先頭, 終わり) で判定する。どの境界も含まない行き先（ファイルの終わり）では広げない

### 確認したこと（2026-09-25）

- **同梱定義で広げる場面が起きないこと**: 広げたときにログを書く一時的な計装を入れ、main の同梱定義で Integration（ゴールデンを含む 634 件）・Fuzz（295 件）・Cli（32 件）のテストを流した。1 回も広げなかった。ゴールデンファイルも変わらない
- **PR #21 の定義での確認**: `req-188-executables`（PR #21）に本要望の変更だけを当てると、Integration テスト 644 件が通り、ゴールデンは変わらない
- **ELF の回避策を外せること**: 上の状態で `formats/elf.bdef.yaml` のセクションの中身の switch に `size: "{sh_size}"` を戻し、各ケースの `size: "{sh_size}"`（文字列表・ノート・分解しないセクション）と `size: "{sh_size − 24}"` / `"{sh_size − 12}"`（圧縮セクション）を `remaining`・size なしに戻した（7 行追加・13 行削除のうち、6 か所）。次の 7 ファイルの JSON 出力が変更前と 1 バイトも変わらず、ELF のテストとゴールデンも通る。本要望と PR #21 のマージ後に別の PR で行う（下記の「ELF の回避策を外した」を参照）
  - clang で作った x86-64 / i386 / PowerPC / MIPS64 のオブジェクトファイル（zlib で圧縮したデバッグ情報付き）
  - SkiaSharp の libSkiaSharp.so（ARM・AArch64）
  - PR #21 のテスト用の ELF
- **変更前のテスト結果**: `SeekBoundaryTests` のうち、変更前は 5 件が失敗する。次の 3 件は今の動作を保つことを確かめるテストで、変更前から通る
  - `SeekInsideTheCurrentScope_KeepsTheScopeBoundary`
  - `SeekOutsideWithoutRestore_LeavesThePositionOutsideTheScope`
  - `FailureInsideTheSeekedField_RestoresTheScopeBoundary`

  `DecodeContext_PushSeekBoundary_…` は新しいメソッドのテスト

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SeekBoundaryTests | SeekOutsideSizedSwitch_ReadsTheTargetAndReturnsToTheScope | 1 |
| SeekBoundaryTests | PerElementSeekOutsideSizedScope_ReadsEachTarget | 2 |
| SeekBoundaryTests | SeekOutside_RemainingRefersToTheWidenedBoundary | 3 |
| SeekBoundaryTests | SeekInsideTheCurrentScope_KeepsTheScopeBoundary | 4 |
| SeekBoundaryTests | SeekOutsideTheInnerScope_WidensOnlyToTheEnclosingScopeThatContainsTheTarget | 5 |
| SeekBoundaryTests | SeekOutside_KeepsTheEndiannessAndBindsTheValueAsUsual | 6 |
| SeekBoundaryTests | SeekOutsideWithoutRestore_LeavesThePositionOutsideTheScope | 7 |
| SeekBoundaryTests | FailureInsideTheSeekedField_RestoresTheScopeBoundary | 8 |
| SeekBoundaryTests | DecodeContext_PushSeekBoundary_WidensOnlyForTargetsOutsideTheCurrentBoundary | 1, 4, 5 |
| GoldenFileTests（既存） | DecodeResult_MatchesGoldenFile | 9 |

### 気づき・今後の課題

- **ELF の回避策を外した（2026-09-25）。** PR #21・#22 のマージ後の main で、上記の 6 か所を戻した。
  - セクションの中身の switch に `size: "{sh_size}"` を戻した。
  - 文字列表・ノート・分解しないセクション・圧縮データの大きさを、`remaining` か size なしに戻した。
  - シンボル・再配置・動的セクションのエントリ数（`sh_size ÷ エントリのバイト数`）は、仕様どおりの数え方なのでそのまま残した。

  clang のオブジェクトファイル 6 つ（x86-64 / i386 / AArch64 / PowerPC / MIPS64 / RISC-V）、SkiaSharp の .so 2 つ、テスト用の ELF の計 9 ファイルで、JSON 出力が変更前と 1 バイトも変わらないことを確かめた。ゴールデンと全テストも変わらない
- **PE の名前を別のセクションから引く。** 本要望で、PE のインポート名・エクスポート名が別のセクションにある場合も seek では読めるようになった。ただし RVA をファイル上の位置に直す差（`file_delta`）はディレクトリのあるセクションのもので、名前のあるセクションのものではない。RVA からセクションを引く仕組み（セクション表を RVA で探す関数など）が別に要る
- **スコープの push / pop の例外安全性。** size 付きのスコープの push / pop は try/finally ではなく、子のフィールドのエラーはその子の `DecodeField` で捕まえる前提で動いている。本要望で足した境界の広げ直しは finally で戻すが、既存の push / pop は変えていない
