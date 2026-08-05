---
status: done
type: step-plan
task: verbesserungen-mcp
step: 003
title: "Einheitlicher Symbol-Identifikator-Parser: SymbolIdentifierResolver als gemeinsamer Einstiegspunkt fuer find_references, get_impact und get_symbol_body"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T11:30:00Z
related_to: []
---

# Step 003: Einheitlicher Symbol-Identifikator-Parser

## Bezug

- **Task:** `verbesserungen-mcp`
- **Epic:** `EPIC-02` aus `roadmap.md` — vollstaendig offen, erster Step
  dieses Epics (EPIC-01 ist mit `step-001`/`step-002`/`step-002/fix-01`
  abgeschlossen, siehe Roadmap-Diff in diesem Planer-Lauf).
- **Konzept-Referenz:** `Konzept.md` Scope „P1 — Einheitlicher
  Symbol-Identifikator-Parser": „Alle drei dokumentierten Formate
  (qualifizierter Name, `Datei:Zeile:Spalte`, DocumentationCommentId)
  funktionieren fuer **dasselbe** Symbol in `find_references`,
  `get_symbol_body` **und** `get_impact` gleichermassen." Sowie „Wo im
  Projekt" (`SymbolIdentifierResolver.cs`: „aktuell laut Dateikommentar
  nur fuer `FindReferencesTool` ausgelagert, nicht einheitlich ueber
  alle Tools mit Identifikator-Input genutzt").

## Aktueller Projektzustand (JIT-Kontext)

Vollstaendig gelesen: `SymbolIdentifierResolver.cs`, `FindReferencesTool.cs`,
`GetSymbolBodyTool.cs`, `GetImpactTool.cs`, `GetTypeHierarchyTool.cs`,
`SymbolGraphToolRegistrations.cs`, `SymbolBodyToolRegistrations.cs`, sowie
die drei zugehoerigen Test-Klassen. Kernbefund — **die Luecke ist kleiner
und praeziser als der Konzept-Text vermuten laesst:**

- `FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct)` ist
  bereits heute der **eine** gemeinsame Einstiegspunkt fuer drei von vier
  Symbolgraph-Tools: `FindReferencesTool.ExecuteAsync` ruft ihn direkt,
  `GetImpactTool.ExecuteSymbolBranchAsync` und
  `GetTypeHierarchyTool.ExecuteAsync` delegieren beide ebenfalls direkt
  an ihn (verifiziert per Grep — keine eigene Parsing-Logik in diesen
  beiden Dateien). Es gibt also **keine drei parallelen, voneinander
  abweichenden Implementierungen** zu vereinheitlichen, wie der grobe
  Konzept-Text nahelegt.
- Das Problem: `ResolveSymbolAsync` selbst implementiert nur **zwei** der
  drei Formate — `TryParsePosition` (Datei:Zeile:Spalte) und einen
  Namens-Fallback ueber `SymbolFinder` (qualifizierter/teil-qualifizierter
  Name). Das dritte Format, die stabile `DocumentationCommentId`
  (`SymbolIdentifierResolver.TryResolveByStableIdAsync`, iteriert ueber
  alle `DeclaredSymbolInfo`s der Solution und vergleicht per
  `DocumentationCommentId.CreateDeclarationId`), wird hier **gar nicht**
  aufgerufen.
- `GetSymbolBodyTool.ExecuteAsync` hat als einziges der vier Tools eine
  **eigene, duplizierte** Vorstufe: private Methode `TryResolveByStableIdAsync`
  (Zeile 66-73, ruft `SymbolIdentifierResolver.TryResolveByStableIdAsync`
  auf), davor geschaltet vor den Fallback auf
  `FindReferencesTool.ResolveSymbolAsync`. Deshalb funktioniert die stabile
  ID nur hier — bestaetigt durch `GetSymbolBodyToolTests.
  ExecuteAsync_ValidStableId_ReturnsBodyForMethod` (existiert), waehrend
  weder `FindReferencesToolTests.cs` noch `GetImpactToolTests.cs` einen
  aequivalenten Test haben. Die Tool-Beschreibungs-Konstanten bestaetigen
  das nochmal explizit: `SymbolBodyToolRegistrations.GetSymbolBodyDescription`
  nennt „stabiler ID (DocumentationCommentId, ...)" zuerst, waehrend
  `SymbolGraphToolRegistrations.FindReferencesDescription` und
  `GetImpactDescription` nur „Datei:Zeile:Spalte oder qualifizierter/
  teil-qualifizierter Name" dokumentieren — die Doku selbst raeumt also
  ein, dass `find_references`/`get_impact` das dritte Format nicht
  unterstuetzen.
