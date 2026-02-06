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
- **StructDefinition** — 名前付きフィールドの集合（エンディアン上書き、アライメント、文字列テーブルフラグ）
- **FieldDefinition** — 型、サイズ、enum/flags参照、繰り返しモード、switch、期待値、条件式、アライメント、パディング、値式（virtual用）、seekオフセット式、seek復帰フラグ、エンディアン上書き、バリデーション式、文字列テーブル参照
- **EnumDefinition / FlagsDefinition** — 値のマッピング
- **ChecksumSpec** — チェックサム検証仕様（アルゴリズム名とフィールド名リスト）
- **SwitchCase / BitfieldEntry / RepeatMode** — switch分岐、ビットフィールド、繰り返しモードの定義

### 式システム — Core/Expressions/

DSL内の動的値を扱うミニ言語: `{length}`, `{length - 4}`, `{type == 'IHDR'}`

- **ExpressionNode** — 判別共用体AST: LiteralInt, LiteralString, FieldReference, BinaryOp, UnaryOp
- **ExpressionTokenizer** — 字句解析器
- **ExpressionParser** — 演算子優先順位対応の再帰下降パーサー

ASTの定義はCore（DSLとEngineの両方が必要とするため）。評価はEngineに配置。

### バリデーション — Core/Validation/

フォーマット定義の静的検証。デコード前にエラーと警告を検出。

- **FormatValidator** — 全フィールド・struct定義の整合性チェック（VAL001〜VAL011: エラー、VAL101〜VAL112: 警告）
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

- **DecodeContext** — ReadOnlyMemory\<byte\>のラッパー。位置追跡、スコープスタック、変数バインディング、Seek()による絶対オフセットジャンプ、SavePosition()/RestorePosition()による位置の保存・復帰、文字列テーブル登録・参照
- **ExpressionEvaluator** — DecodeContextの変数を使用してASTを評価
- **BinaryDecoder** — フィールドデコード、繰り返し処理、switch解決のオーケストレーター
- **Crc32Calculator** — ISO 3309準拠のCRC-32計算器（PNG/ZIP互換）
- **EncodingHelper** — Shift-JISエンコーディング登録・キャッシュヘルパー
- **DiffEngine** — 2つのDecodedStructを再帰比較し、変更・追加・削除の差分リストを生成

### DSLパーサー — Dsl/

- **YamlModels/** — YamlDotNetデシリアライズ用DTO（IRとは別クラス）
- **YamlToIrMapper** — YAML DTOをIRに変換、式のパース、参照の検証
- **YamlFormatLoader** — IFormatLoader実装。インポートの再帰解決と定義マージを担当

### 出力フォーマッター — Output/

デコード済みツリーを各種形式に変換:

- **TreeOutputFormatter** — インデント付きツリー表示（デフォルト）
- **JsonOutputFormatter** — JSON形式
- **HexDumpOutputFormatter** — フィールド注釈付きヘックスダンプ
- **HtmlOutputFormatter** — インタラクティブHTML（折りたたみ/展開、ダークテーマ、検索・フィルタ機能）
- **MapOutputFormatter** — バイナリフィールドレイアウトのビジュアルマップ
- **CsvOutputFormatter** — CSV / TSV形式（フラットなフィールド一覧、RFC 4180準拠エスケープ）
- **DiffOutputFormatter** — 2つのバイナリの構造的差分表示

各フォーマッターはANSIカラー出力に対応（ColorMode: Auto / Always / Never）。

## 設計上の重要ポイント

1. **YAMLモデルとIRは分離** — DSL構文の進化がエンジンに影響しない
2. **式のASTはCore、評価はEngine** — DSLも式を作成する必要があるため
3. **スコープスタックで変数バインディング** — switchのバウンダリスコープ、ネスト構造体の変数解決
4. **`size: remaining` は現在のスコープ境界を参照** — ファイル全体ではない
5. **文字列フィールドにフラグを付与可能** — PNGチャンクタイプはASCII文字列かつビットフラグ; DecodedStringにオプションのFlags
6. **バイナリ読み取りはBCLのみ** — BinaryPrimitives, ReadOnlyMemory\<byte\>, Span\<byte\>
7. **DSLインポートはDSL層で解決** — インポートの再帰解決・マージはYamlFormatLoader内で完結し、Engineには単一のFormatDefinitionが渡される
8. **アライメント・パディングはフィールド/構造体レベルで宣言的に指定** — DecodeContext.AlignTo()でバイト境界調整。パディングフラグはDecodedNodeに伝搬し、出力層でフィルタ
9. **seekによる絶対オフセットジャンプ** — DecodeContext.Seek()で任意の絶対位置にジャンプ。seek_restoreでデコード後に元の位置に復帰。ポインタベースフォーマット（PE/ELF/ZIP/TIFF等）の解析に対応
10. **エンディアンのスコープスタック** — 構造体・フィールドレベルでエンディアンを上書き可能。DecodeContextのオーバーレイスコープ（IsOverlay=true）でposition変更なしにエンディアンのみ切り替え。優先順位: フィールド > 構造体 > フォーマットデフォルト
11. **カスタムバリデーション式** — フィールドに `validate` 式を指定し、デコード後に評価。結果はDecodedNodeのValidationInfoに格納され、出力フォーマッターが ✓/✗ で表示
12. **エラー回復モード** — `--on-error continue` でデコードエラー後も解析を継続。エラー箇所はDecodedErrorノードとして結果ツリーに含まれる。DecodeResultでエラーリストも返却
13. **文字列テーブル参照** — IsStringTable構造体のバイト列をDecodeContextに登録し、整数フィールドのStringTableRefでNUL終端文字列をルックアップ。ELFの.strtab等で使用
