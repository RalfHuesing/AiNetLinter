#nullable enable

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie eines Mini-Fixtures mit genau einer kaputten Datei. Dient als
/// gezielter Treiber fuer die Singular-Form der aggregierten Compile-Fehler-Warnung
/// ("1 Datei hat Compile-Fehler") in den Tool-Tests.
/// </summary>
public sealed class SingleCompileErrorMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public SingleCompileErrorMiniFixtureWorkspace()
        : base("SingleCompileErrorMini", "ainetlinter-single-compile-error-mini-")
    {
    }
}
