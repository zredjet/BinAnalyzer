# REQ-008: WAVフォーマット定義

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 中 |
| 依存 | なし |
| 作成日 | 2026-02-06 |
| 更新日 | 2026-02-06 |

## 背景・動機

WAV（RIFF WAVE）は広く使用される音声フォーマットであり、RIFFコンテナ構造を持つ。PNGのチャンク構造と類似した「タグ＋長さ＋データ」パターンだが、リトルエンディアンである点が異なる。

BinAnalyzerのエンディアン切替機能の実証と、RIFFベースのフォーマット（AVI等）への対応の足がかりとして有用。

WAV構造:
- RIFFヘッダー（"RIFF" + ファイルサイズ + "WAVE"）
- fmtチャンク（オーディオフォーマット、チャンネル数、サンプリングレート等）
- dataチャンク（オーディオデータ）
- その他オプションチャンク（LIST, fact等）

## 機能要件

### 追加する機能

- [ ] `formats/wav.bdef.yaml` の新規作成
- [ ] 対応するWAV構造:
  - RIFFヘッダー（"RIFF" シグネチャ、ファイルサイズ、"WAVE" フォームタイプ）
  - fmtチャンク（オーディオフォーマット、チャンネル数、サンプリングレート、バイトレート、ブロックアライン、ビット深度）
  - dataチャンク（raw bytesとして表示）
  - 未知のチャンク（raw_dataへのフォールバック）
- [ ] WAV用のenum定義:
  - オーディオフォーマット（1=PCM, 3=IEEE Float, 6=A-law, 7=mu-law等）
- [ ] エンディアン指定: `endianness: little`
- [ ] テストデータ生成: `WavTestDataGenerator` の作成

### 変更する既存機能

- なし（新しいフォーマット定義ファイルの追加のみ）

### 変更しないもの（スコープ外）

- 拡張fmtチャンク（WAVEFORMATEXTENSIBLE）
- LISTチャンク内のINFOメタデータの詳細解析
- オーディオデータの波形解析

## 受入条件

1. [ ] `formats/wav.bdef.yaml` が `YamlFormatLoader` で正しく読み込めること
2. [ ] バリデーター（REQ-001実装済みの場合）でエラーが0件であること
3. [ ] 最小WAVバイナリ（PCM, 1ch, 8000Hz）がデコードできること
4. [ ] RIFFヘッダーの "RIFF" と "WAVE" シグネチャが検証されること
5. [ ] fmtチャンクの各フィールドが正しくデコードされること（リトルエンディアン）
6. [ ] オーディオフォーマットのenum表示が正しいこと（1 = "PCM" 等）
7. [ ] チャンクのswitch分岐（fmt/data/unknown）が正しく動作すること
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
| BinAnalyzer.Integration.Tests | `WavTestDataGenerator`, WAVパーステスト追加 |

### 変更が必要なドキュメント

- [x] docs/dsl-reference.md — 変更不要
- [x] docs/architecture.md — 変更不要
- [x] CLAUDE.md — 変更不要
- [ ] README.md — 対応フォーマット一覧にWAVを追記

---

## 設計メモ

### 設計方針

フォーマット定義ファイル（`formats/wav.bdef.yaml`）の追加のみ。エンジンへの変更不要。

PNGのチャンク構造（ビッグエンディアン、repeat: eof）に対して、WAVのRIFFチャンク構造（リトルエンディアン、repeat: eof、switchによるチャンク種別分岐）が既存DSLで表現可能であることを実証する。

### DSL構造設計

