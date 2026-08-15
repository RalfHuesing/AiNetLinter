#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.IntegrationTests.Platform;

/// <summary>
/// Einmaliger echter <see cref="Microsoft.CodeAnalysis.MSBuild.MSBuildWorkspace"/>-Load einer isolierten
/// Kopie der kanonischen Mini-Solution <c>BaselineMini</c>, geteilt ueber eine
/// xUnit-v3-Assembly-Fixture fuer <c>AiNetLinter.IntegrationTests</c> (Registrierung siehe
/// <see cref="MsBuildFixtureHostAssemblyFixture"/>). Ersetzt die pro Testklasse duplizierte Kombination
/// aus lokalem <c>FindSolutionRoot</c>-Helper plus Direktaufruf von <see cref="SourceFileCatalog.LoadAsync"/>.
/// Bewusst nicht in <c>AiNetLinter.TestKit</c>, weil das dortige <c>FastTestsDependencyGuardTests</c>-Pendant
/// jede MSBuild-Referenz in <c>TestKit.dll</c> als Verletzung meldet.
/// </summary>
public sealed class MsBuildFixtureHost : IAsyncLifetime
{
    private LoadedFixture? fixture;

    /// <summary>
    /// Der einmalig geladene Katalog. Nicht <see langword="null"/> nach <see cref="InitializeAsync"/>.
    /// </summary>
    public SourceFileCatalog Catalog => fixture?.Catalog ?? throw new InvalidOperationException(
        $"{nameof(MsBuildFixtureHost)} wurde noch nicht initialisiert.");

    /// <summary>
    /// Die geladene, read-only geteilte Solution der isolierten <c>BaselineMini</c>-Kopie.
    /// </summary>
    public Solution Solution => Catalog.Solution;

    public async ValueTask InitializeAsync()
    {
        fixture = await LoadedFixture.CreateAsync("BaselineMini");
    }

    public ValueTask DisposeAsync()
    {
        return fixture?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

}
