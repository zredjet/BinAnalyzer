# REQ-068: TARフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-037で実装されたTARフォーマット定義（`formats/tar.bdef.yaml`）は基本的なヘッダフィールドとtypeflag enumに対応しているが、以下の点が不完全である。

1. **typeflag不完全**: FIFO('6')、GNU拡張（LONGNAME='L'、LONGLINK='K'）、PAX拡張（EXTENDED='x'、GLOBAL='g'）が含まれておらず、GNU tarやPAX形式のアーカイブで未知のタイプフラグが数値のみで表示される。
2. **UStar検証なし**: UStarフォーマットであることを示すmagicフィールド（"ustar"）のvalidateが未設定で、フォーマットの妥当性検証ができない。
3. **GNU/PAX拡張なし**: GNU tarの長いファイル名やPAXの拡張属性に対応するtypeflagが定義されていないため、これらのアーカイブ内のエントリが適切にラベル付けされない。
4. **UStar拡張フィールド不足**: prefix、devmajor、devminorといったUStar拡張ヘッダフィールドが不足している可能性がある。

本要望では、既存の `tar.bdef.yaml` のtypeflag enumを拡充し、validateの追加、UStar拡張フィールドの補完を行う。

## 機能要件

### 追加する機能

- [ ] tar_typeflag enum拡充
  - FIFO = 0x36（'6'）
  - GNU_LONGNAME = 0x4C（'L'）— GNU拡張：長いファイル名
  - GNU_LONGLINK = 0x4B（'K'）— GNU拡張：長いリンク名
  - PAX_EXTENDED = 0x78（'x'）— PAX拡張ヘッダ（次のエントリに適用）
  - PAX_GLOBAL = 0x67（'g'）— PAXグローバル拡張ヘッダ（以降の全エントリに適用）
- [ ] ustar magic validate追加
  - validate: `"{magic == 'ustar'}"` （UStar形式の場合）
  - UStarフォーマットであることの妥当性検証
- [ ] checksum validate description追加
  - checksumフィールドに description: "Sum of all header bytes (treating checksum field as spaces)" を追記
- [ ] prefix field追加（UStar拡張）
  - name: prefix, type: ascii, size: 155
  - 現在のheaderにprefixが含まれていなければ追加
- [ ] devmajor/devminor追加
  - devmajor: ascii, size: 8 — デバイスファイル用メジャー番号
  - devminor: ascii, size: 8 — デバイスファイル用マイナー番号

### 変更する既存機能

- [ ] `formats/tar.bdef.yaml` — tar_typeflag enum拡充、magic validate追加、checksum description追加、UStar拡張フィールド補完

### 変更しないもの（スコープ外）

- GNU拡張ヘッダの内部構造解析（LongName/LongLinkのデータブロック内のパス名抽出）
- PAX拡張ヘッダの内部構造解析（key=value形式のテキストパース）
- checksumの実際の計算・検証（式エンジンでのヘッダバイト合計は非対応）
- tar.gz / tar.bz2 / tar.xz の圧縮層の解析
- ASCIIオクタル文字列からの数値変換（sizeフィールドの動的解釈）

## 受入条件

1. [ ] tar_typeflag enumにFIFO(0x36), GNU_LONGNAME(0x4C), GNU_LONGLINK(0x4B), PAX_EXTENDED(0x78), PAX_GLOBAL(0x67)が追加されていること
2. [ ] magicフィールドにvalidateが設定されていること（UStar形式の検証）
3. [ ] checksumフィールドに計算方法の説明がdescriptionとして記載されていること
4. [ ] UStar拡張フィールド（prefix, devmajor, devminor）がヘッダ構造に含まれていること
5. [ ] 既存のTARヘッダフィールド（name, mode, uid, gid, size, mtime等）の解析に回帰がないこと
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

- [ ] formats/tar.bdef.yaml — typeflag enum拡充、validate追加、UStar拡張フィールド補完
- [ ] docs/architecture.md — フォーマット一覧の説明更新（TAR UStar拡張対応を明記）

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
