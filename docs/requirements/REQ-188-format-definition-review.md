# REQ-188: 既存フォーマット定義の見直し（仕様の版・日本語の説明・DSL の制約の解消）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | implementing |
| 優先度 | 中 |
| 依存 | なし（REQ-183 のドキュメント整合性テスト、REQ-186 の「同梱定義の警告 0 件」テストの上で行う） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

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

1. [ ] `docs/format-authoring.md` に定義の書き方の規約があり、README か architecture.md から参照されていること
2. [ ] 全 42 定義（共通ライブラリを含む）の先頭コメントに準拠仕様（名称・版）と、対応していないものが書かれていること
3. [ ] 全フィールドに日本語の description があること（テストで固定され、「対応待ち一覧」が空になっていること）
4. [ ] 背景 3 の差分の候補のそれぞれについて、定義に足したか、足さない理由を先頭コメントに書いたかのどちらかであること
5. [ ] 背景 4 の parquet / pdf の末尾からの読み取りが、今の DSL で書き直されているか、書き直さない理由が書かれていること
6. [ ] 足した構造ごとに、テストデータ（Generator）と解析テストがあること。可能なものは実在のツール（`xz` / `ffmpeg` など）で作ったファイルでもデコードを確認し、実装メモに記録すること
7. [ ] 同梱定義の検証警告が 0 件のままであること（REQ-186 のテスト）
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過。ゴールデンファイルは見直した定義の分だけ更新する）

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

- [ ] docs/format-authoring.md — 新規（定義の書き方の規約）
- [ ] README.md / docs/architecture.md — 規約への参照
- [ ] docs/dsl-reference.md — 変更なし（見直しで DSL の説明の不足が見つかれば追記）
- [ ] CLAUDE.md — 定義の書き方の規約への参照（フォーマット定義を書くときに読むもの）

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
| アーカイブ・圧縮 | zip / gzip / tar / 7z / lz4 | 未着手 |
| 実行形式・バイトコード | elf / pe / macho / java-class / wasm | 未着手 |
| 音声・映像 | mp3 / mp4 / wav / flac / ogg / avi / flv / midi / mkv / common/riff / common/isobmff | 未着手 |
| データ・その他 | sqlite / parquet / pdf / pcap / dns / protobuf / msgpack / cbor / x509 / fat / otf | 未着手 |

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

### 気づき・今後の課題

- XZ の見直しで、description が英語のみ（`Index Indicator（0x00）`）のものも検査で見つかった。原語を括弧で添えるのはよいが、日本語の説明を必ず含める
