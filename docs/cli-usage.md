# CLIリファレンス

## 基本コマンド

```
binanalyzer [<file>] -f <format> [-o <output>] [--color <mode>] [--no-validate] [--on-error <mode>] [--max-repeat <N>] [--filter <pattern>...] [--stdin] [-q] [--error-format <format>]
```

### 引数

| 引数 | 説明 |
|------|------|
| `file` | 解析対象のバイナリファイル（`-` でstdin入力、省略時は `--stdin` が必要） |

### オプション

| オプション | 説明 | デフォルト |
|------------|------|-----------|
| `-f, --format <file>` | フォーマット定義ファイル（`.bdef.yaml`）**必須** | — |
| `-o, --output <format>` | 出力形式（`tree`, `json`, `hexdump`, `html`, `map`, `csv`, `tsv`, `tui`） | `tree` |
| `--color <mode>` | カラー出力（`auto`, `always`, `never`） | `auto` |
| `--no-validate` | フォーマット定義のバリデーションをスキップ | — |
| `--on-error <mode>` | エラー時の動作（`stop`, `continue`） | `stop` |
| `--filter <pattern>` | 出力フィルタ（フィールドパスパターン、複数指定可） | — |
| `--stdin` | 標準入力からバイナリデータを読み込む | — |
| `-q, --quiet` | デコード結果を出力せず、終了コードのみ返す（バリデーション用途） | — |
| `--max-repeat <N>` | 繰り返し回数のグローバル上限（フォーマット定義の `repeat_max` が優先） | — |
| `--error-format <format>` | エラー出力形式（`text`, `json`） | `text` |

### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 正常終了 |
| `1` | エラー（ファイル未検出、フォーマット定義エラー、デコードエラー等） |
| `2` | バリデーション失敗（デコード成功だが `validate` 式が false） |

### エラー回復

`--on-error` オプションでデコードエラー時の動作を制御します:

| モード | 動作 |
|--------|------|
| `stop` | エラー発生時に解析を中断する（デフォルト） |
| `continue` | エラー箇所をスキップして解析を継続する |

`continue` モードではエラー箇所が `DecodedError` ノードとしてツリーに含まれ、赤色で表示されます。全エラーのサマリーが解析後にstderrに出力されます。

```bash
# エラーを無視して解析を継続
dotnet run --project src/BinAnalyzer.Cli -- broken.bin -f formats/png.bdef.yaml --on-error continue
```

### 繰り返しガード

`--max-repeat` オプションで全繰り返しフィールドにグローバルな上限を設定できます。壊れたバイナリで巨大な繰り返しカウントが検出された場合のメモリ枯渇やハングを防止します。

フォーマット定義のフィールドレベルで `repeat_max` が指定されている場合、そちらが優先されます。

```bash
# 繰り返しを最大1000要素に制限
dotnet run --project src/BinAnalyzer.Cli -- broken.bin -f formats/png.bdef.yaml --max-repeat 1000

# エラー回復と併用
dotnet run --project src/BinAnalyzer.Cli -- broken.bin -f formats/png.bdef.yaml --on-error continue --max-repeat 500
```

ガードにより打ち切られた配列はツリー出力で `(truncated: ...)` と表示され、JSON出力では `truncated: true` と `truncation_reason` フィールドが追加されます。

### バリデーション

デフォルトではデコード前にフォーマット定義の静的検証が実行されます。エラー（VAL001〜VAL011）が検出された場合、解析は中断されます。警告（VAL101〜VAL112）はstderrに表示されますが解析は継続します。

`--no-validate` を指定するとバリデーションをスキップします。

検証項目:
- 未定義のstruct/enum/flags参照
- switch型フィールドの必須プロパティ
- サイズ指定が必要な型のサイズ未指定
- alignの値が正の整数であること
- virtual型フィールドのvalue未指定
- seek_restoreがseekなしで指定されている
- string_tableが整数型以外に指定されている（警告）
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

デコード結果をCSV形式で出力します。カラムは `path,type,offset,size,value,validation` です。リーフフィールドのみ出力します（構造体・配列ノードは行を出力しません）。RFC 4180準拠のエスケープに対応しています。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv
```

### tsv

CSV と同じ構造でタブ区切りの出力です。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o tsv
```

### tui

対話型ターミナルUI（TUI）を起動します。Terminal.Gui v2 ベースの3ペイン構成で、デコード結果を対話的に探索できます。

```bash
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o tui
```

#### レイアウト

- **左上: Tree** — デコード結果のツリービュー。ノードの展開・折りたたみが可能
- **右上: Detail** — 選択ノードの詳細情報（型、オフセット、サイズ、値など）
- **下段: Hex** — 選択ノードに対応するバイト範囲のヘックスダンプ（`^^` マーカーでハイライト）

#### キーバインド

