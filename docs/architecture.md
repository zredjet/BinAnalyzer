# アーキテクチャ

## パイプライン

```
DSL(.bdef.yaml) → [DSLパーサー] → IR(中間表現) → [バイナリデコーダー] + バイナリデータ → デコード済みツリー → [出力フォーマッター] → ツリー表示
                                        └→ [スキーマフォーマッター] → Mermaid / DOT スキーマ図
```

## プロジェクト構成

```
BinAnalyzer/
├── src/
│   ├── BinAnalyzer.Core/          # ドメインモデル、式、インタフェース
│   ├── BinAnalyzer.Dsl/           # YAML → IR変換（YamlDotNet）
│   ├── BinAnalyzer.Engine/        # バイナリデコーダーエンジン（BCLのみ）
│   ├── BinAnalyzer.Output/        # 出力フォーマッター（BCLのみ）
│   ├── BinAnalyzer.Presentation/  # 表示用ViewModel・純関数（Coreのみ、UI非依存）
│   ├── BinAnalyzer.Tui/            # 対話型ターミナルUI（Terminal.Gui）
│   ├── BinAnalyzer.Gui/           # GUI共通コンポーネント（Razor Class Library）
│   ├── BinAnalyzer.Gui.Desktop/   # デスクトップGUIホスト（Photino.Blazor）
│   ├── BinAnalyzer.Web/           # Blazor WebAssembly版（ブラウザUI、Guiをホスト）
│   └── BinAnalyzer.Cli/           # CLIエントリポイント
├── tests/
│   ├── BinAnalyzer.Core.Tests/
│   ├── BinAnalyzer.Dsl.Tests/
│   ├── BinAnalyzer.Engine.Tests/
│   ├── BinAnalyzer.Presentation.Tests/
│   ├── BinAnalyzer.Tui.Tests/
│   ├── BinAnalyzer.Gui.Tests/     # bUnit コンポーネントテスト
│   └── BinAnalyzer.Integration.Tests/
├── benchmarks/
│   └── BinAnalyzer.Benchmarks/    # BenchmarkDotNetによるパフォーマンス計測
└── formats/
    ├── png.bdef.yaml
    ├── bmp.bdef.yaml
    ├── wav.bdef.yaml
    ├── zip.bdef.yaml
    ├── elf.bdef.yaml
    ├── pdf.bdef.yaml
    ├── jpeg.bdef.yaml
    ├── ico.bdef.yaml
    ├── midi.bdef.yaml
    ├── pcap.bdef.yaml
    ├── webp.bdef.yaml
    ├── mp3.bdef.yaml
    ├── tar.bdef.yaml
    ├── flac.bdef.yaml
    ├── java-class.bdef.yaml
    ├── gif.bdef.yaml
    ├── pe.bdef.yaml
    ├── macho.bdef.yaml
    ├── sqlite.bdef.yaml
    └── tiff.bdef.yaml
```

## 依存関係

```
Cli → Dsl, Engine, Output, Tui, Gui.Desktop
Gui.Desktop → Gui, Dsl（+ Photino.Blazor）
Gui → Presentation, Dsl, Engine, Output, Compression（+ Microsoft.AspNetCore.Components.Web）
Web → Gui, Dsl, Engine, Output, Compression（+ Blazor WASM）
Dsl → Core（+ YamlDotNet）
Engine → Core
Output → Core
Tui → Core, Presentation（+ Terminal.Gui）
Presentation → Core
Core → （なし）
```

## 主要コンポーネント

### IR（中間表現） — Core/Models/

IRはバイナリフォーマットの正規表現。YAML構文から独立しています。

- **FormatDefinition** — 最上位: 名前、エンディアン、列挙型、フラグ、構造体、ルート構造体
- **StructDefinition** — 名前付きフィールドの集合（エンディアン上書き、アライメント、文字列テーブルフラグ、ビットストリームモードフラグ、テンプレートパラメータリスト）
- **FieldDefinition** — 型、サイズ、enum/flags参照、繰り返しモード、switch、期待値、条件式、アライメント、パディング、値式（virtual用）、seekオフセット式、seek復帰フラグ、エンディアン上書き、バリデーション式、文字列テーブル参照、diffキー（単一キーまたは複合キーリスト）、テンプレートstruct引数リスト
- **EnumDefinition / FlagsDefinition** — 値のマッピング
- **ChecksumSpec** — チェックサム検証仕様（アルゴリズム名とフィールド名リスト）
- **TemplateParameter** — テンプレートstructのパラメータ定義（名前、オプションのデフォルト値）
- **StructArgument** — struct参照時のテンプレート引数（名前付き/位置引数、リテラル値/式）
- **SwitchCase / BitfieldEntry / RepeatMode** — switch分岐、ビットフィールド、繰り返しモードの定義

