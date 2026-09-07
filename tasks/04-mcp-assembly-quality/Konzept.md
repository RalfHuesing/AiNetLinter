---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Welche Assemblypfade sollen nach Task 03 zuerst als reale Decompiler-Proben dienen?
depends_on:
  - tasks/03-mcp-unified-analysis-target/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks: []
supersedes: null
---

# Task 04: MCP-Assembly- und Decompiled-Qualität

## Ziel und Problem

Externe DLL-/EXE-Analyse soll als statischer Snapshot nützlich und ehrlich
sein, ohne Projektsemantik, Laufzeitwissen oder Consumer-Kontext vorzutäuschen.
Ein Agent soll die Kette

`Assembly-Katalog → Typ → Member/Body/Graph`

mit kopierbaren IDs und belastbarer Herkunft verfolgen können. Fehlende
Referenzen, Diagnosebudgets und dekompilierte Grenzen dürfen keine falsche
Vollständigkeits- oder Anwendbarkeitsaussage erzeugen.

## Source of Truth und betroffene Bereiche

- Befunde: `tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`
- gemeinsame Verträge: `tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md`
- Assembly-Registry, Snapshot und Decompiler: `Mcp/Assemblies/Analysis/*`
- Assemblytools: `Tools/AssemblyAnalysis/*`
- gemeinsame Symbolidentität: `AnalysisSymbolIdentity`,
  `SymbolIdentifierResolver`, `Tools/SymbolGraph/Assembly*`
- Type Origin und Referenzen: `TypeResolution/*`
- Assembly-Response-Builder und `AssemblyAnalysisResponseLimits`
- Assembly-, Cross-Target-, Lifecycle-, Concurrency- und Integrationstests

## Scope

- konsistente Assembly-Identität, Herkunft, Generation und stale-Fehler;
- Content-basierte Snapshot-Identität zusätzlich zum kanonischen Pfad;
- gleichnamige Assemblies aus unterschiedlichen Verzeichnissen getrennt halten;
- geänderte Assemblyinhalte erzeugen neue Generationen, bestehende Snapshots
  werden nicht still mutiert;
- stabile Snapshots bei Änderung während Hashing/Dekompilierung oder klarer
  Stale-/Retry-Fehler;
- parallele Erstöffnung desselben Ziels ohne inkonsistente Snapshots;
- Snapshot-Wiederverwendung, Lease-, TTL-, Kapazitäts- und Cleanup-Verhalten;
- Katalog → Typ → Member/Body/Graph mit einer kopierbaren ID-Kette;
- Diagnose-, BCL-, Referenz-, Member- und Body-Limits sichtbar budgetieren;
- fehlende PDBs, Referenzen, obfuszierter Code, Native-Abhängigkeiten,
  Trimming/AOT und unvollständige Metadaten als `partial` plus Diagnostics;
- nicht dekompilierbarer Root-Snapshot als `failed` oder expliziter Fehler,
  nie als leere Erfolgsliste;
- `not_decidable` bei Extension-Anwendbarkeit ohne Consumer-Projekt;
- project-only-Tools melden auf Assemblys echte Capability-Fehler;
- keine Aussagen über Laufzeit- oder Consumer-Anwendbarkeit aus dem Snapshot.

Die verbleibenden Assembly-/Precision-Befunde aus der bisherigen Paketgruppe
06 gehören hierher, wenn sie speziell durch Snapshot-, Herkunfts- oder
Decompiled-Semantik verursacht werden. Allgemeine Source-Precision bleibt in
Task 02.

## Analysegrenzen

Ein Decompiled-Kontext ist kein Originalquellcode. Antwortfelder machen
`origin=decompiled`, Generation, Content-Identität, Snapshot-Herkunft,
Completeness und fehlende Referenzen sichtbar. Git, Build, Tests, Original-
zeilen und reguläres Linting bleiben `unsupported` beziehungsweise nicht
entscheidbar.

Assemblyinhalt, Kommentare, Strings und Typnamen sind untrusted Input und
dürfen keine Agenten- oder Serverinstruktionen überschreiben.

## Muss-Kriterien

- ein Assembly-Typ lässt sich aus dem Katalog mit einer gültigen ID bis zum
  Detail-Call verfolgen;
- alte und aktuelle Generationen sind eindeutig und nicht mit Target-Mismatch
  verwechselt;
- `partial`, Diagnostics sowie Body-/Member-Limits sind sichtbar und wahr;
- fehlende Caller, Referenzen oder Tests in einem partiellen Snapshot werden
  nicht als globale Negativaussage ausgegeben;
- Extension-Anwendbarkeit wird ohne Consumer-Kontext als `not_decidable`
  kenntlich gemacht;
- Project-only-Tools liefern `unsupported` statt irreführender leerer
  Ergebnisse;
- Snapshot- und Lease-Lifecycle bleiben unter Parallelzugriff und Cleanup
  deterministisch;
- der read-only Assembly-Happy-Path aus dem gemeinsamen Rahmen bleibt grün.

## Non-Goals

- keine Ausführung oder dynamische Ladung von Assemblies;
- kein Ersatz für Originalquellcode, PDB- oder Consumer-Kontext;
- kein Assembly-Linting gegen dekompilierten Code;
- keine Git-, Build- oder Runtime-Coverage-Aussagen;
- keine Änderung des öffentlichen Target-Vertrags; dieser liegt in Task 03;
- keine allgemeine Source-Heuristikneuentwicklung; diese liegt in Task 02.

## Risiken und Alternativen

Ein größerer Snapshot kann CPU, Speicher und Diskbudget stark belasten.
Bestehende Ressourcenlimits und getrennte Assembly-Leases bleiben daher
erhalten. Eine künstliche Zusammenlegung mit Project-Lifecycle würde die
bereits getesteten Concurrency-Grenzen gefährden.

Eine vollständige Assembly-Parität zur Source-Solution wäre fachlich falsch,
weil ein Decompiled-Snapshot keinen Consumer- oder Laufzeitbeweis besitzt.
Die Zielqualität ist deshalb ehrliche Navigation mit sichtbaren Grenzen.

## Verifikation und Dokumentation

- gleichnamige Assemblies aus verschiedenen Pfaden;
- gleicher Pfad mit geändertem Inhalt;
- Mutation während Hashing/Dekompilierung;
- parallele Erstöffnung, Generationen, stale IDs und Cleanup;
- fehlende PDBs/Referenzen sowie nicht dekompilierbare Snapshots;
- Katalog-, Typ-, Member-, Body-, Graph-, Extension- und Cross-Target-Ketten;
- Response-Budgets, Diagnostics, Completeness und Capability-Fehler;
- Fast-/Integration-/Concurrency-Tests, gezielte Stress-Tests nur auf
  ausdrückliche Anforderung;
- `Docs/agent-api.md`, `Docs/integration.md` und Assembly-Grenzen in den
  MCP-Instructions synchronisieren;
- `dotnet build` und beide vollständigen Nicht-Stress-Testläufe vor Abschluss;
- nach Abschluss vollständiger 45-Finding-Audit und Review der positiven
  Baseline.

## Abnahme / Release-Gate

Der Task ist releasefähig, wenn Assembly-Navigation über mehrere Folge-Calls
stabil funktioniert, Snapshot-Identität und Lebensdauer nachvollziehbar sind
und kein partieller Decompiled-Befund als Source-, Laufzeit- oder
Consumer-Aussage missverstanden werden kann.
