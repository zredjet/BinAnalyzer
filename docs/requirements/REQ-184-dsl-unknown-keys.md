# REQ-184: DSL の未知キーの検出

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | REQ-172（定義元の行番号）、REQ-183（ドキュメント整合性テスト） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

`YamlFormatLoader` は YamlDotNet の `IgnoreUnmatchedProperties()` で読み込んでおり、DSL に存在しないキーは黙って捨てられる。キーの書き間違いはエラーにも警告にもならない。

2026-09-24 の実測: 次の定義を `binanalyzer validate` にかけると `OK`（終了コード 0）になる。`expected` の検証も `repeat_count` の繰り返しも行われない。

```yaml
name: t
root: s
structs:
  s:
    fields:
      - name: magic
        type: bytes
        size: "4"
        expect: [0x4D]      # expected の書き間違い
        repeat_cout: 3      # repeat_count の書き間違い
```

REQ-183 では、dsl-reference.md の resync_marker の例がまさにこの状態（`expect: "MTrk"`）で、「検証したつもりで検証されていない」定義を利用者にコピーさせていた。フォーマット定義を書くことがこのツールの中心なので、書き間違いを気づかせる仕組みがないのは影響が大きい。

- JSON Schema（`schemas/bdef.schema.json`）は全定義で `additionalProperties: false` なので、Schema を設定したエディタでは気づける。CLI・GUI・Web で読む場合や、Schema を使わない利用者は気づけない
- 同梱の `formats/*.bdef.yaml`（`common/` を含む）に未知キーは 0 件（REQ-183 の設計時に確認）。検出を足しても同梱定義は壊れない

## 機能要件

### 追加する機能

- [x] ローダーが YAML の全階層で未知キーを集める。対象はトップレベル・`imports` の要素・enum のエントリ・flags 定義とそのフィールド・struct 定義・フィールド・`checksum`・`range` / `ranges` の要素・bitfield のエントリ。キーの一覧は YAML DTO（`YamlFormatModel` 等）の `[YamlMember(Alias)]` から得る（DTO にキーを足せば自動で「既知」になる）
- [x] 新しい検証コード **VAL123（警告）**「未知のキー」。メッセージにキー名・場所（どの struct のどのフィールドか等）・定義元の `file:line` を含める。受け付けるキーの中に近いもの（編集距離 2 以内）があれば「もしかして `expected`?」と候補を添える
- [x] インポート先のファイルの未知キーは、インポート先のファイル名と行で報告する
- [x] JSON Schema と YAML DTO のキー集合が一致することをテストで固定する（Schema の各定義の `properties` と、対応する DTO の Alias 集合）

### 変更する既存機能

- [x] `FormatValidator` — IR が持つ未知キーの一覧から VAL123 を出す
- [x] CLI — 既存の診断表示に VAL123 が加わる（`validate` は警告として表示し終了コード 0、`--warnings-as-errors` で 1。デコード時の事前検証でも stderr に警告が出て解析は続く）。CLI は `validate --format json` に `file` / `line` を足した（実装メモ参照）
- [x] docs/parser-design.md の検証コード一覧に VAL123 を追加。docs/dsl-reference.md に「未知のキーは VAL123 の警告になる」旨を追記

### 変更しないもの（スコープ外）

- 未知キーをエラー（読み込み失敗）にすること。警告にとどめる（設計メモの代替案を参照）
- 値の型の誤り（`size: [1, 2]` など）の検出方法の変更。現状どおり YamlDotNet のデシリアライズ例外になる
- GUI / Web で検証の警告を表示すること（現状も検証の警告は出していない）
- `--strict` のような新しい CLI オプション（`--warnings-as-errors` で足りる）

## 受入条件

