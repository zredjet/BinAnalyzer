# REQ-126: import機能の実用化（共通定義ライブラリの構築）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 取り下げ |
| 優先度 | 高 |
| 依存 | REQ-026、REQ-155 |
| 作成日 | 2026-02-17 |
| 更新日 | 2026-09-18 |
| 却下日 | 2026-09-18 |

## 背景・動機

> **取り下げ（2026-09-18）**: 本要望の中核（`formats/common/` の構築と既存定義の imports 化）は REQ-155 で実現済み。残件 2 点は REQ-178（共通ライブラリファイルの単体バリデーション）と REQ-179（DOS datetime の共通化）として起票し直した。以降はそれぞれの要望を参照。詳細は「REQ-155 による対応状況」を参照。

REQ-026 でインポート機能の基盤（DSLパーサーでのマージ処理）は実装済みだが、実際のフォーマット定義（formats/）では一切使用されていない。33個のフォーマット定義間で共通構造体・enum・flagsの重複が存在する。

インポート機能を実用化するために、共通定義ライブラリを構築し、既存フォーマット定義をリファクタリングする必要がある。

### 共通化が見込まれるパターン

- タイムスタンプ構造（Unix epoch, DOS datetime, Windows FILETIME等）
- TLV（Type-Length-Value）パターン
- 圧縮方式enum（deflate, bzip2, lzma等 — ZIP, 7z, gzip等で共通）
- チェックサム/ハッシュ関連の共通enum

## REQ-155 による対応状況（2026-09-18 整理）

本要望の起票（2026-02-17）後に起票された REQ-155（共通定義ライブラリ / 標準型ライブラリ、done）が、本要望の中核である「`formats/common/` の構築」と「既存定義の imports 化」を実施した。さらに REQ-170（done）で Web / WASM からも imports 付き定義を読めるようになり、「実用化」の前提はすべて揃っている。

### REQ-155 で対応済み

| 本要望の項目 | 対応内容 |
|---|---|
| `formats/common/` ディレクトリの作成 | `common/isobmff.bdef.yaml`（`container_box`, `ftyp_box`, `hdlr_box`, `dref_box`, `raw_data`）と `common/riff.bdef.yaml`（`raw_data`）を配置 |
| 既存定義の imports リファクタリング | MP4 / HEIF / WAV / AVI / WebP の 5 定義が imports を使用（38 定義中） |
| TLV パターンの共通化 | ISO BMFF の box、RIFF の chunk としてファミリー別に共通化。tag 型・length 型・エンディアンがファミリーごとに異なるため、これ以上汎用の TLV 構造体は切り出せない |
| 受入条件 1・2・3・5 | REQ-155 の受入条件 1〜4 と同内容で達成済み（ゴールデンテストで結果同一を確認） |
| docs/dsl-reference.md の実例更新 | 「インポート > 共通定義ライブラリ」節に ISO BMFF / RIFF の実例を記載済み |

### 調査の結果、共通化の対象にならないもの

- **圧縮方式 enum** — 同名の `compression_method` が ZIP / gzip / PNG にあるが値の集合が異なる（ZIP: 0/8/9/12/14/93/95、gzip: 8 のみ、PNG: 0=deflate）。BMP・TIFF も各自のコード体系を持つ。値が違う以上 1 つの enum にまとめられず、同名のまま import すると名前衝突エラーになる。対象外とする。
- **チェックサム / ハッシュ enum** — `formats/` にハッシュやチェックサム種別を表す enum は 1 つも存在せず、重複がない。対象外とする。
- **Unix epoch タイムスタンプ** — gzip `mtime` と Mach-O `timestamp` は単なる `uint32` で、struct 化しないと import で共有できない。tar は 8 進 ASCII、MP4 は 1904 年基準で形式が異なる。単一フィールドを struct に包む変更は木構造（パス）を変え、受入条件 3「同じデコード結果」と両立しないため対象外とする。

### 残件

対応済みの内容を除くと、本要望に残るのは次の 2 点である。

#### 残件 1: 共通定義ファイル単体のバリデーション（受入条件 4）

2026-09-18 に `binanalyzer validate` で実測した結果:

| ファイル | 結果 | 内容 |
|---|---|---|
| `common/riff.bdef.yaml` | OK | VAL109（`raw_data` がルート `_riff_common` から到達不能）警告 1 件 |
| `common/isobmff.bdef.yaml` | NG | VAL002（`container_box.children` が参照する `iso_box` が未定義）エラー 1 件、VAL109 警告 5 件 |

