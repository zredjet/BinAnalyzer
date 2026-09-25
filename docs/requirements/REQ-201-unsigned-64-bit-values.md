# REQ-201: 2^63 以上の符号なし 64 ビットの値を正しく表示する

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

整数のデコード結果（`DecodedInteger.Value`）と式の値は符号付き 64 ビット（long）で、`uint64` のフィールドは `(long)` に変換して持っている。2^63 以上の値は負の数になり、ツリー・JSON にもそのまま負の数で出る。

REQ-188 の見直しで、次が「対応していないもの」に残った。

- MessagePack の uint 64（`0xCF`）の 2^64 - 1 が -1 と表示される（Python の msgpack の出力で確かめた）
- CBOR の 8 バイトの引数の符号なし整数・負の整数（-1 - 引数）が誤った値になる

同じことは、値を uint64 で持つほかの形式（MP4 の 64 ビットの大きさ、ELF / Mach-O の 64 ビットのアドレスなど）でも、上位ビットが立っていれば起きる。上位ビットの立つカーネルのアドレス（0xFFFF…）を持つ ELF / Mach-O が実例になりうる。

## 機能要件

### 変更する既存機能

- [x] **`uint64` のフィールドの値を、ツリー・JSON・CSV・GUI / TUI の詳細で符号なしとして表示する**（2^64 - 1 は 18446744073709551615、16 進は 0xFFFFFFFFFFFFFFFF）
- [x] 式の中の値の扱いを設計で決める（今の符号付きのままにして、制約として DSL リファレンスに書いた）
- [x] enum・flags・bitfield のエントリ（64 ビットの幅）の値の表示も同じにする

### 変更しないもの（スコープ外）

- 2^64 以上の整数（多倍長整数）。CBOR のタグ 2 / 3 の値など
- CBOR の -2^64 までの負の整数（-1 - 引数）を正しく表すこと（long に収まらない）。値は出さず、引数を符号なしで見せる（`argument_unsigned`）

## 受入条件

1. [x] `uint64` のフィールドで 0xFFFFFFFFFFFFFFFF が 18446744073709551615、0x8000000000000000 が 9223372036854775808 と、ツリー・JSON で表示されること（16 進も符号なし）
2. [x] 2^63 未満の値の表示は変わらないこと（ゴールデンが変わらない）
3. [x] MessagePack の uint 64 の 2^64 - 1 が正しく表示されること
4. [x] 式の扱いを決めた方針どおりに、比較（`{x > 0}`）が動くこと（または制約が DSL リファレンスに書かれていること）— 符号付きのままとし、制約を書いた
5. [x] 値の編集（`FieldEncoder`）で 2^63 以上の値を書けること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `IntegerText`（型に合わせた表記・整数のリテラル）、`DecodedInteger.ValueText` / `ShowsHex`、`BitfieldValue.ValueText` |
| BinAnalyzer.Engine | 64 ビットの幅の bitfield のエントリ、編集の初期値、差分の表示 |
| BinAnalyzer.Dsl | enum の値を文字列で受けて読む（2^63 以上） |
| BinAnalyzer.Output / Presentation / Tui / Gui / Cli | 表示（ツリー・JSON・CSV・HTML・差分・詳細・GUI の enum の選択肢）、patch の enum のラベル |
| formats/msgpack・cbor.bdef.yaml | 「対応していないもの」の更新、CBOR の 8 バイトの引数の値を uint64 として読み直す |
| tests（Core・Engine・Integration・Presentation・Tui・Fuzz） | 表記・リテラル・表示・編集・差分・CBOR / MessagePack の値 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 整数型・式の値の範囲
- [x] docs/architecture.md — デコード結果のモデル

---

## 設計メモ

### 設計方針

- **案 1（表示だけ）**: 値は long のまま、`DslType == UInt64` のノードは表示・JSON で `(ulong)Value` として書く。式は符号付きのまま（制約として書く）。変更が少ない
- **案 2（値も）**: `DecodedInteger` に ulong の値を持たせ、式の評価で符号なしの比較・算術を行う。式の値の型（long / ulong / double）の組み合わせの規則が要る

### 懸念事項

- JSON の数値として 2^63 以上を書くと、JavaScript（Web 版の GUI）の Number で精度が落ちる。文字列で持つか、今の `hex` と併せて出すか
- CBOR の `value`（virtual）は式の結果なので、案 1 では符号なしにならない。CBOR の定義の側で uint64 のフィールドをそのまま見せるなどの工夫が要る

---

## 実装メモ

### 実装中の設計変更

