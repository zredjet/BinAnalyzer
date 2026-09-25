# フォーマット定義の書き方

同梱のフォーマット定義（`formats/*.bdef.yaml`、`formats/common/*.bdef.yaml`）を書くときの規約（REQ-188）。DSL の構文そのものは [dsl-reference.md](dsl-reference.md) を参照。

規約のうち機械的に確かめられるもの（先頭コメントの 2 行、全フィールドの日本語の description）は `FormatDefinitionQualityTests` が検査する。見直しが済んでいない定義はテストの「対応待ち一覧」に載っていて、一覧から外した定義と新しく追加する定義は規約を満たす必要がある。

## 先頭コメント

ファイルの先頭に、次の順でコメントを書く。`# 仕様:` と `# 対応していないもの:` の 2 行は必須（テストで検査する）。

```yaml
# XZ 圧縮ファイル（.xz）
# 仕様: The .xz File Format 1.2.1 https://tukaani.org/xz/xz-file-format.txt
# 構造: Stream Header → Block* → Index → Stream Footer。
#   Block Header は圧縮後サイズを省略できるため、末尾の Stream Footer と Index を先に読んで各 Block のサイズを求める。
# 対応していないもの: 複数ストリームの連結とストリームパディング、圧縮データの展開（Check の検証を含む）
name: XZ
```

| 行 | 書くこと |
|---|---|
| 1 行目 | フォーマット名と主な拡張子 |
| `# 仕様:` | 準拠する仕様の名称・版（または発行年）・RFC / ISO 番号・URL。複数あれば `、` で区切る。有料で参照できなかった仕様は、代わりに参照した資料（公開の草案・主要な実装のソース）を書く |
| `# 構造:` | 全体の構造と、読み方に工夫がある箇所（末尾から読む、switch で振り分ける等）。複数行にしてよい（2 行目以降は `#   ` で字下げ） |
| `# 対応していないもの:` | 仕様にあるが読まないもの（古い版・拡張・圧縮の展開・テキスト構文など）と、その理由が自明でなければ理由。無ければ `なし` |

## description

すべてのフィールドに日本語の `description` を付ける（テストで検査する）。

- **仕様書を開かなくても意味が分かる**ように書く。単位、値の範囲、計算方法（「(値 + 1) × 4 バイト」）、他のフィールドとの関係（「`record_count` 個の Index Record」）を書く
- 仕様の用語は原語を括弧で添えてよい（「圧縮後サイズ（Compressed Size）」）。フィールド名の言い換えだけの説明（`width` に「幅」）は避け、何の幅か・単位は何かまで書く（「画像の幅（ピクセル）」）
- 予約フィールド・パディング・内部の補助フィールド（VINT の残りバイト等）も短く書く（「予約（0）」「長さを示す先頭バイト」）
- `virtual` は、何を計算した値かを書く
- 値の検証（`expected` / `validate`）があるフィールドは、何を検証しているかが分かるように書く（「ヘッダマジック（FD '7zXZ' 00）」）

## enum

- ラベルは仕様の名前をそのまま使う（`LZMA2`、`CRC64`、`ET_EXEC`）。記号は `_` に置き換えてよい
- 名前から意味が分からない値には `description` を付ける（「`BCJ_x86`: x86 の分岐命令を変換するフィルタ（実行ファイルの圧縮率を上げる）」）
- 仕様に列挙された値は、使われることが少なくても省かない。よく使われる値だけに絞る場合は、先頭コメントの「対応していないもの」に書く

## 命名

- struct 名・フィールド名は `snake_case`。仕様のフィールド名があればそれに合わせる（`e_machine`、`unpadded_size`）
- ルート struct はフォーマット名（`xz`、`png`）。ルートを最初に書き、あとは読む順に並べる
- 共通ライブラリ（`formats/common/`）の struct 名がインポート側と重なるとエラーになる（REQ-155）。役割の分かる名前（`container_box`、`ftyp_box`）にし、インポート側では同じ名前を使わない。ライブラリのルートはダミーの struct（`_riff_common`）でよい（到達不能でも VAL109 の対象外。REQ-186）

## 再帰する struct

同じ struct を入れ子にする定義（ポインタの先の名前・入れ子の値・TLV など）は、struct に `scope: isolated` を付ける（REQ-195）。付けないと、入れ子の値が同じ名前で親の値を上書きし、後ろの `if:` や virtual が入れ子の値で評価される。入れ子の値は、メンバーアクセス（`{child.field}`）で引く。

## テストデータと登録先

新しい定義を追加するとき、または定義に構造を足したときは、次をそろえる。

- [ ] テストデータの Generator: `tests/BinAnalyzer.Integration.Tests/<Format>TestDataGenerator.cs`。足した構造を含む最小のファイルを組み立てる。可能なら実在のツール（`xz`、`ffmpeg`、`python3` の標準ライブラリ等）で、生成したファイルがそのツールで読めることを確かめる
- [ ] 解析テスト: `tests/BinAnalyzer.Integration.Tests/<Format>ParsingTests.cs`。主要な値・チェックサムの ✓・enum のラベルを確かめる
- [ ] ファズ: `tests/BinAnalyzer.Fuzz.Tests/FormatCatalog.cs` の `Samples` に Generator を登録
- [ ] 実ファイルとゴールデン: `RealFileFixture`（`EnsureFile`）、`RealFileValidationTests`、`GoldenFileTests` に登録し、`UPDATE_GOLDEN=1 dotnet test --filter Category=Golden` でゴールデンファイルを作る（差分が意図したものだけか確認する）
- [ ] 新しい定義のとき: README のフォーマット一覧（件数を含む。`DocumentationConsistencyTests` が照合）と、GUI / Web が拡張子から選ぶ `src/BinAnalyzer.Web/wwwroot/formats/format-list.json`（`FormatListConsistencyTests` が照合）
- [ ] `binanalyzer validate` で警告 0 件（`BundledDefinitionValidationTests`）
