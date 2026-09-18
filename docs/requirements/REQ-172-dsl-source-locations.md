# REQ-172: DSL パーサーでの行番号保持（定義ビューの厳密なハイライト）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-168 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

GUI の定義ビュー（REQ-168）は `YamlFieldLocator` によるテキスト探索で選択フィールドの定義行を推定している。同名フィールドが 1 つの struct ブロック内に複数ある場合（インライン struct・switch の分岐内）や、インポートされた struct では正しい行を指せない。

YamlDotNet の `Mark` を使えば、IR の `StructDefinition` / `FieldDefinition` に定義元ファイルと行番号を持たせられる。

## 機能要件

### 追加する機能

- [x] `YamlStructModel` / `YamlFieldModel` にデシリアライズ時の開始行を記録する — `StructsDictionaryDeserializer`（`structs:` の辞書を自前に走査してキーの行を取る）と `StructNodeDeserializer`（フィールドのマッピング開始行）
- [x] IR（`StructDefinition` / `FieldDefinition`）に `SourceFile` / `SourceLine` を追加。`YamlToIrMapper` で引き継ぐ。インポート先は元ファイルのパスを保持 — `YamlFormatLoader` がファイルごとに `SourceFile` を書く（imports はリゾルバが解決した識別子）
- [x] `FormatValidator` のエラー / 警告メッセージに行番号を含める — 診断に `SourceFile` / `SourceLine` を付け、`Location`（`png.bdef.yaml:42`）と `MessageWithLocation` を追加。CLI の `validate` / 読み込み時検証の出力と JSON 形式（`file` / `line`）に反映
- [x] GUI 定義ビューは `SourceLine` を優先し、無い場合のみ `YamlFieldLocator` にフォールバック。別ファイル定義の場合はそのファイルを表示（`IFormatCatalog` 経由で取得）— `IFormatCatalog.ReadSourceAsync` を追加（デスクトップ: ファイル、Web: `HttpImportResolver`）

### 変更しないもの（スコープ外）

- `validate` サブコマンドの出力形式変更（行番号追加のみ）

## 受入条件

1. [x] `formats/png.bdef.yaml` の `ihdr.width` の `SourceLine` が実際の行と一致すること — `SourceLocationTests.Png_IhdrWidth_SourceLine_MatchesTheFile`（ファイルを読んで行を探し、比較）
2. [x] 同名フィールドが複数ある定義で、それぞれの `SourceLine` が異なること — `DuplicateFieldNames_GetDistinctLines`
3. [x] インポートされた struct の `SourceFile` がインポート元ファイルを指すこと — `ImportedStruct_SourceFile_IsTheImportedFile`（wav → common/riff）
4. [x] `FormatValidator` のメッセージに `file:line` が含まれること — `ValidatorDiagnostics_CarryFileAndLine`（`isobmff.bdef.yaml:9`）、`DiagnosticsFromStringLoad_HaveLineOnly`
5. [x] GUI 定義ビューが `SourceLine` の行を強調すること（bUnit）— `DefinitionViewTests` 3 件（本ファイル / インポート先ファイル / struct ノード）と既存の `DefinitionView_HighlightsSelectedFieldLine`
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）— 1,897 件

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | IR に `SourceFile` / `SourceLine`、`ValidationDiagnostic` に位置と `Location` / `MessageWithLocation`、`FormatValidator` が位置を付与 |
| BinAnalyzer.Dsl | `StructsDictionaryDeserializer`（新規）、`StructNodeDeserializer` の行記録、ローダーのファイル記録、マッパー |
| BinAnalyzer.Gui | `IFormatCatalog.ReadSourceAsync`、`DefinitionView` |
| BinAnalyzer.Gui.Desktop / Web | `ReadSourceAsync` の実装（ファイル / HTTP） |
| BinAnalyzer.Cli | 診断出力に位置を追加（テキスト・JSON） |

### 変更が必要なドキュメント

- [x] docs/parser-design.md — 「2.2.1 定義元の位置」
- [x] docs/cli-usage.md — 検証出力の位置表記

---

## 設計メモ

### 設計方針

