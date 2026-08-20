#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.FastTests.Core.Checkers;

// @covers LinterAnalyzer
[Trait("Category", "Unit")]
public sealed class NamespaceDirectoryMappingTests
{
    private static (SyntaxTree, SemanticModel) GetSemanticContext(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return (tree, compilation.GetSemanticModel(tree));
    }

    private static Config CreateDefaultConfig()
    {
        return TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforceValueObjectContracts = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            }
        };
    }

    [Fact]
    public void ModeExact_WithMatchingPath_ReturnsNoViolations()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Features/Admin/Users/UserService.cs", """
            namespace MyApp.Features.Admin.Users;
            public class UserService {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "exact",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Empty(violations);
    }

    [Fact]
    public void ModeExact_WithMismatchingPath_ReturnsViolation()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Features/Admin/Users/UserService.cs", """
            namespace MyApp.Features.Users;
            public class UserService {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "exact",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Single(violations);
        Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
    }

    [Fact]
    public void ModeSuffixMatch_WithIgnoreSegments_ReturnsNoViolations()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Handlers/Domains/Kalender/KalenderHandler.cs", """
            namespace MyApp.Handlers.Kalender;
            public class KalenderHandler {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "suffix-match",
                NamespaceDirectoryMappingIgnorePathSegments = new[] { "Domains" },
                NamespaceDirectoryMappingRequiredTrailingSegments = 2
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Empty(violations);
    }

    [Fact]
    public void ModeSuffixMatch_WithMismatch_ReturnsViolation()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Handlers/Domains/Firmenkalender/KalenderHandler.cs", """
            namespace MyApp.Handlers.Kalender;
            public class KalenderHandler {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "suffix-match",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>(),
                NamespaceDirectoryMappingRequiredTrailingSegments = 2
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Single(violations);
        Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
    }

    [Fact]
    public void ModeContainsAll_MatchesAllSegmentsOutOfOrder_ReturnsNoViolations()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Features/Admin/Users/UserService.cs", """
            namespace MyApp.Users.Admin.Features;
            public class UserService {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "contains-all",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Empty(violations);
    }

    [Fact]
    public void ModeContainsAll_MissingOneSegment_ReturnsViolation()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Features/Admin/Users/UserService.cs", """
            namespace MyApp.Features.Users;
            public class UserService {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "contains-all",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        Assert.Single(violations);
        Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
    }

    [Fact]
    public void EdgeCase_AllSegmentsIgnored_ReturnsNoViolations()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("src/Source/SomeClass.cs", """
            namespace MyApp.CustomNamespace;
            public class SomeClass {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "exact",
                NamespaceDirectoryMappingIgnorePathSegments = new[] { "src", "Source" }
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        // Since all parts "src" and "Source" are ignored, relevantParts is empty, and we return immediately without violation.
        Assert.Empty(violations);
    }

    [Fact]
    public void EdgeCase_RequiredTrailingLargerThanRelevantLength_TakesAllSegments()
    {
        using var tempDir = TestTempDirectory.Create("ns-map-");
        tempDir.CreateFile("TestProj.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var filePath = tempDir.CreateFile("Features/SomeClass.cs", """
            namespace MyApp.Features;
            public class SomeClass {}
            """);

        var config = CreateDefaultConfig() with
        {
            Global = CreateDefaultConfig().Global with
            {
                EnforceNamespaceDirectoryMapping = true,
                NamespaceDirectoryMappingMode = "suffix-match",
                NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>(),
                NamespaceDirectoryMappingRequiredTrailingSegments = 5
            }
        };

        var (_, model) = GetSemanticContext(File.ReadAllText(filePath));
        var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

        // requiredTrailing is 5, but we only have 1 segment ("Features"). It should match since the namespace ends with "Features".
        Assert.Empty(violations);
    }
}
