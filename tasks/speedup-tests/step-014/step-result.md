---
status: done
type: step-result
task: speedup-tests
step: 014
epic: EPIC-4
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra Medium
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-12
code_commit_hash: f41fd31
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 014: Korrektur: Namespace-Glob-Vertrag selektiv kalibrieren

## Zusammenfassung

Der Namespace-Glob-Fall verwendet jetzt `FilterMini.Tests.*`. Er bestätigt den erwarteten
Test-Namespace und schließt die beiden Produktions-Namespaces explizit aus. Die
`SkeletonMapFilterTests`-Kohorte bleibt bei 18 Fällen.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs` — selektiver Namespace-Glob mit positivem Treffer und negativen Produktionsnamespace-Assertions.

## Commit

- **Code-Commit-Hash:** `f41fd31`
- **Message:**
  ```
  test(skeleton): kalibriere Namespace-Glob-Vertrag [speedup-tests]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet build src/AiNetLinter.FastTests → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~SkeletonMapFilterTests → grün (18 Tests, 0 Fehler)
```

## Abweichungen vom Plan

- Keine.

## Beobachtungen

- Die angegebene `coder/SKILL.md` war im Arbeitsbereich nicht vorhanden; die Umsetzung folgte dem Korrekturplan und den referenzierten Regeln.
