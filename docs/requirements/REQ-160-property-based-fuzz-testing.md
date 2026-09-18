# REQ-160: プロパティベーステスト・ファズテスト

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-23 |
| 更新日 | 2026-09-18 |

## 背景・動機

現在のテストスイート（1,326テスト）はユニットテスト・統合テスト・ゴールデンテストで構成されているが、すべて手動で設計した入力に基づいている。ランダム入力やエッジケースの自動生成によるテストが存在しない。

バイナリデコーダーは任意のバイト列を入力として受け取るため、不正入力・切り詰め入力・巨大入力等でクラッシュ・無限ループ・メモリ枯渇が発生しないことを自動的に検証する仕組みが必要である。

## 機能要件

### 追加する機能

- [x] ファズテスト — ランダムバイト列を各フォーマット定義でデコードし、クラッシュ・無限ループがないことを検証 — `RandomInputFuzzTests`（純ランダム / 0x00・0xFF に偏らせたもの、空入力と 1 バイト全 256 通り）
- [x] プロパティベーステスト — 「デコードは常に有限時間で完了する」「例外は適切にキャッチされる」等の不変条件 — `DecodePropertyTests` と `TreeInvariants`（下記）
- [x] タイムアウト付き実行（無限ループ検出） — `FuzzRunner` が 1 回のデコードを専用スレッドで走らせ、既定 5 秒で打ち切る
- [x] 切り詰め入力テスト — 有効なバイナリファイルを1バイトずつ短縮してデコードし、クラッシュしないことを検証 — `TruncationFuzzTests`（全プレフィックス）。加えて有効サンプルへの変異（バイト書き換え・ビット反転・境界値注入・追記・複製・削除・入れ替え）を `MutationFuzzTests` で行う

### 変更する既存機能

- [x] なし（要望時点）。実際にはファズが見つけたエンジンの不具合 6 件と PE フォーマット定義の不備を直した（実装メモ参照）

### 変更しないもの（スコープ外）

- エンジン内部のエラーハンドリング改善（別途対応） — 見つかった不具合のうち「壊れた入力でクラッシュ・ハングする」ものは本要望で直した（直さないと受入条件 2・3 を満たせない）。エラーメッセージや回復の質の改善は引き続き対象外
- CI パイプラインへの統合（当面はローカル実行のみ） — 既定セットは決定的で 3 秒程度なので、`dotnet test BinAnalyzer.slnx` に含まれる形で CI でも走る（`Category=Fuzz`）。長時間の探索はローカルで環境変数を使う

## 受入条件

