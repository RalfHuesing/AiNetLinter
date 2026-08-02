#nullable enable
using System.IO;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des CompileErrorMini-Fixtures fuer EPIC-06-Tests: enthaelt 3 intakte C#-
/// Klassen (ValidClassA/B/C) und 3 Klassen mit absichtlichen Compile-Fehlern (BrokenClassA/B/C).
/// MSBuildWorkspace laedt das Projekt vollstaendig (auch mit Syntax-/Semantik-Fehlern) und meldet
/// die Fehler ueber <c>Compilation.GetDiagnostics()</c> — auf dem die 006-Warnhinweis-Pfade in
/// den 9 MCP-Tools aufsetzen.
/// </summary>
public sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public CompileErrorMiniFixtureWorkspace()
        : base("CompileErrorMini", "ainetlinter-compile-error-mini")
    {
    }

    public string PathFor(string fileName) => Path.Combine(RootPath, "src", "CompileErrorMini", fileName);
}
