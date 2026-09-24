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
        ["7z.bdef.yaml"] = [SevenZipTestDataGenerator.CreateMinimal7z],
        // CreateMinimalAvi は avih を 52 バイトに切り詰めた「わざと不完全な」サンプルなので、完全なものだけを種にする
        ["avi.bdef.yaml"] = [AviTestDataGenerator.CreateAviWithStreamAndIndex, AviTestDataGenerator.CreateAviWithVideoStreamFormat, AviTestDataGenerator.CreateAviWithAudioStreamFormat],
        ["bmp.bdef.yaml"] = [BmpTestDataGenerator.CreateMinimalBmp],
        ["cbor.bdef.yaml"] = [CborTestDataGenerator.CreateMinimalCbor],
        ["dns.bdef.yaml"] = [DnsTestDataGenerator.CreateMinimalDns],
        ["elf.bdef.yaml"] = [ElfTestDataGenerator.CreateMinimalElf64, ElfTestDataGenerator.CreateMinimalElf64BigEndian],
        ["fat.bdef.yaml"] = [FatTestDataGenerator.CreateMinimalFat16Image, FatTestDataGenerator.CreateMinimalFat32Image],
        ["flac.bdef.yaml"] = [FlacTestDataGenerator.CreateMinimalFlac, FlacTestDataGenerator.CreateFlacWithCuesheet],
        ["flv.bdef.yaml"] = [FlvTestDataGenerator.CreateMinimalFlv, FlvTestDataGenerator.CreateFlvWithVideoTag],
        ["gif.bdef.yaml"] = [GifTestDataGenerator.CreateMinimalGif, GifTestDataGenerator.CreateGifWithImageBlock],
        ["gzip.bdef.yaml"] = [GzipTestDataGenerator.CreateMinimalGzip],
        ["heif.bdef.yaml"] = [HeifTestDataGenerator.CreateMinimalHeif],
        ["icc.bdef.yaml"] = [IccTestDataGenerator.CreateMinimalIcc, IccTestDataGenerator.CreateIccWithTags],
        ["ico.bdef.yaml"] = [IcoTestDataGenerator.CreateMinimalIco],
        ["java-class.bdef.yaml"] = [JavaClassTestDataGenerator.CreateMinimalJavaClass],
        ["jpeg.bdef.yaml"] = [JpegTestDataGenerator.CreateMinimalJpeg, JpegTestDataGenerator.CreateJpegWithEntropyData],
        ["lz4.bdef.yaml"] = [Lz4TestDataGenerator.CreateMinimalLz4],
        ["macho.bdef.yaml"] = [MachoTestDataGenerator.CreateMinimalMacho64],
        ["midi.bdef.yaml"] = [MidiTestDataGenerator.CreateMinimalMidi],
        ["mkv.bdef.yaml"] = [MkvTestDataGenerator.CreateMinimalWebm],
        ["mp3.bdef.yaml"] = [Mp3TestDataGenerator.CreateMinimalMp3],
        ["mp4.bdef.yaml"] = [Mp4TestDataGenerator.CreateMinimalMp4],
        ["msgpack.bdef.yaml"] = [MsgpackTestDataGenerator.CreateMinimalMsgpack],
        ["ogg.bdef.yaml"] = [OggTestDataGenerator.CreateMinimalOgg],
        ["otf.bdef.yaml"] = [OtfTestDataGenerator.CreateMinimalOtf, OtfTestDataGenerator.CreateMinimalTtf, OtfTestDataGenerator.CreateOtfWithCmapAndHhea],
        ["parquet.bdef.yaml"] = [ParquetTestDataGenerator.CreateMinimalParquet],
        ["pcap.bdef.yaml"] = [PcapTestDataGenerator.CreateMinimalPcap, PcapTestDataGenerator.CreatePcapWithTcpOptions],
        ["pdf.bdef.yaml"] = [PdfTestDataGenerator.CreateMinimalPdf],
        ["pe.bdef.yaml"] = [PeTestDataGenerator.CreateMinimalPe],
        ["png.bdef.yaml"] = [PngTestDataGenerator.CreateMinimalPng, PngTestDataGenerator.CreatePngWithSrgb],
        ["protobuf.bdef.yaml"] = [ProtobufTestDataGenerator.CreateMinimalProtobuf],
        ["sqlite.bdef.yaml"] = [SqliteTestDataGenerator.CreateMinimalSqlite, SqliteTestDataGenerator.CreateSqliteWithCell],
        ["tar.bdef.yaml"] = [TarTestDataGenerator.CreateMinimalTar],
        ["tiff.bdef.yaml"] = [TiffTestDataGenerator.CreateMinimalTiff, TiffTestDataGenerator.CreateBigEndianTiff, TiffTestDataGenerator.CreateTiffWithRationalTag],
        ["wasm.bdef.yaml"] = [WasmTestDataGenerator.CreateMinimalWasm, WasmTestDataGenerator.CreateWasmWithExportSection],
        ["wav.bdef.yaml"] = [WavTestDataGenerator.CreateMinimalWav, WavTestDataGenerator.CreateWavWithListInfo],
        ["webp.bdef.yaml"] = [WebpTestDataGenerator.CreateMinimalWebp],
        ["x509.bdef.yaml"] = [X509TestDataGenerator.CreateMinimalCertificate],
        ["xz.bdef.yaml"] = [XzTestDataGenerator.CreateMinimalXz, XzTestDataGenerator.CreateTwoBlockXz],
        ["zip.bdef.yaml"] = [ZipTestDataGenerator.CreateMinimalZip],
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
