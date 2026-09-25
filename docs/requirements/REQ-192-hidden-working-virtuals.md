# REQ-192: 作業用の virtual をツリーに出さない（virtual の padding）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし（REQ-188 のデータ・その他の見直しで必要になった） |
| 作成日 | 2026-09-25 |
| 更新日 | 2026-09-25 |

## 背景・動機

REQ-188 のデータ・その他の見直しでは、DSL の式だけで組み立てる値が増えた。例: Parquet の Thrift の構造体の種類とフィールドの番号の差の基準、DNS の圧縮ポインタをたどりながら連ねる名前、PDF の次の文字の先読み、FAT の長いファイル名の組み立て。これらは後ろの式で使う作業用の値で、利用者に見せる意味は無いが、virtual として定義するとツリーに並ぶ。Parquet では 1 つのフィールドに 5 つの作業用の値が並び、目的の値が埋もれた。

`padding: true` はツリーに出さないフィールドを表す既存のキーだが、bytes・整数・文字列・フラグにしか効かず、virtual に付けても無視されていた（`SetPaddingFlag` が DecodedVirtual を扱わない）。

## 機能要件

### 変更する既存機能

- [x] **`padding: true` の virtual はツリーに出さない。** DecodedVirtual の `IsPadding` を立てる。tree 出力・TUI・GUI（`IsPadding` を見て隠す既存の処理）で隠れる
- [x] **値は通常どおり束縛する。** 後ろの式・値の昇格・メンバーアクセスから使える
- [x] **JSON 出力には残す**（JSON 出力は `IsPadding` を見ない。既存の padding の bytes と同じ）
- [x] docs/dsl-reference.md の「パディングフィールド」に書く

### 変更しないもの（スコープ外）

- DSL の構文。新しいキー（`hidden:` など）は足さない（代替案を参照）
- struct・配列の padding（従来どおりマークしない）

## 受入条件

1. [x] `padding: true` の virtual が `IsPadding` を持ち、値を後ろの virtual の式から使えること（Engine のテスト）
2. [x] 同梱定義の作業用の値が tree 出力に出ないこと（Parquet・DNS・FAT・PDF・X.509 の tree 出力のテスト）
3. [x] 既存テストが全て通過すること

## 影響範囲

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Engine | `DecodeVirtualField` が `IsPadding = field.IsPadding` を付ける |
| tests/BinAnalyzer.Engine.Tests | `VirtualFieldTests.PaddingVirtual_IsMarkedAsPaddingAndStillBindsItsValue` |

- [x] docs/dsl-reference.md — 「パディングフィールド」

---

## 設計メモ

- **既存の `padding: true` を virtual にも効かせる。** 「ツリーに出さない」という意味は同じで、予約バイトと同じく「利用者に見せる値ではない」ものを表す。検証・JSON Schema・DTO の変更が要らない
- **代替案: `hidden: true` を足す。** 名前は意図に合うが、DSL・JSON Schema・検証・DTO を変える必要があり、padding との使い分けも増える。不採用
- **代替案: 作業用の値を式の中に展開する。** 長い三項演算子を何か所にも書くことになり、読めなくなる（Parquet の値の列挙型の判定は、式に展開できたものは展開した）