| キー | 動作 |
|------|------|
| `↑` / `↓` | ツリーノード間移動 |
| `→` / `Enter` | ノード展開 |
| `←` | ノード折りたたみ |
| `Tab` | ペイン間移動 |
| `e` | 全ノード展開 |
| `c` | 全ノード折りたたみ |
| `/` | 検索バーを表示 |
| `n` | 次の検索結果へジャンプ |
| `N`（Shift+n） | 前の検索結果へジャンプ |
| `Escape` | 検索バーを閉じる |
| `q` | TUI終了 |

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
binanalyzer diff <file1|dir1> <file2|dir2> -f <format> [--output <format>] [--color <mode>] [--summary] [--summary-only]
```

### 引数

| 引数 | 説明 |
|------|------|
| `file1` / `dir1` | 比較元のバイナリファイルまたはディレクトリ |
| `file2` / `dir2` | 比較先のバイナリファイルまたはディレクトリ |

両方にディレクトリを指定するとバッチモードになります。ファイルとディレクトリの混在指定はエラーになります。

### オプション

| オプション | 説明 | デフォルト |
|------------|------|-----------|
| `-f, --format <file>` | フォーマット定義ファイル（`.bdef.yaml`）**必須** | — |
| `--output <format>` | 出力形式（`flat`, `tree`） | `flat` |
| `--color <mode>` | カラー出力（`auto`, `always`, `never`） | `auto` |
| `--summary` | 詳細差分の末尾に統計サマリー（Changed/Added/Removed件数、一致率）を追加表示 | — |
| `--summary-only` | 統計サマリーのみ表示（詳細差分を省略） | — |

### 出力形式

| 形式 | 説明 |
|------|------|
| `flat` | 差分フィールドのパスと値の一覧をフラットに表示（デフォルト） |
| `tree` | デコード済みツリー構造で差分を表示。同一ノードは `(同一)` と表示し、変更箇所をハイライト |

### 終了コード

| コード | 意味 |
|--------|------|
| `0` | 差分なし |
| `1` | 差分あり、またはエラー |

### 使用例

```bash
# フラット形式（デフォルト）
dotnet run --project src/BinAnalyzer.Cli -- diff original.png modified.png -f formats/png.bdef.yaml

# ツリー形式
dotnet run --project src/BinAnalyzer.Cli -- diff original.png modified.png -f formats/png.bdef.yaml --output tree

# 詳細差分 + 統計サマリー
dotnet run --project src/BinAnalyzer.Cli -- diff original.png modified.png -f formats/png.bdef.yaml --summary

# 統計サマリーのみ（詳細省略）
dotnet run --project src/BinAnalyzer.Cli -- diff original.png modified.png -f formats/png.bdef.yaml --summary-only
```

### バッチモード（ディレクトリ指定）

2つのディレクトリを指定すると、同名ファイル同士を自動的にペアリングして一括比較します。

```bash
# ディレクトリ内の全ファイルを一括比較
dotnet run --project src/BinAnalyzer.Cli -- diff dir_v1/ dir_v2/ -f formats/png.bdef.yaml
```

バッチモードではファイルごとの比較結果がサマリーレポートとして出力されます:

```
--- バッチdiffレポート ---

ファイル比較結果:

  ファイル名                         結果     変更  追加  削除
  ----------------------------------------------------------------------
  file1.bin                          同一
  file2.bin                          差分あり     3     1     0
  file3.bin                          エラー   decode failed

左ディレクトリのみ (1 件):
  - file4.bin

右ディレクトリのみ (1 件):
  + file5.bin

合計: 3 ファイル比較, 1 同一, 1 差分あり, 1 エラー
```

バッチモード固有の動作:

- `--output`, `--summary`, `--summary-only` オプションは無視されます（バッチレポートが常に出力）
- ファイルペアリングはファイル名で突合（非再帰、サブディレクトリは対象外）
- 各ファイルのデコードエラーは個別にキャッチされ、バッチ全体は中断しません
- 終了コード: `0` = 全ファイル同一、`1` = 差分あり・エラーあり・片方のみのファイルあり

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

## schema サブコマンド

フォーマット定義（IR）から struct 間の参照関係を Mermaid または Graphviz DOT 形式で出力します。バイナリファイルは不要です。

```
binanalyzer schema <format-file> [-o <output>]
```

### 引数

| 引数 | 説明 |
|------|------|
| `format-file` | フォーマット定義ファイル（`.bdef.yaml`） |

### オプション

| オプション | 説明 | デフォルト |
|------------|------|-----------|
| `-o, --output <format>` | 出力形式（`mermaid`, `dot`, `graphviz`） | `mermaid` |

### 出力形式

| 形式 | 説明 |
|------|------|
| `mermaid` | Mermaid classDiagram 形式（デフォルト）。GitHub Markdown に直接埋め込み可能 |
| `dot` / `graphviz` | Graphviz DOT digraph 形式。`dot` コマンドで SVG/PNG に変換可能 |

### 出力内容

- 各 struct が Mermaid ではクラス、DOT ではレコードノードとして表示
- ルート struct はステレオタイプ（`<<root>>`）または太枠で強調
- struct フィールド参照、switch 分岐、圧縮フィールドがエッジとして表示
- 繰り返しフィールドは `*` 多重度付き

### 使用例

```bash
# Mermaid形式でスキーマ図を出力（デフォルト）
dotnet run --project src/BinAnalyzer.Cli -- schema formats/png.bdef.yaml

