# REQ-047: Mach-Oフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | REQ-044（オフセットジャンプ） |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

Mach-O（Mach Object）はmacOS/iOSのネイティブバイナリフォーマット。ELF（Linux）、PE（Windows）と並ぶ主要な実行ファイルフォーマットで、macOS環境での開発・セキュリティ解析に重要。

Header → Load Commands → Sections/Segments という構造で、Load Commandのオフセットがセグメントやセクションのデータ位置を指すため、オフセットジャンプ機能が必要。

## 機能要件

### 追加する機能

- [ ] `formats/macho.bdef.yaml` フォーマット定義
  - Mach-O Header（32ビット: 0xFEEDFACE, 64ビット: 0xFEEDFACF）
    - magic, cputype（enum）, cpusubtype, filetype（enum: MH_EXECUTE, MH_DYLIB等）
    - ncmds, sizeofcmds, flags（flags）
  - Load Commandsの繰り返し（ncmds個）
    - cmd（enum: LC_SEGMENT_64, LC_SYMTAB, LC_DYLD_INFO等）
    - cmdsize
    - switchによるコマンド種別の分岐
      - LC_SEGMENT_64: segname, vmaddr, vmsize, fileoff, filesize, nsects, sections配列
      - LC_UUID: uuid（16バイト）
      - LC_ID_DYLIB / LC_LOAD_DYLIB: name offset, timestamp, version
      - その他: raw bytes
  - Section（LC_SEGMENT_64内）
    - sectname, segname, addr, size, offset, align, reloff, nreloc, flags

### 変更する既存機能

なし（YAMLファイルの追加のみ）。

### 変更しないもの（スコープ外）

- Fat Binary（Universal Binary）ヘッダの解析
- シンボルテーブルの詳細解析
- コード署名の解析
- dyld shared cache の解析

## 受入条件

1. [ ] `formats/macho.bdef.yaml` が存在すること
2. [ ] Mach-O Headerが正しく解析できること（64ビット）
3. [ ] Load Commandsが正しい個数分解析できること
4. [ ] LC_SEGMENT_64のセグメント情報とセクション配列が解析できること
5. [ ] LC_UUIDからUUIDが取得できること
6. [ ] 有効なMach-Oファイルでデコードエラーにならないこと
7. [ ] フォーマット定義がバリデーションに通ること
8. [ ] 既存テストが全て通過すること

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

- [ ] README.md — 対応フォーマットにMach-Oを追記
- [ ] docs/architecture.md — formats/ ディレクトリに macho.bdef.yaml を追記

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
