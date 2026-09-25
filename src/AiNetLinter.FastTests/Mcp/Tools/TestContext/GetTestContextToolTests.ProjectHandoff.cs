#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

public sealed partial class GetTestContextToolTests
{
    [Fact]
    public async Task ExecuteAsync_TestClassHandoffResolvesToItsDeclaringTestProject()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\TestContextProjectHandoff.slnx",
            new ProjectSpec("Application", [
                ("Calculator.cs", """
                    namespace HandoffFixture;

                    public sealed class Calculator
                    {
                        public int Add(int left, int right) => left + right;
                    }
                    """)
            ], VirtualProjectDirectory: "src/Application"),
            new ProjectSpec("First.Tests", [
                ("CalculatorTests.cs", """
                    namespace Shared.Testing;

                    public sealed class SharedTests
                    {
                        [Xunit.Fact]
                        public void AddIsUsedByFirstProject()
                        {
                            _ = new HandoffFixture.Calculator().Add(1, 2);
                        }

                        public void FirstProjectOnlyMember() { }
                    }
                    """)
            ], ProjectReferences: ["Application"], VirtualProjectDirectory: "tests/First.Tests"),
            new ProjectSpec("Second.Tests", [
                ("CalculatorTests.cs", """
                    namespace Shared.Testing;

                    public sealed class SharedTests
                    {
                        [Xunit.Fact]
                        public void AddIsUsedBySecondProject()
                        {
                            _ = new HandoffFixture.Calculator().Add(3, 4);
                        }

                        public void SecondProjectOnlyMember() { }
                    }
                    """)
            ], ProjectReferences: ["Application"], VirtualProjectDirectory: "tests/Second.Tests"));
        var state = CreateServer(scenario.Solution);

        var context = await GetTestContextTool.ExecuteAsync(
            state, new TestContextOptions("HandoffFixture.Calculator.Add"), CancellationToken.None);
        var contextText = Assert.IsType<TextContentBlock>(Assert.Single(context.Content)).Text;
        Assert.NotEqual(true, context.IsError);

        var candidates = Regex.Matches(
                contextText,
                @"- `(?<path>[^`]+)`[^\r\n]*\r?\n\s+- testKlassen: `SharedTests` \(handoffId: `(?<id>h:[^`]+)`\)",
                RegexOptions.CultureInvariant)
            .Select(match => (Path: match.Groups["path"].Value, Id: match.Groups["id"].Value))
            .ToArray();

        Assert.Equal(2, candidates.Length);
        Assert.Contains(candidates, candidate => candidate.Path.Contains("First.Tests", StringComparison.Ordinal));
        Assert.Contains(candidates, candidate => candidate.Path.Contains("Second.Tests", StringComparison.Ordinal));

        foreach (var candidate in candidates)
        {
            var structure = await GetClassStructureTool.ExecuteAsync(
                state, candidate.Id, "name", CancellationToken.None);
            var structureText = Assert.IsType<TextContentBlock>(Assert.Single(structure.Content)).Text;

            Assert.NotEqual(true, structure.IsError);
            Assert.DoesNotContain("AMBIGUOUS_SYMBOL", structureText, StringComparison.Ordinal);
            Assert.Contains("SharedTests", structureText, StringComparison.Ordinal);

            if (candidate.Path.Contains("First.Tests", StringComparison.Ordinal))
            {
                Assert.Contains("FirstProjectOnlyMember", structureText, StringComparison.Ordinal);
                Assert.DoesNotContain("SecondProjectOnlyMember", structureText, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("SecondProjectOnlyMember", structureText, StringComparison.Ordinal);
                Assert.DoesNotContain("FirstProjectOnlyMember", structureText, StringComparison.Ordinal);
            }
        }
    }
}
