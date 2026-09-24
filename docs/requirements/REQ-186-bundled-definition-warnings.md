# REQ-186: 同梱定義の検証警告の解消

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（REQ-185 の前提） |
| 作成日 | 2026-09-24 |
| 更新日 | 2026-09-24 |

## 背景・動機

REQ-185（GUI での定義の診断の表示）の設計中に、同梱の 38 定義のうち 15 定義で `binanalyzer validate` が警告を計 24 件出すことが分かった（2026-09-24）。このまま GUI に診断を出すと、PNG では出ないが MP4 や ZIP では常に警告が出る状態になり、利用者の書き間違い（REQ-184 の VAL123）が埋もれる。

1 件ずつ調べると、大半は定義ではなく検証器かエンジンの問題だった。

| 件数 | コード | 定義 | 内容 | 原因 |
|---|---|---|---|---|
| 3 | VAL107 | dns `rcode` / gif `disposal_method` / lz4 `block_max_size` | 未使用の enum | **検証器の誤検知**。bitfield のエントリ（`bits:` + `enum:`）から参照されているが、`ValidateUnusedEnums` はフィールドの `enum:` しか数えていない |
| 5 | VAL109 | avi / wav / webp（`_riff_common`）、heif / mp4（`_isobmff_common`） | 到達不能な struct | **検証器の誤検知**。共通ライブラリ（REQ-155）がルートとして置くダミーの struct。インポートした側から到達できないのは当然 |
| 4 | VAL103 | zip `version_needed_os` ×2 / `version_made_by_os`、protobuf `wire_type` | `virtual` に enum | **エンジンの不足**。`virtual` の `enum:` は無視され、ラベルが出ない（`version_needed_os: = 3`）。定義の意図（OS 名・ワイヤ型名の表示）は妥当 |
| 5 | VAL107 | mp3 `mpeg_version` / `mpeg_layer` / `mp3_bitrate` / `mp3_sample_rate` / `channel_mode` | 未使用の enum | enum は用意されているが、値を持つのが `virtual`（ヘッダのビットを切り出したもの）なので付けられなかった。上の不足を直せば付けられる |
| 1 | VAL107 | tiff `tiff_compression` | 未使用の enum | 圧縮方式は IFD エントリの値（タグ 259）なので、付ける先のフィールドが無かった。`virtual` に enum が付けば、タグ 259 のインライン値として付けられる |
| 4 | VAL109 | java-class `code_attribute_body` / `exception_entry` / `constantvalue_attribute_body` / `exceptions_attribute_body` | 到達不能な struct | 属性名（コンスタントプールの文字列）で振り分けるつもりの定義。long / double が 2 スロットを使うため配列の添字とインデックスがずれ、今の DSL では正しく引けない。`info` の説明に同じレイアウトが書いてある |
| 1 | VAL109 | midi `raw_data` | 到達不能な struct | 使われていない（switch の default は `raw_event_data`） |
| 1 | VAL105 | fat `extended_bpb` | switch に default が無い | 振り分けの値 `fat_type_hint` は 0 / 1 しか取らないが、検証器には分からない |

## 機能要件

### 追加する機能

- [x] `virtual` フィールドの `enum:` を有効にする。値が整数なら enum のラベル・説明を付け、出力（tree / json / html / GUI / TUI / diff）で整数フィールドと同じように表示する（csv は整数でもラベルを出さないので、virtual も値だけのまま）

### 変更する既存機能

- [x] `FormatValidator` VAL107 — bitfield のエントリの `enum:` も使用として数える
- [x] `FormatValidator` VAL109 — インポート先のファイルで定義された struct は対象外にする（本ファイルで定義した struct だけを検査する）
- [x] `FormatValidator` VAL103 — `virtual` への enum を許す
- [x] 同梱定義 — mp3 の 5 つの enum を対応する `virtual` に付ける。tiff に圧縮方式（タグ 259）の `virtual` を足して `tiff_compression` を付ける。java-class の 4 つの struct と midi の `raw_data` を削除する（java-class の属性本体のレイアウトは `info` の説明に残っている）。fat の switch に default を足す
- [x] 同梱の全定義で警告 0 件をテストで固定する

### 変更しないもの（スコープ外）

- java-class の属性をコンスタントプールの名前で振り分けること（インデックスの引き方が DSL に無い。別要望の候補）
- `formats/common/` の共通ライブラリを単体で検証したときの警告（REQ-178）
- VAL105 の判定の改善（振り分けの値が取りうる範囲の推論）

