# REQ-023: ベンチマーク

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のデコードエンジンのパフォーマンス特性が不明。将来的に大きなファイル（数MB〜数百MB）の解析を想定する場合、ベースラインの計測が必要。

BenchmarkDotNetを使用した再現可能なベンチマークを整備し、パフォーマンスリグレッションの検出やボトルネックの特定に活用する。

## 機能要件

### 追加する機能

- [ ] ベンチマークプロジェクトの作成（`benchmarks/BinAnalyzer.Benchmarks/`）
  - BenchmarkDotNet による計測
- [ ] デコードベンチマーク
  - 小さなファイル（〜100B: 最小PNG）のデコードスループット
  - 中規模ファイル（〜10KB: 複数チャンクPNG）のデコードスループット
  - 繰り返しフィールドの多いファイルのデコード
- [ ] 式評価ベンチマーク
  - 式パース速度
  - 式評価速度
- [ ] 出力フォーマットベンチマーク
  - Tree出力の生成速度
  - JSON出力の生成速度
  - Hexdump出力の生成速度

### 変更する既存機能

なし（新規プロジェクトの追加のみ）

### 変更しないもの（スコープ外）

- メモリ使用量のプロファイリング（将来課題）
- CI/CDでの自動ベンチマーク実行（将来課題）
- パフォーマンス改善の実施（計測のみ）

## 受入条件

1. [ ] `dotnet run --project benchmarks/BinAnalyzer.Benchmarks/ -c Release` でベンチマークが実行できること
2. [ ] デコードベンチマークの結果が表示されること（操作/秒、平均時間）
3. [ ] 式評価ベンチマークの結果が表示されること
4. [ ] 出力フォーマットベンチマークの結果が表示されること
5. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 追加するプロジェクト

- `benchmarks/BinAnalyzer.Benchmarks/` — BenchmarkDotNet ベンチマーク

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

BenchmarkDotNet を使用した独立プロジェクトとして作成。ソリューションファイルには追加するが、テストプロジェクトとは分離する。テスト用データ生成にはIntegration.Testsの `TestDataGenerator` を参考にインラインで生成する。

### プロジェクト構成

```
benchmarks/
└── BinAnalyzer.Benchmarks/
    ├── BinAnalyzer.Benchmarks.csproj
    ├── Program.cs
    ├── DecodeBenchmarks.cs
    ├── ExpressionBenchmarks.cs
    └── OutputBenchmarks.cs
```

### csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.14.*" />
    <ProjectReference Include="../../src/BinAnalyzer.Core/BinAnalyzer.Core.csproj" />
    <ProjectReference Include="../../src/BinAnalyzer.Dsl/BinAnalyzer.Dsl.csproj" />
    <ProjectReference Include="../../src/BinAnalyzer.Engine/BinAnalyzer.Engine.csproj" />
    <ProjectReference Include="../../src/BinAnalyzer.Output/BinAnalyzer.Output.csproj" />
  </ItemGroup>
</Project>
```

### ベンチマーク項目

**DecodeBenchmarks:**
- `DecodeMiniPng()` — 最小PNG（67バイト）のフルデコード
- `DecodeLargePng()` — 多チャンクPNG（10KB相当、繰り返しフィールド多数）
- `DecodeMinimalWav()` — 最小WAV（48バイト）

**ExpressionBenchmarks:**
- `ParseSimpleExpression()` — `{length}` のパース
- `ParseComplexExpression()` — `{length - 4 + offset * 2}` のパース
- `EvaluateExpression()` — パース済み式の評価

**OutputBenchmarks:**
- `FormatTree()` — デコード済みPNGのツリー出力
- `FormatJson()` — デコード済みPNGのJSON出力
- `FormatHexdump()` — デコード済みPNGのhexdump出力

### モデル変更

なし。

### インタフェース変更

なし。

### 代替案

なし。

### 懸念事項

1. **BenchmarkDotNetのバージョン**: .NET 10対応バージョンの確認が必要。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
