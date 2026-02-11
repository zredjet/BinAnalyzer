# REQ-101: 構造化 diff

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし（独立して実装可能。ただし REQ-100 完了後のほうがフォーマット定義が充実しており効果が高い） |
| フェーズ | D（応用） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

バイナリファイルのバージョン間比較は、従来 `xxd` + `diff` やバイナリ diff ツールで行われるが、出力はバイト列の差分であり、構造的な意味を読み取るのが困難である。

BinAnalyzer は既にバイナリファイルを構造化ツリーにデコードする機能を持っているため、同一フォーマットの2ファイルをそれぞれデコードし、フィールド単位で値の差分を表示すれば、「どの構造フィールドがどう変わったか」を人間が理解しやすい形で提示できる。

ユースケース例:
- PNG ファイルの編集前後で IHDR の width/height が変わったことを確認
- ELF バイナリのビルド間で entry_point やセクションサイズの変化を追跡
- JPEG の Exif メタデータの変更点を把握

## 機能要件

### 追加する機能

- [x] CLI に `diff` サブコマンドを追加
  ```bash
  binanalyzer diff file1.png file2.png -f formats/png.bdef.yaml
  ```
- [x] 2つのデコード済みツリーをフィールド名ベースで再帰的に比較
- [x] 差分の種類を区別して表示:
  - **値の変更**: 同名フィールドの値が異なる
  - **追加**: file2 にのみ存在するフィールド（配列要素の増加等）
  - **削除**: file1 にのみ存在するフィールド（配列要素の減少等）
- [x] 出力フォーマット:
  - ツリー形式（変更箇所をハイライト）
  - フラット形式（変更フィールドのパスと値の一覧）
- [x] 変更がない場合は「差分なし」と表示
- [x] 配列（repeat）フィールドの差分表示（要素数の変化、各要素の値の変化）

### 使用例

```
$ binanalyzer diff old.png new.png -f formats/png.bdef.yaml

  png_file
    signature          (同一)
    ihdr_chunk
      length           (同一)
      chunk_type       (同一)
      width            800 → 1024
      height           600 → 768
      bit_depth        (同一)
      color_type       (同一)
    idat_chunks
      [0].length       12345 → 15678
      [1]              (削除)
    iend_chunk         (同一)
```

### 変更しないもの（スコープ外）

- 異なるフォーマット間の比較
- バイナリレベル（バイト列）の diff 表示
- 3ファイル以上の同時比較（3-way merge）
- GUI / インタラクティブな diff ビューア

## 受入条件

1. [x] `binanalyzer diff` コマンドで2ファイルの構造化差分が表示できること
2. [x] 値の変更・追加・削除が区別して表示されること
3. [x] 差分がない場合に適切なメッセージが表示されること
4. [x] 配列要素数の変化が正しく検出・表示されること
5. [x] ツリー形式とフラット形式の出力が選択できること
6. [x] 単体テスト（diff ロジック）が追加されていること
7. [x] 統合テスト（CLI 経由の diff 実行）が追加されていること
8. [x] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | DiffKind / DiffEntry / DiffResult モデル追加 |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | DiffEngine（再帰比較エンジン）追加。DecodedFlags / DecodedVirtual 比較を含む |
| BinAnalyzer.Output | DiffOutputFormatter（フラット形式）、DiffTreeOutputFormatter（ツリー形式）追加 |
| BinAnalyzer.Cli | `diff` サブコマンド、`--output flat|tree` オプション追加 |

### 新規プロジェクト（検討）

| プロジェクト | 役割 |
|---|---|
| BinAnalyzer.Diff（案） | diff ロジックを独立プロジェクトにする場合 |

diff ロジックの規模次第で、Output に含めるか新規プロジェクトにするかを設計時に判断する。

→ 結果: DiffEngine は Engine プロジェクト内に配置。出力フォーマッターは Output プロジェクト内に配置。独立プロジェクトは不要と判断。

---

## 設計メモ

### アーキテクチャ

- **DiffEngine**（Engine層）: 2つの DecodedStruct を再帰的に比較し、DiffResult（フラットな DiffEntry リスト）を生成。
- **DiffOutputFormatter**（Output層）: DiffResult を受け取り、フラット形式（パス + 値の差分一覧）で出力。
- **DiffTreeOutputFormatter**（Output層）: 2つの DecodedStruct を直接受け取り、ツリー形式で出力。DiffResult を経由しない。

### ツリー形式出力の設計判断

ツリー形式出力では DiffResult（フラットリスト）を経由せず、DiffTreeOutputFormatter が直接2つのデコード済みツリーを同時走査する方式を採用。理由:

- ツリー出力には「同一」ノードも含める必要がある（フラットな差分リストには含まれない）
- フラットリストからツリー構造を再構成するより、直接走査が自然かつシンプル
- 子が全て同一の struct は折りたたんで `name (同一)` と表示する最適化が容易

### 比較対象ノード種別

DiffEngine は以下の全 DecodedNode サブタイプを比較対象とする:

- DecodedInteger, DecodedString, DecodedBytes, DecodedFloat, DecodedBitfield（初期実装）
- DecodedStruct, DecodedArray, DecodedCompressed（初期実装、再帰比較）
- DecodedFlags（ギャップ補完で追加、RawValue ベース比較）
- DecodedVirtual（ギャップ補完で追加、Value.ToString() ベース比較）

### exit code

- 差分あり: exit code 1
- 差分なし: exit code 0
- ツリー形式でも同じ exit code ルールを適用（HasDifferences プロパティで判定）

---

## 実装メモ

### 実装中の設計変更

- DiffTreeOutputFormatter は HasDifferences プロパティを公開し、CLI が exit code を判定できるようにした（Format 呼び出し後に参照）。
- FormatLeafValue / FormatNodeType に DecodedFlags / DecodedVirtual のケースを追加し、Added/Removed 表示時にも正しく値を表示できるようにした。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| DiffEngineTests | FlagsValueChanged_ReportsChanged | 2 |
| DiffEngineTests | VirtualValueChanged_ReportsChanged | 2 |
| DiffOutputTests | TreeFormat_NoDifferences_ShowsAllIdentical | 3, 5 |
| DiffOutputTests | TreeFormat_ChangedValue_ShowsArrow | 2, 5 |
| DiffOutputTests | TreeFormat_AddedField_ShowsPlus | 2, 5 |
| DiffOutputTests | TreeFormat_RemovedField_ShowsMinus | 2, 5 |
| DiffOutputTests | TreeFormat_NestedStruct_IndentsCorrectly | 5 |
| DiffOutputTests | TreeFormat_ArrayDiff_ShowsElementChanges | 4, 5 |

### 気づき・今後の課題

- DecodedCompressed の diff ツリー表示は、DecodedContent がある場合は子を再帰展開するが、RawDecompressed のみの場合はバイト列の文字列表現で比較する簡易実装。
- DecodedError ノードの比較は現状未対応（型が一致しない場合の Changed としてのみ報告される）。必要に応じて ErrorMessage ベースの比較を追加できる。
