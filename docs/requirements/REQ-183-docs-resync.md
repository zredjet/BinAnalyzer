# REQ-183: ドキュメントと実装のずれの解消（README / architecture.md / dsl-reference.md）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-24 |

## 背景・動機

ドキュメント間、およびドキュメントと実装の間に記述のずれがある（2026-09-18 に実装・`formats/`・`schemas/` と突き合わせて確認）。

### README.md

| 記述 | 実装 |
|---|---|
| 圧縮データ対応は「zlib / deflate」 | Engine は zlib / deflate / gzip / bzip2 / lzma / zstd / lz4 の 7 種を実装（`FieldType`、BinAnalyzer.Compression）。dsl-reference.md は 7 種を記載済み |
| チェックサムは「CRC-32チェックサム検証」 | 実装は 17 アルゴリズム（crc32 / crc16-ccitt / crc16-ibm / adler32 / crc8 / crc8-maxim / crc8-cdma2000 / crc64-ecma / xxhash32 / xxhash64 / fletcher16 / fletcher32 / md5 / sha1 / sha256 / sha384 / sha512）。dsl-reference.md の対応アルゴリズム表は実装と一致 |

### docs/architecture.md

| 記述 | 実装 |
|---|---|
| `formats/` の一覧が 20 ファイル | 38 ファイル（README の列挙と一致） |
| バリデーションコードは「VAL001〜VAL011: エラー、VAL101〜VAL116: 警告」 | `FormatValidator` のエラーは VAL001〜VAL011・VAL013〜VAL015・VAL017〜VAL019（VAL012 / VAL016 は欠番）と **VAL114 / VAL115**、警告は VAL101〜VAL113・VAL116〜VAL122（2026-09-24 に `Error("VAL…")` / `Warning("VAL…")` の呼び出しから確認） |
| プロジェクト構成・依存関係図に BinAnalyzer.Compression がない。Engine は「BCLのみ」「Engine → Core」 | Engine は Compression と System.IO.Hashing に依存（`BinAnalyzer.Engine.csproj`） |
| tests/ の一覧に Cli.Tests / Compression.Tests / Web.Tests がない | 3 プロジェクトが存在する |
| `DecodedCompressed` は「圧縮データ（zlib/deflate）」 | 7 種の圧縮に対応 |
| Engine の計算器は `Crc32Calculator` のみ | Adler32 / Crc8 / Crc16 / Crc32 / Crc64 / Fletcher / XxHash / Hash の 8 ファイル |

### docs/cli-usage.md

- 76 行目「エラー（VAL001〜VAL011）」「警告（VAL101〜VAL112）」が古い（上記と同じ）

### docs/parser-design.md（設計時に追加）

- 「FormatValidator の検証（マッピング後）」節に検証コードの一覧がある（リポジトリ内で唯一の一覧）が、VAL013 / VAL015 / VAL017〜VAL019 / VAL116〜VAL122 が抜けている。見出しの範囲表記「エラー（VAL001〜VAL014, VAL114〜VAL115）」「警告（VAL101〜VAL113）」も古い

### docs/dsl-reference.md

- 「エラー回復マーカー（resync_marker）」節の例（1873 行目付近）が `expect: "MTrk"` になっている。DSL に `expect` というキーは存在せず、正しくは `expected`（バイト値の整数配列）。`type: ascii` に文字列で指定する形も不正で、`expected: "MTrk"` と書くとデシリアライズ例外になる
- この例は現状、`YamlFormatLoader` が未知キーを無視する設定（`IgnoreUnmatchedProperties`）のため、エラーにならず検証も行われない。`binanalyzer validate` も OK を返す（実測）。ユーザーがこの例をコピーすると「検証したつもりで検証されていない」定義ができる
- 「整数系（CRC-32 / CRC-16 / Adler-32）」の見出しが対応アルゴリズム表（CRC-8 / CRC-64 / xxHash / Fletcher を含む）より狭い。「ハッシュ系（MD5 / SHA-1 / SHA-256）」も SHA-384 / SHA-512 を含まない

指摘のうち「patch や `-o gui` が README に出てこない」は誤りで、README（機能一覧、デスクトップ GUI 節、リリース節）・architecture.md（値の書き戻し節、GUI 節）・cli-usage.md のいずれにも記載がある。本要望の対象外とする。

