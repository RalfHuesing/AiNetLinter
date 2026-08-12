#nullable enable

using AiNetLinter.TestKit;

// Registriert PreparedSolutionFixture als echte xUnit-v3-Assembly-Fixture (siehe
// https://xunit.net/docs/shared-context, "Assembly Fixture") -- eine Instanz lebt fuer die gesamte
// AiNetLinter.FastTests-Assembly, ohne die Testklassen zwangsweise zu serialisieren (Regel-Ref
// AiNetLinterRichtlinien.mdc §4). Testklassen erhalten Zugriff ueber einen Konstruktorparameter
// vom Typ PreparedSolutionFixture.
[assembly: AssemblyFixture(typeof(PreparedSolutionFixture))]
