# REQ-094: パフォーマンスベンチマーク

## メタ情報

| 項目 | 値 |
|---|---|
| ステータス | done |
| 優先度 | 高 |
| 依存 | なし |
| フェーズ | A（基盤整備） |
| 作成日 | 2026-02-11 |
| 更新日 | 2026-02-11 |

## 背景・動機

30フォーマットの定義が揃い、DSL 機能も充実してきた。今後さらに DSL 拡張（動的エンディアン、配列 seek 等）を進める前に、現時点のデコード性能のベースラインを計測しておく必要がある。大規模ファイルでのボトルネックが判明すれば、DSL 拡張の設計判断（遅延評価の必要性、メモリ戦略等）にも影響する。

## 機能要件

### 追加する機能

- [x] BenchmarkDotNet を使用したベンチマークプロジェクト `benchmarks/BinAnalyzer.Benchmarks` の新設
- [x] 以下のシナリオのベンチマーク:
  - 小規模ファイル（〜1KB）のデコードスループット（PNG ヘッダ、BMP 等）
  - 中規模ファイル（〜100KB）のデコードスループット（ELF、PE、OTF 等）
  - 大規模ファイル（1MB〜100MB）のデコードスループット（PCAP、SQLite、ZIP 等）
  - YAML フォーマット定義のロード時間（全30フォーマット）
  - 式評価のスループット（複雑な式を大量に評価）
- [x] ベンチマーク用テストデータの生成（既存の TestDataGenerator を拡張、または実ファイルを使用）
- [x] メモリアロケーション量の計測（BenchmarkDotNet の `[MemoryDiagnoser]`）
- [ ] ベンチマーク結果の CI での定期実行と回帰検出（任意）

### 変更しないもの（スコープ外）

- パフォーマンス最適化の実施（本REQは計測のみ。最適化は別REQ）
- デコーダーエンジンの構造変更

## 受入条件

1. [x] BenchmarkDotNet プロジェクトが作成され、`dotnet run` で実行できること
2. [x] 小・中・大規模ファイルのデコード時間が計測できること
3. [x] メモリアロケーション量が計測できること
4. [x] YAML ロード時間が計測できること
5. [x] ベースライン結果が記録されていること（docs または results ディレクトリ）
6. [x] 既存テストが全て通過すること

## 影響範囲

### 変更が必要なプロジェクト

| プロジェクト | 変更内容の概要 |
|---|---|
| BinAnalyzer.Core | 変更なし |
| BinAnalyzer.Dsl | 変更なし |
| BinAnalyzer.Engine | 変更なし |
| BinAnalyzer.Output | 変更なし |
| BinAnalyzer.Cli | 変更なし |
| tests/BinAnalyzer.Benchmarks（新規） | ベンチマークプロジェクト |

---

## 設計メモ

### 設計方針

既存のベンチマークプロジェクト `benchmarks/BinAnalyzer.Benchmarks/` のスケルトンを拡充する方針。既に `DecodeBenchmarks`（小規模のみ）、`ExpressionBenchmarks`、`OutputBenchmarks` が存在していたため、不足していた中規模・大規模デコードベンチマークと YAML ロードベンチマークを追加する。

### ベンチマーク設計

#### デコードベンチマーク — スケーラブルなテストデータ生成

実ファイルではなく、`GlobalSetup` 内でプログラム的にバイナリデータを生成する方式を採用。理由:

- 再現性: データサイズを正確に制御でき、環境依存がない
- 柔軟性: チャンク数・パケット数をパラメータ化して中規模/大規模を同一ロジックで生成
- CI 対応: 外部ファイル不要でベンチマーク実行可能

**PNG スケーラブル生成** (`CreateScaledPng`):
```
Signature(8) + IHDR(25) + IDAT*N(12+dataSize) + IEND(12)
N=1, dataSize=12  →  57B   (小規模: DecodeMinimalPng)
N=100, dataSize=100 → 11,245B ~11KB (中規模: DecodeMediumPng)
N=10000, dataSize=100 → 1,120,045B ~1.1MB (大規模: DecodeLargePng)
```
`repeat: eof` による繰り返しデコードの性能を計測。

