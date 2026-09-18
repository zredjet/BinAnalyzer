# REQ-177: GUI の大容量ファイル性能

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-168 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI モード（REQ-168）のヘックスビューは `<Virtualize>` で行を遅延生成するが、数十 MB のファイル（PCAP・ELF・SQLite 等）での体感性能は未計測である。懸念点:

- `NodeIndex.Build` と `ChecksumSummary.Compute` は全ノード走査（ノード数 100 万規模でのメモリと時間）
- ツリーは展開ノードのみ描画するが、配列 500 要素ページングの操作性
- ヘックス行の 16 セル × イベントハンドラ 2 種による描画コスト、ホバー `<style>` 方式の効き
- 構造マップのセグメント上限 256 の妥当性
- Web（WASM）では 100 MB 上限があるが、デスクトップには上限がなく OOM の可能性

## 機能要件

### 追加する機能

- [x] `BinAnalyzer.Benchmarks` に `NodeIndex.Build` / `HexRowBuilder.Build` / `StructureMapBuilder.Build` のベンチマークを追加（1 万・10 万・100 万ノード）— `PresentationBenchmarks` + `SyntheticTree`。`ChecksumSummary` / `NodeSearch` / `FindByPathPattern` / `BreadcrumbBuilder` / `PathOf` も併せて計測
- [x] 実ファイル（生成した 50 MB 級 PCAP）で、開く→選択→検索→スクロールの所要時間を計測し `docs/benchmark-baseline.md` に記録 — `testdata/real` は最大 512 B しかないため、生成した 50 MB（MTU 級ペイロード）と 5 MB（ペイロード無し・小パケット多数）の 2 種で計測。計測は `BINANALYZER_GUI_TIMING=1` のログで行う
- [x] 計測結果に基づく改善 — `NodeIndex` の軽量化、ヘックス／構造マップのイベント委譲、ツリーの平坦化と `<Virtualize>` 化、CLI 側の二重デコード廃止（下記）
- [x] デスクトップでファイルサイズが閾値（既定 256 MB）を超える場合は確認ダイアログ — `GuiSession.LargeFileThreshold` と `PendingLarge`。ピッカー内に「開く / キャンセル」を出す。`BINANALYZER_GUI_LARGE_FILE_MB` で変更、`0` で無効

### 変更しないもの（スコープ外）

- ファイルのメモリマップ / ストリーミングデコード（エンジン側の大きな変更）
- デコード結果モデルのメモリ削減とデコード時間（→ REQ-180）

## 受入条件

1. [x] 100 万ノードの `NodeIndex.Build` が 1 秒以内、`HexRowBuilder.Build` が 1 行 50 µs 以内 — 86 ms / 0.87 µs（改善前 380 ms / 0.48 µs。行生成は祖先 ID 列の組み立てぶん増えたが 50 µs の 1/50）
2. [x] 50 MB の PCAP を開いてから最初の描画まで 3 秒以内、ノード選択の反映が 100 ms 以内 — プロセス起動から最初の描画まで 2.53 s（デコード 1.08 s + 索引 0.35 s を含む）、選択→描画 42 ms
3. [x] 計測結果と採用した改善が `docs/benchmark-baseline.md` に記録されていること
4. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Presentation | `NodeIndex` のパス・祖先列の遅延生成、`OrdinalOf` / `IdByPath`、`FieldKindMapper` の名前キャッシュ、`HexRowBuilder` の葉の使い回し、`TreeRowBuilder`（新規）、`NodeChildren.Count` |
| BinAnalyzer.Gui | `HexView` / `HexRowView` / `StructureMapView` のイベント委譲（`NodeEvents` + gui.js）、`StructTree` の平坦化・仮想化（`StructTreeRow`、`StructTreeNode` は削除）、`GuiSession` の大容量確認、`FilePicker` の確認 UI、`GuiTiming`（計測ログ） |
| BinAnalyzer.Gui.Desktop | `GuiApp.LargeFileThresholdFromEnvironment` |
| BinAnalyzer.Cli | `-o gui` は CLI 側でデコードせず読み込んだデータをそのまま GUI に渡す |
| BinAnalyzer.Benchmarks | `PresentationBenchmarks` / `SyntheticTree`、Presentation への参照 |

### 変更が必要なドキュメント

- [x] docs/benchmark-baseline.md — 「Presentation 層・GUI の大容量ファイル性能」節
- [x] docs/cli-usage.md — 「大きなファイル」節と環境変数 3 つ

