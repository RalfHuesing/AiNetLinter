#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class DiRegistrationHeuristicsTests
{
    [Fact]
    public async Task FindRegistrationsAsync_NoRegistrationForType_ReturnsEmpty()
    {
        using var fixture = new McpInMemoryTestContext();
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(fixture.Solution, "Greeter", CancellationToken.None);
        var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(fixture.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);
        Assert.Empty(hits);
    }

    [Theory]
    [InlineData("AddScoped:")]
    [InlineData("AddSingleton:")]
    [InlineData("AddTransient:")]
    public async Task FindRegistrationsAsync_Registrations_ReturnExpectedLifestyle(string expected)
    {
        using var scenario = CreateScenario();
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(scenario.Solution, "IReporter", CancellationToken.None);
        var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(scenario.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);
        Assert.Contains(hits, hit => hit.StartsWith(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindRegistrationsAsync_HelperSubstring_IsNotRegistration()
    {
        using var scenario = CreateScenario();
        var (symbol, _) = await FindReferencesTool.ResolveSymbolAsync(scenario.Solution, "IReporter", CancellationToken.None);
        var hits = await DiRegistrationHeuristics.FindRegistrationsAsync(scenario.Solution, (Microsoft.CodeAnalysis.INamedTypeSymbol)symbol!, CancellationToken.None);
        Assert.DoesNotContain(hits, hit => hit.Contains("MyAddScopedHelper", StringComparison.Ordinal));
        Assert.True(hits.Count <= DiRegistrationHeuristics.MaxRegistrationHits);
    }

    private static RoslynTestSolution CreateScenario() => McpInMemoryTestContext.CreateScenario(new ProjectSpec("src", [
        ("DiRegistrationMini/Program.cs", """
            namespace Microsoft.Extensions.DependencyInjection;
            public interface IServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static void AddScoped<TService, TImplementation>(this IServiceCollection services) { }
                public static void AddSingleton<TService>(this IServiceCollection services) { }
                public static void AddTransient<TService>(this IServiceCollection services) { }
            }
            namespace DiRegistrationMini;
            using Microsoft.Extensions.DependencyInjection;
            public interface IReporter { void Report(string value); }
            public class ConsoleReporter : IReporter { public void Report(string value) { } }
            public static class Composition
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddScoped<IReporter, ConsoleReporter>();
                    services.AddSingleton<IReporter>();
                    services.AddTransient<IReporter>();
                    var MyAddScopedHelper = "not a match";
                }
            }
            """)
    ]));
}
