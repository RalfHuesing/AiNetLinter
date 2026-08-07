using Xunit;

namespace AiNetLinter.Tests;

/// <summary>
/// Test-Collection für Tests, die <see cref="System.Console.Out"/> oder
/// <see cref="System.Console.Error"/> umleiten. Verhindert, dass sich parallel
/// laufende Tests die globale Konsolenumleitung gegenseitig überschreiben.
/// </summary>
[CollectionDefinition(nameof(ConsoleTestCollection), DisableParallelization = true)]
public sealed class ConsoleTestCollection;
