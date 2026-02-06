# REQ-012: カラー出力

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

現在のツリー出力はモノクロテキストのみ。フィールド型（整数、文字列、バイト列）やバリデーション結果（✓/✗）、enum値などを色分けすることで、出力の可読性が大幅に向上する。

ターミナルのカラー出力はANSIエスケープシーケンスで実現可能だが、パイプやリダイレクト先がファイルの場合は無効にする必要がある。将来的にはSpectre.Consoleの導入も選択肢。

## 機能要件

### 追加する機能

- [ ] ツリー出力のカラー対応
  - フィールド名: デフォルト色（白/通常）
  - 整数値: シアン
  - 文字列値: 緑
  - バイト列（hex表示）: 黄色
  - enum値ラベル: マゼンタ
  - バリデーション ✓: 緑
  - バリデーション ✗: 赤
  - オフセット/サイズ: 灰色（dim）
  - 罫線文字（├── └── │）: 灰色（dim）
- [ ] カラー出力の自動判定
  - 標準出力がターミナルの場合: カラー有効
  - パイプ/ファイルリダイレクトの場合: カラー無効
- [ ] CLI オプション
  - `--color auto`（デフォルト: 自動判定）
  - `--color always`（常にカラー）
  - `--color never`（常にモノクロ）

### 変更する既存機能

- [ ] `TreeOutputFormatter`: カラーコード埋め込みロジックの追加
- [ ] `Program.cs`（CLI）: `--color` オプションの追加

### 変更しないもの（スコープ外）

- Spectre.Consoleの導入（ANSIエスケープシーケンスで直接実装）
- テーマのカスタマイズ（色設定ファイル等）
- JSON出力やhex dump出力のカラー対応（将来課題）

## 受入条件

1. [ ] `--color always` でANSIカラーコードを含む出力が生成されること
2. [ ] `--color never` でカラーコードを含まない出力が生成されること（現行動作と同一）
3. [ ] `--color auto`（デフォルト）でターミナル判定が行われること
4. [ ] 整数値、文字列値、バイト列、enum値がそれぞれ異なる色で表示されること
5. [ ] バリデーション ✓ が緑、✗ が赤で表示されること
6. [ ] パイプ先（`| cat` 等）ではカラーコードが出力されないこと（auto時）
7. [ ] 既存テストが全て通過すること（テストはカラーコードを含まない出力を期待）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | `TreeOutputFormatter` のカラー対応、ANSIカラーヘルパー追加 |
| BinAnalyzer.Cli | `--color` オプション追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要（CLIヘルプで十分）

---

## 設計メモ

### 設計方針

ANSIエスケープシーケンスを直接使用する。Spectre.Console等の外部ライブラリは導入しない。

`TreeOutputFormatter` にカラーモード（`ColorMode` enum: Auto/Always/Never）を受け取るコンストラクタパラメータを追加。内部でカラーコードの埋め込み/スキップを制御する。

### モデル変更

```
src/BinAnalyzer.Output/
├── AnsiColors.cs               # 新規: ANSIエスケープコード定数
├── TreeOutputFormatter.cs      # 変更: カラー対応
└── ColorMode.cs                # 新規: Auto/Always/Never enum
```

**AnsiColors**（静的クラス）:
```csharp
internal static class AnsiColors
{
    public const string Reset = "\x1b[0m";
    public const string Dim = "\x1b[2m";
    public const string Cyan = "\x1b[36m";
    public const string Green = "\x1b[32m";
    public const string Yellow = "\x1b[33m";
    public const string Magenta = "\x1b[35m";
    public const string Red = "\x1b[31m";
}
```

**TreeOutputFormatter の変更**:
```csharp
public sealed class TreeOutputFormatter : IOutputFormatter
{
    private readonly bool _useColor;

    public TreeOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            ColorMode.Auto => Console.IsOutputRedirected == false,
            _ => false,
        };
    }
}
```

カラーコード挿入はヘルパーメソッドで抽象化:
```csharp
private string Colorize(string text, string color)
    => _useColor ? $"{color}{text}{AnsiColors.Reset}" : text;
```

### インタフェース変更

- `IOutputFormatter` への変更なし
- `TreeOutputFormatter` のコンストラクタにオプションパラメータ追加（デフォルト `ColorMode.Never` で後方互換）
- 既存テストは `ColorMode.Never`（デフォルト）で動作するため影響なし

### 代替案

**案A: Spectre.Console を導入する（不採用）**
- 理由: カラー出力のみであればANSIエスケープで十分。外部依存の追加は最小限に。将来リッチなUI（テーブル、プログレスバー等）が必要になった場合に導入を検討。

### 懸念事項

1. **Windowsターミナルの互換性**: Windows 10 以降はANSIエスケープをサポートするが、古い cmd.exe では表示が崩れる。`Console.IsOutputRedirected` による自動判定で大半のケースは対応可能。

2. **既存テストへの影響**: コンストラクタのデフォルト値が `ColorMode.Never` であるため、既存テストは変更不要。カラーテスト用の新規テストを追加する。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `TreeOutputFormatter`のメソッドを`static`からインスタンスメソッドに変更（`_useColor`フィールド参照のため）。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Output/ColorMode.cs` | ColorMode enum（新規） |
| `src/BinAnalyzer.Output/AnsiColors.cs` | ANSIエスケープコード定数（新規） |
| `src/BinAnalyzer.Output/TreeOutputFormatter.cs` | カラー対応（変更） |
| `src/BinAnalyzer.Cli/Program.cs` | `--color` オプション追加（変更） |
| `tests/.../ColorOutputTests.cs` | カラー出力テスト8件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| ColorOutputTests | ColorAlways_ContainsAnsiEscapeCodes | AC-1 |
| ColorOutputTests | ColorNever_DoesNotContainAnsiEscapeCodes | AC-2 |
| ColorOutputTests | ColorAlways_IntegerValuesInCyan | AC-4 |
| ColorOutputTests | ColorAlways_EnumLabelsInMagenta | AC-4 |
| ColorOutputTests | ColorAlways_BytesInYellow | AC-4 |
| ColorOutputTests | ColorAlways_ValidationPassedInGreen | AC-5 |
| ColorOutputTests | ColorAlways_DimForOffsetsAndConnectors | AC-4 |
| ColorOutputTests | DefaultConstructor_NoColor | AC-7 |

### 受入条件の充足状況

- [x] AC-1: `--color always` でANSIカラーコードを含む出力が生成される
- [x] AC-2: `--color never` でカラーコードを含まない出力が生成される
- [x] AC-3: `--color auto` でターミナル判定が行われる
- [x] AC-4: 整数値、文字列値、バイト列、enum値がそれぞれ異なる色で表示される
- [x] AC-5: バリデーション ✓ が緑、✗ が赤で表示される
- [x] AC-6: パイプ先ではカラーコードが出力されない（auto時）
- [x] AC-7: 既存テスト全て通過（195テスト合格）

### 気づき・今後の課題

- JSON出力やhex dump出力のカラー対応は将来課題。
