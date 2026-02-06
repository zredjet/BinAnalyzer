# REQ-001: DSLバリデーター

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在、`.bdef.yaml` の定義ミスはバイナリデコード実行時（ランタイム）まで検出されない。例えば、存在しないstruct名をswitch caseで参照していても、YAMLの読み込み・IR変換は成功し、該当パスがデコードされた時点で初めて `InvalidOperationException` が発生する。

新しいフォーマット定義を追加する際のフィードバックサイクルを高速化するため、IR変換直後の静的解析（バリデーション）でこれらの問題を事前に検出する仕組みが必要。

現状のYamlToIrMapper（`src/BinAnalyzer.Dsl/YamlToIrMapper.cs`）で行われている検証は以下のみ:
- Root structの存在確認（行22-23）
- フィールド型文字列の妥当性（`ParseFieldType`）
- サイズ式の構文チェック（`ExpressionParser.Parse` による）

以下は**検証されておらず、ランタイムでしか検出されない**:
- struct参照（`StructRef`、switch cases、switch default）の存在確認
- enum参照（`EnumRef`）の存在確認
- flags参照（`FlagsRef`）の存在確認
- フィールド型と参照の組み合わせの妥当性
- 必須属性の欠落（struct型にStructRefがない等）
- 未使用定義の検出

## 機能要件

### 追加する機能

- [ ] `FormatValidator` クラスの新規作成（`BinAnalyzer.Core` に配置）
  - `FormatDefinition` を入力とし、`ValidationResult` を返す
  - エラー（定義が不正で動作不能）と警告（動作するが意図しない可能性）を区別する
- [ ] `ValidationResult` モデルの新規作成
  - エラー一覧（`IReadOnlyList<ValidationError>`）
  - 警告一覧（`IReadOnlyList<ValidationWarning>`）
  - `IsValid` プロパティ（エラーが0件の場合 true）
- [ ] `ValidationError` / `ValidationWarning` モデル
  - エラーコード（例: `VAL001`）
  - メッセージ（日本語）
  - 発生箇所（struct名 + フィールド名）
- [ ] CLIでのバリデーション実行
  - デコード前に自動でバリデーションを実行し、エラーがあれば処理を中断
  - 警告はstderrに出力し、処理は続行
  - `--no-validate` オプションでバリデーションをスキップ可能に

### 検証ルール一覧

#### エラー（動作不能）

| コード | 検証内容 | 現状 |
|---|---|---|
| VAL001 | struct型フィールドの `StructRef` が未指定 | ランタイム例外 |
| VAL002 | `StructRef` が存在しないstruct名を参照 | ランタイム例外 |
| VAL003 | switch caseの参照先structが未定義 | ランタイム例外 |
| VAL004 | switch defaultの参照先structが未定義 | ランタイム例外 |
| VAL005 | switch型フィールドに `switch_on` が未指定 | ランタイム例外 |
| VAL006 | switch型フィールドに `cases` も `default` もない | ランタイム例外 |
| VAL007 | サイズ指定が必要な型（bytes, ascii, utf8）でサイズ未指定 | ランタイム例外 |

#### 警告（意図しない動作の可能性）

| コード | 検証内容 | 現状 |
|---|---|---|
| VAL101 | `EnumRef` が存在しないenum名を参照 | 黙って無視 |
| VAL102 | `FlagsRef` が存在しないflags名を参照 | 黙って無視 |
| VAL103 | `EnumRef` が整数型以外のフィールドに指定されている | 黙って無視 |
| VAL104 | `FlagsRef` がascii型以外のフィールドに指定されている | 黙って無視 |
| VAL105 | switch型フィールドに `default` がない | マッチしない場合にランタイム例外 |
| VAL106 | struct型でないフィールドに `StructRef` が指定されている | 黙って無視 |
| VAL107 | 未使用のenum定義（どのフィールドからも参照されていない） | 検出されない |
| VAL108 | 未使用のflags定義（どのフィールドからも参照されていない） | 検出されない |
| VAL109 | 未使用のstruct定義（rootから到達不可能） | 検出されない |

### 変更する既存機能

