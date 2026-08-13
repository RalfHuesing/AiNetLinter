---
status: done (pending audit)
type: step-result
task: speedup-tests
step: 026
epic: EPIC-6
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
code_commit_hash: 06fdc20
status_after: done
blocker_category: n/a
---

# Result Step 026

## Zusammenfassung

Der MSBuild-ladende Command-Hostvertrag liegt in Integration; die reine Fast-Kohorte bleibt runtime-sauber. Der Integration-Host besitzt Retry, Loading-Retry und das Max-2-Lebensdauerbudget. Die HEAD-gegen-Ziel-Discovery belegt 66 Fast + 55 Integration = 121 historische Fälle.

## Commit

- Code: `06fdc20` — `test(mcp): migriere Mini-Hostvertraege [speedup-tests]`
- Push: nein.

## Build-/Test-Output

```
Pure Command + Dependency-Guards -> grün (12/12)
Fast-Ziel + Kategorie-/Dependency-Guards -> grün (69/69)
Retry-/Budgetvertrag -> grün (2/2)
Integration-Kohorte -> grün (62/62); Callsite-Guard -> grün (1/1)
Framing-Wiederholung -> grün (3/3, zweimal zusätzlich)
dotnet build -> grün (0 Warnungen, 0 Fehler)
git diff --check -> grün
```

## Vertragsmatrix

| Familie | Fast | Integration | Summe |
|---|---:|---:|---:|
| Policy/Options/CallLog/Loading | 46 | 0 | 46 |
| Command | 10 | 13 | 23 |
| GetImpact | 9 | 5 | 14 |
| Prozess/E2E/Retry | 1 | 37 | 38 |
| **Gesamt** | **66** | **55** | **121** |

Die 21 Ledgerzeilen nennen Zielorte, Fallzahlen und Gate-Evidenz; zusätzliche Guard-/Hosttests zählen nicht in die 121.

## Abweichungen vom Plan

Die Pre-Move-Laufbaseline fehlt und wurde nicht nachträglich behauptet: HEAD-Quellinventur und nach dem Move erhobene Ziel-TRX sind die Evidenz. Der Callsiteguard wurde auf die drei tatsächlichen besitzenden Transportstellen kalibriert.

## Beobachtungen

Der DRY-Audit fand bekannte Strangler-Duplikate und fachlich unterschiedliche near-Testpaare, keine neue riskante Duplikation dieses Schnitts.

## Bekannte Unschärfen

TD-008 und TD-010 bleiben offen; Voll-, Dogfood-, Performance-, Stress- und Legacy-Volltest wurden nicht ausgeführt.
