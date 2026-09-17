# REQ-176: GUI のマルチプラットフォーム検証とリリース配布

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | in-progress |
| 優先度 | 中 |
| 依存 | REQ-168 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI モード（REQ-168）は macOS でのみ手動確認した。Windows（WebView2、STA スレッド）と Linux（`libwebkit2gtk-4.1`）は未検証で、`release.yml` は win-x64 の CLI しか publish していない。GUI を含むバイナリを各 OS 向けに配布し、起動確認を CI に組み込みたい。

## 機能要件

### 追加する機能

- [x] Windows で `-o gui` の起動・描画・終了コード 0 を確認（STA スレッド経路）。WebView2 ランタイム不在時のエラーメッセージを分かりやすくする（`DesktopRuntimeCheck`: レジストリの Evergreen クライアント ID で検出）
- [x] Linux（ubuntu）で `libwebkit2gtk-4.1` 導入後の起動を確認。不在時は依存パッケージ名を案内して終了コード 1（`NativeLibrary.TryLoad` で 4 ライブラリを確認、DISPLAY 未設定も検出）
- [x] `release.yml` に osx-x64 / osx-arm64 / linux-x64 の publish を追加。self-contained 単一ファイル。`formats/` を同梱（各 RID をネイティブ OS のランナーで publish するマトリクスに再構成）
- [x] CI に「GUI 起動スモーク」ジョブを追加: `xvfb-run` + `BINANALYZER_GUI_AUTOCLOSE`（一定時間後に自動終了する隠しオプション）で終了コード 0 を確認（ubuntu / windows / macos の 3 OS）
- [x] publish サイズと初回起動時間（`Photino.Native` 展開）を `docs/benchmark-baseline.md` に記録

### 変更する既存機能

- [x] `GuiApp.Run` に自動終了オプション（環境変数 `BINANALYZER_GUI_AUTOCLOSE`、`GuiLaunchOptions.AutoCloseAfter`）を追加（テスト専用）
- [x] Photino の既定ログ（Set* / SendWebMessage の全記録）を `BINANALYZER_GUI_DEBUG=1` 指定時のみ出す

### 変更しないもの（スコープ外）

- コード署名・公証（macOS notarization、Windows Authenticode）
- インストーラ作成

## 受入条件

1. [ ] Windows / macOS / Linux で `binanalyzer file -f fmt -o gui` が起動し、閉じると終了コード 0
2. [ ] WebView2 / WebKitGTK が無い環境で分かりやすいエラーと終了コード 1
3. [ ] release ワークフローが 4 RID の成果物を生成し、各成果物の `-o tree` と `-o gui`（`BINANALYZER_GUI_AUTOCLOSE`）が動くこと
4. [ ] CI の GUI スモークジョブが ubuntu-latest で通ること
5. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Gui.Desktop | 依存ランタイム検出、自動終了オプション |
| BinAnalyzer.Cli | エラーメッセージ |
| .github/workflows | release.yml / ci.yml |

### 変更が必要なドキュメント

- [ ] docs/cli-usage.md — `### gui` の動作環境
- [ ] README.md — 配布物の説明
- [ ] docs/benchmark-baseline.md

---

## 設計メモ

### 設計方針

- **Mac 環境で実装し、実機確認は GitHub Actions のランナーに任せる。** Photino.Native 4.0.22 は win-x64 / linux-x64 / osx-x64 / osx-arm64 のネイティブを NuGet に同梱しているので、どの OS からでも全 RID を publish できる。Windows / Linux 固有の挙動は CI のスモークで見る。
- **ランタイム検出は Photino より前に、OS 差分を `IRuntimeProbe` に隔離して行う。** Photino.Native は依存ライブラリが無いと dlopen / LoadLibrary で分かりにくい例外を投げる。判定ロジックは純関数にして、どの OS 上でも全経路（Windows 有／無、Linux 有／欠落／ディスプレイ無）をテストする。
- **自動終了は「窓が生成された後」にタイマーを起動し、`Invoke` 越しに `Close` を呼ぶ。** `Close` は UI スレッド限定。`Task.Delay` から直接呼ぶと落ちる。
- **release は各 RID をネイティブ OS のランナーで publish する。** ubuntu から osx をクロス publish すると apphost の署名が付かず、Apple Silicon で起動を拒否されることがある。macOS ランナーなら SDK が ad-hoc 署名する。GUI を含むので publish 直後に同じランナーで起動スモークも行い、受入条件 3 を成果物そのもので満たす。`dry_run` 入力で Release 作成だけを省略できる。

