---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 008
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T21:00:00+02:00
code_commit_hash: 9511b8f2d20e50cbfb8a11ed87c346ffd95a0465
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 008: Deterministische Source-Match-Auflösung über Project.AssemblyName

## Zusammenfassung

Der neue synchrone `AssemblySourceMatchResolver` löst ausschließlich explizit
konfigurierte Assembly-Aliase gegen normalisierte `Project.AssemblyName`-Werte
eines geleasten `ExternalSourceSnapshot` auf. Er liefert immutable Ergebnisse
für `Matched`, `NoMatch` und `Ambiguous` mit Snapshot-Identität, geordneten
Kandidaten, Evidence-Codes und Confidence. Fremde oder bereits freigegebene
Snapshots erzeugen keinen source-backed Treffer; Lease- und Workspace-
Ownership bleibt beim Aufrufer beziehungsweise der Registry.

## Änderungen

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs` — Matchvertrag,
  Alias-/`.dll`-Normalisierung, Identitätsprüfung, Project.AssemblyName-
  Selektion, stabile Sortierung und Evidence-/Confidence-Zustände.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs`
  — neun deterministische In-Memory-Roslyn-Tests für Match, Normalisierung,
  Aliasgrenze, Fallback-Ausschluss, fremde/ungültige bzw. freigegebene
  Snapshots, fehlende/empty Assembly-Namen, Ambiguous-Ausgabe und Lease-
  Nichtbesitz.

## Commits

- **Code-/Test-Commit:** `9511b8f2d20e50cbfb8a11ed87c346ffd95a0465`
- **Message:** `feat: Source-Match-Resolver einführen [decompiled-assembly-analysis]`
- **Branch:** `main`
- **Push:** nein
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.

## Tests

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblySourceMatchResolverTests" --no-restore` — grün, 9/9 Tests.
- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1.906/1.906 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360 Tests, Dauer 2 m 18 s.
- Stress-Tests wurden nicht ausgeführt.
- AiNetLinter-MCP `get_violations` im neuen Produktionsscope — 0 Verstöße.

## Tech-Debt und Auditbefunde

- Der Drift-Audit mit `find_duplicates(scopeDir="src", minTokens=20)` fand
  einen bestehenden Exact-Cluster für `IsDriveQualified` in
  `ExternalSourceMappingValidator` und `SourceSnapshotIdentity`. Beide
  Methoden liegen außerhalb des Resolver-Vertrags beziehungsweise in einem
  unveränderbaren Step-007-Vertrag; eine Konsolidierung wurde deshalb nicht
  in diesen Step gezogen.
- Der Magic-Value-Audit meldete die elf einmalig verwendeten Evidence-Codes.
  Sie sind bereits als lokale typisierte Konstanten gebündelt. Eine weitere
  Auslagerung in ein globales Constants-Modul würde den Resolver-Scope ohne
  Wiederverwendung vergrößern und wurde nicht vorgenommen.
- Der Dead-Code-Audit meldete keinen unreferenzierten Code im neuen Resolver.

## Abweichungen vom Plan

Die Tests können über die öffentliche Roslyn-In-Memory-API kein echtes
`Project.AssemblyName == null` erzeugen: `ProjectInfo.Create` und
`Project.WithAssemblyName` weisen `null` bereits an ihrer API-Grenze ab. Der
Resolver behandelt `null` dennoch defensiv über die nullable
Normalisierungsroutine; die Regressionstests prüfen die erreichbaren leeren
und Whitespace-Werte. Alle übrigen geplanten Zustände und Ownership-
Invarianten sind direkt abgedeckt.

Entsprechend dem Nutzerauftrag wurde `tasks/decompiled-assembly-analysis/codemap.md`
trotz der allgemeinen Coder-Skill-Vorgabe nicht geändert; ebenso
blieben `task-state.md`, `roadmap.md`, frühere Steps, `tech-debt.md` und die
Session-/MCP-/Provider-Dateien unverändert. Der Step-Plan erhält ausschließlich
den vorgeschriebenen Statuswechsel.

## Beobachtungen

Die Identitätsprüfung kanonisiert URL und repository-relativen Solution-Pfad
über den bestehenden `SourceSnapshotIdentity`-Vertrag; die geladene Revision
wird nicht gegen das Mapping verglichen, weil das Mapping keine Revision
enthält. Kandidaten werden nach `FilePath`, `ProjectName`, `AssemblyName` und
einer ordinalen `ProjectId`-Tie-Breaker-Reihenfolge sortiert. `Project.Name`,
Projektpfad und DLL-Dateiname wirken nur als Ergebnisdaten beziehungsweise
Sortierkriterien und niemals als Matchsignal.

Der sichere Einstiegspunkt für den Folge-Step ist der spätere Verbraucher
`AssemblyAnalysisContextFactory`: Dort kann `MatchedCandidate.ProjectId` in
die Session projiziert werden. Resolver, Snapshot, Registry und Lease müssen
für diesen Folge-Step als read-only Matchgrenze erhalten bleiben.

## Bekannte Unschärfen

Der Resolver beweist weder Binary-/PDB-/SourceLink-Versionstreue noch lädt er
Solutions oder Assemblies. Ein Snapshot mit abweichender Repository-URL oder
abweichendem Solution-Pfad wird als `NoMatch` ausgewiesen; ein bereits
disposed Snapshot erhält ebenfalls explizit `NoMatch`. Für mehrere Projekte
mit demselben normalisierten `AssemblyName` gibt es bewusst kein ausgewähltes
Projekt und keine hohe Confidence.
