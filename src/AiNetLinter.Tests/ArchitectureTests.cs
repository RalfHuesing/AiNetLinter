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
                AllowOutParameters = false
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
}
