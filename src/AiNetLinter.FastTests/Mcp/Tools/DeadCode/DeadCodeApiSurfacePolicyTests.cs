#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.Mcp.Server;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Unit")]
public sealed class DeadCodeApiSurfacePolicyTests
{
    [Fact]
    public async Task ScanAsync_ExternalLibraryProtectsEffectivePublicAndProtectedSurface()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ApiSurface.slnx",
            new ProjectSpec("PublicSdk", [("Api.cs", """
                public class PublicApi
                {
                    public void PublicMethod() { }
                    protected void ProtectedMethod() { }
                    protected internal void ProtectedInternalMethod() { }
                    internal void InternalMethod() { }
                    private protected void PrivateProtectedMethod() { }
                }

                internal class InternalContainer
                {
                    public void PublicMemberInInternalType() { }
                }
                """)], VirtualProjectDirectory: "."));
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new DeadCodeConfig { DefaultApiSurface = "external_library" },
        };

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            solution.Solution,
            new DeadCodeAdvisoryOptions(
                Config: config),
            CancellationToken.None);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName is
            "PublicMethod" or "ProtectedMethod" or "ProtectedInternalMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "InternalMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "PrivateProtectedMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "InternalContainer" && symbol.Kind == "class");
    }

    [Fact]
    public async Task ScanAsync_ClosedSolutionIncludesEffectivePublicSurfaceAsCandidate()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ClosedApi.slnx",
            new ProjectSpec("Application", [("Api.cs", "public sealed class PublicApi { public void Unused() { } }")], VirtualProjectDirectory: "."));
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new DeadCodeConfig { DefaultApiSurface = "closed_solution" },
        };

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            solution.Solution,
            new DeadCodeAdvisoryOptions(
                Config: config),
            CancellationToken.None);

        var candidate = Assert.Single(result.DeadSymbols, symbol => symbol.SymbolName == "PublicApi");
        Assert.Equal("class", candidate.Kind);
    }

    [Fact]
    public void ValidateApiSurface_ReportsInvalidPolicyForAllCandidateProjectsInStableOrder()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Preflight.slnx",
            new ProjectSpec("Zeta", [("Zeta.cs", "public sealed class Zeta { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("Alpha", [("Alpha.cs", "public sealed class Alpha { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("AlphaTests", AdditionalReferences: [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)], Documents: [("AlphaTests.cs", "public sealed class AlphaTests { }")], VirtualProjectDirectory: "."));

        var issues = DeadCodeAdvisoryScanner.ValidateApiSurface(
            solution.Solution,
            scopeFiles: null,
            TestHelper.CreateDefaultConfig() with
            {
                DeadCode = new DeadCodeConfig { DefaultApiSurface = "Closed_Solution" },
            });

        Assert.Collection(issues,
            issue => Assert.Equal("Alpha", issue.ProjectName),
            issue => Assert.Equal("Zeta", issue.ProjectName));
        Assert.All(issues, issue => Assert.Equal("Closed_Solution", issue.Value));
    }

    [Fact]
    public void ValidateApiSurface_ReportsUnknownProjectOverrideAsInvalid()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\UnknownApiSurfaceOverride.slnx",
            new ProjectSpec("Alpha", [("Alpha.cs", "public sealed class Alpha { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("Zeta", [("Zeta.cs", "public sealed class Zeta { }")], VirtualProjectDirectory: "."));
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new DeadCodeConfig { DefaultApiSurface = "closed_solution" },
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["Alpha"] = new() { DeadCode = new DeadCodeConfigOverride { ApiSurface = "unknown" } },
            },
        };

        var issue = Assert.Single(DeadCodeAdvisoryScanner.ValidateApiSurface(solution.Solution, null, config));

        Assert.Equal("Alpha", issue.ProjectName);
        Assert.Equal("ProjectOverrides.Alpha.DeadCode.ApiSurface", issue.FieldPath);
        Assert.Equal("unknown", issue.Value);
    }

    [Fact]
    public void ConfigLoader_DefaultsApiSurfaceAndPreservesInvalidPolicies()
    {
        using var directory = TestTempDirectory.Create("dead-code-api-config-");
        var path = directory.CreateFile("ainetlinter-rules.json", "{\"Global\":{},\"Metrics\":{}}");

        var missing = ConfigLoader.TryLoadConfig(path, isRequired: false);

        Assert.Equal("closed_solution", missing!.DeadCode.DefaultApiSurface);
        var serialized = ConfigSyncer.Serialize(missing);
        Assert.Contains("\"DeadCode\"", serialized);

        var invalidPath = directory.CreateFile("invalid.json", "{\"Global\":{},\"Metrics\":{},\"DeadCode\":{\"DefaultApiSurface\":\"unknown\"},\"ProjectOverrides\":{\"Sdk*\":{\"DeadCode\":{\"ApiSurface\":\"unknown\"}}},\"PathOverrides\":{\"src/**\":{\"DeadCode\":{\"ApiSurface\":\"unknown\"}}}}");
        var invalid = ConfigLoader.TryLoadConfig(invalidPath, isRequired: false);

        Assert.Equal("unknown", invalid!.DeadCode.DefaultApiSurface);
        Assert.False(DeadCodeApiSurfacePolicy.IsKnown(invalid.DeadCode.DefaultApiSurface));
        Assert.Equal("unknown", ProjectConfigResolver.ResolveForProject("SdkClient", invalid).DeadCode.DefaultApiSurface);
        Assert.Equal("unknown", invalid.ProjectOverrides["Sdk*"].DeadCode!.ApiSurface);
        Assert.Equal("unknown", invalid.PathOverrides!["src/**"].DeadCode!.ApiSurface);

        var typoPath = directory.CreateFile("typo.json", "{\"Global\":{},\"Metrics\":{},\"DeadCode\":{\"DefaultApiSurface\":\"Closed_Solution\"}}");
        var typo = ConfigLoader.TryLoadConfig(typoPath, isRequired: false);

        Assert.Equal("Closed_Solution", typo!.DeadCode.DefaultApiSurface);
        Assert.False(DeadCodeApiSurfacePolicy.IsKnown(typo.DeadCode.DefaultApiSurface));
    }

    [Fact]
    public async Task Verify_PreflightErrorContainsEveryProjectAndNoPartialGateResult()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyApiSurface.slnx",
            new ProjectSpec("Zeta", [("Zeta.cs", "public sealed class Zeta { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("Alpha", [("Alpha.cs", "public sealed class Alpha { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("AlphaTests", AdditionalReferences: [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)], Documents: [("AlphaTests.cs", "public sealed class AlphaTests { }")], VirtualProjectDirectory: "."));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: TestHelper.CreateDefaultConfig() with
                {
                    DeadCode = new DeadCodeConfig { DefaultApiSurface = "unknown" },
                },
                ReadOnlySolutionSnapshot: solution.Solution)));

        var result = await VerifyTool.ExecuteAsync(server, VerifyScope.Solution, CancellationToken.None);

        var content = Assert.Single(result.Content);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(content).Text;
        Assert.True(result.IsError);
        Assert.Contains("verdict: error", text, System.StringComparison.Ordinal);
        Assert.Contains("code: DEAD_CODE_API_SURFACE_NOT_CONFIGURED", text, System.StringComparison.Ordinal);
        Assert.Contains("projects: [Alpha, Zeta]", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("AlphaTests", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("score:", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("violationCount:", text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("deadCode:", text, System.StringComparison.Ordinal);
    }
}
