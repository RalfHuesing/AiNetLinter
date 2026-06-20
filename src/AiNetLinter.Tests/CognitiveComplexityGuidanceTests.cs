#nullable enable

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Metrics;

namespace AiNetLinter.Tests;

/// <summary>
/// Prüft, dass CognitiveComplexityGuidance.Build unterschiedliche Guidance liefert
/// je nachdem ob die Methode kurz+dicht oder lang+komplex ist.
/// </summary>
public sealed class CognitiveComplexityGuidanceTests
{
    private static MethodDeclarationSyntax ParseMethod(string methodBody)
    {
        var source = $"class C {{ {methodBody} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
    }

    [Fact]
    public void Build_ComplexityWithinExcessThreshold_ReturnsBaseGuidance()
    {
        var method = ParseMethod("void M(int a) { if (a > 0) { } }");
        var result = CognitiveComplexityGuidance.Build(method, complexity: 5, limit: 4);
        Assert.Contains("Vereinfache verschachtelte Kontrollstrukturen", result);
        Assert.DoesNotContain("kurz", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Extract Method", result);
    }

    [Fact]
    public void Build_ShortDenseMethod_WithNestedIfs_ReturnsNamingGuidance()
    {
        // < 20 code lines, deeply nested ifs, complexity >> limit
        const string source = @"
void Dense(int a, int b, int c) {
    if (a > 0) {
        if (b > 0) {
            if (c > 0) {
                var x = a + b + c;
                if (x > 10) {
                    var y = x * 2;
                }
            }
        }
    }
}";
        var method = ParseMethod(source);
        var result = CognitiveComplexityGuidance.Build(method, complexity: 10, limit: 4);

        Assert.Contains("benannte Properties", result);
        Assert.Contains("Dense", result); // method name in hint
        Assert.DoesNotContain("Extract Method", result);
    }

    [Fact]
    public void Build_ShortDenseMethod_WithoutNestedIfs_ReturnsGuardClauseGuidance()
    {
        // Short method, no if nesting (below nested-if threshold), high CogC
        const string source = @"
void NoIfs(int a) {
    var x = a + 1;
    var y = x + 2;
    var z = y + 3;
}";
        var method = ParseMethod(source);
        var result = CognitiveComplexityGuidance.Build(method, complexity: 12, limit: 4);

        Assert.Contains("Guard-Clauses", result);
        Assert.DoesNotContain("Extract Method", result);
    }

    [Fact]
    public void Build_LongComplexMethod_WithNestedIfs_ReturnsExtractMethodGuidance()
    {
        // >= 20 code lines, nested ifs, complexity >> limit
        var bodyLines = new System.Text.StringBuilder();
        bodyLines.AppendLine("void LongDense(int a, int b, int c) {");
        for (int i = 1; i <= 17; i++)
            bodyLines.AppendLine($"    var v{i} = a + {i};");
        bodyLines.AppendLine("    if (a > 0) {");
        bodyLines.AppendLine("        if (b > 0) {");
        bodyLines.AppendLine("            if (c > 0) {");
        bodyLines.AppendLine("                var x = a + b + c;");
        bodyLines.AppendLine("            }");
        bodyLines.AppendLine("        }");
        bodyLines.AppendLine("    }");
        bodyLines.AppendLine("}");

        var method = ParseMethod(bodyLines.ToString());
        var result = CognitiveComplexityGuidance.Build(method, complexity: 10, limit: 4);

        Assert.Contains("Extract Method", result);
        Assert.Contains("LongDense", result); // method name in hint
        Assert.DoesNotContain("Guard-Clauses", result);
    }

    [Fact]
    public void Build_LongComplexMethod_WithoutNestedIfs_ReturnsGenericExtractMethodGuidance()
    {
        // >= 20 code lines, no nested ifs, complexity >> limit
        var bodyLines = new System.Text.StringBuilder();
        bodyLines.AppendLine("void LongFlat(int a) {");
        for (int i = 1; i <= 22; i++)
            bodyLines.AppendLine($"    var v{i} = a + {i};");
        bodyLines.AppendLine("}");

        var method = ParseMethod(bodyLines.ToString());
        var result = CognitiveComplexityGuidance.Build(method, complexity: 10, limit: 4);

        Assert.Contains("Extract Method", result);
        Assert.DoesNotContain("Guard-Clauses", result);
    }
}