### 式システム — Core/Expressions/

DSL内の動的値を扱うミニ言語: `{length}`, `{length - 4}`, `{type == 'IHDR'}`

- **ExpressionNode** — 判別共用体AST: LiteralInt, LiteralString, FieldReference, BinaryOp, UnaryOp
- **ExpressionTokenizer** — 字句解析器
- **ExpressionParser** — 演算子優先順位対応の再帰下降パーサー

ASTの定義はCore（DSLとEngineの両方が必要とするため）。評価はEngineに配置。

### バリデーション — Core/Validation/

フォーマット定義の静的検証。デコード前にエラーと警告を検出。

- **FormatValidator** — 全フィールド・struct定義の整合性チェック（VAL001〜VAL011: エラー、VAL101〜VAL116: 警告）
- **ValidationResult** — 診断結果コレクション（IsValid, Errors, Warnings）
- **ValidationDiagnostic** — 個別診断: 重大度、コード、メッセージ、struct名、フィールド名

### 出力フィルタ — Core/

- **PathFilter** — ドット区切りパスパターンマッチャー。`*`（1階層）、`**`（0階層以上）ワイルドカード対応
- **NodeFilterHelper** — DecodedNodeツリーをPathFilterで刈り込み、マッチしたリーフとその祖先のみ保持

### エラーハンドリング — Core/

- **DecodeException** — 構造化デコードエラー（Offset, FieldPath, FieldType, Hint）。フォーマット済みメッセージ生成
- **ErrorMode** — エラー時動作モード（Stop / Continue）
- **DecodeResult** — デコード結果コンテナ（Root: DecodedStruct, Errors: IReadOnlyList\<DecodeError\>）
- **DecodeError** — エラー回復モードで記録された個別エラー（Message, Offset, FieldPath, FieldType）
- **ValidationInfo** — カスタムバリデーション結果（Passed, Expression）

### 差分データ構造 — Core/Diff/

- **DiffResult** — 差分結果コンテナ（Entries, HasDifferences）
- **DiffEntry** — 個別差分（Kind, FieldPath, OldValue, NewValue）
- **DiffKind** — 変更種別: Changed, Added, Removed

### デコード結果ツリー — Core/Decoded/

デコード出力: 型付きノードのツリー。

- **DecodedStruct** — 名前付き構造体と子要素
- **DecodedArray** — 繰り返し要素
- **DecodedInteger** — オプションのenumラベル付き、チェックサム検証結果（ChecksumValid, ChecksumExpected）、文字列テーブル参照値
- **DecodedFloat** — 単精度/倍精度浮動小数点数
- **DecodedBytes** — オプションのバリデーション付き
- **DecodedString** — オプションのフラグ付き
- **DecodedBitfield** — ビットフィールドと抽出値
- **DecodedFlags** — ビットレベルのフラグ状態
- **DecodedCompressed** — 圧縮データ（zlib/deflate）。展開サイズ、アルゴリズム名、オプションのネスト解析結果を保持
- **DecodedVirtual** — 計算フィールド（バイナリデータを消費しない、式の評価結果を保持）
- **DecodedError** — エラー回復モードでデコード失敗したフィールドのプレースホルダー（エラーメッセージ、フィールド型を保持）

### バイナリデコーダー — Engine/

- **DecodeContext** — ReadOnlyMemory\<byte\>のラッパー。位置追跡、スコープスタック、変数バインディング、Seek()による絶対オフセットジャンプ、SavePosition()/RestorePosition()による位置の保存・復帰、文字列テーブル登録・参照、BitReader内部クラスによるビットストリーム読み取り、PushVariableScope()によるテンプレートパラメータ用オーバーレイスコープ、状態変数ストア（スコープスタックとは独立した永続Dictionary）
- **ExpressionEvaluator** — DecodeContextの変数を使用してASTを評価。`@state_name` による状態変数の参照にも対応
- **BinaryDecoder** — フィールドデコード、繰り返し処理、switch解決、テンプレート引数の解決・バインドのオーケストレーター
- **Crc32Calculator** — ISO 3309準拠のCRC-32計算器（PNG/ZIP互換）
- **EncodingHelper** — Shift-JISエンコーディング登録・キャッシュヘルパー
- **DiffEngine** — 2つのDecodedStructを再帰比較し、変更・追加・削除の差分リストを生成

