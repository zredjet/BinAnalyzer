# REQ-110: CI 実ファイルリグレッション

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-107（ゴールデンファイルテスト） |
| フェーズ | E（品質） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-17 |

## 背景・動機

REQ-107 でゴールデンファイルテストフレームワークを構築した後、それを CI パイプライン（GitHub Actions）に組み込んで自動実行する。PR ごとにデコード結果のリグレッションを検出し、意図しない変更を防止する。

### ユースケース

- PR 作成時に全フォーマットのゴールデンファイルテストが自動実行される
- デコード結果の変更があった場合、CI が失敗して変更箇所が明示される
- 意図的な変更の場合はゴールデンファイル更新コミットで CI を通過させる

## 機能要件

### 追加する機能

- `.github/workflows/ci.yml` にゴールデンファイルテスト実行ステップを追加
- CI 環境でのテスト実行時間を妥当な範囲に維持
- テスト用サンプルバイナリが CI 環境で `TestDataGenerator` により確実に生成され、全30フォーマットのゴールデンファイルテストが実行されること

### 変更しないもの（スコープ外）

- 大容量バイナリファイルのテスト（CI 実行時間の制約）
- パフォーマンスリグレッション検出
- 自動的なゴールデンファイル更新 PR 作成

## 受入条件

1. CI ワークフローでゴールデンファイルテストが自動実行されること
2. デコード結果の変更がある場合に CI が失敗すること
3. ゴールデンファイル更新後に CI が通過すること
4. CI 実行時間がゴールデンファイルテスト追加前から大幅に増加しないこと
5. テスト用サンプルバイナリが CI 環境で TestDataGenerator により確実に生成され、全30フォーマットのゴールデンファイルテストが実行されること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（インフラ要件のため） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Compression | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| BinAnalyzer.Integration.Tests | `GoldenFileTests.cs` / `RealFileValidationTests.cs` のサイレントスキップ修正（`return;` → `Assert.Fail`） |

### 変更が必要なドキュメント

- [ ] `.github/workflows/ci.yml` — テストステップを3ステップに分離
- [ ] `testdata/README.md` — 新規: CI テスト実行フロー・ゴールデンファイル更新手順
- [x] docs/architecture.md — 変更不要
- [x] docs/dsl-reference.md — 変更不要
- [x] docs/parser-design.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

サイレントスキップ修正 + CI テストステップ分離 + ゴールデンファイル更新ワークフロー文書化の3点。C# プロジェクトコード変更は最小限（テストコード2行修正のみ）で、主な変更は CI ワークフローとドキュメント。

### テストバイナリ戦略

`RealFileFixture` + `TestDataGenerator` による実行時生成を維持する。

- `testdata/real/` は `.gitignore` のまま（バイナリを git にコミットしない）
- `testdata/golden/` は30 JSON がコミット済み（リグレッション検出の基準）
- 根拠: 全30フォーマットが `TestDataGenerator` でカバー済み、クロスプラットフォーム動作確認済み

### サイレントスキップ修正

`RealFileFixture` が全テスト用バイナリを生成するため、ファイル不在は即バグである。`return;` によるサイレントスキップは問題を隠蔽するため、`Assert.Fail` に変更する。

#### GoldenFileTests.cs 66-70行目

```csharp
// 変更前:
if (!File.Exists(filePath))
{
    _output.WriteLine($"SKIPPED: Test file not found: {binaryFile}");
    return;
}

// 変更後:
if (!File.Exists(filePath))
{
    Assert.Fail($"Test file not found: {binaryFile} — RealFileFixture should have generated it");
}
```

#### RealFileValidationTests.cs 71-75行目

```csharp
// 変更前:
if (!File.Exists(filePath))
{
    _output.WriteLine($"SKIPPED: Test file not found: {fileName}");
    return;
}

// 変更後:
if (!File.Exists(filePath))
{
    Assert.Fail($"Test file not found: {fileName} — RealFileFixture should have generated it");
}
```

### CI ワークフロー構造（3ステップ分離）

現在の単一 `dotnet test` ステップを3ステップに分離し、GitHub Actions UI で各カテゴリの成否を一目で確認できるようにする。

```yaml
- name: Unit & Integration Tests
  run: >
    dotnet test BinAnalyzer.slnx
    --no-build --configuration Release --verbosity normal
    --filter "Category!=Golden&Category!=RealFile"

- name: Golden File Regression Tests
  run: >
    dotnet test BinAnalyzer.slnx
    --no-build --configuration Release --verbosity normal
    --filter "Category=Golden"

- name: Real File Validation Tests
  run: >
    dotnet test BinAnalyzer.slnx
    --no-build --configuration Release --verbosity normal
    --filter "Category=RealFile"
```

