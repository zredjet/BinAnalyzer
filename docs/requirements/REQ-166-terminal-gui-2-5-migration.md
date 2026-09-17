# REQ-166: Terminal.Gui 2.5 系への移行

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-17 |
| 更新日 | 2026-09-17 |

## 背景・動機

`BinAnalyzer.Tui` は Terminal.Gui を `2.0.0` に固定し、さらに 2.0.0 が推移的に引き込む脆弱な
`Microsoft.Build.Tasks.Git 8.0.0`（GHSA-23fw-v26w-5fgq）を回避するため
`Microsoft.Build.Tasks.Git 10.0.111` を直接参照するワークアラウンドを抱えていた（fb10246）。

Terminal.Gui 2.5.x では API が大きく再編され（名前空間分割、静的 `Application` の非推奨化、
`Toplevel`/`ColorScheme` の廃止、`TextView` の非推奨化）、`Directory.Build.props` の
`TreatWarningsAsErrors=true` により CS0618（obsolete）警告もビルド失敗になるため、
単純にバージョンを上げるだけでは移行できなかった。

本要望は TUI を Terminal.Gui 2.5 系の新 API へ移行し、バージョン固定と
`Microsoft.Build.Tasks.Git` の上書き参照の両方を撤去することを目的とする。

## 機能要件

### 追加する機能

- なし（ユーザーから見える機能追加はない）

### 変更する既存機能

- [x] `Terminal.Gui` パッケージ参照を `2.0.0` 固定から `2.5.*`（浮動）に戻す
- [x] `Microsoft.Build.Tasks.Git` の上書き参照とそのコメントを削除する
- [x] 名前空間の移動に追随する（`Terminal.Gui.App` / `Terminal.Gui.ViewBase` / `Terminal.Gui.Views` / `Terminal.Gui.Drawing` / `Terminal.Gui.Drivers`）
- [x] 静的 `Application.Init()`/`Run()`/`Shutdown()` をインスタンスベースの `Application.Create().Init()` + `IApplication.Run()` に置き換える
- [x] ルートビュー `Toplevel` を `Runnable` に置き換える
- [x] ステータスバーの配色を `ColorScheme` から `Scheme` + `View.SetScheme()` に置き換える
- [x] 非推奨の `TextView`（Detail ペイン・Hex ペイン）を非推奨でない読み取り専用ビューに置き換える
- [x] グローバルキーバインド（q / / / n / N / e / c、検索バー内の Enter / Esc）が 2.5 でも動作すること

### 変更しないもの（スコープ外）

- TUI の 3 ペインレイアウト、キーバインドの割り当て、検索仕様
- `HexPane.GenerateHexDump` のテキスト形式（既存テストが依存）
- `NodeDetailFormatter` / `DecodedNodeTreeBuilder` / `TuiState` のロジック
- Terminal.Gui 組み込みの `HexView` への置き換え（将来の課題）

## 受入条件

1. [x] `dotnet build` が警告 0・エラー 0 で成功すること（CS0618 を含む）
2. [x] `src/BinAnalyzer.Tui/BinAnalyzer.Tui.csproj` に `Microsoft.Build.Tasks.Git` の参照が存在せず、`Terminal.Gui` が `2.5.*` で解決されること（`project.assets.json` で 2.5.0 に解決）
3. [x] `dotnet test` が全通過すること（`BinAnalyzer.Tui.Tests` 46 件を含む全 1336 件）
4. [x] `dotnet run --project src/BinAnalyzer.Cli -- testdata/real/test.png -f formats/png.bdef.yaml -o tui` で Tree / Detail / Hex の 3 ペインとステータスバーが描画されること
5. [x] ツリー選択の変更に追随して Hex ペインのタイトルとハイライト（`^^`）が更新されること
6. [x] `/` で検索バーが表示され、`width` + Enter で `1/1` が表示されること。Esc で検索バーが閉じてステータスバーが戻ること
7. [x] `e` / `c` で全展開 / 全折り畳みされ、`q` で終了コード 0 で終了すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Tui | csproj のパッケージ参照、TuiApp / TreePane / DetailPane / HexPane / SearchBar / DecodedNodeTreeBuilder の API 追随 |
| BinAnalyzer.Tui.Tests | 変更なし（既存テストがそのまま通過すること） |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 不要
- [ ] docs/architecture.md — 不要（Terminal.Gui 依存という記述は変わらない）
- [ ] CLAUDE.md — 不要
- [ ] README.md — 不要

---

## 設計メモ

### 設計方針

