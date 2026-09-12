#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Architecture;

[Trait("Category", "Integration")]
public sealed class McpContentOnlyProductionArchitectureGuardTests
{
    [Fact]
    public void ProductionCode_ContainsNoStructuredContentReferencesOrContractLiterals()
    {
        var root = SolutionRootLocator.Find();
        var productionDirectory = Path.Combine(root, "src", "AiNetLinter");
        var violations = Directory.EnumerateFiles(productionDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !FileSystemExclusionHelpers.IsGeneratedPath(path))
            .SelectMany(FindStructuredContentReferences)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Production Content-only architecture violations:" + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> FindStructuredContentReferences(string path)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (line.Contains("StructuredContent", StringComparison.Ordinal)
                || line.Contains("structuredContent", StringComparison.Ordinal))
            {
                yield return $"{Path.GetRelativePath(SolutionRootLocator.Find(), path)}:{lineNumber}: {line.Trim()}";
            }
        }
    }
}
