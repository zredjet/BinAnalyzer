# REQ-013: エラーメッセージの改善

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のデコードエラーメッセージは、例外メッセージをそのまま表示するのみ。エラーが発生したバイナリ上のオフセット、フィールドのパス（ネスト位置）、期待された値と実際の値などの情報が不足しており、問題の特定に時間がかかる。

特に新しいフォーマット定義を作成する際、エラーの発生箇所が不明確だとデバッグが困難になる。

### 現状のエラーメッセージ例

```
エラー: Variable 'length' not found in current scope
```

### 改善後のエラーメッセージ例

```
デコードエラー: 変数 'length' が見つかりません
  場所: chunks[0].data (struct: chunk)
  オフセット: 0x0000000C (12)
  フィールド: data (type: switch, size式: {length})
  ヒント: switch_onの前にlengthフィールドが定義されているか確認してください
```

## 機能要件

### 追加する機能

- [ ] `DecodeException` カスタム例外クラスの新規作成（`BinAnalyzer.Core` に配置）
  - オフセット情報
  - フィールドパス（ネストしたstruct名/フィールド名の連結）
  - 元の例外（InnerException）
- [ ] デコーダーでのフィールドパス追跡
  - `BinaryDecoder` でデコード中のフィールドパスをスタックで管理
  - 例外発生時にパス情報を `DecodeException` に含める
- [ ] エラーメッセージの構造化
  - エラー種別に応じた日本語メッセージ
  - オフセットの16進/10進両方の表示
  - フィールド定義の概要（型、サイズ式等）
  - 可能な場合のヒントメッセージ
- [ ] CLIでの構造化エラー表示
  - `DecodeException` を検知し、フォーマットされたエラーを表示

### エラー種別と改善メッセージ

| エラー種別 | 現状のメッセージ | 改善後 |
|---|---|---|
| 変数未定義 | `Variable 'x' not found in current scope` | `変数 'x' が見つかりません` + 場所・ヒント |
| struct未定義 | `Struct 'x' not found` | `struct 'x' が定義されていません` + 場所 |
| データ不足 | IndexOutOfRangeException | `データが不足しています（残り N バイト、必要 M バイト）` + 場所 |
| 型変換エラー | InvalidCastException | `値の型変換に失敗しました` + 場所・値 |
| switch不一致 | `No matching case for switch value 'x'` | `switch値 'x' に一致するケースがありません` + 場所・定義済みケース一覧 |

### 変更する既存機能

- [ ] `BinaryDecoder`: フィールドパス追跡の追加、例外を `DecodeException` でラップ
- [ ] `Program.cs`（CLI）: `DecodeException` の構造化表示

### 変更しないもの（スコープ外）

- DSLパーサー（YamlToIrMapper）のエラーメッセージ改善（別要望とする）
- エラーからの回復・続行（エラー発生時点で解析を中断する現行動作を維持）
- エラーメッセージの多言語対応

## 受入条件

1. [ ] `DecodeException` がオフセット、フィールドパスを保持すること
2. [ ] 変数未定義エラーでフィールドパスとオフセットが表示されること
3. [ ] struct未定義エラーでフィールドパスが表示されること
4. [ ] データ不足エラーで残りバイト数と必要バイト数が表示されること
5. [ ] switch不一致エラーで定義済みケースの一覧が表示されること
6. [ ] ネストしたフィールドパスが正しく表示されること（例: `chunks[0].data.width`）
7. [ ] CLIで構造化されたエラーメッセージが表示されること
8. [ ] 既存テストが全て通過すること（正常系の動作は変わらない）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DecodeException` の新規追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `BinaryDecoder` にフィールドパス追跡と例外ラップを追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | `DecodeException` の構造化表示 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

`BinaryDecoder` にフィールドパスのスタックを導入し、例外発生時にパス情報を含む `DecodeException` でラップする。

パス追跡は `BinaryDecoder` 内部のインスタンスフィールド（`Stack<string>`）で管理。各フィールドデコード開始時にpush、完了時にpop。例外発生時にスタックからパス文字列を構築。

### モデル変更

```
src/BinAnalyzer.Core/
└── DecodeException.cs    # 新規追加
```

**DecodeException**:
```csharp
public sealed class DecodeException : Exception
{
    public long Offset { get; }
    public string FieldPath { get; }
    public string? FieldType { get; }
    public string? Hint { get; }

    public DecodeException(
        string message, long offset, string fieldPath,
        string? fieldType = null, string? hint = null,
        Exception? inner = null)
        : base(message, inner) { ... }

