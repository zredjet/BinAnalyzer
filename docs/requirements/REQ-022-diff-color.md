# REQ-022: diff出力のカラー対応

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-012（カラー出力基盤）, REQ-014（差分出力） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

diff出力はREQ-014で実装済みだが、カラー表示に対応していない。変更(~)、追加(+)、削除(-) の視覚的な区別をカラーで強調することで、差分の把握が容易になる。

git diffと同様のカラースキーム（追加=緑、削除=赤、変更=黄色）を採用することで、開発者にとって直感的な表示となる。

## 機能要件

### 追加する機能

- [ ] diff出力のカラー対応
  - 追加行（`+`）: 緑
  - 削除行（`-`）: 赤
  - 変更行（`~`）: 黄色
  - フィールドパス: 通常色
  - 旧値: 赤（dim）
  - 新値: 緑
  - 矢印（→）: 灰色（dim）
  - ヘッダー（「差分: N 件」）: 通常色
- [ ] `diff` サブコマンドに `--color` オプションの追加

### 変更する既存機能

- [ ] DiffOutputFormatter: `ColorMode` パラメータの追加、カラーコード埋め込み
- [ ] Program.cs（diff サブコマンド）: `--color` オプションの追加

### 変更しないもの（スコープ外）

- サイドバイサイド表示
- コンテキスト行の表示（変更なしのフィールド表示）

## 受入条件

1. [ ] `diff --color always` でカラー付きdiff出力が生成されること
2. [ ] 追加行が緑で表示されること
3. [ ] 削除行が赤で表示されること
4. [ ] 変更行の旧値と新値が異なる色で表示されること
5. [ ] `--color never` でカラーコードを含まないこと
6. [ ] 既存のdiffテストが全て通過すること（デフォルトはカラーなし）
7. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | DiffOutputFormatter のカラー対応 |
| BinAnalyzer.Cli | diff サブコマンドに --color 追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

既存の `DiffOutputFormatter` に `ColorMode` パラメータを追加する。git diffと同様のカラースキームを採用。既存のカラー基盤を再利用。

### 変更内容

**DiffOutputFormatter:**
```csharp
public sealed class DiffOutputFormatter
{
    private readonly bool _useColor;

    public DiffOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch { ... };
    }

    public string Format(DiffResult result) { ... }
}
```

### カラースキーム

```csharp
DiffKind.Changed => $"  {C("~", AnsiColors.Yellow)} {entry.FieldPath}: {C(entry.OldValue, AnsiColors.Red)} {C("→", AnsiColors.Dim)} {C(entry.NewValue, AnsiColors.Green)}"
DiffKind.Added   => $"  {C("+", AnsiColors.Green)} {entry.FieldPath}: {C(entry.NewValue, AnsiColors.Green)}"
DiffKind.Removed => $"  {C("-", AnsiColors.Red)} {entry.FieldPath}: {C(entry.OldValue, AnsiColors.Red)}"
```

### Program.cs の変更

diff サブコマンドに `--color` オプション追加。`DiffOutputFormatter(colorMode)` を渡す。

### モデル変更

なし。

### インタフェース変更

`DiffOutputFormatter` のコンストラクタにオプションパラメータ追加（デフォルト `ColorMode.Never` で後方互換）。

### 代替案

なし。

### 懸念事項

1. **既存テストの互換性**: デフォルトが `ColorMode.Never` なので既存テストは影響なし。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
