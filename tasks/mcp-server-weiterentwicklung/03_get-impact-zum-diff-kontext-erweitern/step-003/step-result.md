---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 003
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-22T23:30:00+02:00
code_commit_hash: 85c7fdce
status_after: done
blocker_category: n/a
---

# Result Step 003: Breiter Diff-Symbolscanner (change-context-Scope) mit kollisionsfreien stabilen IDs

## Zusammenfassung

Der Analyzer hat zwei klar benannte Eintrittspunkte auf einem gemeinsamen
Request-Record-Kern: `AnalyzeDiffAsync` (Scope `Callers`, unverändert) und
neu `AnalyzeChangeContextAsync` (Scope `ChangeContext`). Die gesamte
Symbolermittlung — Kandidatenmenge je Scope, Range-Überlappung,
Innerste-Deklarations-Regel, Accessibility-Filter und knotenbasierte
Entry-Bildung — liegt jetzt im neuen `Core/DiffSymbolScanner.cs`; der breite
Scope deckt Methoden, Konstruktoren, Properties/Indexer, Events (custom und
field-like), Felder, Typdeklarationen, Delegates und lokale Funktionen ohne
Accessibility-Filter ab, mit artabhängigen Anzeigenamen. **TD-002-Entscheidung
(Plan-Pflicht):** der ID-Sonderfall sitzt IN `GetStableSymbolId` (gemeinsame
Quelle, kein Scanner-lokaler Wrapper) — lokale Funktionen erhalten die stabile
ID des nächsten nicht-lokalen einschließenden Members plus deterministisches
Suffix `#lf:<Name>@<Zeile>:<Spalte>` (1-basiert, aus der Symbol-Location);
der Nicht-LF-Pfad bleibt Ausdruck für Ausdruck identisch, damit bestehende IDs
und `callers`-Snapshots unverändert bleiben. Der step-002-Pinntest
(`CreateChangedSymbolEntry_ForLocalFunction_UsesSharedStableIdLogic`) blieb
unangetastet grün. **MINOR aus dem step-002-Review mitgefixt:** die falsche
XML-Doc-Aussage „Fallback … z. B. lokale Funktionen“ an `GetStableSymbolId`
ist im selben Doc-Block korrekt neu gefasst (Sonderfall explizit
beschrieben); zusätzlich trägt `ChangedSymbolEntry.SymbolId` den
erweiterten Vertragstext.

## Geänderte Dateien

- `src/AiNetLinter/Core/DiffSymbolScanner.cs` (neu) — Enum
  `DiffSymbolScope`, breiter Scanner (`FindChangedSymbolsAsync`),
  Kandidaten-/Innerste-/Überlappungslogik für beide Scopes,
  artabhängige `FormatDisplayName`.
- `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` — dünne Umschaltung:
  `AnalyzeDiffAsync`/`AnalyzeChangeContextAsync` → privater
  `RunAnalysisAsync(DiffAnalysisRequest)`; `CreateChangedSymbolEntry`-
  Überladung mit expliziter Location; `FormatMemberDisplayName` internal;
  Datei dadurch von 485 auf 447 Zeilen.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs` —
  `GetStableSymbolId` mit lokalen-Funktions-Sonderfall
  (`FormatLocalFunctionId`) + neu gefasste XML-Doc (MINOR-Fix).
- `src/AiNetLinter/Core/DiffImpactAnalysisModels.cs` — nur XML-Doc an
  `ChangedSymbolEntry.SymbolId` (`#lf:`-Sonderfall im Vertragstext).
