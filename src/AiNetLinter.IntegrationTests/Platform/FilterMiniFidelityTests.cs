#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
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
/// Laedt die Disk-Fixture bewusst direkt ueber <see cref="IsolatedFixtureLease"/> statt ueber einen
/// geteilten Host, weil hier keine Wiederverwendung ueber mehrere Testklassen ansteht.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FilterMiniFidelityTests
{
    [Fact]
    public async Task DiskAndInMemoryFilterMini_MatchStructurallyAndBehaviorally()
    {
        var root = FindSolutionRoot();
        IsolatedFixtureLease? lease = null;
        SourceFileCatalog? catalog = null;
        RoslynTestSolution? inMemory = null;
        try
        {
            lease = IsolatedFixtureLease.CopyFixture(root, "FilterMini");
            catalog = await SourceFileCatalog.LoadAsync(lease.RootPath);
            inMemory = RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs());

            AssertProjectNamesMatch(catalog.Solution, inMemory.Solution);
            AssertDocumentCountsMatch(catalog.Solution, inMemory.Solution);
            AssertNullableContextMatches(catalog.Solution, inMemory.Solution);
            AssertTestProjectDetectionMatches(catalog.Solution, inMemory.Solution);
            await AssertWidgetDescribeReturnTypeMatchesAsync(catalog.Solution, inMemory.Solution);
        }
        finally
        {
            inMemory?.Dispose();
            catalog?.Dispose();
            lease?.Dispose();
        }
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
        Assert.False(TestProjectDetector.IsTestProject(GetProject(disk, "FilterMini")));
        Assert.True(TestProjectDetector.IsTestProject(GetProject(disk, "FilterMini.Tests")));
        Assert.True(TestProjectDetector.IsTestProject(GetProject(inMemory, "FilterMini.Tests")));

        // In-memory-Projekte teilen sich CoreReferences aus dem laufenden Testhost-AppDomain
        // (RoslynTestSolutionFactory), der selbst xunit referenziert -- die referenzbasierte
        // Erkennung schlaegt dadurch fuer jedes In-Memory-Projekt an, unabhaengig vom
        // tatsaechlichen Testprojektstatus. Nur die Namenssuffix-Erkennung ist im In-Memory-Fall
        // aussagekraeftig; die referenzbasierte Erkennung wird ausschliesslich in der Disk-Welt
        // sinnvoll geprueft (siehe Zeilen oben).
        Assert.True(TestProjectDetector.IsTestProject(GetProject(inMemory, "FilterMini")));
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

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }
}
