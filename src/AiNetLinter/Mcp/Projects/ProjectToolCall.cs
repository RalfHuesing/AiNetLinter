#nullable enable

using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiNetLinter.Mcp.Projects;

using AiNetLinter.Output;

/// <summary>
/// Gemeinsamer Dispatch-Weg aller projektgebundenen Tool-Aufrufe: validiert den
/// konkreten Solution-Dateipfad auf Argumentebene (Defense-in-Depth zur
/// Schema-Validierung des SDK), bindet den Aufruf per Lease an genau diesen
/// Registry-Key und uebersetzt die Zustandsmaschine der Instanz in die
/// Tool-Fehlervertraege (Loading, PROJECT_LOAD_FAILED, [WARN]-Kopf bei
/// ueberschattetem gutem Stand).
/// </summary>
internal static class ProjectToolCall
{
    internal static async Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? targetPath,
        Func<ProjectLease, Task<CallToolResult>> call)
    {
        var leaseResolution = ResolveLease(registry, targetPath);
        if (leaseResolution.Error is not null)
        {
            return leaseResolution.Error;
        }

        using var lease = leaseResolution.Lease!;
        var server = lease.Server;
        switch (server.LoadState)
        {
            case ServerLoadState.Loading:
                return McpToolResults.Loading();
            case ServerLoadState.LoadFailed:
                var loadFailed = LoadFailedResult(server, lease);
                lease.MarkLoadFailedResponseEmitted();
                return loadFailed;
        }

        var result = await call(lease);
        return WithDegradedHeader(server, result);
    }

    internal static async Task<CallToolResult> ExecuteFilesystemAsync(
        ProjectRegistry registry,
        string? targetPath,
        Func<ProjectLease, Task<CallToolResult>> call)
    {
        var leaseResolution = ResolveLease(registry, targetPath);
        if (leaseResolution.Error is not null)
        {
            return leaseResolution.Error;
        }

        using var lease = leaseResolution.Lease!;
        return await call(lease);
    }

    private static LeaseResolution ResolveLease(ProjectRegistry registry, string? targetPath)
    {
        var definition = ProjectDefinitionLoader.LoadSolutionTarget(targetPath);
        if (!definition.Succeeded || definition.Definition is null)
        {
            var error = McpToolResults.Recoverable(
                definition.ErrorCode!,
                definition.Message!,
                hint: RecoverHint(definition.ErrorCode!));
            return new(null, error);
        }

        // The registry key is always the canonical Solution file. The containing
        // directory is intentionally not used here; it is only a filesystem scope
        // for tools that explicitly operate on physical files.
        var leaseResult = registry.Lease(definition.Definition.SolutionPath);
        if (!leaseResult.Succeeded || leaseResult.Lease is null)
        {
            var error = McpToolResults.Recoverable(
                leaseResult.ErrorCode!,
                leaseResult.ErrorMessage!,
                hint: RecoverHint(leaseResult.ErrorCode!));
            return new(null, error);
        }

        return new(leaseResult.Lease, null);
    }

    /// <summary>Der SDK-Schema-Check ist der Normalfall; dieser Code-Guard ist die
    /// Ruefallebene fuer direkte Resolver-Aufrufe (Tools) und bestehende Resource-/Datei-
    /// Adapter. Liefert null bei einer vorhandenen, konkreten Solution-Datei.</summary>
    internal static ProjectRootGuardFailure? GuardRequiredAbsoluteRoot(string? targetPath)
    {
        var definition = ProjectDefinitionLoader.LoadSolutionTarget(targetPath);
        if (definition.Succeeded)
        {
            return null;
        }

        return new ProjectRootGuardFailure(
            definition.ErrorCode!,
            definition.Message!,
            RecoverHint(definition.ErrorCode!) ??
            "Den absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei uebergeben.");
    }

    internal static string FormatGuard(ProjectRootGuardFailure guard) =>
        LinterErrorFormatter.Format(guard.Code, guard.Message, hint: guard.Hint);

    private static CallToolResult LoadFailedResult(McpCodeGraphServer server, ProjectLease lease)
    {
        var failure = BuildLoadFailure(server, lease);
        return McpToolResults.Error(
            ProjectErrorCodes.ProjectLoadFailed,
            failure.Message,
            context: failure.Context,
            hint: failure.Hint);
    }

    internal static ProjectLoadFailure BuildLoadFailure(McpCodeGraphServer server, ProjectLease lease)
    {
        var detail = server.LastLoadError
            ?? $"Hintergrund-Load lieferte keine Solution ({lease.Definition.SolutionPath}).";
        return new(
            $"Solution-Load fehlgeschlagen: {detail}",
            lease.Definition.SolutionPath,
            "Ursache beheben (Solution-/Build-Fehler); der naechste Aufruf startet den Load " +
            "automatisch neu, fehlgeschlagene Loads werden nicht negativ gecacht.");
    }

    private static CallToolResult WithDegradedHeader(McpCodeGraphServer server, CallToolResult result)
    {
        if (!server.HasDegradedAnswerState)
        {
            return result;
        }

        const string header = "[WARN]: Ein frueherer inkrementeller Refresh schlug fehl; die Antwort " +
                              "basiert auf dem letzten guten Solution-Stand.\n\n";
        var content = new List<ContentBlock>();
        foreach (var block in result.Content)
        {
            content.Add(block is TextContentBlock text ? new TextContentBlock { Text = header + text.Text } : block);
        }

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = content,
            StructuredContent = AddDegradedMetadata(result.StructuredContent, header),
        };
    }

    private static JsonElement? AddDegradedMetadata(JsonElement? structuredContent, string warning)
    {
        if (structuredContent is not { } element)
        {
            return null;
        }

        var metadata = new JsonObject
        {
            ["degraded"] = true,
            ["freshness"] = "stale",
            ["degradedReason"] = "refresh-failed",
            ["freshnessWarning"] = warning.Trim(),
        };

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                metadata[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }
        else
        {
            metadata["payload"] = JsonNode.Parse(element.GetRawText());
        }

        return JsonSerializer.SerializeToElement(metadata, McpJsonOptions.Default);
    }

    internal static string? RecoverHint(string errorCode)
    {
        return errorCode switch
        {
            ProjectErrorCodes.RulesInvalid or ProjectErrorCodes.RulesNotFound =>
                "Die optionale ainetlinter-rules.json direkt neben der adressierten Solution " +
                "pruefen und korrigieren; der naechste Aufruf versucht es erneut.",
            ProjectErrorCodes.SolutionNotFound =>
                "Einen absoluten, vorhandenen Pfad der konkreten .sln- oder .slnx-Datei " +
                "uebergeben; es wird keine andere Solution gesucht.",
            ProjectErrorCodes.ProjectRootRequired or ProjectErrorCodes.ProjectRootInvalid =>
                "Den absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei uebergeben.",
            _ => null,
        };
    }

    private sealed record LeaseResolution(ProjectLease? Lease, CallToolResult? Error);
}
