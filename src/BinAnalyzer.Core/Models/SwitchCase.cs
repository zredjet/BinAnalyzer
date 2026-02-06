using BinAnalyzer.Core.Expressions;

namespace BinAnalyzer.Core.Models;

public sealed record SwitchCase(Expression Condition, string StructRef);
