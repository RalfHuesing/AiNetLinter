---
status: blocked
type: step-result
task: decompiled-assembly-analysis
step: 004
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T14:37:34+02:00
code_commit_hash: 9f934109dd6119223170843e0f2e9ea5f27f1ffa
status_after: blocked
blocker_category: content
---

# Result Step 004: Assembly-Session-Fundament korrigieren: Cache, Limits, Referenzen und Identität

## Zusammenfassung

Das Korrekturpaket kapselt das flache Manifest hinter einem fokussierten JSON-Converter, führt immutable Cache-Generationen mit current.json und last-good-Adoption ein und validiert Manifest, Dateien, Fingerprints, Referenzen und Roslyn-Compilation vor dem Publish. Die Decompilation verwendet verschachtelte Typ-/Member-/Komplexitätsbudgets ohne Whole-Module-Fallback; der PEReader-basierte Resolver prüft Assembly-Identitäten und gibt ResolvedPath sowie Diagnosen weiter. Assembly-Identität wird bis in Context und Inspect-Payload transportiert; die freien Integration-Scans nutzen den zentralen Generated-/bin-Filter. Der Implementierungsstand ist im Code-Commit gesichert, aber der verpflichtende Whole-Solution-Dogfood-Gate bleibt wegen drei bestehender Test-DuplicateCode-Befunde außerhalb dieses Step-Scopes blockiert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs`, `AssemblyAnalysisSessionModels.cs` — Session-Lifecycle, typisierte Manifestgruppen, Generation-Requests und Identitätsweitergabe.
- `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationCache.cs` und `AssemblyDecompilationManifestJsonConverter.cs` (neu) — immutable Generationen, Pointer-Publish, strikte Validierung und flaches JSON-Wire-Format.
- `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationAdapter.cs`, `AssemblyReferenceResolver.cs`, `AssemblyRoslynWorkspaceFactory.cs` — begrenzte Nested-Trees, PE-Identitätsauflösung und Compilation-Gate.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`, `AssemblyAnalysisModels.cs`, `InspectAssemblyTool.cs` — echte Identität, ResolvedPath und sichtbare Referenzdiagnosen in Context/Inspect.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs`, `AssemblyAnalysisToolTests.cs` — Cache-, Budget-, Identitäts- und Resolverregressionen.
- `src/AiNetLinter.IntegrationTests/Architecture/McpProcessArchitectureGuardTests.cs`, `Platform/LoadedFixtureTests.cs` — zentrale Generated-/bin-Filter in den freien Scans.

## Commit

- **Code-Commit-Hash:** `9f934109dd6119223170843e0f2e9ea5f27f1ffa`
- **Message:**
  ```
  fix: Assembly-Session korrigieren [decompiled-assembly-analysis]

  Refs: tasks/decompiled-assembly-analysis/step-004
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Resultat.

## Build-/Test-Output

```
dotnet clean AiNetLinter.slnx --verbosity quiet → grün
dotnet build AiNetLinter.slnx → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1.868 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → fehlgeschlagen im Whole-Solution-Dogfood: CliRepositoryDogfoodTests meldete zunächst MaxLineCount 501 plus 3 DuplicateCode-Befunde
dotnet build AiNetLinter.slnx (nach der scope-konformen Zeilenzahlkorrektur) → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress (Wiederholung) → Gate nicht abschließend grün; verbleibende drei DuplicateCode-Befunde außerhalb des Step-Scopes
AiNetLinter-MCP get_violations → 3 Warnungen, keine Fehler; keine Produktionswarnung mehr
```

## Abweichungen vom Plan

Die Roslyn-Zeilenmessung zählte den abschließenden Zeilentrenner der Cache-Datei als 501. Eine einzelne überflüssige Leerzeile wurde deshalb innerhalb des betroffenen Planbereichs entfernt. Der Step-Plan wurde wegen des nicht erfüllbaren Abschluss-Gates auf `blocked` gesetzt; eine Bereinigung oder Suppression der drei fremden Test-DuplicateCode-Befunde wurde ausdrücklich nicht vorgenommen, um den Nutzer-Scope nicht zu erweitern. Produktdokumentation blieb unverändert, da der Step-Plan keine inhaltliche CLI-/Konfigurationsänderung vorsieht.

## Beobachtungen

Die drei verbleibenden Warnungen sind `AssemblyAnalysisSessionTests.EmitAssembly` gegenüber `AssemblyAnalysisToolTests.EmitAssembly` sowie `TextOf` und `WaitForConditionAsync` in den beiden Wiring-Contract-Testklassen. Der MCP-Befund und die direkte Whole-Solution-Linter-Reproduktion zeigen nach der Cache-Zeilenkorrektur denselben Restbestand; die Wiring-Datei wurde in diesem Step nicht verändert. Die zuvor auftretende Dateisperre durch liegengebliebene Testhost-/Daemon-Prozesse war ein temporärer Infrastrukturzustand und wurde durch Beenden der eindeutig zu diesem Lauf gehörenden Prozesse bereinigt; sie ist nicht der finale Blocker. Es wurden keine Tech-Debt-Einträge angelegt.

## Bekannte Unschärfen

Der zweite vollständige Integration-Lauf beendete den Testprozess, lieferte über die Ausführungsschnittstelle jedoch kein abschließendes Summary; die deterministische direkte Dogfood-Ausgabe und `get_violations` bestätigen den verbleibenden Drei-Warnungen-Bestand. Der Kritiker sollte entscheiden, ob diese bestehenden Test-Duplikate in einem ausdrücklich erweiterten Scope bereinigt/suppressiert oder als Gate-Baseline behandelt werden dürfen. Ohne eine solche Entscheidung kann der vorgeschriebene vollständige Integration-Gate-Lauf nicht als grün bescheinigt werden.

## Falls Status `blocked`

**Blocker-Art:** `content`

**Blockiert weil:** Der verpflichtende Whole-Solution-Dogfood-Test bleibt wegen drei bestehender `DuplicateCode`-Warnungen in Testdateien außerhalb des Step-Plans nicht grün.

**Brauche von Nutzer:** Freigabe für einen ausdrücklich erweiterten Scope zur Bereinigung/Suppression dieser drei Test-Duplikate oder eine dokumentierte Ausnahme vom Gate.

**Aktueller Stand:** Das gesamte step-004-Korrekturpaket ist implementiert und im Code-Commit `9f934109dd6119223170843e0f2e9ea5f27f1ffa` gesichert; Build und FastTests sind grün, der finale Integration-Gate-Nachweis fehlt.
