#nullable enable

using System;
using System.IO;

namespace AiNetLinter.IntegrationTests.Fixtures;

/// <summary>
/// Disposable Handle fuer eine generierte Last-Fixture-Loesung. Raeumt das
/// temporaere Verzeichnis beim Dispose auf und haelt den Pfad zur erzeugten
/// Solution-Datei bereit, damit Tests dort andocken koennen.
/// </summary>
public sealed class LoadFixtureHandle : IDisposable
{
    private readonly string directoryPath;

    public LoadFixtureHandle(string name, string directoryPath, string solutionPath)
    {
        Name = name;
        this.directoryPath = directoryPath;
        SolutionPath = solutionPath;
    }

    /// <summary>Mensch-lesbarer Name fuer Test-Output-Identifikation.</summary>
    public string Name { get; }

    /// <summary>Wurzelverzeichnis der generierten Solution.</summary>
    public string RootPath => directoryPath;

    /// <summary>Absoluter Pfad zur generierten <c>.slnx</c>-Loesungsdatei.</summary>
    public string SolutionPath { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Ignore temporary directory cleanup failures during test cleanup
        }
    }
}
