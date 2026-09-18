using BenchmarkDotNet.Running;
using BinAnalyzer.Benchmarks;

// `probe <file> <format>`: 実ファイル 1 回デコードのメモリ計測（REQ-180）。それ以外は BenchmarkDotNet
if (args.Length >= 3 && args[0] == "probe")
    return DecodeMemoryProbe.Run(args[1], args[2]);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return 0;
