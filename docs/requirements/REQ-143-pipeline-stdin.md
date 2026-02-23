# REQ-143: パイプライン統合（stdin入力・CI/CD向け）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-22 |

## 背景・動機

現在のCLIはファイルパスを引数として受け取る。パイプラインやCI/CDスクリプトでの利用を考えると、標準入力からのバイナリデータ読み込みや、他のコマンドとの組み合わせが求められる。

### ユースケース

1. **パイプ入力** — `curl -s https://example.com/file.bin | binanalyzer -f format.yaml` のようにネットワーク取得と組み合わせる
2. **CI/CDバリデーション** — ビルド成果物のバイナリ構造検証を自動化パイプラインに組み込む
3. **部分バイナリの解析** — `dd if=disk.img bs=512 count=1 | binanalyzer -f mbr.yaml` のように切り出したデータを解析
4. **JSON出力のパイプ** — `binanalyzer file.bin -f format.yaml -o json | jq '.header.version'` でフィールド値を抽出

### 現在の制約

- stdin入力が不可 — ファイルパスが必須
- 終了コードが成功(0)/エラー(1)のみ — バリデーション結果を細かく返せない
- マシンリーダブルなエラー出力がない

## 機能要件

### 追加する機能

- [x] `-` または `--stdin` でstdinからバイナリデータを読み込む
- [x] `--quiet` / `-q` — デコード結果を出力せず、終了コードのみ返す（バリデーション用途）
- [x] 終了コードの拡張 — 0=成功、1=エラー、2=バリデーション失敗（デコード成功だが `validate` 式がfalse）
- [x] stderr へのエラー出力のJSON形式オプション（`--error-format json`）

### 変更する既存機能

- [x] CLIの入力引数処理 — stdin対応
- [x] 終了コードの定義拡張

### 変更しないもの（スコープ外）

- stdinからのフォーマット定義読み込み — ファイルパス指定のまま
- 対話的入力 — stdinはバイナリデータのみ想定

## 受入条件

1. [x] `echo -n "data" | binanalyzer - -f format.yaml` でstdinからデコードできること
2. [x] `--stdin` オプションでも同様にstdinからデコードできること
3. [x] `--quiet` 指定時にstdoutに何も出力されないこと
4. [x] バリデーション失敗時の終了コードが2であること
5. [x] `--error-format json` でstderrにJSON形式のエラーが出力されること
6. [x] パイプが閉じられた場合にBrokenPipeエラーが適切にハンドリングされること
7. [x] 既存のファイルパス指定が引き続き動作すること
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | stdin読み込み、`--quiet`/`--error-format` オプション追加、終了コード拡張 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — パイプライン使用例の追加
- [x] docs/cli-usage.md — stdin、--quiet、終了コードの説明追加

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

- 変更はCLIプロジェクトのみ。Core/Dsl/Engine/Output は変更なし。
- `fileArg` を `Argument<string?>` (optional) に変更し、`-` または `--stdin` でstdin読み込み。
- `CliErrorReporter` クラスでエラー出力の text/json 切り替えを抽象化。
- `HasValidationFailures` 再帰関数で validate 式の失敗を検出し、終了コード 2 を返す。
- SIGPIPE を `PosixSignalRegistration` で無視し、`Console.Write` の `IOException` を catch して exit 0。

### モデル変更

なし

### インタフェース変更

なし

### 代替案

- stdin 読み込みに `Console.In` ではなく `Console.OpenStandardInput()` を使用（バイナリデータのためテキストストリームは不適）。

### 懸念事項

- stdin は全データをメモリに読み込むため、巨大ファイルではOOMの可能性がある（現状のファイル読み込みも同様）。

---

## 実装メモ

### 実装中の設計変更

- エラー出力の置換時、バリデーション結果は `ReportValidationResult` で一括出力する方式に統一。
- `--quiet` でもstderrへのエラーサマリーは出力する（終了コードの理由を知るため）。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| PipelineTests | StdinDash_DecodesSuccessfully | AC1 |
| PipelineTests | StdinFlag_DecodesSuccessfully | AC2 |
| PipelineTests | Quiet_NoStdoutOutput | AC3 |
| PipelineTests | Quiet_ExitCode0_OnSuccess | AC3 |
| PipelineTests | ErrorFormatJson_ProducesJson | AC5 |
| PipelineTests | ErrorFormatJson_DecodeException | AC5 |
| PipelineTests | BrokenPipe_ExitsCleanly | AC6 |
| PipelineTests | FilePath_StillWorks | AC7 |
| PipelineTests | StdinAndFile_Error | — |
| PipelineTests | NoFileNoStdin_Error | — |

### 気づき・今後の課題

- exit code 2 のテストは validate 式を含むフォーマット定義が必要。今後 validate 対応のテストデータ整備時に追加予定。
