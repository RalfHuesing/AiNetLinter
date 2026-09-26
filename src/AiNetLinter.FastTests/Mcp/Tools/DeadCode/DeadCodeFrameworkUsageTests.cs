#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeFrameworkUsageTests
{
    [Fact]
    public async Task RegisteredMiddlewareProtectsEntryButNotOrdinaryMethod()
    {
        using var fixture = Create("""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using System.Threading.Tasks;
            public sealed class Middleware
            {
                public Task InvokeAsync(HttpContext context) => Task.CompletedTask;
                public void Orphan() { }
            }
            public static class Startup
            {
                public static void Configure(IApplicationBuilder app) => app.UseMiddleware<Middleware>();
            }
            """);
        var result = await Scan(fixture);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "InvokeAsync");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task OptionsRegistrationProtectsContractButNotOrdinaryMethod()
    {
        using var fixture = Create("""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Options;
            public sealed class Settings { }
            public sealed class Setup : IConfigureOptions<Settings>
            {
                public void Configure(Settings options) { }
                public void Orphan() { }
            }
            public static class Startup
            {
                public static void Register(IServiceCollection services) => services.ConfigureOptions<Setup>();
            }
            """);
        var result = await Scan(fixture);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ContainerType == "Setup" && entry.SymbolName == "Configure");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    private static RoslynTestSolution Create(string source)
    {
        var assemblies = new[]
        {
            typeof(Microsoft.AspNetCore.Builder.IApplicationBuilder).Assembly,
            typeof(Microsoft.AspNetCore.Builder.UseMiddlewareExtensions).Assembly,
            typeof(Microsoft.AspNetCore.Http.HttpContext).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly,
            typeof(Microsoft.Extensions.DependencyInjection.OptionsServiceCollectionExtensions).Assembly,
            typeof(Microsoft.Extensions.Options.IConfigureOptions<>).Assembly,
            System.Reflection.Assembly.Load("System.Runtime")
        };
        return RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Framework.slnx",
            new ProjectSpec("Host", [("Code.cs", source)], AdditionalReferences: assemblies.Distinct()
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).ToArray()));
    }

    private static async Task<DeadCodeScanResult> Scan(RoslynTestSolution fixture)
    {
        var compilation = await fixture.Solution.Projects.Single().GetCompilationAsync();
        Assert.DoesNotContain(compilation!.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        return await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Method));
    }
}
