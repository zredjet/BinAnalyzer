# REQ-188: 既存フォーマット定義の見直し（仕様の版・日本語の説明・DSL の制約の解消）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（REQ-183 のドキュメント整合性テスト、REQ-186 の「同梱定義の警告 0 件」テストの上で行う） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-25 |

## 背景・動機

同梱のフォーマット定義（`formats/*.bdef.yaml` 40 種 + `formats/common/` 2 種）は、2026 年 2 月から段階的に書かれ、書かれた時期の DSL と当時参照した仕様の版のままになっている。2026-09-24 に全定義を調べたところ、次の 4 種類の問題があった。

### 1. 説明（description）が足りない・日本語でない

description のあるフィールドは **1,799 件中 657 件（37%）**。定義による差が大きい。

| 説明の割合 | 定義 |
|---|---|
| 0% | ogg（29 フィールド）、protobuf（9）、x509（103）、common/isobmff（16）、common/riff（2） |
| 1〜20% | elf 2%（78）、pe 4%（105）、mp4 13%（98）、msgpack 10%（57）、mkv 15%（51）、fat 20%（68） |
| 21〜50% | macho 21%、zip 26%、otf 27%、wav 33%、bmp 37%、midi 41%、xz 42%、png 43%、jpeg 44%、java-class 46%、cbor 47%、pcap 47% |
| 51% 以上 | 残りの 18 定義（tar / parquet / pdf は 100%。ただし parquet・pdf はフィールド数 2〜3） |

- 英語のままの description がある: sqlite 5 件、cbor 3 件、fat 3 件、midi 2 件、msgpack 2 件、avi / heif / pcap / otf / flv / mp3 / xz 各 1 件
- enum のエントリにも説明がほとんど無い（すべてのエントリに説明があるのは jpeg / tar / pcap / bmp / fat / ico / midi のみ）。例: tiff の enum（計 59 エントリ）、java-class の `cp_tag`・`java_version`（計 40 エントリ）、mkv の `ebml_id`（100 エントリ）は説明が 0 件で、値の意味が名前だけでは分からないものがある

### 2. 準拠している仕様の版が分からない

定義の先頭に準拠仕様（名称・版・RFC 番号など）を書いているのは **xz / mkv / pdf の 3 定義だけ**。残り 37 定義はどの版に基づくのか、何に対応していないのかが定義から分からない。

### 3. 仕様の新しい版・広く使われる拡張が入っていない

主な仕様の新しい版や、広く使われている拡張との差分の候補（2026-09-24、定義の中身の grep と enum の確認による。各定義の見直しで仕様書と照合して確定する）:

| 定義 | 差分の候補 |
|---|---|
| png | PNG 第 3 版（W3C 勧告 2025）の cICP / mDCV / cLLI、APNG（acTL / fcTL / fdAT、第 3 版で正式化）、eXIf、iCCP、sPLT、hIST のチャンクが無い（読めるのは IHDR / PLTE / tEXt / zTXt / iTXt / tIME / pHYs / sRGB / gAMA / cHRM / tRNS / bKGD / sBIT） |
| java-class | バージョンの enum が Java 23（67）まで。Java 25 LTS（69）以降が無い |
| wasm | セクション ID が 11（data）まで。WASM 2.0 の data count（12）、例外処理の tag（13）が無い |
| dns | レコード型（QTYPE）の enum が無い（rcode のみ）。HTTPS / SVCB（RFC 9460）、OPT（EDNS(0)、RFC 6891）も含めて足す |
| elf | `e_machine` の enum が 9 種のみ（LoongArch 258 など主要アーキテクチャの不足） |
| zip | ZIP64（APPNOTE 6.3.x）と主要な拡張フィールド（0x0001 ZIP64、0x5455 拡張タイムスタンプ、0x7075 Info-ZIP UTF-8 パス、0x9901 AES）が無い |
| tiff | BigTIFF（マジック 43、64 ビットオフセット）が無い |
| flac | RFC 9639（2024）で標準化済み。定義の照合が必要 |
| webp | RFC 9649（2024）で標準化済み。定義の照合が必要 |
| otf | 可変フォントのテーブル（fvar / gvar / avar / STAT）が無い |
| flv | Enhanced RTMP / FLV（HEVC / AV1 等の FourCC）が無い |
| avi | OpenDML AVI 2.0（`indx` / `AVIX`、1 GB を超える AVI）が無い |
| wav | BWF の `bext`、RF64（`ds64`）が無い |
| mp4 | サンプル記述（`stsd`）の中身（avc1 / hvc1 / av01 / mp4a 等）が生のバイト列 |

### 4. 当時の DSL の制約を前提にした記述が残っている

DSL はその後の要望で表現力が増えた（REQ-137 のメンバーアクセス、REQ-147 の `{remaining}` を使った末尾からの `seek` と `a.b[i].c` の添字、REQ-186 の virtual の enum など）。一方で、定義のコメントや構造には当時の制約が残っている。

- **parquet**: 「フッタは末尾からの逆方向解析が必要で DSL では構造化不可」とあり、先頭のマジックと残り全部のバイト列しか読んでいない。末尾の 4 バイトのフッタ長と `PAR1` は `{remaining}` + `seek` で読める（Thrift のメタデータ本体は引き続きバイト列）
- **pdf**: 「xref の検索には末尾からの逆方向解析が必要でシーケンシャルなリーダーでは対応不可」とある。末尾の `startxref` 付近は `seek` で読める範囲がある（テキスト構文の解釈そのものは対象外）
- **java-class**: 属性（Code / ConstantValue / Exceptions）を名前で振り分けられず、REQ-186 で未使用の struct を削除した。コンスタントプールのインデックスの引き方が DSL に無いのが原因（本要望では DSL は変えない。別要望の候補として整理する）
- 各定義に、今の DSL なら素直に書ける回りくどい書き方が残っていないかを見直す（例: virtual の enum が無かった頃の、enum を付けられずに説明文で値の意味を書いている箇所）

## 機能要件

### 追加する機能

- [ ] **定義の書き方の規約**（新規ドキュメント `docs/format-authoring.md`）。先頭コメントの書式、description の書き方、enum のラベル・説明の付け方、命名、テストデータ（Generator）の置き方を決める。新しく定義を追加するときもこれに従う
  - 先頭コメント: フォーマット名、**準拠仕様（名称・版・RFC / ISO 番号・URL）**、構造の概要、**対応していないもの**（版・拡張・圧縮の展開など）
  - description: 仕様書を開かなくてもフィールドの意味が分かる日本語。単位・値の範囲・計算方法（例: 「(値 + 1) × 4 バイト」）、他のフィールドとの関係を書く。予約フィールドやパディングも短く書く。仕様の用語は原語を括弧で添えてよい（例: 「圧縮後サイズ（Compressed Size）」）
  - enum: ラベルは仕様の名前をそのまま使い、意味が名前から分からない値には description を付ける
- [ ] **定義の品質をテストで固定する**（REQ-183 / REQ-186 と同じ形）
  - 全定義の先頭コメントに準拠仕様の行（例: `# 仕様: ...`）があること
  - 全フィールドに description があり、日本語を含むこと
  - 未対応の定義は「対応待ち一覧」としてテストに列挙し、見直しが済んだ定義から一覧を減らす。一覧に無い定義（新規追加を含む）は規約を満たさないと失敗する

### 変更する既存機能

- [ ] **全 40 定義 + 共通ライブラリ 2 種を見直す。** 定義ごとに次を行う
  1. 最新の仕様（または事実上の標準）と照合し、準拠する版を決めて先頭コメントに書く
  2. 背景 3 の差分の候補を確定し、足すものは定義に足す（足さないものは先頭コメントの「対応していないもの」に理由と一緒に書く）
  3. 全フィールドに日本語の description を付け、英語の description を日本語にする。値の意味が分からない enum の値に description を付ける
  4. 背景 4 の、当時の DSL の制約を前提にした記述・構造を今の DSL で書き直す
  5. テストデータ（Generator）と解析テストを、足した構造に合わせて追加する。ゴールデンファイルを更新する