isobmff の NG は REQ-155 の設計（`container_box` は import 元が定義する `iso_box` を参照し、マージ後の統合名前空間で解決する）に起因しており、単体では原理的に解決できない。対応の選択肢:

- ライブラリファイルであることを宣言する DSL 属性（例: `library: true`）を追加し、バリデータが「ルート不要・未定義 struct 参照は警告止まり」として扱う（DSL / Validator の変更を伴うので REQ-026 の拡張になる）
- 受入条件 4 を「import 元と合わせた状態で検証が通ること」に緩め、単体検証はスコープ外とする（REQ-155 のゴールデンテストで既に担保済み）

#### 残件 2: DOS datetime の共通化

- ZIP（`mod_time` / `mod_date`、local header と central directory の 2 箇所）と FAT（`modification_time` / `modification_date`、`creation_*`、`last_access_date`）が同じ 16 bit × 2 のレイアウトを持ち、FAT 側だけが `virtual` で年月日・時分秒を展開している
- 共通化する場合は `common/dos_datetime.bdef.yaml` に `dos_time` / `dos_date`（各 `uint16` + `virtual` 展開）の struct を置き、ZIP / FAT から import する
- ただし ZIP のフィールドをネスト struct に置き換えるとパスが `mod_time` → `mod_time.hours` のように変わり、ゴールデンファイル・`patch` のフィールドパス・既存テストが影響を受ける。「デコード結果同一」を維持できないので、受入条件 3 は「値は同一、木構造の変更はゴールデン再生成で許容」に緩める必要がある

残件 2 は効果が ZIP / FAT の 2 定義に限られ、木構造の変更を伴う。残件 1 は本要望というより共通ライブラリの検証ルールの問題である。**本要望は取り下げ、残件 1 を REQ-178、残件 2 を REQ-179 として起票し直した（2026-09-18）。**

## 機能要件

### 追加する機能

- [x] `formats/common/` ディレクトリの作成 — REQ-155 で対応済み
- [ ] 共通タイムスタンプ定義ファイル — 残件は DOS datetime のみ（ZIP / FAT）。Unix epoch は対象外（上記）
- [x] 共通圧縮方式enum定義ファイル — 調査の結果、対象外（値の集合がフォーマットごとに異なる）
- [x] 共通TLV構造体定義ファイル（必要に応じて） — REQ-155 でファミリー別（ISO BMFF / RIFF）に対応済み。汎用 TLV は対象外

### 変更する既存機能

- [x] 既存フォーマット定義のうち、共通定義を使えるものをimportsを使ったリファクタリング — REQ-155 で MP4 / HEIF / WAV / AVI / WebP の 5 定義を対応済み。残件は ZIP / FAT（DOS datetime）

### 変更しないもの（スコープ外）

- インポート機能自体の拡張（名前空間、選択的インポート等）
- 全フォーマットの一括リファクタリング — 効果が高いものから段階的に
- 圧縮方式 enum・チェックサム enum・Unix epoch タイムスタンプの共通化（上記「対象にならないもの」）

## 受入条件

1. [x] `formats/common/` に1つ以上の共通定義ファイルが作成されていること — REQ-155 で達成
2. [x] 2つ以上の既存フォーマット定義がimportsを使って共通定義を参照していること — REQ-155 で達成（5 定義）
3. [x] リファクタリング後のフォーマットが変更前と同じデコード結果を返すこと — REQ-155 で達成（ゴールデンテスト）。残件の DOS datetime を進める場合は「値は同一、木構造の変更はゴールデン再生成で許容」に緩める
4. [ ] 共通定義ファイル単体でもバリデーション（`--no-validate` なし）が通ること — **未達成**（2026-09-18 に `validate` で実測）。`common/riff.bdef.yaml` は OK（VAL109 警告 1 件）だが、`common/isobmff.bdef.yaml` は `container_box` が import 元の `iso_box` を参照するため VAL002 エラーで NG（加えて VAL109 警告 5 件）。残件
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過） — REQ-155 で達成

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — importsの使用例を実例ベースで更新 — REQ-155 / REQ-170 で対応済み（「インポート」「共通定義ライブラリ」節）
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

- 本要望の中核は REQ-155 で実現された。詳細は「REQ-155 による対応状況」を参照
- 残件は REQ-178（共通ライブラリファイルの単体バリデーション）、REQ-179（DOS datetime の共通化）に分割
