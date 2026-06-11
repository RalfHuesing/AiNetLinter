using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class LinterAnalyzerTests
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
                EnforceValueObjectContracts = true
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10,
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5
            }
        };
    }

    [Fact]
    public void Analyze_WithValidCode_HasNoViolations()
    {
        const string source = @"
namespace TestNamespace;
public sealed class TestClass
{
    public void Work(int x, int y) {}
}";
        var config = CreateDefaultConfig();
        var violations = LinterAnalyzer.Analyze("Test.cs", source, config);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithForbiddenNamespaceInStatement_ReturnsViolation()
    {
        const string source = @"
namespace MyFeature.Domain;
public sealed class DomainService
{
    public void Run()
    {
        var helper = new MyFeature.Infrastructure.DbHelper();
    }
}";
        var config = CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule { SourceNamespace = "MyFeature.Domain", TargetNamespace = "MyFeature.Infrastructure" }
            }
        };
        var violations = LinterAnalyzer.Analyze("Test.cs", source, config);
        Assert.Contains(violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }
}
