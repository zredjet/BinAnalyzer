# REQ-185: GUI でのフォーマット定義の検証結果の表示

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-168（GUI モード）、REQ-184（未知キーの検出）。REQ-186（同梱定義の警告の解消）を先に行うのが望ましい |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

REQ-184 で、DSL の未知キー（書き間違い）が検証の警告 VAL123 になった。しかし GUI（デスクトップ・Web のトップページ）はフォーマット定義の検証（`FormatValidator`）を呼んでいないため、GUI で定義を切り替えて使う利用者は警告にもエラーにも気づけない（2026-09-24 に確認）。

| ホスト | 現状 |
|---|---|
| GUI（`BinAnalyzer.Gui`、デスクトップと Web のトップページ） | 検証を呼ばない。`DecodeService` はエラー継続モードでデコードするだけで、定義のエラー（VAL0xx）も警告（VAL1xx）も表示しない。ステータスバーの「検証 n / m」は `expected` / `validate` の結果（データ側）で、定義の検証ではない |
| Web の旧ページ（`/classic`） | エラーのみ表示してデコードを中止。警告は表示しない |
| CLI | デコード前に検証し、エラーで中止・警告は stderr（REQ-184 で VAL123 も出る） |

GUI には定義ビュー（`DefinitionView`、REQ-172 で選択フィールドの行を強調）があり、診断の `file:line` を使えば該当行を示せる。

## 機能要件

### 追加する機能

- [x] GUI でフォーマット定義を読み込んだとき（起動時・定義の切り替え時）に `FormatValidator` を実行し、結果を読み込んだ定義（`FormatDocument`）に保持する
- [x] ステータスバーに定義の診断の件数（エラー / 警告）を表示する。0 件なら表示しない、または目立たない表示にする
- [x] 件数をクリックすると診断の一覧（コード・メッセージ・`file:line`）を表示する
- [x] 一覧の項目を選ぶと、定義ビューで該当行を表示・強調する（インポート先の定義はそのファイルを表示。REQ-172 の仕組みを使う）
- [x] 定義にエラー（VAL0xx 等）がある場合も、現状どおりエラー継続モードでデコードを試み、エラーがあることを明示する

### 変更する既存機能

- [x] `FormatDocument` / `GuiSession` — 定義の検証結果の保持、一覧の開閉
- [x] `StatusBar` — 件数の表示
- [x] `DefinitionView` — 診断の行の強調

### 変更しないもの（スコープ外）

- Web の旧ページ（`/classic`）の表示（トップページが GUI を使うため）
- 定義の編集（REQ-162 は取り下げ済み）
- 検証の内容・重大度の変更

## 受入条件

1. [x] 未知キーを含む定義を GUI で開くと、ステータスバーに警告の件数が表示されること
2. [x] 件数から一覧を開き、項目を選ぶと定義ビューで該当行が強調されること（インポート先の定義を含む）
3. [x] 診断の無い定義では、件数の表示が出ない（または 0 件として目立たない）こと
4. [x] 定義を切り替えると診断が切り替わること
5. [x] 同梱の全定義を GUI で開いたときの診断が CLI の `validate` と一致すること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Gui | 検証の実行と保持、ステータスバー・一覧・定義ビュー |
| BinAnalyzer.Gui.Desktop | 変更なし（見込み） |
| BinAnalyzer.Web | 変更なし（トップページは Gui をホスト） |
| tests/BinAnalyzer.Gui.Tests | bUnit のコンポーネントテスト |

### 変更が必要なドキュメント

- [x] docs/architecture.md — GUI 節に定義の診断
- [x] README.md — GUI の機能一覧に追記
- [ ] CLAUDE.md — 変更不要

---

## 設計メモ

### 設計方針

