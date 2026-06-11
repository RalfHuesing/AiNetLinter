using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.IO;

namespace AiNetLinter.Tests;

public sealed class LinterEngineTests
{
    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10,
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxInheritanceDepth = 2
            }
        };
    }

    [Fact]
    public void LinterEngine_CanBeInitialized()
    {
        var config = CreateDefaultConfig();
        var engine = new LinterEngine(config);
        Assert.NotNull(engine);
    }

    [Fact]
    public void ParseSlnx_WithValidXml_ReturnsProjects()
    {
        const string slnxContent = @"<Solution>
  <Project Path=""src/AiNetLinter/AiNetLinter.csproj"" />
  <Project Path=""src/AiNetLinter.Tests/AiNetLinter.Tests.csproj"" />
</Solution>";
        var tempSlnx = Path.GetTempFileName() + ".slnx";
        File.WriteAllText(tempSlnx, slnxContent);

        try
        {
            var projects = LinterEngine.ParseSlnx(tempSlnx);

            Assert.Equal(2, projects.Count());
            Assert.Contains(projects, p => p.EndsWith("AiNetLinter.csproj"));
            Assert.Contains(projects, p => p.EndsWith("AiNetLinter.Tests.csproj"));
        }
        finally
        {
            File.Delete(tempSlnx);
        }
    }

    [Fact]
    public void ParseSln_WithValidText_ReturnsProjects()
    {
        const string slnContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""AiNetLinter"", ""src\AiNetLinter\AiNetLinter.csproj"", ""{GUID1}""
Project(""{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"") = ""AiNetLinter.Tests"", ""src\AiNetLinter.Tests\AiNetLinter.Tests.csproj"", ""{GUID2}""
Global
EndGlobal";
        var tempSln = Path.GetTempFileName() + ".sln";
        File.WriteAllText(tempSln, slnContent);

        try
        {
            var projects = LinterEngine.ParseSln(tempSln);

            Assert.Equal(2, projects.Count());
            Assert.Contains(projects, p => p.EndsWith("AiNetLinter.csproj"));
            Assert.Contains(projects, p => p.EndsWith("AiNetLinter.Tests.csproj"));
        }
        finally
        {
            File.Delete(tempSln);
        }
    }

    [Fact]
    public void GetExcludedFiles_WithCompileRemoveOrExclude_FiltersFiles()
    {
        const string csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <Compile Remove=""ExcludedFile.cs"" />
    <Compile Exclude=""subfolder/AnotherExcluded.cs"" />
  </ItemGroup>
</Project>";
        var tempCsproj = Path.GetTempFileName() + ".csproj";
        File.WriteAllText(tempCsproj, csprojContent);

        try
        {
            var excluded = LinterEngine.GetExcludedFiles(tempCsproj, "/MockDir");

            Assert.Equal(2, excluded.Count);
            Assert.Contains(excluded, path => path.EndsWith("ExcludedFile.cs"));
            Assert.Contains(excluded, path => path.EndsWith("AnotherExcluded.cs"));
        }
        finally
        {
            File.Delete(tempCsproj);
        }
    }

    [Fact]
    public void Run_WithHighlyRelevantClassMissingTestClass_ReturnsSentinelViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        const string sourceClass = @"
namespace Domain;

public sealed class ComplexDomainService
{
    public void HighComplexityMethod(int x)
    {
        if (x > 1)
        {
            if (x > 2)
            {
                if (x > 3)
                {
                }
            }
        }
    }
}";
        var sourcePath = Path.Combine(tempDir, "ComplexDomainService.cs");
        File.WriteAllText(sourcePath, sourceClass);

        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true }
        };

        try
        {
            var engine = new LinterEngine(config);
            var violations = engine.Run(tempDir);

            Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
            Assert.Contains(violations, v => v.Details.Contains("Klasse 'ComplexDomainService' hat eine hohe Relevanz"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_WithInheritanceDepthExceeded_ReturnsViolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        const string sourceCode = @"
namespace Test;
/// <summary>
/// Root.
/// </summary>
public class RootClass {}

/// <summary>
/// Parent.
/// </summary>
public class ParentClass : RootClass {}

/// <summary>
/// Child.
/// </summary>
public sealed class ChildClass : ParentClass {}";

        var sourcePath = Path.Combine(tempDir, "Classes.cs");
        File.WriteAllText(sourcePath, sourceCode);

        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        try
        {
            var engine = new LinterEngine(config);
            var violations = engine.Run(tempDir);

            Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_WithDuplicateClassNamesInDifferentNamespaces_NoCrashAndResolvesCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        const string code = @"
namespace Test.N1;
public class MyClass {}

namespace Test.N2;
public class MyClass : Test.N1.MyClass {}
";
        var sourcePath = Path.Combine(tempDir, "DuplicateClasses.cs");
        File.WriteAllText(sourcePath, code);

        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        try
        {
            var engine = new LinterEngine(config);
            var violations = engine.Run(tempDir);
            
            Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_WithConfigurableSentinelThreshold_RespectsConfigValue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        const string sourceClass = @"
namespace Domain;
public sealed class ComplexService
{
        public void MediumComplexityMethod(int x)
        {
            if (x > 1)
            {
                if (x > 2) {}
            }
        }
}";
        var sourcePath = Path.Combine(tempDir, "ComplexService.cs");
        File.WriteAllText(sourcePath, sourceClass);

        var configLow = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 1 }
        };

        var configHigh = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 5 }
        };

        try
        {
            var engineLow = new LinterEngine(configLow);
            var violationsLow = engineLow.Run(tempDir);
            Assert.Contains(violationsLow, v => v.RuleName == "StaticTestSentinel");

            var engineHigh = new LinterEngine(configHigh);
            var violationsHigh = engineHigh.Run(tempDir);
            Assert.Empty(violationsHigh.Where(v => v.RuleName == "StaticTestSentinel"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsNullableEnabledGlobally_WithDirectoryBuildProps_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var subDir = Path.Combine(tempDir, "SubProject");
        Directory.CreateDirectory(subDir);

        const string propsContent = @"<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
        File.WriteAllText(Path.Combine(tempDir, "Directory.Build.props"), propsContent);

        const string csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk""></Project>";
        File.WriteAllText(Path.Combine(subDir, "SubProject.csproj"), csprojContent);

        var classFile = Path.Combine(subDir, "MyClass.cs");
        File.WriteAllText(classFile, "// Content");

        try
        {
            var isEnabled = AnalyzerHelpers.IsNullableEnabledGlobally(classFile);
            Assert.True(isEnabled);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_WithTestFileButNoTestMethods_SentinelFails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        const string sourceClass = @"
namespace Domain;
public sealed class HighlyRelevantService
{
    public void ComplexMethod(int x)
    {
        if (x > 1)
        {
            if (x > 2)
            {
                if (x > 3) {}
            }
        }
    }
}";
        const string testClass = @"
namespace Domain.Tests;
public class HighlyRelevantServiceTests
{
    // Keine Testmethoden mit [Fact] oder [Test]
}";

        File.WriteAllText(Path.Combine(tempDir, "HighlyRelevantService.cs"), sourceClass);
        File.WriteAllText(Path.Combine(tempDir, "HighlyRelevantServiceTests.cs"), testClass);

        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true },
            Metrics = new MetricsConfig { MinCognitiveComplexityForTest = 1 }
        };

        try
        {
            var engine = new LinterEngine(config);
            var violations = engine.Run(tempDir);

            Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
