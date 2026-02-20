# REQ-133: 整数型フィールドのflags展開対応

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-18 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在、`flags` 参照はASCII型フィールドでのみ有効であり、整数型フィールド（uint8/uint16/uint32等）に `flags` を指定してもデコーダが無視する。バリデータもVAL104警告を出す。

しかし、実際のバイナリフォーマットにおけるフラグフィールドはほぼ全て整数型である。既存のフォーマット定義でも多数の整数型フィールドに `flags` が指定されており、いずれもデコード時にフラグ展開されていない。

### 影響を受ける既存フォーマット定義

| フォーマット | フィールド | 型 | flags定義 |
|---|---|---|---|
| PE | `characteristics` | uint16 | `pe_characteristics` |
| PE | `dll_characteristics` | uint16 | `dll_characteristics` |
| PE | `characteristics`（セクション） | uint32 | `section_characteristics` |
| ELF | `p_flags` | uint32 | `p_flags` |
| Mach-O | `flags` | uint32 | `mh_flags` |
| Java Class | `access_flags` | uint16 | `class_access_flags` |
| Java Class | `access_flags`（メンバー） | uint16 | `member_access_flags` |
| FAT | `attributes` | uint8 | `dir_attributes` |

これらのフォーマットでは、ツリー出力でフラグの個別ビットが表示されず、生の整数値のみが表示される。フォレンジックやバイナリ解析においてフラグの各ビットの意味を即座に確認できることは重要であり、対応が望ましい。

なお、`DecodedFlags` クラスと各出力フォーマッタ（Tree/JSON/HTML/CSV）の `DecodedFlags` ハンドリングコード、および `DiffEngine` の `CompareFlags` は既に実装済みだが、デコーダがこの型のインスタンスを生成しないため、実質的に未使用コードとなっている。

## 機能要件

### 追加する機能

- [x] `BinaryDecoder.DecodeIntegerField` で `FlagsRef` が指定されている場合に `DecodedFlags` を生成する
- [x] `DecodeFlagValues` メソッドを整数型フィールドに対しても呼び出す
- [x] 変数バインディング（`context.SetVariable`）は整数値のまま維持する（式からの参照を壊さない）

### 変更する既存機能

- [x] `FormatValidator.ValidateFlagsRef` — VAL104警告の対象型を変更（ASCII型のみ → ASCII型 + 整数型を許可、それ以外は警告）
- [x] `BinaryDecoder.DecodeIntegerField` — `FlagsRef` 指定時に `DecodedInteger` ではなく `DecodedFlags` を返す
- [x] `SetPaddingFlag` / `SetValidation` — `DecodedFlags` のケースを追加

### 変更しないもの（スコープ外）

- ASCII型フィールドの `flags` ハンドリング — 既存動作（`DecodedString.Flags` プロパティ）を維持
- `DecodedFlags` クラス自体の構造変更
- 出力フォーマッタや `DiffEngine` の `DecodedFlags` ハンドリング — 既に実装済みのため変更不要

## 受入条件

1. [x] 整数型フィールド（uint8/uint16/uint32/uint64）に `flags` を指定した場合、デコード結果が `DecodedFlags` になること
2. [x] `DecodedFlags.RawValue` に元の整数値が格納されること
3. [x] `DecodedFlags.FlagStates` に各ビットのセット/クリア状態が正しく格納されること
4. [x] ツリー出力でフラグの各ビットが表示されること
5. [x] 式から変数参照した場合、整数値として参照できること（`{characteristics & 0x100}` 等）
6. [x] `FormatValidator` がVAL104警告を整数型フィールドに対して出さなくなること
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FormatValidator` — VAL104の対象型変更 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `BinaryDecoder` — `DecodeIntegerField` でflags展開、`SetPaddingFlag`/`SetValidation` にDecodedFlags対応追加 |
| BinAnalyzer.Output | 変更なし（既に `DecodedFlags` ハンドリング実装済み） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — flagsセクションに整数型フィールドでの使用例を追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

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

### 実装中の設計変更

- `DecodeIntegerField` の戻り値型を `DecodedInteger` → `DecodedNode` に変更（flags指定時は `DecodedFlags` を返すため）
- flags展開は `context.SetVariable` 実行後、enum/checksum処理の前に early return する構造
- `(uint)value` でキャスト — 現存の全flags定義は32bit以下で問題なし

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FormatValidatorTests | VAL104_FlagsRefOnNonAsciiNonIntegerField_ReportsWarning | 6 |
| FormatValidatorTests | VAL104_FlagsRefOnIntegerField_DoesNotReportWarning | 6 |
| BinaryDecoderTests | Decode_UInt32WithFlags_ReturnsDecodedFlags | 1, 2, 3 |
| BinaryDecoderTests | Decode_UInt8WithFlags_ReturnsDecodedFlags | 1, 2, 3 |
| BinaryDecoderTests | Decode_IntegerWithFlags_VariableBindingPreservesIntegerValue | 5 |
| BinaryDecoderTests | Decode_IntegerWithoutFlags_StillReturnsDecodedInteger | 回帰テスト |
| ElfParsingTests | ElfFormat_ProgramHeaders_DecodesCorrectly | 1, 2, 3, 4 |

### 気づき・今後の課題

- 64bit flags値の場合、`(uint)value` でtruncateされる。現時点では問題ないが、64bit幅のflags定義が必要になった場合は `DecodeFlagValues` のシグネチャを `ulong` に変更する必要がある。
