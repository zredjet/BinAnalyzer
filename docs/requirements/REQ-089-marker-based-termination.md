# REQ-089: マーカーベースのストリーム終了検出

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-084（remaining式利用） |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-09 |

## 背景・動機

JPEGのSOS（Start of Scan）セグメント以降のエントロピーコードデータは、EOIマーカー（0xFF 0xD9）が出現するまで続くが、データ長が事前に分からない。現在のDSLでは `bytes remaining` で全体を取り込むしかなく、EOIマーカーとエントロピーデータを分離できない。

同様のパターンは他のマーカーベースフォーマットでも発生し得る:
- **JPEG**: SOS後のエントロピーデータ + EOIマーカー分離
- 将来的にはTIFFのIFDチェーン終端検出等にも応用可能

```yaml
# 改善後:
- name: entropy_data
  type: bytes
  size: "until_marker(0xFF, 0xD9)"   # マーカーの直前まで

- name: eoi
  type: bytes
  size: 2
  expected: [0xFF, 0xD9]
```

## 機能要件

### 追加する機能

- [x] 新しいサイズ指定方式 `until_marker` — 指定バイト列が出現する位置までのサイズを返す組み込み関数
- [x] `size: "until_marker(0xFF, 0xD9)"` のように、バイト列をカンマ区切りで指定
- [x] マーカーバイト列自体はフィールドに含めない（マーカーの直前まで）
- [x] マーカーが見つからない場合はスコープ終端までのサイズを返す（エラーにしない）
- [x] `until_marker` は式の組み込み関数として実装

### 変更する既存機能

- [x] `ExpressionParser`: 関数呼び出し構文 `funcname(arg1, arg2, ...)` の解析追加
- [x] `ExpressionEvaluator`: `until_marker` 関数の評価ロジック（バイナリデータの前方スキャン）
- [x] `Expression` AST: `FunctionCallExpression` ノード追加

### 変更しないもの（スコープ外）

- エスケープシーケンスの処理（JPEG 0xFF 0x00のバイトスタッフィング）は対象外
  - JPEG固有のスタッフィングは将来の専用オプションで対応
- 後方スキャン（ファイル末尾からの検索）は対象外
- 正規表現パターンによる検索は対象外
- `until_marker` 以外の組み込み関数は本REQでは追加しない（拡張基盤のみ）

## 受入条件

1. [x] 式パーサーが関数呼び出し構文 `funcname(args...)` を解析できること
2. [x] `until_marker(0xFF, 0xD9)` が正しいバイト位置を返すこと
3. [x] マーカーが見つからない場合、スコープ終端までのサイズを返すこと
4. [x] マーカー自体はフィールドに含まれないこと（直前まで）
5. [x] JPEGフォーマット: エントロピーデータとEOIマーカーが分離されること
6. [x] 引数が0個や不正な場合にエラーメッセージが出ること
7. [x] 単体テスト: until_markerの各種パターン（先頭、中間、末尾、不存在）
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | Expression AST: FunctionCallExpression追加 |
| BinAnalyzer.Dsl | 変更なし（式パーサーはCore側） |
| BinAnalyzer.Engine | ExpressionEvaluator: until_marker関数実装、バイナリスキャンロジック |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 式の組み込み関数セクション追加

### 変更が必要なフォーマット定義

- [x] `formats/jpeg.bdef.yaml` — SOS後のエントロピーデータとEOI分離

### 変更が必要なテスト

- [x] JpegParsingTests: EOIマーカー分離の検証追加
- [x] 単体テスト: FunctionCallExpressionのパースと評価

---

## 設計メモ

### 全体方針

式システムに汎用的な「関数呼び出し構文」を追加し、その最初の組み込み関数として `until_marker` を実装する。これにより将来の組み込み関数追加が容易になる拡張基盤を確立する。

### レイヤー分離

| レイヤー | 責務 | 変更内容 |
|---|---|---|
| Core（式AST） | 構文表現のみ。意味は持たない | `FunctionCall` ノード追加、`Comma` トークン追加 |
| Core（パーサー） | `IDENTIFIER "(" args ")"` の構文解析 | `ParsePrimary` で識別子の後に `(` が続く場合を分岐 |
| Engine（評価器） | 関数名に基づくディスパッチと実行 | `EvaluateFunction` → 関数名でswitch → `EvaluateUntilMarker` |
| Engine（DecodeContext） | バイナリデータの低レベル操作 | `FindMarker` メソッド追加（前方スキャン） |

