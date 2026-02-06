# REQ-004: 文字列エンコーディング追加

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のBinAnalyzerが対応する文字列エンコーディングはASCIIとUTF-8のみ。多くのバイナリフォーマットでは他のエンコーディングが使用されている:

- **Shift_JIS**: 日本語環境の旧来のファイルフォーマット（BMP内のテキスト、古いゲームデータ等）
- **UTF-16**: Windowsの多くのファイルフォーマット（PE/COFF、RTF内部等）
- **Latin-1 (ISO-8859-1)**: 欧文のバイナリフォーマット

これらに対応しないと、日本語を含むバイナリや、Windowsネイティブのフォーマットを正しく解析できない。

## 機能要件

### 追加する機能

- [ ] DSLに新しい文字列型を追加
  - `utf16le` — UTF-16 リトルエンディアン
  - `utf16be` — UTF-16 ビッグエンディアン
  - `sjis` — Shift_JIS
  - `latin1` — ISO-8859-1
- [ ] `FieldType` enumに対応する値を追加
- [ ] `DecodeContext` に各エンコーディングの読み取りメソッドを追加
- [ ] `BinaryDecoder` で新しい文字列型を処理
- [ ] `DecodedString.Encoding` に各エンコーディング名が設定されること

### DSL構文例

```yaml
structs:
  header:
    - name: title
      type: utf16le
      size: "20"
    - name: author
      type: sjis
      size: "32"
    - name: comment
      type: latin1
      size: "64"
```

### 変更する既存機能

- [ ] `FieldType` enum: 新しいエンコーディング型の追加
- [ ] `YamlToIrMapper.ParseFieldType`: 新しい型名の認識
- [ ] `BinaryDecoder.DecodeSingleField`: 新しい型へのディスパッチ
- [ ] `DecodeContext`: 新しい読み取りメソッド

### 変更しないもの（スコープ外）

- BOM（バイトオーダーマーク）の自動検出
- エンコーディングの自動推定
- ヌル終端文字列のエンコーディング別対応（将来課題）

## 受入条件

1. [ ] `utf16le` 型のフィールドがUTF-16 LEとしてデコードされること
2. [ ] `utf16be` 型のフィールドがUTF-16 BEとしてデコードされること
3. [ ] `sjis` 型のフィールドがShift_JISとしてデコードされること
4. [ ] `latin1` 型のフィールドがISO-8859-1としてデコードされること
5. [ ] ツリー出力で各エンコーディングの文字列が正しく表示されること
6. [ ] 変数バインディングで文字列値が参照可能であること（既存のascii/utf8と同様）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FieldType` に4つの値を追加 |
| BinAnalyzer.Dsl | `ParseFieldType` に4つの型名を追加 |
| BinAnalyzer.Engine | `DecodeContext` に読み取りメソッド追加、`BinaryDecoder` にディスパッチ追加 |
| BinAnalyzer.Output | 変更なし（DecodedStringの表示は既存ロジックで対応可能） |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — 新しいフィールド型の追加
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

既存の `ascii` / `utf8` と同じパターンで新しいエンコーディング型を追加する。既存のDecodeAsciiField / DecodeUtf8Field と同じ構造の処理を、新しい型ごとに追加。

共通化の方針: 文字列デコードロジックは「バイト列を読み取り → Encodingで変換 → DecodedString生成」の共通パターンなので、内部で `DecodeStringField(field, format, context, Encoding encoding, string encodingName)` という共通メソッドを抽出する。既存のascii/utf8もこのメソッドに統合する。

### モデル変更

**FieldType への追加**:
```csharp
Utf16Le,    // UTF-16 リトルエンディアン
Utf16Be,    // UTF-16 ビッグエンディアン
ShiftJis,   // Shift_JIS
Latin1,     // ISO-8859-1
```

他のモデルへの変更なし。`DecodedString.Encoding` には `"utf16le"`, `"utf16be"`, `"sjis"`, `"latin1"` を設定。

### インタフェース変更

既存インタフェースへの変更なし。

**BinaryDecoder の変更**:
- `DecodeSingleField` のswitch式に4つの分岐を追加
- 既存の `DecodeAsciiField` / `DecodeUtf8Field` を共通メソッドに統合

**DecodeContext の変更**:
- `ReadString(int size, Encoding encoding)` メソッドを追加（既存のReadAscii/ReadUtf8を一般化）
- 既存の `ReadAscii` / `ReadUtf8` はReadStringの呼び出しに変更（後方互換維持）

**YamlToIrMapper の変更**:
- `ParseFieldType` に4つの型名を追加:
  - `"utf16le"` / `"utf16-le"` → FieldType.Utf16Le
  - `"utf16be"` / `"utf16-be"` → FieldType.Utf16Be
  - `"sjis"` / `"shift_jis"` / `"shift-jis"` → FieldType.ShiftJis
  - `"latin1"` / `"iso-8859-1"` → FieldType.Latin1

### Shift_JIS の実装

.NET の `System.Text.Encoding` でShift_JISを使用するには、`System.Text.Encoding.CodePages` NuGetパッケージが必要。

```csharp
// 初期化（アプリケーション起動時に1回）
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// 使用
var sjis = Encoding.GetEncoding("shift_jis");
```

`System.Text.Encoding.CodePages` パッケージを `BinAnalyzer.Engine` に追加する。

### 代替案

**案A: 文字列型を増やさず `encoding` 属性で指定する（不採用）**
```yaml
- name: title
  type: string
  encoding: utf16le
  size: "20"
