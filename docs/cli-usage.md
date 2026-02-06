# CLIリファレンス

## 基本コマンド

```
binanalyzer <file> -f <format> [-o <output>] [--color <mode>] [--no-validate] [--filter <pattern>...]
```

### 引数

| 引数 | 説明 |
|------|------|
| `file` | 解析対象のバイナリファイル |

### オプション

| オプション | 説明 | デフォルト |
|------------|------|-----------|
| `-f, --format <file>` | フォーマット定義ファイル（`.bdef.yaml`）**必須** | — |
| `-o, --output <format>` | 出力形式（`tree`, `json`, `hexdump`, `html`, `map`, `csv`, `tsv`） | `tree` |
| `--color <mode>` | カラー出力（`auto`, `always`, `never`） | `auto` |
| `--no-validate` | フォーマット定義のバリデーションをスキップ | — |
| `--filter <pattern>` | 出力フィルタ（フィールドパスパターン、複数指定可） | — |

### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 正常終了 |
| `1` | エラー（ファイル未検出、フォーマット定義エラー、デコードエラー等） |

### バリデーション

デフォルトではデコード前にフォーマット定義の静的検証が実行されます。エラー（VAL001〜VAL010）が検出された場合、解析は中断されます。警告（VAL101〜VAL110）はstderrに表示されますが解析は継続します。

`--no-validate` を指定するとバリデーションをスキップします。

検証項目:
- 未定義のstruct/enum/flags参照
- switch型フィールドの必須プロパティ
- サイズ指定が必要な型のサイズ未指定
- alignの値が正の整数であること
- virtual型フィールドのvalue未指定
- 未使用のenum/flags/struct定義（警告）

## 出力形式

### tree（デフォルト）

構造をインデント付きツリーで表示します。フィールド名、オフセット、サイズ、値を階層的に表示します。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml
```

```
PNG [0x00000000] (45 bytes)
├── signature [0x00000000] (8 bytes): 89 50 4E 47 0D 0A 1A 0A  ✓
└── chunks [0x00000008] (37 bytes) [2 items]
    ├── #0 → chunk [0x00000008] (25 bytes)
    │   ├── length: 13
    │   ├── type: "IHDR"
    │   └── ...
    └── #1 → chunk [0x00000021] (12 bytes)
        └── ...
```

### json

デコード結果をJSON形式で出力します。プログラムからの後処理に適しています。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o json
```

### hexdump

フィールド注釈付きのヘックスダンプを出力します。各バイト範囲がどのフィールドに属するかを確認できます。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o hexdump
```

### html

インタラクティブなHTMLレポートを生成します。ブラウザで開いて折りたたみ/展開操作が可能です。ダークテーマに対応しています。

検索機能も内蔵しています:
- 画面上部の検索バーでフィールド名・値・enumラベル等をインクリメンタル検索
- マッチしたノードはハイライト表示され、自動的に折りたたみが展開されます
- キーボードショートカット: `/`（検索フォーカス）、`Enter`（次へ）、`Shift+Enter`（前へ）、`Escape`（クリア）

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o html > report.html
```

### map

バイナリのフィールドレイアウトをバーチャート形式でビジュアル表示します。各フィールドが占めるバイト範囲を直感的に確認できます。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o map
```

### csv

デコード結果をCSV形式で出力します。カラムは `path,type,offset,size,value` です。リーフフィールドのみ出力します（構造体・配列ノードは行を出力しません）。RFC 4180準拠のエスケープに対応しています。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv
```

### tsv

CSV と同じ構造でタブ区切りの出力です。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o tsv
```

## 出力フィルタ

`--filter` オプションでフィールドパスパターンを指定して、出力を絞り込むことができます。

### パスパターン

- パス区切りは `.`（ドット）
- `*` は任意の1階層にマッチ
- `**` は任意の0階層以上にマッチ
- 複数指定はOR結合（`--filter "a.b" --filter "c.d"`）

### パスパターンの例

| パターン | マッチ対象 |
|----------|-----------|
| `chunks.*.type` | 全チャンクのtypeフィールド |
| `**.width` | 任意の深さのwidthフィールド |
| `chunks.0.data` | 最初のチャンクのdata |
| `header.*` | header直下の全フィールド |

### 使用例

```bash
# 全チャンクのtype名だけを一覧
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --filter "PNG.chunks.*.type"

# CSV出力と組み合わせてデータ抽出
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv --filter "**.width" --filter "**.height"
```

## diff サブコマンド

2つのバイナリファイルを同じフォーマット定義で解析し、構造的な差分を表示します。

```
binanalyzer diff <file1> <file2> -f <format> [--color <mode>]
```

### 引数

| 引数 | 説明 |
|------|------|
| `file1` | 比較元のバイナリファイル |
| `file2` | 比較先のバイナリファイル |

### オプション

| オプション | 説明 | デフォルト |
|------------|------|-----------|
| `-f, --format <file>` | フォーマット定義ファイル（`.bdef.yaml`）**必須** | — |
| `--color <mode>` | カラー出力（`auto`, `always`, `never`） | `auto` |

### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 差分なし |
| `1` | 差分あり、またはエラー |

### 使用例

```bash
dotnet run --project src/BinAnalyzer.Cli -- diff original.png modified.png -f formats/png.bdef.yaml
```

## カラー出力

`--color` オプションで制御します:

| モード | 動作 |
|--------|------|
| `auto` | 端末出力時のみカラー表示。リダイレクト・パイプ時は無効 |
| `always` | 常にANSIカラーを使用 |
| `never` | カラーを使用しない |

### カラースキーム

tree出力でのカラー割り当て:

| 色 | 用途 |
|----|------|
| シアン | フィールド名、数値 |
| 緑 | 文字列値、バリデーション成功（✓） |
| 黄 | ヘックスバイト列 |
| マゼンタ | enumラベル |
| 赤 | バリデーション失敗（✗） |
| 暗色 | オフセット、サイズ、ツリー罫線 |

## 使用例

```bash
# PNGファイルをツリー表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml

# JSON形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o json

# ヘックスダンプ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o hexdump

# HTMLレポート生成
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o html > report.html

# ビジュアルマップ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o map

# CSV形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv

# TSV形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o tsv

# カラー出力を強制
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --color always

# 2つのファイルの差分比較
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml

# バリデーションをスキップして解析
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --no-validate

# 出力フィルタで特定フィールドのみ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --filter "PNG.chunks.*.type"

# フィルタとCSVを組み合わせてデータ抽出
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv --filter "**.width" --filter "**.height"
```
