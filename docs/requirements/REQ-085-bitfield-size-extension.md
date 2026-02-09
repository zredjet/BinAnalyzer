# REQ-085: ビットフィールドサイズ拡張（1-8バイト対応）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在、bitfieldの `size` は1, 2, 4バイトのみ対応している。これにより以下のフォーマットでデコードエラーが発生し、`DecodeWithRecovery` でのリカバリが必要になっている。

- **WebP**: VP8 `frame_tag` が3バイトのbitfield（keyframe, version, show_frame, first_part_size）
- **FLAC**: STREAMINFOの主要フィールドが8バイトのbitfield（sample_rate 20bit, channels 3bit, bps 5bit, total_samples 36bit）

```yaml
# 現在エラーになるパターン:
- name: frame_tag
  type: bitfield
  size: 3              # ← サポート外でエラー
  fields:
    - name: keyframe
      bits: "0"

- name: stream_info_bits
  type: bitfield
  size: 8              # ← サポート外でエラー
  fields:
    - name: sample_rate
      bits: "44..63"
```

## 機能要件

### 追加する機能

- [x] bitfieldの `size` に1〜8の任意の整数を指定可能にする
- [x] 3, 5, 6, 7バイトのbitfieldが正しくデコードされること
- [x] 8バイトbitfieldでは64ビットの値範囲を扱えること

### 変更する既存機能

- [x] `BinaryDecoder`のbitfield処理: サイズ検証の条件変更（1,2,4 → 1〜8）
- [x] bitfield値の読み取り: 任意バイト長からの整数変換（現在のReadByte/ReadUInt16/ReadUInt32 → 汎用バイト列→ulong変換）
- [x] `FormatValidator`: bitfieldサイズの有効範囲検証を1〜8に変更

### 変更しないもの（スコープ外）

- 9バイト以上のbitfieldは対象外
- bitfieldのビットオーダー（LSBファースト）は変更しない
- bitfieldのエンディアン処理は変更しない

## 受入条件

1. [x] size: 3 のbitfieldが正しくデコードされること（WebP frame_tag）
2. [x] size: 8 のbitfieldが正しくデコードされること（FLAC STREAMINFO）
3. [x] size: 5, 6, 7 のbitfieldが正しくデコードされること
4. [x] 既存の size: 1, 2, 4 のbitfieldが引き続き正常動作すること（後方互換）
5. [x] WebPフォーマット: VP8 frame_tagのbitfield子フィールドが正しく抽出されること
6. [x] FLACフォーマット: STREAMINFOのsample_rate, channels, bps, total_samplesが正しく抽出されること
7. [x] FormatValidatorがsize: 1〜8を受け入れ、0やsize: 9以上をエラーとすること
8. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FormatValidator: bitfieldサイズ検証範囲変更（VAL013追加） |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | BinaryDecoder: 任意バイト長bitfield読み取りロジック（ReadBitfieldBytesヘルパー追加） |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — bitfieldのsize仕様を更新

### 変更が必要なフォーマット定義

なし（WebP/FLACは既にsize: 3/8で定義済み。DSL側の修正で動作するようになる）

### 変更が必要なテスト

- [x] WebpParsingTests: `Decode()` に変更、frame_tag bitfield子フィールドの値検証追加
- [x] FlacParsingTests: STREAMINFO bitfield子フィールドの値検証追加

---

## 設計メモ

BinaryDecoderのbitfield読み取りswitch文を拡張。1/2/4バイトは既存のReadUInt8/16/32を維持（パフォーマンス）、8バイトはReadUInt64を使用、3/5/6/7バイトはReadBytesで読み取りエンディアンに応じてlong値に変換するReadBitfieldBytesヘルパーを追加。FormatValidatorにVAL013（bitfieldサイズ1-8範囲チェック）を追加。

---

## 実装メモ

### 実装中の設計変更

- FlacTestDataGeneratorのSTREAMINFO bitfieldエンコーディングにバグを発見・修正（sample_rateが44100ではなく705600としてエンコードされていた）。以前はDecodeWithRecoveryでbitfield部分がスキップされていたため発覚していなかった

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | bitfield sizeスイッチに8バイト・3-7バイト分岐追加、ReadBitfieldBytesヘルパー追加 |
| `src/BinAnalyzer.Core/Validation/FormatValidator.cs` | VAL013: bitfieldサイズ1-8バリデーション追加 |
| `docs/dsl-reference.md` | bitfield説明に「1〜8バイト」を明記 |
| `tests/BinAnalyzer.Engine.Tests/BitfieldDecoderTests.cs` | 3/5/8バイト・無効サイズテスト追加 |
| `tests/BinAnalyzer.Core.Tests/Validation/FormatValidatorTests.cs` | VAL013テスト追加（有効1-8、無効0/9/16） |
| `tests/BinAnalyzer.Integration.Tests/WebpParsingTests.cs` | DecodeWithRecovery→Decode()移行、VP8 frame_tag bitfield値検証追加 |
| `tests/BinAnalyzer.Integration.Tests/FlacParsingTests.cs` | DecodeWithRecovery→Decode()移行、STREAMINFO bitfield値検証追加 |
| `tests/BinAnalyzer.Integration.Tests/FlacTestDataGenerator.cs` | STREAMINFO bitfieldエンコーディング修正 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| BitfieldDecoderTests | Decode_3ByteBitfield_ExtractsCorrectly | 1 |
| BitfieldDecoderTests | Decode_8ByteBitfield_ExtractsCorrectly | 2 |
| BitfieldDecoderTests | Decode_5ByteBitfield_ExtractsCorrectly | 3 |
| BitfieldDecoderTests | Decode_InvalidBitfieldSize_Throws | 7 |
| FormatValidatorTests | VAL013_BitfieldSizeValid_NoError (x8) | 7 |
| FormatValidatorTests | VAL013_BitfieldSizeInvalid_ReportsError (x3) | 7 |
| WebpParsingTests | WebpFormat_DecodesSuccessfully | 1, 5 |
| WebpParsingTests | WebpFormat_VP8FrameTag_BitfieldDecodesCorrectly | 1, 5 |
| FlacParsingTests | FlacFormat_DecodesSuccessfully | 2, 6 |
| FlacParsingTests | FlacFormat_StreamInfo_BitfieldDecodesCorrectly | 2, 6 |

### 気づき・今後の課題

- 既存のFlacTestDataGeneratorにbitfieldエンコーディングバグがあった。DecodeWithRecoveryモードでは検出されなかった問題が、正常デコードへの移行で発覚
- 9バイト以上のbitfieldが必要なフォーマットは現時点で確認されていないが、128ビットフィールド等が必要になった場合はBigInteger対応を検討