- [ ] **分けて進める。** 1 つの PR で全部は変えない。分類ごと（下表）に要望の実装メモへ記録しながら進め、1 分類 1 PR を目安にする

  | 分類 | 定義 | 主な作業 |
  |---|---|---|
  | 画像 | png / jpeg / gif / bmp / tiff / webp / ico / heif / icc | PNG 第 3 版・APNG、BigTIFF、WebP の RFC との照合 |
  | 音声・映像 | mp3 / mp4 / wav / flac / ogg / avi / flv / midi / mkv / common/riff / common/isobmff | ogg・isobmff の説明 0% の解消、FLAC の RFC との照合、BWF / RF64、OpenDML、Enhanced FLV |
  | アーカイブ・圧縮 | zip / gzip / tar / 7z / lz4 / xz | ZIP64 と拡張フィールド |
  | 実行形式・バイトコード | elf / pe / macho / java-class / wasm | ELF / PE の説明の不足、`e_machine`、Java のバージョン、WASM のセクション |
  | データ・その他 | sqlite / parquet / pdf / pcap / dns / protobuf / msgpack / cbor / x509 / fat / otf | x509・protobuf の説明 0%、DNS のレコード型、parquet / pdf の末尾からの読み取り、OTF の可変フォント |

### 変更しないもの（スコープ外）

- 新しいフォーマットの追加（pcapng、exFAT など別の形式は別要望）。BigTIFF のように同じ定義の中で扱える新しい版は本要望に含める
- DSL・エンジンの機能追加。見直しの中で必要になった機能（例: java-class のコンスタントプールのインデックス引き）は別要望として起票し、本要望の実装メモに記録する
- 圧縮データの展開や、テキスト構文（PDF のオブジェクト）の解釈
- README のフォーマット一覧の書き方の変更（REQ-183 の整合性テストの対象）

## 受入条件

1. [x] `docs/format-authoring.md` に定義の書き方の規約があり、README か architecture.md から参照されていること
2. [x] 全 42 定義（共通ライブラリを含む）の先頭コメントに準拠仕様（名称・版）と、対応していないものが書かれていること
3. [x] 全フィールドに日本語の description があること（テストで固定され、「対応待ち一覧」が空になっていること）
4. [x] 背景 3 の差分の候補のそれぞれについて、定義に足したか、足さない理由を先頭コメントに書いたかのどちらかであること
5. [x] 背景 4 の parquet / pdf の末尾からの読み取りが、今の DSL で書き直されているか、書き直さない理由が書かれていること
6. [x] 足した構造ごとに、テストデータ（Generator）と解析テストがあること。可能なものは実在のツール（`xz` / `ffmpeg` など）で作ったファイルでもデコードを確認し、実装メモに記録すること
7. [x] 同梱定義の検証警告が 0 件のままであること（REQ-186 のテスト）
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過。ゴールデンファイルは見直した定義の分だけ更新する）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| formats/ | 全定義の見直し |
| BinAnalyzer.Core / Dsl / Engine / Output | 変更なし（DSL の機能追加が必要になった場合は別要望） |
| tests/BinAnalyzer.Integration.Tests | 定義の品質テスト、足した構造の Generator・解析テスト |
| tests/BinAnalyzer.Fuzz.Tests | 追加した Generator のファズへの登録 |
| testdata/golden | 見直した定義のゴールデンファイルの更新 |

### 変更が必要なドキュメント

- [x] docs/format-authoring.md — 新規（定義の書き方の規約）
- [x] README.md / docs/architecture.md — 規約への参照
- [x] docs/dsl-reference.md — 共通ライブラリの表（音声・映像）、virtual の padding（データ・その他、REQ-192）
- [x] CLAUDE.md — 定義の書き方の規約への参照（フォーマット定義を書くときに読むもの）

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- **規約は文書 1 つ（`docs/format-authoring.md`）にまとめ、機械的に確かめられる部分だけテストにする。** 先頭コメントの `# 仕様:` / `# 対応していないもの:` の 2 行と、全フィールドの description が日本語を含むことを `FormatDefinitionQualityTests` が検査する。説明の質（単位・計算方法を書いているか）や enum の説明の要否はレビューで見る
- **対応待ち一覧で段階的に進める。** 最初は XZ 以外の 41 定義を一覧（`FormatDefinitionQualityTests.Pending`）に載せる。一覧に載っていない定義は規約を満たさないと失敗し、載っている定義が規約を満たすと「一覧から外す」よう失敗する。一覧は減る一方で、新しく追加する定義は最初から規約を満たす必要がある
- **先頭コメントは YAML のコメントで書く。** DSL に `spec:` のようなキーを足す案もあるが、ツールの表示には使わず、定義を読む人のための情報なのでコメントで足りる（キーを足すと DSL・JSON Schema・DTO の変更になる）
- **最初の PR で XZ をお手本にする。** 一番新しい定義で構造も把握済みのため。規約の文書の例も XZ の先頭コメントにする
- **分類ごとの見直しの順番**: 利用者の多さと差分の大きさから、画像 → アーカイブ・圧縮 → 実行形式・バイトコード → 音声・映像 → データ・その他 を目安にする（着手時に決め直してよい）
- **fields の検査は YAML の構文木で行う。** ローダーを通すと imports の解決が要り、`formats/common/` の単体読み込みは REQ-178 の課題がある。`structs:` の各値（旧形式のリストと新形式の `fields:` の両方）のフィールドを直接見る

### モデル変更

- なし（定義ファイル・ドキュメント・テストのみ）

### インタフェース変更

- なし

### 代替案

- 一度にすべての定義を見直す案。差分とゴールデンファイルの更新が大きくなりレビューできないので、分類ごとに分ける方向
- description の日本語化だけを先に全定義で行い、仕様の版の見直しは後にする案。同じフィールドを 2 度触ることになり、版の見直しで構造が変わると説明も書き直しになる。分類ごとに両方を一度に行う方向

### 懸念事項

- 一部の仕様は有料（ISO/IEC 14496-12 の ISO BMFF、ISO/IEC 23008-12 の HEIF など）。参照できた資料（公開の草案・実装のソースなど）と版を先頭コメントに正直に書く
- description の量が増えると JSON / HTML 出力とゴールデンファイルが大きくなる。出力の大きさの増分を計測して実装メモに記録する
- 仕様の新しい版に合わせて構造を変えると、旧版のファイルを読めなくしないよう注意する（例: 既存の enum 値を消さない、構造の分岐で両方を読む）

---

## 実装メモ

### 進捗

| 段階 | 内容 | 状態 |
|---|---|---|
| 土台 | `docs/format-authoring.md`、`FormatDefinitionQualityTests`（対応待ち 41 定義）、XZ をお手本に（先頭コメント、全 40 フィールドと enum の説明）、README / architecture.md / CLAUDE.md からの参照 | 済 |
| 画像 | png / jpeg / gif / bmp / tiff / webp / ico / heif / icc（先頭コメント・全フィールドの説明、PNG 第 3 版・APNG、BigTIFF と IFD チェーン、WebP の RFC 9649 照合、ICC の型ごとの分解、下記のバグ修正） | 済 |
| アーカイブ・圧縮 | zip / gzip / tar / 7z / lz4（先頭コメント・全フィールドの説明、ZIP を Central Directory から読む形に変更・Zip64・拡張フィールド、7z のヘッダの分解、pax / GNU tar、LZ4 のフレームの種類と Block Checksum、下記のバグ修正。画像の BMP の条件式のバグもここで直した） | 済 |
| 実行形式・バイトコード | elf / pe / macho / java-class / wasm（先頭コメント・全フィールドの説明、ELF のセクションの中身、PE のデータディレクトリ、Mach-O のユニバーサルバイナリ・シンボル・コード署名、Java のコンスタントプールの 2 スロットと属性、WASM 3.0 の全セクション、下記のバグ修正） | 済 |
| 音声・映像 | mp3 / mp4 / wav / flac / ogg / avi / flv / midi / mkv / common/riff / common/isobmff（先頭コメント・全フィールドの説明、RF64 / BW64・WAVEFORMATEXTENSIBLE・BWF、OpenDML、MP4 のサンプルエントリ・フラグメント・iTunes のメタデータ、ID3v2.4 / ID3v1 / Xing、Opus・FLAC・Speex・Theora の Ogg、AMF0 と Enhanced FLV の FourCC、MIDI のランニングステータス、下記のバグ修正） | 済 |
| データ・その他 | sqlite / parquet / pdf / pcap / dns / protobuf / msgpack / cbor / x509 / fat / otf（先頭コメント・全フィールドの説明、Parquet のフッタ（Thrift Compact Protocol）、PDF の改訂・相互参照表・FlateDecode、SQLite の全ページと記録、pcapng・pcap のバイト順とナノ秒、DNS の名前の圧縮と RR、X.509 の DER の汎用の分解と拡張、FAT の長いファイル名とディレクトリの連鎖、OTF の TTC・name・cmap・fvar、下記のバグ修正。エンジンの変更 REQ-192・REQ-193） | 済 |

