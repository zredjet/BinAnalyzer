# REQ-151: カスタムチェックサムアルゴリズム追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-21 |
| 更新日 | 2026-02-21 |

## 背景・動機

現在サポートされているチェックサムアルゴリズムは以下の7種類である。

- **整数チェックサム**: CRC-32, CRC-16/CCITT, CRC-16/IBM, Adler-32
- **ハッシュ**: MD5, SHA-1, SHA-256

一部のバイナリフォーマットでは、上記以外のアルゴリズムが使われている。

| アルゴリズム | 使用フォーマット |
|---|---|
| CRC-8 | SMBus, 1-Wire, USB token packets |
| CRC-64/ECMA | XZ |
| xxHash (XXH32/XXH64) | LZ4 frame, Zstandard |
| FNV-1/FNV-1a | DNS, various hash tables |
| Fletcher-16/32 | TCP, Adler-32の派生 |
| SHA-512 | セキュリティ関連フォーマット |

## 機能要件

### 追加する機能

- [x] CRC-8（多項式指定可能）
- [x] CRC-64/ECMA-182
- [x] xxHash32 / xxHash64
- [x] SHA-384 / SHA-512
- [x] Fletcher-16 / Fletcher-32

### 変更する既存機能

- [x] `BinaryDecoder` のチェックサムアルゴリズムディスパッチ — 新アルゴリズムの追加

### 変更しないもの（スコープ外）

- 既存のチェックサムアルゴリズムの動作 — 変更なし
- CRC多項式のユーザー定義 — CRC-8のみパラメトリック対応、他は固定多項式
- HMAC — スコープ外

## 受入条件

1. [x] 各新規アルゴリズムでチェックサム検証が正常に動作すること
2. [x] 既知のテストベクター（RFC等）に対して正しい結果を返すこと
3. [x] DSLの `checksum.algorithm` に新アルゴリズム名を指定できること
4. [x] 未知のアルゴリズム名に対してバリデーションエラーが発生すること
5. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `ChecksumAlgorithm` enum — 新値追加 |
| BinAnalyzer.Dsl | 変更なし（アルゴリズム名の文字列マッピングは既存の仕組みで対応） |
| BinAnalyzer.Engine | チェックサム計算クラスの追加（Crc8Calculator, Crc64Calculator, XxHashCalculator, FletcherCalculator）。SHA-384/512はBCLの`System.Security.Cryptography`で対応 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — チェックサムアルゴリズム一覧の更新
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

- CRC-8: 3バリアント（SMBus, Maxim, CDMA2000）を名前付きで実装。既存の crc16-ccitt / crc16-ibm と同パターン
- CRC-64/ECMA-182: 非反転多項式 0x42F0E1EBA9EA3693、MSB-first ルックアップテーブル方式
- xxHash: NuGet `System.IO.Hashing` を Engine に追加。XxHash32.Hash() / XxHash64.Hash() のラッパー
- SHA-384/512: BCL の System.Security.Cryptography で対応
- Fletcher-16/32: 自前実装。Fletcher-32 は 16-bit ワード入力（ビッグエンディアン）、奇数バイト長はゼロパディング

### モデル変更

- `ChecksumAlgorithms`（Core）: 10個の定数追加、IntegerAlgorithms / HashAlgorithms HashSet に登録、DisplayName 追加

### インタフェース変更

なし。DSL モデルはアルゴリズム名を文字列保持のため変更不要。

### 代替案

- xxHash を自前実装: バグリスクが高いため却下。Microsoft 公式 NuGet を採用
- CRC-64 に反転形式（CRC-64/XZ）を使用: ECMA-182 仕様に合わせ非反転形式を採用

### 懸念事項

- CRC-64 / xxHash64 は ulong を返すが DecodedInteger.Value は long。(long)ulong_value でビットパターン保持キャスト
- Engine プロジェクトに初めての NuGet 依存（System.IO.Hashing）追加

---

## 実装メモ

### 実装中の設計変更

- CRC-64/ECMA-182: 当初反転多項式（CRC-64/XZ）で実装したが、テストベクターと不一致。非反転 MSB-first に修正

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| Crc8CalculatorTests | ComputeSmbus_StandardVector_ReturnsExpected | AC2 |
| Crc8CalculatorTests | ComputeMaxim_StandardVector_ReturnsExpected | AC2 |
| Crc8CalculatorTests | ComputeCdma2000_StandardVector_ReturnsExpected | AC2 |
| Crc64CalculatorTests | ComputeEcma_StandardVector_ReturnsExpected | AC2 |
| XxHashCalculatorTests | ComputeXxHash32_StandardVector_MatchesBclImplementation | AC2 |
| XxHashCalculatorTests | ComputeXxHash64_StandardVector_MatchesBclImplementation | AC2 |
| FletcherCalculatorTests | ComputeFletcher16_Abcde_ReturnsExpected | AC2 |
| ChecksumDecoderTests | Decode_Crc8Smbus_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Crc8Maxim_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Crc8Cdma2000_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Crc64Ecma_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_XxHash32_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_XxHash64_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Fletcher16_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Fletcher32_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Sha384_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Sha512_ValidChecksum | AC1 |
| ChecksumDecoderTests | Decode_Sha512_InvalidChecksum_ReturnsExpectedHex | AC1 |
| ChecksumParsingTests | Load_NewAlgorithmNames_ParsesCorrectly (10 new InlineData) | AC3 |

### 気づき・今後の課題

- FNV-1/FNV-1a は本 REQ のスコープから除外。必要に応じて別 REQ で追加
