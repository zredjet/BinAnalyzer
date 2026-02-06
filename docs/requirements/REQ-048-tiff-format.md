# REQ-048: TIFFフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-044（オフセットジャンプ）、REQ-045（エンディアン切り替え） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

TIFF（Tagged Image File Format）はプロフェッショナル向け画像フォーマット。デジタルカメラのRAWフォーマット（DNG等）のベースでもある。

先頭2バイト（"II"=リトルエンディアン, "MM"=ビッグエンディアン）でエンディアンが決まるユニークな構造を持ち、IFD（Image File Directory）エントリのオフセット値でデータを参照するポインタベースの構造。

オフセットジャンプとエンディアン切り替えの両方が必要な典型的なフォーマット。

## 機能要件

### 追加する機能

- [ ] `formats/tiff.bdef.yaml` フォーマット定義
  - ヘッダ（8バイト）
    - Byte Order（"II" or "MM"）
    - Magic（42 = 0x002A）
    - IFD Offset（最初のIFDへのオフセット）
  - IFD（Image File Directory）— seekでオフセット位置にジャンプ
    - エントリ数（u16）
    - IFDエントリの配列（各12バイト）
      - Tag（enum: ImageWidth, ImageLength, BitsPerSample, Compression, PhotometricInterpretation等）
      - Type（enum: BYTE, ASCII, SHORT, LONG, RATIONAL等）
      - Count
      - Value/Offset（4バイト以下はインライン値、超える場合はオフセット）
    - Next IFD Offset（0なら終端）
  - エンディアンは先頭のByte Orderに基づいて切り替え

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- Strip/Tileデータの解析
- Exif IFDの再帰解析
- GPS IFDの解析
- BigTIFF（8バイトオフセット）対応
- マルチページTIFFの全IFDチェーン走査

## 受入条件

1. [ ] `formats/tiff.bdef.yaml` が存在すること
2. [ ] リトルエンディアンTIFF（"II"）が正しく解析できること
3. [ ] ビッグエンディアンTIFF（"MM"）が正しく解析できること
4. [ ] IFDエントリのタグ・タイプ・値が正しく解析できること
5. [ ] 主要タグ（ImageWidth, ImageLength, BitsPerSample等）がenumラベル付きで表示されること
6. [ ] IFDオフセットへのseekが正しく動作すること
7. [ ] 有効なTIFFファイルでデコードエラーにならないこと
8. [ ] フォーマット定義がバリデーションに通ること
9. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにTIFFを追記
- [ ] docs/architecture.md — formats/ ディレクトリに tiff.bdef.yaml を追記

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