### 実装中の設計変更

- 規約の「共通ライブラリの struct 名」は、既存の共通ライブラリ（`container_box`・`raw_data` など、接頭辞なし）に合わせて「役割の分かる名前にし、インポート側では同じ名前を使わない」とした。接頭辞を強制すると既存のインポート側が壊れる

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FormatDefinitionQualityTests | Definition_FollowsTheAuthoringGuide（全 42 定義。対応待ちは「まだ満たしていない」ことも確かめる） | 2, 3 |
| FormatDefinitionQualityTests | Pending_ListsOnlyExistingDefinitions | 3 |
| FormatDefinitionQualityTests | Check_AcceptsAConformingDefinition / Check_ReportsMissingHeaderLines / Check_ReportsMissingAndEnglishDescriptions | 2, 3 |
| PngParsingTests | ThirdEdition_ColourChunks / Apng_AnimationAndFrameControl / ColourTypeDependentChunks_AreDecodedByIhdrColourType | 4, 6 |
| TiffParsingTests | TwoPageTiff_FollowsNextIfd / BigTiff_HeaderEntriesAndNextIfd | 4, 6 |
| WebpParsingTests | AnimatedWebp_Vp8xFlagsFollowRfc9649 / AnimatedWebp_FramesContainVp8lSubChunks / AnimatedWebp_XmpChunkIsText | 4, 6 |
| IccParsingTests | IccV4Profile_TagsAreDecodedByTypeSignature（既存 2 件は子を名前で引くよう更新） | 6 |
| BmpParsingTests | ImplicitPalette_HasTwoToTheBppEntries / Bitfields_MasksFollowTheInfoHeader | 6 |
| JpegParsingTests | JpegFormat_MergedTables_DecodesEveryDqtAndDhtTable | 6 |
| ZipParsingTests | StreamedZip_UsesCentralDirectorySizesAndReadsDataDescriptors / Zip64_ReadsLocatorExtraFieldAndUnsignedDataDescriptor（既存 5 件は子を名前で引くよう更新） | 4, 6 |
| GzipParsingTests | OptionalFields_AreReadInOrder | 6 |
| TarParsingTests | PaxAndGnuTar_SplitsRecordsLongNamesAndEndOfArchive（既存の TarFormat_Magic_ValidatesCorrectly に検証の ✓ を追加） | 4, 6 |
| SevenZipParsingTests | PlainHeader_DecodesStreamsAndFilesInfo / EncodedHeader_DecodesPackedHeaderLocationAndCoder | 6 |
| Lz4ParsingTests | SkippableFrame_AndBlockChecksum_AreDecoded（既存 6 件はフレームの下を引くよう更新） | 6 |
| BmpParsingTests | V5Header_DecodesWithoutErrorAndReadsThePalette | 6 |
| JsonSchemaTests | Schema_ValidatesFormatFile に gzip / tar / 7z / lz4 を追加（YAML の引用符付きの数字を整数にしないよう変換を直した） | 7 |
| ElfParsingTests | Sections_ResolveNamesSymbolsNotesDynamicAndCompressedData（既存は子を名前で引くよう更新） | 4, 6 |
| PeParsingTests | Pe32PlusDll_DecodesDirectoriesInsideTheirSections / ManagedPe32_DecodesClrHeaderMetadataAndThirtyTwoBitImports | 6 |
| MachoParsingTests | UniversalBinary_DecodesBothSlicesWithTheirEndianness（既存の LC_BUILD_VERSION の値を 0x32 に直した） | 6 |
| JavaClassParsingTests | WideConstants_TakeTwoSlotsAndNamesResolveThroughTheConstantPool | 4, 6 |
| WasmParsingTests | Wasm3Module_DecodesGcTypesImportsConstantExpressionsAndNames | 4, 6 |
| JsonSchemaTests | Schema_FieldTypeEnum_MatchesDslTypeNames（スキーマの型名に uleb128 / sleb128 / vlq と別名を追加）、Schema_ValidatesFormatFile に pe / macho / java-class / wasm を追加 | 7 |
| WavParsingTests | PcmWith18ByteFmt_ReadsCbSize / Rf64_UsesDs64Sizes / Extensible_DecodesSubFormatAndChannelMask / OddSizedChunk_SkipsPaddingByte（既存は子を名前で引くよう更新） | 4, 6 |
| AviParsingTests | OpenDml_DecodesSuperIndexAndStandardIndex / OpenDml_DecodesStreamNameAndExtendedHeader / OpenDml_DecodesAvixAndOddSizedChunk（既存は子を名前で引くよう更新） | 4, 6 |
| Mp4ParsingTests | SampleEntries_DecodeAvcCAndEsds / Ilst_DecodesTextMetadata / Fragments_DecodeMoofAndTrun / Mvex_DecodesTrackExtends | 6 |
| Mp3ParsingTests | Id3v24_DecodesSyncsafeFramesAndPadding / Frames_DecodeHeaderAndXing / Id3v1_DecodesAtEnd / WithoutTags_DecodesMpeg2Frames（既存は子を名前で引くよう更新） | 4, 6 |
| FlacParsingTests | FirstFrameHeader_DecodesCorrectly（既存は子を名前で引くよう更新） | 6 |
| OggParsingTests（新規） | MinimalVorbis_DecodesIdentificationHeader / Opus_DecodesHeadAndTags / PageBody_UsesSegmentTableEvenIfDataContainsCapturePattern | 6 |
| FlvParsingTests | ScriptTag_DecodesOnMetaData / AvcTags_DecodePacketTypeAndCompositionTime / AacTag_DecodesPacketType / PreviousTagSize_IsValidated（既存は子を名前で引くよう更新） | 6 |
| MidiParsingTests | UnknownChunk_IsSkipped / RunningStatus_ReusesPreviousStatus / MetaEvents_DecodeTempoAndTimeSignature（既存は子を名前で引くよう更新） | 6 |
| JsonSchemaTests | Schema_ValidatesFormatFile に mp3 / mp4 / flac / ogg / avi / flv / midi / mkv / common/riff / common/isobmff を追加 | 7 |
| DnsParsingTests | DecodesMinimalQuery / CompressionPointers_ResolveFullNames / ResourceRecords_DecodeRdataByType / OptRecord_DecodesEdns（既存は子を名前で引くよう書き直し） | 4, 6 |
| ProtobufParsingTests（新規） | MinimalMessage_DecodesFields / Varints_ShowTwosComplementAndZigZag / FixedValues_ShowIntegerAndFloat / LengthDelimited_ShowsTextAndBytes / Group_DecodesNestedFieldsUntilEndGroup | 6 |
| MsgpackParsingTests（新規） | MinimalMap_DecodesKeyAndValue / Document_DecodesAllTypes / Timestamps_DecodeThreeForms | 6 |
| CborParsingTests（新規） | MinimalMap_DecodesKeyAndValue / Rfc8949Examples_MatchDiagnosticNotation / IndefiniteLength_ReadsUntilBreak | 6 |
| ParquetParsingTests | MagicAndFooterLength_DecodeCorrectly / FileMetaData_NamesFieldsAndValues / Schema_DecodesElementsWithEnums / ColumnMetaData_FollowsDataPageOffsetToPageHeader / TreeOutput_HidesWorkingValues | 4, 5, 6 |
| PdfParsingTests | Header_DecodesVersionAndBinaryComment / MinimalPdf_DecodesObjectsXrefAndTrailer / FlateStream_IsDecompressed / IncrementalUpdate_DecodesTwoRevisions | 5, 6 |
| SqliteParsingTests | MinimalSqlite_DecodesFirstPage / Records_DecodeSerialTypes / OverflowingPayload_KeepsLocalPartAndOverflowPage（既存は書き直し） | 4, 6 |
| PcapParsingTests | EthernetMix_DecodesUdpArpVlanAndIpv6 / BigEndianNanosecond_UsesMagicForByteOrder / RawIp_DispatchesByVersionNibble / PcapNg_DecodesBlocksAndPackets（既存は書き直し） | 4, 6 |
| X509ParsingTests | MinimalCertificate_DecodesTbsFields / V1Certificate_HasNoVersionField / RsaKey_DecodesModulusAndExponent / Extensions_DecodeInnerDer / ConcatenatedCertificates_AreReadInOrder（既存は書き直し） | 4, 6 |
| FatParsingTests | SmallImage_IsFat12ByClusterCount / LongFileNames_AreAssembledFromLfnEntries / Subdirectory_FollowsClusterChain / Fat32_ReadsFsInfoAndRootCluster（既存は書き直し） | 4, 6 |
| OtfParsingTests | NameTable_DecodesStringsByPlatform / Cmap_DecodesFormat4And12 / MetricsAndVariationTables_Decode / Collection_DecodesEachFontWithSharedTables（既存は書き直し） | 4, 6 |
| VirtualFieldTests（Engine） | PaddingVirtual_IsMarkedAsPaddingAndStillBindsItsValue（REQ-192） | — |
| ErrorRecoveryTests（Engine） | ContinueMode_FailedBitfield_HidesEarlierValuesOfItsEntries（REQ-193） | 8 |
| JsonSchemaTests | Schema_ValidatesFormatFile に sqlite / parquet / pdf / pcap / dns / protobuf / msgpack / cbor / x509 / fat / otf を追加 | 7 |
| SourceLocationTests / GuiImportTests | common/isobmff の dref の子を iso_box にしたことと、WAV の fmt のフィールド名の変更に合わせて更新 | — |