- [ ] `Program.cs`（CLI）: デコード前にバリデーションを実行するフローの追加
- [ ] `Program.cs`（CLI）: `--no-validate` オプションの追加

### 変更しないもの（スコープ外）

- 式内のフィールド参照の存在確認（式が参照するフィールドが先行フィールドに存在するかの検証は、フィールド順序の解析が必要で複雑なため、将来の要望として分離）
- 循環参照の検出（structの相互参照による無限ループ検出は別要望とする）
- Expected値とサイズの整合性チェック（動的サイズの場合に静的検証不可）

## 受入条件

1. [ ] `FormatValidator.Validate(FormatDefinition)` が `ValidationResult` を返すこと
2. [ ] エラーVAL001-VAL007: 不正な定義に対してそれぞれエラーが報告されること
3. [ ] 警告VAL101-VAL109: 不正な定義に対してそれぞれ警告が報告されること
4. [ ] 正しい定義（`png.bdef.yaml`）に対してエラーが0件であること
5. [ ] `ValidationResult.IsValid` がエラー0件で `true`、エラーありで `false` を返すこと
6. [ ] エラーメッセージにstruct名・フィールド名が含まれること
7. [ ] CLIでバリデーションエラーがある場合、デコードを実行せず終了コード1で終了すること
8. [ ] CLIで警告がある場合、stderrに出力し、デコードは続行すること
9. [ ] `--no-validate` オプションでバリデーションをスキップできること
10. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `FormatValidator`, `ValidationResult`, `ValidationError`, `ValidationWarning` の新規追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | バリデーション実行フローと `--no-validate` オプションの追加 |
| BinAnalyzer.Core.Tests | バリデーターのユニットテスト追加 |
| BinAnalyzer.Integration.Tests | CLIレベルのバリデーション動作テスト追加（任意） |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — バリデーションルール一覧セクションの追加
- [ ] docs/architecture.md — バリデーターコンポーネントの追加
- [x] CLAUDE.md — 変更不要（アーキテクチャ概要に含まれない細粒度）
- [x] README.md — 変更不要（--no-validate は詳細使い方なので）

---

## 設計メモ

### 設計方針

バリデーターは `BinAnalyzer.Core` に配置する。理由:
- バリデーション対象の `FormatDefinition` が Core に存在する
- Engine や Dsl に依存しない純粋なモデル検証
- テストも Core.Tests 内で完結

単一の `FormatValidator` クラスに全検証ルールを集約する（個別バリデータークラスへの分割はしない）。理由:
- 検証ルールは16個（エラー7 + 警告9）と少数
- ルール間の依存がない（各ルールは独立して検証可能）
- 1クラスにまとめた方がルール一覧の見通しが良い
- 将来ルールが増えた場合、privateメソッドの抽出で対応可能

### モデル変更

#### 新規追加: `BinAnalyzer.Core/Validation/`

```
src/BinAnalyzer.Core/Validation/
├── FormatValidator.cs       # バリデーション実行
├── ValidationResult.cs      # 結果モデル
├── ValidationDiagnostic.cs  # エラー/警告の共通モデル
└── DiagnosticSeverity.cs    # Error / Warning
```

**ValidationDiagnostic**（エラーと警告の共通モデル）:
```csharp
public sealed record ValidationDiagnostic(
    DiagnosticSeverity Severity,   // Error / Warning
    string Code,                    // "VAL001" 等
    string Message,                 // 日本語メッセージ
    string? StructName,             // 発生箇所のstruct名
    string? FieldName               // 発生箇所のフィールド名
);
```

エラーと警告を別クラスにしない理由:
- 構造が同一（severity以外は全く同じ）
- LINQでの操作（フィルタリング、ソート）が容易
- 将来 Info レベルの追加も容易

**ValidationResult**:
```csharp
public sealed class ValidationResult
{
    public IReadOnlyList<ValidationDiagnostic> Diagnostics { get; }
    public bool IsValid => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public IEnumerable<ValidationDiagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
    public IEnumerable<ValidationDiagnostic> Warnings => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
}
```

**FormatValidator**:
```csharp
public static class FormatValidator
{
    public static ValidationResult Validate(FormatDefinition format);
}
```

