# REQ-156: hexdump差分表示

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-101, REQ-168 |
| 作成日 | 2026-02-23 |
| 更新日 | 2026-09-24 |

## 背景・動機

> **見直し（2026-09-24）**: 当初は CLI の `diff --hexdump` を主としていたが、その後 REQ-168 の GUI に 2 ファイルの構造差分（差分ペイン、`DiffEngine` の `DiffResult` を表示）ができた。ただしバイト単位の差分はどこにも無い（CLI の `diff` は flat / tree の構造差分、GUI の差分ペインはフィールドの一覧のみでヘックスは強調しない）。主対象を「GUI で差分を開いたとき変わったバイトをヘックスで強調する」に変え、CLI の hexdump 差分は後段に回す。優先度は高 → 中。

REQ-101 で構造化 diff（ツリーレベルの差分表示）が実装済みである。しかし、バイナリファイルのデバッグでは、構造的な差分に加えてバイトレベルの差分を hexdump 形式で確認したいケースが多い。

例えば、同一フォーマットの2つのファイルで「どのバイトが変わったか」を hexdump 上でハイライト表示できると、フォーマット定義のデバッグやバイナリ解析の効率が大幅に向上する。

## 機能要件

### 追加する機能

- [x] GUI: 差分表示中、ヘックスビューで左右のファイルの内容が異なるバイトを強調する
- [x] GUI: 差分ペインでフィールドを選ぶと、ヘックスがその範囲に移動する（既存の選択の仕組みを使う）
- [x] Core: 2 つのバイト列の同じオフセット同士を比べる純関数（`ByteDiff`）。GUI と CLI で共有する（設計で Presentation から Core に変更。差分範囲の列挙ではなく行ごとの比較）
- [x] CLI: `binanalyzer diff <file1> <file2> -f <format> --output hexdump` で差分のある行を hexdump 形式で表示し、差分バイトを色で強調する。`--only-diff` で差分のある行だけを表示する（設計で `--hexdump` から `--output hexdump` に変更）

### 変更する既存機能

- [x] GUI の `HexView` / `HexRowView`（Presentation の `HexRowBuilder`） — 差分バイトの表示
- [x] `binanalyzer diff` コマンド — `--output hexdump` / `--only-diff`

### 変更しないもの（スコープ外）

- 構造化 diff の表示方法変更（REQ-101 の範囲）
- 挿入・削除によるずれの整列（同じオフセット同士を比べる）
- CLI の左右並列（side-by-side）表示
- バイナリパッチ適用（REQ-164 の範囲）

## 受入条件

