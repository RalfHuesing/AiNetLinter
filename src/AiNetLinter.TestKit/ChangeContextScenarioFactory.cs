#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.TestKit;

/// <summary>
/// Neutrale Mehrprojekt-Szenario-Fixture fuer die diff-bezogene change-context-Kette:
/// zwei Produktionsprojekte (<c>App.Core</c> → <c>App</c>) plus ein Testprojekt
/// (<c>App.Tests</c>, xUnit-aehnliche Attribute ohne echtes Paket). Der Diff aendert zwei
/// Methoden in zwei Dateien — <c>App.OrderService.PlaceAsync</c> (public, mit Call-Sites)
/// und <c>App.Core.AuditLogger.LogInternal</c> (private, ohne externe Aufrufstellen).
/// Liefert Solution, Symbol-Handles und Hunk-Ranges; erwartete Testtreffer: direkte
/// Invocation und Namenskonvention als getrennte Evidenzarten gegen PlaceAsync,
/// Namenskonvention-Treffer fuer LogInternal. Rein in-memory — kein Dateisystem, kein Git.
/// </summary>
public static class ChangeContextScenarioFactory
{
    public const string CoreProjectName = "App.Core";
    public const string AppProjectName = "App";
    public const string TestsProjectName = "App.Tests";

    public const string AuditLoggerFileName = "AuditLogger.cs";
    public const string OrderServiceFileName = "OrderService.cs";
    public const string InvocationTestsFileName = "OrderServiceInvocationTests.cs";
    public const string OrderServiceTestsFileName = "OrderServiceTests.cs";
    public const string AuditLoggerTestsFileName = "AuditLoggerTests.cs";

    /// <summary>Virtueller Solution-Pfad des In-Memory-Szenarios (Basis der relativen Dokumentpfade).</summary>
    public const string VirtualSolutionFilePath = @"C:\ainetlinter-virtual\ChangeContextScenario.slnx";

    public const int PlaceAsyncBodyLine = 7;
    public const int LogInternalBodyLine = 7;

    public const string TestClassNameWithDirectInvocation = "OrderServiceInvocationTests";
    public const string TestClassNameWithNamingConvention = "OrderServiceTests";
    public const string TestClassNameForPrivateMethod = "AuditLoggerTests";

    /// <summary>Hunk auf die geaenderte Body-Zeile von PlaceAsync (Quelldatei App/OrderService.cs).</summary>
    internal static IReadOnlyList<HunkRange> PlaceAsyncBodyHunk { get; } = [new HunkRange(PlaceAsyncBodyLine, 1)];

    /// <summary>Hunk auf die geaenderte Body-Zeile von LogInternal (Quelldatei App.Core/AuditLogger.cs).</summary>
    internal static IReadOnlyList<HunkRange> LogInternalBodyHunk { get; } = [new HunkRange(LogInternalBodyLine, 1)];

    private const string InvocationTestsSource = """
        namespace App.Tests;

        public class OrderServiceInvocationTests
        {
            [Xunit.Fact]
            public void PlacesOrder_ThroughService()
            {
                var service = new App.OrderService();
                service.PlaceAsync();
            }
        }
        """;

    private const string OrderServiceTestsSource = """
        namespace App.Tests;

        public class OrderServiceTests
        {
            [Xunit.Fact]
            public void PlaceOrder_PersistsDraft()
            {
            }
        }
        """;

    private const string AuditLoggerTestsSource = """
        namespace App.Tests;

        public class AuditLoggerTests
        {
            [Xunit.Fact]
            public void WritesEntry_ForEveryMessage()
            {
            }
        }
        """;

    private const string OrderServiceSource = """
        namespace App;

        public class OrderService
        {
            public void PlaceAsync()
            {
                _ = 0;
            }
        }
        """;

    private const string ChangedOrderServiceSource = """
        namespace App;

        public class OrderService
        {
            public void PlaceAsync()
            {
                _ = 1;
            }
        }
        """;

    private const string AuditLoggerSource = """
        namespace App.Core;

        public class AuditLogger
        {
            private void LogInternal(string message)
            {
                _ = message;
            }
        }
        """;

    private const string ChangedAuditLoggerSource = """
        namespace App.Core;

        public class AuditLogger
        {
            private void LogInternal(string message)
            {
                _ = message.Trim();
            }
        }
        """;

    /// <summary>Baut das In-Memory-Szenario mit virtuellen Pfaden.</summary>
    public static RoslynTestSolution CreateScenario() =>
        RoslynTestSolutionFactory.CreateSolution(VirtualSolutionFilePath, CreateSpecs());

    /// <summary>Baut die Szenario-Solution mit Dokumentpfaden unter dem gegebenen Wurzelverzeichnis.</summary>
    public static RoslynTestSolution CreateSolution(string rootPath) =>
        RoslynTestSolutionFactory.CreateSolution(Path.Combine(rootPath, "ChangeContextScenario.slnx"), CreateSpecs());

    public static ProjectSpec[] CreateSpecs() =>
    [
        new ProjectSpec(CoreProjectName, [(AuditLoggerFileName, AuditLoggerSource)]),
        new ProjectSpec(AppProjectName, [(OrderServiceFileName, OrderServiceSource)], ProjectReferences: [CoreProjectName]),
        new ProjectSpec(
            TestsProjectName,
            [
                (InvocationTestsFileName, InvocationTestsSource),
                (OrderServiceTestsFileName, OrderServiceTestsSource),
                (AuditLoggerTestsFileName, AuditLoggerTestsSource)
            ],
            ProjectReferences: [AppProjectName, CoreProjectName])
    ];

    /// <summary>Die unveraenderten Quelldateien je Projekt (fuer physische Workspace-Varianten).</summary>
    public static IReadOnlyList<(string ProjectName, string FileName, string Content)> GetProductionSources() =>
    [
        (CoreProjectName, AuditLoggerFileName, AuditLoggerSource),
        (AppProjectName, OrderServiceFileName, OrderServiceSource)
    ];

    /// <summary>Die geaenderte Quelldatei je Projekt (uncommittete Body-Aenderung beider Methoden).</summary>
    public static IReadOnlyList<(string ProjectName, string FileName, string Content)> GetChangedProductionSources() =>
    [
        (CoreProjectName, AuditLoggerFileName, ChangedAuditLoggerSource),
        (AppProjectName, OrderServiceFileName, ChangedOrderServiceSource)
    ];

    /// <summary>Loest die beiden geaenderten Methoden als Symbol-Handles aus den Kompilierungen auf.</summary>
    internal static async Task<ScenarioSymbols> ResolveSymbolsAsync(Solution solution, CancellationToken ct = default)
    {
        var coreCompilation = await GetCompilationAsync(solution, CoreProjectName, ct);
        var appCompilation = await GetCompilationAsync(solution, AppProjectName, ct);

        var auditLoggerType = coreCompilation.GetTypeByMetadataName("App.Core.AuditLogger");
        var orderServiceType = appCompilation.GetTypeByMetadataName("App.OrderService");

        return new ScenarioSymbols(
            PlaceAsync: orderServiceType!.GetMembers().OfType<IMethodSymbol>().Single(m => m.Name == "PlaceAsync"),
            LogInternal: auditLoggerType!.GetMembers().OfType<IMethodSymbol>().Single(m => m.Name == "LogInternal"));
    }

    private static async Task<Compilation> GetCompilationAsync(Solution solution, string projectName, CancellationToken ct) =>
        await solution.Projects.Single(p => p.Name == projectName).GetCompilationAsync(ct)
        ?? throw new InvalidOperationException($"Keine Kompilierung fuer Projekt '{projectName}'.");
}
