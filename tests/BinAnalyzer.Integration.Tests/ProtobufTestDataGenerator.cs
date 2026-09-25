using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class ProtobufTestDataGenerator
{
    /// <summary>
    /// 最小Protobufメッセージ（3フィールド、17バイト）:
    /// Field 1 (varint):           tag=0x08 (field=1, wt=0), value=150 (0x96 0x01)
    /// Field 2 (length-delimited): tag=0x12 (field=2, wt=2), length=5, data="Hello"
    /// Field 3 (fixed32):          tag=0x1D (field=3, wt=5), value=42 (LE)
    /// </summary>
    public static byte[] CreateMinimalProtobuf()
    {
        var ms = new MemoryStream();

        // Field 1: varint (field_number=1, wire_type=0)
        // tag = (1 << 3) | 0 = 0x08
        ms.WriteByte(0x08);
        // value = 150 → LEB128: 0x96 0x01
        ms.WriteByte(0x96);
        ms.WriteByte(0x01);

        // Field 2: length-delimited (field_number=2, wire_type=2)
        // tag = (2 << 3) | 2 = 0x12
        ms.WriteByte(0x12);
        // length = 5
        ms.WriteByte(0x05);
        // data = "Hello"
        ms.Write(Encoding.ASCII.GetBytes("Hello"));

        // Field 3: fixed32 (field_number=3, wire_type=5)
        // tag = (3 << 3) | 5 = 0x1D
        ms.WriteByte(0x1D);
        // value = 42 (little-endian)
        ms.WriteByte(0x2A); // 42
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);
        ms.WriteByte(0x00);

        return ms.ToArray();
    }

    /// <summary>
    /// Python の protobuf 4.25 で proto2 のメッセージを SerializeToString した 78 バイト（REQ-188）。
    /// 1: int32 = -5（10 バイトの varint）、2: sint32 = -3（ジグザグ）、3: string = "こんにちは"、4: bytes = 00 FF、5: 入れ子のメッセージ { 1: 42 }、
    /// 6: double = 3.5、7: float = -1.25、8: fixed32 = 0xDEADBEEF、9: bool = true、10: グループ { 11: "g" }、12: パックされた int32 [1, 150, 3]、300: uint64 = 2^40。
    /// </summary>
    public static byte[] CreateProtobufWithAllWireTypes() => Convert.FromHexString(
        "08fbffffffffffffffff0110051a0fe38193e38293e381abe381a1e381af220200ff2a02082a310000000000000c403d0000a0bf45efbeadde4801535a016754620401960103e012808080808020");
}
