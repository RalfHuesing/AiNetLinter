using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

// @covers LinterAnalyzer
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

    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
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
                EnforceNoSilentCatch = false,
                EnforceNoVariableShadowing = false,
                EnforceReadonlyParameters = false,
                EnforceReadonlyFields = false,
                EnforceNoMagicValues = false,
                EnforceExplicitStateImmutability = false,
                EnforceStrictBoundaryForBusinessLogic = false,
                PreventContextDependentOverloads = false,
                RequireExplicitTruncationHandling = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig()
        };
    }

    [Fact]
    public void ModeExact_WithMatchingPath_ReturnsNoViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Features", "Admin", "Users");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "UserService.cs");

            const string source = """
                namespace MyApp.Features.Admin.Users;
                public class UserService {}
                """;

            var config = CreateDefaultConfig() with
            {
                Global = CreateDefaultConfig().Global with
                {
                    EnforceNamespaceDirectoryMapping = true,
                    NamespaceDirectoryMappingMode = "exact",
                    NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
                }
            };

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Empty(violations);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ModeExact_WithMismatchingPath_ReturnsViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Features", "Admin", "Users");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "UserService.cs");

            const string source = """
                namespace MyApp.Features.Users;
                public class UserService {}
                """;

            var config = CreateDefaultConfig() with
            {
                Global = CreateDefaultConfig().Global with
                {
                    EnforceNamespaceDirectoryMapping = true,
                    NamespaceDirectoryMappingMode = "exact",
                    NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
                }
            };

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Single(violations);
            Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ModeSuffixMatch_WithIgnoreSegments_ReturnsNoViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Handlers", "Domains", "Kalender");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "KalenderHandler.cs");

            const string source = """
                namespace MyApp.Handlers.Kalender;
                public class KalenderHandler {}
                """;

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

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Empty(violations);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ModeSuffixMatch_WithMismatch_ReturnsViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Handlers", "Domains", "Firmenkalender");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "KalenderHandler.cs");

            const string source = """
                namespace MyApp.Handlers.Kalender;
                public class KalenderHandler {}
                """;

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

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Single(violations);
            Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ModeContainsAll_MatchesAllSegmentsOutOfOrder_ReturnsNoViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Features", "Admin", "Users");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "UserService.cs");

            const string source = """
                namespace MyApp.Users.Admin.Features;
                public class UserService {}
                """;

            var config = CreateDefaultConfig() with
            {
                Global = CreateDefaultConfig().Global with
                {
                    EnforceNamespaceDirectoryMapping = true,
                    NamespaceDirectoryMappingMode = "contains-all",
                    NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
                }
            };

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Empty(violations);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ModeContainsAll_MissingOneSegment_ReturnsViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Features", "Admin", "Users");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "UserService.cs");

            const string source = """
                namespace MyApp.Features.Users;
                public class UserService {}
                """;

            var config = CreateDefaultConfig() with
            {
                Global = CreateDefaultConfig().Global with
                {
                    EnforceNamespaceDirectoryMapping = true,
                    NamespaceDirectoryMappingMode = "contains-all",
                    NamespaceDirectoryMappingIgnorePathSegments = Array.Empty<string>()
                }
            };

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            Assert.Single(violations);
            Assert.Equal("EnforceNamespaceDirectoryMapping", violations.First().RuleName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EdgeCase_AllSegmentsIgnored_ReturnsNoViolations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "src", "Source");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "SomeClass.cs");

            const string source = """
                namespace MyApp.CustomNamespace;
                public class SomeClass {}
                """;

            var config = CreateDefaultConfig() with
            {
                Global = CreateDefaultConfig().Global with
                {
                    EnforceNamespaceDirectoryMapping = true,
                    NamespaceDirectoryMappingMode = "exact",
                    NamespaceDirectoryMappingIgnorePathSegments = new[] { "src", "Source" }
                }
            };

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            // Since all parts "src" and "Source" are ignored, relevantParts is empty, and we return immediately without violation.
            Assert.Empty(violations);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EdgeCase_RequiredTrailingLargerThanRelevantLength_TakesAllSegments()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "TestProj.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            var subDir = Path.Combine(tempDir, "Features");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "SomeClass.cs");

            const string source = """
                namespace MyApp.Features;
                public class SomeClass {}
                """;

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

            var (tree, model) = GetSemanticContext(source);
            var violations = LinterAnalyzer.Analyze(filePath, model, config, isTestFile: false);

            // requiredTrailing is 5, but we only have 1 segment ("Features"). It should match since the namespace ends with "Features".
            Assert.Empty(violations);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
