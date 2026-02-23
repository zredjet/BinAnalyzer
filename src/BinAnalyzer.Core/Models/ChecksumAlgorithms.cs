namespace BinAnalyzer.Core.Models;

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

    private static readonly HashSet<string> IntegerAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Crc32, Crc16Ccitt, Crc16Ibm, Adler32,
        Crc8, Crc8Maxim, Crc8Cdma2000, Crc64Ecma,
        XxHash32, XxHash64, Fletcher16, Fletcher32,
    };

    private static readonly HashSet<string> HashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Md5, Sha1, Sha256, Sha384, Sha512,
    };

    public static bool IsIntegerAlgorithm(string algorithm) => IntegerAlgorithms.Contains(algorithm);
    public static bool IsHashAlgorithm(string algorithm) => HashAlgorithms.Contains(algorithm);
    public static bool IsKnown(string algorithm) => IntegerAlgorithms.Contains(algorithm) || HashAlgorithms.Contains(algorithm);

    public static string DisplayName(string algorithm) => algorithm.ToUpperInvariant() switch
    {
        "CRC32" => "CRC-32",
        "CRC16-CCITT" => "CRC-16/CCITT",
        "CRC16-IBM" => "CRC-16/IBM",
        "ADLER32" => "Adler-32",
        "MD5" => "MD5",
        "SHA1" => "SHA-1",
        "SHA256" => "SHA-256",
        "CRC8" => "CRC-8",
        "CRC8-MAXIM" => "CRC-8/Maxim",
        "CRC8-CDMA2000" => "CRC-8/CDMA2000",
        "CRC64-ECMA" => "CRC-64/ECMA",
        "XXHASH32" => "xxHash32",
        "XXHASH64" => "xxHash64",
        "FLETCHER16" => "Fletcher-16",
        "FLETCHER32" => "Fletcher-32",
        "SHA384" => "SHA-384",
        "SHA512" => "SHA-512",
        _ => algorithm,
    };
}