- **行はパーサーのイベントから取る。** YamlDotNet の `INodeDeserializer` は `IParser` を受け取り、`Current.Start` にその節の `Mark`（行・列）がある。フィールドは `YamlFieldModel` を組み立てる直前の `MappingStart` の行。struct は「名前のキーの行」が欲しいので、`structs:` の辞書（`Dictionary<string, YamlStructModel>`）を自前に走査してキーのスカラーの行を使う。標準の `DictionaryNodeDeserializer` が先に処理してしまうため、`OnTop()` でチェーンの先頭に置く
- **ファイルはローダーが書く。** デシリアライザはファイルを知らないので、`YamlFormatLoader` が YAML を読むたびに（本体も imports も）その識別子を struct とフィールドに書く。識別子は `LoadAsync` の `basePath` と同じ規則（ファイルなら絶対パス、Web なら `formats/...` の相対 URL）なので、カタログがそのまま読み戻せる。`LoadFromString(yaml)` は null
- **診断は後付け。** `FormatValidator` の各検証は `structName` / `fieldName` を診断に入れているので、最後に IR を引いて位置を付ける（フィールド行、無ければ struct 行）。`Message` 本文は変えず `MessageWithLocation` を別に用意し、既存のメッセージ比較テストを壊さない
- **定義ビューは IR から引く。** 選択ノードの最寄り祖先 struct（`StructType`）と（配列要素なら親配列の）フィールド名で `FieldDefinition` を引き、`SourceLine` があればそれを、無ければ従来のテキスト探索を使う。定義元が本ファイルと違えばカタログの `ReadSourceAsync` でそのファイルを取得して表示し、ヘッダにインポート定義である旨を出す。取得済みのテキストはビュー内でキャッシュする

### モデル変更

- `StructDefinition` / `FieldDefinition`: `SourceFile: string?`、`SourceLine: int?`（1 始まり）
- `ValidationDiagnostic`: 末尾に省略可能な `SourceFile` / `SourceLine`。既存の 5 引数の生成はそのまま
- `YamlStructModel` / `YamlFieldModel`: `[YamlIgnore]` の `SourceLine` / `SourceFile`

### インタフェース変更

- `IFormatCatalog.ReadSourceAsync(string sourceFile)` を追加（テストの `FakeFormatCatalog` は `AddSource` で応答を登録）
- CLI の診断出力にテキストでは ` (file:line)`、JSON では `file` / `line` が加わる

### 代替案

- `YamlFieldLocator` の探索を賢くする案（新形式の `fields:`、コメント、引用符付きの名前に対応）— 同名フィールドとインポートは原理的に区別できないので不採用。フォールバックとしては残す
- `DecodedNode` に定義（`FieldDefinition`）への参照を持たせる案 — 同名フィールドが同じ struct に複数あるときも厳密になるが、REQ-180 で削ったばかりのノードに 8 B 足す。同名重複は `if:` で排他な稀な書き方なので見送り

### 懸念事項

- struct 内に同名フィールドが複数ある場合、定義ビューは最初のものを指す（診断も同様）
- `YamlDotNet` の行はゼロ始まりではなく 1 始まり（`Mark.Line`）。定義ビュー内部の `YamlLocation` は 0 始まりなので −1 して渡す

---

## 実装メモ

### 実装中の設計変更

- 当初は `StructNodeDeserializer` の中で `Dictionary<string, YamlStructModel>` も横取りしようとしたが、`InsteadOf<ObjectNodeDeserializer>` の位置では `DictionaryNodeDeserializer` が先に辞書を処理してしまい、struct の行が取れなかった。辞書用を別クラスにして `OnTop()` で登録した

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| SourceLocationTests（Dsl.Tests） | Png_IhdrWidth_SourceLine_MatchesTheFile、DuplicateFieldNames_GetDistinctLines、ImportedStruct_SourceFile_IsTheImportedFile、ValidatorDiagnostics_CarryFileAndLine、DiagnosticsFromStringLoad_HaveLineOnly | AC-1〜4 |
| DefinitionViewTests（Gui.Tests） | FieldInMainFile_HighlightsParserLine、FieldInImportedStruct_ShowsImportedFile_AndItsLine、StructNodeSelected_HighlightsStructLine_InItsFile | AC-5 |

### 気づき・今後の課題

- REQ-173（GUI のスキーマ図）の「図のノードから定義へジャンプ」は `StructDefinition.SourceFile` / `SourceLine` をそのまま使える
- 定義ビューは読み取り専用のまま。インポート先ファイルを表示したときに戻るリンクは無く、本ファイルのフィールドを選ぶと戻る
- `validate` の JSON 出力に `file` / `line` が加わったが、スキーマ（`schemas/`）は定義ファイル用で診断出力には無いので更新不要
