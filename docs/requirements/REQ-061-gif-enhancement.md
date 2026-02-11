# REQ-061: GIFフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 低 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のgif.bdef.yaml（REQ-035で導入）では、サブブロック構造が一括bytesとして扱われており詳細未解析である。graphic_control_extのpacked_fieldやlogical_screen_descのpacked_fieldがbitfield化されていない。また、アニメーションGIFで広く使用されるNETSCAPE2.0アプリケーション拡張のloop_count（ループ回数）が未認識であり、application_extのサブブロックデータが生bytesのままである。

GIFアニメーションの解析においてdisposal_methodやloop_countは基本的なパラメータであり、bitfield化やNETSCAPE2.0認識は実用上の改善となる。

## 機能要件

### 追加する機能

- [ ] graphic_control_ext改善
  - packed_fieldをbitfield化
    - `disposal_method`: bits 4:2, enum=disposal_method
    - `user_input`: bit 1
    - `transparent_flag`: bit 0
- [ ] application_ext改善
  - NETSCAPE2.0のloop_count解析
    - `sub_block_size`: u8（=3を期待）
    - `sub_block_id`: u8（=1を期待）
    - `loop_count`: uint16le（ループ回数、0=無限ループ）
- [ ] image_block改善
  - local_color_table_flagのif条件でlocal_color_tableを条件パース
  - local_color_table_flagが1の場合のみlocal_color_tableを読み取り
- [ ] logical_screen_desc改善
  - packed_fieldをbitfield化
    - `global_color_table_flag`: bit 7
    - `color_resolution`: bits 6:4
    - `sort_flag`: bit 3
    - `size_of_global_color_table`: bits 2:0
- [ ] plain_text_extension追加（extension_label=0x01）
  - テキスト表示パラメータ（text_grid_left, text_grid_top, text_grid_width, text_grid_height, cell_width, cell_height, fg_color_index, bg_color_index）
  - テキストデータ（サブブロック連鎖、remainingでraw bytes）

### 変更する既存機能

- [ ] `formats/gif.bdef.yaml` — bitfield化、NETSCAPE2.0認識、条件パース追加、plain_text_extension追加
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新（GIFエントリの拡充内容反映）

### 変更しないもの（スコープ外）

- LZW圧縮データの展開
- サブブロック連鎖の個別サブブロック単位の詳細解析（size + data の繰り返し構造）
- アニメーションフレームのレンダリング
- NETSCAPE2.0以外のアプリケーション拡張の詳細解析

## 受入条件

1. [ ] graphic_control_extのpacked_fieldがbitfield表示され、disposal_method/user_input/transparent_flagが個別に確認できること
2. [ ] logical_screen_descのpacked_fieldがbitfield表示され、global_color_table_flag/color_resolution/sort_flag/size_of_global_color_tableが個別に確認できること
3. [ ] NETSCAPE2.0アプリケーション拡張のloop_countが解析・表示されること
4. [ ] image_blockでlocal_color_table_flagに基づくlocal_color_tableの条件パースが動作すること
5. [ ] plain_text_extension（label=0x01）がswitch caseに追加されていること
6. [ ] 有効なGIFファイルで拡充後もデコードエラーにならないこと
7. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] `formats/gif.bdef.yaml` — bitfield化、NETSCAPE2.0認識、条件パース、plain_text_extension追加
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新

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
