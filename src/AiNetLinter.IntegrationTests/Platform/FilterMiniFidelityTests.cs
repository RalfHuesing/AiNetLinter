#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.IntegrationTests.Platform;

/// <summary>
/// Struktureller Formvergleich und kleine Verhaltensparität zwischen der echten Disk-Fixture
/// <c>tests/Fixtures/FilterMini/</c> und ihrem In-Memory-Spiegel <see cref="FilterMiniSolutionSpec"/>:
/// belegt, dass dieselbe Quelltext-Spezifikation in beiden Welten dieselbe Solution-Form ergibt.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FilterMiniFidelityTests
{
    [Fact]
    public async Task DiskAndInMemoryFilterMini_MatchStructurallyAndBehaviorally()
    {
        await using var loaded = await LoadedFixture.CreateAsync("FilterMini");
        using var inMemory = RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs());

        AssertProjectNamesMatch(loaded.Solution, inMemory.Solution);
        AssertDocumentCountsMatch(loaded.Solution, inMemory.Solution);
        AssertNullableContextMatches(loaded.Solution, inMemory.Solution);
        AssertTestProjectDetectionMatches(loaded.Solution, inMemory.Solution);
        await AssertWidgetDescribeReturnTypeMatchesAsync(loaded.Solution, inMemory.Solution);
    }

    private static void AssertProjectNamesMatch(Solution disk, Solution inMemory)
    {
        var expected = new[] { "FilterMini", "FilterMini.Tests" };
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), ProjectNames(disk));
        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), ProjectNames(inMemory));
    }

    private static void AssertDocumentCountsMatch(Solution disk, Solution inMemory)
    {
        foreach (var name in new[] { "FilterMini", "FilterMini.Tests" })
        {
            var diskCount = SourceDocumentCount(GetProject(disk, name));
            var inMemoryCount = SourceDocumentCount(GetProject(inMemory, name));
            Assert.Equal(inMemoryCount, diskCount);
        }
    }

    private static void AssertNullableContextMatches(Solution disk, Solution inMemory)
    {
        foreach (var name in new[] { "FilterMini", "FilterMini.Tests" })
        {
            var diskOptions = (CSharpCompilationOptions)GetProject(disk, name).CompilationOptions!;
            var inMemoryOptions = (CSharpCompilationOptions)GetProject(inMemory, name).CompilationOptions!;
            Assert.Equal(inMemoryOptions.NullableContextOptions, diskOptions.NullableContextOptions);
        }
    }

    private static void AssertTestProjectDetectionMatches(Solution disk, Solution inMemory)
    {
        Assert.False(TestDetector.IsTestProject(GetProject(disk, "FilterMini")));
        Assert.True(TestDetector.IsTestProject(GetProject(disk, "FilterMini.Tests")));
        Assert.False(TestDetector.IsTestProject(GetProject(inMemory, "FilterMini")));
        Assert.True(TestDetector.IsTestProject(GetProject(inMemory, "FilterMini.Tests")));
    }

    private static async Task AssertWidgetDescribeReturnTypeMatchesAsync(Solution disk, Solution inMemory)
    {
        var diskReturnType = await GetWidgetDescribeReturnTypeNameAsync(GetProject(disk, "FilterMini"));
        var inMemoryReturnType = await GetWidgetDescribeReturnTypeNameAsync(GetProject(inMemory, "FilterMini"));
        Assert.Equal(inMemoryReturnType, diskReturnType);
        Assert.Equal("String", diskReturnType);
    }

    private static async Task<string> GetWidgetDescribeReturnTypeNameAsync(Project project)
    {
        var compilation = await project.GetCompilationAsync();
        var widgetType = compilation!.GetTypeByMetadataName("FilterMini.Core.Widget");
        var describeMethod = widgetType!.GetMembers("Describe").OfType<IMethodSymbol>().Single();
        return describeMethod.ReturnType.Name;
    }

    private static Project GetProject(Solution solution, string name) =>
        solution.Projects.Single(p => p.Name == name);

    private static string[] ProjectNames(Solution solution) =>
        [.. solution.Projects.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)];

    private static int SourceDocumentCount(Project project) =>
        project.Documents.Count(d => !IsGeneratedFilePath(d.FilePath));

    private static bool IsGeneratedFilePath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        var parts = filePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains("obj", StringComparer.OrdinalIgnoreCase) ||
               parts.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }

}
