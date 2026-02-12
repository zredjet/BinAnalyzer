# REQ-102: GitHub Actions リリースワークフロー

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| フェーズ | インフラ |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-12 |

## 背景・動機

BinAnalyzer は開発が進み多数のフォーマット定義と機能が揃ったが、エンドユーザーが利用するための配布手段がない。現状ではソースからビルドする必要があり、.NET SDK のインストールが前提となっている。

GitHub Actions によるリリースワークフローを整備し、.NET ランタイム同梱のシングル実行ファイルを GitHub Release で配布できるようにする。

## 機能要件

### 追加する機能

#### ワークフロー

- [ ] GitHub Actions リリースワークフロー（`.github/workflows/release.yml`）を新規作成
- [ ] トリガーは `workflow_dispatch`（手動実行のみ）
  - 入力パラメータでバージョン番号を指定（例: `1.0.0`）
- [ ] CI テスト（`dotnet test`）を実行し、全テスト通過をリリースの前提条件とする

#### ビルド成果物

- [ ] Windows x64 向けのシングル実行ファイル（`.exe`）をビルド
  - `dotnet publish` で self-contained / single-file を指定
  - .NET ランタイムが未インストールの環境でも動作すること
- [ ] Mac / Linux 向けビルドはワークフロー内にコメントアウトで定義し、将来の有効化に備える
  - コメントアウト対象: `osx-x64`, `osx-arm64`, `linux-x64`

#### 配布パッケージの構成

- [ ] リリース用 zip ファイルの構成:
  ```
  BinAnalyzer-{version}-win-x64/
  ├── binanalyzer.exe          # シングル実行ファイル
  ├── formats/                 # 全フォーマット定義（*.bdef.yaml）
  ├── schemas/                 # JSON Schema（bdef.schema.json）
  ├── README.md
  ├── LICENSE
  └── THIRD-PARTY-LICENSES.txt # NuGet 依存ライブラリのライセンス（自動生成）
  ```

#### サードパーティライセンス自動生成

- [ ] NuGet 依存パッケージのライセンス情報を収集し `THIRD-PARTY-LICENSES.txt` を生成するスクリプトを用意
  - パッケージ名、バージョン、ライセンス種別、著作権表示を含む
  - リリースワークフロー内で自動実行される

#### GitHub Release

- [ ] GitHub Release を自動作成し、zip ファイルをアセットとしてアップロード
  - Release タイトル: `v{version}`
  - Release タグ: `v{version}`（ワークフロー内で作成）

### 変更しないもの（スコープ外）

- Homebrew / Chocolatey 等のパッケージマネージャーへの公開
- インストーラー（MSI / pkg）の作成
- 自動アップデート機能
- Docker イメージの作成
- NuGet パッケージとしての公開

## 受入条件

1. [ ] `workflow_dispatch` でバージョン番号を指定してリリースワークフローを実行できること
2. [ ] テスト全通過がリリースの前提条件であること（テスト失敗時はリリースが中断されること）
3. [ ] Windows x64 向けの self-contained single-file exe が生成されること
4. [ ] 生成された exe が .NET 未インストール環境で動作すること
5. [ ] zip に `formats/`, `schemas/`, `README.md`, `LICENSE`, `THIRD-PARTY-LICENSES.txt` が含まれること
6. [ ] `THIRD-PARTY-LICENSES.txt` に NuGet 依存パッケージのライセンス情報が記載されていること
7. [ ] GitHub Release が作成され、zip ファイルがアセットとしてダウンロード可能なこと
8. [ ] Mac / Linux 向けビルドがコメントアウトとしてワークフローに含まれていること
9. [ ] 既存の CI ワークフロー（`ci.yml`）に影響がないこと

## 影響範囲

### 変更が必要なファイル

| ファイル | 変更内容 |
|---|---|
| `.github/workflows/release.yml` | **新規** リリースワークフロー |
| `scripts/generate-third-party-licenses.*` | **新規** サードパーティライセンス生成スクリプト |
| `.github/workflows/ci.yml` | 変更なし |

### 変更が必要なドキュメント

- [ ] README.md — ダウンロード方法・インストール手順の追記
- [ ] docs/cli-usage.md — exe 直接実行の使用例追記（`dotnet run` に加えて `binanalyzer` コマンド）

---

## 設計メモ

### ワークフロー構成

`test` → `build-and-release` の2ジョブ構成。

- **test ジョブ**: `dotnet restore` → `dotnet build` → `dotnet test`。ci.yml と同一手順で全テスト通過をゲートとする。
- **build-and-release ジョブ**: `needs: test` で依存。テスト失敗時はビルド・リリースに進まない。
- ランナーは両ジョブとも `ubuntu-latest`。win-x64 は cross-compile で対応（Windows ランナー不要）。
- `permissions: contents: write` を build-and-release ジョブに付与し、タグ作成・Release 作成を許可。

### ビルド方式

