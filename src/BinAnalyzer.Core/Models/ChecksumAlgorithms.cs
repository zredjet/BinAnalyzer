namespace BinAnalyzer.Core.Models;

/// <summary>チェックサムの種類。整数系は整数型フィールド、ハッシュ系は bytes 型フィールドに指定する。</summary>
public enum ChecksumKind
{
    Integer,
    Hash,
}

/// <summary>チェックサムアルゴリズム 1 つ（DSL の名前・種類・表示名）。</summary>
public sealed record ChecksumAlgorithmInfo(string Name, ChecksumKind Kind, string DisplayName);

/// <summary>
/// DSL で指定できるチェックサムアルゴリズムの一覧（REQ-187）。名前・種類・表示名はこの表 1 か所で持ち、
/// 整数系 / ハッシュ系の集合も表から作る。計算の実装は Engine の <c>ChecksumCalculators</c> が同じ名前で持ち、
/// 両者の一致はテストで固定している。
/// </summary>
public static class ChecksumAlgorithms
{
    public const string Crc32 = "crc32";
    public const string Crc16Ccitt = "crc16-ccitt";
    public const string Crc16Ibm = "crc16-ibm";
    public const string Adler32 = "adler32";
    public const string Md5 = "md5";
    public const string Sha1 = "sha1";
    public const string Sha256 = "sha256";
    public const string Crc8 = "crc8";
    public const string Crc8Maxim = "crc8-maxim";
    public const string Crc8Cdma2000 = "crc8-cdma2000";
    public const string Crc64Ecma = "crc64-ecma";
    public const string XxHash32 = "xxhash32";
    public const string XxHash64 = "xxhash64";
    public const string Fletcher16 = "fletcher16";
    public const string Fletcher32 = "fletcher32";
    public const string Sha384 = "sha384";
    public const string Sha512 = "sha512";
    public const string InternetChecksum = "internet-checksum";
    public const string Crc32Ogg = "crc32-ogg";
    public const string Sum32Be = "sum32-be";

    /// <summary>全アルゴリズム（DSL リファレンスの対応アルゴリズム表と同じ順）。</summary>
    public static IReadOnlyList<ChecksumAlgorithmInfo> All { get; } =
    [
        new(Crc32, ChecksumKind.Integer, "CRC-32"),
        new(Crc16Ccitt, ChecksumKind.Integer, "CRC-16/CCITT"),
        new(Crc16Ibm, ChecksumKind.Integer, "CRC-16/IBM"),
        new(Adler32, ChecksumKind.Integer, "Adler-32"),
        new(Crc8, ChecksumKind.Integer, "CRC-8"),
        new(Crc8Maxim, ChecksumKind.Integer, "CRC-8/Maxim"),
        new(Crc8Cdma2000, ChecksumKind.Integer, "CRC-8/CDMA2000"),
        new(Crc64Ecma, ChecksumKind.Integer, "CRC-64/ECMA"),
        new(XxHash32, ChecksumKind.Integer, "xxHash32"),
        new(XxHash64, ChecksumKind.Integer, "xxHash64"),
        new(Fletcher16, ChecksumKind.Integer, "Fletcher-16"),
        new(Fletcher32, ChecksumKind.Integer, "Fletcher-32"),
        new(InternetChecksum, ChecksumKind.Integer, "Internet Checksum"),
        new(Crc32Ogg, ChecksumKind.Integer, "CRC-32/Ogg"),
        new(Sum32Be, ChecksumKind.Integer, "Sum-32/BE"),
        new(Md5, ChecksumKind.Hash, "MD5"),
        new(Sha1, ChecksumKind.Hash, "SHA-1"),
        new(Sha256, ChecksumKind.Hash, "SHA-256"),
        new(Sha384, ChecksumKind.Hash, "SHA-384"),
        new(Sha512, ChecksumKind.Hash, "SHA-512"),
    ];

    private static readonly Dictionary<string, ChecksumAlgorithmInfo> ByName =
        All.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>整数系アルゴリズム（整数型フィールドに指定する）。大文字小文字を区別しない。</summary>
    public static IReadOnlySet<string> IntegerAlgorithms { get; } = NamesOf(ChecksumKind.Integer);

    /// <summary>ハッシュ系アルゴリズム（bytes 型フィールドに指定する）。大文字小文字を区別しない。</summary>
    public static IReadOnlySet<string> HashAlgorithms { get; } = NamesOf(ChecksumKind.Hash);

    public static bool IsIntegerAlgorithm(string algorithm) => IntegerAlgorithms.Contains(algorithm);
    public static bool IsHashAlgorithm(string algorithm) => HashAlgorithms.Contains(algorithm);
    public static bool IsKnown(string algorithm) => ByName.ContainsKey(algorithm);

    /// <summary>表示名（<c>CRC-32</c> など）。未知の名前はそのまま返す。</summary>
    public static string DisplayName(string algorithm) =>
        ByName.TryGetValue(algorithm, out var info) ? info.DisplayName : algorithm;

    private static HashSet<string> NamesOf(ChecksumKind kind) =>
        All.Where(a => a.Kind == kind).Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
