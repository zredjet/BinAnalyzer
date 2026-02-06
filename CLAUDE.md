# BinAnalyzer

汎用バイナリ構造解析ツール。YAML-DSL（.bdef.yaml）でバイナリフォーマットを定義し、任意のバイナリファイルを構造的に解析・ツリー表示する。

## ビルド・テスト

```bash
dotnet build
dotnet test
dotnet run --project src/BinAnalyzer.Cli -- <バイナリファイル> -f <フォーマットファイル>
```

## アーキテクチャ

```
DSL(.bdef.yaml) → [DSLパーサー] → IR(中間表現) → [バイナリデコーダー] + バイナリデータ → デコード済みツリー → [出力フォーマッター] → ツリー表示
```

### プロジェクト構成

- **BinAnalyzer.Core** — ドメインモデル(IR)、式、デコード結果、インタフェース。外部依存ゼロ。
- **BinAnalyzer.Dsl** — YAML → IR変換。YamlDotNetに依存。
- **BinAnalyzer.Engine** — バイナリデコーダーエンジン。BCLのみ。
- **BinAnalyzer.Output** — 出力フォーマッター(ツリー等)。BCLのみ。
- **BinAnalyzer.Cli** — CLIエントリポイント。

### 設計上の重要ポイント

- YAMLモデルとIRモデルは別クラス — DSL構文の進化がエンジンに影響しない。
- 式のAST定義はCore、評価はEngine — DSLも式を作成する必要があるため。
- スコープスタックで変数バインディング — switchのバウンダリスコープ、ネスト構造体の変数解決。
- `size: remaining` は現在のバウンダリスコープ内の残りバイト数を参照 — ファイル全体ではない。
