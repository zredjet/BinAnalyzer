# REQ-074: ICC Profileフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

ICC Profile（International Color Consortium）はカラーマネジメントの標準フォーマットで、PNG、TIFF、JPEG等の画像フォーマット内に埋め込まれるカラープロファイルを定義する。

ヘッダ（128バイト固定）にプロファイルの基本情報が格納され、タグテーブルで各種カラー情報へのオフセットを管理する。seekを活用したオフセットベースのテーブル解析のショーケースとして適している。

ICC構造:
- プロファイルヘッダ（128バイト固定）
- タグテーブル（タグ数 + タグエントリ配列）
- タグデータ（各タグのオフセット位置にデータが格納）

## 機能要件

### 追加する機能

- [ ] `formats/icc.bdef.yaml` フォーマット定義
  - プロファイルヘッダ（128バイト）
    - profile_size（u32、プロファイル全体のサイズ）
    - preferred_cmm（u32、優先CMMタイプ）
    - version（bytes 4、プロファイルバージョン）
    - device_class（ascii 4、enum: scnr=Scanner, mntr=Monitor, prtr=Printer, link=DeviceLink, spac=ColorSpace, abst=Abstract, nmcl=NamedColor）
    - color_space（ascii 4、enum: XYZ, Lab, Luv, YCbr, Yxy, RGB, GRAY, HSV, HLS, CMYK, CMY等）
    - pcs（ascii 4、Profile Connection Space）
    - creation_date（bytes 12、日時情報）
    - signature（ascii 4、expected "acsp"）
    - primary_platform（ascii 4、enum: APPL=Apple, MSFT=Microsoft, SGI=Silicon Graphics, SUNW=Sun Microsystems）
    - flags（u32、プロファイルフラグ）
    - device_manufacturer（u32）
    - device_model（u32）
    - attributes（u64、デバイス属性）
    - rendering_intent（u32、enum: Perceptual=0, RelativeColorimetric=1, Saturation=2, AbsoluteColorimetric=3）
    - pcs_illuminant（bytes 12、PCS照明体のXYZ値）
    - creator（u32、プロファイル作成者シグネチャ）
    - id（bytes 16、プロファイルID / MD5）
    - reserved（bytes 28、予約領域）
  - タグテーブル
    - tag_count（u32、タグ数）
    - tags（repeat_count tag_count）
      - signature（ascii 4、タグシグネチャ）
      - offset（u32、タグデータのオフセット）
      - size（u32、タグデータのサイズ）

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- 個別タグデータの詳細解析（タグの種類ごとに異なるフォーマット）
- ICC.2（iccMAX）プロファイルへの対応
- カラー変換処理
- 埋め込みプロファイルの抽出（PNG/TIFF/JPEG内からの取り出し）

## 受入条件

1. [ ] `formats/icc.bdef.yaml` が存在すること
2. [ ] プロファイルヘッダの各フィールドが正しく解析できること
3. [ ] signatureフィールドの "acsp" が正しく検証されること
4. [ ] device_classがenumラベル付きで表示されること
5. [ ] color_spaceがenumラベル付きで表示されること
6. [ ] rendering_intentがenumラベル付きで表示されること
7. [ ] タグテーブル（tag_count + タグエントリ配列）が正しく解析できること
8. [ ] ICCヘッダ・タグテーブルが解析可能であること
9. [ ] フォーマット定義がバリデーションに通ること
10. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] README.md — 対応フォーマットにICC Profileを追記
- [ ] docs/architecture.md — formats/ ディレクトリに icc.bdef.yaml を追記

---

## 設計メモ

> 設計Phaseで記入する。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