### 画像の見直し（PR: 画像）

**直したバグ**

- WebP: VP8X のフラグのビット位置がほぼ逆だった（アニメーション WebP で `icc_profile = 1` と表示）。RFC 9649 どおり ICC=5・alpha=4・Exif=3・XMP=2・animation=1 に直した
- ICC: タグの中身をタグのシグネチャ（`desc` 等）で振り分けていたため、v4 の `mluc` 型などが誤読されていた。先頭 4 バイトの型シグネチャ（`tag_type`）で振り分けるよう変え、`mluc`（レコード + UTF-16BE の文字列）・`para`・`sf32`・`sig`・`chrm`・`curv` を分解した。XYZ は複数の値を持てるので配列にした
- BMP: パレット数が `colors_used == 0` のとき 0 個になっていた（2^bpp 個が正しい）。BI_BITFIELDS / BI_ALPHABITFIELDS の色マスク（BITMAPINFOHEADER の後ろ）を読んでいなかった。画素データを `pixel_offset` へ seek せずに読んでいた。未知の DIB ヘッダのサイズが 4 バイト多かった
- TIFF: 値がエントリ内に収まる型の判定から ASCII / UNDEFINED / FLOAT が抜けていた。次の IFD をたどっていなかった（複数ページの 2 ページ目以降が見えない）
- JPEG: 1 つの DQT / DHT セグメントに複数のテーブルがあると、2 つ目以降を 1 つ目の値として読んでいた（ffmpeg の出力は DHT 4 個を 1 セグメントにまとめる）。テーブルの `repeat: eof` にし、DHT の値のバイト数を L1〜L16 の合計から求める

**足した構造**

- PNG 第 3 版（W3C 2025）: cICP・mDCV・cLLI・eXIf、APNG（acTL・fcTL・fdAT）、色の種類で形の変わる sBIT / tRNS / bKGD（IHDR の `color_type` で switch）、iCCP・hIST・sPLT。tEXt は Latin-1、zTXt / iCCP は zlib で展開
- BigTIFF（マジック 43、64 ビットのオフセットと個数）。従来の TIFF の子の並びは変えず、BigTIFF 用のフィールドを `if` で足した。LONG8 / SLONG8 / IFD8 型と、Predictor・SampleFormat・ICCProfile・XMP など 12 タグ
- WebP: VP8 のフレームヘッダ（幅・高さ・拡大率）、VP8L のヘッダ（14 ビットの幅・高さ、alpha_is_used、版）、ANMF の中のサブチャンク、XMP チャンク

**実在のツールでの確認**: Pillow 12.3 で作った APNG（2 フレーム）・パレット / グレースケール PNG、LZW の複数ページ TIFF、可逆 α 付き / アニメーション / 非可逆 WebP、Pillow の v4.4 sRGB ICC プロファイル（mluc の文字列 "sRGB built-in" など）、BMP を読んで値が合うことを確かめた。自作の APNG は Pillow で 2 フレームとして読め、2 フレーム目の画素が意図どおり。JPEG は Pillow（テーブルごとに別セグメント）と ffmpeg（DHT 4 個を 1 セグメント）の両方で確認した

**出力の大きさ**: 画像 9 件のゴールデン（JSON）は合計 74,233 → 104,520 バイト（+41%）。増分の大半は description と enum の説明で、残りは足した構造

**残した課題**（各定義の「対応していないもの」に記載）

- JPEG: プログレッシブの 2 つ目以降の SOS（最初の SOS のエントロピー符号化データにまとめて入る）、長さを持たない単独マーカー（RSTn 等）がセグメントの並びに現れる場合、JFXX 拡張、APPn の中身
- GIF: NETSCAPE 拡張はサブブロック 1 個（ループ回数）を前提にしている。ANIMEXTS1.0 やバッファリング指定は分解しない
- HEIF: iloc のアイテム配列・ipma・iref・アイテムプロパティ、uuid ボックス
- ICO: 画像データの中身（DIB / PNG）はバイト列のまま
- ICC: v5（iccMAX）の型

### アーカイブ・圧縮の見直し（PR: アーカイブ・圧縮）

**直したバグ**

- ZIP: 先頭から署名をたどって読んでいたため、Local File Header のサイズが 0 のエントリ（汎用フラグ bit 3。.NET の ZipArchive・Java の ZipOutputStream などのストリーム出力）で、2 つ目以降のエントリを読めていなかった。公式の読み方どおり End of Central Directory → Central Directory を先に読み、各ファイルの位置とサイズは Central Directory の値を使う形に変えた。Data Descriptor（署名は任意、Zip64 は 8 バイトのサイズ）も読む
- tar: マジックの検証が `magic == 'ustar'` で、値の "ustar\0" の NUL まで比べていたため常に ✗ だった（`trim` で比べる。GNU 形式の "ustar " と v7 形式の空も受け付ける）。終端の 0 ブロック 2 個とレコードの詰め物をエントリとして読んでいた（次のヘッダの先頭バイトを先読みして終端を判定する）
- gzip: FHCRC のヘッダ CRC16 を読んでおらず、FHCRC のあるファイルでは圧縮データが 2 バイトずれていた
- LZ4: FLG の B.Checksum が 1 のとき、各ブロックの後ろの Block Checksum を読んでおらず、2 つ目のブロックでデコードエラーになっていた（`lz4 -BX` の出力）。Block Checksum は xxHash-32 で検証する
- BMP（画像の見直しで入れたバグ）: color_masks と color_table の `if` に DSL に無い `false` を書いていたため、BITMAPINFOHEADER 以外（V4 / V5 / OS/2 の 12 バイトのヘッダ）でデコードエラーになっていた。ImageMagick の既定の出力（V5）が読めなかった

**足した構造**

