using System.Collections.Generic;
using System.Linq;
using Xunit;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Tests.Core;

public sealed class AIContextFootprintDeduplicationTests
{
    private static ClassInfo MakePartial(string name, string filePath, string? project = null) =>
        new()
        {
            Name = name,
            FilePath = filePath,
            LineNumber = 1,
            MaxCognitiveComplexity = 0,
            InheritanceDepth = 0,
            AIContextFootprint = 9000,
            HasTestMethods = false,
            IsPartial = true,
            ProjectName = project,
        };

    private static ClassInfo MakeNonPartial(string name, string filePath, string? project = null) =>
        new()
        {
            Name = name,
            FilePath = filePath,
            LineNumber = 1,
            MaxCognitiveComplexity = 0,
            InheritanceDepth = 0,
            AIContextFootprint = 9000,
            HasTestMethods = false,
            IsPartial = false,
            ProjectName = project,
        };

    [Fact]
    public void PartialClass_MultipleFiles_DeduplicatedToOne()
    {
        var classes = new[]
        {
            MakePartial("SchedulerJsInterop", "Scheduler.cs"),
            MakePartial("SchedulerJsInterop", "Scheduler.Commands.cs"),
            MakePartial("SchedulerJsInterop", "Scheduler.Groups.cs"),
        };

        var result = PostAnalysisChecks.DeduplicatePartialClasses(classes).ToList();

        Assert.Single(result);
    }

    [Fact]
    public void PartialClass_ReturnsAlphabeticallyFirstFile()
    {
        var classes = new[]
        {
            MakePartial("MyClass", "Z_File.cs"),
            MakePartial("MyClass", "A_File.cs"),
            MakePartial("MyClass", "M_File.cs"),
        };

        var result = PostAnalysisChecks.DeduplicatePartialClasses(classes).ToList();

        Assert.Single(result);
        Assert.Equal("A_File.cs", result[0].FilePath);
    }

    [Fact]
    public void NonPartialClass_MultipleWithSameName_BothReported()
    {
        var classes = new[]
        {
            MakeNonPartial("Helper", "NamespaceA/Helper.cs"),
            MakeNonPartial("Helper", "NamespaceB/Helper.cs"),
        };

        var result = PostAnalysisChecks.DeduplicatePartialClasses(classes).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void PartialClasses_DifferentProjects_BothReported()
    {
        var classes = new[]
        {
            MakePartial("DataTable", "DataTable.cs", project: "ProjectA"),
            MakePartial("DataTable", "DataTable.razor.cs", project: "ProjectB"),
        };

        var result = PostAnalysisChecks.DeduplicatePartialClasses(classes).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MixedPartialAndNonPartial_HandledCorrectly()
    {
        var classes = new[]
        {
            MakePartial("BigService", "BigService.cs"),
            MakePartial("BigService", "BigService.Part2.cs"),
            MakeNonPartial("SmallHelper", "SmallHelper.cs"),
            MakeNonPartial("AnotherHelper", "AnotherHelper.cs"),
        };

        var result = PostAnalysisChecks.DeduplicatePartialClasses(classes).ToList();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Name == "BigService");
        Assert.Contains(result, c => c.Name == "SmallHelper");
        Assert.Contains(result, c => c.Name == "AnotherHelper");
    }
}
