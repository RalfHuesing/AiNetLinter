#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class TestDetectorSuffixTests
{
    private static Project CreateProjectWithName(string projectName)
    {
        var workspace = new AdhocWorkspace();
        var projectInfo = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Default,
            projectName,
            projectName,
            LanguageNames.CSharp);
        return workspace.AddProject(projectInfo);
    }

    [Theory]
    [InlineData("MyApp.Tests")]
    [InlineData("MyApp.Test")]
    [InlineData("MyApp.IntegrationTests")]
    [InlineData("MyApp.Specs")]
    [InlineData("MyApp.Spec")]
    [InlineData("Tests")]
    [InlineData("MyAppTests")]
    public void DefaultSuffixes_IsRecognizedAsTestProject(string projectName)
    {
        var project = CreateProjectWithName(projectName);
        Assert.True(TestDetector.IsTestProject(project));
    }

    [Theory]
    [InlineData("MyApp")]
    [InlineData("MyApp.Domain")]
    [InlineData("MyApp.Infrastructure")]
    [InlineData("MyApp.Api")]
    public void NonTestProjectName_IsNotRecognized(string projectName)
    {
        var project = CreateProjectWithName(projectName);
        Assert.False(TestDetector.IsTestProject(project));
    }

    [Fact]
    public void CustomSuffixes_OverridesDefault_RecognizesCustomSuffix()
    {
        var project = CreateProjectWithName("MyApp.Scenarios");
        var customSuffixes = new[] { "Scenarios" };
        Assert.True(TestDetector.IsTestProject(project, customSuffixes));
    }

    [Fact]
    public void CustomSuffixes_EmptyList_DoesNotMatchByName()
    {
        var project = CreateProjectWithName("MyApp.Tests");
        var emptySuffixes = Array.Empty<string>();
        Assert.False(TestDetector.IsTestProject(project, emptySuffixes));
    }

    [Fact]
    public void ProjectNameWithDotSuffix_IsRecognized()
    {
        var project = CreateProjectWithName("MyApp.IntegrationTests");
        Assert.True(TestDetector.IsTestProject(project));
    }
}
