# Roadmap: Behebung der Usability- & Token-Cost-Findings des AiNetLinter MCP-Servers

Diese Roadmap trackt die vollständige, schrittweise Umsetzung aller 10 verifizierten Findings aus [findings.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/using-audit-funktionstest/findings.md).
Jedes Paket wird autonom implementiert, durch Tests abgesichert, auf Safeguard/Linter-Verstöße geprüft und mit fachlich sauberem Commit gesichert.

---

## Fortschrittsübersicht

- [x] **Paket 1: Call-Tree Sufficiency & Symbol Resolution (`[F-04]`, `[F-06]`)**
  - [x] `[F-04]` Signature-Only Erkennung in `GetCallTreeTool`: `DecompiledSignatureOnlyLimitation` statt falschem Sufficiency-Hint
  - [x] `[F-06]` `ResolveByLineAsync`: Deklarierte Member auf der Zeile vor referenzierten Typen und Parametern priorisieren
  - [x] `[F-06]` `SymbolIdentifierResolver.TryResolveByStableIdAsync`: Optionales `~ReturnType`-Suffix für Standard-DocCommentIds normalisieren
  - [x] `[F-06]` `FindSymbolTool`: Ausgabe von `id: <DocCommentId>` auch für Projekt-Symbole
  - [x] Tests für Paket 1 (Unit-/Component-Tests)
  - [x] Commit Paket 1

- [x] **Paket 2: Feature-Context Callers & Test Mapping (`[F-07]`)**
  - [x] `[F-07]` `DiffImpactAnalyzer.FindCallSiteEntriesAsync`: Aufrufende Methode via `semanticModel.GetEnclosingSymbol` erfassen
  - [x] `[F-07]` `FeatureContextFormatter.AppendCallersSection`: Formatierung `{call.CallerMethod}() in {call.ProjectName}`
  - [x] `[F-07]` `TestDetector.MatchesTestClassName`: Präfix-Suffix-Matching (`Target*Tests`, `Target*Test`, `Target*Fixture`)
  - [x] Tests für Paket 2 (Unit-/Component-Tests)
  - [x] Commit Paket 2

- [x] **Paket 3: File-Tree Completeness & Truncation Transparency (`[F-08]`)**
  - [x] `[F-08]` `GetFileTreeScanner` / `FileTreeAccumulator`: `"maxDepth"` in `TruncatedBy` aufnehmen wenn Verzeichnisse wegen Tiefe beschnitten wurden
  - [x] `[F-08]` `GetFileTreeRenderer.AppendCompleteness`: Bei `"maxDepth"` zwingend `[partiell]` ausgeben
  - [x] `[F-08]` `GetFileTreeRenderer.AppendTree`: Redundante flache Dateiliste nach Verzeichnisbaum entfernen
  - [x] Tests für Paket 3 (Unit-/Component-Tests)
  - [x] Commit Paket 3

- [x] **Paket 4: Linter Ergonomics & Signal-to-Noise Ratio (`[F-09]`, `[F-10]`)**
  - [x] `[F-09]` `MagicValuesStringHeuristics.ClassifyHeaderIdentifierCandidate`: Strings mit führendem `-` oder `--` (CLI-Optionen) ausschließen
  - [x] `[F-09]` `FindMagicValuesTool`: Default `MinOccurrences` auf 2 anheben
  - [x] `[F-10]` `DuplicateDetectionToolRegistrations`: Default `scopeType` auf `"production"` setzen
  - [x] Tests & Dokumentation für Paket 4
  - [x] Commit Paket 4

- [x] **Paket 5: Assembly Analysis Latency, Budgets & Truncation (`[F-01]`, `[F-02]`, `[F-03]`, `[F-05]`)**
  - [x] `[F-01]` `AssemblyAnalysisToolRegistrations`: `includeReferences` Default ausnahmslos auf `false`
  - [x] `[F-02]` `AssemblyAnalysisResponseLimits`: `MaxResponseBytes` auf 32 KB anheben
  - [x] `[F-02]` `AssemblyAnalysisResponseLimits.Budget`: Trimming-Strategie überarbeiten (Mindestkontingent an Membern pro Typ, Typen statt alle Member kappen)
  - [x] `[F-03]` `InspectAssemblyFormatter` & `FindAssemblyExtensionsResponseBuilder`: `(gekürzt: ...)` nur bei tatsächlicher Kürzung der Teilliste
  - [x] `[F-05]` `AssemblyFindSymbolTool`: Sampling von Diagnosen (max 5) & Unterdrückung interner Budget-Logs
  - [x] Tests für Paket 5
  - [x] Commit Paket 5

- [x] **Abschluss-Verifikation & Definition of Done**
  - [x] Vollständiger Testlauf: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` (2391 bestanden)
  - [x] Vollständiger Testlauf: `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (384 bestanden)
  - [x] `dotnet build` warnungsfrei (0 Fehler, 0 Warnungen)
  - [x] Safeguard-Score auf 10,00/10 verifiziert (0 Verstöße, 988 Klassen)
  - [x] DRY-, MagicValues- und DeadCode-Audit via MCP-Tools
  - [x] Agent-Rules Sync (`rules.json` unverändert)
  - [x] Dokumentation synchronisiert (`Docs/agent-api.md`)
  - [x] Finaler Abschluss-Commit
