using BenchmarkDotNet.Attributes;
using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Benchmarks;

[MemoryDiagnoser]
public class ExpressionBenchmarks
{
    private Expression _simpleExpr = null!;
    private Expression _complexExpr = null!;
    private DecodeContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        _simpleExpr = ExpressionParser.Parse("{length}");
        _complexExpr = ExpressionParser.Parse("{length - 4 + offset * 2}");
        _context = new DecodeContext(new byte[16], Endianness.Big);
        _context.SetVariable("length", 100L);
        _context.SetVariable("offset", 8L);
    }

    [Benchmark]
    public Expression ParseSimpleExpression()
    {
        return ExpressionParser.Parse("{length}");
    }

    [Benchmark]
    public Expression ParseComplexExpression()
    {
        return ExpressionParser.Parse("{length - 4 + offset * 2}");
    }

    [Benchmark]
    public long EvaluateSimpleExpression()
    {
        return ExpressionEvaluator.EvaluateAsLong(_simpleExpr, _context);
    }

    [Benchmark]
    public long EvaluateComplexExpression()
    {
        return ExpressionEvaluator.EvaluateAsLong(_complexExpr, _context);
    }
}
