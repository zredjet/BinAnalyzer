# REQ-172: DSL パーサーでの行番号保持（定義ビューの厳密なハイライト）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | REQ-168 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI の定義ビュー（REQ-168）は `YamlFieldLocator` によるテキスト探索で選択フィールドの定義行を推定している。同名フィールドが 1 つの struct ブロック内に複数ある場合（インライン struct・switch の分岐内）や、インポートされた struct では正しい行を指せない。

YamlDotNet の `Mark` を使えば、IR の `StructDefinition` / `FieldDefinition` に定義元ファイルと行番号を持たせられる。

## 機能要件

### 追加する機能

- [ ] `YamlStructModel` / `YamlFieldModel` にデシリアライズ時の開始行を記録する（`INodeDeserializer` ラッパー）
- [ ] IR（`StructDefinition` / `FieldDefinition`）に `SourceFile` / `SourceLine` を追加。`YamlToIrMapper` で引き継ぐ。インポート先は元ファイルのパスを保持
- [ ] `FormatValidator` のエラー / 警告メッセージに行番号を含める
- [ ] GUI 定義ビューは `SourceLine` を優先し、無い場合のみ `YamlFieldLocator` にフォールバック。別ファイル定義の場合はそのファイルを表示（`IFormatCatalog` 経由で取得）

### 変更しないもの（スコープ外）

- `validate` サブコマンドの出力形式変更（行番号追加のみ）

## 受入条件

1. [ ] `formats/png.bdef.yaml` の `ihdr.width` の `SourceLine` が実際の行と一致すること
2. [ ] 同名フィールドが複数ある定義で、それぞれの `SourceLine` が異なること
3. [ ] インポートされた struct の `SourceFile` がインポート元ファイルを指すこと
4. [ ] `FormatValidator` のメッセージに `file:line` が含まれること
5. [ ] GUI 定義ビューが `SourceLine` の行を強調すること（bUnit）
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | IR に `SourceFile` / `SourceLine` |
| BinAnalyzer.Dsl | 行番号記録デシリアライザ、マッパー |
| BinAnalyzer.Gui | `DefinitionView` |

### 変更が必要なドキュメント

- [ ] docs/parser-design.md

---

## 設計メモ

### 設計方針
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

- REQ-168 の設計時見積りは約 1 日。`YamlFieldLocator` は残し、フォールバックとして使う。

---

## 実装メモ

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
