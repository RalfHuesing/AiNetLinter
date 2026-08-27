#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class GetFileTreeInputValidator
{
    internal static CallToolResult? Validate(string projectRoot, GetFileTreeInput input)
    {
        var rootGuard = ProjectToolCall.GuardRequiredAbsoluteRoot(projectRoot);
        if (rootGuard is not null)
        {
            return McpToolResults.Error(rootGuard.Code, rootGuard.Message, hint: rootGuard.Hint);
        }

        var path = FileTreePathResolver.ResolveRoot(projectRoot, input.Root);
        if (!path.Succeeded)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                path.ErrorMessage ?? "root ist ungueltig.",
                hint: "root relativ zum absoluten projectRoot angeben.");
        }

        var rootError = ValidateRootDirectory(path.EffectiveRoot!);
        if (rootError is not null) return rootError;
        if (!IsValidView(input.View)) return Invalid("view muss summary, tree oder files sein.");
        if (!IsValidSort(input.SortBy)) return Invalid("sortBy muss path, size_desc oder extension sein.");
        var maxDepthError = ValidateDepth(input.MaxDepth, GetFileTreeTool.MaxDepthCap, "maxDepth");
        if (maxDepthError is not null) return maxDepthError;
        var treeDepthError = ValidateDepth(input.TreeDepth, GetFileTreeTool.MaxDepthCap, "treeDepth");
        if (treeDepthError is not null) return treeDepthError;
        if (input.MaxResults is < 1 or > GetFileTreeTool.MaxResultsCap)
        {
            return Invalid($"maxResults muss zwischen 1 und {GetFileTreeTool.MaxResultsCap} liegen.");
        }

        var extensionError = ValidateExtensions(input.IncludeExtensions);
        if (extensionError is not null) return extensionError;
        var filterError = ValidateGlob(input.FileFilter, "fileFilter");
        if (filterError is not null) return filterError;
        return ValidateGlobs(input.ExcludePatterns, "excludePatterns");
    }

    private static CallToolResult? ValidateRootDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ResourceNotFound,
                $"Root-Verzeichnis '{root}' wurde nicht gefunden oder ist kein Verzeichnis.",
                context: root,
                hint: "root relativ zum projectRoot pruefen.");
        }

        try
        {
            if (FileSystemExclusionHelpers.IsExcludedDirectoryName(Path.GetFileName(root)))
            {
                return McpToolResults.Recoverable(
                    LinterErrorCodes.ResourceNotFound,
                    $"Root-Verzeichnis '{root}' ist ein standardmaessig ausgeschlossener Teilbaum.",
                    context: root,
                    hint: "Einen nicht generierten Root ausserhalb von obj/bin/.git angeben.");
            }

            var attributes = File.GetAttributes(root);
            if (!FileSystemExclusionHelpers.IsTraversableSubDirectory(attributes))
            {
                return McpToolResults.Recoverable(
                    LinterErrorCodes.ResourceNotFound,
                    $"Root-Verzeichnis '{root}' ist ein Reparse-Point und wird nicht traversiert.",
                    context: root,
                    hint: "Einen physischen Verzeichnisroot ohne Junction oder Symlink angeben.");
            }
        }
        catch (IOException ex)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.ResourceNotFound, ex.Message, context: root);
        }
        catch (UnauthorizedAccessException ex)
        {
            return McpToolResults.Recoverable(LinterErrorCodes.ResourceNotFound, ex.Message, context: root);
        }

        return null;
    }

    private static CallToolResult? ValidateExtensions(IReadOnlyList<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0) return null;
        foreach (var extension in extensions)
        {
            if (!FileTreeFilter.IsValidExtension(extension))
            {
                return Invalid($"includeExtensions enthaelt eine ungueltige Extension: '{extension}'.");
            }
        }

        return null;
    }

    private static CallToolResult? ValidateGlobs(IReadOnlyList<string>? patterns, string parameterName)
    {
        if (patterns is null) return null;
        foreach (var pattern in patterns)
        {
            var error = ValidateGlob(pattern, parameterName);
            if (error is not null) return error;
        }

        return null;
    }

    private static CallToolResult? ValidateGlob(string? pattern, string parameterName)
    {
        if (pattern is null or "") return null;
        if (FileTreeFilter.IsValidRelativeGlob(pattern)) return null;
        return Invalid($"{parameterName} enthaelt ein ungueltiges relatives Glob: '{pattern}'.");
    }

    private static bool IsValidView(string? view) => view is not null &&
        view.Equals("summary", StringComparison.OrdinalIgnoreCase) ||
        view is not null && view.Equals("tree", StringComparison.OrdinalIgnoreCase) ||
        view is not null && view.Equals("files", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidSort(string? sortBy) => sortBy is not null &&
        (sortBy.Equals("path", StringComparison.OrdinalIgnoreCase) ||
         sortBy.Equals("size_desc", StringComparison.OrdinalIgnoreCase) ||
         sortBy.Equals("extension", StringComparison.OrdinalIgnoreCase));

    private static CallToolResult? ValidateDepth(int? depth, int cap, string name)
    {
        if (depth is null) return null;
        return depth < 0 || depth > cap
            ? Invalid($"{name} muss zwischen 0 und {cap} liegen.")
            : null;
    }

    private static CallToolResult Invalid(string message) =>
        McpToolResults.Recoverable(
            LinterErrorCodes.InvalidArgument,
            message,
            hint: "Parameter gemaess get_file_tree-Vertrag korrigieren.");
}
