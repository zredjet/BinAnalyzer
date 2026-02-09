# REQ-083: 変数スコープ可視性改善

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在、bitfieldの子フィールドやvirtualフィールドの値が、同一struct内の後続フィールドの式から参照できない。これにより8フォーマット（FLV, PE, GIF, GZIP, ICO, LZ4, MP3, FLAC）でデコードエラーが発生し、テストでは`DecodeWithRecovery(ErrorMode.Continue)`を使わざるを得ない状態になっている。

例: GIFフォーマットでは、bitfieldで抽出した`global_color_table_flag`を後続フィールドの`if`条件で参照するが、ExpressionEvaluator.ResolveFieldがbitfield子フィールドのスコープを検索できず、変数未定義エラーになる。

```yaml
# 現在エラーになるパターン:
- name: packed
  type: bitfield
  size: 1
  fields:
    - name: global_color_table_flag
      bits: "7"

- name: global_color_table
  type: bytes
  size: "3 * (1 << (gct_size + 1))"
  if: "global_color_table_flag == 1"    # ← 参照不可
```

## 機能要件

### 追加する機能

- [x] bitfieldの各子フィールド値を、bitfieldが所属するstruct（親スコープ）の変数として自動的に登録する
- [x] virtualフィールドの値を、そのvirtualが所属するstructスコープの変数として登録する（現状通り）
- [ ] 子structで定義されたフィールド値が、親struct内の後続フィールド式から参照可能になること → 今回のスコープでは不要（bitfield/virtualの直接修正で解決）

### 変更する既存機能

- [ ] ~~`DecodeContext`のスコープスタック: bitfieldデコード後、子フィールド値を親スコープにマージ~~ → 不要（SetVariable追加で解決）
- [ ] ~~`ExpressionEvaluator.ResolveField`: bitfield子フィールドの探索パスを追加~~ → 不要（SetVariable追加で解決）

### 変更しないもの（スコープ外）

- スコープの基本構造（スタックベース）は変更しない
- 異なるstruct間のクロスリファレンス（ドット記法等）は対象外
- switch内の分岐別スコープの扱いは変更しない

## 受入条件

1. [x] bitfieldの子フィールド値が、同一struct内の後続フィールド式で参照できること
2. [x] virtualフィールドの値が、同一struct内の後続フィールド式で参照できること
3. [x] GIFフォーマット: `global_color_table_flag`による条件分岐が`Decode()`で正常動作すること
4. [x] GZIPフォーマット: `FEXTRA`等のフラグによる条件分岐が`Decode()`で正常動作すること
5. [x] FLVフォーマット: virtual `data_size`参照が`Decode()`で正常動作すること
6. [△] 8フォーマットのうち6フォーマット（GIF, GZIP, LZ4, FLV, MP3, FLAC→一部）が`Decode()`に移行。ICO（seek_restoreスコープ問題）、PE（前方参照問題）、FLAC（8バイトbitfieldサイズ制約=REQ-085）は`DecodeWithRecovery`維持
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）— 568テスト全通過

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | BinaryDecoder: DecodeBitfieldField/DecodeVirtualFieldにSetVariable追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — スコープ可視性ルールの記述更新（今回は動作変更のみ、ドキュメントは別途）

### 変更が必要なフォーマット定義

なし（DSL側の修正のみ。フォーマット定義は現状のまま正常動作するようになる）

### 変更が必要なテスト

- [x] 6フォーマットの統合テスト: `DecodeWithRecovery` → `Decode` に変更、アサーション強化

---

## 設計メモ

当初はDecodeContextのスコープスタック改修やExpressionEvaluatorのフィールド解決パス追加を検討していたが、根本原因を調査した結果、より単純な修正で解決できることが判明した。

**根本原因**: `DecodeBitfieldField()` がbitfieldエントリ値を `SetVariable` していない、`DecodeVirtualField()` が評価結果を `SetVariable` していない、という2箇所の欠落。

**修正方針**: 2行追加のみ。DecodeContextやExpressionEvaluatorの変更は不要。

---

## 実装メモ

### 実装中の設計変更

- DecodeContext/ExpressionEvaluator の改修は不要であった（SetVariable追加のみで解決）
- ICOは `seek_restore` 時のスコープ消失が原因（bitfield/virtualとは無関係）→ `DecodeWithRecovery` 維持
- FLACは virtual修正の恩恵を受けるが、8バイトbitfield（STREAMINFO）がサイズ制約(1,2,4のみ)で失敗 → `DecodeWithRecovery` 維持、REQ-085で対応

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `DecodeBitfieldField()`: エントリループ内に `context.SetVariable(entry.Name, value)` 追加、`DecodeVirtualField()`: Evaluate後に `context.SetVariable(field.Name, value)` 追加 |
| `tests/BinAnalyzer.Engine.Tests/BitfieldDecoderTests.cs` | 2テスト追加（bitfieldエントリ値のif条件参照） |
| `tests/BinAnalyzer.Engine.Tests/VirtualFieldTests.cs` | 2テスト追加（virtual値のsize式/if条件参照） |
| `tests/BinAnalyzer.Integration.Tests/GifParsingTests.cs` | `DecodeWithRecovery` → `Decode()`、bitfieldアサーション強化 |
| `tests/BinAnalyzer.Integration.Tests/GzipParsingTests.cs` | `DecodeWithRecovery` → `Decode()`、bitfieldアサーション強化 |
| `tests/BinAnalyzer.Integration.Tests/Lz4ParsingTests.cs` | `DecodeWithRecovery` → `Decode()`、bitfieldアサーション強化 |
| `tests/BinAnalyzer.Integration.Tests/FlvParsingTests.cs` | `DecodeWithRecovery` → `Decode()` |
| `tests/BinAnalyzer.Integration.Tests/Mp3ParsingTests.cs` | `DecodeWithRecovery` → `Decode()`、virtualフィールドアサーション追加 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitfieldDecoderTests | Decode_BitfieldEntryValue_AvailableInIfCondition | 1 |
| BitfieldDecoderTests | Decode_BitfieldEntryValue_ConditionFalse_FieldSkipped | 1 |
| VirtualFieldTests | VirtualField_ValueAvailableInSizeExpression | 2 |
| VirtualFieldTests | VirtualField_ValueAvailableInCondition | 2 |

### DecodeWithRecovery残存フォーマット

| フォーマット | 原因 | 対応REQ |
|---|---|---|
| FLAC | 8バイトbitfieldサイズ制約 | REQ-085 |
| ICO | seek_restoreスコープ消失 | 未定 |
| PE | switch_onの前方参照 | 未定 |

### 気づき・今後の課題

- ICOの `seek_restore` 後にseek先で読んだ変数がスコープから消える問題は、bitfield/virtualとは独立した課題。seek_restoreのスコープ保持改善が必要
- FLACの8バイトbitfieldはREQ-085（bitfieldサイズ拡張）で対応予定