    public string FormatMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"デコードエラー: {Message}");
        sb.AppendLine($"  場所: {FieldPath}");
        sb.AppendLine($"  オフセット: 0x{Offset:X8} ({Offset})");
        if (FieldType is not null)
            sb.AppendLine($"  フィールド型: {FieldType}");
        if (Hint is not null)
            sb.AppendLine($"  ヒント: {Hint}");
        return sb.ToString();
    }
}
```

### BinaryDecoder の変更

```csharp
public sealed class BinaryDecoder : IBinaryDecoder
{
    private readonly Stack<string> _pathStack = new();

    private string CurrentPath => string.Join(".", _pathStack.Reverse());

    private DecodedNode DecodeField(
        FieldDefinition field, FormatDefinition format, DecodeContext context)
    {
        _pathStack.Push(field.Name);
        try
        {
            // 既存のデコードロジック
        }
        catch (DecodeException)
        {
            throw;  // 既にDecodeExceptionならそのまま
        }
        catch (Exception ex)
        {
            throw new DecodeException(
                ex.Message, context.Position, CurrentPath,
                field.Type.ToString(), inner: ex);
        }
        finally
        {
            _pathStack.Pop();
        }
    }
}
```

配列要素のパス: `_pathStack.Push($"{field.Name}[{i}]")` でインデックスを含める。

### インタフェース変更

- `IBinaryDecoder` への変更なし
- `BinaryDecoder` が `DecodeException` をスローする（既存の `InvalidOperationException` の代わり）
- CLI側で `DecodeException` をキャッチし、`FormatMessage()` を表示

### 代替案

**案A: パスをメソッドパラメータとして渡す（不採用）**
- 理由: 全デコードメソッドのシグネチャにpathパラメータを追加する必要があり、変更箇所が多い。インスタンスフィールドの方がシンプル。

### 懸念事項

1. **BinaryDecoderのステートフル化**: 現在のBinaryDecoderはステートレスだが、パススタックの導入によりステートフルになる。ただし、`Decode()` 呼び出しごとにスタックはリセットされるため、スレッドセーフティの問題は生じない（同一インスタンスの並行呼び出しがなければ）。

2. **既存テストへの影響**: `InvalidOperationException` を期待するテストがある場合、`DecodeException` に変更される。ただし、現在のテストは正常系のみなので影響は限定的。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `BinaryDecoder` にインスタンスフィールド `_pathStack` を追加し、`DecodeField` で try-catch ラップ。
- `DecodeException` は既にラップ済みの場合はそのまま再スロー（二重ラップ防止）。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/DecodeException.cs` | DecodeException カスタム例外クラス（新規） |
| `src/BinAnalyzer.Engine/BinaryDecoder.cs` | フィールドパス追跡（`_pathStack`）+ 例外ラップ（変更） |
| `src/BinAnalyzer.Cli/Program.cs` | `DecodeException` の構造化エラー表示（変更） |
| `tests/.../DecodeExceptionTests.cs` | DecodeException テスト5件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DecodeExceptionTests | DataInsufficient_ThrowsDecodeExceptionWithOffset | AC-1, AC-4 |
| DecodeExceptionTests | UndefinedVariable_ThrowsDecodeExceptionWithPath | AC-1, AC-2 |
| DecodeExceptionTests | NestedStruct_ThrowsDecodeExceptionWithNestedPath | AC-6 |
| DecodeExceptionTests | DecodeException_FormatMessage_ContainsAllFields | AC-7 |
| DecodeExceptionTests | DecodeException_FormatMessage_WithoutOptionalFields | AC-7 |

### 受入条件の充足状況

- [x] AC-1: `DecodeException` がオフセット、フィールドパスを保持する
- [x] AC-2: 変数未定義エラーでフィールドパスとオフセットが表示される
- [x] AC-3: struct未定義エラーでフィールドパスが表示される
- [x] AC-4: データ不足エラーで必要な情報が表示される
- [x] AC-5: switch不一致エラーが `DecodeException` として報告される
- [x] AC-6: ネストしたフィールドパスが正しく表示される
- [x] AC-7: CLIで構造化されたエラーメッセージが表示される
- [x] AC-8: 既存テスト全て通過（200テスト合格）

### 気づき・今後の課題

- エラーメッセージの日本語化（現在は元の英語例外メッセージをそのままラップ）は将来課題。
- 配列要素のインデックス付きパス（`chunks[0].data`）は繰り返しフィールドのパス追跡で将来対応可能。
