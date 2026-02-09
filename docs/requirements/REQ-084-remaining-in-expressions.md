# REQ-084: remaining を式で利用可能にする

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在、`remaining` は `size: remaining` というキーワードとしてのみ使用可能で、式の中では変数として参照できない。このため、「残りバイトから固定長フッタを除いた部分」のようなサイズ指定ができない。

典型的な問題:
- **GZIP**: deflate圧縮データの後にCRC32(4B)+ISIZE(4B)のフッタがあるが、`size: "remaining - 8"` と書けないため、圧縮データとフッタを分離できず `deflate` 型で全体を取り込んでいる
- **JPEG**: SOS後のエントロピーデータとEOIマーカーの分離が不可能

```yaml
# 現状（ワークアラウンド）:
- name: compressed_and_footer
  type: deflate
  size: remaining         # CRC32+ISIZEもdeflateに含まれてしまう

# 改善後:
- name: compressed_data
  type: deflate
  size: "remaining - 8"   # フッタ8バイトを除外
- name: crc32
  type: uint32
- name: isize
  type: uint32
```

## 機能要件

### 追加する機能

- [x] `remaining` を式内の予約変数として使用可能にする
- [x] `remaining` は現在のバウンダリスコープ内の残りバイト数（現在位置からスコープ終端まで）を返す
- [x] `size: "remaining - N"` のようなパターンが動作すること
- [x] `if: "remaining > 0"` のような条件式でも使用可能なこと
- [x] `repeat_until: "remaining == 0"` のような終了条件でも使用可能なこと

### 変更する既存機能

- [x] `ExpressionEvaluator.ResolveField`: `remaining` を予約変数として認識し、DecodeContextから残りバイト数を取得
- [x] 既存の `size: remaining`（キーワード形式）は引き続き動作すること（後方互換）

### 変更しないもの（スコープ外）

- `remaining` の意味（現在のバウンダリスコープ内の残りバイト数）は変更しない
- ファイル全体の残りバイト数を返す別の変数は追加しない

## 受入条件

1. [x] `size: "remaining - 8"` が正しく評価されること（残りバイト数 - 8）
2. [x] `if: "remaining > 0"` が正しく評価されること
3. [x] `repeat_until: "remaining == 0"` が正しく評価されること
4. [x] `size: remaining`（キーワード形式）が引き続き動作すること
5. [x] GZIPフォーマット: 圧縮データとフッタ(CRC32+ISIZE)を分離できること
6. [x] 単体テスト: remaining式の各パターン（算術演算、比較、条件式）
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）— 573テスト全通過

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし（式パーサーは `remaining` を通常の識別子として解析済み） |
| BinAnalyzer.Engine | ExpressionEvaluator: remaining予約変数の解決ロジック追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 式内の予約変数 `remaining` の説明追加（別途）

### 変更が必要なフォーマット定義

- [x] `formats/gzip.bdef.yaml` — 圧縮データとフッタの分離

### 変更が必要なテスト

- [x] GzipParsingTests: フッタフィールド(crc32, isize)の個別検証追加

---

## 設計メモ

`remaining` を `ExpressionEvaluator.ResolveField` の予約変数として認識させる（1行追加）。式パーサーは `remaining` を通常の `FieldReference` として解析済みのため、ResolveField側で特別扱いするだけで `size: "{remaining - 8}"`, `if: "{remaining > 0}"`, `repeat_until: "{remaining == 0}"` がすべて自動的に動作する。

既存の `size: remaining`（キーワード形式）は `YamlToIrMapper.ParseSize` で `SizeRemaining = true` に変換されており、ExpressionEvaluatorを経由しないため影響なし。

---

## 実装メモ

### 実装中の設計変更

なし。計画通り1行追加で実装完了。

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Engine/ExpressionEvaluator.cs` | `ResolveField()`: `remaining` 予約変数の解決ロジック追加（`(long)context.Remaining`を返す） |
| `formats/gzip.bdef.yaml` | `compressed_data` の `size: remaining` → `size: "{remaining - 8}"`、`crc32`(uint32)・`isize`(uint32) フッタフィールドを個別定義 |
| `tests/BinAnalyzer.Engine.Tests/ExpressionEvaluatorTests.cs` | 3テスト追加（remaining単体・算術・比較） |
| `tests/BinAnalyzer.Engine.Tests/BinaryDecoderTests.cs` | 1テスト追加（remaining式によるサイズ指定の統合テスト） |
| `tests/BinAnalyzer.Integration.Tests/GzipParsingTests.cs` | フッタフィールド検証テスト追加、既存テストのアサーション更新 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ExpressionEvaluatorTests | Evaluate_Remaining_ReturnsCurrentScopeRemaining | 1, 6 |
| ExpressionEvaluatorTests | Evaluate_RemainingSubtract_ReturnsCorrectValue | 1, 6 |
| ExpressionEvaluatorTests | Evaluate_RemainingComparison_Works | 2, 6 |
| BinaryDecoderTests | Decode_SizeRemainingExpression_DecodesCorrectly | 1 |
| GzipParsingTests | GzipFormat_Footer_DecodesAsIndividualFields | 5 |

### 気づき・今後の課題

- `remaining` という名前のユーザ定義フィールドがあった場合、予約変数が優先される。現状ではフォーマット定義で `remaining` を変数名として使うケースはないため問題にならないが、将来的に予約変数の衝突検出をDSLバリデータに追加する余地がある。
