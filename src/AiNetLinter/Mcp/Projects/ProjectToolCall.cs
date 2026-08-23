#nullable enable

using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Projects;

using AiNetLinter.Output;

/// <summary>
/// Gemeinsamer Dispatch-Weg aller projektgebundenen Tool-Aufrufe: validiert den
/// Pflicht-Parameter <c>projectRoot</c> auf Argumentebene (Defense-in-Depth zur
/// Schema-Validierung des SDK), bindet den Aufruf per Lease an einen Registry-Key und
/// uebersetzt die Zustandsmaschine der Instanz in die Tool-Fehlervertraege (Loading,
/// PROJECT_LOAD_FAILED, [WARN]-Kopf bei ueberschattetem gutem Stand).
/// </summary>
internal static class ProjectToolCall
{
    internal static async Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        string? projectRoot,
        Func<ProjectLease, Task<CallToolResult>> call)
    {
        var guard = GuardRequiredAbsoluteRoot(projectRoot);
        if (guard is not null)
        {
            return GuardResult(guard);
        }

        var leaseResult = registry.Lease(projectRoot!);
        if (!leaseResult.Succeeded || leaseResult.Lease is null)
        {
            return McpToolResults.Recoverable(leaseResult.ErrorCode!, leaseResult.ErrorMessage!, hint: RecoverHint(leaseResult.ErrorCode!));
        }

        using var lease = leaseResult.Lease;
        var server = lease.Server;
        switch (server.LoadState)
        {
            case ServerLoadState.Loading:
                return McpToolResults.Loading();
            case ServerLoadState.LoadFailed:
                return LoadFailedResult(server, lease);
        }

        var result = await call(lease);
        return WithDegradedHeader(server, result);
    }

    /// <summary>Der SDK-Schema-Check ist der Normalfall; dieser Code-Guard ist die
    /// Ruefallebene fuer direkte Resolver-Aufrufe (Tools) und die Pflichtpruefung der
    /// Overview-Resource. Liefert null bei validem absolutem Projektroot.</summary>
    internal static ProjectRootGuardFailure? GuardRequiredAbsoluteRoot(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return new ProjectRootGuardFailure(
                ProjectErrorCodes.ProjectRootRequired,
                "Der Parameter 'projectRoot' ist erforderlich.",
                "Absoluten Projektroot uebergeben, z. B. C:/repos/mein-projekt.");
        }

        if (!Path.IsPathRooted(projectRoot))
        {
            return new ProjectRootGuardFailure(
                ProjectErrorCodes.ProjectRootInvalid,
                $"Der Parameter 'projectRoot' muss ein absoluter Verzeichnispfad sein: '{projectRoot}'.",
                "Relativpfade sind nicht zulaessig; Projektroot absolut angeben.");
        }

        return null;
    }

    /// <summary>Fehlerinfo eines verletzten Root-Guards; Konsumenten wandeln sie in ihren
    /// jeweiligen Antwortkanal um (Tool-Ergebnis bzw. Resource-Fehler).</summary>
    private static CallToolResult GuardResult(ProjectRootGuardFailure guard) =>
        McpToolResults.Error(guard.Code, guard.Message, hint: guard.Hint);

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

        return new CallToolResult { IsError = result.IsError, Content = content };
    }

    internal static string? RecoverHint(string errorCode)
    {
        return errorCode switch
        {
            ProjectErrorCodes.RulesInvalid or ProjectErrorCodes.RulesNotFound =>
                "Definitionsdatei ainetlinter.project.json und die referenzierte rules.json im " +
                "Projektroot pruefen und korrigieren; der naechste Aufruf versucht es erneut.",
            ProjectErrorCodes.SolutionNotFound =>
                "Solution-Pfad in der Definitionsdatei ainetlinter.project.json pruefen.",
            _ => null,
        };
    }
}
