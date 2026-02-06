# REQ-015: CI/CD パイプライン

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在215件のテストが存在するが、全て手動実行に依存している。機能追加のたびにリグレッションリスクが高まるが、自動的な品質ゲートがない。PRマージ前にテスト通過を保証する仕組みが必要。

CI/CDパイプラインは早期に導入するほど恩恵が大きく、今後の全機能開発の基盤となる。

## 機能要件

### 追加する機能

- [ ] GitHub Actions ワークフローの作成
  - push / pull_request トリガー
  - `dotnet build` によるビルド検証
  - `dotnet test` による全テスト実行
  - テスト結果のサマリー表示
- [ ] マトリックスビルド
  - OS: ubuntu-latest（最低限）
  - .NET 10 SDK
- [ ] ビルドバッジの追加（README.md）

### 変更する既存機能

- [ ] README.md: ビルドステータスバッジの追加

### 変更しないもの（スコープ外）

- CD（自動デプロイ/NuGetパッケージ公開）
- コードカバレッジの計測・レポート（将来課題）
- マルチOS対応（Windows, macOS）— 初期はLinuxのみ

## 受入条件

1. [ ] mainブランチへのpushでワークフローが自動実行されること
2. [ ] PRに対してワークフローが自動実行されること
3. [ ] テスト失敗時にワークフローが失敗ステータスになること
4. [ ] テスト全通過時にワークフローが成功ステータスになること
5. [ ] README.mdにビルドステータスバッジが表示されること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — ビルドバッジ追加

### 追加するファイル

- `.github/workflows/ci.yml`

---

## 設計メモ

### 設計方針

GitHub Actionsで最小限のCIパイプラインを構築する。ワークフローファイル1つのみ。

### ワークフロー定義

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore BinAnalyzer.slnx
      - run: dotnet build BinAnalyzer.slnx --no-restore --configuration Release
      - run: dotnet test BinAnalyzer.slnx --no-build --configuration Release --verbosity normal
```

### モデル変更

なし。

### インタフェース変更

なし。

### 代替案

**案A: Azure DevOps Pipelines（不採用）**
- 理由: リポジトリがGitHub上にあるため、GitHub Actionsの方が統合が自然。

**案B: マルチOS対応（保留）**
- ubuntu + windows + macOS のマトリックスビルドも可能だが、初期はubuntuのみで十分。ランナーコストも考慮。

### 懸念事項

1. **.NET 10 SDKの可用性**: .NET 10はプレビュー版の可能性がある。`actions/setup-dotnet@v4` が `10.0.x` に対応しているか確認が必要。対応していない場合は `global.json` で正確なバージョンを指定し、`include-prerelease: true` を使用する。

2. **ビルドバッジ**: `README.md` に `![CI](https://github.com/zredjet/BinAnalyzer/actions/workflows/ci.yml/badge.svg)` を追加。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