---

## 設計メモ

### 設計方針

計測してから直す。改善前のベンチマークで、Presentation 層の中で問題だったのは `NodeIndex.Build` の割り当て（100 万ノードで 513 MB）だけで、ヘックス行・構造マップ・集計は十分速かった。実ファイルでは GUI 側の処理よりエンジンのデコード（1.2M ノードで 1.1 s）とデコード結果のメモリ（780 MB）が支配的で、これはスコープ外なので REQ-180 に切り出した。

1. **NodeIndex の軽量化** — パス文字列と祖先 ID 列はノード数に比例したメモリを食う（1 ノードあたり 200 B 超）。ID が前順（親 < 子）であることを使い、親リンク・順序番号（配列内インデックス / struct 内位置）だけを持ち、`PathOf` / `AncestorIdPath` / `ByPath` / `IsAncestorOrSelf` は親をたどって計算する。`ByPath` はパスを字句解析して子を名前で引く（同名は最初のもの、従来の `TryAdd` と同じ）。`FindByPathPattern` は検索時に全パスを生成する（171 ms / 100 万ノード、明示操作なので許容）
2. **イベント委譲** — ヘックスの 1 行にはセル 16 × 2（hex / ASCII）× ハンドラ 2 種 = 64 個の Blazor ハンドラがあり、構造マップにも数千個あった。Blazor のカスタムイベント（`EventHandlerAttribute`）で `mouseover` / `click` を `nodehover` / `nodeclick` として登録し、gui.js 側で `closest("[data-n]")` からノード ID を引数に載せる。ハンドラはコンテナ 1 つになり、描画バッチが小さくなる。ツリーは可視行が仮想化で有限なので行ごとのハンドラのまま
3. **ツリーの平坦化と仮想化** — 入れ子の `<ul>` をやめ、`TreeRowBuilder` が展開状態から可視行（ID・深さ・「さらに表示」行）を平坦なリストにする。512 行を超えたら `<Virtualize>`。「全て展開」で 100 万行になっても描画は見えている分だけ。選択時はヘックスと同じ `scrollRowTo` で行位置へスクロール（従来はスクロールしていなかった）。配列のページング（500）は行リストの上限として残す
4. **大容量ファイルの確認** — ネイティブの同期ダイアログは Photino.Blazor 下で不安定（REQ-168）なので、`GuiSession` の状態（`PendingLarge`）としてピッカー内に確認を出す。ファイルは既に読み込まれているが、支配的なのはデコード結果のメモリなので読み込み後の確認で十分。デスクトップだけが閾値を設定し、Web は既存の 100 MB 上限のまま
5. **計測の仕組みを残す** — `BINANALYZER_GUI_TIMING=1` で開く各段階・最初の描画・選択の反映時間を標準エラーに出す。`BINANALYZER_GUI_TIMING_SELECT=<id>` で最初の描画の 3 秒後に選択して反映時間を測れる（手動クリック無しで再現できる）

### モデル変更

- `NodeIndex`: `AncestorIdPath` / `PathOf` / `ByPath` は同じ結果を返すが計算で求める。`OrdinalOf(id)` と `IdByPath(path)` を追加。`ElementIndexOf` は O(1)
- `FieldKindMapper.Map` に名前判定キャッシュ（`Dictionary<string, NameTraits>`）の省略可能引数
- `HexCell.AncestorPath` は同じ葉のセルで同じ文字列インスタンスを共有
- `TreeRow` / `TreeRowBuilder`、`NodeChildren.Count` を追加
- `GuiSession.LargeFileThreshold` / `PendingLarge` / `ConfirmLargeFileAsync` / `CancelLargeFile`、`OpenAsync(..., confirmedLarge)`

### インタフェース変更

- `StructTreeNode` コンポーネントを削除し `StructTreeRow` に置き換え（外部からは `StructTree` のみ使う）
- ヘックスのセル・構造マップのセグメントに `@onclick` / `@onmouseover` が無くなった。bunit からは `TriggerEventAsync("onnodeclick", new NodeEventArgs { NodeId })` で起こす
- gui.js の `scrollHexTo(containerId, top)` を `scrollRowTo(containerId, top, height)` に変更

### 代替案

