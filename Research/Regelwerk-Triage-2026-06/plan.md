# Regelwerk-Triage — Radikales Ausmisten (Juni 2026)

**Ziel:** 6 Regeln vollständig entfernen, verbleibende Opt-In-Regeln auf `false` defaulten,
Code-Defaults mit gelebter `rules.json` synchronisieren, Doku bereinigen.

**Entscheidungsgrundlage:** Analyse vom 2026-06-18 (Gnadengesuch-Triage).
Kriterium: "Verhindert das Limit ein konkretes LLM-Versagensmuster?" — Nein → raus.

---

## Commit-Strategie

Vier saubere Commits, logisch abgegrenzt:

```
1. refactor(rules): remove RequireExplicitTruncationHandling + BusinessLogicChecker restlos
2. refactor(rules): remove EnforceNoVariableShadowing, ReadonlyParameters, ReadonlyFields
3. refactor(rules): remove EnforceNoMagicValues restlos
4. refactor(defaults): sync code-defaults mit gelebter rules.json (Complexity, MagicValues, Opt-In)
```

---

## Commit 1 — TruncationChecker + BusinessLogicChecker

**Betroffene Regeln:** `RequireExplicitTruncationHandling`, `EnforceStrictBoundaryForBusinessLogic`

**Warum raus?**
- `TruncationChecker`: Triggert auf Methodennamen-Präfixe (z.B. `ReadAllTextAsync`).
  Diese APIs truncaten nicht — sie werfen oder liefern vollständig. 100% False-Positives.
- `BusinessLogicChecker`: naming-getrieben (`*Calculator`, `*Rule`). Erzwingt `static`
  und bricht idiomatische DI. Ein Rename löst Architektur-Violations aus. Kein LLM-Mehrwert.

**Zu ändern:**