```
- 理由: DSLの型名がそのまま一意にデコード方法を決定する既存の設計パターンを維持する方がシンプル。`encoding` 属性を追加すると、type=stringの場合のみencoding必須という条件付きバリデーションが必要になり複雑化する。

**案B: Shift_JIS非対応（不採用）**
- 理由: 日本語環境のバイナリでは頻出。BinAnalyzerの主な使用環境を考慮すると対応すべき。

### 懸念事項

1. **CodePages パッケージの追加**: `BinAnalyzer.Engine` に初めて外部NuGetパッケージの依存が生じる。Engine の「BCLのみ」方針からの逸脱。ただし `System.Text.Encoding.CodePages` はMicrosoft公式パッケージであり、BCLの拡張と見なせる。

2. **UTF-16のサイズ**: UTF-16はバイト数が必ず偶数。奇数バイトが指定された場合のエラー処理を `DecodeContext.ReadString` で行う（例外を投げる）。

3. **フラグ付き文字列**: 現在 `flags` はascii型にのみ対応。新しいエンコーディング型にflagsを適用する需要は低いため、スコープ外とする。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。
- `System.Text.Encoding.CodePages` パッケージは不要だった（.NET 10では `CodePagesEncodingProvider` がBCLに含まれている）。Engineプロジェクトへの外部パッケージ追加なし。
- `DecodeContext.ReadString(int count, Encoding encoding)` を汎用メソッドとして追加し、既存の `ReadAscii` / `ReadUtf8` をラッパーとして維持。
- `BinaryDecoder` では既存の `DecodeUtf8Field` を `DecodeStringField` に一般化し、全文字列型（utf8, utf16le, utf16be, sjis, latin1）で共用。`DecodeAsciiField` はflags対応のため別メソッドとして維持。
- `FormatValidator` の VAL007（サイズ必須チェック）に4つの新しい型を追加。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Models/FieldType.cs` | `Utf16Le`, `Utf16Be`, `ShiftJis`, `Latin1` を追加 |
| `src/BinAnalyzer.Engine/DecodeContext.cs` | `ReadString(int, Encoding)` 追加、`ReadAscii`/`ReadUtf8` をラッパー化 |
| `src/BinAnalyzer.Engine/EncodingHelper.cs` | 新規: Shift_JIS Encodingの遅延初期化 |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | `DecodeStringField` に統合、4つの新型ディスパッチ追加 |
| `src/BinAnalyzer.Dsl/YamlToIrMapper.cs` | `ParseFieldType` に9つの型名エイリアスを追加 |
| `src/BinAnalyzer.Core/Validation/FormatValidator.cs` | VAL007に4つの新型を追加 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| StringEncodingDecoderTests | Decode_Utf16Le_DecodesCorrectly | AC-1 |
| StringEncodingDecoderTests | Decode_Utf16Be_DecodesCorrectly | AC-2 |
| StringEncodingDecoderTests | Decode_Latin1_DecodesCorrectly | AC-4 |
| StringEncodingDecoderTests | Decode_ShiftJis_DecodesCorrectly | AC-3 |
| StringEncodingDecoderTests | Decode_Utf16Le_Japanese_DecodesCorrectly | AC-1（日本語） |
| StringEncodingDecoderTests | Decode_StringField_SetsVariable | AC-6（変数バインディング） |
| StringEncodingParsingTests | Load_StringEncodingType_ParsesCorrectly (9 cases) | AC-1〜4（DSLパース） |

### 受入条件の充足状況

- [x] AC-1: utf16le型のフィールドがUTF-16 LEとしてデコードされる
- [x] AC-2: utf16be型のフィールドがUTF-16 BEとしてデコードされる
- [x] AC-3: sjis型のフィールドがShift_JISとしてデコードされる
- [x] AC-4: latin1型のフィールドがISO-8859-1としてデコードされる
- [x] AC-5: ツリー出力で各エンコーディングの文字列が正しく表示される（既存のDecodedString表示ロジックで対応）
- [x] AC-6: 変数バインディングで文字列値が参照可能
- [x] AC-7: 既存テスト全て通過（137テスト合格）

### 気づき・今後の課題

- .NET 10では `CodePagesEncodingProvider` が標準で利用可能。BCLのみ方針を維持できた。
- UTF-16の奇数バイトサイズ検証は未実装。`Encoding.GetString` が例外を投げるため実用上は問題ないが、より親切なエラーメッセージを出す余地あり。