1. [x] 背景の定義を `binanalyzer validate` にかけると、`expect` と `repeat_cout` の 2 件の VAL123 警告が `file:line` 付きで出て、それぞれ `expected` / `repeat_count` の候補が示されること。終了コードは 0、`--warnings-as-errors` では 1
2. [x] 対象の各階層（トップレベル、imports、enum エントリ、flags とそのフィールド、struct、フィールド、checksum、range、bitfield エントリ）に置いた未知キーがそれぞれ検出されること。struct の旧形式（フィールドのリストを直接書く形）でも検出されること
3. [x] インポート先の未知キーがインポート先のファイル名と行で報告されること
4. [x] `formats/*.bdef.yaml` と `formats/common/*.bdef.yaml` の全定義で VAL123 が 0 件であること
5. [x] JSON Schema の各定義と YAML DTO のキー集合が一致すること。どちらか一方にだけキーを足すとテストが失敗すること
6. [x] 既知のキーだけの定義では、読み込み結果（IR）と既存の診断が変わらないこと（既存テスト全通過で確認）
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過。REQ-183 のドキュメント整合性テストを含む）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FormatDefinition.UnknownKeys`、`DslUnknownKey` レコードの追加。`FormatValidator` に VAL123 |
| BinAnalyzer.Dsl | 未知キーの走査（`UnknownKeyScanner`）、`YamlFormatLoader` から呼び出し、インポートのマージ、`YamlToIrMapper` で IR へ |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | `validate --format json` の診断に `file` / `line` を追加（実装時に追加。下記） |
| tests/BinAnalyzer.Dsl.Tests | 走査・候補・インポートのテスト |
| tests/BinAnalyzer.Core.Tests | VAL123 のテスト |
| tests/BinAnalyzer.Integration.Tests | 同梱定義で 0 件、Schema と DTO の一致 |
| tests/BinAnalyzer.Cli.Tests | `validate` の終了コードと表示 |

### 変更が必要なドキュメント

- [x] docs/parser-design.md — 検証コード一覧に VAL123
- [x] docs/dsl-reference.md — 未知キーの扱い
- [x] docs/architecture.md — Dsl の説明に一文（未知キーの走査）
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要（`validate` の説明の範囲内）

---

## 設計メモ

### 設計方針

- **デシリアライズとは別に、表現モデル（`YamlStream`）を DTO の型に沿って走査する。** YamlDotNet の `IgnoreUnmatchedProperties` は「捨てる」か「例外」かの二択で、捨てたキーを知る手段がない。未知キーを例外にせず集めるため、同じ YAML を `YamlStream` でも読み、`YamlFormatModel` から型をたどって歩く:
  - DTO（`[YamlMember]` を持つクラス）× マッピング → 各キーを Alias 集合と比べ、無ければ未知キーとして記録、あれば値をそのプロパティの型で再帰
  - `List<T>` × シーケンス → 各要素を `T` で再帰
  - `Dictionary<string, T>` × マッピング → 各値を `T` で再帰（キーは struct 名・enum 名などの利用者の名前なので検査しない）
  - `YamlStructModel` × シーケンス → 旧形式として `List<YamlFieldModel>` で再帰（`StructNodeDeserializer` と同じ規則）
  - それ以外（`string` / `int` / `object` / `Dictionary<string, string>` の値）→ 止まる
  型の対応を反射で得るので、DTO にプロパティを足せば走査も追従する。REQ-183 の文書用の走査（`name` + `type` を持つマッピングをフィールドとみなす推測）とは違い、型が分かっているので推測は要らない
- **未知キーは IR に載せて、診断は `FormatValidator` が出す。** 未知キーはローダーでしか分からないが、診断の出し方（コード・重大度・`--warnings-as-errors`・JSON 出力・`file:line`）は検証器にそろっている。`FormatDefinition` に未知キーの一覧を持たせ、`FormatValidator` が VAL123 に変換する。CLI の `validate` / デコード前検証 / `patch` / Web がそのまま拾う
- **場所の表現。** 各未知キーに、キー名・定義元の `SourceFile` / `SourceLine`（キーのスカラーの行）・文脈（`struct 's' のフィールド 'magic'`、`flags 'chunk_type_flags' のフィールド 'ancillary'`、`トップレベル` 等）・受け付けるキーの集合（候補計算用）を持たせる。struct 名とフィールド名が分かる場合は `ValidationDiagnostic.StructName` / `FieldName` にも入れる（JSON 出力の `struct` / `field`）
- **候補（もしかして）。** 同じ文脈で受け付けるキーのうち、編集距離（Levenshtein、大文字小文字を無視）が最小で 2 以内のものを 1 つ示す。同点は Alias の定義順で先のもの。`expect` → `expected`（1）、`repeat_cout` → `repeat_count`（1）
- **警告にする（エラーにしない）。** 利用者が手元に持つ定義は同梱定義と違って確認できない。エラーにすると、今まで読めていた定義が次のバージョンで読めなくなる。警告なら表示で気づけ、CI では既存の `--warnings-as-errors` で止められる
- **インポート。** `Deserialize(yaml, sourceFile)` がファイルごとに走査するので、インポート先の未知キーはインポート先の識別子を持つ。`MergeDefinitions` で struct 等と一緒に本体側へ足す
- **Schema と DTO の一致をテストで固定する。** 未知キーの判定基準（DTO）とエディタの判定基準（Schema）がずれると、「エディタでは赤いのに CLI は黙る」、またはその逆になる。Schema のトップレベルと `$defs` の 9 定義（トップレベル・`import`・`enumEntry`・`flagsDefinition`・`flagField`・`structDefinition`・`field`・`checksum`・`checksumRange`・`bitfieldEntry`）と DTO の対応表をテストに置き、`properties` の名前集合と Alias 集合の一致を確かめる

### モデル変更

- Core: `public sealed record DslUnknownKey(string Key, string Context, string? StructName, string? FieldName, string? Suggestion, string? SourceFile, int? SourceLine)`（Core/Models）
- Core: `FormatDefinition.UnknownKeys: IReadOnlyList<DslUnknownKey>`（既定は空。`init`）
- Dsl: `YamlFormatModel` に `[YamlIgnore] List<DslUnknownKey> UnknownKeys`（ローダーが走査結果を入れ、マージで連結、マッパーが IR へ）

### インタフェース変更

- `IFormatLoader` の変更なし
- 新しい診断コード VAL123（警告）。メッセージ例: `struct 's' のフィールド 'magic' の未知のキー 'expect' は無視されます（もしかして 'expected'?）`

### 代替案

- **未知キーをエラーにする（`IgnoreUnmatchedProperties` を外す）。** 実装は 1 行だが、最初の 1 件で例外になり全件を示せない。行番号・候補も付けにくく、既存の利用者定義を一斉に読めなくする。不採用
- **YamlDotNet の `ObjectNodeDeserializer` を置き換え、デシリアライズ中に未知キーを記録する。** 読み込みが 1 回で済むが、YamlDotNet の内部（型インスペクタ・命名規則・`required` の扱い）を再実装することになり、バージョン更新に弱い。フォーマット定義は小さい（最大でも数十 KB）ので 2 回読む費用は小さい。不採用
- **JSON Schema で検証する。** Schema は既にあり厳密だが、Engine / Dsl に JSON Schema の検証器（外部依存）を持ち込むことになり、行番号の対応付けも別途要る。不採用（Schema は DTO との一致テストで追従させる）
- **CLI に `--strict` を足す。** 既存の `--warnings-as-errors` と役割が重なる。不採用

### 懸念事項

- 読み込みが YAML を 2 回パースする。同梱の最大の定義（`java-class.bdef.yaml`、11.7 KB）で `YamlStream.Load` は 1 回約 1.5 ms（設計時に計測、Release、ウォームアップ後 50 回の平均）なので増分は無視できる。走査を含めた増分は実装時に測り直して実装メモに記録する
- 背景の実測に使った `expect` / `repeat_cout` はどちらも「値を黙って無視」になる例。`size` のような必須寄りのキーの書き間違いは VAL007（サイズ未指定）などの既存の検証にも掛かるので、VAL123 と重なって 2 件出ることがある（そのままにする。原因が分かる VAL123 の方が有用）
- 「キーは利用者の名前」の辞書（`structs` / `enums` / `flags` / `cases`）を誤って検査しないこと。反射で `Dictionary<string, T>` を見たらキーは検査しない規則で担保する
- `object` 型のプロパティ（`string_table`、`diff_key`）の中身は検査しない。`string_table` の構造体側の書式が DTO 化されていないため
- Schema と DTO の一致テストは、Schema に DTO に無い定義（`$defs` の共通部品）が増えると対応表の更新が要る。対応表に無い定義は失敗にして気づかせる

---

## 実装メモ

### 実装中の設計変更

- **`validate --format json` の診断に `file` / `line` を足した。** デコード時の `--error-format json`（`CliErrorReporter`）は REQ-172 で `file` / `line` を出すようになっていたが、`validate` の JSON（`Program.cs`）には入っていなかった。VAL123 は行番号が無いと使えないので、同じキー名で追加した（cli-usage.md も更新）
- **場所の文言。** トップレベル直下のリスト（`imports`）は「トップレベル の 1 番目のインポート」ではなく「1 番目のインポート」とする（`Location.Within`）。英字・記号で終わる場所の後だけ「の」の前に空白を入れる（`struct 's' のフィールド 'magic' の未知のキー` / `トップレベルの未知のキー`）
- **`FormatValidator.WithLocation` は、既に行を持つ診断を上書きしない。** VAL123 はフィールドの行ではなくキーの行を指すため
- **`UnknownKeyScanner` は public。** ローダー経由だけでなく、同梱定義の検査テスト（`formats/common/` は単体では読み込めないものがあるため走査だけを呼ぶ）からも使う
- **`ValidateCommandTests` の `--no-build` を外した。** CLI テストは同じコレクションで順に走るが、このクラスだけ `dotnet run --no-build`（Debug）で、他クラスが先に Debug ビルドしている前提になっていた。テストの追加で実行順が変わると CI（Release ビルドのみ）で落ちうるので、他クラスと同じくビルド付きにした
- **REQ-183 の否定テストの架空コードを `VAL123` → `VAL999` に変えた。** 「実装に無いコード」の例として `VAL123` を使っていたため、本要望で実在のコードになった

### 読み込み時間（Release、ウォームアップ後 100 回の平均、2026-09-24）

| 定義 | `Load` 全体 | うち未知キーの走査 |
|---|---|---|
| java-class.bdef.yaml（11.7 KB、最大） | 5.33 ms | 2.03 ms（38%） |
| png.bdef.yaml | 3.14 ms | 1.10 ms（35%） |
| wav.bdef.yaml（imports あり） | 1.56 ms | 0.31 ms（20%） |

割合は大きいが絶対値は数 ms で、CLI の起動（数百 ms）に比べて無視できる。YAML を 1 回だけ読む形（表現モデルからデシリアライズする）にすれば削れるが、YamlDotNet に表現モデルを入力にする公開 API が無いので見送る

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Dsl.Tests / UnknownKeyTests | Typos_AreCollectedWithLineAndSuggestion | 1 |
| Dsl.Tests / UnknownKeyTests | UnknownKey_IsCollectedAtEveryLevel（11 ケース: トップレベル・enum エントリ・flags・flags フィールド・struct・フィールド・checksum・range・ranges 要素・bitfield エントリ・旧形式のフィールド） | 2 |
| Dsl.Tests / UnknownKeyTests | ImportedFile_UnknownKeys_ReportImportedFileAndLine（imports 要素の未知キーも含む） | 2, 3 |
| Dsl.Tests / UnknownKeyTests | KnownKeysOnly_NoUnknownKeys / UserNamedKeys_AreNotChecked / UnknownKeys_DoNotChangeTheDecodedDefinition | 6 |
| Dsl.Tests / UnknownKeyTests | FarFromAnyKnownKey_NoSuggestion / Suggestion_IsCaseInsensitive | 1 |
| Core.Tests / UnknownKeyValidationTests | VAL123_UnknownKey_ReportsWarningAtKeyLineWithSuggestion / VAL123_WithoutSuggestion_OmitsHint / NoUnknownKeys_NoVAL123 | 1 |
| Integration.Tests / UnknownKeyIntegrationTests | BundledDefinition_HasNoUnknownKeys（`formats/` と `formats/common/` の全 40 ファイル） | 4 |
| Integration.Tests / UnknownKeyIntegrationTests | JsonSchema_KeysMatchYamlDtos / JsonSchema_DetectsKeyAddedOnlyToDto / JsonSchema_DetectsKeyAddedOnlyToSchema | 5 |
| Cli.Tests / ValidateCommandTests | UnknownKeys_ReportVAL123WithLineAndSuggestion / UnknownKeys_WarningsAsErrors_ExitCode1 / UnknownKeys_JsonHasFileAndLine | 1 |
| Integration.Tests / DocumentationConsistencyTests（既存） | ParserDesign_ValidationCodes_MatchFormatValidator（VAL123 の一覧記載を確認） | 7 |

### 気づき・今後の課題

- GUI / Web では検証の警告を表示していない（Web はエラーのみ、GUI は検証自体を呼ばない）。定義を GUI で切り替えて使う利用者は VAL123 に気づけないので、GUI のステータスバー等に警告件数を出すのは別要望の候補
- `string_table` / `diff_key` は `object` 型のため中身の未知キーは検出しない
