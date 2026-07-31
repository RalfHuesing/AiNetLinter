---
status: done
type: step-result
task: ignore-suppressions
step: "005"
epic: EPIC-05
step_type: single
coded_by: coder
coded_by_model: Gemini 3.6 Flash (High)
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T08:36:00+02:00
code_commit_hash: f079e3a
status_after: done
blocker_category: n/a
---

# Result Step 005: Dokumentation in Docs/configuration.md, Docs/ROADMAP.md und README.md mit --ignore-suppressions synchronisieren

## Zusammenfassung

Die Dokumentation in `Docs/configuration.md` (CLI-Parameter & Beispiele), `Docs/ROADMAP.md` (Epic 12 Eintrag) und `README.md` (CLI-Feature-Tabelle) wurde mit der neuen CLI-Option `--ignore-suppressions` synchronisiert. Zudem wurden die Agenten-Regeln über `dotnet run --project src/AiNetLinter -- --config rules.json --path . --sync-agent-rules-only` auf Aktualität geprüft.

## Geänderte Dateien

- `Docs/configuration.md` — Spezifikation & Beispiele für `--ignore-suppressions`.
- `Docs/ROADMAP.md` — Epic 12 Meilenstein als erledigt markiert.
- `README.md` — `--ignore-suppressions` in die Feature-Tabelle aufgenommen.

## Commit

- **Code-Commit-Hash:** `f079e3a`
- **Message:** `docs: add --ignore-suppressions CLI option specification and sync roadmap`
- **Branch:** main
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build → grün
dotnet test  → grün (1015 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt.

## Beobachtungen

Keine.

## Bekannte Unschärfen

Keine.
