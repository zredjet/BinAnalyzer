# REQ-021: Hexdump出力のカラー対応

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | approved |
| 優先度 | 低 |
| 依存 | REQ-012（カラー出力基盤） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

ツリー出力はREQ-012でカラー対応済みだが、hexdump出力はモノクロのまま。hexdumpではフィールドの境界が視覚的に分かりづらく、特に隣接するフィールドを区別するためにカラー表示が有効。

既存のカラー基盤（`AnsiColors`, `ColorMode`）を再利用できるため、実装コストは低い。

## 機能要件

### 追加する機能

- [ ] Hexdump出力のフィールド別色分け
  - 異なるフィールドに属するバイトを交互に色分け（背景色またはテキスト色）
  - フィールド名列のカラー表示
- [ ] オフセット列の色分け（dim/灰色）
- [ ] ASCII列の色分け（フィールドに対応）
- [ ] `--color` オプションのhexdump出力への適用

### 変更する既存機能

- [ ] HexDumpOutputFormatter: `ColorMode` パラメータの追加、カラーコード埋め込み
- [ ] Program.cs: hexdump出力時にも `--color` 設定を渡す

### 変更しないもの（スコープ外）

- フィールド境界の罫線表示
- カスタムカラーテーマ

## 受入条件

1. [ ] `--output hexdump --color always` でカラー付きhexdumpが出力されること
2. [ ] 異なるフィールドのバイトが視覚的に区別できること
3. [ ] `--color never` でカラーコードを含まないこと
4. [ ] `--color auto` でターミナル判定が行われること
5. [ ] 既存のhexdumpテストが全て通過すること（デフォルトはColorMode.Never）
6. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | HexDumpOutputFormatter のカラー対応 |
| BinAnalyzer.Cli | hexdump出力時に ColorMode を渡す |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

既存の `HexDumpOutputFormatter` に `ColorMode` パラメータを追加する。フィールド境界を交互の色で表現する。既存のカラー基盤（`AnsiColors`, `ColorMode`）を再利用。

### 変更内容

**HexDumpOutputFormatter:**
```csharp
public sealed class HexDumpOutputFormatter
{
    private readonly bool _useColor;

    public HexDumpOutputFormatter(ColorMode mode = ColorMode.Never)
    {
        _useColor = mode switch { ... };
    }

    public string Format(DecodedStruct root, ReadOnlyMemory<byte> data) { ... }
}
```

### カラースキーム

- オフセット列: Dim（灰色）
- hexバイト列: フィールドごとに交互色（Cyan / Green）で色分け
- ASCII列: hexバイト列と同じ色
- フィールド名列: Yellow
- ヘッダー行: Dim

フィールド境界の色は `FieldRegion` のインデックスの偶奇で決定:
```csharp
var color = fieldIndex % 2 == 0 ? AnsiColors.Cyan : AnsiColors.Green;
```

### Program.cs の変更

```csharp
if (outputFormat == "hexdump")
{
    var hexFormatter = new HexDumpOutputFormatter(colorMode);  // colorModeを渡す
    output = hexFormatter.Format(decoded, data);
}
```

### モデル変更

なし。

### インタフェース変更

`HexDumpOutputFormatter` のコンストラクタにオプションパラメータ追加（デフォルト `ColorMode.Never` で後方互換）。

### 代替案

なし。

### 懸念事項

1. **既存テストの互換性**: デフォルトが `ColorMode.Never` なので既存テストは影響なし。カラーテスト用の新規テストを追加する。

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
