#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// E2E-Test fuer EPIC-07 Staleness-Invalidierung (Konzept Z. 612-615): eine Datei-Aenderung
/// auf Disk zwischen zwei Tool-Calls muss beim naechsten betroffenen Call korrekt propagiert
/// werden. Unit-Tests in <c>McpCodeGraphServerTests.cs</c> beweisen die Scanner-Logik;
/// dieser Test beweist die Wire-Propagierung durch den realen MCP-Subprozess.
///
/// A3-Pfad: wenn in <c>McpCodeGraphServer.TryApplyContentChange</c> der Aufruf
/// <c>updated.WithDocumentText(...)</c> auskommentiert wird (oder durch ein no-op ersetzt),
/// dann aktualisiert der Staleness-Check zwar mtime/Hash-State, aber die Solution
/// enthaelt weiterhin den alten Inhalt. Der zweite find_symbol-Aufruf wuerde die neue
/// Klasse <c>CallerRenamedXyz</c> nicht finden, der Test schlaegt fehl.
/// </summary>
[Collection("ConsoleTestCollection")]
[Trait("Category", "Integration")]
public sealed class McpServerCommandStalenessTests
{
    [Fact]
    public async Task RunAsync_FileChangeBetweenCalls_ReflectedInSecondCall()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var callerPath = fixture.CallerPath;

        await using var client = await McpTestClient.ConnectAsync(fixture.RootPath);

        // 1) Initial: CallerRenamedXyz existiert noch nicht in Caller.cs.
        var initial = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "CallerRenamedXyz" });
        Assert.NotEqual(true, initial.IsError);
        var initialText = Assert.IsType<TextContentBlock>(Assert.Single(initial.Content)).Text;
        Assert.Contains("Keine Treffer fuer 'CallerRenamedXyz'", initialText, StringComparison.Ordinal);

        // 2) Datei aendern: neue Klasse CallerRenamedXyz anhaengen, mtime nach vorn ziehen
        // (analog McpCodeGraphServerTests.cs:45), damit der mtime-Check in
        // RefreshStaleDocuments die Aenderung erkennt.
        var original = File.ReadAllText(callerPath);
        File.WriteAllText(
            callerPath,
            original + Environment.NewLine + "public class CallerRenamedXyz { }" + Environment.NewLine);
        File.SetLastWriteTimeUtc(callerPath, DateTime.UtcNow.AddSeconds(2));

        // 3) Zweiter Call: muss die neue Klasse finden (Staleness-Propagierung durch den
        // realen MCP-Subprozess). Ohne den Staleness-Mechanismus waere die Solution-Sicht
        // des Servers noch der Initial-Snapshot ohne CallerRenamedXyz.
        var updated = await client.CallToolAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "CallerRenamedXyz" });
        Assert.NotEqual(true, updated.IsError);
        var updatedText = Assert.IsType<TextContentBlock>(Assert.Single(updated.Content)).Text;

        Assert.Contains("CallerRenamedXyz", updatedText, StringComparison.Ordinal);
        Assert.Contains("Caller.cs", updatedText, StringComparison.Ordinal);
    }
}
