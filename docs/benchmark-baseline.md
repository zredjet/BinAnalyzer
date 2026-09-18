# ベンチマーク ベースライン

計測日: 2026-02-11

## 実行環境

```
BenchmarkDotNet v0.14.0, macOS 26.2 (25C56) [Darwin 25.2.0]
Apple M4 Max, 1 CPU, 16 logical and 16 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 10.0.2 (10.0.225.61305), Arm64 RyuJIT AdvSIMD
```

## デコード ベンチマーク

| メソッド | データサイズ | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| DecodeSimpleStruct | 15 B | 237.0 ns | 2.20 ns | 2.06 ns | 0.2131 | 0.0012 | - | 1.74 KB |
| DecodeMinimalPng | ~60 B | 1,917.9 ns | 8.65 ns | 6.75 ns | 0.9766 | 0.0210 | - | 7.98 KB |
| DecodeMediumPng | ~11 KB | 77,203.5 ns | 1,532.99 ns | 1,765.39 ns | 23.5596 | 4.8218 | - | 192.66 KB |
| DecodeLargePng | ~1.1 MB | 12,513,352.6 ns | 243,603.39 ns | 386,380.82 ns | 2609.3750 | 1250.0000 | 546.8750 | 18701.22 KB |
| DecodeMediumPcap | ~7 KB | 198,167.6 ns | 3,882.50 ns | 3,813.13 ns | 122.0703 | 53.2227 | - | 997.8 KB |
| DecodeLargePcap | ~700 KB | 69,679,877.4 ns | 1,929,495.83 ns | 5,689,162.39 ns | 13125.0000 | 5250.0000 | 1750.0000 | 99482.25 KB |

### スケーリング分析

- **PNG (repeat: eof)**: 100チャンク (77 us) -> 10,000チャンク (12.5 ms) = データ量100倍に対し約162倍。ほぼ線形。
- **PCAP (switch + ネスト構造体)**: 100パケット (198 us) -> 10,000パケット (69.7 ms) = データ量100倍に対し約352倍。要素ごとのswitch評価オーバーヘッドあり。
- **PCAP vs PNG（要素あたり）**: PCAP ~2 us/パケット vs PNG ~0.77 us/チャンク。switch + ネスト構造体のデコードは約2.6倍重い。

## YAMLロード ベンチマーク

| メソッド | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| LoadAllFormats (30ファイル) | 6,298.0 us | 72.25 us | 60.33 us | 1328.1250 | 664.0625 | 10887.09 KB |
| LoadSingleFormat_Png | 254.1 us | 2.05 us | 1.71 us | 56.6406 | 9.7656 | 475.21 KB |
| LoadSingleFormat_Elf | 302.4 us | 1.00 us | 0.83 us | 73.7305 | 14.6484 | 605.19 KB |

### 備考

- フォーマットあたりの平均ロード時間: 約210 us（6.3 ms / 30フォーマット）。
- ELF（約302 us）はPNG（約254 us）より複雑（構造体・列挙型の定義が大きいため）。

## 式 ベンチマーク

| メソッド | Mean | Error | StdDev | Gen0 | Allocated |
|---|---:|---:|---:|---:|---:|
| ParseSimpleExpression | 46.252 ns | 0.3545 ns | 0.3316 ns | 0.0334 | 280 B |
| ParseComplexExpression | 141.328 ns | 0.9423 ns | 0.8353 ns | 0.1090 | 912 B |
| EvaluateSimpleExpression | 5.954 ns | 0.0271 ns | 0.0254 ns | - | - |
| EvaluateComplexExpression | 37.904 ns | 0.2842 ns | 0.2519 ns | 0.0143 | 120 B |

## 出力 ベンチマーク

| メソッド | Mean | Error | StdDev | Gen0 | Gen1 | Allocated |
|---|---:|---:|---:|---:|---:|---:|
| FormatTree | 520.4 ns | 3.75 ns | 3.13 ns | 0.4139 | 0.0019 | 3.38 KB |
| FormatJson | 1,786.3 ns | 22.51 ns | 21.05 ns | 1.5392 | 0.0381 | 12.59 KB |
| FormatHexdump | 1,456.4 ns | 6.46 ns | 5.73 ns | 1.4095 | 0.0114 | 11.52 KB |
| FormatHtml | 2,321.8 ns | 9.14 ns | 8.10 ns | 6.3477 | 0.6332 | 51.95 KB |
| FormatMap | 721.3 ns | 2.89 ns | 2.41 ns | 0.7477 | 0.0076 | 6.11 KB |

## GUI 配布物（REQ-176）

計測日: 2026-09-18 / macOS 15（Apple Silicon）/ .NET SDK 10.0.302 / Photino.Native 4.0.22

publish オプション: `-c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`

| 項目 | 値 | 備考 |
|---|---:|---|
| 単一ファイルサイズ（osx-arm64, self-contained） | 93 MB | Razor コンポーネントを含むため `Microsoft.AspNetCore.App` 共有フレームワークが同梱される。参考: フレームワーク依存では 17 MB |
| 初回起動（ネイティブ展開あり）`-o tree` | 1.12 s | `Photino.Native.dylib` と `libonigwrap.dylib`（計 約 1 MB）を `DOTNET_BUNDLE_EXTRACT_BASE_DIR` へ展開する時間を含む |
| 2 回目以降 `-o tree` | 0.08 s | |
| `-o gui` 窓表示までの overhead（初回展開あり） | 約 0.9 s | `BINANALYZER_GUI_AUTOCLOSE=3000` での総時間 3.94 s から自動終了待ち 3 s を引いた値 |
| `-o gui` 窓表示までの overhead（2 回目以降） | 約 0.45 s | 同上、総時間 3.44 s |