設計ポイント:
- **パーサーは関数名を知らない** — 任意の識別子 + `(` を関数呼び出しとして解析する。未知の関数はEngine層の評価時にエラーになる。
- **DecodeContextは式を知らない** — `FindMarker` は純粋なバイト列検索のみ。式の評価ロジックはExpressionEvaluatorに閉じる。
- **引数は通常の式** — `until_marker(0xFF, 0xD9)` の各引数はリテラルに限らず任意の式（変数参照、算術式等）が使える。

### パーサー文法の拡張

```
primary → INTEGER | STRING | IDENTIFIER | IDENTIFIER "(" arg_list? ")" | "(" or_expr ")"
arg_list → or_expr ("," or_expr)*
```

識別子の後に `(` が続くかどうかで `FieldReference` と `FunctionCall` を判別する（1トークン先読み）。既存の `FieldReference` パスは変更なし。

### FindMarker の実装

`MemoryExtensions.IndexOf(ReadOnlySpan<byte>)` を使用。.NET標準のSIMD最適化済みバイト列検索を活用するため、大きなエントロピーデータに対しても効率的。検索範囲は `_position..CurrentScope.End` に限定し、スコープ外のデータは見ない。

### マーカー未発見時の挙動

エラーではなく `remaining`（スコープ終端までのサイズ）を返す。理由:
- 破損したJPEGや切り詰められたデータでも解析を続行できる
- `size: remaining` と同等のフォールバック動作となり、デグレードが穏やか
- 要件に「エラーにしない」と明記されている

---

## 実装メモ

### 変更ファイル一覧

| ファイル | 変更種別 | 概要 |
|---|---|---|
| `src/BinAnalyzer.Core/Expressions/ExpressionToken.cs` | 修正 | `Comma` トークン追加（`Eof` の前） |
| `src/BinAnalyzer.Core/Expressions/ExpressionTokenizer.cs` | 修正 | `','` → `Comma` トークン生成 |
| `src/BinAnalyzer.Core/Expressions/ExpressionNode.cs` | 修正 | `FunctionCall(Name, Arguments)` sealed record 追加 |
| `src/BinAnalyzer.Core/Expressions/ExpressionParser.cs` | 修正 | `ParsePrimary` の `Identifier` ケースで関数呼び出しを分岐、文法コメント更新 |
| `src/BinAnalyzer.Engine/DecodeContext.cs` | 修正 | `FindMarker(ReadOnlySpan<byte>)` メソッド追加 |
| `src/BinAnalyzer.Engine/ExpressionEvaluator.cs` | 修正 | `FunctionCall` 評価、`EvaluateFunction`、`EvaluateUntilMarker` 追加 |
| `formats/jpeg.bdef.yaml` | 修正 | SOS `compressed_data`: `size: remaining` → `size: "{until_marker(0xFF, 0xD9)}"` |
| `schemas/bdef.schema.json` | 修正 | `size` プロパティの description に `until_marker()` 言及追加 |
| `docs/dsl-reference.md` | 修正 | 式セクションに関数呼び出し構文、組み込み関数セクション追加 |
| `tests/BinAnalyzer.Core.Tests/Expressions/FunctionCallParserTests.cs` | 新規 | 関数呼び出しパーステスト 6件 |
| `tests/BinAnalyzer.Engine.Tests/UntilMarkerTests.cs` | 新規 | until_marker 評価テスト 8件 |
| `tests/BinAnalyzer.Integration.Tests/JpegTestDataGenerator.cs` | 修正 | `CreateJpegWithEntropyData()` 追加 |
| `tests/BinAnalyzer.Integration.Tests/JpegParsingTests.cs` | 修正 | EOI分離検証テスト追加 |

### 実装中の設計変更

