#nullable enable

using AiNetLinter.IntegrationTests.Platform;

// Registriert MsBuildFixtureHost als echte xUnit-v3-Assembly-Fixture (siehe
// https://xunit.net/docs/shared-context, "Assembly Fixture") -- eine Instanz lebt fuer die gesamte
// AiNetLinter.IntegrationTests-Assembly und laedt BaselineMini genau einmal ueber einen echten
// MSBuildWorkspace, ohne die Testklassen zwangsweise zu serialisieren (Regel-Ref
// AiNetLinterRichtlinien.mdc §4). Testklassen erhalten Zugriff ueber einen Konstruktorparameter
// vom Typ MsBuildFixtureHost.
[assembly: AssemblyFixture(typeof(MsBuildFixtureHost))]
[assembly: AssemblyFixture(typeof(AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraphCatalogFixture))]
[assembly: AssemblyFixture(typeof(AiNetLinter.IntegrationTests.Mcp.Platform.ReadOnlyMcpHostFixture))]
[assembly: AssemblyFixture(typeof(AiNetLinter.IntegrationTests.Mcp.Platform.RepositoryMcpHostFixture))]
