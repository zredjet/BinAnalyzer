# REQ-050: デコードエラー回復

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のBinAnalyzerはデコード中にエラーが発生すると、その時点で解析を中断し例外をスローする。しかし実用上、部分的な解析結果でも有用な場面が多い:

- **フォーマット定義が未完成**: 開発中の .bdef.yaml で途中まで正しく動作するか確認したい
- **壊れたファイル**: 破損したバイナリのどこまでが正常か調査したい
- **未知のバリエーション**: 想定外のswitch値やサイズに遭遇しても、他のフィールドは見たい
- **デバッグ**: エラー発生位置がフォーマット定義のバグの手がかりになる

エラー回復機能により、エラー発生箇所をマークしつつ解析を可能な限り継続することで、ツールの実用性が大幅に向上する。

## 機能要件

### 追加する機能

- [ ] ベストエフォートデコードモード
  - デコードエラー発生時、エラーを記録して可能な限り解析を継続
  - エラーが発生したフィールドは `DecodedError` ノードとしてツリーに含める
  - エラー後の位置回復: 構造体の既知サイズがあれば次のフィールドにスキップ
- [ ] `DecodedError` ノード（新規DecodedNodeサブタイプ）
  - エラーメッセージ、エラー発生オフセット、フィールドパス
  - ツリー表示でエラー箇所を赤色で明示
- [ ] CLIオプション `--on-error` の追加
  - `stop`（デフォルト）: 従来通りエラーで停止
  - `continue`: ベストエフォートモードで解析継続
- [ ] エラーサマリーの出力
  - 解析完了後、発生したエラーの一覧をstderrに出力
  - エラー数と影響を受けたフィールドのパスを表示

### 変更する既存機能

- [ ] `BinaryDecoder` — try-catch でフィールドデコードエラーを捕捉、DecodedError生成
- [ ] `DecodeContext` — エラーリスト管理
- [ ] 全出力フォーマッター — DecodedError ノードの表示対応
- [ ] `Program.cs` — `--on-error` オプション追加

### 変更しないもの（スコープ外）

- 自動修復（壊れたデータの推測・補完）
- フォーマット定義自体のエラー回復（バリデーションエラーは従来通り停止）
- ストリーム再同期（マーカーベースの再同期点検出）— 将来課題

## 受入条件

1. [ ] `--on-error continue` でエラー発生後も解析が継続されること
2. [ ] エラー箇所が `DecodedError` ノードとしてツリーに含まれること
3. [ ] エラーノードにエラーメッセージとオフセットが含まれること
4. [ ] tree出力でエラーノードが赤色で表示されること
5. [ ] json出力でエラーノードが `_type: "error"` として出力されること
6. [ ] 解析完了後にエラーサマリーがstderrに出力されること
7. [ ] `--on-error stop`（デフォルト）では従来通りエラーで停止すること
8. [ ] 既存テストが全て通過すること
9. [ ] エラー回復後の位置が妥当であること（無限ループしない）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | DecodedError ノード追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | BinaryDecoder にエラー回復ロジック追加、DecodeContext にエラーリスト |
| BinAnalyzer.Output | 全フォーマッターに DecodedError 表示追加 |
| BinAnalyzer.Cli | --on-error オプション追加 |

### 変更が必要なドキュメント

- [ ] docs/cli-usage.md — --on-error オプションの説明
- [ ] docs/architecture.md — DecodedError ノードの説明、エラー回復の仕組み
- [ ] README.md — 機能一覧にエラー回復を追記

---

## 設計メモ

### 1. DecodedError ノード（BinAnalyzer.Core）

`BinAnalyzer.Core/Decoded/DecodedError.cs` を新規作成する。

```csharp
namespace BinAnalyzer.Core.Decoded;

public sealed class DecodedError : DecodedNode
{
    /// <summary>エラーの詳細メッセージ。</summary>
    public required string ErrorMessage { get; init; }

    /// <summary>エラー発生時のフィールドパス（例: "header.data_size"）。</summary>
    public required string FieldPath { get; init; }
}
```

- `DecodedNode` の既存プロパティ（`Name`, `Offset`, `Size`）を活用する。`Size` はエラー発生フィールドの想定サイズ（不明なら0）。
- `Name` にはエラーが発生したフィールド名を設定し、ツリー上の位置を維持する。

### 2. ErrorMode enum と DecodeOptions（BinAnalyzer.Core）

`BinAnalyzer.Core/Models/ErrorMode.cs` を新規作成する。

```csharp
namespace BinAnalyzer.Core.Models;

public enum ErrorMode
{
    Stop,     // デフォルト: エラーで即停止（従来動作）
    Continue, // ベストエフォート: エラーを記録して継続
}
```

`BinaryDecoder.Decode()` のシグネチャにオプションを追加する方式を採用する。既存の `Decode(ReadOnlyMemory<byte>, FormatDefinition)` オーバーロードは `ErrorMode.Stop` のデフォルト値で後方互換を維持する。

```csharp
public DecodedStruct Decode(ReadOnlyMemory<byte> data, FormatDefinition format,
    ErrorMode errorMode = ErrorMode.Stop)
```