- `src/AiNetLinter.FastTests/Core/DiffImpactAnalyzerBroadScopeTests.cs`
  (neu) — neun Unit-Tests laut Plan-Testliste (serverlos über
  `CreateScenario` + synthetische Hunk-Ranges).
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SymbolGraph/GetImpactToolIntegrationTests.cs`
  — End-to-End-Test
  `AnalyzeChangeContextAsync_OnModifiedPrivateMethod_ListsSymbolWithoutCallSites_AndCallersWrapperOmitsIt`.
- `src/AiNetLinter.IntegrationTests/Fixtures/FixtureWorkspaces.cs` — neue
  Change-Methode `ChangeCalculatorNormalizeBodyWithoutCommitting()`.
- `tests/Fixtures/GitImpactMini/src/GitImpactMini/Calculator.cs` — Fixture-
  Template um private Methode `Normalize` erweitert (im Initial-Commit
  enthalten, damit der Integrationstest eine BESTEHende private Methode
  ändert).

## Commit

- **Code-Commit-Hash:** `85c7fdce`
- **Message:**
  ```
  feat: breiter Symbolscanner [03_get-impact-zum-diff-kontext-erweitern]

  Der Git-Diff-Zweig erhaelt den zweiten, klar benannten Scannerpfad ... (Body gekürzt)

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-003
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1600 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (347 Tests, 0 Fehler)
```

Schnelliteration während der Entwicklung: nur
`FullyQualifiedName~DiffImpactAnalyzerBroadScopeTests` (9/9) bzw. der neue
Integrationstest einzeln (1/1).

## Abweichungen vom Plan

1. **Schmaler Pfad läuft mit durch die einheitliche Scanner-Pipeline (statt
   paralleler Altfunktionen im Analyzer):** Der Plan fordert einerseits
   „Innerste-Filter … trotzdem uniform angewendet, damit es nur eine Regel
   gibt“ und andererseits „Callers → exakt die heutige Logik … unverändert“.
   Wörtlich gleichzeitig geht das nur, wenn beide Scopes dieselbe Pipeline
   nutzen — deshalb wanderten `GetChangedSymbolsAsync`,
   `AddChangedSymbols`, `GetValidChangedSymbol`,
   `IntersectsWithChangedLines` und `IsPublicOrInternal` in den Scanner
   (dort private Helfer), statt im Analyzer zu bleiben und doppelt zu
   existieren. Die callers-Behauptungen sind strukturell gesichert:
   Kandidatensammlung im Scope `Callers` exakt im bisherigen
   Zwei-Durchlauf (erst alle Methoden, dann alle Konstruktoren, je in
   Quellreihenfolge — Reihenfolge bleibt byte-identisch), gleiche Filter in
   gleicher Reihenfolge. Belegt durch alle Bestands-Tests rund um
   `get_impact`/`find_references`/Wrapper/Subprozess/Dogfood — komplett
   ohne Anpassung grün.
2. **Fixture-Template erweitert (nicht im Plan-Abschnitt „Konkrete
   Änderungen“ genannt, vom Integrationstest gefordert):** Der Plan verlangt
   den Ende-zu-Ende-Test „nach dem Initial-Commit eine private Methode
   ändern“, die bestehende Fixture hatte aber nur die öffentliche `Add`.
   `Calculator.cs` trägt deshalb eine private, nie aufgerufene `Normalize`;
   alle Bestandsnutzer derselben Fixture (Integrationstests, Subprozess- und
   Contract-Tests) bleiben ohne Anpassung grün (Contains-basierte
   Assertions, `Single`-Filter auf Dateiebene).
3. Sonst Plan 1:1 umgesetzt (kein Tool-Wiring, kein Docs/agent-api.md/README-
   Touch, Git genau einmal im Kern, kein bool-Flag, Records/Parametergrenzen
   eingehalten).

## Beobachtungen

- **LF-Displayname bei verschachtelten Typen — Wortlaut-Auslegung:** Der
  Plan definiert lokale Funktionen als
  „EnthaltendeMethode-im-bisherigen-Format.Name“. „Bisheriges Format“ ist
  `FormatMemberDisplayName` = „EnthaltenderTyp.Name“ OHNE Namensraum — für
  eine lokale Funktion in `Outer.Inner.Work` heißt das
  `Inner.Work.Helper` (nicht namensraumqualifiziert wie bei
  Typdeklarationen). So implementiert und so gepinnt; wer stattdessen die
  volle Kette möchte, müsste den Vertrag ändern (EPIC-7-Doku nennt nur das
  Beispiel `LocalFuncHost.Run.Scale`, das beide Lesarten erfüllt).
- **Roslyn liefert für lokale Funktionen `DeclaredAccessibility = Private`**
  (nicht `NotApplicable`). Der Plan erlaubt explizit „den Symbolwert — wie
  Roslyn ihn liefert“; Entries tragen daher `Private`. Für die EPIC-7-Doku
  merken: Accessibility von lokalen Funktionen im `change-context` liest
  sich wie „private“, ist aber Roslyns Default-Angabe.
- **Kantenfall der Innerste-Regel bei mehreren Hunks je Datei:** Die
  geplante Regel sammelt ÜBERLAPPENDE Kandidaten dateiweit und wirkt dann
  Container weg. Trifft ein Hunk einen Member und ein zweiter Hunk
  dieselber Datei die Deklarationszeile des enthaltenen Typs, erscheint NUR
  der Member — der Typ wird zwar eigenständig getroffen, enthält aber einen
  anderen Kandidaten. Eine strenge „pro Zeile“-Zuweisung würde hier zwei
  Entries liefern. Ich habe genau die geplante Regel implementiert (keine
  Eigenmächtigkeit); falls der Kritiker das Verhalten anders will, ist es
  eine bewusste Vertragsentscheidung, kein Bugfix.
- **Dogfooding ausgeführt:** `metrics_lookup` über 14 neue/geänderte
  Symbole — LOC/CC/CogC/Parameter durchweg im Grünen (max. 4 Parameter,
  kein bool-Parameter); `find_duplicates` (clone/near, minTokens 20, Scope
  `src/AiNetLinter`, 1464 Methoden) — keine Cluster. Erster Versuch mit
  handgeschriebenen Doc-ID-Signaturen löste nicht auf (SYMBOL_NOT_FOUND),
  qualifizierte Namen ohne Parameterliste funktionierten.
- **Externes Commit bemerkt:** Zwischen Sessionstart (HEAD `e31006dd`) und
  meinem Commit lag ein fremder Doku-Commit `a4987709` („Konzept 11 …“) auf
  main. Mein Code-Commit darauf ohne Konflikt; nur der Vollständigkeit halber
  gemeldet.

## Bekannte Unschärfen

- **Byte-Identität des `callers`-Pfads:** belegt durch die unverändert
  grünen Bestands-, Subprozess- und Dogfood-Tests plus die strukturell
  erhaltene Reihenfolgenlogik — nicht durch einen direkten Alt/Neu-Diff.
  Ein theoretischer Feldunterschied bleibt: Entries entstehen jetzt über
  Knoten-/explizite Location statt `symbol.Locations.First(IsInSource)`.
  Bei partiellen METHODEN (gleiches gemergtes ISymbol, zwei Syntaxteile)
  könnte die Spanne nun die geänderte Teildeklaration zeigen statt der
  ersten Symbol-Location. Für die schmale Kandidatenmenge praktisch ohne
  Bestandsabdeckung, aber der Kritiker soll es wissen.
- **Reached-From-ID-Stringwechsel** für Call-Sites im Body lokaler
  Funktionen (`find_references`/get_impact-Symbol-Branch): von der
  mehrdeutigen Doc-ID des umgebenden Members zur eindeutigen `#lf:`-ID —
  laut Plan-Notes bewusste Korrektur, Doku-Pflicht steht bei EPIC-7. Von
  meinen Tests nicht separat gepinnt (Traversal-Seite unverändert).
- Die Roslyn-Semantik von `DocumentationCommentId.CreateDeclarationId` bei
  lokalen Funktionen bleibt empirisch beobachtet (fortgeschriebene
  Unschärfe aus step-002); mein Sonderfall greift VOR dem Doc-ID-Aufruf,
  sodass das Verhalten dort nicht mehr darauf ankommt.
