# REQ-176: GUI のマルチプラットフォーム検証とリリース配布

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | REQ-168 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI モード（REQ-168）は macOS でのみ手動確認した。Windows（WebView2、STA スレッド）と Linux（`libwebkit2gtk-4.1`）は未検証で、`release.yml` は win-x64 の CLI しか publish していない。GUI を含むバイナリを各 OS 向けに配布し、起動確認を CI に組み込みたい。

## 機能要件

### 追加する機能

- [ ] Windows で `-o gui` の起動・描画・終了コード 0 を確認（STA スレッド経路）。WebView2 ランタイム不在時のエラーメッセージを分かりやすくする
- [ ] Linux（ubuntu）で `libwebkit2gtk-4.1` 導入後の起動を確認。不在時は依存パッケージ名を案内して終了コード 1
- [ ] `release.yml` に osx-x64 / osx-arm64 / linux-x64 の publish を追加（コメントアウト済みブロックを有効化）。self-contained 単一ファイル。`formats/` を同梱
- [ ] CI に「GUI 起動スモーク」ジョブを追加: `xvfb-run` + `BINANALYZER_GUI_AUTOCLOSE=1`（一定時間後に自動終了する隠しオプション）で終了コード 0 を確認
- [ ] publish サイズと初回起動時間（`Photino.Native` 展開）を `docs/benchmark-baseline.md` に記録

### 変更する既存機能

- [ ] `GuiApp.Run` に自動終了オプション（環境変数）を追加（テスト専用）

### 変更しないもの（スコープ外）

- コード署名・公証（macOS notarization、Windows Authenticode）
- インストーラ作成

## 受入条件

1. [ ] Windows / macOS / Linux で `binanalyzer file -f fmt -o gui` が起動し、閉じると終了コード 0
2. [ ] WebView2 / WebKitGTK が無い環境で分かりやすいエラーと終了コード 1
3. [ ] release ワークフローが 4 RID の成果物を生成し、各成果物の `-o tree` と `-o gui --autoclose` が動くこと
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
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

- Photino.Native の Linux ビルドが依存する WebKitGTK のバージョン（4.0 / 4.1）はディストリビューションで異なる。

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
