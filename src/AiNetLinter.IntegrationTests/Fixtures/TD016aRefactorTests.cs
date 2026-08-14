#nullable enable

using System.Linq;
using System.Reflection;
using Xunit;

namespace AiNetLinter.IntegrationTests.Fixtures;

/// <summary>
/// Strukturelle A3-Sicherung fuer Fixture-Klassen:
/// Workspace-Klassen erben von <see cref="FixtureWorkspace"/> und definieren keine
/// eigenen <c>CopyFixture</c>/<c>IsGeneratedPath</c>/<c>FindSolutionRoot</c>-Helper mehr.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TD016aRefactorTests
{
    [Theory]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace))]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace))]
    public void Workspace_InheritsFromFixtureWorkspace(System.Type workspaceType)
    {
        Assert.True(
            typeof(FixtureWorkspace).IsAssignableFrom(workspaceType),
            $"{workspaceType.Name} erbt nicht von FixtureWorkspace — Fixture-Helper-Regression.");
    }

    [Theory]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "CopyFixture")]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "IsGeneratedPath")]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "FindSolutionRoot")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "CopyFixture")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "IsGeneratedPath")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "FindSolutionRoot")]
    public void Workspace_DoesNotDefineDuplicatedHelper(System.Type workspaceType, string helperName)
    {
        var hasOwnDefinition = workspaceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Any(m => m.Name == helperName && m.DeclaringType == workspaceType);

        Assert.False(
            hasOwnDefinition,
            $"{workspaceType.Name} definiert immer noch eine eigene {helperName}-Methode " +
            "— Fixture-Helper dupliziert statt aus FixtureWorkspace geerbt.");
    }
}
