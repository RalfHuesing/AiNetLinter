namespace AiNetLinter.Output;

public static class PathNormalizer
{
    public static string ToRelative(string outputRoot, string absoluteFilePath)
    {
        if (string.IsNullOrEmpty(absoluteFilePath))
        {
            return string.Empty;
        }

        var normalizedRoot = Path.GetFullPath(outputRoot);
        var normalizedFile = Path.GetFullPath(absoluteFilePath);

        if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalizedFile);
        }

        var relative = Path.GetRelativePath(normalizedRoot, normalizedFile);
        return relative.Replace('\\', '/');
    }

    public static bool IsTestFile(string relativePath) =>
        relativePath.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Contains(".Tests\\", StringComparison.OrdinalIgnoreCase);
}
