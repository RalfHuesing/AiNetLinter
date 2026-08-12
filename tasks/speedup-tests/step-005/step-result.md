---
status: done
type: step-result
task: speedup-tests
step: 005
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: bffe3e3
status_after: done
blocker_category: n/a
---

# Result Step 005: Korrektur — AiNetLinterRichtlinien.mdc §4 an Quarantäne-Entscheidung anpassen

## Zusammenfassung

Die im Plan wörtlich vorgegebene Ersatzzeile 1:1 eingesetzt, keine weiteren Änderungen
in der Datei. Reine Textkorrektur, kein Build/Test erforderlich.

## Geänderte Dateien

- `.agents/rules/AiNetLinterRichtlinien.mdc` — Zeile 94 (Bullet „MCP & Dogfood Testing")
  ersetzt: Verweist nicht mehr ausschließlich auf `McpLiveRepositoryTests`/`McpTestClient`
  in `AiNetLinter.Tests`, sondern nennt `McpHandshakeToolRegistrationTests` in
  `AiNetLinter.IntegrationTests` als aktuellen Weg und beschreibt die verbleibenden
  `pending`-Verträge als Migrationsrest.

## Commit

- **Code-Commit-Hash:** `bffe3e3`
- **Message:** `fix(rules): korrigiere MCP-Testing-Regel an Quarantaene-Entscheidung [speedup-tests]`
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

Keine — laut Plan reine Regel-Doku-Änderung ohne Code-/Testauswirkung, kein Testlauf
durchgeführt.

## Abweichungen vom Plan

- Keine. Genau die eine Zeile wortgleich zum Plan ersetzt, sonst nichts in der Datei
  angefasst.

## Beobachtungen

- Vor dem Edit per `git diff` verifiziert, dass ausschließlich diese eine Zeile geändert
  wurde (keine ungewollten CRLF/Whitespace-Nebeneffekte außer der von Git gemeldeten
  LF→CRLF-Normalisierungswarnung, die von Git selbst beim Commit vorgenommen wird, nicht
  vom Edit).

## Bekannte Unschärfen

- Keine.