# Graphviz DOT形式で出力
dotnet run --project src/BinAnalyzer.Cli -- schema formats/otf.bdef.yaml -o dot

# DOT出力をSVGに変換（Graphvizが必要）
dotnet run --project src/BinAnalyzer.Cli -- schema formats/png.bdef.yaml -o dot | dot -Tsvg -o schema.svg
```

## パイプライン統合

CLIはパイプラインやCI/CDスクリプトでの利用を想定した機能を備えています。

### stdin入力

`-`（ハイフン）または `--stdin` オプションで標準入力からバイナリデータを読み込めます。

```bash
# ネットワーク取得と組み合わせ
curl -s https://example.com/file.png | binanalyzer - -f formats/png.bdef.yaml

# --stdin オプション
curl -s https://example.com/file.png | binanalyzer --stdin -f formats/png.bdef.yaml

# dd で切り出したデータを解析
dd if=disk.img bs=512 count=1 2>/dev/null | binanalyzer - -f formats/mbr.yaml
```

### バリデーション専用モード（`--quiet`）

`--quiet` / `-q` オプションを指定すると、stdoutへのデコード結果出力を抑制し、終了コードのみを返します。CI/CDでのバイナリ構造検証に適しています。

```bash
# バリデーションのみ実行（結果は終了コードで判定）
binanalyzer image.png -f formats/png.bdef.yaml --quiet
echo $?  # 0=成功, 1=エラー, 2=validate式失敗
```

`--quiet` でもstderrへの警告・エラー出力は行われます。

### JSON形式エラー出力

`--error-format json` を指定すると、stderrへのエラー出力がJSON形式になります。

```bash
# エラーをJSON形式で取得
binanalyzer broken.bin -f formats/png.bdef.yaml --error-format json 2>errors.json
```

JSON出力例:

```json
{"type":"error","message":"ファイルが見つかりません: broken.bin"}
```

```json
{"type":"decode_error","message":"...","offset":123,"offset_hex":"0x0000007B","field_path":"header.magic","field_type":"bytes","hint":"..."}
```

```json
{"type":"validation","errors":[{"code":"VAL001","message":"...","struct":"header","field":"magic"}],"warnings":[]}
```

### パイプ切断ハンドリング

stdout がパイプで接続されている場合、受信側が閉じた（broken pipe）際にクラッシュせず正常終了（exit 0）します。

```bash
# head で最初の1行だけ取得してもクラッシュしない
binanalyzer image.png -f formats/png.bdef.yaml -o json | head -1
```

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

# 2つのファイルの差分比較（フラット形式）
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml

# 2つのファイルの差分比較（ツリー形式）
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml --output tree

# 差分比較に統計サマリーを追加表示
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml --summary

# 統計サマリーのみ表示（詳細差分を省略）
dotnet run --project src/BinAnalyzer.Cli -- diff v1.png v2.png -f formats/png.bdef.yaml --summary-only

# バリデーションをスキップして解析
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --no-validate

# エラーを無視して解析を継続
dotnet run --project src/BinAnalyzer.Cli -- broken.bin -f formats/png.bdef.yaml --on-error continue

# 出力フィルタで特定フィールドのみ表示
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --filter "PNG.chunks.*.type"

# フィルタとCSVを組み合わせてデータ抽出
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o csv --filter "**.width" --filter "**.height"

# フォーマット定義のスキーマ図をMermaid形式で出力
dotnet run --project src/BinAnalyzer.Cli -- schema formats/png.bdef.yaml

# スキーマ図をGraphviz DOT形式で出力
dotnet run --project src/BinAnalyzer.Cli -- schema formats/otf.bdef.yaml -o dot

# 対話型TUIで探索
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o tui

# stdin からデコード（ - 指定）
cat image.png | dotnet run --project src/BinAnalyzer.Cli -- - -f formats/png.bdef.yaml

# stdin からデコード（--stdin オプション）
cat image.png | dotnet run --project src/BinAnalyzer.Cli -- --stdin -f formats/png.bdef.yaml

# バリデーションのみ（quiet モード）
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --quiet; echo $?

# JSON出力をパイプで後処理
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml -o json | jq '.header'

# エラーをJSON形式で出力
dotnet run --project src/BinAnalyzer.Cli -- image.png -f formats/png.bdef.yaml --error-format json 2>errors.json
```
