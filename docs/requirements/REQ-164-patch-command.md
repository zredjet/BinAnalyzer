# REQ-164: binanalyzer patch コマンド

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-169（`FieldEncoder` / `BinaryPatcher`） |
| 作成日 | 2026-02-23 |
| 更新日 | 2026-09-18 |

## 背景・動機

現在 BinAnalyzer はバイナリファイルの「読み取り専用」ツールである。しかし、フォーマット定義に基づいてフィールド値を書き換えたいユースケースがある。例えば、ヘッダーのバージョン番号変更、チェックサムの再計算、フラグビットの切り替え等。

フォーマット定義が構造とオフセットを知っているため、フィールド名を指定して値を書き換える「構造認識型バイナリエディタ」を提供できる。

## 機能要件

### 追加する機能

- [x] `binanalyzer patch <file> -f <format> --set <field>=<value> -o <output>` サブコマンド
- [x] フィールドパスによる値指定（例: `header.version=2`）
- [x] 整数型フィールドの書き換え
- [x] バイト列フィールドの書き換え（16進指定）
- [x] 文字列フィールドの書き換え
- [x] ドライランモード（`--dry-run`）— 変更内容の表示のみ
- [x] 変更範囲を含むチェックサムの再計算（既定。`--no-checksum` で抑止）と enum のラベル指定（REQ-169 の基盤により追加）

### 変更する既存機能

- [ ] なし

### 変更しないもの（スコープ外）

- サイズ変更を伴うフィールド書き換え（固定長フィールドのみ対応）
- 圧縮領域内のフィールド書き換え

> チェックサムの自動再計算は当初スコープ外だったが、REQ-169 で `BinaryPatcher` が実装されたので既定で行う（`--no-checksum` で抑止）。

## 受入条件

1. [x] `binanalyzer patch --set` で指定フィールドの値が書き換わること
2. [x] 元ファイルを変更せず、出力ファイルに書き込むこと
3. [x] `--dry-run` で変更内容が表示され、ファイルが作成されないこと
4. [x] 書き換え後のファイルが元のフォーマット定義で正しくデコードできること
5. [x] 存在しないフィールドパスを指定した場合にエラーが出ること
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし（REQ-169 の `Patching/` を使用） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし（REQ-169 の `FieldEncoder` / `BinaryPatcher` を使用。`TryParseInteger` を public に） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | `patch` サブコマンド追加（`PatchCommand.cs`）、Presentation を参照 |

### 変更が必要なドキュメント

- [x] docs/cli-usage.md — patch コマンドの説明追加
- [x] docs/architecture.md — Cli → Presentation の依存と `PatchCommand` を追記
- [x] CLAUDE.md — 変更不要
- [x] README.md — 機能一覧に追記

---

## 設計メモ

### 設計方針

- **エンコードとパッチ適用は REQ-169 の基盤をそのまま使う。** `FieldEncoder`（値 → バイト列、範囲チェック）と `BinaryPatcher`（固定長パッチ適用 ＋ 変更範囲を含むチェックサムの再計算）は Engine にあり、CLI は「引数解析 → パス解決 → エンコード → 適用 → 出力」だけを担う。GUI と CLI で書き戻しの結果が一致する。
- **フィールドパスは GUI と同じ規則。** 一度デコードして Presentation の `NodeIndex` を作り、`ByPath("chunks[0].data.width")` で引く。struct は `.`、配列要素は `[i]`。存在しなければ `フィールドが見つかりません` で終了コード 1。パスの解決はデコード時のレイアウトに対して行うので、複数の `--set` は全て元ファイル基準（長さフィールドを変えた影響で他のフィールドが動くことは考慮しない。その旨を注意として出す）。
- **編集可否は `FieldEditability` で判定する。** 可変長整数・NUL 終端文字列・ビットストリーム・圧縮ストリーム内・struct / array は理由付きで拒否する。
- **enum はラベルでも指定できる。** `--set color_type=truecolor_alpha` のように、数値として解釈できず enum 参照があればラベルから値を引く。
- **出力は必ず別ファイル。** `-o` が入力と同じパスならエラー（元ファイルを変更しない、という受入条件 2 をコマンドの仕様として固定する）。`--dry-run` では `-o` 省略可で、変更内容（パス・旧値 → 新値・オフセット・バイト列・再計算されるチェックサム）だけを表示する。
- **適用後に再デコードして検証結果を報告する。** チェックサム不一致やデコードエラーが残る場合は標準エラーに警告を出すが、終了コードは 0（ユーザーが意図的に壊すケースもある）。

