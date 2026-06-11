using System.Security.Cryptography;

namespace AiNetLinter.Baseline;

/// <summary>
/// Berechnet SHA-256-Checksummen für Dateiinhalte.
/// </summary>
public static class FileChecksumCalculator
{
    /// <summary>
    /// Liest die Datei und liefert den SHA-256-Hash als lowercase-Hex-String (64 Zeichen).
    /// </summary>
    public static string ComputeSha256Hex(string absoluteFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteFilePath);

        var bytes = File.ReadAllBytes(absoluteFilePath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
