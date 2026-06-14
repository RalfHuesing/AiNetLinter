# Implementierungspläne — False-Positive-Korrekturen AiNetLinter

Basierend auf: [False-Positive-Research.md](../False-Positive-Research.md)  
Regeln: [`.cursor/rules/AiNetLinter.mdc`](../../../.cursor/rules/AiNetLinter.mdc) | [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc)

**Jeder Plan gilt:**
- Regeln in `.cursor/rules/` müssen beachtet werden (Referenz im jeweiligen Plan)
- README muss nach Implementierung aktualisiert werden (Checkliste in Plan 12)
- xUnit v3 Tests sind Pflicht für jede Logik-Änderung
- `rules.json` aktualisieren wenn neue Config-Optionen hinzukommen
- `--sync-cursor-rules` ausführen nach rules.json-Änderungen

---

## Pläne nach Priorität

| Plan | Prio | Betroffene Regel(n) | Hauptänderung |
|------|------|---------------------|---------------|
| [01 (Erledigt)](01-P0-MaxInheritanceDepth-Framework-Typen.md) | **P0** | `MaxInheritanceDepth` | `InheritanceDepthFrameworkPrefixes` in MetricsConfig; `GetInheritanceDepth()` anpassen |
| [02 (Erledigt)](02-P0-EnforceNoMagicValues-Profile.md) | **P0** | `EnforceNoMagicValues` | Neues `MagicValuesConfig`-Record; Mode + IgnorePatterns + IgnoreInvocationPrefixes |
| [03](03-P1-EnforceSealedClasses-Partial-WPF.md) | **P1** | `EnforceSealedClasses` | `SealedClassExemptSuffixes` in GlobalConfig; `ShouldSkipSealedCheck()` |
| [04](04-P1-EnforceNoSilentCatch-IsSwallowed-Fix.md) | **P1** | `EnforceNoSilentCatch` / `AllowCancellationShutdownCatch` | `IsSwallowed()` + `IsAllowedCancellationCatch()` Bug-Fix |
| [05](05-P1-EnforceExplicitStateImmutability-Blazor-WPF.md) | **P1** | `EnforceExplicitStateImmutability` | `ImmutabilityExemptBaseTypes` + `ImmutabilityAllowPrivateBackingFields` |
| [06](06-P2-Complexity-SwitchDispatcher-NearMiss.md) | **P2** | `MaxCyclomaticComplexity` / `MaxCognitiveComplexity` | `SwitchDispatcherDetector`-Klasse; `ComplexityNearMissTolerance` |
| [07](07-P2-MaxConstructorDependencies-FrameworkTypes.md) | **P2** | `MaxConstructorDependencies` | `ConstructorDependencyIgnoreTypePrefixes` in MetricsConfig |
| [08](08-P2-GeneratedCode-Dateiausschluss.md) | **P2** | Alle Regeln | Neues `FileFiltersConfig`-Record; `FileFilterEvaluator`; `ExcludeFilePatterns` |
| [09](09-P2-EnforceNamespaceDirectoryMapping-FeatureFolder.md) | **P2** | `EnforceNamespaceDirectoryMapping` | `NamespaceDirectoryMappingMode` + `IgnorePathSegments` + `RequiredTrailingSegments` |
| [10](10-P3-StaticTestSentinel-ExemptSuffixes.md) | **P3** | `StaticTestSentinel` | `ExemptClassNameSuffixes` + `ExemptWhenInheritsFrom` + `ExemptStaticClasses` in TestSentinelConfig; `ClassInfo` um `IsStatic` + `BaseTypeNames` erweitern |
| [11](11-P3-EnforceResultPattern-AllowThrowIn.md) | **P3** | `EnforceResultPatternOverExceptions` | `ResultPatternAllowThrowInNamespaceSuffixes` + `ResultPatternAllowCatchRethrow` |
| [12](12-Cross-README-Integrationsleitfaden.md) | Cross | README + Profil-Vorlagen | Vollständige README-Überarbeitung; WPF- und Blazor-Profil-Templates |

---

## Abhängigkeiten zwischen Plänen

```
Plan 01 → unabhängig
Plan 02 → unabhängig (neues Record MagicValuesConfig)
Plan 03 → unabhängig
Plan 04 → unabhängig (Bug-Fix)
Plan 05 → Plan 10 koordinieren (beide brauchen ClassInfo.BaseTypeNames)
Plan 06 → unabhängig (neue Datei SwitchDispatcherDetector)
Plan 07 → unabhängig
Plan 08 → unabhängig (neue Datei FileFilterEvaluator)
Plan 09 → unabhängig
Plan 10 → Plan 05 koordinieren (ClassInfo-Erweiterung gemeinsam)
Plan 11 → unabhängig
Plan 12 → nach jedem Plan aktualisieren (Checkliste in Plan 12)
```

**Empfohlene Implementierungsreihenfolge:**
1. Plan 01 + 04 (Bug-Fixes, wenig Risiko)
2. Plan 08 (Datei-Ausschluss — wirkt quer, sollte früh rein)
3. Plan 03 (Sealed-Partial — einfach)
4. Plan 02 (MagicValues — umfangreich, aber isoliert)
5. Plan 05 + 10 (zusammen — ClassInfo-Erweiterung)
6. Plan 07 + 09 (Config-Erweiterungen)
7. Plan 06 (SwitchDispatcher — neue Klasse, etwas komplexer)
8. Plan 11 (ResultPattern)
9. Plan 12 (README — laufend aktualisieren)

---

## Architektur-Zusammenfassung: Was sich ändert

### Neue Dateien:
- `src/AiNetLinter/Metrics/SwitchDispatcherDetector.cs` (Plan 06)
- `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` (Plan 08)
- `src/AiNetLinter.Tests/MaxInheritanceDepthTests.cs` (Plan 01)
- `src/AiNetLinter.Tests/MagicValuesTests.cs` (Plan 02)
- `src/AiNetLinter.Tests/FileFilterTests.cs` (Plan 08)
- `src/AiNetLinter.Tests/Core/ComplexityDispatcherTests.cs` (Plan 06)

### Erweiterte Dateien:
- `src/AiNetLinter/Configuration/LinterConfig.cs` — neue Records + Properties (alle Pläne)
- `src/AiNetLinter/Core/LinterAnalyzer.Architecture.cs` — Plans 01, 03
- `src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs` — Plans 04, 11
- `src/AiNetLinter/Core/LinterAnalyzer.Immutability.cs` — Plan 05
- `src/AiNetLinter/Core/LinterAnalyzer.Complexity.cs` — Plan 06
- `src/AiNetLinter/Core/LinterAnalyzer.State.cs` — Plan 07
- `src/AiNetLinter/Core/LinterAnalyzer.Scope.cs` — Plan 09
- `src/AiNetLinter/Core/PostAnalysisChecks.cs` — Plan 10
- `src/AiNetLinter/Models/ClassInfo.cs` — Plans 05+10 (IsStatic, BaseTypeNames)
- `src/AiNetLinter/Core/LinterEngine.cs` — Plan 08 (FileFilter-Integration)
- `rules.json` — nach jedem Plan
- `README.md` — nach jedem Plan

### Kein neuer Namespace erforderlich — alle neuen Klassen passen in bestehende Namespaces.
