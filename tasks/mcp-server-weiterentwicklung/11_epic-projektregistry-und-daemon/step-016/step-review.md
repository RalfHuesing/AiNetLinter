---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 016
epic: EPIC-B
step_type: batch
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-24T16:05:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 016: Tech-Debt-Pflegepaket (TD-008, TD-001, TD-003, TD-010 fixen; TD-004 als Akzeptanz verankern)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/**` (nur die im Plan zitierten Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `Konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün (gefilterte Läufe bauten warnungsfrei; TreatWarningsAsErrors aktiv)
- [x] Tests: selbst nachgeprüft, grün (gezielte Filterläufe als Stichprobe, kein Vollstack-Rerun)

## Befund

### Plan-Erfüllung

Alle fünf Items umgesetzt — item-01 (Janitor-Fixture mit Bildpfad-Identifikation,
Gate-integriertem Cleanup und transparentem Skip samt Escape-Pin und Budget-Anpassung),
item-02 (nach Code-Verifikation korrekt reduziert auf die Bauanleitung, da der
`RULES_INVALID`-Vertrag in `ProjectInstanceFactory.TryCreate` bereits bestand und der
Batch-Pfad `MaterializeRules` unverändert mit dokumentiertem Fallback bleibt — genau die
vom Plan-Notes geforderte Abgrenzung), item-03 (Guard null/""/"   " →
`PROJECT_ROOT_REQUIRED` inkl. Theory-Test), item-04 (Zeile entfernt; übrige Tabelle von
mir vollständig gegen Emittenten verifiziert: alle 18 verbleibenden Codes haben
Emittenten, `AMBIGUOUS_SOLUTION` keinen mehr) und item-05 (nur XML-Doc, kein
Produktionscode-Verhalten angetastet; Capacity-Contract-Test nagelt Überlauf bei
nur-busy, Tick-Nicht-Räumen und Eviction-Wiedereintritt fest); die Abweichung von
„GENAU EINMAL" ist regelkonform, denn beide Erstlauf-Befunde (Datei-Limit-Dogfood,
ungepinnter Lifetime-Test) wurden nach TRX-Ursachenanalyse am Ursprung behoben und der
Finalzustand je Suite genau einmal grün gefahren — Ursachenfix, kein Symptom-Fixing.

### Rules-Konformität

Richtlinien §3 (gezieltes Gate/Fixture statt Collection-Serialisierung,
`TestTempDirectory`, TRX-Diagnose statt blindem Rerun) und §5 (Zero-Warning,
Ursachenfixes statt Symptom-Fixing, keine Task-/ID-Referenzen in neuen Kommentaren)
sowie AiNetLinter.mdc-Grenzwerte sind eingehalten.

### Logische Korrektheit

Die Janitor-Identifikation ist korrekt restriktiv (Bildpfad `AiNetLinter.exe` UND
Test-EXE-Gleichheit bzw. Repos-Lage mit Trenner-Check gegen Präfix-Fehltreffer;
unlesbare Bildpfade werden nie angetastet); die Escape-Pin-Behauptung habe ich gegen
den Code verifiziert (`ThinClientProxy.cs:50-62` → `McpServerCommand.cs:48` startet
`McpServerLifetime.Start(ParentPid)` auch im Escape-Modus), der Watchdog-Test bleibt
also real; die Gate-Freigabe beim Skip (`catch → Release → throw`) ist lückenlos.

### Konzept-Treue (Ebene 4)

Kein Non-Goal berührt (Batch-Pipeline unverändert, kein neues Tool, deterministische
Fehlerverträge mit Bauanleitung entsprechen sogar ausdrücklich dem Konzept-Zielbild),
die Pin-/Budget-Maßnahmen sind notwendige Folgerungen aus dem TD-008-Befund und nicht
Scope-Überschreitung; die 240-s-Budgets sind verhältnismäßig — mein eigener
Stichprobenlauf zeigte ~2 min legitime Gate-Wartezeit hinter dem Shared-Warmth-Test,
womit 45 s empirisch zu kurz waren, während phasenspezifische Timeouts den
Hang-Schutz erhalten.

### Build-/Test-Status

```
dotnet test src/AiNetLinter.FastTests --filter "…CapacityContract|…DefinitionLoaderTests|…InstanceFactoryTests" → grün (20/20)
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~Mcp.Daemon|FullyQualifiedName~McpServerLifetimeTests" → grün (8/8, 2:08 min)
```

Coders Vollstack-Nachweis (FastTests 1730/1730, IntegrationTests 359/359) gemäß
`step-result.md` übernommen; Testzahlen konsistent (+4 FastTests = Capacity-Test +
3 Theory-Fälle). MCP-Quality-Gates laut Result (get_violations 0, safeguard 10/10)
dokumentiert übernommen, nicht unabhängig wiederholt.

## Sonstige Beobachtungen / MINOR / NITPICK

- `ProjectRegistryTests.cs:395-396` — zwei durch den Testauszug zurückgebliebene
  Leerzeilen zwischen Methoden; kosmetisch, kein Linter-Befund.
- Der Endpunkt-Probe des Janitors wertet ein `OperationCanceledException` des
  2-s-Connect-Timeouts als „kein Listener vorhanden" — für einen gesunden Daemon
  irrelevant (Connect schlägt sofort an), für einen wedged-but-bound Daemon wäre das
  die großzügigere Deutung; der anschließende Doppelstart-Lock fängt dies ab und das
  Result dokumentiert die Skip-Pfad-Unschärfe ohnehin als bekannt.