- **Bereits bestehende Struktur, die wiederverwendet statt dupliziert
  wird (Kern des JIT-Ansatzes):** Statt `SymbolIdentifierResolver` zu
  einem eigenstaendigen dritten Einstiegspunkt mit eigener
  Positions-/Namens-Logik auszubauen (wuerde `DiffImpactAnalyzer.
  FindDocumentByPath` und `FindSymbolTool.FormatSymbolLocations` in eine
  Klasse ziehen, die aktuell bewusst schlank gehalten ist, siehe deren
  eigener Klassenkommentar zum `AIContextFootprint`), wird
  **`FindReferencesTool.ResolveSymbolAsync` um genau einen zusaetzlichen,
  vorgeschalteten Zweig ergaenzt** (stabile ID zuerst pruefen, sonst wie
  bisher Position/Name). Dieser eine Zweig kommt dann automatisch **allen
  vier** Tools zugute (find_references, get_impact, get_type_hierarchy als
  Bonus, get_symbol_body), weil alle vier bereits denselben Aufruf teilen.
  `GetSymbolBodyTool`s eigene, jetzt redundante Vorstufe kann ersatzlos
  entfernt werden (Simplification, keine Verhaltensaenderung: exakt
  dieselbe Reihenfolge stabile-ID-zuerst-dann-Fallback bleibt erhalten,
  nur an einer statt zwei Stellen implementiert).
- **Reihenfolge-Sicherheit gegen Doppel-Interpretation:** Der stabile-ID-
  Zweig wird nur aktiv, wenn `HasKnownDocumentationCommentIdPrefix`
  zutrifft (`identifier` beginnt mit `M:`/`T:`/`P:`/`F:`/`E:`/`!:`) — ein
  billiger `StartsWith`-Check, der fuer normale Datei-Pfade und
  qualifizierte Namen in der Praxis nie zutrifft (Windows-Laufwerksbuchstaben
  `M:`/`T:`/`P:`/`F:`/`E:` waeren die einzige theoretische Kollision;
  dieses Risiko existiert unveraendert bereits heute in
  `get_symbol_body` und wird durch diesen Step nicht neu eingefuehrt,
  nur auf zwei weitere Tools ausgedehnt). Kein Performance-Regressions-
  Risiko fuer den Normalfall: Der teure Solution-weite
  `SymbolFinder.FindSourceDeclarationsAsync`-Scan in
  `TryResolveByStableIdAsync` laeuft nur, wenn dieser Praefix-Check
  bereits positiv war.
