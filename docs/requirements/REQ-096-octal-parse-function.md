# REQ-096: 文字列→整数変換関数

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし（独立して実装可能） |
| フェーズ | B（軽量 DSL 拡張） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

TAR フォーマットのファイルサイズフィールドは8進 ASCII 文字列（例: `"0000644\0"`）で格納されている。現在の DSL では ASCII 文字列を整数に変換する手段がないため、TAR のファイルデータブロック数を算出できず、ヘッダ1ブロックのみの浅い解析に留まっている。

汎用的な `parse_int(field, base)` 関数を追加することで、TAR に限らずテキストベースの数値フィールドを持つバイナリフォーマット全般に対応できる。

## 機能要件

### 追加する機能

- [x] 組み込み関数 `parse_int(field_name, base)` の追加
  - 第1引数: 文字列フィールド名（ASCII/UTF8 フィールドの値を参照）
  - 第2引数: 基数（8, 10, 16 等の整数リテラル）
  - 戻り値: 変換後の `long` 整数値
  - 末尾のヌル文字・空白は自動トリミング
  - 変換失敗時は 0 を返す（またはデコードエラー）
- [x] 式パーサーへの関数追加（`ExpressionEvaluator` の `EvaluateFunction` に分岐追加）
- [x] TAR フォーマット定義の改善（`parse_int` を使ったファイルサイズ算出とデータブロックスキップ）

### 使用例

```yaml
# TAR ヘッダ
- name: file_size_octal
  type: ascii
  size: "12"
  description: "ファイルサイズ（8進ASCII）"
- name: file_size
  type: virtual
  value: "{parse_int(file_size_octal, 8)}"
  description: "ファイルサイズ（バイト）"
```

### 変更しないもの（スコープ外）

- 浮動小数点数の文字列パース
- 文字列の部分切り出し関数

## 受入条件

1. [x] `parse_int` 関数が式内で使用でき、8進・10進・16進の文字列を整数に変換できること
2. [x] ヌル終端・空白パディングされた文字列を正しく処理できること
3. [x] TAR フォーマット定義で `parse_int` を使いファイルサイズが取得できること
4. [x] 単体テスト（ExpressionEvaluator）が追加されていること
5. [x] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | なし（FunctionCall ノードは既存） |
| BinAnalyzer.Dsl | なし |
| BinAnalyzer.Engine | ExpressionEvaluator に `parse_int` 関数追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

---

## 設計メモ

- `parse_int` は `ExpressionEvaluator.EvaluateFunction` の switch に分岐追加し、`EvaluateParseInt` メソッドで処理
- 第1引数は `EvaluateNode` で評価 → FieldReference（フィールド値参照）にも LiteralString にも対応
- `TrimEnd('\0', ' ')` で TAR の末尾ヌル文字・空白を自動トリミング
- 変換失敗時は `0L` を返す（ロバスト性重視、デコード停止させない）
- 基数は `Convert.ToInt64` が対応する 2, 8, 10, 16 に限定
- TAR フォーマット: `element_size: "512"` を削除し、`tar_header` を `tar_entry` にリネーム。virtual フィールドで `parse_int` → 512バイト境界切り上げ → `if` 条件付きデータブロック読み込み

---

## 実装メモ

### 実装中の設計変更

- 計画では TAR フォーマットの条件フィールドを `condition:` として記載していたが、DSL の YAML モデルでは `if:` が正しいプロパティ名であるため `if:` に修正

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ParseIntFunctionTests | ParseInt_OctalString_ReturnsCorrectValue | 1 |
| ParseIntFunctionTests | ParseInt_DecimalString_ReturnsCorrectValue | 1 |
| ParseIntFunctionTests | ParseInt_HexString_ReturnsCorrectValue | 1 |
| ParseIntFunctionTests | ParseInt_NullTerminated_TrimsCorrectly | 2 |
| ParseIntFunctionTests | ParseInt_SpacePadded_TrimsCorrectly | 2 |
| ParseIntFunctionTests | ParseInt_EmptyAfterTrim_ReturnsZero | 2 |
| ParseIntFunctionTests | ParseInt_InvalidString_ReturnsZero | 1 |
| ParseIntFunctionTests | ParseInt_WrongArgCount_Throws | 1 |
| ParseIntFunctionTests | ParseInt_InvalidBase_Throws | 1 |
| ParseIntFunctionTests | ParseInt_ZeroValue_ReturnsZero | 1 |
| ParseIntFunctionTests | ParseInt_InArithmeticExpression_Works | 1, 3 |
| ParseIntFunctionTests | ParseInt_BinaryString_ReturnsCorrectValue | 1 |
| TarParsingTests | TarFormat_VirtualFields_ComputeCorrectly | 3, 5 |

### 気づき・今後の課題

- DSL の条件フィールドは YAML 上 `if:` であり、IR 上は `Condition`。プラン作成時に YAML プロパティ名を確認する必要がある。