| Datei | Aktion |
| :--- | :--- |
| `src/AiNetLinter/Core/Checkers/TruncationChecker.cs` | **Datei löschen** |
| `src/AiNetLinter/Core/Checkers/BusinessLogicChecker.cs` | **Datei löschen** |
| `src/AiNetLinter/Core/LinterAnalyzer.cs` | `TruncationChecker.Check(...)` entfernen (Zeile ~243); `BusinessLogicChecker.Check(...)` entfernen (Zeile ~146); using-Direktiven entfernen |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | Properties `RequireExplicitTruncationHandling` + `EnforceStrictBoundaryForBusinessLogic` aus `GlobalConfig` entfernen; Apply-Methoden bereinigen |
| `src/AiNetLinter/Configuration/LinterConfigOverrides.cs` | Entsprechende Override-Properties entfernen |
| `src/AiNetLinter/Configuration/RuleMetadataRegistry.cs` | Einträge für beide Regeln entfernen |
| `src/AiNetLinter/Core/CursorRulesGenerator.cs` | Einträge für beide Regeln in der Disabled-Liste / Erklärungstexte entfernen |
| `src/AiNetLinter/Output/ViolationTextFormatter.cs` | Einträge für beide Regeln entfernen |
| `src/AiNetLinter.Tests/Core/ControlFlowResilienceTests.cs` | Tests für Truncation entfernen |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` | Truncation + BusinessLogic FP-Tests entfernen |
| `rules.json` | Keys `RequireExplicitTruncationHandling` + `EnforceStrictBoundaryForBusinessLogic` entfernen |
| `Docs/configuration.md` | Abschnitte zu beiden Regeln entfernen |
| `Docs/rationale.md` | Begründungen / Erwähnungen entfernen |
| `.cursor/rules/AiNetLinter.mdc` | Regenerieren (auto via Tool oder manuell) |

---

## Commit 2 — VariableShadowing + ReadonlyParameters + ReadonlyFields

**Betroffene Regeln:** `EnforceNoVariableShadowing`, `EnforceReadonlyParameters`, `EnforceReadonlyFields`

**Warum raus?**
- `VariableShadowing`: C# verhindert gefährliches Shadowing bereits im Compiler.
  Der Rest (`catch(Exception ex)` im inneren Scope, Lambda-`x`) ist normales C#. Null LLM-Mehrwert.
- `ReadonlyParameters`: C# kennt kein `readonly`-Keyword für Parameter (nur `in`, andere Semantik).
  Die Implementierung erzwingt Artificial-Reassignment-Tracking — Aufwand ohne Nutzen.
- `ReadonlyFields`: Felder, die nur im Konstruktor gesetzt werden, `readonly` zu nennen ist
  guter Stil, aber kein LLM-Lesbarkeitsproblem. Bestraft Records/init-Properties fälschlich.

**Zu ändern:**

| Datei | Aktion |
| :--- | :--- |
| `src/AiNetLinter/Core/Checkers/ScopeChecker.cs` | `CheckVariableShadowing` Methode entfernen |
| `src/AiNetLinter/Core/Checkers/StateChecker.cs` | `CheckParameterReassignment`, `CheckReadonlyFields`, `AnalyzePrivateField`, `RegisterFieldWrite` entfernen |
| `src/AiNetLinter/Core/FieldReadonlyTracker.cs` | **Datei löschen** (nur für ReadonlyFields benötigt) |
| `src/AiNetLinter/Core/LinterAnalyzer.cs` | Alle Aufrufe zu VariableShadowing (Zeilen ~167,217,223,229,235), CheckParameterReassignment (~179,188,198,208), AnalyzePrivateField (~173), RegisterFieldWrite (~180,189,199,209), CheckReadonlyFields (~58,286) entfernen |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `EnforceNoVariableShadowing`, `EnforceReadonlyParameters`, `EnforceReadonlyFields` aus `GlobalConfig` entfernen |
| `src/AiNetLinter/Configuration/LinterConfigOverrides.cs` | Override-Properties entfernen |
| `src/AiNetLinter/Configuration/RuleMetadataRegistry.cs` | 3 Einträge entfernen |
| `src/AiNetLinter/Core/CursorRulesGenerator.cs` | Einträge entfernen |
| `src/AiNetLinter/Output/ViolationTextFormatter.cs` | Einträge entfernen |
| `src/AiNetLinter.Tests/Core/ReadonlyParametersRefKindTests.cs` | **Datei löschen** |
| `src/AiNetLinter.Tests/Core/ReadonlyFieldsPartialClassTests.cs` | **Datei löschen** |
| `src/AiNetLinter.Tests/Core/ScopeImmutabilityTests.cs` | Shadowing-Tests entfernen (ggf. ganzen File) |
| `rules.json` | 3 Keys entfernen |
| `Docs/configuration.md` | 3 Abschnitte entfernen |
| `Docs/rationale.md` | Erwähnungen entfernen |

---

## Commit 3 — EnforceNoMagicValues restlos

**Betroffene Regel:** `EnforceNoMagicValues` (inkl. `MagicValues`-Konfig-Sektion und `MagicValuesChecker`)

**Warum raus?**
- Mit `Mode: "all"` und `MinStringLength: 0` flaggt das jeden `0`, `1`, `""`, `"/"`.
  Extreme False-Positive-Rate im Default-Zustand.
- Kein LLM-Halluzinationsmuster verhindert — ein Agent erzeugt keine schlechtere Ausgabe
  wegen einer Magic-`2` als Literal.
- Die ganze `MagicValues`-Konfig-Sektion in `rules.json` wird mitentfernt.

**Zu ändern:**

| Datei | Aktion |
| :--- | :--- |
| `src/AiNetLinter/Core/Checkers/MagicValuesChecker.cs` | **Datei löschen** |
| `src/AiNetLinter/Core/LinterAnalyzer.cs` | `MagicValuesChecker.Check(...)` Aufruf entfernen |
| `src/AiNetLinter/Configuration/LinterConfig.cs` | `MagicValuesConfig`-Record entfernen; Property `MagicValues` aus `LinterConfig` entfernen; `EnforceNoMagicValues` aus `GlobalConfig` entfernen |
| `src/AiNetLinter/Configuration/LinterConfigOverrides.cs` | `MagicValuesConfigOverride` entfernen |
| `src/AiNetLinter/Configuration/RuleMetadataRegistry.cs` | Eintrag entfernen |
| `src/AiNetLinter/Core/CursorRulesGenerator.cs` | MagicValues-Eintrag entfernen |
| `src/AiNetLinter/Output/ViolationTextFormatter.cs` | Eintrag entfernen |
| `src/AiNetLinter.Tests/MagicValuesTests.cs` | **Datei löschen** |
| `src/AiNetLinter.Tests/FalsePositives/FalsePositiveTests.cs` | MagicValues FP-Tests entfernen |
| `rules.json` | `EnforceNoMagicValues` aus Global + gesamte `"MagicValues": {...}` Sektion entfernen |
| `Docs/configuration.md` | MagicValues-Abschnitt entfernen |

---

## Commit 4 — Code-Defaults mit gelebter rules.json synchronisieren

**Ziel:** Erstanwender erhalten sofort sinnvolle Defaults ohne Konfigurationsaufwand.

### Metrics-Defaults anpassen (`LinterConfig.cs` → `MetricsConfig`)

| Property | Alt | Neu | Begründung |
| :--- | :---: | :---: | :--- |
| `MaxLineCount` | 500 | 700 | Kohäsive Datei schlägt Datei-Streuung; RAG-Paging kostet Kontext |
| `MaxMethodLineCount` | 42 | 60 | 42 ist ein Gag-Wert; 60 ist gelebter Wert |
| `MaxCyclomaticComplexity` | 5 | 10 | 5 scheitert an jedem realen switch; 12 ist gelebter Wert, 10 guter Mittelweg |
| `MaxCognitiveComplexity` | 5 | 15 | Gelebter Wert |
| `ComplexityNearMissTolerance` | 0 | 1 | Warning-Puffer vermeidet harte Brüche bei Grenzfällen |
| `MethodParameterCountIgnoreTypeNames` | `[]` | `["CancellationToken"]` | CT zählt nie sinnvoll; gelebter Wert |
| `ConstructorDependencyIgnoreTypePrefixes` | `[]` | (Liste unten) | Kritischster Fix: DI-Infrastruktur soll nicht zählen |
| `ExcludeSwitchDispatcherCases` | `false` | `true` | Dispatcher-Pattern ist idiomatisch |
| `SwitchDispatcherMaxCaseBodyLines` | 3 | 3 | bleibt |

`ConstructorDependencyIgnoreTypePrefixes` neu:
```
["ILogger","IOptions","IOptionsSnapshot","IOptionsMonitor",
 "IHostEnvironment","IWebHostEnvironment","IConfiguration",
 "IServiceProvider","IHttpContextAccessor"]