1. [x] GUI で 2 ファイルの差分を開くと、同じオフセットで値の異なるバイトがヘックスで強調されること
2. [x] 差分ペインでフィールドを選ぶと、ヘックスがその範囲に移動し、強調が見えること
3. [x] 同一内容のファイル同士では強調が出ないこと
4. [x] ファイル長が異なる場合、短い側の末尾を越える範囲が差分として扱われること
5. [x] `binanalyzer diff --output hexdump` で差分のある行が hexdump 形式で表示され、`--only-diff` で差分のある行だけになること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ByteDiff`（同じオフセット同士の比較） |
| BinAnalyzer.Presentation | `HexRowBuilder` / `HexCell` に差分フラグ |
| BinAnalyzer.Gui | ヘックスでの差分強調 |
| BinAnalyzer.Output | `HexDiffOutputFormatter` |
| BinAnalyzer.Cli | `diff --output hexdump` / `--only-diff` |

### 変更が必要なドキュメント

- [x] docs/cli-usage.md — hexdump diff の説明追加
- [x] docs/architecture.md — GUI 節に差分バイトの強調
- [ ] CLAUDE.md — 変更不要
- [x] README.md — 機能一覧の差分比較に追記

---

## 設計メモ

### 設計方針

- **バイトの比較は行ごとにその場で行い、差分範囲を事前に列挙しない。** 表示・出力する行の 16 バイトを、相手のファイルの同じオフセットと比べる。ファイル全体が違うと差分範囲の列挙は巨大になり得るが、行ごとの比較なら仮想化されたヘックスで見えている行の分しかかからない（REQ-177 の大容量ファイル対応と両立する）。差分バイト数の合計だけは、差分を開くときに 1 回数える（`Span.CommonPrefixLength` で一致区間を読み飛ばすので、ほぼ一致するファイルでは速い）
- **比較の関数は Core に置く（`Core/Diff/ByteDiff`）。** GUI（Presentation / Gui）と CLI の出力（Output）で共有するため。Output は Core のみに依存するので、要望時点の「Presentation に置く」から変えた。長さが違う場合、短い側の末尾を越えるオフセットは「差分あり」とする（受入条件 4）
- **GUI: 差分を開いている間だけ、ヘックスに相手のバイト列を渡す。** `HexRowBuilder` に比較相手（省略可）を渡し、`HexCell.Differs` を立てる。`GuiDocument.SetCompareData` で左右それぞれに相手の `Data` を渡し、差分を閉じる・入れ替える・編集で差分が無効になる、のいずれでも付け直す（`GuiSession` の差分の開始・入れ替え・クリアの 1 か所でまとめて行う）。`HexRowView` は `Differs` のセルに `dx` クラスを付け、バイトと ASCII の両方を強調する。差分ペインの要約には「バイト差分 n B」を足す
- **差分ペインからの移動は既存の仕組みを使う。** 差分ペインで項目を選ぶと、既にそのノードを選択してヘックスをスクロールする。強調はそのまま見える（受入条件 2）
- **CLI: `--hexdump` ではなく `--output hexdump` にする。** `diff` は既に `--output flat|tree` を持つので、出力形式の 1 つとして足す方が一貫する。差分のある行を `-`（file1）/ `+`（file2）の 2 行で出し、同じ行は ` ` 1 行で出す。`--only-diff` で差分のある行だけにする。色が有効なら差分バイトを赤 / 緑で強調する。各差分行の右端に、その行の最初の差分バイトが属するフィールドのパス（file1 のデコード結果。file1 に無い範囲は file2）を出す（`HexDumpOutputFormatter` のフィールド収集を共有）。終了コードは他の diff と同じく差分ありで 1

### モデル変更

- Core: `ByteDiff`（`Differs(a, b, offset)`、`RowMask(a, b, rowStart, rowLength)`、`CountDifferences(a, b)`、`RowDiffers(a, b, rowStart, rowLength)`）
- Presentation: `HexCell` に `Differs`、`HexRowBuilder` のコンストラクタに比較相手（省略可）
- Gui: `GuiDocument.CompareData` / `SetCompareData(byte[]?)`、`DiffState.DifferentBytes`
- Output: `HexDiffOutputFormatter`

### インタフェース変更

- CLI: `diff --output hexdump [--only-diff]`

### 代替案

- **差分範囲（`ByteRange` の列）を差分を開くときに全部求める。** 全く違うファイルでは 1 バイトおきの範囲が数百万個になり得る。行ごとの比較で足りるので不採用
- **GUI で左右のヘックスを並べる。** 画面の構成変更が大きく、REQ-148（Web 3 ペイン化）が取り下げられた経緯もある。今回は表示中のタブのヘックスを強調するだけにする（左右は差分ペインの ⇄ とタブの切り替えで行き来できる）
- **CLI に `--hexdump` を足す（要望の当初案）。** `--output` と二重になるので不採用

### 懸念事項

- 挿入・削除でずれたファイルは、ずれ以降がすべて差分になる（同じオフセット同士を比べるため。要望のスコープ外）

---

## 実装メモ

### 実装中の設計変更

- **比較相手の null の渡し方（不具合の修正）。** `HexRowBuilder` の比較相手は `ReadOnlyMemory<byte>?` で受ける。`GuiDocument` から `byte[]?` の null をそのまま渡すと、暗黙の変換で「空の `ReadOnlyMemory`」になり null にならない。すると差分を開いていない通常表示でも「相手は 0 バイト」とみなされ、全バイトが差分として強調される。テスト（差分を閉じたあとの `IsComparing`）で見つかったので、`GuiDocument.NewHex()` で null のときは比較相手を渡さない形にし、通常表示で何も強調されないテスト（`WithoutDiff_NothingIsMarked`）を足した
- **CLI の差分行のフィールド名。** 差分行の右端に、その行の最初の差分バイトが属するフィールドのパスを出す（file1 のデコード結果、無ければ file2）。デコードはエラー継続モードで行い、失敗してもバイト差分は出す
- **色が無効なときの `^^` 行。** リダイレクト時などに、どのバイトが違うかを `-` / `+` の 2 行を見比べずに分かるよう、差分バイトの下に `^^` の行を添える

### 確認

- CLI: `sample.png` と、width・bit_depth を書き換え末尾に 2 バイト足したファイルで `--output hexdump [--only-diff]` を実行し、差分行・`^^`・フィールド名・「差分: 4 バイト（2 行）」・終了コード 1 を確認。同一ファイルでは「差分なし」・終了コード 0
- GUI: 同じ 2 ファイルを比較した状態のヘックスと差分ペインを bUnit の描画結果に `gui.css` を当てて確認（`01` / `08` に橙の枠、ASCII 列に下線、差分ペインに「バイト差分 4 B」）

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Core.Tests / ByteDiffTests | Differs_ComparesSameOffset_AndTreatsMissingSideAsDifferent / RowMask_SetsBitsOfDifferingBytes / RowDiffers_DetectsInnerAndLengthDifferences / CountDifferences_* | 1, 3, 4 |
| Presentation.Tests / HexRowBuilderTests | WithoutCompare_NoCellDiffers / WithCompare_MarksDifferingCells_Only / WithShorterCompare_BytesBeyondItsEnd_Differ | 1, 3, 4 |
| Gui.Tests / ByteDiffHighlightTests | WithoutDiff_NothingIsMarked / Compare_GivesEachSideTheOthersBytes / HexView_MarksDifferingBytes / IdenticalFiles_NoMarks / DifferentLength_BytesBeyondTheShorterEnd_AreMarked / ClearingOrInvalidatingTheDiff_RemovesMarks / Swap_KeepsBothSidesMarked / DiffView_ShowsByteDifferenceCount | 1, 2, 3, 4 |
| Integration.Tests / HexDiffOutputTests | DifferingRow_ShownAsMinusPlusPair_WithCaretsAndFieldPath / OnlyDiff_OmitsEqualRows_AndMarksGaps / DifferentLengths_ExtraBytesAreDifferences / IdenticalFiles_NoDifferences / Color_HighlightsDifferingBytes_WithoutCarets | 5 |
| Cli.Tests / PatchCommandTests | DiffHexdump_ShowsPatchedBytes_AndExitCodes（patch した結果を diff --output hexdump で確認） | 5 |

受入条件 2（差分ペインで選んだフィールドへの移動）は既存の仕組み（差分ペインの項目 → ノード選択 → ヘックスのスクロール、既存テスト）で満たし、強調は選択と重ねて表示される（`.b.dx.sel`）。

### 気づき・今後の課題

- 挿入・削除でずれたファイルは、ずれ以降がすべて差分になる（同じオフセット同士の比較のため）
- 左のタブが短いファイルのとき、右のファイルにだけある末尾のバイトは左のヘックスには表示されない（右のタブで強調される）。左右を並べて表示する形は別要望の候補
