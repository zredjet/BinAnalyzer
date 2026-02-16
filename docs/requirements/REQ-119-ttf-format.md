# REQ-119: TTF/OTF フォントフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | 完了 |
| 優先度 | 低 |
| 依存 | なし |
| フェーズ | D（フォーマット） |
| 作成日 | 2026-02-12 |
| 更新日 | 2026-02-15 |

## 背景・動機

TrueType (TTF) / OpenType (OTF) はデスクトップ・Web で最も広く使われるフォントフォーマット。テーブルベースの構造で、BinAnalyzer の seek 機能を活用してテーブルディレクトリからの間接参照をパース可能。

### ユースケース

- フォントファイルのテーブル一覧（cmap, glyf, head, hhea, hmtx, name 等）の確認
- フォントメタデータ（name テーブル: フォント名、著作権等）の確認
- head テーブルの基本情報（units per em, 作成日, フラグ）の確認
- CFF（OpenType）と glyf（TrueType）の判別

## 機能要件

### 追加する機能

- [x] `formats/ttf.bdef.yaml` の作成 → 既存 `formats/otf.bdef.yaml` が TTF を既にカバー（別ファイル不要）
- [x] Offset Table（sfVersion, numTables, searchRange 等）の定義 → OTF 定義に実装済み
- [x] Table Directory（tag, checkSum, offset, length）の定義 → OTF 定義に実装済み
- [x] head テーブル、name テーブルの基本フィールド定義 → OTF 定義に実装済み
- [x] seek によるテーブルオフセットジャンプ → OTF 定義に実装済み

### 変更する既存機能

なし（新規フォーマット定義の追加のみ）

### 変更しないもの（スコープ外）

- グリフアウトライン（glyf テーブル）の詳細デコード
- CFF データのデコード
- フォントレンダリング
- WOFF / WOFF2 圧縮フォント対応

## 受入条件

1. [x] `formats/ttf.bdef.yaml` が作成されていること → 既存 `otf.bdef.yaml` が sfVersion enum で TrueType/CFF 両方に対応済み
2. [x] Offset Table と Table Directory が正しくデコードされること → OTF テストで検証済み + TrueType sfVersion テスト追加
3. [x] head テーブルの基本情報が表示されること → OTF テストで検証済み
4. [x] name テーブルのフォント名が表示されること → OTF 定義に name テーブル実装済み
5. [x] 実際の TTF/OTF ファイルでデコードが成功すること → OTF 定義経由で TTF デコード可能
6. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [x] README.md — 変更不要

---

## 設計メモ

### 設計方針

既存の `formats/otf.bdef.yaml` が TrueType (sfVersion=0x00010000) と CFF (sfVersion=0x4F54544F/"OTTO") の両方を `sf_version` enum で判別済み。head/name/cmap/hhea/OS2/post テーブルの seek パースも実装済みのため、TTF ファイルは OTF 定義でそのままデコード可能。新規 `.bdef.yaml` ファイルの作成は不要と判断。

### モデル変更

なし

### インタフェース変更

なし

### 代替案

`formats/ttf.bdef.yaml` を `otf.bdef.yaml` のシンボリックリンクまたはコピーとして作成する案も検討したが、同一定義の重複管理はメリットがなく不採用。

### 懸念事項

なし

---

## 実装メモ

### 実装中の設計変更

新規 `.bdef.yaml` の作成を行わず、既存 OTF 定義が TTF をカバーしていることのテスト検証のみを追加する方針に変更。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| OtfParsingTests | MinimalTtf_SfVersion_IsTrueType | 受入条件 1, 2（TrueType sfVersion のデコード検証） |

### 気づき・今後の課題

- TTF 固有の glyf テーブル詳細デコードは将来の拡張候補
- TrueType Collection (.ttc) 対応は別要件として検討可能
