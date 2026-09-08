#nullable enable

using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class FileTreePathResolver
{
    internal static FileTreePathResolution ResolveRoot(string? analysisRoot, string? relativeRoot)
    {
        if (string.IsNullOrWhiteSpace(analysisRoot))
        {
            return FileTreePathResolution.Invalid("Der Parameter 'analysisRoot' ist erforderlich und muss absolut sein.");
        }

        var requestedRoot = string.IsNullOrWhiteSpace(relativeRoot) ? "." : relativeRoot;
        requestedRoot = Common.McpInputNormalizer.StripEnclosingQuotesAndBackticks(requestedRoot);
        if (string.IsNullOrWhiteSpace(requestedRoot)) requestedRoot = ".";

        if (Path.IsPathRooted(requestedRoot))
        {
            return FileTreePathResolution.Invalid(
                $"Der Parameter 'root' muss relativ zum analysisRoot sein: '{relativeRoot}'.");
        }

        if (!Path.IsPathRooted(analysisRoot))
        {
            return FileTreePathResolution.Invalid(
                $"Der Parameter 'analysisRoot' muss absolut sein: '{analysisRoot}'.");
        }

        try
        {
            var fullAnalysisRoot = Path.GetFullPath(analysisRoot);
            var candidate = Path.GetFullPath(Path.Combine(fullAnalysisRoot, requestedRoot));

            var relative = PathNormalizer.NormalizeSeparators(
                Path.GetRelativePath(fullAnalysisRoot, candidate));

            if (relative == ".."
                || relative.StartsWith("../", StringComparison.Ordinal)
                || Path.IsPathRooted(relative))
            {
                return FileTreePathResolution.Invalid(
                    $"Der Parameter 'root' liegt außerhalb des analysisRoot: '{relative}'.");
            }

            return FileTreePathResolution.Success(candidate);
        }
        catch (ArgumentException)
        {
            return FileTreePathResolution.Invalid("analysisRoot und root müssen gültige Pfade sein.");
        }
        catch (IOException)
        {
            return FileTreePathResolution.Invalid("analysisRoot und root konnten nicht normalisiert werden.");
        }
        catch (NotSupportedException)
        {
            return FileTreePathResolution.Invalid("analysisRoot und root müssen gültige Pfade sein.");
        }
    }
}

internal sealed record FileTreePathResolution(
    string? EffectiveRoot,
    string? ErrorCode,
    string? ErrorMessage)
{
    internal bool Succeeded => EffectiveRoot is not null;

    internal static FileTreePathResolution Success(string effectiveRoot) =>
        new(effectiveRoot, null, null);

    internal static FileTreePathResolution Invalid(string errorMessage) =>
        new(null, LinterErrorCodes.InvalidArgument, errorMessage);
}
