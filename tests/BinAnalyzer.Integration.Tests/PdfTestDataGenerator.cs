using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class PdfTestDataGenerator
{
    /// <summary>
    /// 最小の PDF（REQ-188 で有効なファイルに変更）: 空のページ 1 枚（カタログ・ページの木・ページ）と相互参照表・トレーラ。
    /// 以前はヘッダ + コメントだけの 20 バイトで、PDF の構造を持たなかった
    /// </summary>
    public static byte[] CreateMinimalPdf()
    {
        var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        var offsets = new long[4];
        W("%PDF-1.4\n"); ms.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });
        offsets[1] = ms.Position; W("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        offsets[2] = ms.Position; W("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        offsets[3] = ms.Position; W("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << >> >>\nendobj\n");
        var xref = ms.Position;
        W("xref\n0 4\n0000000000 65535 f \n");
        for (var i = 1; i <= 3; i++) W($"{offsets[i]:D10} 00000 n \n");
        W($"trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return ms.ToArray();
    }

    /// <summary>
    /// 増分更新を含む PDF（REQ-188）。1 つ目の改訂はカタログ・ページの木・ページ・FlateDecode の内容ストリーム・フォントの 5 オブジェクトと相互参照表、
    /// 2 つ目の改訂はフォント（5 番）の置き換えと文書情報（6 番）の追加、/Prev 付きのトレーラ。qpdf --check が通ることを確かめた。
    /// </summary>
    public static byte[] CreatePdfWithIncrementalUpdate()
    {
        var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.Latin1.GetBytes(s));
        var offsets = new Dictionary<int, long>();
        void Obj(int n, string body) { offsets[n] = ms.Position; W($"{n} 0 obj\n{body}\nendobj\n"); }
        W("%PDF-1.7\n"); ms.Write(new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>");
        var content = Deflate("BT /F1 24 Tf 72 720 Td (Hello PDF) Tj ET"u8.ToArray());
        offsets[4] = ms.Position;
        W($"4 0 obj\n<< /Length {content.Length} /Filter /FlateDecode >>\nstream\n"); ms.Write(content); W("\nendstream\nendobj\n");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var xref1 = ms.Position;
        W("xref\n0 6\n0000000000 65535 f \n");
        for (var i = 1; i <= 5; i++) W($"{offsets[i]:D10} 00000 n \n");
        W($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref1}\n%%EOF\n");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        Obj(6, "<< /Title (Updated) >>");
        var xref2 = ms.Position;
        W($"xref\n0 1\n0000000000 65535 f \n5 2\n{offsets[5]:D10} 00000 n \n{offsets[6]:D10} 00000 n \n");
        W($"trailer\n<< /Size 7 /Root 1 0 R /Info 6 0 R /Prev {xref1} >>\nstartxref\n{xref2}\n%%EOF\n");
        return ms.ToArray();
    }

    private static byte[] Deflate(byte[] data)
    {
        var output = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            z.Write(data);
        return output.ToArray();
    }
}