同種のずれは機能追加のたびに再発している（圧縮 7 種化・チェックサム追加・フォーマット追加・検証コード追加のいずれも README / architecture.md に反映されていない）。手作業の修正に加えて、機械的に検出できるものはテストで固定する。

## 機能要件

### 追加する機能

- [x] ドキュメント整合性テスト（`tests/BinAnalyzer.Integration.Tests`）を追加する。対象:
  - README の「計 N 種」および列挙が `formats/*.bdef.yaml` の実ファイル数・ファイル名と一致すること
  - dsl-reference.md の対応アルゴリズム表のアルゴリズム名とカテゴリ（整数系 / ハッシュ系）が `ChecksumAlgorithms` と一致すること
  - dsl-reference.md の圧縮型一覧が `FieldType` の圧縮系メンバーと一致すること
  - parser-design.md の検証コード一覧（コードと重大度）が `FormatValidator` の実装と一致すること
  - README / docs/*.md に現れる `VALnnn` がすべて実装に存在し、範囲表記（`VALnnn〜VALnnn`）が使われていないこと
  - architecture.md のプロジェクト構成が `BinAnalyzer.slnx` のプロジェクト集合と、依存関係図が各 src プロジェクトの `ProjectReference` と一致すること（設計時に追加）
  - README / docs/*.md 内の YAML コードブロックのフィールド・struct・checksum 定義に、`YamlFieldModel` / `YamlStructModel` / `YamlChecksumModel` が受け付けないキー（`expect` 等）が現れないこと
  - dsl-reference.md の resync_marker の例がそのままロード・デコードでき、`magic` が検証されること
- [x] 上記テストで「文書側の記述」を取り出す方法は設計メモで決める → 機械可読なマーカー（HTML コメント）で範囲を示す。詳細は設計メモ
- [x] Core に圧縮型の判定 `FieldTypeCategories.IsCompressed(FieldType)` を追加し、重複している判定（Engine / Validator / Output の 4 箇所）を置き換える（設計時に追加。テストが照合する「実装」を 1 箇所にするため）
- [x] `ChecksumAlgorithms` の整数系 / ハッシュ系の集合を公開する（設計時に追加）

### 変更する既存機能

- [x] README.md — 機能一覧の「圧縮データ対応」を 7 種に、「条件フィールド / チェックサム」を CRC 系・Adler / Fletcher / xxHash・MD5 / SHA 系を含む記述に更新。フォーマット一覧の文にマーカーを付ける
- [x] docs/architecture.md — `formats/` の列挙をやめて README 参照にする。検証コードの範囲表記を parser-design.md への参照に置き換え、プロジェクト構成・依存関係図に Compression を追加し、Engine の依存記述・tests/ 一覧・`DecodedCompressed` の説明・チェックサム計算器の一覧を直す
- [x] docs/cli-usage.md — 検証コードの範囲表記を parser-design.md への参照に置き換える
- [x] docs/parser-design.md — 検証コード一覧を実装の全コードに更新し、見出しの範囲表記をやめる（設計時に追加）
- [x] docs/dsl-reference.md — resync_marker の例を `type: bytes` + `expected: [0x4D, 0x54, 0x72, 0x6B]` に修正（`formats/midi.bdef.yaml` の `mtrk` と同形）し、単体でロードできる完全な定義にする。整数系・ハッシュ系の見出しを表に合わせる

### 変更しないもの（スコープ外）

- `YamlFormatLoader` の未知キーの扱い（無視 → 警告またはエラー）。設計判断を伴うため別要望とする（候補: 「DSL 未知キーの検出」）。本要望では文書側の誤記のみ直す
- dsl-reference.md / cli-usage.md に検証コードの全件表を新設すること。全件の一覧は既存の parser-design.md の一覧を正とし、他の文書はそこを参照する
- 検証コードのカタログ（コード・重大度・説明の定数表）を Core に新設すること（代替案を参照）
- 文書の構成変更・翻訳・文体統一
- CLAUDE.md のプロジェクト構成の検査（現状は一致している）

## 受入条件

1. [x] README の圧縮・チェックサムの記述が実装と一致し、dsl-reference.md と矛盾しないこと
2. [x] architecture.md の `formats/` の扱い・検証コード・プロジェクト構成・依存関係図・tests 一覧・`DecodedCompressed`・計算器一覧が実装と一致すること
3. [x] cli-usage.md・architecture.md に検証コードの範囲表記が残らず、parser-design.md の一覧が実装の全コード（重大度を含む）と一致すること
4. [x] dsl-reference.md の resync_marker の例をそのまま `.bdef.yaml` として保存し `MTrk` + 長さ + データのバイナリをデコードすると、`magic` に検証結果（✓）が表示されること。`expected` を意図的に変えた場合は ✗ になること
5. [x] ドキュメント整合性テストが追加され、現状の文書で通過すること。`formats/` にファイルを追加して README を更新しない、チェックサムアルゴリズムや圧縮型を実装に追加して文書を更新しない、検証コードを追加して一覧を更新しない、YAML 例に未知キーを書く、といった変更で失敗すること（テスト内でその状況を再現して確認）
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldTypeCategories.IsCompressed` を追加。`ChecksumAlgorithms` の集合を公開。`FormatValidator` のサイズ必須判定を `IsCompressed` で置き換え |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `BinaryDecoder` の圧縮型ディスパッチを `IsCompressed` に置き換え |
| BinAnalyzer.Output | `DotSchemaFormatter` / `MermaidSchemaFormatter` の private な `IsCompression` を `IsCompressed` に置き換え |
| BinAnalyzer.Cli | 変更なし |
| tests/BinAnalyzer.Integration.Tests | ドキュメント整合性テストの追加 |
| tests/BinAnalyzer.Core.Tests | `IsCompressed` のテスト |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — resync_marker の例、整数系・ハッシュ系の見出し、マーカー
- [x] docs/architecture.md — 上記一覧、マーカー
- [x] docs/parser-design.md — 検証コード一覧、マーカー
- [ ] CLAUDE.md — 変更不要（プロジェクト構成には Compression が既に記載済み）
- [x] README.md — 圧縮・チェックサムの記述、マーカー
- [x] docs/cli-usage.md — 検証コードの参照

---

## 設計メモ

### 設計方針

- **照合の単位は「文書の抜き出し」と「実装の集合」の 2 つの純関数に分ける。** 各検査は `(文書テキスト, 実装側の集合) → 不一致メッセージのリスト` の純関数（テスト内の `DocConsistency` 静的クラス）にする。実ファイルに対する `[Fact]` は「リストが空」を確かめ、受入条件 5 の「壊したら落ちる」は同じ関数に書き換えた入力（ファイル名を 1 つ足す、アルゴリズム名を 1 つ足す等）を渡して「リストが空でない」ことで確かめる。リポジトリのファイルを書き換えずに再現できる
- **文書側の範囲は HTML コメントのマーカーで示す。** `<!-- doc-sync: <名前> -->` 〜 `<!-- /doc-sync -->` で囲んだ範囲だけを読む。GitHub のレンダリングには出ない。見出しを正規表現で探す方式と比べ、「この表はテストで照合されている」ことが文書の編集者に見え、マーカーが無い・閉じていないときは「マーカー `checksum-algorithms` が見つからない」と明確に落とせる。範囲内の書式は下表の規則に固定する

  | マーカー | 文書 | 範囲内の抜き出し規則 | 照合先（実装） |
  |---|---|---|---|
  | `formats` | README.md | `（A, B, … — 計N種）` を正規表現で取り、名前の列と N | `formats/*.bdef.yaml` のファイル名（拡張子除く）と件数 |
  | `checksum-algorithms` | dsl-reference.md | 表の行 `` \| `名前` \| カテゴリ \| … `` の 1・2 列目 | `ChecksumAlgorithms.IntegerAlgorithms` / `HashAlgorithms`（カテゴリ「整数系」「ハッシュ系」と対応） |
  | `compression-types` | dsl-reference.md | 範囲内の最初の `（…）` の中のバッククォートのトークン | `FieldType` のうち `IsCompressed` のものの `FieldTypeNames.ToDslName` |
  | `validation-codes` | parser-design.md | `- VALnnn:` の行。直前の `**エラー` / `**警告` 行で重大度を決める | `FormatValidator.cs` の `Error("VALnnn"` / `Warning("VALnnn"`（後述） |
  | `project-tree` | architecture.md | `BinAnalyzer(.X)+` のトークン全部 | `BinAnalyzer.slnx` の `<Project Path>` のプロジェクト名 |
  | `project-dependencies` | architecture.md | `A → B, C（+ …）` の行。`（…）` を除いて `,` で分割、`（なし）` は空 | src の各 `.csproj` の `ProjectReference`（直接参照のみ） |
  | `resync-marker-example` | dsl-reference.md | 範囲内の最初の ```` ```yaml ```` ブロック | `YamlFormatLoader.LoadFromString` でロードしデコード |

- **README のフォーマット名は表示名のまま照合する。** 「Java Class」「Mach-O」「X.509」のような表示名は読みやすさのために残し、小文字化して英数字以外を除いた形（`javaclass` / `macho` / `x509`）でファイル名と比べる。これで合わないのは MessagePack（`msgpack`）だけなので、テスト内に別名表を 1 行置く
- **検証コードは parser-design.md の一覧 1 箇所を正とする。** 範囲表記は欠番（VAL012 / VAL016）も、1xx 番台のエラー（VAL114 / VAL115）も表せない。architecture.md / cli-usage.md は範囲をやめて「コード一覧は parser-design.md を参照」にし、既存の一覧（唯一の全件に近い一覧）を全コードに更新して照合対象にする。加えて、README / docs/*.md（`docs/requirements/` を除く）に現れる `VALnnn` はすべて実装に存在すること、`VALnnn〜VALnnn` の範囲表記が無いことを文書全体で検査する（dsl-reference.md の節ごとの言及や VAL017 / VAL117 の表で、存在しないコードを書いた場合も拾える）
- **検証コードの実装側はソースを読む。** `FormatValidator` はコードを文字列リテラルで `Error("VAL001", …)` / `Warning("VAL101", …)` に渡している（44 箇所の `Error(` / `Warning(` のうち 42 が呼び出し、2 が定義）。テストは `src/BinAnalyzer.Core/Validation/FormatValidator.cs` を読んで `\b(Error|Warning)\("(VAL\d{3})"` を集める。書き方が変わって抜き出しが漏れるのを防ぐため、「`Error(` / `Warning(` の出現数 = リテラル付き呼び出し数 + 定義 2」と「同じコードが両方の重大度に出ない」も確かめる
- **YAML 例の未知キーはキー集合を反射で得て汎用走査する。** README / docs/*.md の ```` ```yaml ```` ブロックを YamlDotNet の `YamlStream`（表現モデル）で読み、ノードを再帰的に走査して、`name` とスカラーの `type` を両方持つマッピングをフィールド定義とみなしてキーを `YamlFieldModel` の `[YamlMember(Alias)]` 集合と比べる。その `checksum:` の値は `YamlChecksumModel`、`structs:` の各値（マッピングの場合）は `YamlStructModel` と比べる。bitfield のエントリ（`bits`）・flags のフィールド（`bit`）は `type` を持たないので対象外になる。試作（2026-09-24）では dsl-reference.md の 91 ブロック・parser-design.md の 3 ブロックがすべてパースでき、検出は resync_marker の `expect` 1 件だけだった（`formats/` の全定義にも同じ走査をかけて 0 件）
- **resync_marker の例は単体でロードできる形にする。** `name` / `endianness: big` / `root: midi_track` を足し、`magic` を `type: bytes` / `size: "4"` / `expected: [0x4D, 0x54, 0x72, 0x6B]` にする（`formats/midi.bdef.yaml` の `mtrk` と同形）。テストはマーカー範囲の YAML をそのままロードし、`MTrk` + 長さ 3 + 3 バイトのデータをデコードして `magic` の `ValidationPassed == true`、YAML の `0x6B` を `0x6C` に置き換えたものでは `false` を確かめる（受入条件 4）
- **圧縮型の判定を Core に 1 つ置く。** 「どの `FieldType` が圧縮か」は Engine のディスパッチ・Validator のサイズ必須判定・Dot / Mermaid の `(圧縮)` 表示に 4 回書かれている。テストが照合する実装をこの 1 箇所にするため `FieldTypeCategories.IsCompressed` を追加し、4 箇所を置き換える。Engine のディスパッチもこれを使うので、判定に型を足し忘れればデコード自体が動かず既存テストで気づく
- **README の本文に件数を書かない。** チェックサム・圧縮の記述は種類名を挙げるにとどめ「計 17 種」のような数は書かない（照合対象でない数はまたずれる）。件数を書くのは照合しているフォーマット一覧だけにする

### モデル変更

- `ChecksumAlgorithms`（Core）: private の `HashSet<string> IntegerAlgorithms` / `HashAlgorithms` を `public static IReadOnlySet<string> IntegerAlgorithms { get; }` / `HashAlgorithms { get; }` にする（中身・比較子は同じ）。`IsIntegerAlgorithm` / `IsHashAlgorithm` / `IsKnown` はそのまま
- `FieldTypeCategories`（Core/Models、新規）: `public static bool IsCompressed(FieldType type)`（Zlib / Deflate / Gzip / Bzip2 / Lzma / Zstd / Lz4）
- 置き換え箇所: `BinaryDecoder.DecodeSingleField` の圧縮アーム（`var t when FieldTypeCategories.IsCompressed(t) =>`）、`FormatValidator` のサイズ必須型の条件、`DotSchemaFormatter.IsCompression` / `MermaidSchemaFormatter.IsCompression`（削除）。`BinaryDecoder` 689 行付近の「BCL で展開する 3 種」の判定は意味が違うので残す

### インタフェース変更

- なし（CLI・DSL・出力の挙動は変わらない）
- テスト: `tests/BinAnalyzer.Integration.Tests/DocumentationConsistencyTests.cs`（実ファイルの照合と、書き換えた入力での失敗の確認）と、抜き出し・照合の純関数 `DocConsistency.cs`。リポジトリのルートは既存の `JsonSchemaTests` と同じく `AppContext.BaseDirectory` から 5 階層上。Category は付けない（CI の通常のテストステップで走る）

### 代替案

- 整合性テストを置かず、文書修正のみで済ませる案。今回のずれは複数の機能追加にまたがって蓄積しており、再発防止がないと同じ指摘を繰り返すため不採用
- 文書の該当箇所をコードから生成する案（フォーマット一覧・アルゴリズム表を生成物にする）。生成の仕組みが増えるわりに対象が少ないので、まずは検証のみとする
- 検証コードの範囲表記を正しい範囲に書き直すだけの案。欠番と 1xx 番台のエラー（VAL114 / VAL115）を表せず、ドラフトの記述（「VAL001〜VAL019 エラー、VAL101〜VAL122 警告」）自体がこの理由で誤っていた。不採用
- 検証コードのカタログ（コード・重大度・概要の定数表）を Core に置き、`FormatValidator` と文書の両方がそれを参照する案。ソース走査より頑健だが 42 箇所の呼び出しを書き換える。`validate --list-codes` のような利用者が出てきたときに行う
- 見出し（`## チェックサム` など）を手がかりに正規表現で節を探す案。見出しの改名で落ちる点はマーカーと同じだが、どこが照合されているかが文書から見えない。不採用
- `YamlFormatLoader` を厳格モード（未知キーでエラー）にして YAML 例を実際にロードする案。例の大半はフィールドだけの断片で単体ではロードできず、ローダーの挙動変更はスコープ外。不採用

### 懸念事項

- 文書を正規表現で読むテストは、文書の書き方が変わると壊れやすい。マーカー範囲内の書式（上の表の抜き出し規則）を変えるときはテストも直す必要がある。マーカーの直後に「この一覧はテストで照合しています」等の注記は置かない（HTML コメント自体が目印になる）
- YAML コードブロックはすべてパースできる必要がある。意図的に不正な例を載せる場合は ```` ```text ```` 等の別のフェンスにする
- `name` + `type` を持つマッピングをフィールドとみなす判定は、将来 `type` を持つ別種の定義（例: flags に型指定）が入ると誤検出する。その時点で走査の文脈（親キー）を見るようにする
- 検証コードのソース走査は `Error("VALnnn"` の書き方に依存する。書き方が変わった場合は出現数のガードで落ちるので、その時点でカタログ案に移る
- 依存関係図は「直接参照」を書く規則にするため、Gui / Web の行に Core を足す（現状は推移的に得られる Core を省いている）

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

- **`compression-types` の抜き出し規則。** 段落を分けずにマーカーで囲めるよう、「範囲内のバッククォート全部」から「範囲内の最初の `（…）` の中のバッククォート」に変えた（同じ段落の `` `struct` `` を拾わないため）。設計メモの表も更新済み
- **`tests/BinAnalyzer.Cli.Tests` を `BinAnalyzer.slnx` に追加した。** プロジェクト構成の照合テストで、このプロジェクトがソリューションに入っておらず、`dotnet test` でも CI でも一度も実行されていなかったことが分かった（2026-09-24、ユーザー確認のうえ追加）。追加に合わせて `PipelineTests` の入力を `.gitignore` 対象の `testdata/real/test.png` からコミット済みの `testdata/smoke/sample.png` に変えた（CI には real が無く、そのままでは失敗する）。Debug の成果物と `testdata/real` を消し、CI と同じ `--no-build --configuration Release` で 28 件の通過を確認した。CI のテスト時間は約 50 秒延びる
- **parser-design.md の VAL104 の説明を実装に合わせた。** 「ascii型以外」→「ascii型・整数型以外」（`FormatValidator` は整数型も許す）
- **欠番の注記はコード表記を使わない。** 「VAL012 / VAL016 は欠番」と書くと「文書中の VALnnn は実装に存在する」検査に掛かるため、「012 番と 016 番は欠番」とした

### 追加したテスト

`tests/BinAnalyzer.Integration.Tests/DocumentationConsistencyTests.cs`（照合ロジックは同じディレクトリの `DocConsistency.cs`）と `tests/BinAnalyzer.Core.Tests/FieldTypeCategoriesTests.cs`。

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DocumentationConsistencyTests | Readme_FormatList_MatchesFormatsDirectory | 5 |
| DocumentationConsistencyTests | Readme_FormatList_DetectsFormatAddedWithoutReadmeUpdate / DetectsListedFormatWithoutFile | 5 |
| DocumentationConsistencyTests | DslReference_ChecksumTable_MatchesImplementation | 1, 5 |
| DocumentationConsistencyTests | DslReference_ChecksumTable_DetectsAlgorithmAddedWithoutDocUpdate / DetectsWrongCategory | 5 |
| DocumentationConsistencyTests | DslReference_CompressionTypes_MatchImplementation | 1, 5 |
| DocumentationConsistencyTests | DslReference_CompressionTypes_DetectsTypeAddedWithoutDocUpdate | 5 |
| DocumentationConsistencyTests | ParserDesign_ValidationCodes_MatchFormatValidator | 3, 5 |
| DocumentationConsistencyTests | ParserDesign_ValidationCodes_DetectsCodeAddedWithoutDocUpdate / DetectsSeverityMismatch | 5 |
| DocumentationConsistencyTests | ValidatorCodeExtraction_KnowsErrorsInThe1xxRange / DetectsNonLiteralCall | 3, 5 |
| DocumentationConsistencyTests | Docs_ValidationCodeMentions_ExistAndNoRanges | 2, 3 |
| DocumentationConsistencyTests | Docs_ValidationCodeMentions_DetectsUnknownCodeAndRange | 5 |
| DocumentationConsistencyTests | Architecture_ProjectTree_MatchesSolution | 2 |
| DocumentationConsistencyTests | Architecture_ProjectTree_DetectsProjectAddedWithoutDocUpdate | 5 |
| DocumentationConsistencyTests | Architecture_Dependencies_MatchProjectReferences | 2 |
| DocumentationConsistencyTests | Architecture_Dependencies_DetectsReferenceAddedWithoutDocUpdate | 5 |
| DocumentationConsistencyTests | Docs_YamlExamples_UseOnlyKnownKeys | 4, 5 |
| DocumentationConsistencyTests | Docs_YamlExamples_DetectsUnknownKeys / IgnoreBitfieldAndFlagsEntries | 5 |
| DocumentationConsistencyTests | DslReference_ResyncMarkerExample_ValidatesMagic | 4 |
| DocumentationConsistencyTests | DslReference_ResyncMarkerExample_FailsWhenExpectedChanged | 4 |
| FieldTypeCategoriesTests | IsCompressed_ExactlyTheSevenCompressionTypes / IsCompressed_FalseForNonCompressionTypes | 1 |

### 気づき・今後の課題

- `YamlFormatLoader` が未知キーを無視するため、`expect` のようなタイプミスが `validate` でも検出できない。別要望の候補
- 設計時の走査で、`formats/*.bdef.yaml` の全定義に未知キーは無いことを確認した（2026-09-24）。上記の別要望で厳格化しても既存定義は壊れない見込み
