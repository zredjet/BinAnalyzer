# REQ-179: DOS datetime の共通定義化（ZIP / FAT）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 低 |
| 依存 | REQ-155 |
| 作成日 | 2026-09-18 |
| 更新日 | 2026-09-18 |

## 背景・動機

REQ-126（取り下げ）の「共通タイムスタンプ定義」のうち、調査の結果で唯一共通化の効果があった DOS datetime を切り出した要望。

MS-DOS 形式の日時（16 bit の time + 16 bit の date）は ZIP と FAT で同じレイアウトを持つ。

| フォーマット | フィールド | 展開 |
|---|---|---|
| `zip.bdef.yaml` | `mod_time` / `mod_date`（local file header と central directory の 2 箇所） | なし（生の uint16 のまま） |
| `fat.bdef.yaml` | `creation_time` / `creation_date`、`last_access_date`、`modification_time` / `modification_date` | `virtual` で年・月・日・時・分・秒を展開（`mod_*` / `create_*` の 12 フィールド） |

FAT だけが年月日に展開しており、ZIP は生の値のまま。展開ロジック（`(date >> 9 & 0x7F) + 1980` 等）を共通ライブラリに置いて両方から import すれば、ZIP でも日時が読めるようになり、FAT の重複した `virtual` 定義も減る。

Unix epoch（gzip `mtime`、Mach-O `timestamp`）や MP4 の 1904 年基準タイムスタンプは形式が異なる、または単一の `uint32` で struct 化の効果がないため対象外（REQ-126 の調査結果）。

## 機能要件

### 追加する機能

- [ ] `formats/common/dos_datetime.bdef.yaml` を追加。`dos_datetime` struct（`time: uint16`、`date: uint16` + `year` / `month` / `day` / `hour` / `minute` / `second` の `virtual`）と、日付のみの `dos_date` struct を提供
- [ ] `zip.bdef.yaml` の local file header / central directory の `mod_time` + `mod_date` を `dos_datetime` struct の 1 フィールド（例: `modified`）に置き換え
- [ ] `fat.bdef.yaml` の `creation_time` + `creation_date` を `created`、`modification_time` + `modification_date` を `modified`（`dos_datetime`）、`last_access_date` を `dos_date` に置き換え、`mod_*` / `create_*` の `virtual` 12 フィールドを削除

### 変更する既存機能

- [ ] ZIP のフィールドパス: `mod_time` / `mod_date` → `modified.time` / `modified.date`（+ `modified.year` 等）。`patch --set` で指定するパス、ゴールデン `testdata/golden/zip.json`、`ZipParsingTests` が影響を受ける
- [ ] FAT のフィールドパス: `modification_time` → `modified.time`、`mod_year` → `modified.year` 等。`FatParsingTests` が影響を受ける
- [ ] 両ファイルに `imports: [common/dos_datetime.bdef.yaml]` を追加。ZIP / FAT は Web の `HttpImportResolver` 経由でも読めること（REQ-170 で対応済みの経路）

### 変更しないもの（スコープ外）

- ZIP の extra field（0x5455 extended timestamp、0x000a NTFS）の解釈
- `dos_datetime` を「日時文字列」として 1 行に表示する表示型の追加（`virtual` の整数展開まで）
- Unix epoch / FILETIME / 1904 年基準タイムスタンプの共通化

## 受入条件

1. [ ] `common/dos_datetime.bdef.yaml` が存在し、ZIP と FAT の 2 定義が import していること
2. [ ] ZIP のデコードで `modified.year` 等の値が、変更前の `mod_time` / `mod_date` から手計算した値と一致すること（`ZipParsingTests`）
3. [ ] FAT のデコードで `modified.*` / `created.*` の値が、変更前の `mod_*` / `create_*` の値と一致すること（`FatParsingTests`）
4. [ ] 生バイト列の値（`modified.time` / `modified.date`）は変更前の `mod_time` / `mod_date` と同一。木構造の変更はゴールデンファイルの再生成で許容する（REQ-126 受入条件 3 の緩和）
5. [ ] `RealFileValidationTests` の ZIP / FAT が通ること
6. [ ] `docs/dsl-reference.md` の共通定義ライブラリ表に `common/dos_datetime.bdef.yaml` が追加されていること
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし（`patch` のパス指定例をドキュメントで更新） |
| formats / testdata | `common/dos_datetime.bdef.yaml` 追加、`zip.bdef.yaml` / `fat.bdef.yaml` 変更、`testdata/golden/zip.json` 再生成 |
| tests | `ZipParsingTests` / `FatParsingTests` / `GoldenFileTests` のパス更新 |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 共通定義ライブラリ表に追加、使用例
- [ ] docs/cli-usage.md — `patch --set` の例で ZIP のパスを使っていれば更新
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

- `virtual` の展開だけを共通化し、`mod_time` / `mod_date` は平坦なまま残す案。import で共有できるのは struct / enum / flags 単位なので、フィールド単位の共有はできず不採用
- ZIP は現状のまま（生の値）とし FAT だけ整理する案。ZIP で日時が読めないままになるので、共通化の効果が薄い

### 懸念事項

- `patch --set mod_time=...` のように既存のパスを使っているユーザー向けの互換性。REQ-164 は done 直後で利用実績が薄いため、ドキュメント更新のみで対応する想定
- 共通ライブラリファイルは単体で `validate` が通らない（REQ-178）。`dos_datetime.bdef.yaml` は外部参照を持たないので dummy root を置けば VAL109 警告のみで OK になるが、REQ-178 が先に入っていれば `library: true` を使う

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
