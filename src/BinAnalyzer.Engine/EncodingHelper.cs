using System.Text;

namespace BinAnalyzer.Engine;

internal static class EncodingHelper
{
    private static Encoding? _shiftJis;

    public static Encoding ShiftJis => _shiftJis ??= InitShiftJis();

    private static Encoding InitShiftJis()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("shift_jis");
    }
}
