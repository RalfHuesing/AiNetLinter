#nullable enable

using System;
using System.Linq;
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
        var compactText = Assert.IsType<TextContentBlock>(Assert.Single(compact.Content)).Text;
        Assert.Contains("Diagnosen gesamt: 4", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnosen: 4 von 4", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-root-0", compactText, StringComparison.Ordinal);
        Assert.DoesNotContain("health-transitive-0", compactText, StringComparison.Ordinal);

        var detailed = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, MaxDiagnostics: 2));
        Assert.Contains("health-root-0", Assert.IsType<TextContentBlock>(Assert.Single(detailed.Content)).Text, StringComparison.Ordinal);

        var sessionDetails = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true, IncludeSessions: true, MaxDiagnostics: 2));
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

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(entry.TargetPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Version:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Daemon", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Assembly-Sessions", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IncludeDiagnosticsWithoutSessions_ReportsNoDiagnostics()
    {
        var entry = CreateAssemblyEntry("C:\\fixtures\\health-without-diagnostics.dll");

        var result = GetServerHealthResponseBuilder.Build(
            Array.Empty<ProjectSnapshot>(),
            [entry],
            new GetServerHealthOptions(IncludeDiagnostics: true));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(entry.TargetPath, text, StringComparison.Ordinal);
        Assert.Contains("Diagnosen: keine", text, StringComparison.Ordinal);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Vollständigkeit: partial", text, StringComparison.Ordinal);
        Assert.Contains("Nächste Aktion: Scope oder Detaillevel verfeinern", text, StringComparison.Ordinal);
    }

    private static AssemblyHealthEntry CreateAssemblyEntry(string targetPath) =>
        new(targetPath, "complete", "decompiled", null, null, null, null, null, null, null, null);
}
