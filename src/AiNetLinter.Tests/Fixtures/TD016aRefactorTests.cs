#nullable enable
using System.Linq;
using System.Reflection;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Strukturelle A3-Sicherung fuer
/// Workspace-Klassen tatsaechlich von <see cref="FixtureWorkspaceBase"/> erben und keine
/// eigenen <c>CopyFixture</c>/<c>IsGeneratedPath</c>/<c>FindSolutionRoot</c>-Helper mehr
/// definieren. Verhindert, dass die Refactor-Wirkung versehentlich rueckgaengig gemacht
/// wird —, weil es
/// keine strukturelle Sicherung gab.
/// </summary>
public sealed class TD016aRefactorTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace))]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace))]
    public void Workspace_InheritsFromFixtureWorkspaceBase(System.Type workspaceType)
    {
        Assert.True(
            typeof(FixtureWorkspaceBase).IsAssignableFrom(workspaceType),
            $"{workspaceType.Name} erbt nicht von FixtureWorkspaceBase — Fixture-Helper-Regression.");
    }

    [Theory]
    [Trait("Category", "Unit")]
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
            "\u2014 Fixture-Helper dupliziert statt aus FixtureWorkspaceBase geerbt.");
    }
}
