#nullable enable

using System;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Fixtures;

/// <summary>
/// Disposable Handle fuer eine generierte Last-Fixture-Loesung. Raeumt das
/// temporaere Verzeichnis beim Dispose auf und haelt den Pfad zur erzeugten
/// Solution-Datei bereit, damit Tests dort andocken koennen.
/// </summary>
public sealed class LoadFixtureHandle : IDisposable
{
    private readonly TestTempDirectory tempDir;

    public LoadFixtureHandle(string name, TestTempDirectory tempDir, string solutionPath)
    {
        Name = name;
        this.tempDir = tempDir;
        SolutionPath = solutionPath;
    }

    /// <summary>Mensch-lesbarer Name fuer Test-Output-Identifikation.</summary>
    public string Name { get; }

    /// <summary>Wurzelverzeichnis der generierten Solution.</summary>
    public string RootPath => tempDir.DirectoryPath;

    /// <summary>Absoluter Pfad zur generierten <c>.slnx</c>-Loesungsdatei.</summary>
    public string SolutionPath { get; }

    public void Dispose() => tempDir.Dispose();
}

