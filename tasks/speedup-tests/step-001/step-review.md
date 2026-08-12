---
status: done
type: step-review
task: speedup-tests
step: 001
epic: EPIC-1
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: approved
tech_debt_ids: []
---

# Review Step 001: Drei neue Testzielprojekte + gemeinsame Props + Config-Verträge auf neue Namen

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/AiNetLinterRichtlinien.mdc` §3/§4 eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle zehn im Plan genannten Dateien (`tests/AiNetLinter.TestProject.props`, drei neue `.csproj`,
`AiNetLinter.slnx`, `rules.json` §`ProjectOverrides`/§`TestSentinel.TestProjectNameSuffixes`, drei
Proof-Tests) sind 1:1 wie geplant umgesetzt, inklusive der bewusst offen gelassenen Punkte
(`InternalsVisibleTo`, Architekturguards, Ledger — alle korrekt im JIT-Kontext/den Notes als
Folge-Step deklariert, nicht stillschweigend vergessen).

### Rules-Konformität

Eingehalten. `parallelizeAssembly: false` nur in `AiNetLinter.IntegrationTests/xunit.runner.json`,
nicht in `AiNetLinter.FastTests` (§4 Testsuite-Parallelität bewahren); keine zwangsserialisierende
Collection in den drei neuen Testklassen. `dotnet test`/TRX-Diagnose (§3) selbst genutzt, keine
Auffälligkeiten.

### Logische Korrektheit

`TestProject.props` spiegelt das Legacy-Pinning (`Update` für `Microsoft.Build.Framework`,
`Include` für `Microsoft.NET.StringTools`) exakt 1:1 aus `AiNetLinter.Tests.csproj`, kein
`RunSettingsFilePath` fest verdrahtet — konsistent mit der TestKit-Class-Library-Anforderung. Die
`rules.json`-Änderung `"*.Tests"` → `"*Tests"` übersetzt sich über `ProjectConfigResolver.IsMatch`
korrekt zu `^.*Tests$` und deckt alle drei neuen Namen ab, ohne den Legacy-Match zu brechen
(selbst verifiziert: `ArchitectureTests` weiterhin grün). Der separate `"AiNetLinter.TestKit"`-Key
ist nötig, da `TestKit` nicht auf `Tests` endet — korrekt erkannt und umgesetzt. Die drei
Proof-Tests treffen exakt die im Plan verlangten produktiven Einstiegspunkte
(`ConfigLoader.TryLoadConfig`, `ProjectConfigResolver.ResolveForProject`,
`TestProjectDetector.IsTestProject`) mit den korrekten Signaturen.

### Konzept-Treue (Ebene 4)

Kein Non-Goal umgesetzt, kein Muss-Haben-Punkt aus diesem Teilschritt ausgelassen. Scope entspricht
exakt der in `roadmap.md`/Konzept vereinbarten „ersten Teil"-Abgrenzung von EPIC-1
(Leitplanke 0/8 Punkt 1). Leitplanke 11 (TestKit ohne künstliche xUnit-Abhängigkeit) eingehalten.

### Build-/Test-Status

Selbst reproduziert (nicht nur Coder-Bericht übernommen):

```
dotnet build AiNetLinter.slnx                                                              → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --filter ...ProjectOverrideResolutionTests|...Suffix  → grün (6 Tests)
dotnet test src/AiNetLinter.IntegrationTests --filter ...ProjectOverrideRealSolutionTests    → grün (3 Tests, ~24s)
dotnet test --filter FullyQualifiedName~ArchitectureTests                                    → grün (13 Tests, Legacy-*.Tests-Verhalten unverändert)
```

Die dokumentierte Abweichung (ungültiger `--` in XML-Kommentar in `tests/AiNetLinter.TestProject.props`
verursachte `MSB4024`) ist im committeten Stand nicht mehr vorhanden — Fix bestätigt.

## Sonstige Beobachtungen / MINOR / NITPICK

- Die vom Coder in „Bekannte Unschärfen" selbst benannte Wildcard-Weitung (`*Tests` matcht auch
  künftige, fachlich unbeteiligte `*Tests`-Projektnamen) ist dieselbe Charakteristik wie beim
  bisherigen `*.Tests` und laut Plan bewusst in Kauf genommen — kein neuer Fund, nur zur Kenntnis
  genommen für künftige Steps bei Neuanlage weiterer Projekte.