```
dotnet publish --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

- `PublishSingleFile`: 全 .NET アセンブリを単一 exe に統合。
- `IncludeNativeLibrariesForSelfExtract`: ネイティブライブラリも exe 内に埋め込み、実行時に一時展開。
- `--self-contained true`: .NET ランタイム同梱。SDK/ランタイム未インストール環境で動作可能。
- バージョンは `-p:Version=${{ inputs.version }}` でワークフロー入力から注入。

### Mac / Linux 対応の準備

osx-x64, osx-arm64, linux-x64 の publish・パッケージングステップをコメントアウトで記述済み。コメント解除のみで有効化可能。Mac/Linux ではバイナリ名が `BinAnalyzer.Cli`（拡張子なし）→ `binanalyzer` にリネーム。

### サードパーティライセンス生成

外部ツール（`dotnet-project-licenses` 等）に依存しない軽量アプローチを採用。

処理フロー:
1. `dotnet list package --include-transitive --format json` でパッケージ一覧を JSON 取得
2. Python3（ubuntu-latest に標準搭載）で JSON パースし、パッケージ ID + resolvedVersion を抽出
3. NuGet Flat Container API (`https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.nuspec`) から nuspec を取得
4. `sed` で `<license>`, `<licenseUrl>`, `<copyright>`, `<projectUrl>` を抽出
5. `THIRD-PARTY-LICENSES.txt` に書き出し

### パッケージング

zip 内にバージョン名付きディレクトリを配置:

```
BinAnalyzer-{version}-win-x64.zip
└── BinAnalyzer-{version}-win-x64/
    ├── binanalyzer.exe
    ├── formats/
    ├── schemas/
    ├── README.md
    ├── LICENSE
    └── THIRD-PARTY-LICENSES.txt
```

### GitHub Release

`softprops/action-gh-release@v2` を使用。`tag_name: v{version}` でタグを自動作成し、zip をアセットとしてアップロード。`generate_release_notes: true` で前回リリースからのコミット差分を自動記載。

---

## 実装メモ

### 作成ファイル

| ファイル | 行数 | 内容 |
|---|---|---|
| `.github/workflows/release.yml` | 155行 | リリースワークフロー（test + build-and-release） |
| `scripts/generate-third-party-licenses.sh` | 101行 | サードパーティライセンス生成スクリプト |

### 実装中の設計変更

- **`files` ブロック内のコメント除去**: 当初 `softprops/action-gh-release` の `files:` に YAML `|` ブロックスカラー内で `#` コメントを記述したが、ブロックスカラー内ではコメントではなくリテラル文字列として扱われるため、コメントアウト行を除去し、`files:` の直前に通常の YAML コメントとして記載する形に変更した。

### テスト

| 検証項目 | 方法 | 結果 |
|---|---|---|
| ライセンス生成スクリプト動作 | `bash scripts/generate-third-party-licenses.sh` をローカル実行 | `THIRD-PARTY-LICENSES.txt` が正常生成。System.CommandLine (MIT) と YamlDotNet (MIT) の2パッケージが正しく抽出された |
| YAML 文法 | `python3 -c "import yaml; yaml.safe_load(...)"` で検証 | パース成功 |
| ワークフロー全体の動作 | GitHub 上で `workflow_dispatch` 実行（プッシュ後に確認要） | 未実施 |
| self-contained exe の動作 | .NET 未インストール Windows 環境で実行（リリース後に確認要） | 未実施 |

### 気づき・今後の課題

- **ライセンス生成スクリプトの python3 依存**: JSON パースに python3 を使用している。ubuntu-latest には標準搭載だが、ローカル開発環境によっては未インストールの可能性がある。`jq` が使える環境なら `jq` に置き換え可能だが、ubuntu-latest にも `jq` はプリインストールされており、どちらでも動作する。
- **publish 出力バイナリ名**: `dotnet publish` は `BinAnalyzer.Cli.exe`（プロジェクト名由来）を出力する。パッケージングステップで `binanalyzer.exe` にリネームしている。将来 `<AssemblyName>binanalyzer</AssemblyName>` を csproj に追加すれば publish 時点で `binanalyzer.exe` が出力される。
- **マルチプラットフォーム有効化時の注意**: osx の PublishSingleFile は .NET 8+ で正式サポートされているが、Apple Silicon (osx-arm64) の cross-compile は ubuntu 上で可能。ただし codesign されないため、macOS Gatekeeper の警告が出る。将来的には `macos-latest` ランナーでのネイティブビルド + codesign を検討すべき。
- **NuGet API のレート制限**: nuspec 取得に NuGet Flat Container API を使用しているが、パッケージ数が少ない（現状2個）ため問題ない。依存が大幅に増えた場合はレート制限に注意。
- **`generate_release_notes: true`**: softprops/action-gh-release が前回タグからのコミット差分を自動生成する。初回リリース時は全コミットがリストされる可能性がある。必要に応じて手動でリリースノートを編集する。
