using System.Buffers.Binary;
using System.Text;

namespace BinAnalyzer.Integration.Tests;

public static class JavaClassTestDataGenerator
{
    /// <summary>
    /// 最小Java Classファイル: magic(4B) + minor(2B) + major(2B) + cp_count(2B) +
    /// 2 cp_entries(1+2+3=6B, 1+2+1=4B) + access_flags(2B) + this_class(2B) + super_class(2B) +
    /// interfaces_count(2B) + fields_count(2B) + methods_count(2B) + attributes_count(2B) = 34バイト
    /// Java 17 (major=61), constant_pool_count=3, cp[1]=Class(name_index=2), cp[2]=Utf8("Test")
    /// </summary>
    public static byte[] CreateMinimalJavaClass()
    {
        var data = new byte[34];
        var span = data.AsSpan();
        var pos = 0;

        // magic: 0xCAFEBABE
        data[0] = 0xCA;
        data[1] = 0xFE;
        data[2] = 0xBA;
        data[3] = 0xBE;
        pos = 4;

        // minor_version: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // major_version: 61 (Java 17)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 61); pos += 2;

        // constant_pool_count: 3 (entries are 1-indexed, so 2 entries)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 3); pos += 2;

        // === cp_entry[1]: CONSTANT_Class (tag=7) ===
        data[pos] = 7; pos += 1; // tag
        // info (cp_class): name_index = 2
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 2); pos += 2;

        // === cp_entry[2]: CONSTANT_Utf8 (tag=1) ===
        data[pos] = 1; pos += 1; // tag
        // info (cp_utf8): length = 4
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 4); pos += 2;
        // value: "Test" (4 bytes)
        data[pos] = 0x54; pos += 1; // 'T'
        data[pos] = 0x65; pos += 1; // 'e'
        data[pos] = 0x73; pos += 1; // 's'
        data[pos] = 0x74; pos += 1; // 't'

        // access_flags: 0x0021 (ACC_PUBLIC | ACC_SUPER)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0x0021); pos += 2;

        // this_class: 1 (cp index of CONSTANT_Class)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 1); pos += 2;

        // super_class: 0 (java.lang.Object, represented as 0 for minimal)
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // interfaces_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // fields_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // methods_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0); pos += 2;

        // attributes_count: 0
        BinaryPrimitives.WriteUInt16BigEndian(span[pos..], 0);

        return data;
    }

    /// <summary>
    /// public class Gen { public static final long BIG = 1234567890123L; public static final double PI = 3.5; public static void main(String[] a) { return; } }
    /// 相当のクラスファイル（Java 8 = 52、REQ-188）。Long / Double の定数が 2 スロットずつ使い、main に Code と LineNumberTable、クラスに SourceFile を持つ。
    /// </summary>
    public static byte[] CreateClassWithWideConstantsAndCode()
    {
        var ms = new MemoryStream();
        void U1(int v) => ms.WriteByte((byte)v);
        void U2(int v) { U1(v >> 8); U1(v); }
        void U4(long v) { U2((int)(v >> 16) & 0xFFFF); U2((int)v & 0xFFFF); }
        void Utf8(string v) { U1(1); var b = Encoding.UTF8.GetBytes(v); U2(b.Length); ms.Write(b); }

        U4(0xCAFEBABE); U2(0); U2(52);
        U2(20);                                              // constant_pool_count（スロット 1〜19）
        Utf8("Gen");                                         // #1
        U1(7); U2(1);                                        // #2 Class Gen
        Utf8("java/lang/Object");                            // #3
        U1(7); U2(3);                                        // #4 Class java/lang/Object
        Utf8("BIG");                                         // #5
        Utf8("J");                                           // #6
        Utf8("ConstantValue");                               // #7
        var wide = new byte[8];
        U1(5); BinaryPrimitives.WriteInt64BigEndian(wide, 1234567890123L); ms.Write(wide);  // #8 Long（#9 は使えない）
        U1(6); BinaryPrimitives.WriteDoubleBigEndian(wide, 3.5); ms.Write(wide);            // #10 Double（#11 は使えない）
        Utf8("main");                                        // #12
        Utf8("([Ljava/lang/String;)V");                      // #13
        Utf8("Code");                                        // #14
        Utf8("LineNumberTable");                             // #15
        Utf8("SourceFile");                                  // #16
        Utf8("Gen.java");                                    // #17
        Utf8("PI");                                          // #18
        Utf8("D");                                           // #19

        U2(0x0021); U2(2); U2(4); U2(0);                     // public super、this = #2、super = #4、インタフェースなし
        U2(2);                                               // フィールド 2 個
        foreach (var (name, descriptor, value) in new[] { (5, 6, 8), (18, 19, 10) })
        {
            U2(0x0019); U2(name); U2(descriptor); U2(1);
            U2(7); U4(2); U2(value);                         // ConstantValue
        }
        U2(1);                                               // メソッド 1 個
        U2(0x0009); U2(12); U2(13); U2(1);
        U2(14); U4(2 + 2 + 4 + 1 + 2 + 2 + 12);              // Code（LineNumberTable の 12 バイトを含む）
        U2(0); U2(1); U4(1); U1(0xB1);                       // max_stack、max_locals、code = return
        U2(0);                                               // 例外表なし
        U2(1); U2(15); U4(6); U2(1); U2(0); U2(3);           // LineNumberTable: pc 0 → 3 行目
        U2(1); U2(16); U4(2); U2(17);                        // SourceFile = Gen.java
        return ms.ToArray();
    }
}
