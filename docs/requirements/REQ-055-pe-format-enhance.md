# REQ-055: PEフォーマット拡充

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| 作成日 | 2026-02-07 |
| 更新日 | 2026-02-07 |

## 背景・動機

REQ-046で実装されたPEフォーマット定義（`formats/pe.bdef.yaml`）はDOS Header、COFF Header、Optional Header、Section Tableの基本構造に対応しているが、Data Directoryが未解析であり、各種フラグ・enum定義が不完全な状態にある。

特にdll_characteristicsはセキュリティ機能（ASLR、DEP/NX、CFG等）の有効状態を示す重要なフィールドだが、現在は数値のみの表示でフラグの個別ビットが解読できない。machine_typeもIA64やRISC-V等の新しいアーキテクチャが不足しており、subsystemもEFI関連が未定義。pe_characteristicsやsection_characteristicsのフラグ値も不完全で、マルウェア解析やセキュリティ監査時に必要な情報が欠落している。

本要望では、既存の `pe.bdef.yaml` のフラグ・enum定義を拡充し、PE解析の実用性を向上させる。

## 機能要件

### 追加する機能

- [x] dll_characteristics のflags化（bitfield）
  - DYNAMIC_BASE = 0x40（ASLR対応）
  - NX_COMPAT = 0x100（DEP/NX対応）
  - NO_SEH = 0x400（SEH不使用）
  - GUARD_CF = 0x4000（Control Flow Guard）
  - TERMINAL_SERVER_AWARE = 0x8000（ターミナルサーバー対応）
- [x] machine_type enum拡充
  - IA64 = 0x200 (IMAGE_FILE_MACHINE_IA64)
  - RISCV64 = 0x5064 (IMAGE_FILE_MACHINE_RISCV64)
  - ARM64EC = 0xA641 (IMAGE_FILE_MACHINE_ARM64EC)
- [x] subsystem enum拡充
  - EFI_BOOT = 11 (IMAGE_SUBSYSTEM_EFI_BOOT_SERVICE_DRIVER)
  - EFI_RUNTIME = 12 (IMAGE_SUBSYSTEM_EFI_RUNTIME_DRIVER)
  - XBOX = 14 (IMAGE_SUBSYSTEM_XBOX)
- [x] pe_characteristics flags拡充
  - LINE_NUMS_STRIPPED = 0x4 (IMAGE_FILE_LINE_NUMS_STRIPPED)
  - LOCAL_SYMS_STRIPPED = 0x8 (IMAGE_FILE_LOCAL_SYMS_STRIPPED)
  - DEBUG_STRIPPED = 0x200 (IMAGE_FILE_DEBUG_STRIPPED)
  - SYSTEM = 0x1000 (IMAGE_FILE_SYSTEM)
- [x] section_characteristics flags拡充
  - LNK_NRELOC_OVFL = 0x1000000 (IMAGE_SCN_LNK_NRELOC_OVFL)
  - MEM_DISCARDABLE = 0x2000000 (IMAGE_SCN_MEM_DISCARDABLE)
  - MEM_NOT_CACHED = 0x4000000 (IMAGE_SCN_MEM_NOT_CACHED)
  - MEM_NOT_PAGED = 0x8000000 (IMAGE_SCN_MEM_NOT_PAGED)
  - MEM_SHARED = 0x10000000 (IMAGE_SCN_MEM_SHARED)

### 変更する既存機能

- [x] `formats/pe.bdef.yaml` — dll_characteristicsをenumからflags(bitfield)に変更、各enum定義にエントリ追加、section_characteristicsにフラグ追加

### 変更しないもの（スコープ外）

- Data Directoryの各テーブル（Import, Export, Resource等）の詳細解析（RVA解決が必要）
- .NETメタデータの解析
- リソースセクションの解析
- デバッグ情報（PDB参照等）の解析
- Authenticode署名の検証

## 受入条件

1. [x] dll_characteristicsがflags（bitfield）として表示され、DYNAMIC_BASE/NX_COMPAT/NO_SEH/GUARD_CF/TERMINAL_SERVER_AWAREの各ビットが個別に識別可能であること
2. [x] machine_typeにIA64(0x200), RISCV64(0x5064), ARM64EC(0xA641)が追加されていること
3. [x] subsystemにEFI_BOOT(11), EFI_RUNTIME(12), XBOX(14)が追加されていること
4. [x] pe_characteristicsにLINE_NUMS_STRIPPED(0x4), LOCAL_SYMS_STRIPPED(0x8), DEBUG_STRIPPED(0x200), SYSTEM(0x1000)が追加されていること
5. [x] section_characteristicsにLNK_NRELOC_OVFL(0x1000000), MEM_DISCARDABLE(0x2000000), MEM_NOT_CACHED(0x4000000), MEM_NOT_PAGED(0x8000000), MEM_SHARED(0x10000000)が追加されていること
6. [x] 既存のPE解析機能（DOS Header、COFF Header、Optional Header、Section Table）に回帰がないこと
7. [x] 既存テストが全て通過すること（`dotnet test` 全通過）

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

- [x] formats/pe.bdef.yaml — dll_characteristicsのflags化、各enum/flags定義の拡充
- [x] docs/architecture.md — フォーマット一覧の説明更新（PE flags対応を明記）

---

## 設計メモ

YAML DSLのみの変更。dll_characteristicsをenumからbitfieldに変更し、各セキュリティフラグを個別ビットとして定義。machine_type、subsystem、pe_characteristics、section_characteristicsの各enum/flags定義にエントリ追加。

---

## 実装メモ

### 実装中の設計変更

なし。YAML DSLの拡充のみで全受入条件を充足。

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `formats/pe.bdef.yaml` | dll_characteristicsのbitfield化、enum/flags拡充 |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| PeParsingTests | PeFormat_LoadsWithoutErrors | 7 |
| PeParsingTests | PeFormat_DecodesWithRecovery | 6 |
| PeParsingTests | PeFormat_DosHeader_DecodesCorrectly | 6 |
| PeParsingTests | PeFormat_CoffHeader_DecodesCorrectly | 2, 6 |
| PeParsingTests | PeFormat_TreeOutput_ContainsExpectedElements | 7 |

### 気づき・今後の課題

- Data Directoryの詳細解析（Import/Export/Resource等）はRVA解決が必要で未対応
- dll_characteristicsのbitfield化により、NX_COMPAT, ASLR等のセキュリティフラグが可読性高く表示可能に