- パス文字列を保持したまま `string.Intern` 等で共有する案 — 配列要素ごとにパスが異なるので共有できない。不採用
- `_ids`（ノード→ID 辞書）を無くし `DecodedNode` に ID を持たせる案 — Core のモデル変更になり、同じノードを複数の索引（差分の左右など）が指す場合に破綻する。残る 143 MB / 100 万ノードのうち辞書は約 40 MB で、REQ-180 のノード自体の削減の方が効く。不採用
- ヘックスセルの `data-p`（祖先 ID 列）をやめてホバー強調を JS で計算する案 — CSS 属性セレクタ方式（REQ-168）はホバー時に `<style>` を 1 つ差し替えるだけで済み、計測上も問題なかった。不採用
- Photino の `ShowMessage` で確認ダイアログを出す案 — 同期呼び出しで、Web と共有できない。不採用

### 懸念事項

- `FindByPathPattern` は検索のたびに全パスを生成する（100 万ノードで 336 MB の一時割り当て）。`PathFilter` が `ReadOnlySpan<char>` を受ければ削減できるが、Core の変更になるので見送り
- 「全て展開」を 100 万ノードで押すと、選択のたびに 100 万行の平坦化（数十 ms）が走る。仮想化で描画は問題ないが、行リスト構築の差分更新は入れていない

---

## 実装メモ

### 実装中の設計変更

- Razor コンパイラは `EventHandlerAttribute` を **4 引数のコンストラクタ形**（`enableStopPropagation` / `enablePreventDefault` 付き）でしか認識しない。2 引数だと `@onnodeclick` が素の属性として出力され、ハンドラにならない（`NodeEvents.cs` にコメント）
- `HexRowBuilder.Build` の葉の使い回しは、「今の葉が終わる」か「別の葉が始まる」オフセットでだけ `LeafAt` を引き直す。ビットストリームのように葉が重なる場合も従来と同じ葉を選ぶ
- 実ファイルの索引構築（1.2M ノードで 390 ms）がベンチマーク（86 ms / 1M）より遅かったので、`Add` / `CountNodes` / `CollectLeaves` に `AggressiveOptimization` を付けて 350 ms に。残りの差はデコード直後の GC 昇格とキャッシュ局所性
- CLI の `-o gui` は従来 CLI 側で一度デコードしてから GUI が再デコードしていた。読み込み直後に GUI へ渡すよう順序を変えた

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| NodeIndexPathTests | ByPath_ReturnsNull_ForMalformedOrMissing 他 7 件（パス解析、順序番号、祖先判定、padding、圧縮、深さ 200） | AC-1（索引の等価性） |
| NodeIndexTests（既存） | PathOf / ByPath / AncestorIdPath の往復 | AC-1 |
| TreeRowBuilderTests | CollapsedRoot / ExpandedRoot / FullyExpanded / Limit_AddsMoreRow / IndexOf / LargeArray | AC-4 |
| StructTreeTests | Rows_AreFlat_WithDepthIndent / LargeArray_ShowsPage_ThenMoreButtonExtends / SelectingHiddenElement_ExpandsAncestors_AndRowAppears | AC-4 |
| LargeFileTests | OverThreshold_BecomesPending_ConfirmOpens / Cancel_DiscardsPending / AtOrUnderThreshold_OrDisabled / UnknownExtension_AfterConfirm / FilePicker_ShowsConfirmation / DesktopThreshold_ReadsEnvironment | 機能要件 4 |
| ComponentTests（更新） | HexView_ClickingCell_SelectsNode / StructureMap_SegmentWidthsProportional_ClickSelects（委譲イベントで起こす） | AC-4 |
| PresentationBenchmarks | NodeIndex_Build / HexRowBuilder_Build64Rows / StructureMapBuilder_Build 他（ベンチマーク） | AC-1 |

### 気づき・今後の課題

- **REQ-180** — デコード結果のメモリ（約 500 B / ノード、50 MB PCAP で 780 MB、5 MB 小パケット PCAP で 1.29 GB）とデコード時間（約 0.8 µs / ノード）。GUI 側を直した後はこれが「開く」の支配項
- **REQ-181** — 配布物サイズ（86〜93 MB）の削減。REQ-176 のメモで「REQ-177 以降」としていたが実行時性能とは別なので切り出し
- **REQ-182** — Web（WASM）版の計測。GUI コンポーネントは共有だがランタイムが違い、実測が無い
- `DiffEngine.Compare` を 100 万ノード同士で取った場合の時間・メモリは未計測（差分は REQ-101 の範囲）
- `ChecksumDependencies.Covering` は編集プレビューのたびに全ノードを走査する（100 万ノードで数 ms）。問題になれば `ChecksumCoverage` を持つノードだけの索引を作る
