# アーキテクチャ

## パイプライン

```
DSL(.bdef.yaml) → [DSLパーサー] → IR(中間表現) → [バイナリデコーダー] + バイナリデータ → デコード済みツリー → [出力フォーマッター] → ツリー表示
```

## プロジェクト構成

```
BinAnalyzer/
├── src/
│   ├── BinAnalyzer.Core/          # ドメインモデル、式、インタフェース
│   ├── BinAnalyzer.Dsl/           # YAML → IR変換（YamlDotNet）
│   ├── BinAnalyzer.Engine/        # バイナリデコーダーエンジン（BCLのみ）
│   ├── BinAnalyzer.Output/        # 出力フォーマッター（BCLのみ）
│   └── BinAnalyzer.Cli/           # CLIエントリポイント
├── tests/
│   ├── BinAnalyzer.Core.Tests/
│   ├── BinAnalyzer.Dsl.Tests/
│   ├── BinAnalyzer.Engine.Tests/
│   └── BinAnalyzer.Integration.Tests/
└── formats/
    └── png.bdef.yaml
```

## 依存関係

```
Cli → Dsl, Engine, Output
Dsl → Core（+ YamlDotNet）
Engine → Core
Output → Core
Core → （なし）
```

## 主要コンポーネント

### IR（中間表現） — Core/Models/

IRはバイナリフォーマットの正規表現。YAML構文から独立しています。

- **FormatDefinition** — 最上位: 名前、エンディアン、列挙型、フラグ、構造体、ルート構造体
- **StructDefinition** — 名前付きフィールドの集合
- **FieldDefinition** — 型、サイズ、enum/flags参照、繰り返しモード、switch、期待値
- **EnumDefinition / FlagsDefinition** — 値のマッピング

### 式システム — Core/Expressions/

DSL内の動的値を扱うミニ言語: `{length}`, `{length - 4}`, `{type == 'IHDR'}`

- **ExpressionNode** — 判別共用体AST: LiteralInt, LiteralString, FieldReference, BinaryOp, UnaryOp
- **ExpressionTokenizer** — 字句解析器
- **ExpressionParser** — 演算子優先順位対応の再帰下降パーサー

ASTの定義はCore（DSLとEngineの両方が必要とするため）。評価はEngineに配置。

### デコード結果ツリー — Core/Decoded/

デコード出力: 型付きノードのツリー。

- **DecodedStruct** — 名前付き構造体と子要素
- **DecodedArray** — 繰り返し要素
- **DecodedInteger** — オプションのenumラベル付き
- **DecodedBytes** — オプションのバリデーション付き
- **DecodedString** — オプションのフラグ付き
- **DecodedFlags** — ビットレベルのフラグ状態

### バイナリデコーダー — Engine/

- **DecodeContext** — ReadOnlyMemory\<byte\>のラッパー。位置追跡、スコープスタック、変数バインディング
- **ExpressionEvaluator** — DecodeContextの変数を使用してASTを評価
- **BinaryDecoder** — フィールドデコード、繰り返し処理、switch解決のオーケストレーター

### DSLパーサー — Dsl/

- **YamlModels/** — YamlDotNetデシリアライズ用DTO（IRとは別クラス）
- **YamlToIrMapper** — YAML DTOをIRに変換、式のパース、参照の検証
- **YamlFormatLoader** — IFormatLoader実装

## 設計上の重要ポイント

1. **YAMLモデルとIRは分離** — DSL構文の進化がエンジンに影響しない
2. **式のASTはCore、評価はEngine** — DSLも式を作成する必要があるため
3. **スコープスタックで変数バインディング** — switchのバウンダリスコープ、ネスト構造体の変数解決
4. **`size: remaining` は現在のスコープ境界を参照** — ファイル全体ではない
5. **文字列フィールドにフラグを付与可能** — PNGチャンクタイプはASCII文字列かつビットフラグ; DecodedStringにオプションのFlags
6. **バイナリ読み取りはBCLのみ** — BinaryPrimitives, ReadOnlyMemory\<byte\>, Span\<byte\>
