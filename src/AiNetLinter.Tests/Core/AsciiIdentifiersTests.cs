#nullable enable

using System.Linq;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class AsciiIdentifiersTests
{
    [Fact]
    public void CheckAscii_Reports_UmlautsInClassAndMethodAndVariable()
    {
        var code = """
            namespace MyNamespace.Valid;
            
            public class BestätigungsService
            {
                private string Straße { get; set; } = "";
                
                public void PrüfeDaten(int größe)
                {
                    var result = 42;
                    int zähler = 0;
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = true } };
        var violations = LinterAnalyzer.Analyze("BestätigungsService.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        
        // Expected violations:
        // 1. Class: BestätigungsService
        // 2. Property: Straße
        // 3. Method: PrüfeDaten
        // 4. Parameter: größe
        // 5. Variable: zähler
        Assert.Equal(5, asciiViolations.Count);
        
        Assert.Contains(asciiViolations, v => v.Details.Contains("BestätigungsService"));
        Assert.Contains(asciiViolations, v => v.Details.Contains("Straße"));
        Assert.Contains(asciiViolations, v => v.Details.Contains("PrüfeDaten"));
        Assert.Contains(asciiViolations, v => v.Details.Contains("größe"));
        Assert.Contains(asciiViolations, v => v.Details.Contains("zähler"));
    }

    [Fact]
    public void CheckAscii_Reports_UmlautsInNamespace()
    {
        var code = """
            namespace MyNamespace.Prüfung;
            
            public class ValidClass
            {
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = true } };
        var violations = LinterAnalyzer.Analyze("ValidClass.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        Assert.Single(asciiViolations);
        Assert.Contains(asciiViolations, v => v.Details.Contains("MyNamespace.Prüfung"));
    }

    [Fact]
    public void CheckAscii_Reports_UmlautsInEnumAndEnumMember()
    {
        var code = """
            namespace MyNamespace.Valid;
            
            public enum BestätigungsStatus
            {
                Pending,
                Ausgeführt
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = true } };
        var violations = LinterAnalyzer.Analyze("Enum.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        Assert.Equal(2, asciiViolations.Count);
        Assert.Contains(asciiViolations, v => v.Details.Contains("BestätigungsStatus"));
        Assert.Contains(asciiViolations, v => v.Details.Contains("Ausgeführt"));
    }

    [Fact]
    public void CheckAscii_NoViolation_ForPureAscii()
    {
        var code = """
            namespace MyNamespace.Valid;
            
            public class BestaetigungsService
            {
                private string Strasse { get; set; } = "";
                
                public void PruefeDaten(int groesse)
                {
                    var result = 42;
                    int zaehler = 0;
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = true } };
        var violations = LinterAnalyzer.Analyze("BestaetigungsService.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        Assert.Empty(asciiViolations);
    }

    [Fact]
    public void CheckAscii_NoViolation_WhenDisabled()
    {
        var code = """
            namespace MyNamespace.Prüfung;
            
            public class BestätigungsService
            {
                private string Straße { get; set; } = "";
                
                public void PrüfeDaten(int größe)
                {
                    var result = 42;
                    int zähler = 0;
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = false } };
        var violations = LinterAnalyzer.Analyze("BestätigungsService.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        Assert.Empty(asciiViolations);
    }

    [Fact]
    public void CheckAscii_NoViolation_ForVerbatimIdentifiers()
    {
        var code = """
            namespace MyNamespace.Valid;
            
            public class VerbatimClass
            {
                public void TestMethod(int @override)
                {
                    int @class = 42;
                }
            }
            """;

        var (tree, model) = TestHelper.ParseCode(code);
        var config = TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceAsciiIdentifiers = true } };
        var violations = LinterAnalyzer.Analyze("VerbatimClass.cs", model, config, isTestFile: false);

        var asciiViolations = violations.Where(v => v.RuleName == "EnforceAsciiIdentifiers").ToList();
        Assert.Empty(asciiViolations);
    }
}
