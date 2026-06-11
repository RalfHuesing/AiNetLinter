using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class ArchitectureTests
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
                EnforceSemanticNaming = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10, // Small line count to test lines limit
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5
            }
        };
    }

    [Fact]
    public void Analyze_WithCompliantCode_ReturnsZeroViolations()
    {
        // Arrange
        const string sourceCode = @"
namespace CompliantNamespace;

public sealed class GoodClass
{
    public void Calculate(int a, string b)
    {
        // Clean implementation
    }
}";
        var config = CreateDefaultConfig();

        // Act
        var violations = LinterAnalyzer.Analyze("TestFile.cs", sourceCode, config);

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithViolations_ReturnsExpectedViolations()
    {
        // Arrange
        const string sourceCode = @"
namespace BadNamespace;

public class NonSealedClass
{
    public void DoSomething(int a, string b, double c, bool d)
    {
        dynamic badVariable = 42;
    }

    public void OutMethod(out int value)
    {
        value = 10;
    }
}";
        var config = CreateDefaultConfig();

        // Act
        var violations = LinterAnalyzer.Analyze("TestFile.cs", sourceCode, config);

        // Assert
        Assert.NotEmpty(violations);
        
        // Assert specific violations exist
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceSealedClasses));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxMethodParameterCount));
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.AllowDynamic));
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.AllowOutParameters));
    }

    [Fact]
    public void Analyze_WithFileTooLong_ReturnsLineCountViolation()
    {
        // Arrange
        const string sourceCode = @"
// Line 1
// Line 2
// Line 3
// Line 4
// Line 5
// Line 6
// Line 7
// Line 8
// Line 9
// Line 10
// Line 11
// Line 12
";
        var config = CreateDefaultConfig(); // MaxLineCount is 10

        // Act
        var violations = LinterAnalyzer.Analyze("LongFile.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxLineCount));
    }

    [Fact]
    public void Analyze_WithHighCyclomaticComplexity_ReturnsCyclomaticComplexityViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace ComplexNamespace;

public sealed class ComplexClass
{
    public void Verify(int x)
    {
        if (x > 1) {}
        if (x > 2) {}
        if (x > 3) {}
        if (x > 4) {}
        if (x > 5) {}
    }
}";
        var config = CreateDefaultConfig();

        // Act
        var violations = LinterAnalyzer.Analyze("ComplexFile.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity));
    }

    [Fact]
    public void Analyze_WithHighCognitiveComplexity_ReturnsCognitiveComplexityViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace CognitiveNamespace;

public sealed class CognitiveClass
{
    public void Nesting(int a, int b, int c)
    {
        if (a > 0)
        {
            if (b > 0)
            {
                if (c > 0)
                {
                }
            }
        }
    }
}";
        var config = CreateDefaultConfig();

        // Act
        var violations = LinterAnalyzer.Analyze("CognitiveFile.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxCognitiveComplexity));
    }

    [Fact]
    public void ParseSlnx_WithValidXml_ReturnsProjects()
    {
        // Arrange
        const string slnxContent = @"<Solution>
  <Project Path=""src/AiNetLinter/AiNetLinter.csproj"" />
  <Project Path=""src/AiNetLinter.Tests/AiNetLinter.Tests.csproj"" />
</Solution>";
        var tempSlnx = Path.GetTempFileName() + ".slnx";
        File.WriteAllText(tempSlnx, slnxContent);

        try
        {
            // Act
            var projects = LinterEngine.ParseSlnx(tempSlnx);

            // Assert
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
        // Arrange
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
            // Act
            var projects = LinterEngine.ParseSln(tempSln);

            // Assert
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
        // Arrange
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
            // Act
            var excluded = LinterEngine.GetExcludedFiles(tempCsproj, "/MockDir");

            // Assert
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
    public void Analyze_WithForbiddenNamespaceDependency_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace MyApp.Features.Invoicing;

using MyApp.Features.Customer;

public sealed class InvoiceService {}";
        var config = CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule
                {
                    SourceNamespace = "MyApp.Features.Invoicing",
                    TargetNamespace = "MyApp.Features.Customer"
                }
            }
        };

        // Act
        var violations = LinterAnalyzer.Analyze("InvoiceService.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }

    [Fact]
    public void Analyze_WithValueObjectNotRecordOrStruct_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace Domain;

public class EmailValueObject
{
    public string Address { get; }
}";
        var config = CreateDefaultConfig(); // EnforceValueObjectContracts is true by default

        // Act
        var violations = LinterAnalyzer.Analyze("Email.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceValueObjectContracts));
        Assert.Contains(violations, v => v.Details.Contains("ist als 'class' deklariert"));
    }

    [Fact]
    public void Analyze_WithValueObjectMutableProperty_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace Domain;

public sealed record MoneyValueObject
{
    public decimal Amount { get; set; }
}";
        var config = CreateDefaultConfig();

        // Act
        var violations = LinterAnalyzer.Analyze("Money.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceValueObjectContracts));
        Assert.Contains(violations, v => v.Details.Contains("veränderbare Eigenschaft"));
    }

    [Fact]
    public void Run_WithHighlyRelevantClassMissingTestClass_ReturnsSentinelViolation()
    {
        // Arrange
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

        var config = CreateDefaultConfig(); // EnableTestSentinel is true by default

        try
        {
            // Act
            var engine = new LinterEngine(config);
            var violations = engine.Run(tempDir);

            // Assert
            Assert.Contains(violations, v => v.RuleName == "StaticTestSentinel");
            Assert.Contains(violations, v => v.Details.Contains("Klasse 'ComplexDomainService' hat eine hohe Relevanz"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Analyze_WithNonPascalCaseTypeName_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace Test;
public sealed class badClass {}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforcePascalCase = true }
        };

        // Act
        var violations = LinterAnalyzer.Analyze("Source.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforcePascalCase));
    }

    [Fact]
    public void Analyze_WithPublicMethodMissingXmlDoc_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good doc.
/// </summary>
public sealed class GoodClass
{
    public void MissingDocMethod() {}
}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceXmlDocumentation = true }
        };

        // Act
        var violations = LinterAnalyzer.Analyze("Source.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceXmlDocumentation));
    }

    [Fact]
    public void Analyze_WithGenericParameterName_ReturnsViolation()
    {
        // Arrange
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good class.
/// </summary>
public sealed class GoodClass
{
    /// <summary>
    /// Method with bad param name.
    /// </summary>
    public void Save(string data) {}
}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceSemanticNaming = true }
        };

        // Act
        var violations = LinterAnalyzer.Analyze("Source.cs", sourceCode, config);

        // Assert
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceSemanticNaming));
    }
}
