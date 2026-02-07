using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class PdfTestDataGenerator
{
    /// <summary>
    /// 最小PDFバイナリ: version(8B) + binary_comment(5B) + body(7B) = 20バイト
    /// </summary>
    public static byte[] CreateMinimalPdf()
    {
        using var ms = new MemoryStream();

        // version: 8 bytes ASCII "%PDF-1.4"
        ms.Write(Encoding.ASCII.GetBytes("%PDF-1.4"));

        // binary_comment: 5 bytes (% + 4 high bytes)
        ms.WriteByte(0x25); // '%'
        ms.WriteByte(0xE2);
        ms.WriteByte(0xE3);
        ms.WriteByte(0xCF);
        ms.WriteByte(0xD3);

        // body: remaining bytes (some dummy content)
        ms.Write(Encoding.ASCII.GetBytes("%%EOF\n\0"));

        return ms.ToArray();
    }
}
