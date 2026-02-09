# REQ-086: LEB128/VLQ 可変長整数型

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

多くのバイナリフォーマットが可変長整数エンコーディングを使用しているが、現在のDSLでは対応できず、固定長型で近似するか、bytes丸取りのワークアラウンドが必要になっている。

- **WASM**: LEB128（section_size, 各種インデックス） — 現在uint32で近似
- **7z**: 独自可変長エンコーディング（LEB128互換） — NextHeader内部構造がパース不可
- **MIDI**: VLQ（Variable Length Quantity、delta time） — trackデータをbytes丸取り

LEB128とVLQは類似のエンコーディングで、各バイトのMSB（最上位ビット）が継続フラグとして機能する。

- **LEB128**: リトルエンディアン。最初のバイトが最下位7ビット。DWARF, WASM, Protocol Buffers, Android DEXで使用。
- **VLQ**: ビッグエンディアン。最初のバイトが最上位7ビット。MIDI, Git packfileで使用。

```yaml
# 改善後の記述例:
# WASM section
- name: section_size
  type: uleb128           # unsigned LEB128

# MIDI track event
- name: delta_time
  type: vlq               # Variable Length Quantity
```

## 機能要件

### 追加する機能

- [x] 新しいフィールド型 `uleb128` — 符号なしLEB128可変長整数
- [x] 新しいフィールド型 `sleb128` — 符号付きLEB128可変長整数
- [x] 新しいフィールド型 `vlq` — Variable Length Quantity（ビッグエンディアン可変長整数）
- [x] 3型ともサイズ自動決定（`asciiz` と同様に `size` 指定不要）
- [x] 最大10バイト（64ビット値）に制限。超過時はデコードエラー
- [x] デコード結果は `DecodedInteger` として出力（既存の整数型と同じ出力形式）
- [x] `enum` 参照が可能なこと（通常の整数型と同様）
- [x] デコードされた値が式の変数として参照可能なこと

### 変更する既存機能

- [x] `FieldType` enum に `ULeb128`, `SLeb128`, `Vlq` を追加
- [x] `BinaryDecoder`: 可変長整数の読み取りロジック追加
- [x] `YamlFieldModel` → IR変換: 新しい型のマッピング追加
- [x] `FormatValidator`: 新しい型をIsIntegerTypeに追加（enum/string_table参照有効化）

### 変更しないもの（スコープ外）

- 7z独自エンコーディングの完全対応（LEB128互換部分のみ対応）
- MIDI VLQを使ったイベント構造の完全パース（delta_timeの読み取りのみ）
- Protocol Buffersのvarint（LEB128と同一なので uleb128 で対応可能）

## 受入条件

1. [x] `uleb128` 型: 1バイト値（0x00〜0x7F）が正しくデコードされること
2. [x] `uleb128` 型: 複数バイト値（例: 0x80 0x01 = 128）が正しくデコードされること
3. [x] `sleb128` 型: 正の値が正しくデコードされること
4. [x] `sleb128` 型: 負の値（例: 0x7F = -1）が正しくデコードされること
5. [x] `vlq` 型: 1バイト値が正しくデコードされること
6. [x] `vlq` 型: 複数バイト値（例: 0x81 0x00 = 128）が正しくデコードされること
7. [x] 10バイト超の可変長整数でデコードエラーが発生すること
8. [x] WASMフォーマット: section_sizeが `uleb128` で正確にデコードされること
9. N/A MIDIフォーマット: スコープ外（REQ-065に委譲）
10. [x] enum参照が正常に動作すること
11. [x] デコード結果が式内で変数として参照可能なこと
12. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldType enum追加、FormatValidator: 新型の検証ルール |
| BinAnalyzer.Dsl | YamlFieldModel→IR変換: 新型マッピング |
| BinAnalyzer.Engine | BinaryDecoder: LEB128/VLQ読み取りロジック |
| BinAnalyzer.Output | 変更なし（DecodedIntegerとして出力） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 新しいフィールド型の仕様追加

### 変更が必要なフォーマット定義

- [x] `formats/wasm.bdef.yaml` — section_sizeを `uleb128` に変更
- [ ] `formats/midi.bdef.yaml` — スコープ外（REQ-065に委譲）
- [ ] `formats/7z.bdef.yaml` — スコープ外

### 変更が必要なテスト

- [x] WasmParsingTests: section_sizeの値検証強化
- [ ] MidiParsingTests: スコープ外（REQ-065に委譲）
- [x] 単体テスト: LEB128/VLQの各種エッジケース

---

## 設計メモ

- asciiz/utf8z と同パターンの「サイズ自動決定型」として実装。ValidateSizeSpecのサイズ必須型リストに含めないことで、size未指定を許容。
- DecodeContext にReadULeb128/ReadSLeb128/ReadVlq を追加し、BinaryDecoder には DecodeVariableLengthIntegerField を新設。DecodeIntegerField とは分離（チェックサム検証等を省略するため）。
- FormatValidator の IsIntegerType に3型を追加し、enum参照・string_table参照を有効化。

---

## 実装メモ

### 実装中の設計変更

- MIDIフォーマット・7zフォーマットの変更はスコープ外とした（計画通り）。
- CS0675警告回避のため、ReadVlq内の `(ulong)(b & 0x7F)` を `(uint)(b & 0x7F)` に変更。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| VariableLengthIntegerTests | ULeb128_SingleByte_Zero | AC1 |
| VariableLengthIntegerTests | ULeb128_SingleByte_127 | AC1 |
| VariableLengthIntegerTests | ULeb128_TwoBytes_128 | AC2 |
| VariableLengthIntegerTests | ULeb128_MultiBytes_624485 | AC2 |
| VariableLengthIntegerTests | ULeb128_ExceedsMaxBytes_ThrowsException | AC7 |
| VariableLengthIntegerTests | SLeb128_PositiveValue_2 | AC3 |
| VariableLengthIntegerTests | SLeb128_NegativeValue_Minus1 | AC4 |
| VariableLengthIntegerTests | SLeb128_NegativeValue_Minus127 | AC4 |
| VariableLengthIntegerTests | SLeb128_ExceedsMaxBytes_ThrowsException | AC7 |
| VariableLengthIntegerTests | Vlq_SingleByte_Zero | AC5 |
| VariableLengthIntegerTests | Vlq_SingleByte_127 | AC5 |
| VariableLengthIntegerTests | Vlq_TwoBytes_128 | AC6 |
| VariableLengthIntegerTests | Vlq_MultiBytes_480 | AC6 |
| VariableLengthIntegerTests | Vlq_ExceedsMaxBytes_ThrowsException | AC7 |
| VariableLengthIntegerTests | ULeb128_EnumRef_ResolvesLabel | AC10 |
| VariableLengthIntegerTests | ULeb128_VariableBinding_UsableInExpression | AC11 |
| VariableLengthIntegerTests | ULeb128_FollowedByMoreFields_PositionCorrect | - |
| WasmParsingTests | WasmFormat_SectionSize_DecodesAsULeb128 | AC8 |
| FormatValidatorTests | VAL007_VariableLengthIntegerWithoutSize_NoError | - |
| FormatValidatorTests | VAL103_EnumRefOnULeb128Field_NoWarning | AC10 |

### 気づき・今後の課題

- MIDI delta_timeへのVLQ適用はREQ-065で対応予定。
