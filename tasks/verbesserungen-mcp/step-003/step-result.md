---
status: done
type: step-result
task: verbesserungen-mcp
step: 003
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-05T13:00:00Z
code_commit_hash: 48d596c
status_after: done
blocker_category: n/a
---

# Result Step 003: Einheitlicher Symbol-Identifikator-Parser

## Zusammenfassung

`FindReferencesTool.ResolveSymbolAsync` prueft jetzt zuerst per
`SymbolIdentifierResolver.TryResolveByStableIdAsync`, ob `identifier` eine
stabile DocumentationCommentId ist, bevor wie bisher auf
Datei:Zeile:Spalte bzw. qualifizierten Namen zurueckgefallen wird. Da
`find_references`, `get_impact`, `get_type_hierarchy` und `get_symbol_body`
bereits alle ueber diese eine Methode laufen, profitieren alle vier
gleichzeitig. `GetSymbolBodyTool`s eigene, jetzt redundante
Stable-ID-Vorstufe wurde entfernt (reine Simplification, keine
Verhaltensaenderung). Tool-Beschreibungen und der Klassenkommentar von
`SymbolIdentifierResolver` wurden entsprechend aktualisiert.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` — neuer, vorgeschalteter
  Stable-ID-Zweig in `ResolveSymbolAsync`; Klassen- und Methoden-XML-Doc
  aktualisiert.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` — private
  `TryResolveByStableIdAsync`-Vorstufe entfernt, `ExecuteAsync` ruft direkt
  `FindReferencesTool.ResolveSymbolAsync` auf.
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` —
  `FindReferencesDescription`, `GetImpactDescription` und
  `GetTypeHierarchyDescription` (Bonus) um DocumentationCommentId-Hinweis
  ergaenzt.
- `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs` —
  Klassen-XML-Doc aktualisiert: `FindReferencesTool.ResolveSymbolAsync` als
  gemeinsamer Einstiegspunkt fuer alle vier Tools benannt.
- `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` — 2 neue
  Tests: `ResolveSymbolAsync_StableId_ReturnsSymbolAtId` (Unit-Ebene) und
  `ExecuteAsync_StableId_ReturnsCallSiteInCaller` (End-to-End, prueft
  Konzept-DoD-Schnellcheck-Punkt 3).
- `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` — 1 neuer Test:
  `ExecuteAsync_StableSymbolIdentifierGiven_ReturnsCallSites`.
- `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` — 1
  optionaler Bonus-Test: `ExecuteAsync_StableTypeIdentifier_ReturnsInterfaceAndDerivedClass`.

## Commit

- **Code-Commit-Hash:** `48d596c`
- **Message:**
  ```
  refactor(mcp): FindReferencesTool.ResolveSymbolAsync um Stable-ID-Zweig ergaenzen [verbesserungen-mcp]

  find_references, get_impact und get_type_hierarchy akzeptieren jetzt wie
  get_symbol_body eine stabile DocumentationCommentId, weil alle vier Tools
  bereits ResolveSymbolAsync als gemeinsamen Einstiegspunkt nutzen. Die
  duplizierte Stable-ID-Vorstufe in GetSymbolBodyTool entfaellt dadurch
  ersatzlos. Tool-Beschreibungen und Klassenkommentar aktualisiert.

  Refs: tasks/verbesserungen-mcp/step-003
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin —
  Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build → grün (0 Fehler, 0 Warnungen)
dotnet test  → grün (1261 Tests, 0 Fehler, kein Testhost-Absturz)
```

Gezielter Vorablauf (nur die vier betroffenen Testklassen, 43 Tests)
ebenfalls gruen, danach genau ein vollstaendiger `dotnet test`-Lauf als
Pflicht-Gate vor dem Commit.

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt, inklusive des optionalen Bonus-Tests fuer
`GetTypeHierarchyToolTests`.

## Beobachtungen

- Die Methoden-XML-Doc von `SymbolIdentifierResolver.TryResolveByStableIdAsync`
  (Zeilen 64-73 vor der Aenderung) beschreibt noch das alte Bild „der
  Aufrufer kann auf `FindReferencesTool.ResolveSymbolAsync` zurueckfallen"
  — nach diesem Step ist `ResolveSymbolAsync` selbst der Aufrufer dieser
  Methode, nicht mehr ein externer Fallback-Nutzer. Der Plan hat fuer
  Datei 4 explizit nur den Klassenkommentar vorgesehen, nicht diesen
  Methodenkommentar und auch keine Aenderung an der Methode selbst — daher
  bewusst nicht angefasst, um keinen Scope zu erweitern. Kleine, potenziell
  in einem spaeteren Schritt aufzuraeumende Doku-Ungenauigkeit.
- Der aus `Konzept.md`/EPIC-03 bekannte `get_symbol_body`-ID-Korruptions-
  verdacht ist beim Testen **nicht** sichtbar geworden: Die bestehenden 5
  `GetSymbolBodyToolTests` (inkl. `ExecuteAsync_ValidStableId_
  ReturnsBodyForMethod` und `ExecuteAsync_InvalidStableId_FallsBackToFileLineCol`)
  liefen unveraendert gruen im vollen Testlauf — kein Hinweis auf eine
  Regression oder ein neu sichtbares Symptom durch diesen Step.

## Bekannte Unschärfen

- Konzept-DoD-Schnellcheck-Punkt 3 (`find_references(id aus skeleton)` →
  Treffer > 0) wurde nicht manuell live gegen den MCP-Server, sondern ueber
  die neuen automatisierten Tests nachvollzogen:
  `FindReferencesToolTests.ExecuteAsync_StableId_ReturnsCallSiteInCaller`
  ruft `FindReferencesTool.ExecuteAsync` mit einer per
  `DocumentationCommentId.CreateDeclarationId` erzeugten stabilen ID auf
  und prueft `Caller.cs` im Ergebnistext (> 0 Treffer). Das deckt den
  Schnellcheck-Punkt ab, ist aber kein Live-Dogfood-Lauf gegen das reale
  Repo (waere ohnehin ausserhalb der in `AiNetLinterRichtlinien.mdc` §4
  erlaubten Testinfrastruktur, siehe `McpLiveRepositoryTests` fuer den
  bereits bestehenden Live-Dogfood-Kanal).
- `get_type_hierarchy`-Bonus-Test wurde mit `BaseGreeting` (Typ, keine
  Methode) durchgefuehrt — deckt damit implizit ab, dass der neue
  Stable-ID-Zweig auch fuer `T:`-Praefixe (nicht nur `M:`) funktioniert;
  im Plan selbst war das nicht explizit gefordert, ergab sich aber
  natuerlich aus der Fixture-Wiederverwendung.
