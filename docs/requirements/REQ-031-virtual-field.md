# REQ-031: 計算フィールド（virtual field）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

バイナリフォーマットの解析結果に、バイナリデータには直接対応しないが有用な計算値を表示したい場合がある:
- 画像の `width * height` でピクセル数を算出
- `compressed_size / original_size * 100` で圧縮率を表示
- `offset + size` で次のセクションの開始位置を計算
- タイムスタンプフィールドのエポック秒を人間可読な日時に変換（将来拡張）

現在のDSLでは全フィールドがバイナリデータの読み取りに対応しているため、計算値を表示する手段がない。

`type: virtual` フィールドを導入することで、式の評価結果をツリーに表示できるようになる。

## 機能要件

### 追加する機能

- [ ] `virtual` フィールド型
  - バイナリデータを読み取らない（サイズ0、オフセット変化なし）
  - `value` プロパティで式を指定し、その評価結果を表示
  - 既存の式システム（フィールド参照、算術、比較等）をそのまま利用
- [ ] YAMLシンタックス
  ```yaml
  - name: pixel_count
    type: virtual
    value: "{width * height}"
  - name: compression_ratio
    type: virtual
    value: "{compressed_size * 100 / original_size}"
  ```
- [ ] デコード結果
  - 新しいDecodedNodeサブクラス（`DecodedVirtual`）または既存の `DecodedInteger`/`DecodedString` を流用
  - オフセット・サイズは親構造体の現在位置を参照（0バイト消費）
- [ ] 出力
  - tree/json/html等で通常フィールドと同様に表示
  - 「計算値」であることが視覚的にわかる表示（例: `=` プレフィックスやイタリック）

### 変更する既存機能

- [ ] FieldType に `Virtual` を追加
- [ ] BinaryDecoder: virtual フィールドの処理（バイト読み取りなし、式評価のみ）
- [ ] YamlFieldModel: `value` プロパティの追加
- [ ] YamlToIrMapper: virtual フィールドのマッピング
- [ ] FieldDefinition: `ValueExpression` プロパティの追加

### 変更しないもの（スコープ外）

- virtual フィールドの値を他のフィールドの式から参照する機能 — 将来課題
- 文字列フォーマット関数（日時変換等）— 将来課題
- virtual フィールドの繰り返し

## 受入条件

1. [ ] `type: virtual` フィールドがバイナリデータを消費しないこと
2. [ ] `value` の式が正しく評価され、結果が出力に表示されること
3. [ ] 算術式（加算、乗算、除算等）が動作すること
4. [ ] 他フィールドの値を参照する式が動作すること
5. [ ] tree出力で計算フィールドが視覚的に区別できること
6. [ ] json出力に計算フィールドが含まれること
7. [ ] html出力に計算フィールドが含まれ、検索可能であること
8. [ ] `value` が未指定の場合にバリデーションエラーになること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldType.Virtual 追加、FieldDefinition に ValueExpression 追加、DecodedVirtual 追加 |
| BinAnalyzer.Dsl | YamlFieldModel に value 追加、YamlToIrMapper で virtual マッピング |
| BinAnalyzer.Engine | BinaryDecoder に virtual フィールドのデコード処理を追加 |
| BinAnalyzer.Output | 各フォーマッターに DecodedVirtual の出力処理を追加 |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — フィールド型テーブルに virtual を追加、セクションを新設
- [ ] docs/architecture.md — DecodedVirtual をデコード結果ツリーに追記
- [ ] schemas/bdef.schema.json — virtual 型と value プロパティを追加（REQ-028後）

---

## 設計メモ

> 設計Phaseで記入する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
