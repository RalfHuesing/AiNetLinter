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
}
