#nullable enable

using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class FileTreePathResolver
{
    internal static FileTreePathResolution ResolveRoot(string? projectRoot, string? relativeRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return FileTreePathResolution.Invalid("Der Parameter 'projectRoot' ist erforderlich und muss absolut sein.");
        }

        var requestedRoot = string.IsNullOrWhiteSpace(relativeRoot) ? "." : relativeRoot;
        if (Path.IsPathRooted(requestedRoot))
        {
            return FileTreePathResolution.Invalid(
                $"Der Parameter 'root' muss relativ zum projectRoot sein: '{relativeRoot}'.");
        }

        if (!Path.IsPathRooted(projectRoot))
        {
            return FileTreePathResolution.Invalid(
                $"Der Parameter 'projectRoot' muss absolut sein: '{projectRoot}'.");
        }

        try
        {
            var fullProjectRoot = Path.GetFullPath(projectRoot);
            var candidate = Path.GetFullPath(Path.Combine(fullProjectRoot, requestedRoot));
            var relative = PathNormalizer.NormalizeSeparators(
                Path.GetRelativePath(fullProjectRoot, candidate));

            if (relative == ".."
                || relative.StartsWith("../", StringComparison.Ordinal)
                || Path.IsPathRooted(relative))
            {
                return FileTreePathResolution.Invalid(
                    $"Der Parameter 'root' liegt außerhalb des projectRoot: '{relative}'.");
            }

            return FileTreePathResolution.Success(candidate);
        }
        catch (ArgumentException)
        {
            return FileTreePathResolution.Invalid("projectRoot und root müssen gültige Pfade sein.");
        }
        catch (IOException)
        {
            return FileTreePathResolution.Invalid("projectRoot und root konnten nicht normalisiert werden.");
        }
        catch (NotSupportedException)
        {
            return FileTreePathResolution.Invalid("projectRoot und root müssen gültige Pfade sein.");
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