1. [x] ファズテストが全フォーマット定義に対して実行可能であること — `FormatCatalog` が `formats/*.bdef.yaml` 全 38 件を読み込み、全件に有効サンプル（Integration.Tests の生成器）を対応付ける。対応漏れは `EveryFormat_HasAtLeastOneSample` で検出
2. [x] ランダム入力でクラッシュ・ハングが発生しないこと — 既定セット（フォーマットあたり 40 入力 × 2 モード + 257 の境界入力）と 20 倍のセット、ランダムシードの 10 倍セットで確認
3. [x] 切り詰め入力でクラッシュが発生しないこと — 全サンプルの全プレフィックス（長いものは間引き）× 2 モード
4. [x] テスト実行にタイムアウトが設定されていること — 1 デコードあたり `BINANALYZER_FUZZ_TIMEOUT_MS`（既定 5000）。既存のパターンに合わせ、回帰テストは `[Fact(Timeout = 10000)]`
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過） — 1,883 件

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodeOptions.MaxDepth` |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 壊れた入力への防御（下記 6 件） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| formats | `pe.bdef.yaml` の optional_header を修正 |
| testdata/golden | avi / elf / ico / pe を再生成（不具合修正で結果が変わったもの） |
| tests/BinAnalyzer.Fuzz.Tests（新規） | ファズテスト・プロパティベーステスト |
| tests/BinAnalyzer.Engine.Tests | `FuzzRegressionTests`（修正ごとの回帰テスト） |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — テストツリーに Fuzz.Tests、デコーダの防御機構
- [x] docs/cli-usage.md — `--max-repeat` の節に固定の防御（入れ子 64 段、サイズ範囲、位置が進まない要素）を追記
- [x] README.md — ファズの実行方法と環境変数

---

## 設計メモ

### 設計方針

- **依存を増やさない。** FsCheck などのプロパティテストライブラリは入れず、シード付き `Random` と小さな生成器（`InputGenerator`）で書く。失敗メッセージにフォーマット・モード・シード・入力の Base64 を必ず含め、同じシードで再実行すれば再現できるようにする
- **既定は小さく決定的、探索は環境変数で。** `BINANALYZER_FUZZ_ITERATIONS`（倍率）、`BINANALYZER_FUZZ_SEED`（固定値 / `random`）、`BINANALYZER_FUZZ_TIMEOUT_MS`。既定セットは 3 秒程度で `dotnet test` に含める
- **種は既存の生成器から借りる。** `Integration.Tests` の `*TestDataGenerator` を `ProjectReference` で参照し、`FormatCatalog.Samples` で 38 フォーマットすべてに 1 つ以上の有効サンプルを対応付ける。`testdata/real` は生成物で git 管理外なので使わない
- **契約を明文化して分類する。** `FuzzRunner` はデコード結果を Ok / DecodeError（`DecodeException`）/ Unexpected（それ以外の例外 = バグ）/ Timeout に分類する。Stop モードは `DecodeException` だけを投げてよい。Continue モードは例外を投げない
- **木の不変条件。** `TreeInvariants`: サイズとオフセットが負でない、ファイル空間のノードはデータ長の範囲内、名前が空でない、`DecodedBytes.RawBytes` の長さが `Size` と一致、ノード数が 500 万以下。圧縮の展開内容はストリーム空間として展開後サイズで検査
- **モード間・実行間の整合。** 同じ入力を同じ `BinaryDecoder` で 2 回デコードして同じ木（`DiffEngine`）になること。Stop モードで成功した入力は Continue モードでもエラー 0 件で同じ木になり、Stop モードが失敗した入力は Continue モードでエラーが 1 件以上記録されること。どんな木でも tree / JSON フォーマッタが例外を投げないこと
- **タイムアウト。** 専用スレッド（64 MB スタック、優先度最低）で走らせ `Join(timeout)`。止められないスレッドは残るが、優先度を下げているので他のテストを飢えさせない

### モデル変更

- `DecodeOptions.MaxDepth`（null なら 64）

### インタフェース変更

- `DecodeContext.PushEndiannessScope(endianness, capturesVariables = true)`、`DecodeContext.MarkVariableUndefined(name)`
- `BinaryDecoder.DefaultMaxDepth`
- `pe.bdef.yaml`: `optional_header` が switch から struct（`magic` + `fields` switch）になり、パスが `optional_header.magic` / `optional_header.fields.*` に変わる

### 代替案

- FsCheck / CsCheck を使う案 — シュリンク（最小化）は魅力だが、バイナリ入力の縮小はプレフィックス切り詰めで十分代替でき、依存と学習コストに見合わない。不採用
- 別リポジトリ / 別 CI ジョブで長時間回す案 — まずは決定的な小セットを `dotnet test` に含め、大きな探索は手元で行う。CI の別ジョブ化は必要になってから

### 懸念事項

- タイムアウトしたデコードのスレッドは止められない。同じ `BinaryDecoder` を使い回すプロパティテストでは、タイムアウト後にその instance を使わない
- 有効サンプルの一部（`CreateMinimalAvi` / `CreateMinimalIco` 等）は Continue モード前提の「わざと不完全な」もの。切り詰め・変異の種には Stop モードで通るサンプルだけを使う（AVI は完全な 3 サンプルに差し替え、ICO はエンジン修正で通るようになった）

---

## 実装メモ

### 実装中の設計変更

- `TruncationFuzzTests` は「完全なサンプルは Stop モードで成功する」を先に確認する。ここで ICO / PE / AVI のサンプルが Stop モードで通らないことが分かり、ICO と PE は不具合として修正、AVI はサンプルの差し替えで対応した
- 最初の実行でテストホストがスタックオーバーフローで落ちた（msgpack の 1 バイト入力）。結果の一覧だけを見ると失敗 6 件に見えるが、実際にはプロセスごと落ちていた。以降は `Stack overflow` の有無もログで確認するようにした

### ファズが見つけた不具合と修正

| # | 症状（入力） | 原因 | 修正 |
|---|---|---|---|
| 1 | ICO の完全なサンプルが Stop モードで `Variable 'image_magic' not found` | フィールド単位の `endianness:` がオーバーレイスコープを push し、そのフィールドの値がスコープごと捨てられていた | `PushEndiannessScope(..., capturesVariables: false)`。`SetVariable` は最も近い変数捕捉スコープに書く |
| 2 | heif / mp4 / wasm / pcap / webp の切り詰め・変異で負のサイズ・負のオフセット（Continue モード） | `size:` 等の式の評価結果を `(int)` でキャストし、`0xFFFFFFFF` が -1、`0x7FFFFFFF` が `position + size` の桁あふれで負に。負のサイズのスコープが Pop で位置を逆走させていた | `ToByteCount` で 0..int.MaxValue を検査、`PushScope` / `EnsureAvailable` は long で計算し負を拒否 |
| 3 | msgpack / cbor の 1 バイト入力（`0x81` 等）でスタックオーバーフロー（両モードの CLI でも再現） | Continue モードで内側の `format_byte` が読めずエラーになった後、`switch_on: {format_byte}` が外側の同名変数にフォールバックして無限再帰 | 失敗したフィールド名を「未定義」として束縛する `MarkVariableUndefined`。加えて入れ子の深さ上限 `MaxDepth`（既定 64）を `DecodeNestedStruct` で検査 |
| 4 | msgpack / cbor のランダム入力で Continue モードがタイムアウト | `repeat_count` の要素が入れ子の奥で失敗して 0 バイトしか進まない場合、`ContainsError` が直下しか見ないため打ち切られず、壊れた count（数十億）回まわっていた | `ContainsError` を再帰にし、`repeat_count` / `until` / `while` にも「位置が進まないエラー要素で打ち切る」ガードを追加（従来は `repeat: eof` のみ） |
| 5 | ELF の切り詰め・変異で `body` のサイズが -16（ゴールデンにもそのまま記録されていた） | `seek`（restore 無し）で struct の先頭より前に戻ると `Size = 終了位置 - 開始位置` が負になる | struct / switch のサイズを 0 に丸める |
| 6 | OTF を 2 バイトに切り詰めると Continue モードで数秒 | `numTables` の壊れた値（20308）のぶんエラー要素を積んでいた | #4 のガードで 1 要素目で打ち切られる |
| 7 | PE の完全なサンプルが Stop モードで `Variable 'magic' not found`（旧エンジンでも同じ） | `pe.bdef.yaml` の `optional_header` が、その中で初めて読む `magic` を `switch_on` に使っていた。Continue モードでは error ノードになるだけなので統合テストとゴールデンがそれを正としていた | `optional_header` を struct にして `magic` を先に読み、残りを `size: remaining` の switch に。ゴールデンを再生成 |

ゴールデンの変更: ico（image_data が構造として読める）、pe（optional_header 全体が読める）、elf（`body` のサイズ -16 → 0）、avi（EOF の chunk で `chunk_size` が外側の値にフォールバックしなくなり、`data` / `padding` が「未定義」エラーになる）。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| RandomInputFuzzTests | RandomBytes_NeverCrashOrHang（38 フォーマット）、EmptyAndSingleByte_NeverCrash（38） | AC-1, AC-2, AC-4 |
| TruncationFuzzTests | EveryFormat_HasAtLeastOneSample、EveryPrefix_NeverCrashOrHang（52 サンプル） | AC-1, AC-3, AC-4 |
| MutationFuzzTests | MutatedSamples_NeverCrashOrHang（52 サンプル） | AC-2, AC-3 |
| DecodePropertyTests | Decode_IsDeterministic_AndModesAgree（38）、OutputFormatters_NeverThrow_OnAnyDecodedTree（38） | AC-2 |
| FuzzRegressionTests（Engine.Tests） | FieldLevelEndianness_KeepsVariableVisible_ToLaterFields、FieldLevelEndianness_OnStructField_KeepsMemberAccess、HugeSizeExpression_IsADecodeError_NotANegativeRange（3 値）、RecursiveFormat_TruncatedInput_ContinueMode_Terminates_WithoutStackOverflow、CountLoop_ZeroProgressErrorElements_AreTruncated_EvenWhenErrorIsNested、NestingDeeperThanLimit_IsADecodeError_AndLimitIsConfigurable、FailedField_ShadowsOuterVariableOfSameName_InContinueMode、StructThatSeeksBackwards_HasNonNegativeSize | 不具合 #1〜#5 の回帰 |

実行規模: 既定セット 265 テスト 3 秒。`BINANALYZER_FUZZ_ITERATIONS=20` で 26 秒（すべて通過）。

### 気づき・今後の課題

- `DecodeOptions.MaxDepth` は CLI に露出していない（`--max-depth`）。深い入れ子のフォーマットを扱うときに必要になれば追加する
- 入れ子 1 段あたりのスタック消費が Debug ビルドで約 8 KB と大きい（`DecodeStruct` → `DecodeField` → `DecodeSingleField` → `DecodeStructField` の 4〜5 フレーム、ローカル変数が多い）。REQ-180（デコードの時間・メモリ）でフレームを軽くできれば上限を上げられる
- 失敗したフィールドの「未定義」束縛によって、外側の同名変数に意図的にフォールバックさせていた定義があれば挙動が変わる（`avi.bdef.yaml` の EOF 時のような偶然の依存のみで、意図的なものは見つからなかった）
- `CreateMinimalAvi` / `CreateMinimalIco` のような「わざと不完全な」サンプルは Continue モードの挙動を固定するテストに使われている。完全なサンプルを別途用意して両方持つ方が意図が明確になる
- 圧縮フィールド（zlib / bzip2 / lzma / zstd / lz4）へのランダム入力は各ライブラリの例外を `DecodedError` にする既存経路で吸収できている。展開後サイズの上限は無いので、`DecompressedSize` を大きく偽った入力（伸長爆弾）は別途上限を検討する
