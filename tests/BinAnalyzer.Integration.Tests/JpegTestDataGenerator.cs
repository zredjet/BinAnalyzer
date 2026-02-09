using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class JpegTestDataGenerator
{
    /// <summary>
    /// 最小JPEGファイル: SOI(2B) + APP0(2+2+14=18B) + SOF0(2+2+9=13B) + SOS(2+2+1+0=5B) + EOI(2B) = 40バイト
    /// JFIF 1.01, 1x1 1-component baseline DCT
    /// </summary>
    public static byte[] CreateMinimalJpeg()
    {
        using var ms = new MemoryStream();

        // SOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);

        // === APP0 segment (JFIF) ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE0); // APP0 marker_type

        // length: 16 (includes length field itself)
        WriteBE16(ms, 16);

        // identifier: "JFIF\0" (5 bytes)
        ms.Write(Encoding.ASCII.GetBytes("JFIF\0"));

        // version_major: 1
        ms.WriteByte(1);
        // version_minor: 1
        ms.WriteByte(1);
        // density_units: 0 (no_units)
        ms.WriteByte(0);
        // x_density: 1
        WriteBE16(ms, 1);
        // y_density: 1
        WriteBE16(ms, 1);
        // thumbnail_width: 0
        ms.WriteByte(0);
        // thumbnail_height: 0
        ms.WriteByte(0);

        // === SOF0 segment ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xC0); // SOF0

        // length: 11
        WriteBE16(ms, 11);

        // sof0_body:
        // precision: 8
        ms.WriteByte(8);
        // height: 1
        WriteBE16(ms, 1);
        // width: 1
        WriteBE16(ms, 1);
        // num_components: 1
        ms.WriteByte(1);
        // component: id=1, sampling=0x11, qt_id=0
        ms.WriteByte(1);
        ms.WriteByte(0x11);
        ms.WriteByte(0);

        // === SOS segment ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xDA); // SOS

        // length: 3 (minimal)
        WriteBE16(ms, 3);

        // sos_header: 1 byte
        ms.WriteByte(0x00);

        // compressed_data: 0 bytes (empty, remaining=0 with EOI right after)
        // Actually SOS consumes remaining including EOI, so we skip it

        // === EOI marker ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9); // EOI

        return ms.ToArray();
    }

    /// <summary>
    /// SOS後にエントロピーデータ（5バイト）+ EOI マーカーを含むJPEG。
    /// until_marker によりエントロピーデータとEOIが分離されることを検証するためのテストデータ。
    /// </summary>
    public static byte[] CreateJpegWithEntropyData()
    {
        using var ms = new MemoryStream();

        // SOI
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);

        // === APP0 segment (JFIF) ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE0);
        WriteBE16(ms, 16);
        ms.Write(Encoding.ASCII.GetBytes("JFIF\0"));
        ms.WriteByte(1);
        ms.WriteByte(1);
        ms.WriteByte(0);
        WriteBE16(ms, 1);
        WriteBE16(ms, 1);
        ms.WriteByte(0);
        ms.WriteByte(0);

        // === SOF0 segment ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xC0);
        WriteBE16(ms, 11);
        ms.WriteByte(8);
        WriteBE16(ms, 1);
        WriteBE16(ms, 1);
        ms.WriteByte(1);
        ms.WriteByte(1);
        ms.WriteByte(0x11);
        ms.WriteByte(0);

        // === SOS segment ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xDA);
        WriteBE16(ms, 3);
        ms.WriteByte(0x00); // sos_header: 1 byte

        // Entropy-coded data: 5 bytes
        ms.Write(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE });

        // === EOI marker ===
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD9);

        return ms.ToArray();
    }

    private static void WriteBE16(MemoryStream ms, ushort value)
    {
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value & 0xFF));
    }
}