**アプリケーションモデル**: Terminal.Gui 2.5 の推奨パターン
`using IApplication app = Application.Create().Init(); app.Run(runnable);` を採用する。
`IApplication.Dispose()` が端末状態の復元を担うため、旧 `try/finally { Application.Shutdown(); }` は不要になる。
ルートビューは枠なしの `Runnable`（`Toplevel` の後継）を使い、`using` で呼び出し側が破棄する
（`IApplication.Run(IRunnable)` に渡した Runnable は呼び出し側が破棄する規約）。

**TextView の代替**: Detail / Hex ペインは `ListView`（1 行 = 1 アイテム、`ObservableCollection<string>`）で描画する。
非推奨でなく、読み取り専用で、スクロール（キーボード / マウスホイール）が利用でき、追加の NuGet 依存が不要。
`HexPane.GenerateHexDump` は文字列を返す既存シグネチャを維持し、表示時に行分割する（既存テストを変更しない）。

**配色**: `ColorScheme` の後継である `Scheme` を `new Scheme(new Attribute(ColorName16.White, ColorName16.Blue))`
で生成し `Label.SetScheme()` で適用する。`Attribute` は `System.Attribute` と衝突するため完全修飾する。

**グローバルキーバインド**: ルートビューの `KeyDown` ではなく `IApplication.Keyboard.KeyDown` に登録する。
2.5 では `TreeView` / `ListView` が文字キーをレターナビゲーションとして消費し Handled にするため、
フォーカス中のビューを経由した後に呼ばれるルートビューの `KeyDown` には `/`, `q`, `n`, `e`, `c` が届かない。
`IKeyboard.KeyDown` はビューへのディスパッチ前に発火するため、ここで処理する。
検索バーにフォーカスがある間は Esc / Enter のみ横取りし、文字入力は `TextField` に流す。

**検索バーの Enter**: `TextField` に Enter が届かなくなるため、`Accepting` イベントではなく
`SearchBar.Submit()` で「クエリが変わっていれば検索、同じなら次の一致へ」を完結させる。

### モデル変更

なし。

### インタフェース変更

- `SearchBar.Submit()` を追加（内部クラス）。`PerformSearch()` は維持。
- `TuiApp.Run(...)` の public シグネチャは不変。

### 代替案

- **`Terminal.Gui.Editor`（EditorView）パッケージの追加** — `TextView` の公式後継だが、
  読み取り専用のテキスト表示に対しては過剰であり、`Terminal.Gui 2.4.17` 依存の別パッケージを増やすことになる。不採用。
- **Terminal.Gui 組み込み `HexView` への置き換え** — ハイライト表示や既存の `GenerateHexDump` テストとの互換性を失うため、
  本要望（移行）のスコープ外とする。
- **`TreeView.AllowLetterBasedNavigation = false` でルートビュー `KeyDown` を維持** — `ListView`（Hex / Detail）に
  フォーカスが移った場合に同じ問題が再発するため不採用。

### 懸念事項

- `2.5.*` の浮動参照のため、2.5 系のパッチで新たな obsolete が入ると `TreatWarningsAsErrors` によりビルドが壊れる可能性がある。
  `2.*` ではなく `2.5.*` としてマイナーバージョン跨ぎのリスクを避けた。
- `ListView` は選択行をハイライト表示するため、旧 `TextView` と見た目が僅かに異なる。

---

## 実装メモ

### 実装中の設計変更

- **`Pos.AnchorEnd(0)` の修正**: 旧コードはステータスラベルを `Y = Pos.AnchorEnd(0)` に配置していたが、
  v2 の `AnchorEnd(int offset)` は `superview.Height - offset` を返すため 0 では画面外になる。
  `Pos.AnchorEnd()` に変更し、検索バーも同じ最下行に配置して、表示中はステータスラベルを非表示にする
  （`SearchBar.VisibleChanged` で切り替え）。
- **`SearchBar.CanFocus = true`**: 2.x では素の `View` は既定でフォーカス不可のため、
  `TextField.SetFocus()` が失敗し Esc / Enter がツリー / アプリ既定のバインド（Esc = Quit）に流れていた。明示的に有効化した。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| （追加なし） | 既存の `BinAnalyzer.Tui.Tests` 46 件が通過 | 3 |
| （手動） | 疑似端末（pty）上で `-o tui` を起動しキー操作 | 4, 5, 6, 7 |

### 気づき・今後の課題

- Hex ペインは Terminal.Gui 組み込みの `HexView` に置き換えると、選択範囲の色付きハイライトや
  アドレスジャンプが標準機能で得られる（REQ 候補）。
- TUI の自動テストは pty 経由の手動確認に留まっている。Terminal.Gui 2.5 の `IApplication.GetInputInjector()` /
  `Terminal.Gui.Testing` を使えば、キー操作を含む結合テストを `dotnet test` に組み込める可能性がある。