### インタフェース変更

- `GuiLaunchOptions` を `record` にし `AutoCloseAfter: TimeSpan?` を追加。
- `GuiAutoClose.Parse(string?)` / `FromEnvironment()`、`DesktopRuntimeCheck.Check()` → `RuntimeCheckResult(IsOk, Message)`。
- CLI: `-o gui` はランタイム不足時に `エラー: ...` を標準エラーに出して終了コード 1。

### 代替案

- WebView2 検出に `WebView2Loader.dll` の `GetAvailableCoreWebView2BrowserVersionString` を P/Invoke する案。固定バージョン配布も拾えるが、単一ファイル展開ディレクトリからのロード経路が増えるため、Microsoft が文書化しているレジストリ検出（Evergreen クライアント ID の `pv`）を採った。
- ci.yml のスモークを main への push のみに限定する案。公開リポジトリで Actions は無料なので、PR でも回して Windows / Linux 固有の退行を早く見つける方を優先した。`build-and-test` 成功後に走らせて、テスト失敗時に 3 OS 分のランナーを無駄にしないようにしている。

### 懸念事項

- Photino.Native の Linux ビルドが依存する WebKitGTK のバージョン（4.0 / 4.1）はディストリビューションで異なる。4.0.22 の `Photino.Native.so` は `libwebkit2gtk-4.1.so.0` / `libjavascriptcoregtk-4.1.so.0` / `libgtk-3.so.0` / `libnotify.so.4` にリンクしている（`strings` で確認）。4.0 系しか無いディストリでは動かない。
- Windows の「WebView2 不在」は WebView2 を消したランナーが用意できないため、実機確認は検出ロジックの単体テストまで。
- osx-x64 の GUI スモークは Apple Silicon ランナー上の Rosetta 2 で動かしている。

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Gui.Tests / DesktopRuntimeCheckTests | `Windows_WithoutWebView2_ExplainsHowToInstall`, `Linux_MissingWebKit_ListsMissingLibrariesAndAptPackage`, `Linux_WithoutDisplay_ExplainsXvfb`, `Linux_MissingLibraryIsReportedBeforeDisplay`, `MacOS_IsAlwaysOk`, `SystemProbe_DoesNotThrowOnCurrentOs` | 2 |
| Gui.Tests / GuiAutoCloseTests | `Disabled_ReturnsNull`, `Enabled_UsesDefaultDelay`, `Integer_IsMilliseconds` | 1, 4（自動終了オプションの解釈） |
| CI / gui-smoke（linux-x64, win-x64, osx-arm64） | `-o tree`、WebKitGTK 不在で終了コード 1 + 案内、導入後 `-o gui` 自動終了で終了コード 0 | 1, 2, 4 |
| Release / build（4 RID） | 各成果物で `-o tree` と `-o gui` 自動終了 | 3 |

手動スモーク（2026-09-18, macOS 15 / .NET 10.0.302、self-contained osx-arm64 publish）:
- `BINANALYZER_GUI_AUTOCLOSE=3000 ./BinAnalyzer.Cli test.png -f png.bdef.yaml -o gui` → 窓が開いて 3 秒後に閉じ、終了コード 0（REQ-168 で未確認だった「閉じたら 0」も解消）。
- `-o tree` は初回 1.12 s（ネイティブ展開込み）、2 回目 0.08 s。

### 気づき・今後の課題