- **検証は `FormatDocument` が 1 回だけ行う。** `FormatDocument` に `Validation`（`FormatValidator.Validate(Definition)` の結果）を持たせる。定義はカタログ（デスクトップの `DirectoryFormatCatalog`、Web の `HttpFormatCatalog`、テストの `FakeFormatCatalog`）がキャッシュして複数のタブで共有するので、タブや再デコードごとに検証し直さずに済み、カタログ側の変更も要らない。検証はミリ秒単位（同梱の最大の定義で読み込み全体が 5 ms 程度、REQ-184 で計測）
- **ステータスバーには、診断があるときだけ件数を出す。** 「定義: エラー n・警告 m」をボタンとして右側に置く。エラーがあれば赤、警告だけなら黄。0 件なら何も出さない（受入条件 3）。既存の「検証 n / m」（データ側の `expected` / `validate` の結果）とは文言で区別する
- **一覧は定義ビューの上部に置く。** ステータスバーの件数を押すと、右ペインを「定義」に切り替えて一覧を開く（`GuiSession.ShowDefinitionDiagnostics()`）。一覧は折りたたみ式で、各行に重大度・コード・メッセージ・位置（`riff.bdef.yaml:4`）を出す。YAML の該当行を同じ場所で見られるように、別のタブやダイアログにはしない
- **項目を選ぶと、その定義の行へ移る。** 定義ビューは今は「選択ノードの定義行」を表示している。これに「診断の行」という表示対象を足し、項目を選んだら診断の `SourceFile` / `SourceLine` を表示して強調・スクロールする。ツリーでノードを選び直したら、従来どおり選択ノードの定義行に戻る。診断の `SourceFile` が null（文字列から読んだ定義）か、本ファイル（ルート struct の `SourceFile`）と同じなら本ファイル、違えばインポート先としてカタログの `ReadSourceAsync` で取得する（REQ-172 と同じ仕組み）
- **表示中のファイルの診断行に印を付ける。** 定義ビューで、今表示しているファイルに診断がある行へ重大度の印（行のクラスと、マウスを乗せると出るメッセージ）を付ける。一覧を開かなくても YAML を読みながら気づける
- **位置の無い診断も一覧には出す。** VAL107（未使用の enum）のように struct 名も行も無い診断は、一覧に出すが押しても移動しない
- **定義にエラーがあってもデコードは続ける。** 今の GUI の動作（エラー継続モードでデコード）を変えない。CLI のように中止すると、壊れかけた定義を GUI で調べるという用途に合わない。エラーがあることはステータスバーの赤い表示で示す

### 前提: 同梱定義の警告（別要望 REQ-186 として先に対応する）

2026-09-24 の実測では、同梱の 38 定義のうち 15 定義に警告が計 24 件ある。このまま出すと、PNG では出ないが MP4 や ZIP では常に警告が出る状態になり、利用者の書き間違い（VAL123）が埋もれる。内訳:

| 件数 | コード | 内容 | 対応 |
|---|---|---|---|
| 5 | VAL109 | 共通ライブラリのダミーのルート（`_riff_common` / `_isobmff_common`）が到達不能 | 検証器の誤検知。インポート先のルートは到達不能で当然なので対象から外す |
| 4 | VAL103 | zip / protobuf の `virtual` に enum。エンジンは virtual の enum を無視しラベルが出ない（`version_needed_os: = 3`） | virtual でも enum ラベルを出す（定義の意図どおり）か、定義から外す |
| 9 | VAL107 | 未使用の enum（dns / gif / lz4 / mp3 ×5 / tiff） | 定義の掃除（使うか消す） |
| 5 | VAL109 | 到達不能な struct（java-class ×4 / midi） | 定義の掃除 |
| 1 | VAL105 | fat の switch に default が無い | default を足す |

本要望の表示は「診断があれば出す」という単純な規則にし、同梱定義の警告を 0 にする作業は REQ-186 に分ける。REQ-186 の後なら、同梱定義で何か出れば利用者の定義か本物の問題を意味する。

### モデル変更

- `FormatDocument`（Gui/Abstractions）: `public ValidationResult Validation { get; } = FormatValidator.Validate(Definition);`（record の本体に置く。`with` で `Definition` を差し替える使い方は無い）
- `GuiSession`: `bool DefinitionDiagnosticsOpen`、`ShowDefinitionDiagnostics()`（右ペインを定義にして一覧を開く）、`ToggleDefinitionDiagnostics()`

### インタフェース変更