なし。計画通りに実装。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FunctionCallParserTests | Parse_FunctionCall_TwoArguments | AC-1: 関数呼び出し構文の解析 |
| FunctionCallParserTests | Parse_FunctionCall_OneArgument | AC-1: 引数1つの解析 |
| FunctionCallParserTests | Parse_FunctionCall_NoArguments | AC-1: 引数0個の解析（パースは成功） |
| FunctionCallParserTests | Parse_IdentifierWithoutParens_RemainsFieldReference | AC-1: 通常の識別子は従来通り FieldReference |
| FunctionCallParserTests | Parse_FunctionCall_WithExpressionArguments | AC-1: 引数に算術式が使えること |
| FunctionCallParserTests | Parse_FunctionCall_MissingClosingParen_Throws | AC-6: 構文エラー検出 |
| UntilMarkerTests | UntilMarker_TwoByteMarker_InMiddle_ReturnsCorrectOffset | AC-2: 正しいバイト位置 |
| UntilMarkerTests | UntilMarker_MarkerAtStart_ReturnsZero | AC-2, AC-7: 先頭パターン |
| UntilMarkerTests | UntilMarker_MarkerNotFound_ReturnsRemaining | AC-3: 未発見時の挙動 |
| UntilMarkerTests | UntilMarker_MarkerAtEnd_ReturnsCorrectDistance | AC-2, AC-7: 末尾パターン |
| UntilMarkerTests | UntilMarker_NoArguments_ThrowsError | AC-6: 引数0個でエラー |
| UntilMarkerTests | UntilMarker_SingleByteMarker_Works | AC-7: 1バイトマーカー |
| UntilMarkerTests | UntilMarker_AfterReadingBytes_ReturnsOffsetFromCurrentPosition | AC-2: 読み進めた後のオフセット計算 |
| UntilMarkerTests | UntilMarker_WithinScope_OnlySearchesScope | AC-3: スコープ外のマーカーは無視 |
| JpegParsingTests | JpegFormat_WithEntropyData_SeparatesCompressedDataAndEoi | AC-4, AC-5: JPEG統合テスト |

### テスト結果

```
BinAnalyzer.Core.Tests:        128 passed
BinAnalyzer.Dsl.Tests:          73 passed
BinAnalyzer.Engine.Tests:      208 passed
BinAnalyzer.Integration.Tests: 250 passed
合計: 659 passed, 0 failed (AC-8 充足)
```

### 気づき・今後の課題

- **~~JPEGバイトスタッフィング~~（訂正済み）**: 当初「エントロピーデータ中に偶然 `0xFF 0xD9` が出現すると誤検出する」と懸念したが、これは誤りだった。JPEG仕様 (ITU-T T.81 B.1.1.5) ではエントロピーデータ中の `0xFF` は必ず `0xFF 0x00` としてスタッフされるため、生バイトストリームで `0xFF 0xD9` が出現する場合は常に本物の EOI マーカーである。主要なバイトスタッフィングプロトコル（JPEG, SLIP, PPP/HDLC, H.264, COBS）を調査した結果、いずれも「マーカーバイト列がデータ中に出現しないこと」を保証する設計であり、素朴な `IndexOf` で安全に動作する。詳細は REQ-091 に記録。
- **関数呼び出しの汎用基盤**: 今回の実装で `FunctionCall` ノードと `EvaluateFunction` のディスパッチ機構が整った。新しい組み込み関数を追加する場合は `EvaluateFunction` の switch に1行追加し、対応する評価メソッドを書くだけでよい。候補としては `sizeof(struct_name)`、`offset_of(field_name)` 等が考えられる。
- **パフォーマンス**: `FindMarker` は `MemoryExtensions.IndexOf` を使っており、.NETランタイムの SIMD 最適化が効く。大きなエントロピーデータ（数MBのJPEG）でも問題ないはず。ただし `until_marker` が繰り返しフィールド内で毎回呼ばれるケースでは、同じ範囲を繰り返しスキャンする可能性があり、その場合はキャッシュ等の最適化が必要になるかもしれない。
- **スコープ制約**: `FindMarker` の検索範囲は現在のバウンダリスコープ (`CurrentScope.End`) に限定される。これは `remaining` と同じ意味論であり、`size: remaining` を `size: "{until_marker(...)}"` に置き換えた際に自然な動作となる。
- **既存テストへの影響**: `CreateMinimalJpeg()` のテストデータではSOS直後にEOIがあり `compressed_data` のサイズが0になる。`until_marker` に変更後も位置0でEOIマーカーを検出してサイズ0を返すため、既存テストは変更なしで通過した。
