# REQ-090: 文字列ベース switch_on（string_table参照によるswitch）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | wont_do |
| 優先度 | — |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-09 |
| 却下日 | 2026-02-09 |

## 背景・動機

Java Classファイルの `attribute_info` は、属性名がConstant Pool内のUTF8エントリへのインデックスで指定される。属性の内部構造は属性名（"Code", "LineNumberTable", "SourceFile"等）に依存するが、現在のDSLでは:

1. `switch_on` に数値やフィールド値の式は使えるが、string_tableルックアップ結果による分岐ができない
2. 属性の内部構造を `bytes remaining` で取り込むワークアラウンドが必要

同様のパターンは、文字列テーブルを持つフォーマット（ELFのセクション名テーブル等）でも発生し得る。

```yaml
# 改善後:
- name: attribute_body
  type: switch
  switch_on: "string_table_lookup(constant_pool, attribute_name_index)"
  cases:
    "'Code'": code_attribute
    "'LineNumberTable'": line_number_table_attribute
    "'SourceFile'": source_file_attribute
  default: generic_attribute
```

## 却下理由

1. **フラットテーブルと定数プールの構造的不一致**: 既存 string_table（`DecodeContext.RegisterStringTable` / `LookupString`）はバイトオフセットでヌル終端文字列を引くフラットモデル。Java Class 定数プールは 1 始まり整数インデックスで可変長エントリを引くモデル。`string_table_lookup` を追加しても定数プールには適用不可。
2. **フラットテーブルの switch 利用に実需なし**: ELF/PE の文字列テーブル参照は既存の `string_table` フィールド属性（`→ "文字列"` 表示）で十分。switch_on で文字列分岐が必要なケースが ELF/PE 側にはない。
3. **switch_on は既に文字列比較をサポート**: `BinaryDecoder.ValuesEqual` は string-to-string 比較を実装済み。技術的な障壁は string_table_lookup 関数の有無ではなく、定数プールの参照解決。
4. **REQ-069 で方針確定済み**: 「constant pool 参照の解決が複雑なため description 追記による対応を基本方針とする」と明記。

## 調査記録

### 既存 string_table 機構の仕組み

- `StructDefinition.IsStringTable` = true の構造体デコード後、そのバイト範囲を `DecodeContext.RegisterStringTable(name, offset, size)` で登録
- `FieldDefinition.StringTableRef` で参照先テーブル名を指定 → デコード時に `LookupString(tableName, offset)` でヌル終端文字列を解決
- バイトオフセット → ヌル終端文字列の単純な構造のみ対応

### switch_on の文字列比較対応状況

- `DecodeSwitchField` で `ExpressionEvaluator.Evaluate(field.SwitchOn, context)` → 戻り値は `object`（long, string, bool 等）
- ケース条件も `ExpressionParser.Parse(kvp.Key)` で式にパース → 文字列リテラル `'Code'` は `LiteralString("Code")` に
- `ValuesEqual` で string-to-string 比較が動作 → switch_on が文字列を返せば文字列ケースで分岐可能

### FunctionCall 基盤の状態（REQ-089 で追加済み）

- `ExpressionEvaluator.EvaluateFunction` に switch で関数名ディスパッチ
- 将来 `string_table_lookup` を追加すること自体は 1 行追加で可能
- ただし Java Class 定数プールには根本的に適用不可

### Java Class 定数プール問題の本質

- `attribute_name_index` は定数プールの 1 始まり整数インデックス
- 定数プールエントリは可変長（CONSTANT_Utf8: tag + length + bytes、CONSTANT_Class: tag + name_index、等）
- インデックス → バイトオフセットの変換には全エントリの走査が必要
- 解決には「インデックスベースのキーバリューテーブル登録」等の新しい機構が必要 → 新規 REQ として起票すべき

## 機能要件

### 追加する機能

- [ ] ~~新しい組み込み関数 `string_table_lookup(table_name, offset)` — string_tableから文字列を取得~~
- [ ] ~~`switch_on` 式内で `string_table_lookup` の結果（文字列）を使用可能にする~~
- [ ] ~~switch の cases キーに文字列リテラル（`'Code'` 等）を指定可能にする（現在も一部対応済み — 確認・強化）~~
- [ ] ~~`string_table_lookup` が見つからない場合は空文字列を返す（`default` ケースにフォールバック）~~

### 変更する既存機能

- [ ] ~~`ExpressionEvaluator`: `string_table_lookup` 関数の評価ロジック追加~~
- [ ] ~~`ExpressionParser`: 関数呼び出し構文のサポート（REQ-089で追加済みの場合は流用）~~
- [ ] ~~switch_onの評価: 文字列結果とcasesキーの文字列比較を確実にサポート~~

### 変更しないもの（スコープ外）

- string_table自体の登録メカニズムの変更（既存の `string_table: true` をそのまま使用）
- 動的な文字列テーブルの構築（Constant Poolの構造は既存の仕組みで登録済み前提）
- 全Java Class属性の内部構造定義（代表的な数種のみ。残りはdefaultでbytes）

## 受入条件

1. [ ] ~~`string_table_lookup(table, index)` が正しい文字列を返すこと~~
2. [ ] ~~switch_onで文字列結果によるケース分岐が動作すること~~
3. [ ] ~~文字列リテラルのcasesキーとの比較が正しく行われること~~
4. [ ] ~~テーブルに存在しないインデックスの場合、default ケースにフォールバックすること~~
5. [ ] ~~Java Classフォーマット: Code属性の内部構造がパースされること~~
6. [ ] ~~単体テスト: string_table_lookupの各種パターン~~
7. [ ] ~~既存テストが全て通過すること（`dotnet test` 全通過）~~

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | Expression AST: FunctionCallExpression（REQ-089で追加済みなら変更なし） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | ExpressionEvaluator: string_table_lookup関数実装 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] ~~docs/dsl-reference.md — string_table_lookup関数の説明追加~~

### 変更が必要なフォーマット定義

- [ ] ~~`formats/java-class.bdef.yaml` — 主要attribute（Code, SourceFile等）の内部構造追加~~

### 変更が必要なテスト

- [ ] ~~JavaClassParsingTests: Code属性の内部構造検証追加~~
- [ ] ~~単体テスト: string_table_lookup関数のパースと評価~~

---

## 設計メモ

設計フェーズには進まなかった（wont_do のため）。

---

## 実装メモ

実装フェーズには進まなかった（wont_do のため）。
