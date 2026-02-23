# REQ-144: インライン構造体定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 取り下げ |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在、struct型フィールドは必ずトップレベルの `structs:` セクションで定義された名前付き構造体を参照する必要がある。1箇所でしか使われない小さな構造体でも、トップレベルに定義を追加しなければならない。

```yaml
# 現在: 1回しか使わない構造体もトップレベル定義が必要
structs:
  main:
    - name: header
      type: struct
      struct: file_header

  file_header:           # ← mainからしか参照されない
    - name: magic
      type: uint32
    - name: version
      type: uint16
```

大規模なフォーマット定義では、1回限りの構造体が多数トップレベルに並び、定義の見通しが悪くなる。

```yaml
# 望ましい記述: インライン定義
structs:
  main:
    - name: header
      type: struct
      fields:
        - name: magic
          type: uint32
        - name: version
          type: uint16
```

### 影響を受けるフォーマット定義

フォーマット定義の大半に1回限りの構造体が存在する。特に以下のフォーマットではトップレベル定義が多い。

| フォーマット | トップレベルstruct数 | うち1回参照のみ（概算） |
|---|---|---|
| PE | 15+ | 8+ |
| ELF | 12+ | 5+ |
| MP4 | 20+ | 10+ |
| X.509 | 15+ | 10+ |

## 機能要件

### 追加する機能

- [ ] struct型フィールドに `fields` プロパティを直接指定してインラインで子フィールドを定義可能にする
- [ ] インライン構造体にも `endianness`, `align`, `bitstream` 等のstruct属性を指定可能にする
- [ ] インライン構造体のネスト（インライン内にさらにインライン）を許可する

### 変更する既存機能

- [ ] `YamlToIrMapper` — `fields` プロパティがある場合に匿名構造体を生成
- [ ] `FormatValidator` — インライン構造体のバリデーション追加

### 変更しないもの（スコープ外）

- 名前付きstruct定義の既存動作 — 変更なし
- `struct` プロパティによる参照 — 変更なし（`struct` と `fields` は排他的）
- switch/casesのインライン化 — 将来課題

## 受入条件

1. [ ] `fields` プロパティを持つstruct型フィールドがインライン構造体としてデコードされること
2. [ ] インライン構造体のフィールドが変数として正しくバインドされること
3. [ ] `struct` と `fields` が同時に指定された場合にバリデーションエラーになること
4. [ ] インライン構造体がネストできること
5. [ ] インライン構造体に `endianness` 等のstruct属性が適用されること
6. [ ] 出力フォーマッタがインライン構造体を通常の構造体と同様に表示すること
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldDefinition` — `InlineFields` プロパティ追加。`FormatValidator` — インラインstruct検証 |
| BinAnalyzer.Dsl | `YamlFieldModel` — `fields` プロパティ追加。`YamlToIrMapper` — インラインstruct→IR変換 |
| BinAnalyzer.Engine | `BinaryDecoder` — インラインフィールドのデコード（匿名structとして処理） |
| BinAnalyzer.Output | 変更なし（既存のDecodedStruct表示で対応） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — インライン構造体の構文と使い方の説明追加
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針
### モデル変更
### インタフェース変更
### 代替案
### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更
### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