- **Bezug zum P2-Punkt „`get_symbol_body`-ID-Korruption" (EPIC-03, nicht
  dieser Step):** `GetSymbolBodyTool.TryGetDeclarationId` ruft
  `DocumentationCommentId.CreateDeclarationId(symbol)` direkt auf dem von
  `FindReferencesTool.ResolveSymbolAsync` (bzw. bei Position-Input
  konkret `SymbolIdentifierResolver.ResolveSymbolAtToken`) aufgeloesten
  Symbol auf — **derselbe Code-Pfad**, den dieser Step anfasst. Der P2-Bug
  ist damit vermutlich in der Symbolwahl von `ResolveSymbolAtToken`
  begruendet (z. B. wenn `token.Parent` bei einer generischen Methode auf
  ein anderes Token als die Methoden-Deklaration selbst zeigt), nicht in
  `GetSymbolBodyTool` selbst. Dieser Step aendert an
  `ResolveSymbolAtToken`/`ResolveByPositionAsync`/`ResolveByNameAsync`
  **nichts** (nur der neue vorgeschaltete Stable-ID-Zweig wird ergaenzt) —
  falls der P2-Bug beim Testen dieses Steps zufaellig auftaucht, ist er
  **nicht** in diesem Step zu fixen (out of scope, siehe „Bekannte
  Ausnahmen").
- Keine bestehende Test-Infrastruktur muss neu gebaut werden:
  `SymbolGraphCatalogFixture` (`AiNetLinter.Tests.Fixtures`) wird bereits
  von allen drei betroffenen Testklassen genutzt und liefert `Greeter.Greet`
  als stabiles Test-Symbol (`GreeterPath`, Zeile 5, Spalte 19) — exakt das
  Muster, das `GetSymbolBodyToolTests.ExecuteAsync_ValidStableId_
  ReturnsBodyForMethod` fuer die stabile ID bereits verwendet
  (`DocumentationCommentId.CreateDeclarationId(symbol)` aus einem zuvor
  aufgeloesten Symbol ableiten, dann als `identifier` erneut aufrufen).

## Intention

`FindReferencesTool.ResolveSymbolAsync` wird der tatsaechliche, vollstaendige
gemeinsame Einstiegspunkt fuer alle drei dokumentierten Identifikator-Formate
(DocumentationCommentId zuerst, dann Datei:Zeile:Spalte, dann qualifizierter
Name) statt nur fuer zwei davon. Da `find_references`, `get_impact` und
`get_symbol_body` (sowie `get_type_hierarchy` als Nebeneffekt) bereits
alle ueber diese eine Methode laufen, schliesst eine einzige, gezielte
Erweiterung die im Konzept beschriebene Luecke fuer alle betroffenen Tools
gleichzeitig — ohne neue Abstraktion, ohne Verhaltensaenderung fuer die
bereits funktionierenden zwei Formate.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs`

- **Was:** `ResolveSymbolAsync` um einen vorgeschalteten Zweig ergaenzen,
  der zuerst `SymbolIdentifierResolver.TryResolveByStableIdAsync(solution,
  identifier, ct)` versucht; liefert das ein Symbol oder einen Fehler
  zurueck, wird das direkt durchgereicht (analog dem bisherigen Verhalten
  in `GetSymbolBodyTool`); liefert es `(null, null)` (kein erkennbares
  DocumentationCommentId-Praefix oder kein Treffer), faellt die Methode
  wie bisher auf `TryParsePosition` → `ResolveByPositionAsync` bzw.
  `ResolveByNameAsync` zurueck. Keine Aenderung an
  `ResolveByPositionAsync`, `ResolveByNameAsync` oder `ExecuteAsync`
  selbst.
- **Warum:** Das ist die eine Stelle, an der alle vier Symbolgraph-Tools
  bereits zusammenlaufen — hier ergaenzt, kommt die stabile ID allen
  automatisch zugute, ohne Duplikation.
- Zusaetzlich: Klassen- und Methoden-XML-Doc-Kommentar aktualisieren
  (aktuell „Datei:Zeile:Spalte oder qualifizierter/teil-qualifizierter
  Name" — um „oder stabile DocumentationCommentId" ergaenzen, analog zum
  Wortlaut in `GetSymbolBodyDescription`).

### Datei 2: `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`

- **Was:** Private Methode `TryResolveByStableIdAsync` (Zeile 66-73)
  entfernen; in `ExecuteAsync` den bisherigen Zwei-Schritt-Aufruf (erst
  eigene `TryResolveByStableIdAsync`, dann bei `null` Fallback auf
  `FindReferencesTool.ResolveSymbolAsync`) durch einen einzigen Aufruf
  `await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct)`
  ersetzen.
- **Warum:** Nach Datei 1 ist die eigene Vorstufe reine Duplikation
  derselben Logik — Entfernen ist reine Simplification ohne
  Verhaltensaenderung (gleiche Reihenfolge: stabile ID zuerst, dann
  Position/Name), reduziert Codegroesse und haelt `AvoidExcessiveMiddleMen`
  ein.

### Datei 3: `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`

- **Was:** `FindReferencesDescription` und `GetImpactDescription` um die
  DocumentationCommentId-Erwaehnung ergaenzen (Formulierung analog
  `SymbolBodyToolRegistrations.GetSymbolBodyDescription`: „stabiler ID
  (DocumentationCommentId, ueberlebt Zeilenverschiebungen, disambiguiert
  Overloads) oder Datei:Zeile:Spalte bzw. qualifiziertem/teil-
  qualifiziertem Namen"). Optional (Bonus, kostenlos da bereits durch
  Datei 1 technisch korrekt): `GetTypeHierarchyDescription` ebenfalls um
  denselben Zusatz ergaenzen, da `get_type_hierarchy` ueber denselben
  Aufruf automatisch mitprofitiert.
- **Warum:** Tool-Beschreibungen sind der Vertrag, den der Agent liest,
  bevor er `find_references`/`get_impact`/`get_type_hierarchy` aufruft —
  nach Datei 1 waere die bisherige Beschreibung sonst faktisch veraltet
  (dokumentiert weniger, als das Tool tatsaechlich kann).

### Datei 4: `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs`

- **Was:** Klassen-XML-Doc-Kommentar aktualisieren — der aktuelle Satz
  „Kleine, reine Parsing-/Aufloesungs-Helfer fuer `FindReferencesTool`"
  ist nach diesem Step nur noch die halbe Wahrheit (drei weitere Tools
  haengen ueber `FindReferencesTool.ResolveSymbolAsync` daran). Kurz
  ergaenzen, dass `FindReferencesTool.ResolveSymbolAsync` der gemeinsame
  Einstiegspunkt fuer `find_references`, `get_impact`,
  `get_type_hierarchy` und `get_symbol_body` ist, dieser Helfer also
  transitiv fuer alle vier gilt.
- **Warum:** Der veraltete Kommentar war explizit einer der in
  `Konzept.md` benannten Fundstellen fuer die Verwirrung ueber den
  Ist-Zustand — beheben, damit er nicht weiterhin in die Irre fuehrt.

## Tests

- [ ] `FindReferencesToolTests.ResolveSymbolAsync_StableId_ReturnsSymbolAtId`
      — Symbol ueber `Greeter.Greet` aufloesen, `DocumentationCommentId.
      CreateDeclarationId` bilden, mit dieser ID erneut
      `FindReferencesTool.ResolveSymbolAsync` aufrufen, `symbol!.Name ==
      "Greet"` erwarten (Muster 1:1 aus
      `GetSymbolBodyToolTests.ExecuteAsync_ValidStableId_ReturnsBodyForMethod`
      uebernommen).
- [ ] `FindReferencesToolTests.ExecuteAsync_StableId_ReturnsCallSiteInCaller`
      — End-to-End ueber `FindReferencesTool.ExecuteAsync` mit stabiler ID
      als `symbolIdentifier`, erwartet `Caller.cs` im Ergebnistext (Muster
      aus `ExecuteAsync_ValidQualifiedName_ReturnsCallSiteInCaller`).
- [ ] `GetImpactToolTests.ExecuteAsync_StableSymbolIdentifierGiven_ReturnsCallSites`
      — `GetImpactTool.ExecuteAsync` mit stabiler ID als
      `input.SymbolIdentifier`, erwartet `Caller.cs` im Ergebnistext
      (Muster aus
      `ExecuteAsync_SymbolIdentifierGiven_DelegatesToResolveSymbolAndReturnsCallSites`).
- [ ] `GetTypeHierarchyToolTests` — optionaler Bonus-Test mit stabiler ID
      (nur falls ohne Mehraufwand mit bestehendem Fixture-Muster
      dieser Testklasse machbar; kein Muss, da `get_type_hierarchy`
      ausserhalb des EPIC-02-Scopes liegt und rein als Nebeneffekt
      mitprofitiert).
- [ ] Bestehende `GetSymbolBodyToolTests`-Suite (5 Tests, insbesondere
      `ExecuteAsync_ValidStableId_ReturnsBodyForMethod` und
      `ExecuteAsync_InvalidStableId_FallsBackToFileLineCol`) bleibt
      unveraendert gruen — Regressionsnachweis, dass das Entfernen der
      eigenen Vorstufe keine Verhaltensaenderung verursacht.
- [ ] Volllauf `dotnet test` gruen (Definition of Done).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` (Tech-Stack-Notiz) grün — 0 Fehler, 0 Warnungen
- [ ] `dotnet test` (Volllauf) grün — bei Testhost-Absturz ohne
      Einzeltestfehler: TD-003 zur Kenntnis nehmen (bekanntes
      Sandbox-Problem, nicht dieses Steps), Lauf wiederholen statt
      als Fehlschlag werten
