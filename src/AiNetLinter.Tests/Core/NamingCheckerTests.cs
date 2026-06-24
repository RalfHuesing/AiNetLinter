#nullable enable

using System.Linq;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class NamingCheckerTests
{
    [Fact]
    public void CheckDummyName_Reports_MyRegexMethodInTestAndProduction()
    {
        var code = """
            using System.Text.RegularExpressions;
            public class TestClass
            {
                private static Regex MyRegex() => null!;
                private static readonly Regex s_myRegex2 = MyRegex();
                
                public void SomeMethod()
                {
                    var matches = MyRegex().Matches("abc");
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        
        // 1. Check in production code file
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSemanticNaming = true } };
        var violationsProd = LinterAnalyzer.Analyze("TestClass.cs", model, config, isTestFile: false);
        
        var dummyViolationsProd = violationsProd.Where(v => v.RuleName == "EnforceSemanticNaming").ToList();
        Assert.Equal(2, dummyViolationsProd.Count);
        Assert.Contains(dummyViolationsProd, v => v.Details.Contains("MyRegex"));
        Assert.Contains(dummyViolationsProd, v => v.Details.Contains("s_myRegex2"));

        // 2. Check in test file
        var violationsTest = LinterAnalyzer.Analyze("TestClassTests.cs", model, config, isTestFile: true);
        var dummyViolationsTest = violationsTest.Where(v => v.RuleName == "EnforceSemanticNaming").ToList();
        Assert.Equal(2, dummyViolationsTest.Count);
    }

    [Fact]
    public void CheckDummyName_Reports_NewMethodAndClass1()
    {
        var code = """
            public class Class1
            {
                public void NewMethod()
                {
                    int temp1 = 42;
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSemanticNaming = true } };
        var violations = LinterAnalyzer.Analyze("Class1.cs", model, config, isTestFile: false);

        var dummyViolations = violations.Where(v => v.RuleName == "EnforceSemanticNaming").ToList();
        Assert.Equal(2, dummyViolations.Count); // Class1 and NewMethod
        Assert.Contains(dummyViolations, v => v.Details.Contains("Class1"));
        Assert.Contains(dummyViolations, v => v.Details.Contains("NewMethod"));
    }

    [Fact]
    public void CheckDummyName_NoViolation_ForSemanticNames()
    {
        var code = """
            using System.Text.RegularExpressions;
            public class EmailValidator
            {
                private static Regex EmailFormatRegex() => null!;
                private static readonly Regex s_emailRegex = EmailFormatRegex();
                
                public void ValidateEmail(string emailAddress)
                {
                    var isMatch = s_emailRegex.IsMatch(emailAddress);
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSemanticNaming = true } };
        var violations = LinterAnalyzer.Analyze("EmailValidator.cs", model, config, isTestFile: false);

        var dummyViolations = violations.Where(v => v.RuleName == "EnforceSemanticNaming").ToList();
        Assert.Empty(dummyViolations);
    }
}
