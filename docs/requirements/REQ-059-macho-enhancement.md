# REQ-059: Mach-Oフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

現在のmacho.bdef.yaml（REQ-047で導入）は64-bitヘッダ（magic=0xFEEDFACF）のみに対応しており、32-bitバイナリ（magic=0xFEEDFACE）を解析できない。ロードコマンドもLC_SEGMENT_64の1種のみが構造的に解析され、LC_SYMTAB、LC_UUID、LC_LOAD_DYLIB、LC_MAIN、LC_BUILD_VERSIONなどの頻出コマンドはgeneric扱いとなっている。

また、cpu_type、file_type、lc_type等のenumが主要な値のみで不完全であり、解析結果の可読性に影響している。macOSバイナリの解析・セキュリティ調査において、これらのロードコマンドの詳細情報は基本的な要件であり、拡充の価値が高い。

## 機能要件

### 追加する機能

- [ ] 32-bit対応
  - magic(0xFEEDFACE) に対応するswitch分岐を追加し、32-bit/64-bitヘッダをmagicバイトで切り替え
  - 32-bitヘッダではreservedフィールドなし、アドレス/サイズが32bit
- [ ] LC_SYMTAB解析（cmd=0x02）
  - `symoff`: u32（シンボルテーブルのファイルオフセット）
  - `nsyms`: u32（シンボル数）
  - `stroff`: u32（文字列テーブルのファイルオフセット）
  - `strsize`: u32（文字列テーブルサイズ）
- [ ] LC_UUID解析（cmd=0x1B）
  - `uuid`: bytes, size=16
- [ ] LC_LOAD_DYLIB解析（cmd=0x0C）
  - `name_offset`: u32（名前文字列のオフセット）
  - `timestamp`: u32
  - `current_version`: u32
  - `compat_version`: u32
  - `name`: ascii, size=remaining（ライブラリ名）
- [ ] LC_MAIN解析（cmd=0x80000028）
  - `entryoff`: u64（エントリポイントのファイルオフセット）
  - `stacksize`: u64
- [ ] LC_BUILD_VERSION解析（cmd=0x32）
  - `platform`: u32, enum=platform
  - `minos`: u32（最小OS バージョン）
  - `sdk`: u32（SDKバージョン）
  - `ntools`: u32（ビルドツール数）
- [ ] lc_type enum拡充
  - LC_RPATH = 0x8000001C
  - LC_CODE_SIGNATURE = 0x1D
  - LC_SEGMENT_SPLIT_INFO = 0x1E
  - LC_DYLD_INFO_ONLY = 0x80000022
  - LC_DYLD_EXPORTS_TRIE = 0x80000033
  - LC_DYLD_CHAINED_FIXUPS = 0x80000034
- [ ] cpu_type enum拡充
  - CPU_TYPE_POWERPC = 18
  - CPU_TYPE_POWERPC64 = 0x01000012
- [ ] file_type enum拡充
  - MH_DSYM = 10
  - MH_KEXT_BUNDLE = 11
  - MH_FILESET = 12
- [ ] platform enum追加（LC_BUILD_VERSION用）
  - MACOS = 1
  - IOS = 2
  - TVOS = 3
  - WATCHOS = 4
  - BRIDGEOS = 5
  - MACCATALYST = 6
  - IOSSIMULATOR = 7

### 変更する既存機能

- [ ] `formats/macho.bdef.yaml` — 32-bit対応、ロードコマンド追加、enum拡充
- [ ] `docs/architecture.md` — フォーマット一覧の説明更新（Mach-Oエントリの拡充内容反映）

### 変更しないもの（スコープ外）

- Fat Binary（Universal Binary）ヘッダの解析
- シンボルテーブルのエントリ詳細解析（nlist構造体）
- コード署名の内部構造解析
- dyld shared cache の解析
- LC_BUILD_VERSION内のビルドツール配列の詳細解析

## 受入条件

1. [ ] 32-bit Mach-Oバイナリ（magic=0xFEEDFACE）のヘッダが解析可能であること
2. [ ] LC_SYMTABからsymoff/nsyms/stroff/strsizeが抽出できること
3. [ ] LC_UUIDから16バイトのUUIDが取得できること
4. [ ] LC_LOAD_DYLIBからライブラリ名・バージョン情報が抽出できること
5. [ ] LC_MAINからエントリポイントオフセット・スタックサイズが抽出できること
6. [ ] LC_BUILD_VERSIONからplatform・minos・sdkが抽出でき、platform enumが表示されること
7. [ ] lc_type/cpu_type/file_type enumが拡充され、新規追加値にラベルが表示されること
8. [ ] 有効なMach-Oファイルで拡充後もデコードエラーにならないこと
9. [ ] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [ ] `formats/macho.bdef.yaml` — 32-bit対応、6種のロードコマンド構造追加、enum拡充
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