### 3. DecodeContext の変更（BinAnalyzer.Engine）

`DecodeContext` にエラー蓄積用リストを追加する。

```csharp
// DecodeContext に追加
public ErrorMode ErrorMode { get; }
public List<DecodeError> Errors { get; } = new();

public record DecodeError(string Message, long Offset, string FieldPath);
```

コンストラクタに `ErrorMode` パラメータを追加する。デフォルト値 `ErrorMode.Stop` で後方互換を維持する。

### 4. BinaryDecoder の変更（BinAnalyzer.Engine）

`DecodeField` メソッドの既存 try-catch を拡張する。現在は `DecodeException` を再スロー、その他の例外を `DecodeException` にラップして再スローしている。

**変更方針:**
- `ErrorMode.Stop` の場合: 従来通り例外を再スロー（動作変更なし）。
- `ErrorMode.Continue` の場合: 例外を捕捉し、`DecodedError` ノードを生成して返す。

```csharp
// DecodeField の catch ブロック変更イメージ
catch (Exception ex) when (context.ErrorMode == ErrorMode.Continue)
{
    var errorMessage = ex is DecodeException dex ? dex.Message : ex.Message;
    context.Errors.Add(new DecodeError(errorMessage, context.Position, CurrentPath));
    return new DecodedError
    {
        Name = field.Name,
        Offset = context.Position,
        Size = 0,
        ErrorMessage = errorMessage,
        FieldPath = CurrentPath,
    };
}
```

**位置回復戦略:**
- フィールドに固定サイズ (`field.Size`) が指定されている場合: エラー前の開始オフセット + サイズ にスキップする。`DecodeField` の冒頭で `var fieldStartOffset = context.Position;` を記録しておく。
- サイズ不明の場合: 位置を進めずにそのまま次のフィールドに進む。同じ位置で連続エラーが発生し無限ループになるリスクがあるため、`DecodeRepeatedField` では同一オフセットで2回連続エラーになった場合にループを打ち切る。
- 構造体境界でのエラー: 構造体の残りフィールドをすべてスキップし、親に制御を戻す。

### 5. 出力フォーマッターの変更（BinAnalyzer.Output）

各フォーマッターの switch/パターンマッチに `DecodedError` ケースを追加する。

**TreeOutputFormatter:**
```csharp
case DecodedError errorNode:
    sb.Append(prefix);
    sb.Append(C($"✗ {errorNode.Name}: ERROR: {errorNode.ErrorMessage}", AnsiColors.Red));
    sb.Append(C($" [0x{errorNode.Offset:X8}]", AnsiColors.Dim));
    sb.AppendLine();
    break;
```

**JsonOutputFormatter:**
```csharp
case DecodedError errorNode:
    writer.WriteStartObject();
    WriteCommonProperties(writer, errorNode, "error");
    writer.WriteString("name", errorNode.Name);
    writer.WriteString("error_message", errorNode.ErrorMessage);
    writer.WriteString("field_path", errorNode.FieldPath);
    writer.WriteEndObject();
    break;
```

**HtmlOutputFormatter:** error クラスの div として出力。赤色背景のスタイルを適用する。

**CsvOutputFormatter:** type 列に `"error"`、value 列にエラーメッセージを出力する。

**HexDumpOutputFormatter / MapOutputFormatter:** DecodedError はバイト範囲を持たないため、スキップまたは注釈として出力する。

### 6. CLI の変更（BinAnalyzer.Cli）

`Program.cs` に `--on-error` オプションを追加する。

```csharp
var onErrorOption = new Option<string>("--on-error")
{
    Description = "エラー発生時の動作 (stop, continue)",
    DefaultValueFactory = _ => "stop",
};
```

`ErrorMode.Continue` の場合、デコード完了後に `context.Errors` を stderr に出力する。`BinaryDecoder.Decode()` の戻り値を `(DecodedStruct result, IReadOnlyList<DecodeError> errors)` のタプルに変更するか、`DecodeResult` クラスを導入する。

```csharp
// エラーサマリーの出力
if (errors.Count > 0)
{
    Console.Error.WriteLine($"\n--- エラーサマリー ({errors.Count} 件) ---");
    foreach (var err in errors)
        Console.Error.WriteLine($"  {err.FieldPath} [0x{err.Offset:X8}]: {err.Message}");
}
```

### 7. 設計判断

- **DecodeResult の導入 vs タプル戻り値:** `DecodeResult` クラスを新規作成し、`DecodedStruct Root` と `IReadOnlyList<DecodeError> Errors` を保持する方式を推奨する。将来的にメタ情報（デコード時間、処理バイト数等）を追加しやすい。
- **エラーノードの子ノード:** `DecodedError` は子ノードを持たないリーフノードとする。構造体内でエラーが発生した場合、エラー前に成功したフィールドは通常通り `children` に含まれ、エラーフィールドが `DecodedError` として挿入される。
- **既存テストへの影響:** `ErrorMode.Stop` がデフォルトのため、既存の全テストは変更不要。新規テストで `ErrorMode.Continue` の動作を検証する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
