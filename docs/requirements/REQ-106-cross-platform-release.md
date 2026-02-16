# REQ-106: macOS/Linux リリースビルド

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 取り下げ |
| 優先度 | 高 |
| 依存 | REQ-102（リリースワークフロー） |
| フェーズ | E（運用） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-12 |

## 背景・動機

REQ-102 で GitHub Actions リリースワークフローを構築したが、win-x64 のみビルドしている。macOS (x64/arm64) と Linux (x64) のビルドはコメントアウト状態。クロスプラットフォーム配布を実現するために有効化する。

### ユースケース

- macOS (Apple Silicon / Intel) ユーザーがシングルバイナリをダウンロードして使用
- Linux ユーザーが CI/CD パイプラインや開発環境で使用
- GitHub Release ページで全プラットフォーム向けアセットが一覧で表示される

## 機能要件

### 追加する機能

- `osx-x64`, `osx-arm64`, `linux-x64` の self-contained single-file publish を有効化
- 各プラットフォーム向けに zip/tar.gz パッケージを作成
- GitHub Release に全プラットフォームのアセットをアップロード
- macOS バイナリの署名（可能であれば ad-hoc 署名）

### 変更しないもの（スコープ外）

- linux-arm64 ビルド
- Windows ARM64 ビルド
- Homebrew / APT / Chocolatey パッケージマネージャ対応
- macOS の公証（Notarization）

## 受入条件

1. `osx-x64`, `osx-arm64`, `linux-x64` のビルドがリリースワークフローに含まれること
2. 各プラットフォーム向けアセットが GitHub Release にアップロードされること
3. macOS/Linux バイナリが実行可能であること
4. `formats/`, `schemas/`, `README.md`, `LICENSE`, `THIRD-PARTY-LICENSES.txt` が各パッケージに含まれること
5. 既存の win-x64 ビルドに影響がないこと
