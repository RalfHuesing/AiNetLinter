#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.ServerMaintenance;

[Trait("Category", "Unit")]
public sealed class GetServerHealthResponseBuilderTests
{
    [Fact]
    public void Build_DefaultHealthIsCompact_AndDetailDiagnosticsStayBounded()
    {
        var entry = new AssemblyHealthEntry(
            "C:\\fixtures\\health.dll",
            "partial",
            "decompiled",
            null,
            null,
            null,
            null,
            ["health-root-0", "health-root-1"],
            TransitiveDiagnostics: ["health-transitive-0", "health-transitive-1"]);

        var compact = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions());
        var compactPayload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            compact.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.Null(compactPayload.Assemblies);
        Assert.False(compactPayload.SessionsIncluded);
        Assert.Equal(1, compactPayload.TotalAssemblySessions);
        Assert.Equal(0, compactPayload.ShownSessionCount);
        Assert.False(compactPayload.DiagnosticsIncluded);
        Assert.False(compactPayload.SessionsTruncated);
        Assert.Empty(compactPayload.SessionsTruncatedBy!);
        Assert.Equal(4, compactPayload.AssemblyDiagnosticCount);
        Assert.Equal(1, compactPayload.AssemblyStatusCounts!["partial"]);
        Assert.Single(compactPayload.AssemblyStatusCounts);
        var compactText = Assert.IsType<TextContentBlock>(Assert.Single(compact.Content)).Text;
        Assert.Contains("Diagnosen gesamt: 4", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnosen: 4 von 4", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-root-0", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-transitive-0", compactText, StringComparison.Ordinal);

        var detailed = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, MaxDiagnostics: 2));
        var detailedPayload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            detailed.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.NotNull(detailedPayload.Assemblies);
        Assert.True(detailedPayload.DiagnosticsIncluded);
        Assert.Equal(4, detailedPayload.AssemblyDiagnosticCount);
        Assert.Equal(1, detailedPayload.AssemblyStatusCounts!["partial"]);
        Assert.Contains("health-root-0", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);

        var sessionDetails = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, IncludeSessions: true, MaxDiagnostics: 2));
        var sessionDetailsPayload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            sessionDetails.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        var detailedAssembly = Assert.Single(sessionDetailsPayload.Assemblies!);
        Assert.Equal(["health-root-0", "health-transitive-0"], detailedAssembly.Diagnostics);
        Assert.True(detailedAssembly.DiagnosticsSummary!.Truncated);
        Assert.Equal(4, detailedAssembly.DiagnosticsSummary.TotalCount);
        Assert.Equal(2, detailedAssembly.DiagnosticsSummary.ShownCount);
        var sessionDetailsText = Assert.IsType<TextContentBlock>(Assert.Single(sessionDetails.Content)).Text;
        Assert.Contains("health-root-0", sessionDetailsText, StringComparison.Ordinal);
        Assert.Contains("health-transitive-0", sessionDetailsText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-root-1", sessionDetailsText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-transitive-1", sessionDetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_TargetAssemblyHealthContainsOnlyTheRequestedTarget()
    {
        var entry = CreateAssemblyEntry("C:\\fixtures\\target-only.dll");

        var result = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(AssemblyPath: entry.TargetPath, IncludeDiagnostics: true));

        var payload = result.StructuredContent!.Value;
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(payload.TryGetProperty("assembly", out var assembly));
        Assert.Equal(entry.TargetPath, assembly.GetProperty("targetPath").GetString());
        Assert.False(payload.TryGetProperty("version", out _));
        Assert.False(payload.TryGetProperty("repository", out _));
        Assert.False(payload.TryGetProperty("daemon", out _));
        Assert.False(payload.TryGetProperty("totalAssemblySessions", out _));
        Assert.DoesNotContain("Version:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Daemon", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly-Sessions", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludeDiagnosticsWithoutSessions_EmitsExplicitEmptyDiagnosticsArray()
    {
        var entry = CreateAssemblyEntry("C:\\fixtures\\health-without-diagnostics.dll");

        var result = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true));

        var payload = result.StructuredContent!.Value;
        Assert.True(payload.TryGetProperty("assemblies", out var assemblies));
        var assembly = Assert.Single(assemblies.EnumerateArray());
        Assert.True(assembly.TryGetProperty("diagnostics", out var diagnostics));
        Assert.Equal(JsonValueKind.Array, diagnostics.ValueKind);
        Assert.Empty(diagnostics.EnumerateArray());
    }

    [Fact]
    public void Build_DetailedDiagnosticsProjectCompleteStatusToPartial()
    {
        var entry = new AssemblyHealthEntry(
            "C:\\fixtures\\health-detail.dll",
            "complete",
            "decompiled",
            null,
            null,
            null,
            null,
            ["health-root"],
            TransitiveDiagnostics: ["health-transitive"]);

        var detailed = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, IncludeSessions: true));
        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            detailed.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        var assembly = Assert.Single(payload.Assemblies!);

        Assert.Equal("partial", assembly.LoadState);
        Assert.Equal("partial", assembly.Completeness);
        Assert.Contains("- LoadState: partial", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
        Assert.Contains("- Vollständigkeit: partial", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludeSessionsRespectsMaxSessionsAndReportsTruncation()
    {
        var entries = new[]
        {
            CreateAssemblyEntry("C:\\fixtures\\health-1.dll"),
            CreateAssemblyEntry("C:\\fixtures\\health-2.dll"),
            CreateAssemblyEntry("C:\\fixtures\\health-3.dll"),
        };

        var result = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            entries,
            new GetServerHealthOptions(IncludeSessions: true, MaxSessions: 2));

        var payload = JsonSerializer.Deserialize<ServerHealthAggregatePayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.True(payload.SessionsIncluded);
        Assert.Equal(3, payload.TotalAssemblySessions);
        Assert.Equal(2, payload.ShownSessionCount);
        Assert.True(payload.SessionsTruncated);
        Assert.Equal(["maxSessions"], payload.SessionsTruncatedBy);
        Assert.Equal(2, payload.Assemblies!.Count);
        Assert.Equal(entries[0].TargetPath, payload.Assemblies[0].TargetPath);
        Assert.Equal(entries[1].TargetPath, payload.Assemblies[1].TargetPath);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Sessiondetails: 2 von 3 (gekürzt: maxSessions)", text, StringComparison.Ordinal);
        Assert.Contains(entries[0].TargetPath, text, StringComparison.Ordinal);
        Assert.Contains(entries[1].TargetPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain(entries[2].TargetPath, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_PartialAssemblyUsesSameNextActionAsNavigation()
    {
        const string assemblyPath = "C:\\fixtures\\partial-health-probe.dll";
        var diagnostics = Enumerable.Range(0, AssemblyAnalysisResponseLimits.DefaultMaxDiagnostics + 1)
            .Select(index => $"partial-decompilation-diagnostic-{index}")
            .ToArray();
        var raw = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [new AssemblyHealthEntry(
                assemblyPath,
                "partial",
                "decompiled",
                null,
                null,
                null,
                null,
                diagnostics,
                Completeness: "partial",
                NextAction: "Keine Aktion erforderlich.")],
            new GetServerHealthOptions(IncludeDiagnostics: true));
        var target = new AnalysisTarget(
            AnalysisTargetType.Assembly,
            assemblyPath,
            new AnalysisTargetRequest(assemblyPath));

        var result = McpToolResults.WithNavigation(raw, target);
        var payload = result.StructuredContent!.Value;
        var assembly = Assert.Single(payload.GetProperty("assemblies").EnumerateArray());
        var navigationNext = payload.GetProperty("navigation").GetProperty("next");

        Assert.Equal("request_detail", navigationNext.GetProperty("kind").GetString());
        Assert.Equal(
            navigationNext.GetProperty("action").GetString(),
            assembly.GetProperty("nextAction").GetString());
    }

    private static AssemblyHealthEntry CreateAssemblyEntry(string targetPath) =>
        new(targetPath, "complete", "decompiled", null, null, null, null, null, null, null, null);
}
