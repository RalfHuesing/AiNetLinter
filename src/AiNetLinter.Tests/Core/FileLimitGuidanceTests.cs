#nullable enable

using System.Linq;
using System.Text;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

/// <summary>
/// Prüft, dass die MaxLineCount-Guidance den durchschnittlichen CC der Methoden in der Datei berücksichtigt
/// und zwischen "strukturell flach" und "lang UND komplex" unterscheidet.
/// </summary>
public sealed class FileLimitGuidanceTests
{
    private static LinterConfig LowLineLimitConfig(int maxLineCount = 10)
        => new()
        {
            Global = new GlobalConfig { EnforceNullableEnable = false },
            Metrics = new MetricsConfig
            {
                MaxLineCount = maxLineCount,
                CompoundSuppressions = new System.Collections.Generic.List<CompoundSuppression>()
            }
        };

    private static string BuildFlatFile(int lineCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public class Gen {");
        var methodCount = (lineCount / 3) + 1;
        for (int i = 0; i < methodCount; i++)
            sb.AppendLine($"    public int Method{i}() => {i};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildComplexFile(int methodCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public class Complex {");
        for (int i = 0; i < methodCount; i++)
        {
            sb.AppendLine($"    public int M{i}(int a, int b) {{");
            sb.AppendLine($"        if (a > {i}) {{");
            sb.AppendLine($"            if (b > {i}) {{");
            sb.AppendLine($"                return a + b + {i};");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine($"        return {i};");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string BuildNoMethodsFile(int lineCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class Constants {");
        for (int i = 0; i < lineCount; i++)
            sb.AppendLine($"    public const int V{i} = {i};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    [Fact]
    public void FileWithNoMethods_ReturnsGenericGuidance()
    {
        var code = BuildNoMethodsFile(15);
        var (_, model) = TestHelper.ParseCode(code);
        var config = LowLineLimitConfig(maxLineCount: 5);

        var violations = LinterAnalyzer.Analyze("Gen.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxLineCount");

        Assert.NotNull(violation);
        Assert.Contains("logisch in sich geschlossene Klassen", violation.Guidance);
        Assert.DoesNotContain("strukturell flach", violation.Guidance);
        Assert.DoesNotContain("lang UND komplex", violation.Guidance);
    }

    [Fact]
    public void FlatFile_WithSimpleMethods_ReturnsStructurallyFlatGuidance()
    {
        // Datei mit vielen trivialen Methoden (CC=1) → "strukturell flach"
        var code = BuildFlatFile(lineCount: 30);
        var (_, model) = TestHelper.ParseCode(code);
        var config = LowLineLimitConfig(maxLineCount: 5);

        var violations = LinterAnalyzer.Analyze("Gen.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxLineCount");

        Assert.NotNull(violation);
        Assert.Contains("strukturell flach", violation.Guidance);
        Assert.Contains("Ø CC=", violation.Guidance);
        Assert.DoesNotContain("lang UND komplex", violation.Guidance);
    }

    [Fact]
    public void ComplexFile_WithBranchingMethods_ReturnsLongAndComplexGuidance()
    {
        // Datei mit verzweigten Methoden (avg CC > 2) → "lang UND komplex"
        var code = BuildComplexFile(methodCount: 5);
        var (_, model) = TestHelper.ParseCode(code);
        var config = LowLineLimitConfig(maxLineCount: 5);

        var violations = LinterAnalyzer.Analyze("Complex.cs", model, config);
        var violation = violations.FirstOrDefault(v => v.RuleName == "MaxLineCount");

        Assert.NotNull(violation);
        Assert.Contains("lang UND komplex", violation.Guidance);
        Assert.Contains("Ø CC=", violation.Guidance);
        Assert.DoesNotContain("strukturell flach", violation.Guidance);
    }
}
