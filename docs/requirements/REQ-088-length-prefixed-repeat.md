# REQ-088: 長さプレフィックス付きサブブロック繰り返し

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

GIF, PNGのzTXtチャンクなど、一部のバイナリフォーマットでは「長さプレフィックス付きサブブロック」のパターンが使われる。各サブブロックは先頭にサイズバイト（通常1バイト）を持ち、そのサイズ分のデータが続く。サイズが0のブロックが終端を示す。

現在のDSLではこのパターンを直接表現できず、サブブロック列全体を `bytes remaining` で取り込むワークアラウンドが必要。

```
# GIF sub-block構造:
[size: 1B][data: size B][size: 1B][data: size B]...[0x00]
```

既存の `repeat: until` では、終端の0x00ブロックも要素としてデコードされてしまい、空データブロックが配列に含まれる。`repeat: while`（REQ-087）と組み合わせれば表現可能だが、このパターンは頻出するため、専用の repeat モードを提供する価値がある。

```yaml
# 改善後:
- name: sub_blocks
  type: bytes
  repeat: length_prefixed
  length_prefix_size: 1        # プレフィックスのバイト数（デフォルト1）
  # サイズ0で自動終了、各要素のサイズはプレフィックスから自動決定
```

## 機能要件

### 追加する機能

- [ ] 新しいrepeatモード `length_prefixed` — 各要素の先頭N バイトがサイズを示し、サイズ0で終了
- [ ] `length_prefix_size: N` — プレフィックスのバイト数（デフォルト1、1〜4対応）
- [ ] プレフィックスのエンディアンは現在のスコープに従う
- [ ] サイズ0のプレフィックスで繰り返し終了（終端マーカーは配列に含めない）
- [ ] 各要素は `bytes` 型としてデコード（サイズはプレフィックスから自動決定）
- [ ] プレフィックス自体は出力に含めない（データ部分のみ）

### 変更する既存機能

- [ ] `RepeatMode` enum に `LengthPrefixed` を追加
- [ ] `FieldDefinition`: `LengthPrefixSize` プロパティ追加（int、デフォルト1）
- [ ] `BinaryDecoder`: length_prefixedモードのrepeatループ追加
- [ ] `YamlFieldModel`: `length_prefix_size` 属性のマッピング追加
- [ ] `FormatValidator`: `repeat: length_prefixed` の検証ルール追加

### 変更しないもの（スコープ外）

- プレフィックスが示すサイズに基づく内部struct解析（将来の拡張候補）
- 4バイト超のプレフィックス（実用上不要）
- プレフィックス値の加工（例: サイズ+1がデータ長、等の変換）

## 受入条件

1. [ ] `repeat: length_prefixed` で1バイトプレフィックスのサブブロック列が正しくデコードされること
2. [ ] サイズ0の終端マーカーで繰り返しが停止すること
3. [ ] 終端マーカー（サイズ0）は配列に含まれないこと
4. [ ] `length_prefix_size: 2` で2バイトプレフィックスが正しく動作すること
5. [ ] プレフィックスのエンディアンが現在のスコープに従うこと
6. [ ] GIFフォーマット: LZWサブブロック列が構造的にパースされること
7. [ ] FormatValidator: `repeat: length_prefixed` + 不正なlength_prefix_size（0, 5等）でエラーになること
8. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | RepeatMode enum追加、FieldDefinition: LengthPrefixSizeプロパティ |
| BinAnalyzer.Dsl | YamlFieldModel: length_prefix_sizeマッピング |
| BinAnalyzer.Engine | BinaryDecoder: length_prefixedモードのrepeatループ |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |

### 変更が必要なドキュメント

- [ ] docs/dsl-reference.md — length_prefixedモードの説明追加

### 変更が必要なフォーマット定義

- [ ] `formats/gif.bdef.yaml` — LZWサブブロック列を `repeat: length_prefixed` で構造化

### 変更が必要なテスト

- [ ] GifParsingTests: サブブロック構造の検証追加
- [ ] 単体テスト: length_prefixedの各種パターン

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