staticクラスにする理由:
- 状態を持たない
- インスタンス生成の必要がない
- テストでのモック不要（入力→出力の純粋関数）

### インタフェース変更

- 新規インタフェースの追加はしない（FormatValidatorはstaticクラス）
- 既存インタフェース（IFormatLoader, IBinaryDecoder, IOutputFormatter）への変更なし
- CLIでの呼び出しは直接 `FormatValidator.Validate()` を使用

### 検証ルールの実装方針

全ルールを `FormatValidator` 内のprivateメソッドとして実装。`Validate()` から順番に呼び出し、結果を `List<ValidationDiagnostic>` に集約する。

```csharp
public static ValidationResult Validate(FormatDefinition format)
{
    var diagnostics = new List<ValidationDiagnostic>();

    // 全structの全フィールドを走査
    foreach (var (structName, structDef) in format.Structs)
    {
        foreach (var field in structDef.Fields)
        {
            ValidateFieldStructRef(field, structName, format, diagnostics);
            ValidateFieldEnumRef(field, structName, format, diagnostics);
            ValidateFieldFlagsRef(field, structName, format, diagnostics);
            ValidateFieldSizeSpec(field, structName, diagnostics);
            ValidateFieldSwitchSpec(field, structName, format, diagnostics);
            ValidateFieldTypeRefCombination(field, structName, diagnostics);
        }
    }

    // 未使用定義の検出
    ValidateUnusedEnums(format, diagnostics);
    ValidateUnusedFlags(format, diagnostics);
    ValidateUnreachableStructs(format, diagnostics);

    return new ValidationResult(diagnostics);
}
```

#### 未使用struct（VAL109）の検出アルゴリズム

rootStructから到達可能なstructを幅優先探索で収集し、全struct定義との差分を未使用として報告する。

```
到達可能struct = BFS(rootStruct)
  - StructRefのあるフィールド → 参照先structを探索対象に追加
  - SwitchCasesの各case → StructRefを探索対象に追加
  - SwitchDefault → 探索対象に追加
未使用struct = 全struct定義 - 到達可能struct
```

### CLI統合

`Program.cs` の変更箇所:

```csharp
// 既存フロー
var format = loader.Load(formatFile.FullName);
// ↓ ここにバリデーションを挿入

// 追加フロー
if (!noValidate)
{
    var result = FormatValidator.Validate(format);
    foreach (var warning in result.Warnings)
        Console.Error.WriteLine($"警告 [{warning.Code}]: {warning.Message}");
    if (!result.IsValid)
    {
        foreach (var error in result.Errors)
            Console.Error.WriteLine($"エラー [{error.Code}]: {error.Message}");
        return 1;
    }
}
```

`--no-validate` オプション:
```csharp
var noValidateOption = new Option<bool>("--no-validate")
{
    Description = "フォーマット定義のバリデーションをスキップする",
};
```

### 代替案

**案A: YamlToIrMapper内でバリデーションを行う（不採用）**
- 理由: YamlToIrMapperはDTO→IR変換に責務を限定すべき。バリデーションはIR完成後に行う方が検証ルールの追加・変更が容易。

**案B: IFormatValidatorインタフェースを定義する（不採用）**
- 理由: バリデーターは1つしか存在せず、DIの必要がない。テストも入力→出力の検証で十分。過剰な抽象化。

**案C: ValidationError / ValidationWarning を別クラスにする（不採用）**
- 理由: 構造が同一。DiagnosticSeverity enumで区別する方がシンプル。

### 懸念事項

1. **式内フィールド参照のスコープ検証はスコープ外**: 式 `{length}` がstruct内の先行フィールドに存在するかの検証は、フィールド定義の順序依存性があり複雑。REQ-001ではスコープ外とし、将来の要望で対応する。

2. **VAL109（未到達struct）の偽陽性**: switchのdefaultで使用されるstructはランタイムでのみ到達が決まるため、静的解析で「未到達」と判定される可能性がある。ただし、BFS探索ではSwitchDefault参照も辿るため、DSL上で定義されている参照は全て検出できる。実質的な偽陽性のリスクは低い。

