using BinAnalyzer.Core.Diff;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Core.Tests.Diff;

/// <summary>REQ-156: 同じオフセット同士のバイト比較。</summary>
public class ByteDiffTests
{
    private static readonly byte[] A = [0x00, 0x01, 0x02, 0x03, 0x04];
    private static readonly byte[] B = [0x00, 0xFF, 0x02, 0x03, 0x04, 0x05, 0x06];

    [Fact]
    public void Differs_ComparesSameOffset_AndTreatsMissingSideAsDifferent()
    {
        ByteDiff.Differs(A, B, 0).Should().BeFalse();
        ByteDiff.Differs(A, B, 1).Should().BeTrue();
        ByteDiff.Differs(A, B, 5).Should().BeTrue("短い側の末尾を越える");
        ByteDiff.Differs(A, B, 7).Should().BeFalse("どちらにも無い");
    }

    [Fact]
    public void RowMask_SetsBitsOfDifferingBytes()
    {
        ByteDiff.RowMask(A, B, 0, 8).Should().Be(0b0110_0010u);
    }

    [Fact]
    public void RowDiffers_DetectsInnerAndLengthDifferences()
    {
        ByteDiff.RowDiffers(A, B, 0, 1).Should().BeFalse();
        ByteDiff.RowDiffers(A, B, 1, 1).Should().BeTrue();
        ByteDiff.RowDiffers(A, B, 2, 3).Should().BeFalse();
        ByteDiff.RowDiffers(A, B, 4, 2).Should().BeTrue("5 バイト目は file2 にだけある");
        ByteDiff.RowDiffers(A, A, 0, 16).Should().BeFalse();
        ByteDiff.RowDiffers(A, B, 16, 16).Should().BeFalse("どちらの末尾も越えている");
    }

    [Fact]
    public void CountDifferences_CountsMismatchesAndLengthDelta()
    {
        ByteDiff.CountDifferences(A, B).Should().Be(1 + 2);
        ByteDiff.CountDifferences(A, A).Should().Be(0);
        ByteDiff.CountDifferences([], B).Should().Be(B.Length);
    }

    [Fact]
    public void CountDifferences_LargeMostlyEqualBuffers()
    {
        var left = new byte[1 << 20];
        var right = (byte[])left.Clone();
        right[123] = 1;
        right[left.Length - 1] = 2;

        ByteDiff.CountDifferences(left, right).Should().Be(2);
    }
}
