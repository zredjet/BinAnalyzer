using System.Buffers.Binary;

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
}