- ZIP: Zip64（Locator・Zip64 End of Central Directory・拡張フィールド 0x0001）、MS-DOS の日時のビットフィールド、汎用フラグのビットフィールド、UNIX のモード、拡張フィールドの分解（NTFS の時刻・拡張タイムスタンプ・UTF-8 のファイル名・UID / GID・AES）、deflate / bzip2 / Zstandard のデータの展開、APPNOTE 6.3.10 の圧縮方式・OS・拡張フィールド ID の enum
- gzip: FEXTRA のサブフィールド（ID + 長さ + データ）、XFL の enum
- tar: pax 拡張ヘッダ（'x' / 'g'）のレコード（長さ・キーワード・値）、GNU の長い名前（'L' / 'K'）、更新日時の数値、GNU・Solaris の typeflag
- 7z: Start Header と Next Header の CRC32 の検証、ヘッダの全体（kHeader の MainStreamsInfo・FilesInfo、kEncodedHeader の圧縮ヘッダの位置と圧縮方式）。7z の可変長整数、フォルダとコーダー（BCJ2 のような複数の入出力を含む）、LZMA / LZMA2 のプロパティ、CRC の並び（ビット列で有無を示す形を含む）、ファイル名・時刻・属性。Packed Streams の範囲
- LZ4: フレームの並び（連結）、スキッパブルフレーム、レガシーフレーム、無圧縮ブロックのフラグ

**DSL の書き方の工夫**: 7z のストリームの個数（フォルダの出力ストリームの合計など）は、親に初期値の virtual を置き、各要素で「親の値 + この要素の分」の virtual を定義して値の昇格で親の値を置き換える形で数える。昇格は配列の中を走査しないので、最終値は配列の後ろに別名の virtual で写す

**実在のツールでの確認**: Info-ZIP の zip（拡張フィールド付き・`zip -` の Zip64）、Python の zipfile（UTF-8 の名前・コメント・強制 Zip64・シークできないストリームへの Data Descriptor 付きの出力）、gzip -9、Python の tarfile（ustar / GNU / pax）と macOS の bsdtar（pax の LIBARCHIVE / SCHILY の拡張属性）、7-Zip 26.03 の 7zz（既定の kEncodedHeader、`-mhc=off` の圧縮なしヘッダ、非ソリッド、Copy、BCJ2 + LZMA × 3）、lz4 1.10（`-BX` の Block Checksum、`--content-size`、`-l` のレガシー、スキッパブルフレームとの連結）で作ったファイルを読んで値が合うことを確かめた。テスト用に組み立てたファイルも、unzip・zipfile・gzip・tarfile・7zz（t / l）・lz4 -d・ImageMagick で読めることを確かめた。旧定義で ZIP のストリーム出力・LZ4 の Block Checksum・BMP の V5 が崩れることも確かめた

**出力の大きさ**: 5 件のゴールデン（JSON）は合計 39,498 → 66,750 バイト（+69%）。ZIP は Central Directory と Local File Header の両方を表示し、日時・フラグをビットフィールドに分けた分が大きい

**残した課題**（各定義の「対応していないもの」に記載）

- ZIP: 分割アーカイブ、Central Directory の暗号化、コメント付きで先頭側に別の End of Central Directory 署名を含むファイル（先頭から探すため）、CP437 のファイル名、暗号化データ・Deflate64・LZMA 等の展開、CRC-32 の検証
- gzip: 複数メンバーの連結（BGZF を含む）。deflate の終わりが分からないため
- tar: ヘッダのチェックサムの検証（単純な合計のアルゴリズムが無い）、GNU の base-256 表現、GNU 形式の prefix の位置の atime 等
- 7z: 圧縮ヘッダの展開（そのため既定の 7z はファイル一覧まで見えない）、ファイル名の 1 件ずつの分解
- LZ4: ブロックの展開、Header Checksum の検証（xxHash-32 の 2 バイト目）

### 実行形式・バイトコードの見直し（PR: 実行形式・バイトコード）

**直したバグ**

- Java: コンスタントプールを「constant_pool_count − 1 個のエントリ」として読んでいたため、Long / Double（2 スロットを使う）を含むクラスで、コンスタントプールの後ろを 2 エントリ分ずつ読みすぎてデコードエラーになっていた（Java 8 の実際のクラスファイルで確認）。1 スロットずつ読み、2 スロット目は空の要素にする
- Mach-O: LC_BUILD_VERSION の値を 44（本当は LC_ENCRYPTION_INFO_64 = 0x2C）、LC_SOURCE_VERSION を 34（本当は LC_DYLD_INFO = 0x22）としていたため、実際のファイルの LC_BUILD_VERSION（0x32）が分解されていなかった。テストの Generator も誤った値を使っていた。ロードコマンドの struct がリトルエンディアン固定で、ビッグエンディアン（PowerPC）の Mach-O を読めなかった。ユニバーサルバイナリ（macOS の /bin/ls など）を読めなかった
- ELF: プログラムヘッダを e_phoff へ seek せず ELF ヘッダの直後から読み、e_phentsize / e_shentsize も使っていなかった
- WASM: メモリの limits の最大値（flags の bit 0）を読んでいなかった（セクションのサイズで区切られるため後ろはずれない）
- JSON スキーマ（schemas/bdef.schema.json）の型名に uleb128 / sleb128 / vlq と別名が無く、これらを使う定義がスキーマで ✗ になっていた

**足した構造**

- ELF: セクション名（.shstrtab）、セクションの中身（シンボル表と名前・文字列表・再配置・動的セクションと DT_NEEDED などの名前・ノートと GNU の種類・zlib / Zstandard の圧縮セクションの展開）、PT_INTERP のパス、拡張番号（e_shnum = 0 / SHN_XINDEX）、gABI 4.3 の OS ABI・シンボルの属性・GNU の拡張の enum
- PE: DOS スタブ、データディレクトリの番号の名前、エクスポート表・インポート表（PE32 / PE32+）・デバッグディレクトリ（CodeView の PDB のパス）・ベース再配置・証明書の表・CLR ヘッダとメタデータのルート（.NET）。RVA は各セクションヘッダで「そのセクションに入っているか」を確かめて、そのセクションの差でファイル上の位置に直す
- Mach-O: ユニバーサルバイナリ（fat / fat64）、ビッグエンディアン、ロードコマンドの大半（dylib の名前と版・rpath・linkedit・dyld_info・version_min・source_version・暗号化・linker_option・note・fileset）、シンボル表と名前、コード署名（SuperBlob・CodeDirectory の識別子・エンタイトルメント）
- Java: Java SE 25〜27 の版、クラス名・メンバー名・属性名の解決、属性の中身（Code・ConstantValue・Exceptions・SourceFile・LineNumberTable・LocalVariableTable・InnerClasses・BootstrapMethods など）
- WASM: WebAssembly 3.0 の全セクション（GC の型・インポート・テーブル・グローバルと定数式・要素・データ・データの数・タグ・関数の本体のローカル変数）、コンポーネントの層の判別、name / producers / target_features などのカスタムセクション

**実在のファイル・ツールでの確認**:

- ELF: clang（LLVM 23）で x86-64 / i386 / AArch64 / PowerPC / MIPS64 / RISC-V 向けに作ったオブジェクトファイル（zlib で圧縮したデバッグ情報付き）と、SkiaSharp の libSkiaSharp.so（ARM 32 ビット・AArch64、約 10 MB）を読み、セクション・シンボルの数・ビルド ID・再配置の数を llvm-readelf と照合した
- PE: .NET SDK の ilc.exe・clrjit（PE32+ x64）、SkiaSharp の ARM64 の DLL、onigwrap の x86 の DLL、このリポジトリの .NET のアセンブリを読み、インポート・エクスポート・PDB のパス・証明書・CLR のメタデータを llvm-readobj と照合した
- Mach-O: macOS の /bin/ls・/usr/lib/dyld（ユニバーサルバイナリ）、Homebrew の dylib、clang のオブジェクトファイル（arm64・x86_64・i386）を読み、依存ライブラリと版・シンボル・署名の識別子を otool・nm・codesign と照合した
- Java: Homebrew に同梱の jar（concurrent-ruby・Java 8）と JRE の jrt-fs.jar のクラスファイル 169 個、gettext の古いクラスファイルを読み、すべてエラーなく読めることを確かめた
- WASM: clang の wasm32 のオブジェクトファイルと .NET の dotnet.native.wasm（約 3 MB）を読み、セクションのサイズを llvm-objdump と照合した
- テスト用に組み立てたファイルも実際のツールで確かめた:
  - ELF: llvm-readelf が同じ値を表示する
  - PE: llvm-readobj がインポート・エクスポート・再配置・PDB のパスを読める
  - Mach-O: lipo / llvm-objdump / nm で読め、codesign が識別子とエンタイトルメントを表示し、codesign -v の検証も通る
  - Java: JRE 25 が -Xverify:all で読み込んで実行できる
  - WASM: Node.js 24 の WebAssembly.validate が通り、インポートとエクスポートを読める

