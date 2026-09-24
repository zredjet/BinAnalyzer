using System.Text.RegularExpressions;
using System.Xml.Linq;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Dsl.YamlModels;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>
/// README / docs/*.md と実装の整合性（REQ-183）。
/// 実ファイルの照合に加え、同じチェックに「文書を更新し忘れた」入力を渡して検出できることを確かめる。
/// </summary>
public sealed class DocumentationConsistencyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string ReadRepoFile(params string[] parts) => File.ReadAllText(Path.Combine([RepoRoot, .. parts]));

    private static string Readme => ReadRepoFile("README.md");
    private static string DslReference => ReadRepoFile("docs", "dsl-reference.md");
    private static string ParserDesign => ReadRepoFile("docs", "parser-design.md");
    private static string Architecture => ReadRepoFile("docs", "architecture.md");

    /// <summary>README と docs/ 直下の文書（要望ドキュメントは過去の記録なので対象外）。</summary>
    private static List<(string File, string Text)> UserDocs() =>
        [
            ("README.md", Readme),
            .. Directory.GetFiles(Path.Combine(RepoRoot, "docs"), "*.md")
                .Order()
                .Select(p => ($"docs/{Path.GetFileName(p)}", File.ReadAllText(p))),
        ];

    private static List<string> FormatFileStems() =>
        Directory.GetFiles(Path.Combine(RepoRoot, "formats"), "*.bdef.yaml")
            .Select(p => Path.GetFileName(p)[..^".bdef.yaml".Length])
            .Order()
            .ToList();

    private static List<string> CompressionTypeNames() =>
        Enum.GetValues<FieldType>().Where(FieldTypeCategories.IsCompressed).Select(FieldTypeNames.ToDslName).ToList();

    private static Dictionary<string, DiagnosticSeverity> ValidatorCodes()
    {
        var problems = new List<string>();
        var codes = DocConsistency.ExtractValidatorCodes(
            ReadRepoFile("src", "BinAnalyzer.Core", "Validation", "FormatValidator.cs"), problems);
        problems.Should().BeEmpty();
        return codes;
    }

    private static List<string> SolutionProjects() =>
        XDocument.Parse(ReadRepoFile("BinAnalyzer.slnx"))
            .Descendants("Project")
            .Select(p => Path.GetFileNameWithoutExtension(p.Attribute("Path")!.Value))
            .ToList();

    /// <summary>src の各プロジェクトの直接の ProjectReference（<c>BinAnalyzer.</c> を除いた短縮名）。</summary>
    private static Dictionary<string, IReadOnlySet<string>> SrcProjectReferences() =>
        Directory.GetFiles(Path.Combine(RepoRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToDictionary(
                p => ShortName(Path.GetFileNameWithoutExtension(p)),
                p => (IReadOnlySet<string>)XDocument.Load(p)
                    .Descendants("ProjectReference")
                    .Select(r => ShortName(Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/'))))
                    .ToHashSet());

    private static string ShortName(string project) =>
        project.StartsWith("BinAnalyzer.", StringComparison.Ordinal) ? project["BinAnalyzer.".Length..] : project;

    // --- formats（README） ---

    [Fact]
    public void Readme_FormatList_MatchesFormatsDirectory()
    {
        DocConsistency.CheckFormats(Readme, FormatFileStems()).Should().BeEmpty();
    }

    [Fact]
    public void Readme_FormatList_DetectsFormatAddedWithoutReadmeUpdate()
    {
        var problems = DocConsistency.CheckFormats(Readme, [.. FormatFileStems(), "brotli"]);

        problems.Should().Contain(p => p.Contains("formats/ の定義数"));
        problems.Should().Contain(p => p.Contains("formats/brotli.bdef.yaml"));
    }

    [Fact]
    public void Readme_FormatList_DetectsListedFormatWithoutFile()
    {
        var problems = DocConsistency.CheckFormats(Readme, FormatFileStems().Where(s => s != "msgpack").ToList());

        problems.Should().Contain(p => p.Contains("MessagePack"));
    }

    // --- チェックサムアルゴリズム（dsl-reference.md） ---

    [Fact]
    public void DslReference_ChecksumTable_MatchesImplementation()
    {
        DocConsistency.CheckChecksumAlgorithms(
                DslReference, ChecksumAlgorithms.IntegerAlgorithms, ChecksumAlgorithms.HashAlgorithms)
            .Should().BeEmpty();
    }

    [Fact]
    public void DslReference_ChecksumTable_DetectsAlgorithmAddedWithoutDocUpdate()
    {
        var integer = new HashSet<string>(ChecksumAlgorithms.IntegerAlgorithms) { "crc32c" };

        DocConsistency.CheckChecksumAlgorithms(DslReference, integer, ChecksumAlgorithms.HashAlgorithms)
            .Should().ContainSingle(p => p.Contains("crc32c"));
    }

    [Fact]
    public void DslReference_ChecksumTable_DetectsWrongCategory()
    {
        var integer = new HashSet<string>(ChecksumAlgorithms.IntegerAlgorithms) { "md5" };
        var hash = new HashSet<string>(ChecksumAlgorithms.HashAlgorithms);
        hash.Remove("md5");

        DocConsistency.CheckChecksumAlgorithms(DslReference, integer, hash).Should().NotBeEmpty();
    }

    // --- 圧縮型（dsl-reference.md） ---

    [Fact]
    public void DslReference_CompressionTypes_MatchImplementation()
    {
        DocConsistency.CheckCompressionTypes(DslReference, CompressionTypeNames()).Should().BeEmpty();
    }

    [Fact]
    public void DslReference_CompressionTypes_DetectsTypeAddedWithoutDocUpdate()
    {
        DocConsistency.CheckCompressionTypes(DslReference, [.. CompressionTypeNames(), "brotli"])
            .Should().ContainSingle(p => p.Contains("brotli"));
    }

    // --- 検証コード（parser-design.md と文書全体） ---

    [Fact]
    public void ParserDesign_ValidationCodes_MatchFormatValidator()
    {
        DocConsistency.CheckValidationCodes(ParserDesign, ValidatorCodes()).Should().BeEmpty();
    }

    [Fact]
    public void ParserDesign_ValidationCodes_DetectsCodeAddedWithoutDocUpdate()
    {
        var codes = ValidatorCodes();
        codes["VAL123"] = DiagnosticSeverity.Warning;

        DocConsistency.CheckValidationCodes(ParserDesign, codes).Should().ContainSingle(p => p.Contains("VAL123"));
    }

    [Fact]
    public void ParserDesign_ValidationCodes_DetectsSeverityMismatch()
    {
        var codes = ValidatorCodes();
        codes["VAL114"] = DiagnosticSeverity.Warning;

        DocConsistency.CheckValidationCodes(ParserDesign, codes).Should().ContainSingle(p => p.Contains("VAL114"));
    }

    [Fact]
    public void ValidatorCodeExtraction_KnowsErrorsInThe1xxRange()
    {
        var codes = ValidatorCodes();

        codes["VAL114"].Should().Be(DiagnosticSeverity.Error);
        codes["VAL115"].Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ValidatorCodeExtraction_DetectsNonLiteralCall()
    {
        var source = """
            private static ValidationDiagnostic Error(string code, string message) => new(code, message);
            private static ValidationDiagnostic Warning(string code, string message) => new(code, message);
            void A() { diagnostics.Add(Error("VAL001", "x")); }
            void B() { diagnostics.Add(Warning(code, "y")); }
            """;
        var problems = new List<string>();

        DocConsistency.ExtractValidatorCodes(source, problems);

        problems.Should().ContainSingle();
    }

    [Fact]
    public void Docs_ValidationCodeMentions_ExistAndNoRanges()
    {
        DocConsistency.CheckValidationCodeMentions(UserDocs(), ValidatorCodes().Keys).Should().BeEmpty();
    }

    [Fact]
    public void Docs_ValidationCodeMentions_DetectsUnknownCodeAndRange()
    {
        var problems = DocConsistency.CheckValidationCodeMentions(
            [("x.md", "エラー（VAL001〜VAL011）\n警告 VAL999")], ValidatorCodes().Keys);

        problems.Should().Contain(p => p.Contains("範囲表記"));
        problems.Should().Contain(p => p.Contains("VAL999"));
    }

    // --- プロジェクト構成・依存関係（architecture.md） ---

    [Fact]
    public void Architecture_ProjectTree_MatchesSolution()
    {
        DocConsistency.CheckProjectTree(Architecture, SolutionProjects()).Should().BeEmpty();
    }

    [Fact]
    public void Architecture_ProjectTree_DetectsProjectAddedWithoutDocUpdate()
    {
        DocConsistency.CheckProjectTree(Architecture, [.. SolutionProjects(), "BinAnalyzer.Foo.Tests"])
            .Should().ContainSingle(p => p.Contains("BinAnalyzer.Foo.Tests"));
    }

    [Fact]
    public void Architecture_Dependencies_MatchProjectReferences()
    {
        DocConsistency.CheckProjectDependencies(Architecture, SrcProjectReferences()).Should().BeEmpty();
    }

    [Fact]
    public void Architecture_Dependencies_DetectsReferenceAddedWithoutDocUpdate()
    {
        var refs = SrcProjectReferences();
        refs["Engine"] = new HashSet<string>(refs["Engine"]) { "Output" };

        DocConsistency.CheckProjectDependencies(Architecture, refs)
            .Should().ContainSingle(p => p.Contains("Engine") && p.Contains("Output"));
    }

    // --- YAML 例のキー ---

    private static readonly IReadOnlySet<string> FieldKeys = DocConsistency.YamlKeysOf<YamlFieldModel>();
    private static readonly IReadOnlySet<string> StructKeys = DocConsistency.YamlKeysOf<YamlStructModel>();
    private static readonly IReadOnlySet<string> ChecksumKeys = DocConsistency.YamlKeysOf<YamlChecksumModel>();

    [Fact]
    public void Docs_YamlExamples_UseOnlyKnownKeys()
    {
        DocConsistency.CheckYamlExampleKeys(UserDocs(), FieldKeys, StructKeys, ChecksumKeys).Should().BeEmpty();
    }

    [Fact]
    public void Docs_YamlExamples_DetectsUnknownKeys()
    {
        var doc = """
            # 例

            ```yaml
            structs:
              track:
                resync: [0x4D]
                fields:
                  - name: magic
                    type: bytes
                    size: "4"
                    expect: "MTrk"
                  - name: crc
                    type: uint32
                    checksum:
                      algorithm: crc32
                      field: [magic]
            ```
            """;

        var problems = DocConsistency.CheckYamlExampleKeys([("x.md", doc)], FieldKeys, StructKeys, ChecksumKeys);

        problems.Should().HaveCount(3);
        problems.Should().Contain(p => p.StartsWith("x.md:6:") && p.Contains("`resync`"));
        problems.Should().Contain(p => p.StartsWith("x.md:11:") && p.Contains("`expect`"));
        problems.Should().Contain(p => p.StartsWith("x.md:16:") && p.Contains("`field`"));
    }

    [Fact]
    public void Docs_YamlExamples_IgnoreBitfieldAndFlagsEntries()
    {
        var doc = """
            ```yaml
            flags:
              f:
                bit_size: 8
                fields:
                  - name: a
                    bit: 0
            structs:
              s:
                - name: b
                  type: bitfield
                  size: "1"
                  fields:
                    - name: lo
                      bits: "3:0"
            ```
            """;

        DocConsistency.CheckYamlExampleKeys([("x.md", doc)], FieldKeys, StructKeys, ChecksumKeys).Should().BeEmpty();
    }

    // --- resync_marker の例（dsl-reference.md） ---

    private static string ResyncMarkerExample()
    {
        var problems = new List<string>();
        var region = DocConsistency.Region(DslReference, "resync-marker-example", problems);
        problems.Should().BeEmpty();
        var yaml = DocConsistency.FirstYamlBlock(region!);
        yaml.Should().NotBeNull();
        return yaml!;
    }

    private static readonly byte[] MidiTrack = [.. "MTrk"u8, 0x00, 0x00, 0x00, 0x03, 0x01, 0x02, 0x03];

    [Fact]
    public void DslReference_ResyncMarkerExample_ValidatesMagic()
    {
        var format = new YamlFormatLoader().LoadFromString(ResyncMarkerExample());
        FormatValidator.Validate(format).Errors.Should().BeEmpty();

        var decoded = new BinaryDecoder().Decode(MidiTrack, format);

        var magic = decoded.Children.Single(c => c.Name == "magic");
        magic.ValidationPassed.Should().BeTrue();
        ((DecodedInteger)decoded.Children.Single(c => c.Name == "length")).Value.Should().Be(3);
        new TreeOutputFormatter().Format(decoded).Should().MatchRegex(@"magic .*✓");
    }

    [Fact]
    public void DslReference_ResyncMarkerExample_FailsWhenExpectedChanged()
    {
        var yaml = Regex.Replace(ResyncMarkerExample(), @"(expected: \[0x4D, 0x54, 0x72, )0x6B\]", "${1}0x6C]");
        yaml.Should().Contain("0x6C]");
        var format = new YamlFormatLoader().LoadFromString(yaml);

        var decoded = new BinaryDecoder().Decode(MidiTrack, format);

        decoded.Children.Single(c => c.Name == "magic").ValidationPassed.Should().BeFalse();
        new TreeOutputFormatter().Format(decoded).Should().MatchRegex(@"magic .*✗");
    }
}
