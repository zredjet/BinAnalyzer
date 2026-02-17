namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// テストファイルの準備を担当する xUnit Fixture。
/// シェルスクリプトで生成されないフォーマットを既存 TestDataGenerator で補完する。
/// </summary>
public sealed class RealFileFixture : IDisposable
{
    public string TestDataDir { get; }

    public RealFileFixture()
    {
        TestDataDir = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "testdata", "real");
        Directory.CreateDirectory(TestDataDir);

        // シェルスクリプトで生成できないフォーマットを TestDataGenerator で補完
        EnsureFile("test.gif", GifTestDataGenerator.CreateMinimalGif);
        EnsureFile("test.wav", WavTestDataGenerator.CreateMinimalWav);
        EnsureFile("test.mp3", Mp3TestDataGenerator.CreateMinimalMp3);
        EnsureFile("test.flac", FlacTestDataGenerator.CreateMinimalFlac);
        EnsureFile("test.avi", AviTestDataGenerator.CreateMinimalAvi);
        EnsureFile("test.mp4", Mp4TestDataGenerator.CreateMinimalMp4);
        EnsureFile("test.flv", FlvTestDataGenerator.CreateMinimalFlv);
        EnsureFile("test.midi", MidiTestDataGenerator.CreateMinimalMidi);
        EnsureFile("test.webp", WebpTestDataGenerator.CreateMinimalWebp);
        EnsureFile("test.ico", IcoTestDataGenerator.CreateMinimalIco);
        EnsureFile("test.elf", ElfTestDataGenerator.CreateMinimalElf64);
        EnsureFile("test.pe", PeTestDataGenerator.CreateMinimalPe);
        EnsureFile("test.wasm", WasmTestDataGenerator.CreateMinimalWasm);
        EnsureFile("test.dns", DnsTestDataGenerator.CreateMinimalDns);
        EnsureFile("test.7z", SevenZipTestDataGenerator.CreateMinimal7z);
        EnsureFile("test.parquet", ParquetTestDataGenerator.CreateMinimalParquet);
        EnsureFile("test.protobuf", ProtobufTestDataGenerator.CreateMinimalProtobuf);
        EnsureFile("test.ogg", OggTestDataGenerator.CreateMinimalOgg);

        // シェルスクリプトが未実行の場合のフォールバック
        EnsureFile("test.png", PngTestDataGenerator.CreateMinimalPng);
        EnsureFile("test.jpg", JpegTestDataGenerator.CreateMinimalJpeg);
        EnsureFile("test.bmp", BmpTestDataGenerator.CreateMinimalBmp);
        EnsureFile("test.tiff", TiffTestDataGenerator.CreateMinimalTiff);
        EnsureFile("test.gz", GzipTestDataGenerator.CreateMinimalGzip);
        EnsureFile("test.tar", TarTestDataGenerator.CreateMinimalTar);
        EnsureFile("test.zip", ZipTestDataGenerator.CreateMinimalZip);
        EnsureFile("test.sqlite", SqliteTestDataGenerator.CreateMinimalSqlite);
        EnsureFile("test.macho", MachoTestDataGenerator.CreateMinimalMacho64);
        EnsureFile("test.class", JavaClassTestDataGenerator.CreateMinimalJavaClass);
        EnsureFile("test.lz4", Lz4TestDataGenerator.CreateMinimalLz4);
        EnsureFile("test.icc", IccTestDataGenerator.CreateMinimalIcc);
        EnsureFile("test.otf", OtfTestDataGenerator.CreateMinimalOtf);
        EnsureFile("test.pdf", PdfTestDataGenerator.CreateMinimalPdf);
        EnsureFile("test.pcap", PcapTestDataGenerator.CreateMinimalPcap);
    }

    private void EnsureFile(string name, Func<byte[]> generator)
    {
        var path = Path.Combine(TestDataDir, name);
        var data = generator();
        
        // Always write if file doesn't exist, or if size differs (ensures consistency across environments)
        if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
        {
            File.WriteAllBytes(path, data);
        }
    }

    public void Dispose()
    {
        // テストファイルは再利用のため削除しない
    }
}
