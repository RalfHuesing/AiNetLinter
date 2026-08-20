#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.IntegrationTests.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Stress")]
public sealed class SourceFileCatalogRegistrationStressTests
{
    [Fact]
    public async Task LoadAsync_TwentyParallelCallsAcrossFixtures_AllSucceed()
    {
        // Funktionaler Nachweis: 20 parallele LoadAsync-Aufrufe auf unterschiedliche
        // Fixture-Pfade muessen ausnahmslos erfolgreich sein.
        var solutionRoot = SolutionRootLocator.Find();
        var fixturePaths = new[]
        {
            Path.Combine(solutionRoot, "tests", "Fixtures", "BaselineMini"),
            Path.Combine(solutionRoot, "tests", "Fixtures", "SymbolGraphMini"),
            Path.Combine(solutionRoot, "tests", "Fixtures", "CompileErrorMini"),
        };

        var exceptions = new ConcurrentBag<Exception>();
        var successfulLoads = 0;

        var tasks = Enumerable.Range(0, 20)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    var fixturePath = fixturePaths[i % fixturePaths.Length];
                    using var catalog = await SourceFileCatalog.LoadAsync(fixturePath);
                    Interlocked.Increment(ref successfulLoads);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
        Assert.Equal(20, successfulLoads);
    }
}
