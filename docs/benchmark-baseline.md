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