## 受入条件

1. [x] `binanalyzer validate formats/*.bdef.yaml` がすべて「OK」で、警告が 0 件であること
2. [x] bitfield のエントリだけから参照される enum に VAL107 が出ないこと。どこからも参照されない enum には従来どおり出ること
3. [x] インポート先の struct に VAL109 が出ないこと。本ファイルで定義した到達不能な struct には従来どおり出ること
4. [x] `virtual` に付けた enum のラベルが、tree / json / html / diff / GUI（ツリー・インスペクター）/ TUI で整数フィールドと同じ形で表示されること（csv は整数と同じく値のみ）。値が整数でない（真偽値・文字列）ときはラベルを付けないこと
5. [x] zip の `version_needed_os`、protobuf の `wire_type`、mp3 のフレームヘッダ、tiff の圧縮方式にラベルが表示されること
6. [x] 同梱の全定義で警告が 0 件であることをテストで固定すること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過。ゴールデンファイルは表示の変わるフォーマットだけ更新する）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodedVirtual` に `EnumLabel` / `EnumDescription`。`FormatValidator` の VAL103 / VAL107 / VAL109 |
| BinAnalyzer.Dsl | 変更なし（`virtual` の `enum:` は既に IR に入っている） |
| BinAnalyzer.Engine | `virtual` のデコードで enum を引く。`DiffEngine` でラベルを比較・表示 |
| BinAnalyzer.Output | tree / json / html / diff tree で virtual のラベル（csv は変更なし） |
| BinAnalyzer.Presentation | `NodeDisplayText` / `NodeDetailFormatter` で virtual のラベル |
| BinAnalyzer.Tui | `NodeDetailFormatter` で virtual のラベル |
| formats/ | mp3 / tiff / java-class / midi / fat |
| tests（テストデータ） | `TiffTestDataGenerator` のビッグエンディアン TIFF を仕様どおりの SHORT の置き方に修正 |
| testdata/golden | 表示の変わる定義（mp3 / tiff / zip / protobuf など）の更新 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — `virtual` に `enum:` を付けられること
- [x] docs/parser-design.md — VAL103 / VAL107 / VAL109 の説明
- [x] docs/architecture.md — `DecodedVirtual` の説明
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

### 設計方針

- **enum の引き方は整数フィールドと共通にする。** エンジンの整数フィールドは `EnumRef` から `EnumDefinition` を引いてラベルと説明を決めている。`virtual` でも、評価結果が整数（`long` など）なら同じ手順で引く。真偽値・文字列の結果にはラベルを付けない（検証器も値の型は分からないので VAL103 は外すだけにする）
- **表示は各出力の「整数のラベル」の書き方にそろえる。** tree の `= 3 "unix"`、json の `enum_label`、html のラベル表示、diff の値、GUI / TUI の詳細行（csv は整数でもラベルを出さないので変えない）。`virtual` 専用の書式は作らない
- **VAL107 は bitfield エントリの `EnumRef` も使用として数える。**
- **VAL109 は本ファイルで定義した struct だけを検査する。** ルート struct の `SourceFile` と同じ（どちらも null を含む）struct だけを対象にし、インポート先の struct は「ライブラリの使わない部品」として警告しない
- **同梱定義の手入れ。** 削除は、同じ情報が他に残っているもの（java-class は `info` の説明）と、代わりが使われているもの（midi）に限る。fat の default は `fat16_extended_bpb`（`fat_type_hint` は 0 / 1 しか取らないので到達しない）
- **警告 0 件をテストで固定する。** 同梱の全定義を読み込んで検証し、警告もエラーも無いことを確かめる（REQ-184 の VAL123 の検査を含む）

### 代替案

- **定義から `virtual` の enum を外す（zip / protobuf）。** 警告は消えるが、OS 名・ワイヤ型名を出したいという定義の意図と、mp3 / tiff で用意されていた enum が活かせない。不採用
- **警告を抑える記法（`# noqa` のようなもの）を DSL に足す。** 今回の警告は誤検知か直せるものなので要らない。不採用

### 懸念事項

- ゴールデンファイルは実ファイル（`testdata/real`、git 管理外）から作られる。表示が変わる定義の分だけ `UPDATE_GOLDEN=1` で作り直し、差分がラベルの追加だけであることを確認する

