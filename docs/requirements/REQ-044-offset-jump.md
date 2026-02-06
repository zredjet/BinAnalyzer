# REQ-044: オフセットジャンプ（pointer/seek）

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 最高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のBinAnalyzerはバイナリデータをシーケンシャル（先頭から順に）に読み取る。しかし多くの実用的なバイナリフォーマットは、ヘッダ内のフィールド値をオフセットとして別の位置にジャンプしてデータを読む構造を持つ。

例:
- **PE**: DOS HeaderのE_lfanew（オフセット0x3C）がPEシグネチャの位置を指す
- **ELF**: e_shoff がセクションヘッダテーブルの位置を指す
- **ZIP**: End of Central Directoryのoffsetがセントラルディレクトリの位置を指す
- **TIFF**: IFDエントリのvalue offsetが実データの位置を指す
- **PDF**: startxrefがクロスリファレンステーブルの位置を指す

この機能はBinAnalyzerの解析能力を根本的に拡張する最重要機能。

## 機能要件

### 追加する機能

- [ ] DSLに `seek` プロパティを追加
  ```yaml
  - name: pe_header
    type: struct
    struct_ref: pe_header
    seek: "{e_lfanew}"         # 式で指定したオフセット位置にジャンプ
  ```
- [ ] `seek` の動作仕様
  - フィールド読み取り前に指定オフセットに位置を移動
  - フィールド読み取り後、元の位置には**戻らない**（デフォルト動作）
  - `seek_restore: true` を指定すると、読み取り後に元の位置に復帰
  ```yaml
  - name: string_value
    type: ascii
    size: "32"
    seek: "{string_offset}"
    seek_restore: true          # 読み取り後に元の位置に戻る
  ```
- [ ] `seek` に式を指定可能（定数、フィールド参照、算術式）
  ```yaml
  seek: "0x3C"                  # 定数オフセット
  seek: "{e_lfanew}"            # フィールド参照
  seek: "{header_offset + 16}"  # 算術式
  ```
- [ ] `seek` はファイル先頭からの絶対オフセット
- [ ] `seek_base` オプションで基準位置を指定可能（将来拡張）

### 変更する既存機能

- [ ] `FieldDefinition` — `SeekExpression`, `SeekRestore` プロパティ追加
- [ ] `BinaryDecoder` — フィールドデコード前のseek処理、seek_restore処理
- [ ] `DecodeContext` — Position のseek/restore操作
- [ ] `YamlFormatModel` — `seek`, `seek_restore` プロパティ追加
- [ ] `YamlToIrMapper` — seek式のパース
- [ ] `FormatValidator` — seek式の検証

### 変更しないもの（スコープ外）

- ファイル末尾からの相対オフセット（`seek_from_end`）— 将来課題
- 複数ファイルにまたがるseek
- seek先が現在のファイルサイズを超える場合の自動拡張

## 受入条件

1. [ ] `seek` プロパティで指定オフセットにジャンプしてフィールドを読み取れること
2. [ ] `seek` に定数、フィールド参照、算術式が指定できること
3. [ ] `seek_restore: true` で読み取り後に元の位置に復帰すること
4. [ ] `seek_restore` 未指定時はジャンプ先の位置から読み取りを継続すること
5. [ ] seekしたフィールドのOffsetがseek先のオフセットになること
6. [ ] 不正なオフセット（負の値、ファイルサイズ超過）で適切なエラーが発生すること
7. [ ] 既存のシーケンシャル読み取りに影響がないこと（seek未指定時は従来通り）
8. [ ] DSLリファレンスに seek/seek_restore の説明が追加されること
9. [ ] JSON Schemaに seek/seek_restore プロパティが追加されること
10. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | FieldDefinition に SeekExpression, SeekRestore 追加 |
| BinAnalyzer.Dsl | YamlFormatModel に seek/seek_restore 追加、YamlToIrMapper で式パース |
| BinAnalyzer.Engine | BinaryDecoder に seek/restore ロジック追加、DecodeContext に Seek メソッド追加 |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — seek/seek_restore の説明、使用例
- [ ] docs/architecture.md — DecodeContext の seek 機能の説明
- [ ] schemas/bdef.schema.json — seek, seek_restore プロパティ追加

---

## 設計メモ

### 設計方針