```

### Global-Defaults anpassen (`LinterConfig.cs` → `GlobalConfig`)

| Property | Alt | Neu | Begründung |
| :--- | :---: | :---: | :--- |
| `EnforceXmlDocumentation` | `true` | `false` | Zeremoniell-Tokens; du fährst false; kein LLM-Mehrwert |
| `EnforceResultPatternOverExceptions` | `true` | `false` | Architektur-Meinung, kein Lesbarkeitsproblem |
| `PreventContextDependentOverloads` | `true` | `false` | Vage, schwer antizipierbar |
| `DetectAndBanPhantomDependencies` | `true` | `false` | Spezialfall, nicht Default |
| `AllowUnsealedPartialClasses` | `false` | `false` | bleibt (sealed ist gut) |
| `SealedClassExemptSuffixes` | `[]` | `["Base","Foundation","Host"]` | gelebter Wert |
| `ImmutabilityExemptBaseTypes` | `[]` | (Liste unten) | Blazor/WPF Base-Types sollen exempt sein |
| `ImmutabilityAllowPrivateBackingFields` | `false` | `true` | gelebter Wert |

`ImmutabilityExemptBaseTypes` neu:
```
["ComponentBase","LayoutComponentBase","ObservableObject","ObservableRecipient",
 "BackgroundService","AuthenticationStateProvider","INotifyPropertyChanged"]
```

### rules.json synchronisieren

`rules.json` ist die Single Source of Truth — alle obigen Werte dort setzen,
so dass `rules.json` == Code-Defaults nach diesem Commit.

### Doku final bereinigen

| Datei | Aktion |
| :--- | :--- |
| `Docs/configuration.md` | Alle Reste der 6 entfernten Regeln prüfen und entfernen |
| `Docs/rationale.md` | Begründungen der entfernten Regeln entfernen |
| `Docs/ROADMAP.md` | Erwähnungen prüfen und entfernen |
| `.cursor/rules/AiNetLinter.mdc` | Regenerieren (über CursorRulesGenerator oder manuell) |
| `.cursor/rules/AiNetLinterRichtlinien.mdc` | Einträge der entfernten Regeln entfernen |
| `Research/DeepResearch/.../AiNetLinter-LLM.md` | Kein Handlungsbedarf (Read-Only-Artefakt) |

---

## Prüfliste nach Commit 4

- [ ] `dotnet build` → 0 Fehler, 0 Warnings
- [ ] `dotnet test` → alle Tests grün
- [ ] `rules.json` hat keine der 6 entfernten Properties mehr
- [ ] `LinterConfig.cs` hat keine der 6 entfernten Properties mehr
- [ ] `LinterConfig.cs` Defaults stimmen 1:1 mit `rules.json` überein
- [ ] `.cursor/rules/AiNetLinter.mdc` enthält keine entfernten Regeln
- [ ] `Docs/configuration.md` enthält keine entfernten Regeln
- [ ] `AiNetLinter --readme` zeigt keine entfernten Regeln

---

## Nicht-Scope (bewusst außen vor)

- `EnforceExplicitStateImmutability` — bleibt als Opt-In mit `false`-Default (Datenstand schon korrekt)
- `MaxBoolParameterCount` — bleibt bei 1 (AllowPrivate schützt Helfer; öffentliche API korrekt eingeschränkt)
- `MaxConstructorDependencies` — bleibt bei 5 (Fix ist die Ignore-Liste, nicht das Limit)
- `PreventContextDependentOverloads` — wird nur default-`false`, Checker-Code bleibt für Opt-In
- `DetectAndBanPhantomDependencies` — wird nur default-`false`, Code bleibt

---

*Erstellt: 2026-06-18 | Status: Bereit zur Umsetzung*
