using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

public sealed class DiRegistrationHeuristicsTests : IClassFixture<SymbolGraphCatalogFixture>
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public DiRegistrationHeuristicsTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FindRegistrationsAsync_NoRegistrationForType_ReturnsEmpty()
    {
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Catalog.Solution, "Greeter", CancellationToken.None);
        Assert.NotNull(symbol);

        var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(
            _fixture.Catalog.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);

        Assert.Empty(hits);
    }

    [Fact]
    public async Task FindRegistrationsAsync_FindsAddScopedHit_FormatsWithLifestyle()
    {
        var fixture = new DiRegistrationMiniFixtureWorkspace();
        try
        {
            var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
            var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
                catalog.Solution, "IReporter", CancellationToken.None);
            Assert.NotNull(symbol);

            var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(
                catalog.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);

            Assert.Contains(hits, h => h.StartsWith("AddScoped:", StringComparison.Ordinal));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task FindRegistrationsAsync_FindsAddSingletonAndTransient_OrdersByLine()
    {
        var fixture = new DiRegistrationMiniFixtureWorkspace();
        try
        {
            var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
            var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
                catalog.Solution, "IReporter", CancellationToken.None);
            Assert.NotNull(symbol);

            var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(
                catalog.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);

            Assert.NotEmpty(hits);
            Assert.Contains(hits, h => h.StartsWith("AddSingleton:", StringComparison.Ordinal));
            Assert.Contains(hits, h => h.StartsWith("AddTransient:", StringComparison.Ordinal));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task FindRegistrationsAsync_DoesNotMatchAddScopedHelperAsSubstring()
    {
        var fixture = new DiRegistrationMiniFixtureWorkspace();
        try
        {
            var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
            var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
                catalog.Solution, "IReporter", CancellationToken.None);
            Assert.NotNull(symbol);

            var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(
                catalog.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);

            Assert.DoesNotContain(hits, h => h.Contains("MyAddScopedHelper", StringComparison.Ordinal));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task FindRegistrationsAsync_RespectsMaxRegistrationHitsCap()
    {
        var fixture = new DiRegistrationMiniFixtureWorkspace();
        try
        {
            var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
            var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(
                catalog.Solution, "IReporter", CancellationToken.None);
            Assert.NotNull(symbol);

            var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(
                catalog.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);

            Assert.True(hits.Count <= DiRegistrationHeuristics.MaxRegistrationHits);
        }
        finally
        {
            fixture.Dispose();
        }
    }
}

/// <summary>
/// Mini-Fixture mit einem Interface, das auf mehrere Arten DI-registriert ist
/// (AddScoped, AddSingleton, AddTransient) plus eine zusaetzliche "MyAddScopedHelper"-Zeile,
/// um zu verifizieren, dass die \b-Word-Boundary-Regex keine Substring-Treffer erzeugt.
/// </summary>
internal sealed class DiRegistrationMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public DiRegistrationMiniFixtureWorkspace()
        : base("DiRegistrationMini", "ainetlinter-direg-")
    {
        var dir = Path.Combine(RootPath, "src", "DiRegistrationMini");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Program.cs"),
            """
            namespace DiRegistrationMini;

            public interface IReporter { void Report(string s); }
            public class ConsoleReporter : IReporter { public void Report(string s) {} }
            public static class Composition
            {
                public static void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddScoped<IReporter, ConsoleReporter>();
                    services.AddSingleton<IReporter>();
                    services.AddTransient<IReporter>();
                    var MyAddScopedHelper = "not a match";
                }
            }
            """);
        File.WriteAllText(Path.Combine(dir, "DiRegistrationMini.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(RootPath, "DiRegistrationMini.slnx"),
            """
            <Solution>
              <Project Path="src/DiRegistrationMini/DiRegistrationMini.csproj" />
            </Solution>
            """);
    }
}
