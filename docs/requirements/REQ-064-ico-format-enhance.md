# REQ-064: ICOフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-036で実装されたICOフォーマット定義（`formats/ico.bdef.yaml`）では、画像データが一塊のraw bytesとして表示されるのみで、個別画像の解析が行われていない。

ICOファイルは複数の画像を内包し、各エントリのimage_offsetとimage_sizeで個々の画像データの位置とサイズが指定される。画像データの実体はBMP DIBヘッダ（BMPファイルヘッダを除いたDIB形式）またはPNG形式のいずれかであり、先頭バイトのPNGシグネチャ（0x89504E47）で判別可能である。現状ではこれらの個別画像にアクセスできず、ICOファイルの構造解析として不十分である。

本要望では、各画像データをseekとsizeで個別にパースし、画像形式の判別情報を付加することで、ICOファイルの解析精度を向上させる。

## 機能要件

### 追加する機能

- [ ] image_dataの個別化
  - repeat_countでentries配列を解析後、image_data_blocks配列を同じrepeat_countで定義
  - 各image_data_blockは `seek(image_offset)` で対応するオフセットにジャンプし、`size(image_size)` のbytesを読み取る
- [ ] PNG検出と形式判別
  - 画像先頭4バイトが0x89504E47（PNGシグネチャ）ならPNG形式、それ以外はBMP DIBヘッダ形式
  - 先読みの制約があるため、`image_data(bytes size image_size)` として読み取り、descriptionに形式の説明を記載する簡易実装とする
  - switchでの分岐が可能な場合はswitchを使用
- [ ] ico_type enum確認・改善
  - 1 = ICO（アイコン）
  - 2 = CUR（カーソル）
  - descriptionを追加して各値の意味を明記

### 変更する既存機能

- [ ] `formats/ico.bdef.yaml` — ico_entry改善（image_offset/image_sizeを活用した個別パース）、image_data_blocks配列追加、ico_type enum改善

### 変更しないもの（スコープ外）

- PNG形式画像の内部構造解析（PNGチャンクレベルの解析）
- BMP DIB形式画像の内部構造解析（ピクセルデータの展開）
- CUR形式のホットスポット座標の詳細解析
- マルチサイズアイコンの最適サイズ選択ロジック

## 受入条件

1. [ ] 各画像データがimage_data_blocks配列として個別に表示されること
2. [ ] seekで各エントリのimage_offsetに正しくジャンプし、image_sizeバイトを読み取ること
3. [ ] 画像形式（PNG/BMP DIB）の判別情報がdescriptionまたはswitch分岐で表示されること
4. [ ] ico_type enumに1=ICO, 2=CURが定義され、descriptionが付与されていること
5. [ ] 既存のICOヘッダ・エントリ解析に回帰がないこと
6. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なファイル

- [ ] formats/ico.bdef.yaml — image_data個別化、seekによるオフセットジャンプ、ico_type enum改善
- [ ] docs/architecture.md — フォーマット一覧の説明更新（ICO個別画像パース対応を明記）

---

## 設計メモ

> 設計Phase（Phase 2）で記入する。要望定義時点では空欄でよい。

### 設計方針

### モデル変更

### インタフェース変更

### 代替案

### 懸念事項

---

## 実装メモ

> 実装Phase（Phase 3-4）で記入する。設計時点では空欄でよい。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
