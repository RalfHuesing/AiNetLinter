---
status: blocked
type: step-result
task: speedup-tests
step: 018
epic: EPIC-4
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6-terra
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13
code_commit_hash: n/a
status_after: blocked
blocker_category: content
---

# Result Step 018: Read-only MCP-Roslyn-Toolkohorten als In-Memory-Super-Step migrieren

## Zusammenfassung

Die einmalige Legacy-Baseline war grün (243 Tests). Die 24 vorgesehenen Klassen wurden in die
FastTests-Zielstruktur überführt und ein lokaler, direkter Snapshot-Kontext begonnen. Der vollständige
Build bleibt rot; die Migration wird deshalb nicht als Code-Commit gesichert.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter "<24-Klassen-Baselinefilter>" → grün (243 Tests, 0 Fehler)
dotnet build → rot: 26 Compilefehler in noch nicht portierten Legacy-Fixtureverträgen
```

## Abweichungen vom Plan

Keine fachliche Scope-Erweiterung vorgenommen. Die drei zulässigen ursachengerechten Korrekturläufe
endeten in einer breiteren Fixture-Abhängigkeit; daher Status `blocked` statt einer unvollständigen
Migration mit abgeschwächten Verträgen.

## Beobachtungen

- `FindReferencesToolTests`, `GetSymbolBodyToolTests` und `GetFileSkeletonToolTests` verwenden
  konkrete `Workspace`-Pfade der bisherigen Plattenfixture.
- Sechs Toolklassen verwenden die nicht migrierten Compile-error- und DI-Workspace-Fixtures;
  `PatternDetect`/`Safeguard`/`Violations` benötigen zusätzlich den Legacy-Helper
  `TestHelper.CreateFaultySolution`.
- Diese Abhängigkeiten müssen vor einem neuen Coder-Lauf als deklarative In-Memory-Szenarien
  vollständig spezifiziert werden; ein Kompatibilitätswrapper würde den verbotenen Datei-/MSBuild-
  Vertrag in FastTests weitertragen.

## Bekannte Unschärfen

Die verschobenen Code-Dateien sind absichtlich uncommittet und der Ledger/CodeMap unverändert.
Ein Folgeschritt muss die Arbeitskopie entweder gezielt fortsetzen oder die unvollständige
Strukturmigration vor einer Neuplanung sauber verwerfen.

## Falls Status `blocked`

**Blocker-Art:** `content`

**Blockiert weil:** Der bestehende Step-Plan benennt die lokalen Spezialfälle, liefert aber keine
vollständige In-Memory-Spezifikation der von acht Klassen gemeinsam genutzten Compile-error- und
Pfadfixture-Verträge. Nach drei Build-/Fixzyklen ist kein weiterer Versuch zulässig.

**Brauche von Nutzer:** Eine neue JIT-Planung, die diese Fixture-Kohorte als explizite deklarative
ProjectSpecs samt Fehlerstatus und virtuellen Pfadwerten ausarbeitet.

**Aktueller Stand:** Legacy-Baseline dokumentiert; Code-Worktree enthält uncommittete Übernahmen,
die weder Build noch Gate bestehen.