### ゴールデンファイル更新ワークフロー

デコード出力に影響する変更を行った場合の手順:

1. コード変更 → デコード出力に影響
2. `UPDATE_GOLDEN=1 dotnet test --filter "Category=Golden"` でローカル再生成
3. `git diff testdata/golden/` で差分確認
4. コード変更 + ゴールデンファイル更新を同じ PR でコミット
5. CI が更新後のゴールデンファイルで検証 → 通過

### 代替案

| 案 | 概要 | 採否 | 理由 |
|---|---|---|---|
| 案A | 単一 `dotnet test` ステップ維持 | 不採用 | ゴールデンテスト失敗の可視性が低い |
| **案B** | **テストステップ3分割** | **採用** | GitHub Actions UI で各カテゴリの成否が一目瞭然 |
| 案C | ゴールデンテスト用の独立ジョブ | 不採用 | ビルド重複で CI 時間増加、過剰 |
| 案D | テストバイナリをリポジトリにコミット | 不採用 | TestDataGenerator で十分。リポジトリ肥大化 |

---

## 変更・追加ファイル一覧

### 新規ファイル

| ファイル | 内容 |
|---|---|
| `testdata/README.md` | テストデータ構成・ゴールデンファイル更新手順 |

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `.github/workflows/ci.yml` | Test ステップを3ステップに分離（Unit & Integration / Golden / RealFile） |
| `tests/BinAnalyzer.Integration.Tests/GoldenFileTests.cs` | サイレントスキップ修正（`return;` → `Assert.Fail`） |
| `tests/BinAnalyzer.Integration.Tests/RealFileValidationTests.cs` | サイレントスキップ修正（`return;` → `Assert.Fail`） |

---

## テスト計画

インフラ変更のため新規テストクラス追加は不要。検証は CI 実行で確認する。

| 検証項目 | 検証方法 | 対応する受入条件 |
|---|---|---|
| CI でゴールデンテスト実行 | PR 作成 → "Golden File Regression Tests" ステップ成功確認 | AC-1 |
| デコード結果変更時 CI 失敗 | ゴールデンファイル改変 → CI 失敗確認 → 元に戻す | AC-2 |
| ゴールデン更新後 CI 通過 | `UPDATE_GOLDEN=1` → コミット → CI 成功 | AC-3 |
| CI 実行時間の確認 | 変更前後比較、大幅増加なし | AC-4 |
| テストバイナリ生成確認 | CI ログで30テストケース全実行を確認 | AC-5 |

---

## 受入条件の対応付け

| 受入条件 | 実装箇所 | 検証方法 |
|---|---|---|
| AC-1: CI でゴールデンテスト自動実行 | `ci.yml` の "Golden File Regression Tests" ステップ | PR 作成 → ステップ成功確認 |
| AC-2: デコード結果変更時に CI 失敗 | ゴールデンファイル比較ロジック（既存）+ サイレントスキップ修正 | ゴールデンファイル改変 → CI 失敗確認 |
| AC-3: ゴールデン更新後に CI 通過 | `UPDATE_GOLDEN=1` でのローカル再生成 → コミット | 更新後のゴールデンファイルで CI 成功 |
| AC-4: CI 実行時間が大幅増加しない | 3ステップ分離（テストホスト起動3回、増加は数秒程度） | 変更前後の CI 実行時間比較 |
| AC-5: テストバイナリが CI で生成・全テスト実行 | `RealFileFixture` + `TestDataGenerator` による実行時生成、サイレントスキップ修正で不在時即失敗 | CI ログで30テストケース全実行を確認 |

---

## 懸念事項

1. **TestDataGenerator の網羅性**: 新フォーマット追加時に TestDataGenerator への追加を忘れるとテスト即失敗する。これはサイレントスキップ修正後の意図した動作であり、追加漏れを検出できる利点がある。

2. **TestDataGenerator 変更 = ゴールデン再生成**: バイナリ生成コードの変更でゴールデン不一致が発生する場合は `UPDATE_GOLDEN=1` での再生成が必要。

3. **`--filter` 構文**: xunit の `Category!=Golden&Category!=RealFile` で AND 除外。`Category` Trait が付与されていないテストが Unit & Integration ステップで実行される。

4. **CI 実行時間**: 3ステップ分離でテストホスト起動が3回になる。増加は数秒程度と見込む。

5. **release.yml との整合性**: REQ-110 のスコープは `ci.yml` のみ。`release.yml` のテストステップは別途検討が必要であれば将来対応。

6. **ローカルの古いテストファイル**: `testdata/real/` を削除→再生成する手順を `testdata/README.md` に文書化する。