**PCAP スケーラブル生成** (`CreateScaledPcap`):
```
Global header(24) + N * (packet header(16) + Ethernet(14) + IPv4(20) + TCP(20))
N=100  → 7,024B   ~7KB  (中規模: DecodeMediumPcap)
N=10000 → 700,024B ~700KB (大規模: DecodeLargePcap)
```
`switch` + ネスト構造体（ethernet_frame → ipv4_packet → tcp_segment）の性能を計測。各パケットは完全に有効な構造（`ether_type=0x0800`, `protocol=6(TCP)`, `data_offset=5`）を持つ。

#### YAML ロードベンチマーク

- `LoadAllFormats`: 全 30 フォーマット定義を一括ロード。全体の I/O + パース + IR 変換コスト
- `LoadSingleFormat_Png`: 中程度の複雑さのフォーマット（enum 6 + flags 1 + struct 16）
- `LoadSingleFormat_Elf`: 高複雑度のフォーマット（大量の enum/struct 定義）

#### フォーマットディレクトリの解決

BenchmarkDotNet はベンチマーク実行時に exe を深いサブディレクトリ（`bin/Release/net10.0/{guid}/bin/Release/net10.0/`）にコピーして実行する。既存の `Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats")` による相対パス解決は不正確で、`formats` ディレクトリを見つけられない。

**解決策**: `FindFormatsDirectory()` ヘルパーメソッドを追加。`AppContext.BaseDirectory` から親ディレクトリを辿り、`*.bdef.yaml` ファイルを含む `formats` ディレクトリを検出する。

### 代替案

- **`[Params]` による IDAT 数のパラメータ化**: BenchmarkDotNet の `[Params(100, 10000)]` で 1 つのメソッドにまとめる案もあったが、結果テーブルの視認性と個別フィルタ実行の容易さを優先し、個別メソッドとした。
- **実ファイルのベンチマーク**: 手元に実バイナリファイルを用意する案は、リポジトリサイズ増大と環境依存を避けるため却下。

---

## 実装メモ

### 実装順序（実績）

1. **YamlLoadBenchmarks.cs** — 新規作成。3 ベンチマークメソッド
2. **DecodeBenchmarks.cs** — 中規模・大規模ベンチマーク 4 件追加、スケーラブルデータ生成メソッド 2 件追加
3. **パス解決バグ修正** — `FindFormatsDirectory()` ヘルパー追加（既存ベンチマークの潜在バグも修正）
4. **ベンチマーク実行** — 全 18 ベンチマーク完走確認
5. **ベースライン記録** — `docs/benchmark-baseline.md` に結果記録（日本語）

### 実装中の設計変更

#### 1. フォーマットディレクトリのパス解決方式変更

**変更**: 相対パス `"..", "..", "..", "..", ".."` → `FindFormatsDirectory()` による動的探索に変更。

**理由**: BenchmarkDotNet のサブプロセスは `AppContext.BaseDirectory` が `bin/Release/net10.0/{guid}/bin/Release/net10.0/` となり、固定段数の `..` では `formats` ディレクトリに到達しない。既存の `DecodeMinimalPng` ベンチマークも同様に壊れており（`_pngFormat is null` ガードで `null` 返却 → 0.1ns / 0 alloc という偽の結果）、潜在バグだった。

#### 2. `CreateMinimalPng` → `CreateScaledPng` への統合

**変更**: 既存の `CreateMinimalPng()` を `CreateScaledPng(idatCount, idatDataSize)` に統合し、`DecodeMinimalPng` は `CreateScaledPng(1, 12)` を使用するように変更。

**理由**: IDAT チャンク数とデータサイズのパラメータ化により、小・中・大規模データを同一ロジックで生成。コード重複を排除。

