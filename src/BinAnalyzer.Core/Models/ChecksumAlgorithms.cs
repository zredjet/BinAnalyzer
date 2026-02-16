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

    private static readonly HashSet<string> IntegerAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Crc32, Crc16Ccitt, Crc16Ibm, Adler32,
    };

    private static readonly HashSet<string> HashAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        Md5, Sha1, Sha256,
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
        _ => algorithm,
    };
}
