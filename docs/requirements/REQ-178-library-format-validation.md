# REQ-178: 共通ライブラリファイルの単体バリデーション

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | REQ-155 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

REQ-126（取り下げ）の受入条件 4「共通定義ファイル単体でもバリデーションが通ること」を切り出した要望。

`formats/common/` の共通ライブラリファイル（REQ-155）は、import される前提で書かれているため、単体で `binanalyzer validate` にかけると通らない（2026-09-18 実測）:

| ファイル | 結果 | 内容 |
|---|---|---|
| `common/riff.bdef.yaml` | OK | VAL109（`raw_data` がルート `_riff_common` から到達不能）警告 1 件 |
| `common/isobmff.bdef.yaml` | NG | VAL002（`container_box.children` が参照する `iso_box` が未定義）エラー 1 件、VAL109 警告 5 件 |

原因は 2 つある。

- `root` が必須（`YamlToIrMapper` が root struct の存在を要求、JSON Schema でも `required`）なので、ライブラリは実体のない dummy root struct（`_isobmff_common` / `_riff_common`）を置いている。その結果、提供する全 struct が VAL109（到達不能）警告になる
- `container_box` は import 元が定義する `iso_box` を参照する設計（REQ-155）なので、単体では VAL002（未定義 struct 参照）がエラーになる

ライブラリファイルを編集したときに単体で構文・参照を検証できないと、import 元のフォーマットをデコードするまで誤りに気づけない。ライブラリであることを宣言できるようにし、バリデータがそれに応じた規則で検証する。

## 機能要件

### 追加する機能

- [ ] トップレベルに `library: true` を追加できる（`YamlFormatModel` / `FormatDefinition` / JSON Schema）。`library: true` のとき `root` は省略可
- [ ] `library: true` のファイルに対する `validate` の規則:
  - VAL109（到達不能 struct）を出さない（ルートがないので到達性の概念がない）
  - 未定義 struct への参照（VAL002 / VAL003 相当）はエラーではなく警告（新コード、例: VAL110「import 元での定義を前提とする外部参照」）
  - それ以外の検証（フィールド型・式・switch 構造・enum / flags 参照など）は通常どおりエラー
- [ ] `library: true` のファイルを `-f` に直接指定してデコードしようとした場合は、明確なエラー（「ライブラリ定義はデコードに使えません」）で終了
- [ ] `common/isobmff.bdef.yaml` / `common/riff.bdef.yaml` に `library: true` を付け、dummy root struct を削除
- [ ] 全 `formats/**/*.bdef.yaml` を `validate` にかけるテストを追加し、ライブラリを含めてエラー 0 件であることを CI で担保

### 変更する既存機能

- [ ] `YamlToIrMapper` — `library: true` かつ `root` 省略時に例外を投げない（`RootStruct` は空文字または null）
- [ ] `FormatValidator` — `library` フラグに応じた規則の切り替え
- [ ] `FormatDefinition.RootStruct` — nullable 化、またはライブラリ用の別プロパティ。エンジン・GUI・TUI で `RootStruct` を参照する箇所への影響を確認

### 変更しないもの（スコープ外）

- import 機構の拡張（名前空間、選択的 import）
- import 先で `library: true` のファイルの dummy root が VAL109 を出す問題はこの要望で解消されるが、それ以外の import 時の警告仕様の変更は行わない

## 受入条件

1. [ ] `binanalyzer validate formats/common/isobmff.bdef.yaml` と `formats/common/riff.bdef.yaml` がエラー 0 件で OK になること（外部参照は警告として表示されてよい）
2. [ ] `library: true` かつ `root` 省略のファイルがロードできること。`library` を付けずに `root` を省略した場合は従来どおりエラー
3. [ ] `library: true` のファイルを `-f` に指定してデコードすると、明確なメッセージで終了コード 1 になること
4. [ ] `library: true` のファイルで、未定義 struct 参照以外の誤り（不正な型名、壊れた式など）は従来どおりエラーになること
5. [ ] MP4 / HEIF / WAV / AVI / WebP のデコード結果（ゴールデン）が変わらないこと
6. [ ] `schemas/bdef.schema.json` が `library` を受け入れ、`root` を条件付き必須にすること（`JsonSchemaTests` で確認）
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FormatDefinition` に `IsLibrary`、`RootStruct` の扱い変更、`FormatValidator` の規則追加 |
| BinAnalyzer.Dsl | `YamlFormatModel.Library`、`YamlToIrMapper` の root 省略対応 |
| BinAnalyzer.Engine | ライブラリ定義でのデコード拒否（または CLI 側で拒否） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | `validate` の出力（新警告コード）、デコード時のライブラリ拒否メッセージ |
| BinAnalyzer.Web / Gui | `FormatService` のフォーマット一覧からライブラリを除外する（現状の除外方法を確認） |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — `library:` の説明、共通定義ライブラリ節の更新、検証コード一覧に新コードを追加
- [ ] docs/architecture.md — 変更不要
- [ ] CLAUDE.md — 変更不要
- [ ] README.md — 変更不要
- [ ] schemas/bdef.schema.json — `library` 追加、`root` の条件付き必須

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

- `library:` を追加せず、受入条件を「import 元と合わせた状態で検証が通ること」に緩める案（REQ-126 で検討）。ライブラリ単体の検証ができない状態が残るので本要望では採らない
- ファイル名やディレクトリ（`common/` 配下）でライブラリ判定する案。暗黙の規約になり、`formats/` 以外に置いた定義で機能しないため不採用候補

### 懸念事項

- `FormatDefinition.RootStruct` を nullable にすると、エンジン・Presentation・GUI で参照する箇所すべてに null 分岐が要る。ライブラリ定義はデコードに渡さない前提で、ロード直後に弾く方が影響が小さい

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
