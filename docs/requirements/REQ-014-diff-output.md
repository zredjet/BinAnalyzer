# REQ-014: 構造的差分表示

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | REQ-003（JSON出力: 差分の内部表現としてJSON構造を活用可能） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

バイナリファイルの新旧バージョン比較において、バイトレベルの差分（バイナリdiff）では構造的な意味が分からない。同じフォーマット定義で2つのバイナリを解析し、フィールド単位で値の差分を表示することで、「どのフィールドがどう変わったか」が明確になる。

ユースケース:
- ファイルフォーマットのバージョン違いの確認
- 画像メタデータの変更確認（リサイズ前後、変換前後）
- テスト結果の比較（期待値と実際値の構造的差分）

## 機能要件

### 追加する機能

- [ ] `DiffEngine` クラスの新規作成（`BinAnalyzer.Engine` に配置）
  - 2つの `DecodedStruct` を入力とし、`DiffResult` を返す
  - フィールド名をキーにして再帰的に比較
- [ ] `DiffResult` モデルの新規作成（`BinAnalyzer.Core` に配置）
  - 変更されたフィールドの一覧
  - 追加されたフィールド（片方にのみ存在）
  - 削除されたフィールド（片方にのみ存在）
  - 各差分エントリ: フィールドパス、旧値、新値
- [ ] `DiffOutputFormatter` の新規作成（`BinAnalyzer.Output` に配置）
  - 差分結果をツリー形式で表示
  - 変更されたフィールドのみ表示（変更なしのフィールドは省略、またはオプションで表示）
- [ ] CLIに `diff` サブコマンドを追加
  ```
  binanalyzer diff <file1> <file2> -f <format>
  ```

### 差分出力例

```
PNG 差分
├── chunks[0] (IHDR)
│   ├── data.width: 100 → 200
│   ├── data.height: 100 → 150
│   └── crc: 0x1A2B3C4D → 0xDEADBEEF
├── chunks[1] (tEXt)
│   └── (file1のみ)
└── chunks[2] (IEND)
    └── (変更なし)
```

### 変更する既存機能

- [ ] `Program.cs`（CLI）: `diff` サブコマンドの追加

### 変更しないもの（スコープ外）

- バイトレベルの差分表示（hex diff）
- 3つ以上のファイルの同時比較
- 異なるフォーマット定義間の比較
- 差分のマージ・パッチ適用

## 受入条件

1. [ ] `DiffEngine.Compare(DecodedStruct, DecodedStruct)` が `DiffResult` を返すこと
2. [ ] 同一のバイナリを比較した場合、差分が0件であること
3. [ ] 整数フィールドの値が異なる場合、旧値と新値が報告されること
4. [ ] 文字列フィールドの値が異なる場合、旧値と新値が報告されること
5. [ ] 配列の要素数が異なる場合、追加/削除が報告されること
6. [ ] 差分出力にフィールドパスが含まれること
7. [ ] CLIの `diff` サブコマンドで2つのPNGファイルの差分が表示されること
8. [ ] 差分なしの場合、その旨が表示されること
9. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | `DiffResult` モデルの新規追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | `DiffEngine` の新規追加 |
| BinAnalyzer.Output | `DiffOutputFormatter` の新規追加 |
| BinAnalyzer.Cli | `diff` サブコマンドの追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [ ] docs/architecture.md — DiffEngineコンポーネントの追加
- [x] CLAUDE.md — 変更不要
- [ ] README.md — diffコマンドの使い方を追記

---

## 設計メモ

### 設計方針

デコード結果ツリーのノード名をキーにして再帰的に比較する。比較はフィールド値の等価性に基づく。

`DiffEngine` は2つの `DecodedStruct` を入力とし、差分リストを返す。差分結果のフォーマッティングは `DiffOutputFormatter` が担当。

### モデル変更

```
src/BinAnalyzer.Core/Diff/
├── DiffResult.cs          # 差分結果
├── DiffEntry.cs           # 個別差分エントリ
└── DiffKind.cs            # Changed / Added / Removed

src/BinAnalyzer.Engine/
└── DiffEngine.cs          # 差分比較ロジック

src/BinAnalyzer.Output/
└── DiffOutputFormatter.cs # 差分表示
```

**DiffEntry**:
```csharp
public sealed record DiffEntry(
    DiffKind Kind,          // Changed / Added / Removed
    string FieldPath,       // "chunks[0].data.width"
    string? OldValue,       // 旧値の文字列表現（Removedの場合はnull）
    string? NewValue        // 新値の文字列表現（Addedの場合はnull）
);
```

**DiffResult**:
```csharp
public sealed class DiffResult
{
    public IReadOnlyList<DiffEntry> Entries { get; }
    public bool HasDifferences => Entries.Count > 0;
}
```

### 比較アルゴリズム

