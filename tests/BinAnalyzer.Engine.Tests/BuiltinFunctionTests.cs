using BinAnalyzer.Core.Expressions;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class BuiltinFunctionTests
{
    private static DecodeContext CreateContextWithArray(string name, List<object> values)
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable(name, values);
        return ctx;
    }

    // --- len ---

    [Fact]
    public void Len_ReturnsArrayLength()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 10L, 20L, 30L });
        var expr = ExpressionParser.Parse("{len(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(3);
    }

    [Fact]
    public void Len_EmptyArray_ReturnsZero()
    {
        var ctx = CreateContextWithArray("arr", new List<object>());
        var expr = ExpressionParser.Parse("{len(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void Len_NonArray_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", 42L);
        var expr = ExpressionParser.Parse("{len(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not an array*");
    }

    // --- count (alias for len) ---

    [Fact]
    public void Count_ReturnsArrayLength()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 10L, 20L, 30L, 40L });
        var expr = ExpressionParser.Parse("{count(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(4);
    }

    // --- min ---

    [Fact]
    public void Min_ReturnsMinValue()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 30L, 10L, 20L });
        var expr = ExpressionParser.Parse("{min(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(10);
    }

    [Fact]
    public void Min_SingleElement_ReturnsThatElement()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 42L });
        var expr = ExpressionParser.Parse("{min(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(42);
    }

    [Fact]
    public void Min_EmptyArray_Throws()
    {
        var ctx = CreateContextWithArray("arr", new List<object>());
        var expr = ExpressionParser.Parse("{min(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty array*");
    }

    [Fact]
    public void Min_NonArray_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", 42L);
        var expr = ExpressionParser.Parse("{min(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not an array*");
    }

    // --- max ---

    [Fact]
    public void Max_ReturnsMaxValue()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 10L, 50L, 30L });
        var expr = ExpressionParser.Parse("{max(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(50);
    }

    [Fact]
    public void Max_SingleElement_ReturnsThatElement()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 99L });
        var expr = ExpressionParser.Parse("{max(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(99);
    }

    [Fact]
    public void Max_EmptyArray_Throws()
    {
        var ctx = CreateContextWithArray("arr", new List<object>());
        var expr = ExpressionParser.Parse("{max(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*empty array*");
    }

    // --- sum ---

    [Fact]
    public void Sum_ReturnsSumOfValues()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 10L, 20L, 30L });
        var expr = ExpressionParser.Parse("{sum(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(60);
    }

    [Fact]
    public void Sum_EmptyArray_ReturnsZero()
    {
        var ctx = CreateContextWithArray("arr", new List<object>());
        var expr = ExpressionParser.Parse("{sum(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(0);
    }

    [Fact]
    public void Sum_SingleElement_ReturnsThatElement()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 77L });
        var expr = ExpressionParser.Parse("{sum(arr)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(77);
    }

    [Fact]
    public void Sum_NonArray_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("arr", 42L);
        var expr = ExpressionParser.Parse("{sum(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not an array*");
    }

    // --- substr ---

    [Fact]
    public void Substr_ReturnsSubstring()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("magic", "HELLO WORLD");
        var expr = ExpressionParser.Parse("{substr(magic, 0, 4)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("HELL");
    }

    [Fact]
    public void Substr_StartBeyondLength_ReturnsEmpty()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("s", "ABC");
        var expr = ExpressionParser.Parse("{substr(s, 10, 2)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("");
    }

    [Fact]
    public void Substr_LengthExceedsRemaining_ClampsToEnd()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("s", "ABCDE");
        var expr = ExpressionParser.Parse("{substr(s, 3, 100)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("DE");
    }

    [Fact]
    public void Substr_NonString_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("n", 42L);
        var expr = ExpressionParser.Parse("{substr(n, 0, 2)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a string*");
    }

    // --- concat ---

    [Fact]
    public void Concat_TwoStrings()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("a", "Hello");
        ctx.SetVariable("b", " World");
        var expr = ExpressionParser.Parse("{concat(a, b)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("Hello World");
    }

    [Fact]
    public void Concat_ThreeOrMoreArgs()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("a", "A");
        ctx.SetVariable("b", "B");
        ctx.SetVariable("c", "C");
        var expr = ExpressionParser.Parse("{concat(a, b, c)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("ABC");
    }

    [Fact]
    public void Concat_NumberToString()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("prefix", "v");
        ctx.SetVariable("ver", 2L);
        var expr = ExpressionParser.Parse("{concat(prefix, ver)}");
        ExpressionEvaluator.EvaluateAsString(expr, ctx).Should().Be("v2");
    }

    // --- contains ---

    [Fact]
    public void Contains_Found_ReturnsTrue()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("s", "Hello World");
        var expr = ExpressionParser.Parse("{contains(s, 'World')}");
        ExpressionEvaluator.EvaluateAsBool(expr, ctx).Should().BeTrue();
    }

    [Fact]
    public void Contains_NotFound_ReturnsFalse()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("s", "Hello World");
        var expr = ExpressionParser.Parse("{contains(s, 'xyz')}");
        ExpressionEvaluator.EvaluateAsBool(expr, ctx).Should().BeFalse();
    }

    [Fact]
    public void Contains_NonString_Throws()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("n", 42L);
        var expr = ExpressionParser.Parse("{contains(n, 'x')}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a string*");
    }

    // --- non-numeric array errors ---

    [Fact]
    public void Min_StringArray_Throws()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { "hello", "world" });
        var expr = ExpressionParser.Parse("{min(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot convert*");
    }

    [Fact]
    public void Max_StringArray_Throws()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { "hello", "world" });
        var expr = ExpressionParser.Parse("{max(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot convert*");
    }

    [Fact]
    public void Sum_StringArray_Throws()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { "hello", "world" });
        var expr = ExpressionParser.Parse("{sum(arr)}");
        var act = () => ExpressionEvaluator.Evaluate(expr, ctx);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot convert*");
    }

    // --- arithmetic expression ---

    [Fact]
    public void Len_InArithmeticExpression_Works()
    {
        var ctx = CreateContextWithArray("arr", new List<object> { 10L, 20L, 30L });
        var expr = ExpressionParser.Parse("{len(arr) - 1}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(2);
    }

    // --- existing functions still work ---

    [Fact]
    public void ExistingFunctions_StillWork()
    {
        var ctx = new DecodeContext(new byte[] { 0x00 }, Endianness.Big);
        ctx.SetVariable("size", "644");
        var expr = ExpressionParser.Parse("{parse_int(size, 8)}");
        ExpressionEvaluator.EvaluateAsLong(expr, ctx).Should().Be(420);
    }
}