3. **パフォーマンス**: struct数・フィールド数が少ない前提（数十〜数百）なので、BFS探索のコストは無視できる。

---

## 実装メモ

### 実装中の設計変更

設計通りに実装。設計変更なし。

### 追加したファイル

| ファイル | 役割 |
|---|---|
| `src/BinAnalyzer.Core/Validation/DiagnosticSeverity.cs` | Error / Warning enum |
| `src/BinAnalyzer.Core/Validation/ValidationDiagnostic.cs` | 診断結果レコード |
| `src/BinAnalyzer.Core/Validation/ValidationResult.cs` | バリデーション結果（IsValid, Errors, Warnings） |
| `src/BinAnalyzer.Core/Validation/FormatValidator.cs` | 全16ルールの検証ロジック |
| `tests/BinAnalyzer.Core.Tests/Validation/FormatValidatorTests.cs` | 27テスト |

### 変更したファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Cli/Program.cs` | バリデーション実行フロー追加、`--no-validate` オプション追加 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| FormatValidatorTests | VAL001_StructFieldWithoutStructRef_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL002_StructRefToUndefinedStruct_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL003_SwitchCaseRefToUndefinedStruct_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL004_SwitchDefaultRefToUndefinedStruct_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL005_SwitchFieldWithoutSwitchOn_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL006_SwitchFieldWithoutCasesAndDefault_ReportsError | 受入条件2 |
| FormatValidatorTests | VAL007_SizedTypeWithoutSize_ReportsError (x3) | 受入条件2 |
| FormatValidatorTests | VAL007_SizedTypeWithFixedSize_NoError | 受入条件2 |
| FormatValidatorTests | VAL007_SizedTypeWithSizeExpression_NoError | 受入条件2 |
| FormatValidatorTests | VAL007_SizedTypeWithSizeRemaining_NoError | 受入条件2 |
| FormatValidatorTests | VAL101_EnumRefToUndefinedEnum_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL102_FlagsRefToUndefinedFlags_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL103_EnumRefOnNonIntegerField_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL103_EnumRefOnIntegerField_NoWarning | 受入条件3 |
| FormatValidatorTests | VAL104_FlagsRefOnNonAsciiField_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL105_SwitchWithoutDefault_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL106_StructRefOnNonStructField_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL107_UnusedEnum_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL107_UsedEnum_NoWarning | 受入条件3 |
| FormatValidatorTests | VAL108_UnusedFlags_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL109_UnreachableStruct_ReportsWarning | 受入条件3 |
| FormatValidatorTests | VAL109_ReachableViaStructRef_NoWarning | 受入条件3 |
| FormatValidatorTests | VAL109_ReachableViaSwitchCase_NoWarning | 受入条件3 |
| FormatValidatorTests | ValidFormat_NoErrors | 受入条件4, 5 |
| FormatValidatorTests | ErrorMessages_ContainStructAndFieldNames | 受入条件6 |

### 受入条件の検証結果

1. [x] `FormatValidator.Validate(FormatDefinition)` が `ValidationResult` を返すこと — 実装済み
2. [x] エラーVAL001-VAL007: テスト通過
3. [x] 警告VAL101-VAL109: テスト通過
4. [x] 正しい定義（PNG相当）に対してエラーが0件 — `ValidFormat_NoErrors` テスト通過
5. [x] `ValidationResult.IsValid` — テスト通過
6. [x] エラーメッセージにstruct名・フィールド名が含まれる — `ErrorMessages_ContainStructAndFieldNames` テスト通過
7. [x] CLIでバリデーションエラー時にデコード不実行・終了コード1 — 実装済み（Program.cs）
8. [x] CLIで警告時にstderr出力・デコード続行 — 実装済み（Program.cs）
9. [x] `--no-validate` オプション — 実装済み（Program.cs）
10. [x] 既存テスト全通過 — 100テスト全通過（Core:52, Engine:29, Dsl:12, Integration:7）

### 気づき・今後の課題

- 式内フィールド参照の存在確認（`{length}` が先行フィールドに存在するか）は将来の要望候補
- 循環参照の検出（struct A → B → A）は将来の要望候補