```yaml
name: WAV
endianness: little
root: wav

enums:
  audio_format:
    - value: 1
      label: PCM
    - value: 3
      label: IEEE_Float
    - value: 6
      label: A-law
    - value: 7
      label: mu-law

structs:
  wav:
    - name: riff_id
      type: ascii
      size: "4"
      expected: [0x52, 0x49, 0x46, 0x46]  # "RIFF"
    - name: file_size
      type: uint32
    - name: wave_id
      type: ascii
      size: "4"
      expected: [0x57, 0x41, 0x56, 0x45]  # "WAVE"
    - name: chunks
      type: struct
      struct: riff_chunk
      repeat: eof

  riff_chunk:
    - name: chunk_id
      type: ascii
      size: "4"
    - name: chunk_size
      type: uint32
    - name: data
      type: switch
      size: "{chunk_size}"
      switch_on: "{chunk_id}"
      cases:
        "'fmt '": fmt_chunk
      default: raw_data

  fmt_chunk:
    - name: audio_format
      type: uint16
      enum: audio_format
    - name: num_channels
      type: uint16
    - name: sample_rate
      type: uint32
    - name: byte_rate
      type: uint32
    - name: block_align
      type: uint16
    - name: bits_per_sample
      type: uint16

  raw_data:
    - name: data
      type: bytes
      size: remaining
```

注意点: RIFFチャンクの `chunk_size` が奇数の場合、パディング1バイトが追加される（RIFF仕様）。初期実装ではパディング非対応とする（大部分のWAVでは問題ない）。

### テストデータ設計

`WavTestDataGenerator.CreateMinimalWav()`:
- RIFF ヘッダー + fmt チャンク（PCM, 1ch, 8000Hz, 8bit）+ data チャンク（数バイトのダミーデータ）
- リトルエンディアンであることの検証に最適

### 代替案

なし。

### 懸念事項

1. **`fmt ` のスペース**: チャンクID `fmt ` は末尾にスペースを含む4文字。switch caseの条件として `"'fmt '"` と指定する。既存の式パーサーがスペースを含む文字列リテラルを正しく処理できるか要確認（シングルクォート内はそのまま保持されるため問題ないはず）。

2. **奇数サイズのパディング**: RIFF仕様では奇数サイズのチャンクの後に1バイトのパディングがある。これに対応するには条件付きフィールド（REQ-006）または専用のパディング処理が必要。初期実装では非対応とする。

---

## 実装メモ

### 実装中の設計変更

- 設計メモ通りに実装。大きな設計変更なし。
- エンジンへの変更不要。YAML定義ファイルとテストの追加のみ。

### 変更・追加ファイル

| ファイル | 変更内容 |
|---|---|
| `formats/wav.bdef.yaml` | WAVフォーマット定義（新規） |
| `tests/.../WavTestDataGenerator.cs` | 最小WAVバイナリ生成（新規） |
| `tests/.../WavParsingTests.cs` | WAV統合テスト6件（新規） |

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| WavParsingTests | WavFormat_LoadsWithoutErrors | AC-1, AC-2 |
| WavParsingTests | WavFormat_DecodesMinimalWav | AC-3 |
| WavParsingTests | WavFormat_RiffHeader_DecodesCorrectly | AC-4 |
| WavParsingTests | WavFormat_FmtChunk_DecodesCorrectly | AC-5, AC-6 |
| WavParsingTests | WavFormat_DataChunk_DecodesCorrectly | AC-7 |
| WavParsingTests | WavFormat_TreeOutput_ContainsExpectedElements | AC-6 |

### 受入条件の充足状況

- [x] AC-1: `formats/wav.bdef.yaml` が正しく読み込める
- [x] AC-2: バリデーターでエラー0件
- [x] AC-3: 最小WAVバイナリがデコードできる
- [x] AC-4: RIFF/WAVEシグネチャが検証される
- [x] AC-5: fmtチャンクの各フィールドが正しくデコードされる
- [x] AC-6: enum表示が正しい（PCM等）
- [x] AC-7: switchによるfmt/data/unknown分岐が正しく動作
- [x] AC-8: 既存テスト全て通過（167テスト合格）

### 気づき・今後の課題

- RIFF仕様の奇数サイズパディング（チャンクサイズが奇数の場合に1バイト追加）は未対応。条件付きフィールド（REQ-006）を活用すれば対応可能だが、大部分のWAVでは問題ない。
- 拡張fmtチャンク（WAVEFORMATEXTENSIBLE）は未対応。cbSize, validBitsPerSample等の追加フィールドの条件付き読み取りが必要。