```
Compare(nodeA, nodeB, path):
  if 型が異なる → Changed(path, typeA, typeB)
  if DecodedInteger → 値比較
  if DecodedString → 値比較
  if DecodedBytes → バイト列比較
  if DecodedStruct → children を名前でマッチングし再帰
  if DecodedArray → 要素数の差分 + インデックスマッチで再帰
```

配列比較: インデックスベース（要素[0]同士, [1]同士...）。要素数が異なる場合、多い方の余剰要素をAdded/Removedとして報告。

### CLI変更

`diff` サブコマンドの追加:
```csharp
var diffCommand = new Command("diff", "2つのバイナリファイルの構造的差分を表示")
{
    new Argument<FileInfo>("file1"),
    new Argument<FileInfo>("file2"),
    formatOption,
};
rootCommand.AddCommand(diffCommand);
```

### 代替案

**案A: JSON出力の差分で代用する（不採用）**
- 理由: JSON差分ツール（jq等）では構造的な意味（フィールドパス、enum値等）が失われる。専用の構造的差分の方がユーザーにとって有用。

### 懸念事項

1. **配列要素のマッチング**: PNGチャンクのような可変長配列で、要素の追加/削除があった場合、インデックスベースの比較では「ずれ」が生じる。名前ベースのマッチング（チャンクタイプ等）は汎用的に実装が困難。初期実装ではインデックスベースとし、将来改善の余地を残す。

2. **大きなバイナリの差分量**: 多数のチャンクを持つファイルでは差分エントリが大量になる可能性。`--summary` オプション（変更されたフィールド数のみ表示）を将来追加可能。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- `DiffEngine` は静的クラスとして実装（ステートレスな比較ロジック）。
- CLIの `diff` サブコマンドは差分ありの場合に終了コード1、差分なしの場合に0を返す。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `src/BinAnalyzer.Core/Diff/DiffKind.cs` | DiffKind enum（新規） |
| `src/BinAnalyzer.Core/Diff/DiffEntry.cs` | DiffEntry record（新規） |
| `src/BinAnalyzer.Core/Diff/DiffResult.cs` | DiffResult クラス（新規） |
| `src/BinAnalyzer.Engine/DiffEngine.cs` | 差分比較エンジン（新規） |
| `src/BinAnalyzer.Output/DiffOutputFormatter.cs` | 差分出力フォーマッター（新規） |
| `src/BinAnalyzer.Cli/Program.cs` | `diff` サブコマンド追加（変更） |
| `tests/.../DiffEngineTests.cs` | DiffEngine テスト10件（新規） |
| `tests/.../DiffOutputTests.cs` | DiffOutputFormatter テスト5件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DiffEngineTests | IdenticalStructs_NoDifferences | AC-2 |
| DiffEngineTests | IntegerValueChanged_ReportsChanged | AC-3 |
| DiffEngineTests | StringValueChanged_ReportsChanged | AC-4 |
| DiffEngineTests | NestedStruct_ReportsNestedPath | AC-6 |
| DiffEngineTests | ArrayElementCountDifferent_ReportsAddedRemoved | AC-5 |
| DiffEngineTests | ArrayElementAdded_ReportsAdded | AC-5 |
| DiffEngineTests | FieldOnlyInLeft_ReportsRemoved | AC-1 |
| DiffEngineTests | FieldOnlyInRight_ReportsAdded | AC-1 |
| DiffEngineTests | IntegerWithEnumLabel_IncludesLabelInValue | AC-3 |
| DiffEngineTests | BytesChanged_ReportsChanged | AC-3 |
| DiffOutputTests | NoDifferences_DisplaysNoDiffMessage | AC-8 |
| DiffOutputTests | ChangedEntry_DisplaysArrow | AC-6 |
| DiffOutputTests | AddedEntry_DisplaysPlus | AC-6 |
| DiffOutputTests | RemovedEntry_DisplaysMinus | AC-6 |
| DiffOutputTests | MultipleEntries_DisplaysCount | AC-6 |

### 受入条件の充足状況

- [x] AC-1: `DiffEngine.Compare` が `DiffResult` を返す
- [x] AC-2: 同一のバイナリ比較で差分0件
- [x] AC-3: 整数フィールドの値差分が旧値・新値で報告される
- [x] AC-4: 文字列フィールドの値差分が旧値・新値で報告される
- [x] AC-5: 配列要素数の差分が追加/削除として報告される
- [x] AC-6: 差分出力にフィールドパスが含まれる
- [x] AC-7: CLIの `diff` サブコマンドで差分表示可能
- [x] AC-8: 差分なしの場合、その旨が表示される
- [x] AC-9: 既存テスト全て通過（215テスト合格）

### 気づき・今後の課題

- 配列要素のキーベースマッチング（例: チャンクタイプで照合）は将来課題。
- `--summary` オプション（変更フィールド数のみ表示）は将来課題。