`seek` / `seek_restore` は、フィールドのデコード直前に読み取り位置を絶対オフセットへ移動する機能である。

- **`seek`（位置移動、復帰なし）**: フィールド読み取り前に指定オフセットへジャンプし、読み取り後はそのまま新しい位置から続行する。ヘッダのポインタ値に従って後続データを順次読む場合（PE の E_lfanew → PE Header → Optional Header → ... など）に使う。
- **`seek_restore`（位置移動、復帰あり）**: フィールド読み取り前に現在位置を保存し、指定オフセットへジャンプして読み取り後、保存した位置に復帰する。テーブル中のオフセットフィールドが指す先のデータを「覗き見」して元のシーケンシャル読みに戻る場合（TIFF の IFD エントリなど）に使う。

`seek` で指定するオフセットは**ファイル先頭（データ全体の先頭）からの絶対オフセット**である。スコープ相対ではない。これはバイナリフォーマットのポインタ値が通常ファイル先頭基準であるためである。

処理の挿入位置は `BinaryDecoder.DecodeField` メソッド内で、条件チェック (`if`) の後、`DecodeSingleField` / `DecodeRepeatedField` の呼び出し前に seek 処理を行う。seek_restore の場合は読み取り後に位置を復元する。

### モデル変更

#### FieldDefinition への追加プロパティ

```csharp
// FieldDefinition.cs に追加
/// <summary>フィールドデコード前にジャンプする絶対オフセットの式。</summary>
public Expression? SeekExpression { get; init; }

/// <summary>trueの場合、seekしたフィールドのデコード後に元の位置に復帰する。</summary>
public bool SeekRestore { get; init; }
```

#### YamlFormatModel への追加プロパティ

```csharp
// YamlFieldModel クラスに追加
[YamlMember(Alias = "seek")]
public string? Seek { get; set; }

[YamlMember(Alias = "seek_restore")]
public bool? SeekRestore { get; set; }
```

### DecodeContext の変更

`DecodeContext` に位置の直接移動（Seek）と、位置の保存/復元メソッドを追加する。Seek はスコープの境界チェックを**行わない**。seek はファイル先頭からの絶対オフセットであり、現在のバウンダリスコープの外を指すことが正当なユースケースであるためである。ただしデータ全体の範囲（0 以上、`_data.Length` 以下）は検証する。

```csharp
// DecodeContext.cs に追加

/// <summary>
/// 読み取り位置をファイル先頭からの絶対オフセットに移動する。
/// スコープ境界のチェックは行わない（seekはスコープ外を指しうるため）。
/// </summary>
public void Seek(int absoluteOffset)
{
    if (absoluteOffset < 0 || absoluteOffset > _data.Length)
        throw new InvalidOperationException(
            $"Seek offset {absoluteOffset} (0x{absoluteOffset:X}) is out of range: " +
            $"valid range is 0..{_data.Length} (0x{_data.Length:X})");
    _position = absoluteOffset;
}

/// <summary>現在の読み取り位置を保存する。</summary>
public int SavePosition() => _position;

/// <summary>保存された読み取り位置に復元する。</summary>
public void RestorePosition(int savedPosition)
{
    _position = savedPosition;
}
```

`SavePosition` / `RestorePosition` はスタック構造にせず、呼び出し側（BinaryDecoder）がローカル変数で管理する設計とする。seek_restore はフィールドレベルの機能であり、ネストすることがないため、スタックは不要。

### BinaryDecoder の変更

`DecodeField` メソッドで、条件チェック後かつデコード処理前に seek を挿入する。seek_restore の場合は try-finally で位置を復元する。

```csharp
// BinaryDecoder.DecodeField 内の変更箇所
// 既存の Condition チェックの後、Repeat チェックの前に挿入:

// --- seek処理 ---
int? savedPosition = null;
if (field.SeekExpression is not null)
{
    var seekOffset = (int)ExpressionEvaluator.EvaluateAsLong(
        field.SeekExpression, context);
    if (field.SeekRestore)
        savedPosition = context.SavePosition();
    context.Seek(seekOffset);
}

try
{
    if (field.Repeat is not RepeatMode.None)
        return DecodeRepeatedField(field, format, context);

    var node = DecodeSingleField(field, format, context, siblings);

    // フィールドレベルアライメント
    if (field.Align is { } align)
        context.AlignTo(align);

    // パディングフラグを伝搬
    if (field.IsPadding)
        return SetPaddingFlag(node);

    return node;
}
finally
{
    // seek_restore: 元の位置に復帰
    if (savedPosition is { } pos)
        context.RestorePosition(pos);
}
```