**出力の大きさ**: 5 件のゴールデン（JSON）は合計 48,519 → 84,063 バイト（+73%）

**残した課題**（各定義の「対応していないもの」に記載）

- ELF: シンボルのバージョン・ハッシュ表・.eh_frame、セクションの無いファイルの PT_NOTE / PT_DYNAMIC
- PE: リソース・例外・TLS・ロード構成・遅延読み込み、ほかのセクションにまたがる名前、Rich ヘッダ、メタデータの表の中身
- Mach-O: dyld の情報（chained fixups など）の中身、要件と CMS の署名の中身
- Java: バイトコードの逆アセンブル、StackMapTable・アノテーション・Module・Record の中身
- WASM: 関数の本体の命令、コンポーネントモデル

### 音声・映像の見直し（PR: 音声・映像）

**直したバグ**

- WAV: 大きさが奇数のチャンクの後ろの詰め物 1 バイトを読んでいなかった（次のチャンクから 1 バイトずれる）。cbSize を PCM 以外のときに必ず読んでいたため、16 バイトの fmt の非 PCM で範囲外になり、18 バイトの fmt の PCM では cbSize が表示されなかった（fmt の大きさで判定する）。RF64（ffmpeg の `-rf64` の出力）の data の大きさ 0xFFFFFFFF をそのまま使い、デコードエラーになっていた
- MP3: ID3v2 タグを必須にしていたため、タグの無い MP3 でデコードエラーになっていた。ID3v2.4 のフレームの大きさ（シンクセーフ整数）を普通の整数として読み、範囲外になっていた。最初のフレームのヘッダしか読んでいなかった
- Ogg: ページの中身の大きさを次の 'OggS' までとしていた（`until_marker`）ため、中身に 'OggS' のバイト列を含むページで分割を誤っていた。segment_table の合計で決める
- MIDI: ランニングステータス（状態バイトを省いたイベント）に対応しておらず、実際のファイル（Am-s1-full.mid）で 1,351 個のイベントのうち途中から誤読していた（旧定義は 1,160 個として読んでいた）。MThd の後ろの未知のチャンク（XF の XFIH など）を MTrk として読んでいた
- AVI: OpenDML（AVI 2.0）の RIFF 'AVIX'・indx / ix##・dmlh・strn を読めず、フラグも数値のままだった
- common/isobmff: ftyp の互換ブランドと hdlr の名前をバイト列のまま表示していた（ブランドの並び・UTF-8 の文字列として読む）

**足した構造**

- WAV / common/riff: WAVEFORMATEX と WAVEFORMATEXTENSIBLE（有効ビット数・スピーカーの配置のフラグ・SubFormat）を common/riff に移し、AVI の音声の strf と共有した。RF64 / BW64 と ds64、BWF の bext、cue、smpl、iXML、LIST 'INFO'。WAV の fmt のフィールド名は WAVEFORMATEX の名前（`wFormatTag` など）に変わった
- AVI: OpenDML のスーパーインデックスと標準インデックス（キーフレームの判定を含む）、RIFF 'AVIX'、dmlh、strn、avih / strh / idx1 のフラグ、rcFrame
- MP4 / common/isobmff: サンプルエントリ（avc1 / hvc1 / mp4a など。映像・音声のエントリの中の子 box）、avcC・hvcC・esds（MPEG-4 の記述子と AudioSpecificConfig）・dOps・pasp・btrt・colr、フラグメント（mvex / trex / mehd・moof / mfhd / traf / tfhd / tfdt / trun・mfra / mfro・sidx・styp）、meta と iTunes のメタデータ（ilst・data）、ctts・co64・stss、tkhd / mdhd のフラグと言語、64 ビットの大きさと uuid
- MP3: ID3v2.2〜2.4（拡張ヘッダ・テキスト・コメント・歌詞・画像・URL・PRIV・UFID のフレーム、文字コード 4 種、詰め物）、全フレームのヘッダとビットレート・サンプリング周波数・フレーム長、Xing / Info・VBRI ヘッダ、末尾の ID3v1 / ID3v1.1
- FLAC: 最初の音声フレームのヘッダ（同期コードの検証）と音声フレームの範囲、メタデータブロックのヘッダのビットフィールド、STREAMINFO のチャンネル数・ビット数
- Ogg: パケットの種類の判別、Vorbis の識別・コメントヘッダ、OpusHead / OpusTags、Ogg FLAC、Speex、Theora の識別ヘッダ
- FLV: PreviousTagSize の検証（タグの大きさ + 11）、AMF0（onMetaData の連想配列・オブジェクト・配列・日付・長い文字列の再帰）、Enhanced RTMP の拡張ヘッダと FourCC、AVC の AVCPacketType と表示時刻のずれ（符号付き）、AAC の AACPacketType、タイムスタンプの拡張
- MIDI: ランニングステータス（状態変数で直前の状態バイトを引き継ぐ）、イベントの種類とチャンネル、メタイベント（テキスト・テンポと BPM・拍子・調・SMPTE オフセット）、SMPTE の時間の単位
- MKV: 先頭コメント・全フィールドの説明、Block（0xA1）と CRC-32 要素

**実在のツールでの確認**: ffmpeg 9.0.2 で作った WAV（PCM・WAVEFORMATEXTENSIBLE・RF64・浮動小数点）、AVI（MJPEG + PCM）、MP4（H.264・HEVC・フラグメント・Opus）と M4A（ALAC）、MP3（ID3v2.4・VBR の Xing・タグなし・途中で切ったもの）、FLAC（画像とタグ付き・ステレオ）、Ogg（Vorbis・Opus・FLAC）、FLV（H.264 + AAC・古いコーデック）、MKV / WebM を読み、すべてエラーなく読めること、値が ffprobe と合うことを確かめた。MIDI は手元の実際のファイル 2 つ（ランニングステータスを含む）を読んだ。テスト用に組み立てたファイルも ffprobe で読め、コーデック・チャンネル・タグ（Opus の ARTIST、ID3v2.4 の UTF-8 / UTF-16 のタイトル、MP4 の ©nam、FLV の onMetaData）が意図どおりに表示されることを確かめた。旧定義では RF64・タグの無い MP3・ID3v2.4・中身に 'OggS' を含む Ogg がデコードエラーに、ランニングステータスの MIDI が ✗ になることも確かめた

**出力の大きさ**: 9 件のゴールデン（JSON）は合計 319,881 → 407,068 バイト（+27%）。common/isobmff の変更で HEIF のゴールデンも 30,131 → 33,004 バイトになった。AVI のゴールデンの元（CreateMinimalAvi）は avih をわざと 52 バイトに切り詰めたサンプルで、エラーは以前と同じ

**残した課題**（各定義の「対応していないもの」に記載）

- MP4: サンプルの中身、avcC / hvcC の SPS・PPS の中身、QuickTime の古い形式、サンプルグループ・暗号化・HDR のメタデータの box
- MP3: 音声データの分解、ID3v2 の非同期化の解除と圧縮・暗号化されたフレーム、APEv2・Lyrics3、フリーフォーマットのフレーム長
- WAV: data 以外のチャンクの 64 ビットの大きさ（ds64 の表）、iXML 以外の XML・ADM のメタデータ、ID3 チャンクの中身
- AVI: フレームの中身、vprp、フィールドの索引
- FLAC: 2 つ目以降の音声フレームの分割とサブフレーム、CRC の検証、先頭の ID3v2
- Ogg: ページの CRC の検証（Ogg の非反転の CRC-32 のアルゴリズムが無い）、複数ページにまたがるパケット、Vorbis の設定ヘッダ
- FLV: Enhanced RTMP の FourCC より後ろ、暗号化されたタグ、AMF3
- MIDI: RMID、テキストの文字コードの判定（Shift_JIS）、システムエクスクルーシブの中身
- MKV: レーシングの分割、CRC-32 の検証、ContentEncoding の解除、サイズ不定の Cluster の連続

