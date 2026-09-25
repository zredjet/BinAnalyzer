using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Integration.Tests;

namespace BinAnalyzer.Fuzz.Tests;

/// <summary>リポジトリの <c>formats/*.bdef.yaml</c> 全件と、各フォーマットの有効なサンプルバイナリ。</summary>
public static class FormatCatalog
{
    public static readonly string FormatsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats"));

    private static readonly Lazy<IReadOnlyDictionary<string, FormatDefinition>> Loaded = new(LoadAll);

    /// <summary>ファイル名（<c>png.bdef.yaml</c>）→ IR。imports は <see cref="FileImportResolver"/> で解決済み。</summary>
    public static IReadOnlyDictionary<string, FormatDefinition> All => Loaded.Value;

    public static FormatDefinition Get(string file) => All[file];

    /// <summary>xunit の MemberData 用: 全フォーマットのファイル名。</summary>
    public static IEnumerable<object[]> FormatFiles()
        => Directory.GetFiles(FormatsDir, "*.bdef.yaml").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).Select(n => new object[] { n! });

    /// <summary>
    /// フォーマットごとの有効なサンプル（Integration.Tests の生成器）。切り詰め・変異ファズの種になる。
    /// 生成器の無いフォーマットは <see cref="SamplesMissing"/> で検出する。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Func<byte[]>[]> Samples = new Dictionary<string, Func<byte[]>[]>(StringComparer.Ordinal)
    {
        ["7z.bdef.yaml"] = [SevenZipTestDataGenerator.CreateMinimal7z, () => SevenZipTestDataGenerator.Create7zWithCopyFolder(false), () => SevenZipTestDataGenerator.Create7zWithCopyFolder(true)],
        // CreateMinimalAvi は avih を 52 バイトに切り詰めた「わざと不完全な」サンプルなので、完全なものだけを種にする
        ["avi.bdef.yaml"] = [AviTestDataGenerator.CreateAviWithStreamAndIndex, AviTestDataGenerator.CreateAviWithVideoStreamFormat, AviTestDataGenerator.CreateAviWithAudioStreamFormat, AviTestDataGenerator.CreateOpenDmlAvi],
        ["bmp.bdef.yaml"] = [BmpTestDataGenerator.CreateMinimalBmp, BmpTestDataGenerator.CreateV5HeaderWithPalette],
        ["cbor.bdef.yaml"] = [CborTestDataGenerator.CreateMinimalCbor, CborTestDataGenerator.CreateCborRfc8949Examples],
        ["dns.bdef.yaml"] = [DnsTestDataGenerator.CreateMinimalDns, DnsTestDataGenerator.CreateDnsResponse],
        ["elf.bdef.yaml"] = [ElfTestDataGenerator.CreateMinimalElf64, ElfTestDataGenerator.CreateMinimalElf64BigEndian, ElfTestDataGenerator.CreateElf64WithSections],
        ["fat.bdef.yaml"] = [FatTestDataGenerator.CreateMinimalFat16Image, FatTestDataGenerator.CreateMinimalFat32Image, FatTestDataGenerator.CreateFat12ImageWithLongNames],
        ["flac.bdef.yaml"] = [FlacTestDataGenerator.CreateMinimalFlac, FlacTestDataGenerator.CreateFlacWithCuesheet, FlacTestDataGenerator.CreateFlacWithFrameHeader],
        ["flv.bdef.yaml"] = [FlvTestDataGenerator.CreateMinimalFlv, FlvTestDataGenerator.CreateFlvWithVideoTag, FlvTestDataGenerator.CreateFlvWithMetadata],
        ["gif.bdef.yaml"] = [GifTestDataGenerator.CreateMinimalGif, GifTestDataGenerator.CreateGifWithImageBlock],
        ["gzip.bdef.yaml"] = [GzipTestDataGenerator.CreateMinimalGzip, GzipTestDataGenerator.CreateGzipWithOptionalFields],
        ["heif.bdef.yaml"] = [HeifTestDataGenerator.CreateMinimalHeif],
        ["icc.bdef.yaml"] = [IccTestDataGenerator.CreateMinimalIcc, IccTestDataGenerator.CreateIccWithTags],
        ["ico.bdef.yaml"] = [IcoTestDataGenerator.CreateMinimalIco],
        ["java-class.bdef.yaml"] = [JavaClassTestDataGenerator.CreateMinimalJavaClass, JavaClassTestDataGenerator.CreateClassWithWideConstantsAndCode],
        ["jpeg.bdef.yaml"] = [JpegTestDataGenerator.CreateMinimalJpeg, JpegTestDataGenerator.CreateJpegWithEntropyData],
        ["lz4.bdef.yaml"] = [Lz4TestDataGenerator.CreateMinimalLz4, Lz4TestDataGenerator.CreateSkippableAndChecksummedFrames],
        ["macho.bdef.yaml"] = [MachoTestDataGenerator.CreateMinimalMacho64, MachoTestDataGenerator.CreateUniversalBinary],
        ["midi.bdef.yaml"] = [MidiTestDataGenerator.CreateMinimalMidi, MidiTestDataGenerator.CreateMidiWithRunningStatus],
        ["mkv.bdef.yaml"] = [MkvTestDataGenerator.CreateMinimalWebm],
        ["mp3.bdef.yaml"] = [Mp3TestDataGenerator.CreateMinimalMp3, Mp3TestDataGenerator.CreateMp3WithTags, Mp3TestDataGenerator.CreateMp3WithoutTags],
        ["mp4.bdef.yaml"] = [Mp4TestDataGenerator.CreateMinimalMp4, Mp4TestDataGenerator.CreateFragmentedMp4],
        ["msgpack.bdef.yaml"] = [MsgpackTestDataGenerator.CreateMinimalMsgpack, MsgpackTestDataGenerator.CreateMsgpackDocument],
        ["ogg.bdef.yaml"] = [OggTestDataGenerator.CreateMinimalOgg, OggTestDataGenerator.CreateOpusOgg],
        ["otf.bdef.yaml"] = [OtfTestDataGenerator.CreateMinimalOtf, OtfTestDataGenerator.CreateMinimalTtf, OtfTestDataGenerator.CreateOtfWithCmapAndHhea, OtfTestDataGenerator.CreateTtfWithTables, OtfTestDataGenerator.CreateTtc],
        ["parquet.bdef.yaml"] = [ParquetTestDataGenerator.CreateTwoColumnParquet],
        ["pcap.bdef.yaml"] = [PcapTestDataGenerator.CreateMinimalPcap, PcapTestDataGenerator.CreatePcapWithTcpOptions, PcapTestDataGenerator.CreateEthernetMixPcap, PcapTestDataGenerator.CreateBigEndianNanosecondPcap, PcapTestDataGenerator.CreateRawIpPcap, PcapTestDataGenerator.CreatePcapNg, PcapTestDataGenerator.CreateDnsPcap],
        ["pdf.bdef.yaml"] = [PdfTestDataGenerator.CreateMinimalPdf, PdfTestDataGenerator.CreatePdfWithIncrementalUpdate],
        ["pe.bdef.yaml"] = [PeTestDataGenerator.CreateMinimalPe, PeTestDataGenerator.CreatePe32PlusDll, PeTestDataGenerator.CreateManagedPe32],
        ["png.bdef.yaml"] = [PngTestDataGenerator.CreateMinimalPng, PngTestDataGenerator.CreatePngWithSrgb],
        ["protobuf.bdef.yaml"] = [ProtobufTestDataGenerator.CreateMinimalProtobuf, ProtobufTestDataGenerator.CreateProtobufWithAllWireTypes],
        ["sqlite.bdef.yaml"] = [SqliteTestDataGenerator.CreateMinimalSqlite, SqliteTestDataGenerator.CreateSqliteWithCell, SqliteTestDataGenerator.CreateSqliteWithRecords],
        ["tar.bdef.yaml"] = [TarTestDataGenerator.CreateMinimalTar, TarTestDataGenerator.CreatePaxAndGnuTar],
        ["tiff.bdef.yaml"] = [TiffTestDataGenerator.CreateMinimalTiff, TiffTestDataGenerator.CreateBigEndianTiff, TiffTestDataGenerator.CreateTiffWithRationalTag],
        ["wasm.bdef.yaml"] = [WasmTestDataGenerator.CreateMinimalWasm, WasmTestDataGenerator.CreateWasmWithExportSection, WasmTestDataGenerator.CreateWasm3Module],
        ["wav.bdef.yaml"] = [WavTestDataGenerator.CreateMinimalWav, WavTestDataGenerator.CreateWavWithListInfo, WavTestDataGenerator.CreatePcmWavWith18ByteFmt, WavTestDataGenerator.CreateRf64ExtensibleWav],
        ["webp.bdef.yaml"] = [WebpTestDataGenerator.CreateMinimalWebp],
        ["x509.bdef.yaml"] = [X509TestDataGenerator.CreateMinimalCertificate, X509TestDataGenerator.CreateV1Certificate, X509TestDataGenerator.CreateCertificateWithExtensions],
        ["xz.bdef.yaml"] = [XzTestDataGenerator.CreateMinimalXz, XzTestDataGenerator.CreateTwoBlockXz],
        ["zip.bdef.yaml"] = [ZipTestDataGenerator.CreateMinimalZip, ZipTestDataGenerator.CreateStreamedDeflateZip, ZipTestDataGenerator.CreateZip64WithUnsignedDataDescriptor],
    };

    /// <summary>xunit の MemberData 用: (フォーマットファイル, サンプル番号)。</summary>
    public static IEnumerable<object[]> SampleCases()
        => Samples.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .SelectMany(kv => kv.Value.Select((_, i) => new object[] { kv.Key, i }));

    public static IEnumerable<string> SamplesMissing()
        => FormatFiles().Select(o => (string)o[0]).Where(f => !Samples.ContainsKey(f));

    private static IReadOnlyDictionary<string, FormatDefinition> LoadAll()
    {
        var loader = new YamlFormatLoader();
        var result = new Dictionary<string, FormatDefinition>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(FormatsDir, "*.bdef.yaml"))
            result[Path.GetFileName(path)] = loader.Load(path);
        return result;
    }
}
