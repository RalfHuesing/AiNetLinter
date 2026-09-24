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
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: DeadCodeKindFilter.Method,
                Config: config),
            CancellationToken.None);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName is
            "PublicMethod" or "ProtectedMethod" or "ProtectedInternalMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "InternalMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "PrivateProtectedMethod");
        Assert.Contains(result.DeadSymbols, symbol => symbol.SymbolName == "PublicMemberInInternalType");
    }

    [Fact]
    public async Task ScanAsync_ClosedSolutionReportsEffectivePublicSurfaceWithLowConfidence()
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
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: DeadCodeKindFilter.Method,
                Config: config),
            CancellationToken.None);

        var candidate = Assert.Single(result.DeadSymbols, symbol => symbol.SymbolName == "Unused");
        Assert.Equal("low", candidate.Confidence);
    }

    [Fact]
    public void ValidateApiSurface_ReportsInvalidPolicyForAllCandidateProjectsInStableOrder()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Preflight.slnx",
            new ProjectSpec("Zeta", [("Zeta.cs", "public sealed class Zeta { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("Alpha", [("Alpha.cs", "public sealed class Alpha { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("AlphaTests", [("AlphaTests.cs", "public sealed class AlphaTests { }")], VirtualProjectDirectory: "."));

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
    public void ConfigLoader_DefaultsApiSurfaceAndMapsLegacyUnknownWhilePreservingInvalidValues()
    {
        using var directory = TestTempDirectory.Create("dead-code-api-config-");
        var path = directory.CreateFile("ainetlinter-rules.json", "{\"Global\":{},\"Metrics\":{}}");

        var missing = ConfigLoader.TryLoadConfig(path, isRequired: false);

        Assert.Equal("closed_solution", missing!.DeadCode.DefaultApiSurface);
        var serialized = ConfigSyncer.Serialize(missing);
        Assert.Contains("\"DeadCode\"", serialized);

        var legacyPath = directory.CreateFile("legacy.json", "{\"Global\":{},\"Metrics\":{},\"DeadCode\":{\"DefaultApiSurface\":\"unknown\"},\"ProjectOverrides\":{\"Sdk*\":{\"DeadCode\":{\"ApiSurface\":\"unknown\"}}}}");
        var legacy = ConfigLoader.TryLoadConfig(legacyPath, isRequired: false);

        Assert.Equal("closed_solution", legacy!.DeadCode.DefaultApiSurface);
        Assert.Equal("closed_solution", ProjectConfigResolver.ResolveForProject("SdkClient", legacy).DeadCode.DefaultApiSurface);
        Assert.True(DeadCodeApiSurfacePolicy.IsKnown(legacy.DeadCode.DefaultApiSurface));

        var invalidPath = directory.CreateFile("invalid.json", "{\"Global\":{},\"Metrics\":{},\"DeadCode\":{\"DefaultApiSurface\":\"Closed_Solution\"}}");
        var invalid = ConfigLoader.TryLoadConfig(invalidPath, isRequired: false);

        Assert.Equal("Closed_Solution", invalid!.DeadCode.DefaultApiSurface);
        Assert.False(DeadCodeApiSurfacePolicy.IsKnown(invalid.DeadCode.DefaultApiSurface));
    }

    [Fact]
    public async Task Verify_PreflightErrorContainsEveryProjectAndNoPartialGateResult()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyApiSurface.slnx",
            new ProjectSpec("Zeta", [("Zeta.cs", "public sealed class Zeta { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("Alpha", [("Alpha.cs", "public sealed class Alpha { }")], VirtualProjectDirectory: "."),
            new ProjectSpec("AlphaTests", [("AlphaTests.cs", "public sealed class AlphaTests { }")], VirtualProjectDirectory: "."));
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: TestHelper.CreateDefaultConfig() with
                {
                    DeadCode = new DeadCodeConfig { DefaultApiSurface = "closed_Soluton" },
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
