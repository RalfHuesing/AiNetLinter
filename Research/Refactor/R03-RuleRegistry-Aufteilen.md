# R03 — `RuleRegistry.cs` aufteilen

**Problem:** `RuleRegistry.cs` ist 883 Zeilen lang (Limit 500) und enthält eine einzige
Methode `BuildAll()` die alle ~25 Regel-Definitionen als Array-Literal enthält.
Beide Suppressions (`MaxLineCount`, `MaxMethodLineCount`) wurden von einem Agenten eingefügt.

---

## Diagnose: Warum greifen die Suppress-Kommentare überhaupt?

### `MaxLineCount`
Die Datei hat 883 Zeilen, Limit 500. Klar überschritten.

### `MaxMethodLineCount` — eigentlich sollte CompoundSuppression greifen

`BuildAll()` ist ein reines Array-Literal:
```csharp
private static IReadOnlyList<RuleMetadata> BuildAll() => [ new(...), new(...), ... ];
```
CC ≈ 1, CogC ≈ 0 → die `CompoundSuppression` (CC ≤ 3, CogC ≤ 5 → RelaxedLimit 150)
sollte greifen. Die Methode ist jedoch ~840 Zeilen lang — selbst 150 wird überschritten.

Das bedeutet: selbst mit der CompoundSuppression wäre ein Warning vorhanden.
Die Suppression löst das Problem also nicht vollständig.

---

## Lösungsansatz: `BuildAll()` in Intent-Gruppen aufteilen

`BuildAll()` delegiert an private Teil-Methoden, eine pro Intent-Gruppe.
Das Ergebnis: kein Datei-Split nötig, keine partial-Klassen, alle Definitionen
bleiben in einer Datei — aber in überschaubaren Chunks.

```csharp
private static IReadOnlyList<RuleMetadata> BuildAll() =>
[
    ..BuildMetricsRules(),
    ..BuildAgentResilientRules(),
    ..BuildArchitectureRules(),
    ..BuildTestCoverageRules(),
    ..BuildGeneralRules(),
];
```

Jede `Build*`-Methode gibt `RuleMetadata[]` zurück und bleibt unter 60 Zeilen
(ca. 4–5 Regeln à ~12 Zeilen = 48–60 Zeilen).

Falls eine Gruppe mehr Regeln hat als reinpassen: weitere Sub-Methoden
(`BuildMetricsRules_Sizes()`, `BuildMetricsRules_Complexity()`).

---

## Konkrete Änderungen

### Aktuelle Gruppen in `BuildAll()` (aus Kommentaren abgeleitet)

| Kommentar | Regeln (ca.) | Zeilen (ca.) |
|:---|:---:|:---:|
| `// --- Metrics Config Rules (14 Rules) ---` | 14 | ~420 |
| agent-resilience | 3 | ~90 |
| architecture | 2 | ~60 |
| test-coverage | 1 | ~30 |
| general | ~8 | ~240 |

Die Metriken-Gruppe muss weiter unterteilt werden:

```csharp
private static IReadOnlyList<RuleMetadata> BuildAll() =>
[
    ..BuildMetricsSizeRules(),       // MaxLineCount, MaxMethodLineCount, MaxMethodParameterCount (3 Regeln)
    ..BuildMetricsComplexityRules(), // MaxCyclomaticComplexity, MaxCognitiveComplexity, MaxInheritanceDepth, MaxMethodOverloads (4)
    ..BuildMetricsDependencyRules(), // MaxConstructorDependencies, MaxAIContextFootprint (2)
    ..BuildMetricsStructureRules(),  // MaxDirectoryDepth/Children, MaxBoolParameter, MaxPartialClass, MaxPublicMembers, MaxSwitchArms, MaxLinqChain, CompoundSuppressions, MinCognitiveComplexity (7)
    ..BuildAgentResilientRules(),    // BanAsyncVoid, BanBlockingTaskAccess, EnforceNoSilentCatch (3)
    ..BuildArchitectureRules(),      // EnforceNamespaceDirectoryMapping, DetectAndBanPhantomDependencies (2)
    ..BuildTestCoverageRules(),      // EnableTestSentinel (1)
    ..BuildGeneralRules(),           // EnforceSealedClasses, EnforcePascalCase, EnforceSemanticNaming, EnforceNullableEnable, EnforceValueObjectContracts, AllowTryPatternOutParameters, AllowCancellationShutdownCatch, BanPublicNestedTypes (8)
];
```

Beispiel für eine Teil-Methode:
```csharp
private static RuleMetadata[] BuildMetricsSizeRules() =>
[
    new(
        RuleId: "MaxLineCount",
        // ... (unverändert aus BuildAll())
    ),
    new(
        RuleId: "MaxMethodLineCount",
        // ...
    ),
    new(
        RuleId: "MaxMethodParameterCount",
        // ...
    ),
];
```

### Suppress-Kommentare entfernen

```diff
-// ainetlinter-disable MaxLineCount
-// ainetlinter-disable MaxMethodLineCount
 #nullable enable
```

---

## Warum kein Datei-Split per `partial`?

`RuleRegistry` ist eine `internal static class` — `partial` wäre technisch möglich,
aber:
- `MaxPartialClassFiles` Limit ist 2 — bei 8 Gruppen sofort überschritten
- Partial-Dateien erschweren Navigation (man sucht eine Regel, nicht eine Gruppe)
- Sub-Methoden in einer Datei sind einfacher grep-bar: `rg "RuleId: \"MaxLineCount\""` findet sofort

---

## Unit Tests

Kein neuer Testcode für die Aufteilung nötig. Existierende Tests validieren das Ergebnis:

```csharp
// AgentFeaturesTests.cs oder RuleRegistryTests.cs
[Fact]
public void BuildAll_ContainsAllExpectedRuleIds()
{
    var ids = RuleRegistry.All.Select(r => r.RuleId).ToHashSet();
    Assert.Contains("MaxLineCount", ids);
    Assert.Contains("BanAsyncVoid", ids);
    // etc. — alle bekannten Rule-IDs
}

[Fact]
public void BuildAll_NoDuplicateRuleIds()
{
    var ids = RuleRegistry.All.Select(r => r.RuleId).ToList();
    Assert.Equal(ids.Count, ids.Distinct().Count());
}
```

Diese Tests existieren ggf. schon implizit — explizit machen schadet nicht.

---

## Dokumentation

Keine Änderung an Benutzer-Dokumentation nötig (reine Implementierungsstruktur).

Optional: Kommentar in `RuleRegistry.cs` am Anfang von `BuildAll()`:
```csharp
// Delegiert an Intent-Gruppen — jede Methode ≤ 60 Zeilen (MaxMethodLineCount).
private static IReadOnlyList<RuleMetadata> BuildAll() => [ ..BuildMetricsSizeRules(), ... ];
```

---

## Reihenfolge-Empfehlung

Unabhängig von R01/R02. Kann als erster oder letzter Schritt umgesetzt werden.

---

## Commit-Vorschlag

```
refactor: RuleRegistry.BuildAll() in Intent-Gruppen-Methoden aufteilen
```

Entfernt beide Inline-Suppressions durch Extraktion von 8 privaten Teil-Methoden.
Keine API- oder Logik-Änderungen.
