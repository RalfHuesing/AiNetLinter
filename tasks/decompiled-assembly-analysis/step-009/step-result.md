---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 009
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: d281414747751b1370e5079f797742c34ebc1378
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 009: Source-backed Assembly-Context mit Decompilation-Fallback

## Zusammenfassung

Step 009 verbindet eine bereits vorbereitete Source-Auswahl mit der Assembly-
Context-Fabrik. Nur eine konsistente `Matched`-Auswahl mit passender Snapshot-
Identität, lebender Lease, vorhandenem Projekt und verfügbarer Compilation
wird source-backed verwendet. Alle anderen Auswahlzustände laufen ohne
Exception über den bisherigen `AssemblyAnalysisSession`-Decompilationpfad.

Die Source-Compilation bleibt read-only und wird nicht kopiert oder als neuer
Workspace besessen. Target-Assembly-Identität, Referenzdaten und Binary-Hash
kommen source-backed weiterhin aus der statischen PE-/Fingerprint-Auswertung;
`AssemblyOrigin` trägt zusätzlich Snapshot- und Projektprovenienz. Der
Decompilation-Hinweis wird ausschließlich für dekompilierte Origins ausgegeben.

## Änderungen

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelection.cs` — immutable
  Selection- und Request-Vertrag mit Identitätsprüfung sowie nullbarer
  Ablehnung inkonsistenter oder bereits nicht verfügbarer Auswahlen.
- `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs` — optionale
  Source-Provenienz und zentrale `IsDecompiled`-Abfrage im Origin-Modell.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` — minimale
  read-only Lease-Beobachtung für den Fallback bei bereits disposed Lease;
  Ownership und Release-Verhalten bleiben unverändert.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
  — Source-Compilation-Projektion mit statischer Target-Identität und
  unverändertem Session-Fallback; Consumer-Auflösung bleibt auf der
  übergebenen Consumer-Solution.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs` —
  Request-Weiterleitung ohne Provider-/Registry-Abhängigkeit.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisOriginText.cs`,
  `InspectAssemblyTool.cs`, `FindAssemblyExtensionsTool.cs` — gemeinsame
  Herkunftsausgabe für source-backed und decompiled Contexts.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs`
  — fünf deterministische Factory-/Ownership-Regressionen für Match,
  NoMatch, Ambiguous, null/unavailable, Identity-Mismatch, disposed Lease,
  fehlendes Projekt und Registry-Besitz.

## Commits

- **Code-/Test-Commit:** `d281414747751b1370e5079f797742c34ebc1378`
- **Message:** `feat: Source-backed-Kontext anbinden [decompiled-assembly-analysis]`
- **Branch:** `main`
- **Push:** nein
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.

## Tests

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisContextFactoryTests" --no-restore` — grün, 5/5 Tests.
- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1.911/1.911 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360 Tests, Dauer 2 m 21 s.
- MCP-Impact für die Factory sowie MCP-`get_violations` für `Mcp/Assemblies`
  und `Mcp/Tools/AssemblyAnalysis` — keine offenen Violations.
- Stress-Tests wurden nicht ausgeführt.

## Tech-Debt und Auditbefunde

- Die MCP-Linterprüfung meldete zunächst eine durch die Formatter-Anpassung
  entstandene exakte Duplizierung von `AppendOrigin`. Sie wurde direkt im
  berührten Paket durch `AssemblyAnalysisOriginText` konsolidiert; die
  anschließende Prüfung meldet 0 Violations.
- `TD-001` sowie breite DRY-, MagicValues- und DeadCode-Sweeps wurden nicht
  angefasst.

## Abweichungen vom Plan

- Die allgemeine Coder-Skill-Vorgabe zur Aktualisierung von `codemap.md` wurde
  entsprechend dem ausdrücklichen Step-Auftrag nicht angewendet. `codemap.md`,
  `task-state.md`, `roadmap.md`, `tech-debt.md`, frühere Steps und Docs blieben
  unverändert.
- Für den explizit geforderten disposed-Lease-Fallback wurde `IsDisposed` als
  minimale, nicht mutierende Consumer-Beobachtung an `SourceSnapshotLease`
  ergänzt. Die Factory gibt die Lease nicht frei und übernimmt keinen
  Workspace- oder Registry-Besitz.
- Provider-, Registry-Lookup-, MCP- und Daemon-Komposition wurden nicht
  verdrahtet; der bestehende Vier-Argument-Einstieg bleibt ohne Source-
  Selection und nutzt weiterhin ausschließlich den Decompilationpfad.

## Beobachtungen

Die Factory führt keine Alias- oder Projektheuristik aus. Sie verwendet nur
`MatchedCandidate.ProjectId` aus der gelieferten Auswahl und löst dieses
Projekt in der geleasten Snapshot-Solution auf. `NoMatch` und `Ambiguous`
werden als transportierte Fallback-Auswahlen akzeptiert, aber niemals als
Source-Projektion verwendet. Die externe Source-Solution bleibt vollständig
vom Consumer-Receiver-Lookup getrennt.

Der source-backed Context besitzt bewusst `Generation=0`, keinen generierten
Dokumentpfad und keine Decompilation-Session. Die statischen Target-Referenzen
bleiben auch bei Source-Projektion sichtbar; bei deren Warnungen wird der
Context als `Partial` markiert.

## Bekannte Unschärfen

Die aktuelle MCP-Tool-Komposition übergibt noch keine Source-Selection; der
neue Request-Vertrag ist die konsumierbare Factory-/Service-Grenze für den
folgenden Adapter-Step. Deshalb können die beiden Formatter im regulären
MCP-Pfad dieses Steps noch keine source-backed Auswahl erhalten, sind aber für
den neuen Origin-Typ vorbereitet.

Eine nichtnullige Roslyn-Compilation wird als verfügbar verwendet, auch wenn
die Source-Solution eigene Compilerdiagnosen enthalten kann. Die Factory
projiziert keine zusätzlichen Source-Compilerdiagnosen; sichtbare Diagnosen
des source-backed Contexts stammen aus der statischen Target-Referenzauflösung.
Eine feinere Source-Compilation-Fehlersemantik ist nicht Teil dieses Steps.