### DSLパーサー — Dsl/

- **YamlModels/** — YamlDotNetデシリアライズ用DTO（IRとは別クラス）
- **YamlToIrMapper** — YAML DTOをIRに変換、式のパース、参照の検証、テンプレートstructキー・参照のパース
- **YamlFormatLoader** — IFormatLoader実装。インポートの再帰解決（循環検出）と定義マージを担当。取得手段は `IImportResolver`（Core）に委ね、`Load(path)` は `FileImportResolver`、`LoadAsync(yaml, basePath, resolver)` は任意のリゾルバで解決する
- **FileImportResolver / ImportPath** — 既定のファイルシステムリゾルバと、URL 系リゾルバ向けの字句的な相対パス結合（`.` / `..` の正規化）

### 出力フォーマッター — Output/

デコード済みツリーを各種形式に変換（IOutputFormatter）:

- **TreeOutputFormatter** — インデント付きツリー表示（デフォルト）
- **JsonOutputFormatter** — JSON形式
- **HexDumpOutputFormatter** — フィールド注釈付きヘックスダンプ
- **HtmlOutputFormatter** — インタラクティブHTML（折りたたみ/展開、ダークテーマ、検索・フィルタ機能）
- **MapOutputFormatter** — バイナリフィールドレイアウトのビジュアルマップ
- **CsvOutputFormatter** — CSV / TSV形式（フラットなフィールド一覧、RFC 4180準拠エスケープ）
- **DiffOutputFormatter** — 2つのバイナリの構造的差分表示

各フォーマッターはANSIカラー出力に対応（ColorMode: Auto / Always / Never）。

### 値の書き戻し — Core/Patching, Engine/FieldEncoder, Engine/BinaryPatcher

読み取り専用だったエンジンに、固定長の書き戻しを追加する（REQ-169。CLI の `patch` コマンド REQ-164 と共有する基盤）。

- `DecodedNode.DslType`（DSL の型）、`DecodedInteger.Endianness` / `EnumRef`、`DecodedInteger` / `DecodedBytes` の `ChecksumCoverage`（検証時に算出対象とした `ByteRange` の列）をデコーダが記録する。書き戻しに必要な符号・エンディアン・依存関係を、フォーマット定義を再走査せずにデコード結果だけから得るため
- `FieldEditRules.Classify`（Core）がノード単体の編集可否を決め、`FieldEncoder`（Engine）が入力文字列を同じ長さのバイト列にする（範囲チェック、固定長文字列の 0x00 埋め、16 進バイト列）
- `BinaryPatcher.Apply` はパッチを書いた後に再デコードし、変更範囲と `ChecksumCoverage` が重なる無効なチェックサムへ `ChecksumExpected` / `ChecksumExpectedHex` を書き戻す。チェックサムが別のチェックサムの範囲に含まれる場合は変化が無くなるまで反復する（上限 8 回）。アルゴリズム別の計算はデコーダの検証をそのまま使う
- Presentation の `FieldEditability`（データ空間・長さ系フィールドの判定）と `ChecksumDependencies`（再計算対象の事前表示）は `NodeIndex` 上の純関数

### 表示ロジック — Presentation/

デコード結果ツリー（`DecodedNode`）を表示用データに変換する純関数群。UI フレームワーク（Terminal.Gui / Blazor）に依存せず、TUI と GUI で共有する。

- **NodeChildren** — 子ノード走査の単一情報源（struct は padding 除外、array は要素、compressed は展開後の内容）。`Descendants` で深さ優先列挙
- **NodeDisplayText** — ツリー 1 行の表示文字列（`For`）、値部分のみ（`ValueOnly`）、型ラベル（`TypeLabel`）
- **NodeDetailFormatter** — 詳細ペイン / インスペクター用の Key/Value 行（`DetailRow` に `DetailRowKind` でスタイルヒント付き）
- **NodeSearch** — 名前による検索（`ByName`）

### 対話型TUI — Tui/

Terminal.Gui v2 ベースの対話型ターミナルUI。`--output tui` で起動。3ペイン構成でデコード結果を対話的に探索:

- **TuiApp** — エントリポイント。Application ライフサイクル管理、3ペインレイアウト構築、グローバルキーバインド
- **TreePane** — TreeView\<DecodedNode\> によるツリービュー。展開/折りたたみ、ノード選択
- **DetailPane** — 選択ノードの詳細情報表示（型、オフセット、サイズ、値など）
- **HexPane** — 選択ノードに対応するバイト範囲のヘックスダンプ（`^^` マーカーでハイライト）
- **SearchBar** — フィールド名によるインクリメンタル検索、検索結果間のジャンプ
- **TuiState** — 状態管理（選択ノード、検索状態）。イベント駆動でペイン間を連携
- **DecodedNodeTreeBuilder** — ITreeBuilder\<DecodedNode\> 実装。子ノード列挙ロジック
- **NodeDetailFormatter** — DecodedNode → 詳細表示文字列リスト変換（テスト可能な純粋ロジック）

### GUI — Gui/ , Gui.Desktop/

`BinAnalyzer.Gui` は Razor Class Library で、Web（WASM）とデスクトップ（Photino.Blazor）の両方から同じコンポーネントをホストする。

- **Abstractions/** — ホスト差分の抽象。`IFormatCatalog`（フォーマット定義の一覧・読込・拡張子検出）、`IFileSource`（ネイティブダイアログの有無とファイル取得）
- **State/** — Blazor 非依存の状態。`GuiDocument`（タブ 1 枚: データ・フォーマット・エンディアン上書き・デコード結果・`NodeIndex`・選択/ホバー/展開/検索・編集履歴）、`GuiSession`（タブ集合・アクティブ・右ペイン種別・差分・保存）、`DecodeService`（エラー継続モードで所要時間計測）
- **編集・書き戻し（REQ-169）** — `GuiDocument.Data` は「現在デコード・表示しているバイト列」で copy-on-write（編集のたびに新しい配列に差し替え、`Revision` が進む）。原本は `OriginalData`。`PreviewEdit` は Engine の `FieldEncoder` で入力をバイト列にし、Presentation の `ChecksumDependencies` で再計算対象を求める。`ApplyEdit` / `RevertField` は Engine の `BinaryPatcher` に委譲し、本体書き込み＋チェックサム再計算を 1 つの `EditRecord`（`ByteWrite` の束）として Undo / Redo スタックに積む。保存は `IFileSource.SaveAsync`（デスクトップ: `ShowSaveFileAsync`、Web: `downloadFile` でダウンロード）
- **Components/** — `GuiShell`（全体レイアウト）、`HexView` / `HexRowView`（`<Virtualize>`、512 行以下は非仮想化）、`HighlightStyle`（ホバーは `<style>` 1 ルールの再描画のみ）、`StructTree` / `Inspector`、`DefinitionView`（`YamlFieldLocator` で選択フィールド行を強調）、`DiffView`（`DiffEngine` の `DiffResult` に直接バインド）、`StructureMapView`、`CommandBar`、`FilePicker` ほか
- **wwwroot/** — `gui.css` / `gui.js`。デスクトップ配信用に `_content/BinAnalyzer.Gui/...` の論理名で埋め込みリソースにも含める

`BinAnalyzer.Gui.Desktop` は Photino.Blazor ホスト。`EmbeddedWebRootFileProvider` が埋め込みリソースから `index.html` と `_content/...` を配信するため物理 `wwwroot` が不要（CLI の単一ファイル publish を壊さない）。`DirectoryFormatCatalog` は exe 隣 / カレントの `formats/` と `-f` 指定ファイルを提供し、`YamlFormatLoader.LoadAsync` + `FileImportResolver` で imports も解決する。CLI の `-o gui` は `GuiApp.Run` をインプロセスで呼ぶ（Windows では STA スレッド）。

### Blazor WebAssembly版 — Web/

Blazor WebAssembly Standalone アプリケーション。サーバーなしの静的サイトとしてデプロイ可能。既存の Core / Dsl / Engine / Output / Compression ライブラリをブラウザ上で再利用:

- **Program.cs** — WASM エントリポイント。HttpClient と FormatService を DI 登録
- **FormatService** — HttpClient で `wwwroot/formats/` から YAML を取得し、`YamlFormatLoader.LoadAsync()` で IR に変換。キャッシュ付き
- **HttpImportResolver** — `IImportResolver` の HTTP 実装。`imports:` の相対パスをインポート元 URL 基準で結合し（`formats/wav.bdef.yaml` → `formats/common/riff.bdef.yaml`）、404 は「見つからない」として扱う。これにより avi / heif / mp4 / wav / webp など共通型ライブラリを使う定義も Web で読める
- **Home.razor / Home.razor.cs** — メインページ。ファイルアップロード（ドラッグ&ドロップ）、フォーマット自動検出・手動選択、BinaryDecoder でデコード、HtmlOutputFormatter の出力を iframe srcdoc で表示、JsonOutputFormatter の出力を Blob URL でダウンロード

フォーマット定義（IR）からスキーマ図を生成（ISchemaFormatter）:

- **MermaidSchemaFormatter** — Mermaid classDiagram 形式（struct 間参照関係グラフ）
- **DotSchemaFormatter** — Graphviz DOT digraph 形式（struct 間参照関係グラフ）

## 設計上の重要ポイント

1. **YAMLモデルとIRは分離** — DSL構文の進化がエンジンに影響しない
2. **式のASTはCore、評価はEngine** — DSLも式を作成する必要があるため
3. **スコープスタックで変数バインディング** — switchのバウンダリスコープ、ネスト構造体の変数解決
4. **`size: remaining` は現在のスコープ境界を参照** — ファイル全体ではない
5. **文字列フィールドにフラグを付与可能** — PNGチャンクタイプはASCII文字列かつビットフラグ; DecodedStringにオプションのFlags
6. **バイナリ読み取りはBCLのみ** — BinaryPrimitives, ReadOnlyMemory\<byte\>, Span\<byte\>
7. **DSLインポートはDSL層で解決** — インポートの再帰解決・マージはYamlFormatLoader内で完結し、Engineには単一のFormatDefinitionが渡される。取得手段（ファイル / HTTP）だけを `IImportResolver` に切り出し、全ホストで同じフォーマット群を扱う
8. **アライメント・パディングはフィールド/構造体レベルで宣言的に指定** — DecodeContext.AlignTo()でバイト境界調整。パディングフラグはDecodedNodeに伝搬し、出力層でフィルタ
9. **seekによる絶対オフセットジャンプ** — DecodeContext.Seek()で任意の絶対位置にジャンプ。seek_restoreでデコード後に元の位置に復帰。ポインタベースフォーマット（PE/ELF/ZIP/TIFF等）の解析に対応
10. **エンディアンのスコープスタック** — 構造体・フィールドレベルでエンディアンを上書き可能。DecodeContextのオーバーレイスコープ（IsOverlay=true）でposition変更なしにエンディアンのみ切り替え。優先順位: フィールド > 構造体 > フォーマットデフォルト
11. **カスタムバリデーション式** — フィールドに `validate` 式を指定し、デコード後に評価。結果はDecodedNodeのValidationInfoに格納され、出力フォーマッターが ✓/✗ で表示
12. **エラー回復モード** — `--on-error continue` でデコードエラー後も解析を継続。エラー箇所はDecodedErrorノードとして結果ツリーに含まれる。DecodeResultでエラーリストも返却
13. **文字列テーブル参照** — IsStringTable構造体のバイト列をDecodeContextに登録し、整数フィールドのStringTableRefでNUL終端文字列をルックアップ。ELFの.strtab等で使用
14. **ビットストリームモード** — `mode: bitstream` 構造体内ではフィールドのsizeがビット単位で解釈される。DecodeContext.BitReaderがMSB-firstのビット単位読み取りを提供し、構造体終了時に自動バイトアライン。既存bitfield型とは独立して共存
15. **テンプレートパラメータバインディング** — パラメータ付きstruct定義（`tlv(tag_size=1, len_size=1)`）をサポート。DSL層でキー文字列・struct参照文字列をパースし、IRのTemplateParameter/StructArgumentに変換。デコード時にBinaryDecoderが引数を解決（位置/名前付き/式の3種類）し、PushVariableScope()でオーバーレイスコープを作成してパラメータを変数としてバインド。PopScope時に自動クリーンアップされ、親スコープへの漏洩を防止
16. **ステートフル変数** — `state`/`state_if`/`state_default`フィールドプロパティで状態変数を管理。DecodeContextにスコープスタックとは独立したDictionary\<string, object\>を保持し、PushScope/PopScopeの影響を受けずデコードセッション全体で永続。式内では`@state_name`プレフィックスで参照し、通常のフィールド変数との名前空間を分離。MIDIランニングステータス等のステートフルパースに対応