### 各 RID の配布物サイズ（release.yml dry run, 2026-09-18）

| RID | 単一ファイル | 備考 |
|---|---:|---|
| win-x64 | 87 MB | `binanalyzer.exe`（WebView2Loader.dll を内包） |
| linux-x64 | 87 MB | |
| osx-arm64 | 93 MB | |
| osx-x64 | 86 MB | |

### 備考

- 各 OS のサイズは release ワークフローの `Package` ステップのログ（`ls -l`）で確認できる。
- サイズ削減（trimming、`InvariantGlobalization`、ASP.NET Core 共有フレームワークの部分参照）は REQ-181 で扱う。

## Presentation 層・GUI の大容量ファイル性能（REQ-177）

計測日: 2026-09-18 / macOS 26.5.1 / Apple M4 Max / .NET SDK 10.0.302 / BenchmarkDotNet v0.14.0（`--job short`）

`PresentationBenchmarks` は `SyntheticTree`（PCAP 風、1 パケット 38 ノード）で 1 万 / 10 万 / 100 万ノードのツリーを作り、GUI が「開く」ときに走る処理を測る。

```bash
dotnet run -c Release --project benchmarks/BinAnalyzer.Benchmarks -- --filter '*PresentationBenchmarks*' --job short
```

### NodeIndex.Build（索引構築）

| ノード数 | 改善前 Mean | 改善前 Allocated | 改善後 Mean | 改善後 Allocated |
|---:|---:|---:|---:|---:|
| 10,000 | 2.95 ms | 5.56 MB | 0.39 ms | 1.30 MB |
| 100,000 | 34.3 ms | 55.2 MB | 5.1 ms | 15.8 MB |
| 1,000,000 | 380 ms | 513 MB | 86 ms | 143 MB |

改善内容: 全ノードのパス文字列（`chunks[0].data.width`）と祖先 ID 列（`/0/2/3/`）、パス→ID 辞書を保持するのをやめ、親リンクから必要時に組み立てる。名前の正規表現判定（`FieldKindMapper`）を名前ごとにキャッシュ。ノード数を先に数えてリストと辞書を事前確保。残る 143 MB の大半はノード→ID の辞書と 6 本の並列リスト。

### その他（100 万ノード）

| メソッド | 改善前 | 改善後 | 備考 |
|---|---:|---:|---|
| HexRowBuilder.Build（1 行あたり） | 0.48 µs | 0.87 µs | 祖先 ID 列を行生成時に組み立てるぶん増えたが、受入条件（50 µs）の 1/50 |
| StructureMapBuilder.Build | 70 µs | 77 µs | セグメント上限 256 で頭打ち |
| ChecksumSummary.Compute | 3.0 ms | 11.0 ms | チェックサムごとにパスを組み立てる |
| NodeSearch.ByName | 5.2 ms | 5.6 ms | |
| NodeIndex.FindByPathPattern | 43 ms / 251 MB | 171 ms / 336 MB | 全ノードのパスを検索時に生成する（索引時に持たない代償）。検索は明示操作なので許容 |
| BreadcrumbBuilder.Build | 8.7 µs | 76 ns | 配列内インデックスを線形探索から保持値に |
| NodeIndex.PathOf | 0（保持値） | 54 ns | |

### 実ファイル（デスクトップ GUI、`BINANALYZER_GUI_TIMING=1`）

生成した PCAP（`benchmarks` の `CreateScaledPcap` と同じ Ethernet / IPv4 / TCP 構造）を `-o gui` で開き、プロセス起動からの経過時間を記録。Release ビルド、2 回計測の代表値。

| ファイル | ノード数 | デコード | 索引 | 最初の描画（起動から） | 選択→描画 | 最大 RSS |
|---|---:|---:|---:|---:|---:|---:|
| 50 MB（1400 B ペイロード、35,666 パケット） | 1,212,654 | 1.08 s | 0.35 s | 2.53 s | 42 ms | 780 MB |
| 5 MB（ペイロード無し、74,898 パケット） | 2,546,542 | 2.0 s | 1.03 s | 3.9 s | 45 ms | 1.29 GB |

- 受入条件 2（50 MB を 3 秒以内、選択 100 ms 以内）を満たす。起動から「開く」開始までの約 0.5 秒（ランタイム起動 + Photino）を含む
- 索引構築は同じノード数のベンチマーク（86 ms / 100 万）より遅い。デコード直後の冷えた状態（JIT・GC 昇格・キャッシュ局所性）で 1 回だけ走るため。`AggressiveOptimization` で 390 → 350 ms
- メモリと時間はファイルサイズではなくノード数で決まる。5 MB でも小さなパケットが 7.5 万個並ぶと 1.3 GB 使う。デコード結果のノードあたり約 500 B と、デコード自体の時間（約 0.8 µs / ノード）はエンジン側の課題として REQ-180 に切り出した
- 選択の反映（42 ms）は、ヘックス（見えている行のみ再描画）・ツリー（可視行の平坦化と再描画）・インスペクター・構造マップ・ステータスの合計

