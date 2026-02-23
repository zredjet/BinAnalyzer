using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

public sealed class ChecksumRange
{
    public required Expression OffsetExpression { get; init; }
    public required Expression SizeExpression { get; init; }
}