既存の try-catch-finally 構造に組み込む形で実装する。`finally` ブロックで `savedPosition` が設定されている場合のみ復元を行う。`DecodeField` 既存の `try { ... } catch (DecodeException) { throw; } catch (Exception ex) { ... } finally { _pathStack.Pop(); }` に seek_restore の finally 処理をマージする。

#### WithoutRepeat メソッドの更新

`WithoutRepeat` で FieldDefinition をコピーする際に、`SeekExpression` と `SeekRestore` も伝搬する必要がある。

```csharp
// WithoutRepeat メソッドに追加
SeekExpression = field.SeekExpression,
SeekRestore = field.SeekRestore,
```

### YamlToIrMapper の変更

`MapField` メソッドで `seek` 文字列を `ExpressionParser.Parse` で式としてパースし、`seek_restore` を bool として取得する。

```csharp
// MapField 内の return new FieldDefinition に追加
SeekExpression = yaml.Seek is not null ? ExpressionParser.Parse(yaml.Seek) : null,
SeekRestore = yaml.SeekRestore ?? false,
```

`seek` の値は既存の式パーサー (`ExpressionParser.Parse`) をそのまま利用する。定数（`"0x3C"`）、フィールド参照（`"{e_lfanew}"`）、算術式（`"{header_offset + 16}"`）のいずれも既存の式システムで対応可能。`ExpressionParser.Parse` は波括弧あり/なしの両方を受け付けるため、`"0x3C"` は `LiteralInt(0x3C)` に、`"{e_lfanew}"` は `FieldReference("e_lfanew")` にパースされる。

### FormatValidator の変更

新しいバリデーションルールを追加する。

```csharp
// FormatValidator に追加

/// <summary>VAL011: seek_restore が seek なしで指定されている</summary>
private static void ValidateSeek(
    FieldDefinition field, string structName,
    List<ValidationDiagnostic> diagnostics)
{
    // seek_restore が true だが seek がない場合はエラー
    if (field.SeekRestore && field.SeekExpression is null)
    {
        diagnostics.Add(Error("VAL011",
            $"フィールド '{field.Name}' に seek_restore が指定されていますが、seek が指定されていません",
            structName, field.Name));
    }
}
```

`Validate` メソッドの各フィールドループ内に `ValidateSeek(field, structName, format, diagnostics);` 呼び出しを追加する。

### JSON Schema 変更

`$defs/field/properties` に以下の 2 プロパティを追加する。

```json
"seek": {
  "type": "string",
  "description": "フィールドデコード前にジャンプする絶対オフセット。定数（\"0x3C\"）、式（\"{e_lfanew}\"）、算術式（\"{offset + 16}\"）を指定可能。"
},
"seek_restore": {
  "type": "boolean",
  "description": "true の場合、seekしたフィールドのデコード後に元の読み取り位置に復帰する。seek と併用する。",
  "default": false
}
```

### テスト計画

