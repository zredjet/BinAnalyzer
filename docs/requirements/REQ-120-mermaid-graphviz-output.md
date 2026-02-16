# REQ-120: Mermaid/Graphviz 出力

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 実装済み |
| 優先度 | 低 |
| 依存 | なし |
| フェーズ | E（出力） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-15 |

## 背景・動機

バイナリフォーマットの構造を視覚的に理解するために、ダイアグラム出力が有用。Mermaid（Markdown 埋め込み可能）や Graphviz（DOT 言語）形式で出力すれば、ドキュメントやプレゼンテーションに構造図を直接組み込める。

### ユースケース

- フォーマット定義の構造をダイアグラムとしてドキュメントに掲載
- struct 間の参照関係の可視化
- デコード結果の構造をグラフィカルに表示
- GitHub README や Issue にダイアグラムを埋め込み

## 機能要件

### 追加する機能

- [x] `schema` サブコマンドで Mermaid 形式出力（デフォルト）
- [x] `schema -o dot` で Graphviz DOT 形式出力
- [x] フォーマット定義（IR）からの構造ダイアグラム生成（struct 間の参照関係）
- [ ] デコード結果からのツリーダイアグラム生成（将来対応）
- [x] ノードのラベルにフィールド名・型を含める

### 変更する既存機能

なし

### 変更しないもの（スコープ外）

- 画像ファイル（PNG, SVG）への直接レンダリング（外部ツールに委譲）
- インタラクティブなダイアグラム（HTML 出力でカバー）
- レイアウトのカスタマイズオプション

## 受入条件

1. [x] `schema` サブコマンドで有効な Mermaid 構文が出力されること
2. [x] `schema -o dot` で有効な DOT 構文が出力されること
3. [x] struct 間の参照関係がエッジとして表現されること
4. [x] Mermaid Live Editor や Graphviz でレンダリング可能であること
5. [x] 既存の出力フォーマットに影響がないこと
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ISchemaFormatter` インタフェース追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | `MermaidSchemaFormatter`, `DotSchemaFormatter` 追加 |
| BinAnalyzer.Cli | `schema` サブコマンド追加 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md
- [ ] docs/architecture.md
- [ ] CLAUDE.md
- [ ] README.md

---

## 設計メモ

### 設計方針

IR 構造図（FormatDefinition からの struct 間参照関係グラフ）のみをスコープとする。バイナリファイルは不要。既存の `IOutputFormatter`（`DecodedStruct → string`）とは入力が異なるため、新規 `ISchemaFormatter`（`FormatDefinition → string`）インタフェースを導入。CLI には `schema` サブコマンドを追加。

### モデル変更

なし。FormatDefinition / StructDefinition / FieldDefinition をそのまま使用。

### インタフェース変更

`ISchemaFormatter` を `BinAnalyzer.Core.Interfaces` に追加:
```csharp
public interface ISchemaFormatter
{
    string Format(FormatDefinition format);
}
```

### 代替案

- 既存 `IOutputFormatter` を拡張する案 → 入力型が異なるため却下
- `--output mermaid` で既存コマンドに統合する案 → バイナリファイルが不要なため `schema` サブコマンドとして分離

### 懸念事項

- デコード結果からのツリーダイアグラム生成は今回のスコープ外。将来対応。

---

## 実装メモ

### 実装中の設計変更

当初の要件では `--output mermaid/dot` として既存コマンドに統合する想定だったが、IR 構造図はバイナリファイルを必要としないため、独立した `schema` サブコマンドとして実装した。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SchemaOutputTests | Mermaid_ContainsClassDiagramKeyword | 1 |
| SchemaOutputTests | Mermaid_RootStructHasRootStereotype | 1 |
| SchemaOutputTests | Mermaid_StructFieldGeneratesEdge | 3 |
| SchemaOutputTests | Mermaid_SwitchFieldGeneratesConditionalEdges | 3 |
| SchemaOutputTests | Mermaid_SwitchDefaultGeneratesDefaultLabel | 3 |
| SchemaOutputTests | Mermaid_RepeatFieldHasMultiplicity | 3 |
| SchemaOutputTests | Mermaid_AllFieldNamesDisplayedInClass | 1 |
| SchemaOutputTests | Mermaid_RepeatFieldShowsArraySuffix | 1 |
| SchemaOutputTests | Mermaid_EmptyStructGeneratesValidClass | 1 |
| SchemaOutputTests | Mermaid_CompressionFieldGeneratesEdgeWithLabel | 3 |
| SchemaOutputTests | Mermaid_OtfFormatIntegration | 1, 3, 4 |
| SchemaOutputTests | Dot_ContainsDigraphKeyword | 2 |
| SchemaOutputTests | Dot_RootStructHasBoldStyle | 2 |
| SchemaOutputTests | Dot_StructFieldGeneratesEdge | 3 |
| SchemaOutputTests | Dot_SwitchFieldGeneratesLabeledEdges | 3 |
| SchemaOutputTests | Dot_RepeatFieldHasMultiplicity | 3 |
| SchemaOutputTests | Dot_FieldTypesDisplayCorrectly | 2 |
| SchemaOutputTests | Dot_OtfFormatIntegration | 2, 3, 4 |

### 気づき・今後の課題

- デコード結果からのツリーダイアグラム生成（Mermaid flowchart / DOT）は将来の拡張として検討
- SVG 直接出力は Graphviz / Mermaid CLI の外部ツールに委譲
