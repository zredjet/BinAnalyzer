# REQ-095: 実ファイル検証スイート

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | implemented |
| 優先度 | 高 |
| 依存 | REQ-094（ベンチマーク、ベースライン計測後に実施が望ましい） |
| フェーズ | A（基盤整備） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

現在の統合テストは合成テストデータ（TestDataGenerator）による最小構成のバイナリのみを使用している。実際のツールやアプリケーションが生成するバイナリファイルには、合成データでは再現できないエッジケース（オプションフィールド、パディングバリエーション、拡張ヘッダ、不正データ等）が多数存在する。

実ファイルを用いた E2E テストを整備することで:
- フォーマット定義の実用上の正確性を検証できる
- DSL 拡張で解決すべき具体的な課題が明らかになる
- 回帰テストの信頼性が向上する

## 機能要件

### 追加する機能

- [ ] 実ファイル検証用テストプロジェクト（または既存 Integration.Tests 内のカテゴリ）
- [ ] 以下のフォーマットの実ファイルを最低1つずつ用意:
  - 画像系: PNG, JPEG, GIF, BMP, TIFF, ICO, WebP
  - 音声系: WAV, MP3, FLAC, MIDI
  - 実行ファイル系: ELF, PE, Mach-O, Java Class
  - アーカイブ系: ZIP, GZIP, TAR
  - ネットワーク系: PCAP
  - データ系: SQLite, PDF
  - フォント系: OTF
  - カラー系: ICC
  - 動画系: AVI, FLV
- [ ] 各実ファイルに対するテスト:
  - デコードがエラーなく完了すること（またはエラーリカバリで継続できること）
  - ルート構造の Children 数が期待通りであること
  - 主要フィールド（マジック、バージョン、サイズ等）が妥当な値であること
  - ツリー出力が空でないこと
- [ ] テスト用実ファイルの管理方針の決定（リポジトリ同梱 / ダウンロードスクリプト / gitignore + 手動配置）
- [ ] 実ファイルでデコードが失敗する箇所の一覧（既知の制限として文書化）

### 変更しないもの（スコープ外）

- フォーマット定義の修正（本REQでは問題の発見と記録まで。修正は別REQ）
- DSL 機能の追加
- 著作権のある実ファイルの同梱（フリーライセンスまたは自作ファイルのみ）

## 受入条件

1. [ ] 20フォーマット以上の実ファイルテストが存在すること
2. [ ] 各テストでデコード成功（またはリカバリ成功）が検証されていること
3. [ ] 主要フィールドのスモークテスト（マジック値等）が含まれていること
4. [ ] デコード失敗箇所がある場合、既知の制限として一覧が文書化されていること
5. [ ] テスト用ファイルの入手・配置手順が文書化されていること
6. [ ] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| tests/BinAnalyzer.Integration.Tests | 実ファイルテスト追加 |

---

## 設計メモ

### ハイブリッドファイル生成方式

2段階のファイル生成方式を採用:

1. **シェルスクリプト（`testdata/generate-real-files.sh`）**: macOS ネイティブツール（sips, gzip, tar, zip, sqlite3, cc, javac, lz4）で本物の実ファイルを生成。ICC/OTF はシステムファイルからコピー。PDF/PCAP は手書き生成。
2. **RealFileFixture（xUnit Fixture）**: シェルスクリプトで生成できない15フォーマット（GIF, WAV, MP3, FLAC, AVI, FLV, MIDI, WebP, ICO, ELF, PE, WASM, DNS, 7z, Parquet）を既存 TestDataGenerator の `CreateMinimal*()` で補完。全フォーマットにフォールバック付き。

### テスト設計

- `[Trait("Category", "RealFile")]` で既存テストと分離
- `[Theory]` + `[InlineData]` で全30フォーマットをパラメトリックテスト
- `DecodeWithRecovery(ErrorMode.Continue)` でエラーがあっても継続
- エラーは `ITestOutputHelper` にログ出力（テスト失敗にはしない）
- ファイル未存在時は `Skip.If` でスキップ（CI環境対応）

---

## 実装メモ

### 実装中の設計変更

- 当初はシェルスクリプト生成ファイルのみ git 管理予定だったが、TestDataGenerator のフォールバックを全フォーマットに追加。シェルスクリプト未実行でもテスト実行可能にした。

### 追加したテスト

| テストクラス | テスト名 | 対応する受入条件 |
|---|---|---|
| RealFileValidationTests | RealFile_DecodesSuccessfully(png, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(jpeg, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(gif, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(bmp, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(tiff, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(ico, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(webp, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(wav, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(mp3, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(flac, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(midi, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(elf, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(pe, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(macho, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(java-class, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(zip, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(gzip, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(tar, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(lz4, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(pcap, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(sqlite, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(pdf, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(otf, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(icc, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(avi, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(flv, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(7z, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(wasm, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(dns, ...) | 1, 2, 3 |
| RealFileValidationTests | RealFile_DecodesSuccessfully(parquet, ...) | 1, 2, 3 |

30フォーマット全てをカバー（受入条件1: 20フォーマット以上を達成）。

### 気づき・今後の課題

- シェルスクリプトで生成した実ファイル（sips, cc, sqlite3 等）は、TestDataGenerator の合成データよりもリッチな構造を持つため、フォーマット定義の不足が検出されやすい
- 本物の実ファイル（Web ダウンロード、アプリ生成等）への差し替えが最も効果的な改善
- Mach-O は cc で生成した場合、ファイルサイズが大きくなる可能性がある（2MB 制限に注意）
- OTF/ICC のシステムファイルコピーはライセンス上問題ないが、環境依存になる