- [ ] Commit auf aktuellem Branch (Conventional Commit, deutsch,
      inkl. Refs-Suffix `[verbesserungen-mcp]`)
- [ ] `step-003/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt
- [ ] Konzept.md Definition-of-Done-Schnellcheck-Punkt 3
      („`find_references(id aus skeleton)` → Treffer > 0") mit der neuen
      stabilen-ID-Faehigkeit von `find_references` manuell nachvollzogen
      (z. B. ueber die neuen Tests) und im `step-result.md` kurz notiert

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests) — xUnit
  v3 Pflicht fuer jede Logik-Aenderung, Test-Parallelitaet erhalten
  (keine neue Collection/Serialisierung noetig — nutzt bestehende
  `SymbolGraphCatalogFixture` genauso wie die bereits existierenden
  Testklassen), Commit-Vorschlag-Pflicht am Ende der Antwort.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention)
  — Zero-Warning-Direktive gilt fuer alle vier geaenderten Dateien;
  Symptom-Fixing-Verbot ist hier relevant als Abgrenzung: der P2-Bug in
  `GetSymbolBodyTool` (falls beim Testen sichtbar) darf nicht durch
  Abschwaechen einer Assertion kaschiert werden, sondern bleibt
  dokumentiert fuer EPIC-03 (siehe „Bekannte Ausnahmen").
- `.agents/rules/AiNetLinter.mdc` (`AvoidExcessiveMiddleMen`,
  `MaxMethodLineCount` 60, `MaxCyclomaticComplexity` 12) — das Entfernen
  der duplizierten Vorstufe in `GetSymbolBodyTool.cs` ist eine direkte
  Anwendung von `AvoidExcessiveMiddleMen`; der neue Zweig in
  `ResolveSymbolAsync` bleibt weit unter den Methodengrenzwerten (ein
  zusaetzliches `if`, kein Schachtelungs-/Komplexitaetsproblem).

## Bekannte Ausnahmen

- TD-003 (`dotnet test`-Volllauf stuerzt in dieser Sandbox intermittierend
  mit Testhost-Absturz ab, ohne Einzeltestfehler) — bereits dokumentiert,
  reproduziert unabhaengig von Code-Aenderungen dieses Tasks. Bei Absturz:
  Lauf wiederholen, nicht als Regression werten.
- Falls beim Testen dieses Steps der aus `Konzept.md`/EPIC-03 bekannte
  `get_symbol_body`-ID-Korruptionsverdacht (verschachtelte/doppelte
  DocumentationCommentId bei Datei:Zeile:Spalte-Input) sichtbar wird: das
  ist **nicht** Scope dieses Steps (EPIC-03) — beobachten, in
  `step-result.md` unter „Beobachtungen" vermerken, nicht fixen, keine
  Assertion deswegen abschwaechen.

## Code-Skizze (optional)

```csharp
// FindReferencesTool.cs
internal static async Task<(ISymbol? Symbol, CallToolResult? Error)> ResolveSymbolAsync(
    Solution solution, string identifier, CancellationToken ct)
{
    var (stableSymbol, stableError) =
        await SymbolIdentifierResolver.TryResolveByStableIdAsync(solution, identifier, ct);
    if (stableError is not null) return (null, stableError);
    if (stableSymbol is not null) return (stableSymbol, null);

    if (SymbolIdentifierResolver.TryParsePosition(identifier, out var path, out var line, out var column))
    {
        return await ResolveByPositionAsync(solution, identifier, path, line, column, ct);
    }

    return await ResolveByNameAsync(solution, identifier, ct);
}
```

```csharp
// GetSymbolBodyTool.cs — ExecuteAsync, ersetzt den bisherigen Zwei-Schritt-Aufruf
var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, identifier, ct);
if (error is not null) return error;
if (symbol is null) return McpToolResults.SymbolNotFound(identifier);
// private TryResolveByStableIdAsync(...) in dieser Datei komplett entfernen
```

## Notes

- Reihenfolge stabile-ID-zuerst ist bewusst identisch zur bisherigen,
  bereits produktiv gelaufenen Reihenfolge in `GetSymbolBodyTool` gewaehlt
  — kein neues Verhalten, nur an einer Stelle statt zwei implementiert.
  Dadurch bleibt das Risiko dieses Steps trotz „zentrale, von vier Tools
  genutzte Methode aendern" ueberschaubar: die Aenderung ist rein additiv
  (ein neuer, fruehzeitig verlassener Zweig), keine bestehende
  Verzweigung wird umgebaut.
- `get_type_hierarchy` ist kein Teil des EPIC-02-Scopes laut Roadmap,
  profitiert aber automatisch mit, weil es bereits denselben
  `FindReferencesTool.ResolveSymbolAsync`-Aufruf nutzt. Falls der Kritiker
  das als Scope-Creep werten sollte: es handelt sich um reine
  Doku-Ehrlichkeit (Beschreibung sagt jetzt, was das Tool bereits kann),
  keine zusaetzliche Code-Aenderung an `GetTypeHierarchyTool.cs` selbst.
- Kein neuer Test fuer `SymbolIdentifierResolver.TryResolveByStableIdAsync`
  selbst noetig — die Methode wird nicht veraendert, nur an einer neuen
  Stelle aufgerufen; bestehende Abdeckung ueber
  `GetSymbolBodyToolTests` bleibt bestehen.