### モデル変更

なし（REQ-169 で追加済みの `DecodedNode.DslType` 等を使う）。

### インタフェース変更

- Cli: `patch` サブコマンド（`src/BinAnalyzer.Cli/PatchCommand.cs`）。`Program.cs` は引数定義と `PatchCommand.Run` の呼び出しのみ。
- Cli が `BinAnalyzer.Presentation` を直接参照する（`NodeIndex` / `FieldEditability` のため。これまでは Tui 経由の推移的参照だった）。

### 代替案

- 独自のパス構文（`header.version` のみ、配列非対応）— GUI の検索 / 差分と同じ `PathFilter` 系の記法に揃えるほうが学習コストが低いので `NodeIndex` の規則を使う。
- Core に「フィールドパス解析ユーティリティ」を新設する案（要件書の影響範囲）— `NodeIndex` が既にパス → ノードの索引を持っているので不要。
- `--in-place` で上書きを許す案 — 誤操作の被害が大きいので今回は入れない。必要なら別 REQ。

### 懸念事項

- 長さ・個数を決めるフィールドを書き換えると、出力ファイルの後続オフセットは元と変わる。複数 `--set` の解決は元ファイル基準なので、そのような組み合わせは意図どおりにならない可能性がある。適用後の再デコードで検出できるエラーは警告として出す。
- `--set` の値に `=` を含めたい場合は最初の `=` で分割する（`name=a=b` → 値 `a=b`）。

---

## 実装メモ

### 実装中の設計変更

- 要件の「チェックサムの自動再計算はスコープ外」は、REQ-169 で `BinaryPatcher` が先にできたため既定で行う仕様に変えた。`--no-checksum` で従来案どおりの「本体だけ書き換える」動作になる。
- Core への「フィールドパス解析ユーティリティ」追加は不要になった（Presentation の `NodeIndex.ByPath` を使用）。そのため Cli が Presentation を直接参照する。
- 適用後に再デコードし、デコードエラー・チェックサム不一致が残れば標準エラーに警告を出す（終了コードは 0）。`--no-checksum` の効果や、長さフィールドを変えた影響を即座に確認できる。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Cli.Tests / PatchCommandTests | `Patch_SetInteger_WritesOutput_RecalculatesCrc_AndDecodes` | 1, 2, 4 |
| Cli.Tests / PatchCommandTests | `Patch_HexInteger_EnumLabel_StringAndBytes_MultipleSets` | 1, 4 |
| Cli.Tests / PatchCommandTests | `Patch_DryRun_PrintsChanges_AndWritesNothing` | 3 |
| Cli.Tests / PatchCommandTests | `Patch_UnknownPath_FailsWithSuggestion` | 5 |
| Cli.Tests / PatchCommandTests | `Patch_OutOfRange_NonEditable_MissingOutput_SameOutput_Fail` | 5（エラー系） |
| Cli.Tests / PatchCommandTests | `Patch_NoChecksum_LeavesCrcStale_AndWarns` | 1（`--no-checksum`） |
| Cli.Tests / PatchedFileDecodeTests（REQ-169） | `PatchedPng_DecodesWithCli_WidthChangedAndCrcValid` | 4 |
| 全体 | `dotnet test` 1,576 件通過 | 6 |

### 気づき・今後の課題

- シェル（zsh 等）では `chunks[0]` の `[` `]` がグロブ展開されるので、使用例は全てクォート付きにした。
- `--in-place`（上書き）は誤操作の被害が大きいので入れていない。必要になったら `--backup` とセットで別 REQ にする。
- 圧縮ストリーム内のフィールド書き換えは REQ-175 の後続（再圧縮）で扱う。