### データ・その他の見直し（PR: データ・その他）

**直したバグ**（旧定義で実際のファイルを読んで確かめたもの）

- SQLite: 可変長整数を LEB128（リトルエンディアンの 7 ビットずつ）として読んでいた。SQLite の可変長整数はビッグエンディアンの 7 ビットずつなので、128 以上の値（ペイロードの長さ・行 ID）を誤読していた。sqlite3 が作った普通のデータベースでも、スキーマの行が 128 バイト以上だと `81 3D`（189）を 7809 と読んでデコードエラーになった。読むのは 1 ページ目だけで、記録（行の列の値）も分解していなかった
- pcap: マジックがリトルエンディアン・マイクロ秒の形しか無く、ビッグエンディアンとナノ秒（0xA1B23C4D）の pcap はパケットの長さを逆のバイト順で読んでデコードエラーになった。pcapng（Wireshark の既定の形）も読めなかった。IPv4 の中身を total_length ではなくパケットの残りで区切っていたため、60 バイトに満たない Ethernet のフレームの詰め物が TCP / UDP のペイロードに入っていた
- X.509: 版の [0] を必須にしていたため、版 1 の証明書でデコードエラーになった。長さは 2 バイトまで（64 KB 未満）しか読めなかった
- MessagePack: 入れ子の値の format_byte が親の format_byte を上書きし（名前の同じ値の昇格）、マップの最後の値が fixarray だと、マップの後ろの値を配列の要素として読んでいた（`81 A1 61 91 01 2A` の 42）。浮動小数点数と文字列をバイト列のまま表示し、ファイルの先頭の値しか読まなかった
- CBOR: 不定長の値を「残り全部」として読み、後ろの値を失っていた。浮動小数点数をバイト列、負の整数を引数のまま表示し、ファイルの先頭の値しか読まなかった
- FAT: FAT の種類を root_entry_count で判定していた（仕様はクラスタ数）。テストデータの「FAT16」のイメージはクラスタ数 16 で、仕様では FAT12。FAT32 のルートディレクトリ（クラスタの連鎖）を読んでおらず、長いファイル名も組み立てていなかった
- DNS: ヘッダしか読まず、AD / CD のビットを予約のビットとまとめていた
- テストデータの Generator の誤り: 最小の DNS の質問がクラスの途中で終わっていた、最小の X.509 の公開鍵の情報の中身が `DE AD BE EF` だけだった、最小の SQLite がページの途中で切れていて空のページの cell_content_offset が 0 だった（sqlite3 が壊れていると判定する）、最小の Parquet・PDF がフッタ・構造を持たなかった。どれも正しい形に直した

**足した構造**

- Parquet: フッタの FileMetaData を Thrift Compact Protocol として再帰的に分解する。構造体の種類（FileMetaData・SchemaElement・RowGroup・ColumnChunk・ColumnMetaData・Statistics・PageHeader など 15 種）を親のフィールドの番号から決め、フィールドの名前と値の列挙型（物理型・繰り返し・変換型・符号化・圧縮方式・ページの種類）を付ける。ColumnMetaData の data_page_offset / dictionary_page_offset から各列の最初のページのヘッダを読む
- PDF: ヘッダ・バイナリのコメント・改訂（増分更新）ごとの間接オブジェクト・ストリーム（FlateDecode は zlib で展開）・相互参照表と各エントリの位置の確認・トレーラ・startxref の位置の確認。相互参照ストリームだけの PDF 1.5 以降と、リニアライズされた PDF も読める。キーワードの検索が後ろまで走らないよう、オブジェクトの続きは endobj の後ろの最初の文字で、ストリームの有無は endobj までの範囲で判定する
- SQLite: 全ページ（B 木の 4 種類・空きリストのトランク・それ以外）、セル（表の葉・表の内部・索引の葉・索引の内部）、記録のヘッダとシリアル型ごとの値（24 / 48 ビットの符号付き整数、UTF-8 / UTF-16LE / UTF-16BE の文字列）、ページ内に置けるペイロードの計算とあふれページの番号
- pcap / pcapng: pcap の 4 つのマジック（バイト順 × 時刻の単位）、pcapng の SHB・IDB・EPB・SPB・NRB・ISB とオプション。リンク層は Ethernet（VLAN）・NULL・LOOP・RAW・IPv4・IPv6・Linux SLL / SLL2。ARP、IPv4（total_length で区切る）・IPv6 と拡張ヘッダ、TCP のオプション、UDP、ICMP / ICMPv6。MAC・IP アドレスの文字列
- DNS: 質問と回答・権威・追加の RR。名前はラベルと圧縮ポインタを再帰的にたどって完全な名前にする。A・AAAA・NS・CNAME・PTR・DNAME・SOA・MX・TXT・SRV・HINFO・CAA のデータ、EDNS の OPT
- Protobuf: グループ（ワイヤ型 3 / 4）の再帰、ジグザグ符号化の値、8 / 4 バイトの値の浮動小数点数としての値、長さ付きの値の UTF-8 の文字列としての値
- MessagePack: 値の並び、全形式、浮動小数点数・UTF-8 の文字列、タイムスタンプの拡張型（32 / 64 / 96）
- CBOR: 値の並び（RFC 8742）、引数の 1〜8 バイト、不定長のバイト列・文字列・配列・マップ（break まで）、タグ（IANA の主な番号）、単純値、半精度・単精度・倍精度の浮動小数点数
- X.509: DER の TLV の汎用の分解（長さ 4 バイトまで、OID のドット区切りと約 70 個の名前、整数・文字列・時刻）と、証明書の名前付きの層（版の有無・発行者・有効期間・主体・公開鍵（RSA の鍵は DER として分解）・一意の ID・拡張と extnValue の中の DER）。連結した証明書を順に読む
- FAT: 仕様どおりのクラスタ数による FAT の種類、FSInfo、FAT の表（先頭 512 エントリ）、ルート・サブディレクトリを FAT の連鎖でたどる、長いファイル名（UTF-16）の組み立て、日時の文字列、ファイルの中身の先頭。壊れた・細工したイメージで展開し続けないよう、読むディレクトリのクラスタを 1024 個までにする（状態変数）
- OTF: TTC、head・hhea / vhea・maxp・name（文字列）・cmap（形式 0・4・6・12・13・14）・OS/2（版 0〜5）・post（版 2 のグリフ名）・fvar（軸とインスタンス）・GSUB / GPOS（スクリプト・機能の一覧）・CFF / CFF2・DSIG のヘッダ

**エンジンの変更**（別の要望として記録）

- REQ-192: `padding: true` の virtual をツリーに出さない。Parquet・DNS・PDF・FAT・X.509 などの作業用の値を隠す
- REQ-193: エラー継続モードで読めなかった bitfield のエントリの名前を未定義にする。途中で切れた CBOR で、読めなかった値に親の主型が見えて再帰し、メモリ不足になっていた（ファズで見つかった）

**DSL の書き方の工夫**

- 入れ子の struct は、size が無いと親と同じ変数のスコープを使う。再帰する struct（DNS の名前・CBOR / MessagePack の値・X.509 の TLV・Thrift の構造体・FAT のディレクトリ）は、エンディアンを書いた形（`endianness:` + `fields:`）にして独自のスコープを持たせ、入れ子の値が親の同じ名前の値を上書きしないようにした
- 値の昇格は、入れ子の struct の子まで再帰的に親のスコープへ書く（配列の中は走査しない）。Thrift のフィールドは、入れ子の値の後ろに「構造体の種類」「直前のフィールドの番号」を書き直す virtual を置いた。FAT のサブディレクトリは `repeat_count` の配列として読み、昇格が届かないようにした
- 独自のスコープを持つ struct を要素にした `repeat_until` は、条件から要素の値が見えない（要素のスコープは条件を評価する前に閉じる）。CBOR の不定長の並び・FAT のディレクトリは、既定値の virtual を置いた `repeat_while` にした
- 現在の位置は `remaining` からしか分からないので、ファイルの先頭から大きさの無いスコープが続く定義（Protobuf・PDF・X.509 の名前付きの層）では `file_size - remaining` で先読みの位置を求めた。size 付きのスコープの中では使えないので、pcap の RAW の IP の版は、先頭のバイトを読んでから分ける形（ip_packet）にした
- 要素が 0 バイトで成功しうる繰り返し（Thrift の型の分からない値の後ろ）は、壊れた要素数で止まらなくなる（エンジンの「進まない要素で打ち切る」はエラーの要素にしか効かない）。Thrift のリスト・マップの要素数を残りのバイト数で抑えた

