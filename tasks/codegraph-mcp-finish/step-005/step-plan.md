---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 005
title: "Testsuite-Performance — Test-Data-Builder/Object-Mother konsolidieren, Rest-Cluster (F.4, Teil 2/2) + #nullable enable Randmitnahme (F.5)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: ["step-004"]
---

# Step 005: Test-Data-Builder/Object-Mother konsolidieren — Rest-Cluster (F.4, Teil 2/2) + `#nullable enable` Randmitnahme (F.5)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — Testsuite-Performance (Block F).
  F.1–F.3 approved, F.4 Teil 1/2 (19 Dateien Kern-Testinfrastruktur)
  approved in step-004. Dieser Step schließt F.4 vollständig ab (Rest-
  Cluster `Core/Checkers/`+`Metrics/`+`FalsePositives/`) und nimmt F.5
  (`#nullable enable`-Pragma) als Randmitnahme in genau den Dateien mit,
  die dieser Step ohnehin anfasst — F.6 (Laufzeitmessung) bleibt für einen
  späteren Step offen.
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 4 (Test-Data-
  Builder/Object-Mother, wörtlich: „Test-Data-Builder/Object-Mother für
  `Config`/`GlobalConfig`/`CheckerContext` statt ad-hoc-Konstruktion pro
  Test") und Punkt 5 (`#nullable enable`-Pragma, wörtlich: „keine eigene
  Flächenaktion für die ~63 betroffenen Dateien … die Pragma-Zeile wird nur
  in Dateien nachgerüstet, die im Rahmen von A-F ohnehin angefasst
  werden"). Non-Goals (Block F insgesamt): „Keine Änderung an
  Testinhalten/Assertions", „Keine neue Testabdeckung durch Block F".

## Aktueller Projektzustand (JIT-Kontext)

- **step-004 hat den in `roadmap.md`/step-004 geschätzten Rest-Scope von
  „23 Dateien" nicht wörtlich übernommen** — bei erneuter Code-Sichtung für
  diesen Step (per Grep über `Core/Checkers/`, `Metrics/`, `FalsePositives/`
  nach `new Config\s*\{` und dem mehrzeiligen target-typed Muster
  `Config \w+\(...) =>\s*\n?\s*new\(\)`) sind es tatsächlich nur noch **19
  Dateien** mit einer lokalen, noch nicht auf `TestHelper.CreateDefaultConfig()`
  umgestellten Config-Konstruktion. Der Unterschied: 3 der in step-004
  explizit als „NICHT Teil" gelisteten Dateien
  (`BlockingTaskCheckerTests.cs`, `AsyncVoidCheckerTests.cs`,
  `LinqChainLengthCheckerTests.cs`) nutzen bereits
  `TestHelper.CreateDefaultConfig() with {...}` — vermutlich, weil sie im
  Rahmen von step-003 (Checker-Testordner-Neuordnung) bzw. einer früheren
  Bearbeitung bereits in diesem Stil geschrieben wurden. Genau das ist der
  Kern des JIT-Ansatzes: der tatsächliche Stand zum Planungszeitpunkt zählt,
  nicht die zum Zeitpunkt von step-004 geschätzte Zahl.
- **Muster identisch zu step-004, gleiche Sicherheits-Argumentation:**
  `TestHelper.CreateDefaultConfig()` liefert unverändert exakt `new Config
  { Global = new GlobalConfig(), Metrics = new MetricsConfig() }`
  (`src/AiNetLinter.Tests/TestHelper.cs`, in diesem Step nicht angefasst).
  Für jeden bestehenden Ausdruck `new Config { Global = X, Metrics = Y, ... }`
  ist `TestHelper.CreateDefaultConfig() with { Global = X, Metrics = Y, ... }`
  wertgleich (record-`with` überschreibt nur genannte Properties, alle
  anderen `Config`-Properties sind optional mit Default-Initialisierer,
  siehe `src/AiNetLinter/Configuration/Config.cs`) — rein syntaktische,
  nicht verhaltensändernde Transformation.
- **Jede der 19 Dateien hat genau einen lokalen Konstruktions-Punkt**
  (eine private statische Methode `CreateConfig(...)`/`ConfigWith(...)`/
  `CreateDefaultConfig()`/`CreateBaseConfig()`/`LowLineLimitConfig(...)`,
  jeweils exakt einmal pro Datei) — verifiziert per Grep, siehe Tabelle
  unten. Kein Fall mit mehreren verstreuten inline-Konstruktionen wie noch
  in step-004 (`DeveloperExperienceTests.cs` mit 6 inline-Stellen) — dieser
  Rest-Cluster ist strukturell einheitlicher, was das Risiko gegenüber
  step-004 nicht erhöht.
- **`#nullable enable`-Bestand geprüft (F.5-Randmitnahme):** Von den 19
  Dateien, die dieser Step ohnehin anfasst, fehlt bei **11** die Pragma-
  Zeile am Dateianfang (per `head -1` je Datei verifiziert); 8 haben sie
  bereits. Konvention (verifiziert an einer bereits konformen Datei,
  `WpfCodeBehindTests.cs`): `#nullable enable` als exakt erste Zeile,
  gefolgt von einer Leerzeile, dann die `using`-Direktiven. Das deckt sich
  mit `.agents/rules/AiNetLinter.mdc` Zeile 12/70 (`EnforceNullableEnable`
  — „`#nullable enable` am Dateianfang jeder `.cs`-Datei"). Da dieser Step
  ohnehin jede der 19 Dateien anfasst, ist genau das die in `Konzept.md`
  F.5 vorgesehene „Randmitnahme" (keine separate Flächenaktion) — nicht
  mehr, nicht weniger: Dateien außerhalb dieses Steps werden **nicht**
  angefasst, auch wenn ihnen die Pragma-Zeile ebenfalls fehlt.
- **Bestehende Struktur wird weiterverwendet, kein Neubau:** wie in
  step-004 wird ausschließlich `TestHelper.CreateDefaultConfig()`
  referenziert (kein zweiter, konkurrierender Objekt-Mother-Mechanismus).

### Betroffene Dateien (19) mit Konstruktions-Punkt und `#nullable enable`-Status

| Datei | Lokale(r) Konstruktions-Punkt | `#nullable enable` |
|---|---|---|
| `src/AiNetLinter.Tests/Core/Checkers/MaxPartialClassFilesTests.cs` | `CreateConfig(int limit = 2, string[]? exemptTypes = null)` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/WpfCodeBehindTests.cs` | `CreateConfig(...)` (Block-Body, `return new Config`) | vorhanden |
| `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs` | `CreateConfig(...)` (Block-Body, `return new Config`) | vorhanden |
| `src/AiNetLinter.Tests/Core/Checkers/SilentCatchAllowedTypesTests.cs` | `CreateConfig(string[]? allowedTypes = null) => new()` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountOverrideTests.cs` | `CreateConfig(int maxParams = 4) => new()` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountIgnoreTypePrefixesTests.cs` | `CreateConfig(int maxParams = 4) => new()` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MethodParameterCountAccessibilityTests.cs` | `CreateConfig(...) => new()` | vorhanden |
| `src/AiNetLinter.Tests/Core/Checkers/CouplingSemanticTests.cs` | `CreateConfig(int maxConstructorDeps)` (Block-Body) | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MaxBoolParameterCountTests.cs` | `CreateConfig(int limit = 1, bool allowPrivate = true, string[]? exemptPrefixes = null) => new()` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MaxConstructorDependenciesTests.cs` | `CreateConfig(int maxDeps, string[]? ignorePrefixes = null, string[]? exemptSuffixes = null)` (Block-Body) | vorhanden |
| `src/AiNetLinter.Tests/Core/Checkers/MaxInheritanceDepthTests.cs` | `CreateDefaultConfig()` (Block-Body; **Namenskollision mit `TestHelper.CreateDefaultConfig()`**, bereits 4 weitere Aufrufstellen im Test-Körper via `CreateDefaultConfig() with {...}` — diese Aufrufstellen bleiben unverändert, nur der Methodenkörper selbst wird umgestellt) | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MaxPublicMembersPerTypeTests.cs` | `CreateConfig(int limit = 5, string[]? exemptSuffixes = null) => ...` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/MaxSwitchArmsTests.cs` | `CreateConfig(...)` (Block-Body, `return new Config`) | vorhanden |
| `src/AiNetLinter.Tests/Core/Checkers/NamespaceDirectoryMappingTests.cs` | `CreateDefaultConfig()` (Block-Body; **gleiche Namenskollision** wie oben, mehrere Aufrufstellen `CreateDefaultConfig() with {...}` und `CreateDefaultConfig().Global with {...}` bleiben unverändert) | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Core/Checkers/NestedTypesCheckerTests.cs` | `CreateConfig(...)` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/Metrics/FileLimitGuidanceTests.cs` | `LowLineLimitConfig(int maxLineCount = 10) => new()` | vorhanden |
| `src/AiNetLinter.Tests/Metrics/MaxDirectoryChildrenTests.cs` | `CreateConfig(int limit, string[]? exemptNames = null)` | fehlt → ergänzen |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveExtensionsTests.cs` | `CreateBaseConfig() => new()` | vorhanden |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` | `CreateConfig(..., int maxParams = 4) => new()` | vorhanden |

**Damit vollständig abgedeckt:** alle Dateien aus dem in step-004
„Explizit NICHT Teil dieses Steps" gelisteten Cluster, die tatsächlich noch
eine offene Config-Konstruktion haben. `LinqChainLengthCheckerTests.cs`,
`AsyncVoidCheckerTests.cs`, `BlockingTaskCheckerTests.cs` sind bereits
migriert (siehe oben) und werden **nicht** angefasst (keine Änderung nötig,
kein Grund für Diff-Rauschen).

## Intention

Nach diesem Step ist F.4 vollständig abgeschlossen: alle 42 Testdateien mit
vormals lokaler `Config`-Rohkonstruktion (`new Config {...}`/target-typed
`=> new()`) greifen einheitlich auf `TestHelper.CreateDefaultConfig()`
zurück — ohne dass sich ein einziger Rückgabewert, Testname oder Assertion
ändert. Zusätzlich tragen alle 19 hier angefassten Dateien danach die
`#nullable enable`-Pragma (F.5-Randmitnahme), ohne dass F.5 als eigene
Flächenaktion behandelt wird.

## Konkrete Änderungen

### Muster A — Config-Konsolidierung (gilt für alle 19 Dateien unten, identisch zu step-004)

Der jeweilige lokale Konstruktions-Punkt (egal ob `=> new() {...}`
Expression-Body oder `{ return new Config {...}; }` Block-Body) wird von

```csharp
new Config
{
    Global = new GlobalConfig { /* ... */ },
    Metrics = new MetricsConfig { /* ... */ },
    // ggf. weitere Properties
}
```

zu

```csharp
TestHelper.CreateDefaultConfig() with
{
    Global = new GlobalConfig { /* ... unverändert übernommen ... */ },
    Metrics = new MetricsConfig { /* ... unverändert übernommen ... */ },
    // ggf. weitere Properties unverändert übernommen
}
```

umgeschrieben. **Regeln (identisch zu step-004):**

- Methodensignaturen (Name, Parameter, Zugriffsmodifikator, Expression-
  vs. Block-Body-Form) bleiben **exakt** unverändert — nur der
  Konstruktions-Ausdruck selbst wird umgeschrieben.
- Ist `Global`/`Metrics` im Original bereits der reine Default (`new
  GlobalConfig()`/`new MetricsConfig()`, keine Property gesetzt), wird das
  jeweilige `with`-Member weggelassen.
- Sind **beide** reiner Default und keine weiteren Properties gesetzt:
  direkt `TestHelper.CreateDefaultConfig()` ohne `with`-Block.
- Alle sonstigen Config-Properties werden 1:1 übernommen, nur an den
  `with`-Block gehängt statt an die Objekt-Initialisierer-Syntax.
- **`MaxInheritanceDepthTests.cs` und `NamespaceDirectoryMappingTests.cs`
  Sonderfall:** Beide haben eine lokale Methode namens `CreateDefaultConfig()`
  (Namenskollision mit `TestHelper.CreateDefaultConfig()`, exakt wie schon
  bei 4 Dateien in step-004 dokumentiert) UND mehrere Aufrufstellen im
  Testkörper, die bereits `CreateDefaultConfig() with {...}` bzw.
  `CreateDefaultConfig().Global with {...}` schreiben — diese Aufrufstellen
  meinen die **lokale** Methode, nicht `TestHelper`, und bleiben unverändert
  (nur der Rückgabewert der lokalen Methode selbst ändert sich intern auf
  `TestHelper.CreateDefaultConfig() with {...}`, der Methodenname/die
  Aufrufstellen bleiben unangetastet). Umbenennung ist wie in step-004
  bewusst **nicht** Teil dieses Steps.

### Muster B — `#nullable enable`-Pragma (Randmitnahme F.5, gilt für die 11 in der Tabelle oben als „fehlt → ergänzen" markierten Dateien)

Erste Zeile der Datei wird `#nullable enable`, gefolgt von einer Leerzeile,
dann unverändert die bestehende erste `using`-Zeile — exakt das Muster aus
bereits konformen Dateien wie `WpfCodeBehindTests.cs`:

```csharp
#nullable enable

using System;
// ... bestehende usings unverändert
```

Kein weiterer Eingriff in diesen Dateien über das Hinzufügen der Zeile
hinaus (kein Aufräumen von `?`/`!`-Annotationen, kein Beheben neuer
Nullable-Warnungen über das hinaus, was der Compiler ohnehin schon mit
`#nullable disable` (implizit) durchließ — falls das Hinzufügen der Pragma
eine neue Warnung aufdeckt, siehe „Bekannte Ausnahmen"/Definition of Done:
Zero-Warning-Pflicht gilt weiterhin, ggf. minimale `?`/`!`-Annotation an
der konkreten Stelle ergänzen, aber keine Fläche über die aufgedeckte
Stelle hinaus).

### Betroffene Dateien

Siehe Tabelle unter „Aktueller Projektzustand" oben — 19 Dateien für Muster
A, davon 11 zusätzlich für Muster B.

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen (auch nach dem
      Hinzufügen von `#nullable enable` in den 11 Dateien — falls das eine
      neue Nullable-Warnung aufdeckt, siehe Muster B).
- [ ] `dotnet test --filter Category=Unit` — grün, exakt gleiche
      Testanzahl wie vor dem Step.
- [ ] `dotnet test AiNetLinter.slnx --no-build` (Volllauf) — grün, gleiche
      Testanzahl wie vor dem Step (1186, siehe step-004-Ergebnis als
      Baseline).
- [ ] Grep-Sweep nach dem Step: `new Config\s*\{` und das mehrzeilige
      target-typed Muster `Config \w+\([^)]*\)\s*=>\s*\n?\s*new\(\)` dürfen
      in den 19 oben gelisteten Dateien **keine** Treffer mehr liefern
      (außer `TestHelper.cs` selbst).
- [ ] Grep-Sweep: alle 19 Dateien beginnen mit `#nullable enable` als
      erster Zeile.
- [ ] Vor jedem Build/Test: offene `AiNetLinter.exe`/`testhost.exe`-Prozesse
      prüfen und ggf. beenden (Tech-Stack-Notiz, bekannte
      Datei-Sperren-Falle).

## Definition of Done

- [ ] Alle 19 gelisteten Dateien auf `TestHelper.CreateDefaultConfig()
      with {...}` (bzw. direkten Aufruf ohne `with`) umgestellt,
      Methodensignaturen/Testkörper/Assertions unverändert.
- [ ] Alle 19 Dateien beginnen mit `#nullable enable`.
- [ ] Grep-Sweeps (siehe Tests) liefern keine verbleibenden Treffer.
- [ ] `roadmap.md`: F.4 vollständig abgehakt (Notiz „→ step-004 + step-005"),
      F.5-Teilfortschritt vermerkt, F.6 bleibt offen für einen Folge-Step.
- [ ] Build-Command aus Tech-Stack-Notiz grün, 0 Warnungen.
- [ ] Test-Command aus Tech-Stack-Notiz grün (Unit + Volllauf), Testanzahl
      identisch zu vorher.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`).
- [ ] `step-005/step-result.md` geschrieben.
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `EnforceNullableEnable` (Zeile 12/70:
  „`#nullable enable` am Dateianfang jeder `.cs`-Datei") — direkte
  Grundlage für Muster B; außerdem `AIContextFootprint`/`MaxLineCount`
  (keine der 19 Dateien darf durch die Umstellung wachsen — `with` ist wie
  in step-004 knapper als der vollständige Objekt-Initialisierer, plus 2
  Zeilen `#nullable enable` + Leerzeile bei den 11 betroffenen Dateien,
  vernachlässigbar).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 „Testsuite-Parallelität
  bewahren"/Build-Test-Pflichten (Zero-Warning) — reine
  Refactoring-Konsolidierung darf keine neuen Warnungen/keine
  Parallelitäts-Regressionen einführen; §5 Kommentar-Konventionen (keine
  Task-/Planungsartefakt-Referenzen wie `step-005`/`F.4`/`F.5` im Code
  selbst).

## Bekannte Ausnahmen

- Falls das Hinzufügen von `#nullable enable` in einer der 11 Dateien eine
  bislang durch implizites `#nullable disable` verdeckte Nullable-Warnung
  aufdeckt: minimale `?`/`!`-Annotation an exakt dieser Stelle ergänzen
  (Zero-Warning-Pflicht hat Vorrang vor „keine Änderung über das
  Pragma hinaus"), aber keine Fläche über die konkret aufgedeckte Stelle
  hinaus. Kommt das in mehr als 2-3 Dateien vor, im `step-result.md` unter
  „Abweichungen" vermerken statt stillschweigend zu erweitern.
- Keine bekannten flaky Tests in diesem Step-Scope.

## Code-Skizze (optional)

Beispiel `CouplingSemanticTests.cs` (Block-Body-Fall, `#nullable enable`
fehlt):

```csharp
#nullable enable

using ...

// ...

private static Config CreateConfig(int maxConstructorDeps)
{
    return TestHelper.CreateDefaultConfig() with
    {
        Metrics = new MetricsConfig { MaxConstructorDependencies = maxConstructorDeps }
    };
}
```

## Notes

- **F.4 wird mit diesem Step vollständig abgeschlossen** (42/42 Dateien,
  19 aus step-004 + 19 aus diesem Step + 4 bereits vor step-004 auf
  `TestHelper.CreateDefaultConfig()` migriert). `roadmap.md`/`EPIC-01` kann
  danach den F.4-Teilpunkt komplett abhaken, F.5/F.6 bleiben für
  EPIC-01 offen.
- **Namenskollisionen `CreateDefaultConfig()`** (jetzt insgesamt 6 Dateien
  über beide Steps: 4 aus step-004 + `MaxInheritanceDepthTests.cs` +
  `NamespaceDirectoryMappingTests.cs` aus diesem Step) bewusst weiterhin
  nicht umbenannt — wie in step-004 begründet: private Methoden, kein
  Compile-Konflikt, Umbenennung wäre über reine Konsolidierung hinaus. Der
  Kritiker kann bei Bedarf einen gebündelten Tech-Debt-Eintrag für alle 6
  Dateien anlegen, statt wie in step-004 auf einen eigenen Eintrag zu
  verzichten — das entscheidet der Kritiker in diesem Step neu, da die
  Häufung jetzt größer ist.
- **F.5 bleibt über diesen Step hinaus grundsätzlich offen** (nur
  Randmitnahme, keine Flächenaktion, siehe Konzept.md „Nutzer-
  Entscheidung") — es ist bewusst **kein** Ziel dieses Steps, alle ~63
  betroffenen Dateien projektweit zu erfassen oder eine vollständige Liste
  zu führen.
- **F.6 (Laufzeitmessung) ist nicht Teil dieses Steps** — sinnvollerweise
  erst nach Abschluss der Struktur-/Boilerplate-Änderungen in Block F zu
  dokumentieren (F.4/F.5 haben laut Konzept ohnehin „kein Laufzeit-Hebel"),
  ein Folge-Step-Modus-Aufruf entscheidet, ob F.6 als eigener kleiner Step
  folgt oder in den Abschluss von EPIC-01 integriert wird.