---

## 実装メモ

### 実装中の設計変更

- **tiff のインライン SHORT 値のビッグエンディアン対応（既存の誤りの修正）。** 圧縮方式の `virtual` を足す際、既存の `inline_short_value`（`value_offset & 0xFFFF`）がビッグエンディアンの TIFF で常に 0 になることが分かった。4 バイトの値欄に収まる SHORT は先頭に詰めて置かれる（TIFF 6.0）ので、BE では 32 ビット値の上位 16 ビットにある。`{byte_order == 'MM' ? value_offset >> 16 : value_offset & 0xFFFF}` に直し、新しい `compression` も同じ式にした。テスト用の `TiffTestDataGenerator.CreateBigEndianTiff` は値を 32 ビット値として書いていた（`00 00 00 40`、仕様違反）ので仕様どおり（`00 40 00 00`）に直した。dsl-reference.md の例も同じ式に更新
- **csv は変更しない。** csv は整数フィールドでも enum ラベルを出さない（値のみ）ので、virtual もそろえて値のみのままにした（受入条件 4 を修正）
- **TUI の詳細行はテストを足していない。** `Tui/NodeDetailFormatter` は Presentation の同名クラスと同じ変更で、Tui.Tests に該当のテストクラスが無い

### 結果

- `binanalyzer validate formats/*.bdef.yaml`: 38 ファイルすべて OK、警告 0 件（変更前は 15 ファイル・24 件）
- 実ファイルでの表示: zip `version_needed_os: = 0 "MS-DOS"`、protobuf `wire_type: = 2 "length_delimited"`、mp3 `mpeg_version: = 3 "MPEG1"` / `bitrate_index: = 9 "128kbps"` など、tiff（タグ 259 を含む LE / BE の TIFF）`compression: = 5 "LZW"`
- ゴールデンファイル: mp3 / protobuf / tiff / zip の 4 件を再生成。差分は `enum_label` の追加と、`description` の文言変更（mp3 の 2 項目、tiff の 1 項目）のみ

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Core.Tests / BundledWarningRulesTests | VAL107_EnumReferencedOnlyByBitfieldEntry_NoWarning / VAL107_BitfieldWithoutEnum_StillReportsUnusedEnum | 2 |
| Core.Tests / BundledWarningRulesTests | VAL109_UnreachableImportedStruct_NoWarning / VAL109_UnreachableStructInMainFile_StillReported | 3 |
| Core.Tests / BundledWarningRulesTests | VAL103_EnumOnVirtual_NoWarning / VAL103_EnumOnBytes_StillReported | 4 |
| Engine.Tests / VirtualEnumTests | IntegerResult_GetsEnumLabelAndDescription / IntegerWithoutMatchingEntry_NoLabel / BooleanResult_NoLabel | 4 |
| Integration.Tests / VirtualEnumOutputTests | Tree_ShowsLabelAndDescription / Json_HasEnumLabelAndDescription / Html_ShowsLabel / Diff_ShowsLabelsOnBothSides | 4 |
| Presentation.Tests / NodeDisplayTextTests | Virtual_WithEnum_IncludesLabel / Virtual_WithoutEnum_Unchanged | 4 |
| Presentation.Tests / NodeDetailFormatterTests | Format_VirtualWithEnum_IncludesEnumAndDescription | 4 |
| Integration.Tests / TiffParsingTests | TiffFormat_CompressionTag_HasEnumLabel（LE / BE）、TiffFormat_BigEndian_IfdEntry_DecodesCorrectly に inline 値の検証を追加 | 5 |
| Integration.Tests / GoldenFileTests（既存） | mp3 / protobuf / zip / tiff のゴールデン | 5, 7 |
| Integration.Tests / BundledDefinitionValidationTests | BundledFormat_HasNoDiagnostics（38 定義） | 1, 6 |

### 気づき・今後の課題

- java-class の属性（Code / ConstantValue / Exceptions）の構造は、属性名をコンスタントプールから引ければ展開できる。long / double が 2 スロットを使うため、インデックス → 配列要素の対応を DSL で表す手段が要る（別要望の候補）
- mp3 の `bitrate_index` / `sample_rate_index` のラベルは MPEG-1（Layer III）の表。MPEG-2 / 2.5 のフレームでは値が違うので、バージョン・レイヤーで表を切り替えるには switch で virtual を分ける必要がある
