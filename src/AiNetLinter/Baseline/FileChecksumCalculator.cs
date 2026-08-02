using System.Security.Cryptography;

namespace AiNetLinter.Baseline;

public static class FileChecksumCalculator
{
    public static string ComputeSha256Hex(string absoluteFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        var bytes = File.ReadAllBytes(absoluteFilePath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