**実在のツールでの確認**:

- Parquet: pyarrow 22.0（parquet-cpp-arrow）で作った 3 ファイル（圧縮なしの版 1 のデータページと辞書、Snappy の版 2 のデータページと 2 つの行グループ、Zstandard の入れ子のリスト）を読み、各列の data_page_offset・dictionary_page_offset が pyarrow のメタデータと一致し、そこにページヘッダ（DATA_PAGE / DATA_PAGE_V2 / DICTIONARY_PAGE）があることを確かめた。テスト用の Parquet も pyarrow で読めて値が一致する
- PDF: img2pdf・qpdf 12（QDF・オブジェクトストリーム・AES-256 の暗号化・リニアライズ）と手で組み立てた増分更新の PDF を読んだ。テスト用の PDF は qpdf --check が通る
- SQLite: sqlite3 3.51 で作ったデータベース（ページサイズ 1024、各種の型・索引・あふれ・削除による空きリスト、UTF-16LE）を読み、あふれるセルのページ内の大きさが仕様の式と一致することを確かめた。テスト用のデータベースは PRAGMA integrity_check が ok で、SELECT で同じ値が返る
- pcap: 組み立てた pcap（リトルエンディアン・マイクロ秒、ビッグエンディアン・ナノ秒、RAW、NULL）と pcapng を tcpdump 4.99 が同じ内容で読むことを確かめた（DNS の応答の中身も tcpdump の表示と一致）
- X.509: OpenSSL 3.6 で作った証明書（RSA 2048 + SAN・EKU、EC P-256、Ed25519、ML-DSA-44、連結）を読み、シリアル番号・有効期間・拡張が openssl x509 -text と一致することを確かめた。テスト用の証明書（版 1、拡張付き）も openssl が読める
- FAT: macOS の hdiutil / newfs_msdos で作った FAT12・FAT16・FAT32 のイメージに長いファイル名・日本語の名前・入れ子のディレクトリ・40 個のファイルを入れて読み、すべて見つかることを確かめた。テスト用の FAT12 のイメージは macOS でマウントでき、fsck_msdos が問題を報告しない
- OTF: macOS のシステムのフォント（Helvetica.ttc・SFNS.ttf（可変フォント）・LastResort.otf・Arial.ttf・ヒラギノ角ゴシック W3.ttc の 7.8 MB）を読み、fvar の軸・インスタンスの数・グリフ数・cmap の形式・GSUB の機能の数・TTC のフォントの数が fontTools 4.63 と一致することを確かめた。テスト用のフォントも fontTools が読める
- Protobuf・MessagePack・CBOR: Python の protobuf 4.25 と msgpack 1.1 が出力したデータ、RFC 8949 付録 A の例（72 個）を読み、値が一致することを確かめた

**出力の大きさ**: 9 件のゴールデン（JSON）は合計 62,116 → 597,492 バイト。Parquet のゴールデンは 434,993 バイトで、Thrift のフィールドごとに種類・番号・作業用の値を持つため大きい（1 列 3 行のファイル）。JSON は日本語を \u で書くので、説明の分が大きい

**残した課題**（各定義の「対応していないもの」に記載）

- Parquet: ページのデータ（圧縮・符号化された値）、2 つ目以降のページ、暗号化されたフッタ、インデックス・ブルームフィルタ
- PDF: 辞書・配列の構文の分解、FlateDecode 以外のフィルタと予測子、相互参照ストリーム・オブジェクトストリームの中身、暗号化されたストリーム
- SQLite: あふれページに続く記録、9 バイトの可変長整数、2 つ目以降の空きリストのトランク・ポインタマップの判別、WAL
- pcap: チェックサムの検証、再組み立て、アプリケーション層（DNS などはバイト列）
- DNS: TCP の長さの前置き、DNSSEC・SVCB などのデータ
- X.509: PEM、BER、CSR・CRL など証明書以外の名前付きの分解、署名の検証
- FAT: パーティション表、exFAT、ファイルの中身の 2 つ目以降のクラスタ、LFN のチェックサム
- OTF: 表のチェックサム、WOFF、glyf・CFF の中身、ルックアップ、16.16 の固定小数点数の小数での表示
- MessagePack / CBOR: 2^63 以上の整数、CBOR の半精度の値・多倍長整数

### 気づき・今後の課題

- 入れ子の struct の値の扱い（大きさの無い struct は親とスコープを共有する・昇格は入れ子の struct の子まで再帰する・独自のスコープの要素の値は repeat_until の条件から見えない・現在の位置を式で得られない）は、再帰する定義を書くたびに回避策が要った（データ・その他の「DSL の書き方の工夫」）。現在の位置の変数（`_offset` など）と、入れ子の struct に独自のスコープを持たせる指定を DSL に足すかは別要望で検討する → REQ-194（`_offset`）・REQ-195（独自のスコープの指定と昇格）・REQ-196（独自のスコープの要素と repeat_until）として起こした。3 件とも実装し、同梱定義の回避策を置き換えた（データ・その他の「DSL の書き方の工夫」の回避策は、現在の定義には残っていない）
- 0 バイトで成功する要素が壊れた要素数だけ続くと、繰り返しが終わらない（エラーの要素にしか「進まない要素で打ち切る」ガードが働かない。Parquet のファズで見つかり、要素数を残りのバイト数で抑えて避けた） → REQ-197 で解消（0 バイトの要素が 65536 個続いたら打ち切る）。Parquet の要素数の抑えは、壊れた要素数で無駄に読まないよう残した

- size 付きのスコープ（size を持つ struct・switch）の中から、その範囲の外へ seek して読むと「範囲外」のデコードエラーになる。ELF のセクションの中身では、別のセクションの文字列を引くために switch の size を外し、各ケースで sh_size を使って大きさを決める形で避けた → REQ-191 で解消（範囲の外への seek は、行き先を含む外側の境界の中で読む）。PR #21・#22 のマージ後に ELF の回避策を外し、switch に size を戻した（JSON 出力は変わらない）

- `size` と `repeat` を一緒に指定したフィールドは、要素の値の昇格が親のスコープに届かない（BinaryDecoder の配列のバウンダリスコープで捨てられる）。ZIP の拡張フィールドは struct で包み、その中で Zip64 の値を使う形で避けた。エンジンで昇格を親に渡すかは別要望で検討する → REQ-190 で解消（size 付きの繰り返しでも値が外側のスコープに残る）。ZIP の書き直しは REQ-190 の実装メモを参照
- 式に DSL に無い識別子（`false` など）を書いても検証では警告されず、その分岐を通るファイルで初めてデコードエラーになる（BMP のバグ）。未定義の変数の参照を検証で見つけられるとよい（値の昇格があるので静的に決めにくい） → REQ-189 で解消（未定義の名前・未知の関数を警告する）

- 見直しの中で DSL の制約のために残した「対応していないもの」のうち、次を要望として起こした: pcap の中の DNS → REQ-198、インターネットチェックサム・Ogg の CRC-32・OTF の表のチェックサム → REQ-199、半精度の浮動小数点数（CBOR）→ REQ-200、2^63 以上の uint64（MessagePack・CBOR）→ REQ-201。REQ-198 は実装した（pcap は formats/dns.bdef.yaml をインポートして UDP の 53 / 5353 / 5355 番を DNS として読む）。REQ-199 も実装した（pcap の IPv4 のヘッダと ICMP、ogg のページ、otf の表（head 以外）のチェックサムを検証する）。REQ-200 も実装した（型 float16 を足し、CBOR の半精度の浮動小数点数を値で表示する）

- XZ の見直しで、description が英語のみ（`Index Indicator（0x00）`）のものも検査で見つかった。原語を括弧で添えるのはよいが、日本語の説明を必ず含める
