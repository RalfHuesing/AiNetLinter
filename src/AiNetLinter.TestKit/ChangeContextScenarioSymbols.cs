#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.TestKit;

/// <summary>
/// Die beiden geaenderten Methoden des Change-Context-Szenarios
/// (<see cref="ChangeContextScenarioFactory"/>): die public PlaceAsync mit Call-Sites und
/// die private, externe Aufrufstellen-freie LogInternal.
/// </summary>
internal sealed record ScenarioSymbols(IMethodSymbol PlaceAsync, IMethodSymbol LogInternal);