- **案 1（表示だけ）を採った**。値は long（同じビット）のまま、表示のときに型を見て符号なしに戻す。型は `DecodedNode.DslType` で分かり、符号なし 64 ビットの型は `uint64`・`uleb128`・`vlq`（`uleb128` / `vlq` も 2^63 以上は負の値になっていた）。案 2（値も ulong）は、式の値の型（long / ulong / double）の組み合わせの規則と、全ての演算子・組み込み関数・変数の束縛の見直しが要る一方、2^63 以上の値を式で比べる実例（大きさ・位置として使う値はデータの長さより小さい）が見つからなかったので採らなかった
- **式は符号付き 64 ビットのまま**。`{x > 0}` は 2^64 - 1 で偽になる。DSL リファレンスに「整数の値の範囲」を足し、比較・算術・virtual の値・JSON の扱いを書いた
- **表記を Core の `IntegerText` 1 か所にまとめた**。`DecodedInteger.ValueText`（10 進）・`ShowsHex`（16 進を添えるか。以前の `Value is >= 16 or <= -16` は 2^64 - 1 = -1 で 16 進を出さなかった）、`BitfieldValue.ValueText`（エントリは常に符号なし）。ツリー・JSON・CSV・HTML・差分（DiffEngine・DiffTree）・Presentation / TUI の詳細・GUI の構造ツリーと Inspector・編集の初期値が使う。16 進（`0x{Value:X}`）は元から 2 の補数のビットの並びなので符号なしと同じで、変えていない
- **JSON の `value` は数値で書く**（`ulong` の 18446744073709551615）。文字列にすると 2^63 未満の値と形が変わり、既存の利用者が壊れる。JavaScript で読むときの精度は `hex` で補う（Web 版・デスクトップの GUI の画面は JSON を読まずデコード結果を直接使う。Web 版の旧ページ `/classic` は JSON をダウンロード用に作るだけ）
- **enum の値の 2^63 以上**: YAML の値を long で受けていたので `0xFFFFFFFFFFFFFFFF` が「Exception during deserialization」になった。文字列で受けて `IntegerText.TryParseLiteral`（10 進・`0x` の 16 進、-2^63 〜 2^64 - 1、`_` 区切り可）で読み、同じビットの long にする。読めない値は enum とエントリの名前を示すエラーにした。GUI の enum の選択肢と CLI の patch のラベルの解決は、値を型に合わせて書く（以前は 2^63 以上が負の数になり、uint64 の編集で範囲外のエラーになった）
- **64 ビットの幅の bitfield のエントリが 0 になっていた**（元からの誤り）: マスク `(1L << 64) - 1` はシフト量が 64 で割った余りになるので 0。幅 64 は全ビットにし、上位ビットを符号拡張しないよう論理シフトにした
- **CBOR**: `cbor_unsigned` の 8 バイトの引数は `seek` + `seek_restore` で `uint64` として読み直す（virtual の値は式の結果なので符号付きのまま）。`cbor_negative` は、引数が 2^63 以上（値が -2^63 より小さい）なら値を出さず、引数を `argument_unsigned`（uint64）で見せる。`cbor_value` の `argument` の説明に、式の値なので 2^63 以上は負になることを書いた
- **MessagePack**: uint 64 は `uint64` のフィールドなので、表示の変更だけで正しくなった
- ゴールデンは cbor の説明（`argument` と 2 つの `value`）だけが変わった。2^63 未満の値の表示はどの形式でも変わらない

### 照合

- MessagePack: Python の msgpack 1.1 の `packb([2**64 - 1, 2**63, -2**63])` の出力（`93 cf ff… cf 80 00… d3 80 00…`）が 18446744073709551615・9223372036854775808・-9223372036854775808 になる
- CBOR: RFC 8949 付録 A の `1b ff ff ff ff ff ff ff ff` = 18446744073709551615、`3b ff…` = -18446744073709551616（値は出さず、引数 18446744073709551615 を表示）
- CLI の patch: uint64 の enum のラベル（値 0xFFFFFFFFFFFFFFFE）を `--set` で書けることを手で確かめた

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| IntegerTextTests | Format_UsesUnsignedOnlyForUnsigned64BitTypes・ShowsHex_ComparesUnsignedForUnsigned64BitTypes・FormatBits_IsAlwaysUnsigned | 1 |
| IntegerTextTests | TryParseLiteral_ReadsDecimalAndHexUpTo2To64・TryParseLiteral_RejectsOutOfRangeAndNonIntegers | 実装中の設計変更（enum の値） |
| Unsigned64DisplayTests | Uint64_AboveInt64_IsShownUnsigned（uint64・uleb128） | 1 |
| Unsigned64DisplayTests | EnumValuesAbove2To63_MatchTheUint64Field・BitfieldEntryOf64Bits_KeepsAllBits | 機能要件（enum・bitfield） |
| Unsigned64DisplayTests | Expressions_StaySigned | 4 |
| Unsigned64DisplayTests | Edit_WritesValuesAbove2To63 | 5 |
| Unsigned64DisplayTests | Diff_ShowsUnsignedValues | 1（差分） |
| MsgpackParsingTests | MsgpackFormat_Uint64AboveInt64_IsShownUnsigned（ツリー・JSON） | 1・3 |
| CborParsingTests | CborFormat_EightByteIntegers_AreShownWithoutWrapping | 1 |
| NodeDetailFormatterTests（Presentation / Tui） | Format_Uint64AboveInt64_ShowsUnsignedValueAndHex・Uint64AboveInt64_ShowsTheUnsignedValue | 1（GUI / TUI の詳細） |
| GoldenFileTests（既存） | 全形式（cbor は説明だけの差分） | 2 |
| FormatCatalog（ファズ） | CBOR・MessagePack の 64 ビットの整数の見本 | 6 |