| テストファイル | テストケース | 対応する受入条件 |
|---|---|---|
| `BinAnalyzer.Engine.Tests/SeekTests.cs` | `Seek_JumpsToAbsoluteOffset` — 定数 seek で指定位置のデータを読めること | AC-1 |
| 同上 | `Seek_WithFieldReference` — フィールド参照式で seek できること | AC-2 |
| 同上 | `Seek_WithArithmeticExpression` — 算術式で seek できること | AC-2 |
| 同上 | `SeekRestore_ReturnsToOriginalPosition` — seek_restore で元の位置に復帰すること | AC-3 |
| 同上 | `Seek_WithoutRestore_ContinuesFromNewPosition` — seek_restore なしでジャンプ先から継続すること | AC-4 |
| 同上 | `Seek_DecodedNodeOffset_MatchesSeekTarget` — デコード結果の Offset が seek 先の位置であること | AC-5 |
| 同上 | `Seek_NegativeOffset_ThrowsError` — 負のオフセットでエラーが発生すること | AC-6 |
| 同上 | `Seek_BeyondDataLength_ThrowsError` — データサイズ超過でエラーが発生すること | AC-6 |
| 同上 | `NoSeek_SequentialReadUnchanged` — seek 未指定時は従来通り順次読み取りが行われること | AC-7 |
| 同上 | `Seek_WithRepeatField` — seek と repeat の組み合わせが動作すること | AC-1 |
| 同上 | `Seek_WithStructField` — seek で構造体をデコードできること | AC-1 |
| 同上 | `SeekRestore_WithStructField` — seek_restore で構造体をデコードし元の位置に戻ること | AC-3 |
| `BinAnalyzer.Dsl.Tests/SeekParsingTests.cs` | `ParseSeek_ConstantOffset` — YAML の seek 定数が正しくパースされること | AC-9 |
| 同上 | `ParseSeek_FieldReference` — YAML の seek フィールド参照がパースされること | AC-9 |
| 同上 | `ParseSeek_ArithmeticExpression` — YAML の seek 算術式がパースされること | AC-9 |
| 同上 | `ParseSeekRestore_True` — seek_restore: true がパースされること | AC-9 |
| 同上 | `ParseSeekRestore_Omitted_DefaultsFalse` — seek_restore 省略時は false になること | AC-9 |
| `BinAnalyzer.Core.Tests/Validation/SeekValidationTests.cs` | `SeekRestoreWithoutSeek_Error` — seek なしの seek_restore でバリデーションエラーになること | AC-6 |
| `BinAnalyzer.Integration.Tests/JsonSchemaTests.cs` | 既存テストに seek/seek_restore プロパティが JSON Schema に存在することの確認を追加 | AC-9 |

### 懸念事項

#### seek 先が範囲外の場合のエラー処理

`DecodeContext.Seek` で `absoluteOffset < 0` または `absoluteOffset > _data.Length` の場合に `InvalidOperationException` をスローする。これにより `BinaryDecoder.DecodeField` の既存 catch ブロックで `DecodeException` に変換され、エラー位置（フィールドパス、現在のオフセット）を含む情報を提供できる。`_data.Length` と等しい場合は許可する（EOF 位置への seek は、後続で 0 バイトフィールドを読む場合に正当）。

#### ネスト構造体内での seek とスコープの関係

`seek` は `_position` を直接操作するため、現在のバウンダリスコープの外へジャンプする可能性がある。これは意図的な設計である。バイナリフォーマットのポインタは通常ファイル先頭基準であり、ヘッダ構造体の内側からファイルの離れた位置を指すことは一般的なパターンである。

ただし、seek によって移動した位置でのデータ読み取りは、`EnsureAvailable` での `CurrentScope.End` チェックに引っかかる可能性がある。これを解決するため、seek 付きフィールドの場合は**一時的にルートスコープのコンテキストで読み取りを行う**必要がある。具体的には、seek 時に一時スコープ（seek 先から始まる適切なサイズのスコープ）を push し、読み取り後に pop する方法を検討する。

ただし初期実装では、seek は通常バウンダリスコープ内で使われない（ルートレベルまたは size 指定のない構造体で使う）ケースを主対象とする。バウンダリスコープ内での seek はスコープの End チェックによりエラーになりうるが、これはフォーマット定義の誤りとして扱う。将来的に `seek_scope: absolute` のようなオプションで制御する余地を残す。

#### seek_restore と位置追跡の整合性

`seek_restore` で復帰する位置は、seek 前の `_position` 値そのものである。seek 前にフィールドのデコードが完了している場合、復帰位置は seek 前の正しい連続読み取り位置になる。

注意点として、seek_restore を使った場合のデコード結果の `Size` 計算がある。通常 `DecodedStruct` の `Size` は `context.Position - startOffset` で計算されるが、seek_restore 後は `_position` が元に戻るため、seek 先で読んだフィールドの `Size` は seek 先のオフセットに基づいて計算される。親構造体の `Size` は seek_restore により元の位置に戻った後の `context.Position` を使うため、seek 先で読んだバイト数は親の `Size` に含まれない。これは意図した動作であり、seek_restore フィールドは「別の場所からデータを借りている」という意味を持つ。

`DecodedNode.Offset` は seek 先の実際のオフセットが記録されるため、ツリー出力でユーザーがどこからデータを読んだかを確認できる。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
