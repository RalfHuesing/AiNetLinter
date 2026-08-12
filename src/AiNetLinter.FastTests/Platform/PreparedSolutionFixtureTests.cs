#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Platform;

/// <summary>
/// Vertragstests fuer <see cref="PreparedSolutionFixture"/> (konzept.md §2): belegt mechanisch die
/// drei geforderten Eigenschaften -- lazy Materialisierung pro Szenario, Isolation zwischen
/// Szenarien und Thread-Sicherheit -- statt sie nur im XML-Doc zu behaupten. Erhaelt die Fixture
/// ueber die assembly-weite <c>[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]</c>-Registrierung
/// (siehe <c>Platform/PreparedSolutionAssemblyFixture.cs</c>).
/// </summary>
[Trait("Category", "Component")]
public sealed class PreparedSolutionFixtureTests
{
    private readonly PreparedSolutionFixture fixture;

    public PreparedSolutionFixtureTests(PreparedSolutionFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void GetOrCreate_SameScenarioTwice_ReturnsSameSolutionAndSkipsSecondFactory()
    {
        var scenarioName = $"lazy-{Guid.NewGuid()}";
        var secondFactoryCalled = false;

        var firstSolution = fixture.GetOrCreate(scenarioName, BuildMinimalSolution);
        var secondSolution = fixture.GetOrCreate(scenarioName, () =>
        {
            secondFactoryCalled = true;
            return BuildMinimalSolution();
        });

        Assert.Same(firstSolution, secondSolution);
        Assert.False(secondFactoryCalled);
    }

    [Fact]
    public void GetOrCreate_DifferentScenarioNames_ReturnsDistinctSolutions()
    {
        var scenarioA = $"isolation-a-{Guid.NewGuid()}";
        var scenarioB = $"isolation-b-{Guid.NewGuid()}";

        var solutionA = fixture.GetOrCreate(scenarioA, BuildMinimalSolution);
        var solutionB = fixture.GetOrCreate(scenarioB, BuildMinimalSolution);

        Assert.NotSame(solutionA, solutionB);
    }

    [Fact]
    public async Task GetOrCreate_ConcurrentCallsForNewScenario_InvokesFactoryExactlyOnce()
    {
        var scenarioName = $"thread-safety-{Guid.NewGuid()}";
        var callCount = 0;

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => fixture.GetOrCreate(scenarioName, () =>
            {
                Interlocked.Increment(ref callCount);
                return BuildMinimalSolution();
            })))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, callCount);
    }

    private static RoslynTestSolution BuildMinimalSolution() =>
        RoslynTestSolutionFactory.CreateSolution(new ProjectSpec(
            $"Scenario{Guid.NewGuid():N}",
            [("Probe.cs", "namespace ScenarioProbe; public class Probe {}")]));
}