- `IFormatCatalog` の変更なし
- コンポーネント: `StatusBar` に件数ボタン、`DefinitionView` に一覧と行の印。新しいコンポーネント `DefinitionDiagnostics`（一覧）

### 代替案

- **`GuiDocument` がタブごとに検証する。** 同じ定義を開いた複数のタブで検証が重複し、定義の切り替え（`Redecode`）ごとにも走る。定義に付く情報なので `FormatDocument` に置く。不採用
- **診断を専用のタブ（構造 / 定義 / 差分に並ぶ 4 つ目）やダイアログにする。** 診断を押したとき YAML の行を同じ場所で見られない。不採用
- **警告の一部（VAL107 / VAL109 など）を GUI では出さない。** コードごとの表示規則が増え、CLI との一致（受入条件 5）も崩れる。同梱定義の警告を REQ-186 で 0 にする方を取る。不採用
- **エラーがあれば CLI と同じくデコードを中止する。** 上記のとおり GUI の用途に合わない。不採用

### 懸念事項

- Web では `SourceFile` がインポート先の相対 URL（`formats/common/riff.bdef.yaml`）になる。表示名は `Path.GetFileName` で揃うが、`ReadSourceAsync` に渡す識別子はそのまま使う（REQ-172 の定義ビューと同じ）
- 一覧が長い定義（警告が数十件）では、一覧を折りたためるようにして YAML を隠しすぎないようにする

---

## 実装メモ

### 実装中の設計変更

- なし（設計どおり）。表示中のファイルの判定は、診断の `SourceFile` が null なら本ファイル（ルート struct の `SourceFile`）とみなす。`DefinitionView` の表示処理は `Show()` にまとめ、選択ノードの定義行と診断の行の両方から使う

### 確認

- デスクトップ GUI（`-o gui`）で、書き間違い 2 か所を入れた PNG 定義（`expect` / `descripton`）を開くと、ステータスバーに黄色の「定義: 警告 2」が出ることをスクリーンショットで確認した。同じ画面の「検証 0 / 0」は、`expect` の書き間違いでシグネチャの検証が無効になっていることを示しており、この要望で気づかせたかった状態そのもの
- 定義ペイン（一覧を開き 1 件目を選んだ状態）は、bUnit の描画結果に `gui.css` を当ててヘッドレス Chrome で確認した（`expect:` の行が選択の強調と警告の印付きで表示される）。GUI のウィンドウは `dotnet` プロセスでアプリとして登録されていないため、画面操作のツールではクリックできなかった

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Gui.Tests / DefinitionDiagnosticsTests | StatusBar_ShowsWarningCount_AndOpensTheList | 1 |
| Gui.Tests / DefinitionDiagnosticsTests | StatusBar_Errors_ShownInRedWithBothCounts（エラーがあってもデコードは続く） | 1 |
| Gui.Tests / DefinitionDiagnosticsTests | DefinitionView_ListsDiagnostics_WhenOpen | 2 |
| Gui.Tests / DefinitionDiagnosticsTests | SelectingDiagnostic_InMainFile_HighlightsItsLine / SelectingDiagnostic_InImportedFile_ShowsThatFile / SelectingANodeAgain_ReturnsToTheNodesDefinition | 2 |
| Gui.Tests / DefinitionDiagnosticsTests | LinesWithDiagnostics_AreMarked_InTheShownFile | 2 |
| Gui.Tests / DefinitionDiagnosticsTests | StatusBar_NoDiagnostics_ShowsNothing | 3 |
| Gui.Tests / DefinitionDiagnosticsTests | ChangingFormat_SwitchesDiagnostics | 4 |
| Gui.Tests / DefinitionDiagnosticsTests | BundledFormats_GuiDiagnosticsMatchCli（`DirectoryFormatCatalog` と CLI と同じ読み込み・検証の結果を比較） | 5 |

### 気づき・今後の課題

- Web の旧ページ（`/classic`）は今もエラーのみ表示する（スコープ外）
- GUI のスクリーンショットを自動で撮る仕組み（`GuiAutoClose` のような環境変数で、起動後に特定のペインを開いて撮る）があると、README の画像更新や見た目の確認が楽になる
