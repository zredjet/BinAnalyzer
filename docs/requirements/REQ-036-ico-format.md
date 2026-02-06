# REQ-036: ICOフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | draft |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

ICO（Windows Icon）は非常にシンプルなバイナリフォーマット。ICONDIRヘッダ + ICONDIRENTRYの配列 + 画像データで構成される。

エントリ数がヘッダに記載されているため、配列の繰り返し回数を式で指定する実用的な例となる。CUR（カーソル）ファイルも同じ構造。

## 機能要件

### 追加する機能

- [ ] `formats/ico.bdef.yaml` フォーマット定義
  - ICONDIR（reserved, type, count）— typeはリトルエンディアン
  - ICONDIRENTRYの配列（count個）— 幅、高さ、色数、reserved、planes、bpp、size、offset
  - 画像データ部分はraw bytesとして扱う

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- 埋め込みBMP/PNGデータの解析（将来、オフセットジャンプ + ネスト解析で対応可能）
- CURファイル専用のカーソルホットスポット解析

## 受入条件

1. [ ] `formats/ico.bdef.yaml` が存在すること
2. [ ] ICONDIRヘッダが正しく解析できること
3. [ ] ICONDIRENTRYが配列として正しい個数分解析できること
4. [ ] 各エントリの幅・高さ・BPP等が抽出できること
5. [ ] 有効なICOファイルでデコードエラーにならないこと
6. [ ] フォーマット定義がバリデーションに通ること
7. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにICOを追記
- [ ] docs/architecture.md — formats/ ディレクトリに ico.bdef.yaml を追記

---

## 設計メモ

### YAML定義の概要構造

```
name: ICO
endianness: little
root: ico

structs:
  ico            — icondir(struct) + entries(struct icon_dir_entry, repeat_count: {count}) + image_data(bytes, remaining)
  icondir        — reserved(u16, expected=0) + type(u16) + count(u16)
  icon_dir_entry — width(u8) + height(u8) + color_count(u8) + reserved(u8)
                   + planes(u16) + bpp(u16) + bytes_in_res(u32) + image_offset(u32)
```

### 主要struct

- **ico**: ルート構造体。icondir(struct) でヘッダを読み取り、entries(struct icon_dir_entry, repeat_count: {count}) でエントリ配列をヘッダのcount値分繰り返し、残りの画像データをraw bytesで一括読み取り。
- **icondir**: ICONDIR構造。reserved(u16, expected=[0x00, 0x00]) + type(u16, enum: ico_type) + count(u16)。typeフィールドでICO(1)かCUR(2)かを判別。
- **icon_dir_entry**: ICONDIRENTRY構造。各画像エントリの幅・高さ・色数等のメタ情報と、画像データへのオフセット・サイズを保持。

### enumの使い方

```yaml
enums:
  ico_type:
    - value: 1
      label: ICO
      description: "アイコン"
    - value: 2
      label: CUR
      description: "カーソル"
```

typeフィールドに適用し、ICO/CURの判別を表示する。

### 繰り返しの方式

- **entries**: `repeat_count: "{count}"` でICONDIRのcount値を参照し、エントリをcount回繰り返す。PNGのchunks(repeat: eof)やWAVのchunks(repeat: eof)と異なり、要素数がヘッダに明示されているため repeat_count を使用する。
- **image_data**: エントリ配列の後に続く画像データ領域は `bytes, size: remaining` で一括読み取り。

### 注意点・制約

1. **width/heightの0=256問題**: ICOフォーマットでは width=0, height=0 が 256x256 を意味する。DSLでは値をそのまま表示するため、ユーザーが出力を見て解釈する必要がある。descriptionフィールドで「0は256を意味する」旨を記述して補足する。
2. **画像データの個別解析はスコープ外**: 各エントリのimage_offsetフィールドはファイル先頭からの絶対オフセットを示すが、BinAnalyzerは現在オフセットジャンプ（seek）をサポートしていないため、個々の画像データを独立して解析することはできない。画像データ領域全体をraw bytesとして一括読み取りする。
3. **CURファイルとの互換性**: CURファイルはICOと同一のバイナリ構造（type=2）。planes/bppフィールドがCURではhotspot_x/hotspot_yとして解釈されるが、バイナリ構造は同一のため同じYAML定義で解析可能。
4. **バリデーション**: icondir.reservedフィールドにexpected=[0x00, 0x00]を指定し、有効なICOファイルであることを確認する。
5. **シンプルな構造**: ICOは4フォーマット中最もシンプルであり、既存DSL機能（repeat_count、enum、expected）のみで完全に定義可能。switchは不要。

---

## 実装メモ

> 実装Phaseで記入する。

### 実装中の設計変更

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| | | |

### 気づき・今後の課題