### 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `benchmarks/BinAnalyzer.Benchmarks/DecodeBenchmarks.cs` | 拡充 | 中規模・大規模ベンチマーク 4 件追加、`CreateScaledPng`/`CreateScaledPcap`/`FindFormatsDirectory` 追加 |
| `benchmarks/BinAnalyzer.Benchmarks/YamlLoadBenchmarks.cs` | 新規 | YAML ロードベンチマーク 3 件 |
| `docs/benchmark-baseline.md` | 新規 | ベースライン結果記録（日本語） |

### 追加したテスト

本 REQ はベンチマークプロジェクトのため、テストプロジェクトへの変更はなし。既存テスト 677 件（Core 128 + Engine 208 + Dsl 73 + Integration 268）の全通過を確認。

| 検証項目 | 結果 |
|---|---|
| `dotnet build` | 成功（0 警告、0 エラー） |
| `dotnet test` | 全 677 テスト通過 |
| ベンチマーク全 18 件実行 | 全完走、有意な結果取得 |

### 気づき・今後の課題

#### 発見した潜在バグ

1. **既存ベンチマークのパス解決不備**: BenchmarkDotNet サブプロセスの `AppContext.BaseDirectory` が深いサブディレクトリを指すため、既存の `DecodeMinimalPng` は `_pngFormat` が `null` のまま実行され、`0.1ns / 0 alloc` という偽の結果を返していた。`FindFormatsDirectory()` の導入で解消。この問題は `File.Exists` による null ガードで例外にならなかったため、長期間検出されていなかった。

#### スケーリング特性の発見

2. **PNG (repeat: eof) のスケーリング**: 100 チャンク (77 us) → 10,000 チャンク (12.5 ms) = データ量 100 倍に対し約 162 倍。ほぼ線形だが、GC 圧力（Gen2 発生）により若干の超線形性あり。

3. **PCAP (switch + ネスト構造体) のスケーリング**: 100 パケット (198 us) → 10,000 パケット (69.7 ms) = データ量 100 倍に対し約 352 倍。要素あたりの switch 評価・スコープスタック操作・変数バインディングのオーバーヘッドが蓄積。PNG の単純な repeat と比較して、要素あたり約 2.6 倍のコストがかかる。

4. **メモリアロケーション**: PCAP は PNG と比較してデータサイズあたりのアロケーションが著しく大きい（PCAP 中規模: 7KB データに対し 998KB alloc vs PNG 中規模: 11KB データに対し 193KB alloc）。switch 評価によるスコープ・変数辞書の生成が原因と推測される。大規模ファイル向けの最適化を行う場合、switch/ネスト構造体のメモリ効率改善が最も効果的と考えられる。

5. **DecodeLargePcap の分散**: 他のベンチマークと比較して StdDev が大きい（Mean 69.7ms, StdDev 5.7ms, MValue 3.89 = bimodal distribution）。Gen2 GC の発生タイミングが実行ごとに異なるため。大規模デコードではオブジェクト生存期間が長くなり、GC 負荷が不安定になる。

#### YAML ロード性能

6. **フォーマット複雑度とロード時間の相関**: ELF (302 us, 605KB alloc) は PNG (254 us, 475KB alloc) より約 1.2 倍遅い。struct/enum の定義数に比例する傾向。全 30 フォーマットの合計ロード時間は 6.3ms で、CLI 起動時のオーバーヘッドとしては十分小さい。

#### 今後の課題

7. **CI での定期実行**: GitHub Actions でのベンチマーク定期実行と回帰検出（BenchmarkDotNet の `--exporters json` + 前回結果との比較）は未実装。
8. **出力フォーマッターの大規模テスト**: 現在の OutputBenchmarks は小規模な DecodedStruct に対する計測のみ。大規模デコード結果（10,000 要素）に対する出力フォーマッター性能も計測対象になり得る。
9. **デコーダーのメモリ最適化**: PCAP 大規模デコードで約 97MB のアロケーション。switch/ネスト構造体のスコープ・変数辞書のプーリングやオブジェクト再利用が最適化候補。
10. **遅延評価の必要性検討**: 大規模ファイル（1MB 超）のデコード時間が 70ms に達する。100MB 超のファイルでは秒単位になる可能性があり、DSL 拡張での遅延評価（lazy decoding）の必要性を裏付ける結果。
