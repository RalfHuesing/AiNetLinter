---
status: draft
---

# Konzept: Vollständige, kompakte Verify-Advisory-Liste

## Intention

`verify` bleibt ein kurzer Qualitäts-Gate-Aufruf. Ein Agent, der Dead Code
systematisch abbauen will, kann bei Bedarf **alle** Kandidaten einer Solution
in einem gesonderten, schmalen MCP-Ergebnis sehen und einzelne Symbole mit den
vorhandenen Tools gegenprüfen. Die Liste ist statische Review-Evidenz, keine
Löschanweisung. Der neue Aufruf speichert keine Ergebnisse zwischen und führt
keinen vollständigen Verify-Gate-Lauf aus.

## Belegter Ist-Stand

- Die vier Berichte in diesem Taskverzeichnis zählen 1.631
  Dead-Code-Kandidaten; `verify` zeigte davon 49. Der vorhandene synthetische
  Rot-Test erzeugt 31 Kandidaten, belegt die Trunkierung und die Navigation
  der sichtbaren Symbol-IDs. Er ist beim fehlenden Review-Zugriff rot.
- `VerifyAdvisoryProjector.ScanDeadCodeAsync` ruft
  `DeadCodeAdvisoryScanner.ScanAsync` bereits mit unbegrenztem
  `MaxResults` auf. Erst das Advisory-Ranking nimmt höchstens
  `VerifyTool.EvidenceLimit` Einträge; danach begrenzt der
  Verify-Formatter die Antwort auf `VerifyTool.ResponseBudgetBytes`.
- `verify` trennt Gate und Advisories. `deadCode.candidates` zählt die
  Kandidaten des Scans; `shown` und `truncatedBy` beschreiben nur die
  Darstellung. Ein `pass` mit Dead-Code-Kandidaten ist korrekt.

## Scope

### Muss

1. Neues MCP-Tool `get_verify_advisories` mit dem Pflichtparameter
   `category`. In dieser Umsetzung ist ausschließlich `dead_code` gültig;
   andere oder fehlende Werte ergeben einen klaren Parameterfehler. Der
   Kategoriename folgt dem bestehenden `category=dead_code` im Verify-Output.
   Der generische Toolname lässt weitere Advisory-Kategorien später zu,
   ohne sie jetzt vorzutäuschen.
2. Der Aufruf erhält wie andere Source-Tools einen absoluten
   `targetPath` zu `.sln` oder `.slnx` und untersucht alle produktiven
   Deklarationen der geladenen Solution. Referenzen werden solutionweit
   beurteilt. Er verwendet dieselbe Dead-Code-Policy, denselben Scanner,
   dieselben Ausschlüsse und dieselbe Symbolidentität wie
   `verify(scope: "solution")`. Er berechnet die Liste bei **jedem** Aufruf
   neu, unabhängig davon, ob zuvor `verify` aufgerufen wurde.
3. Die erfolgreiche Antwort enthält **jeden** ermittelten Kandidaten genau
   einmal. Weder `EvidenceLimit` noch das 4-KiB-Budget von `verify` gelten
   dafür. Die Reihenfolge ist deterministisch: Confidence (`high` vor
   `low`), dann Projekt, relativer Pfad, Zeile und Symbolkennung. Bei
   Linked Files bleiben projektverschiedene Symbole unterscheidbar.
4. Eine kompakte Kopfzeile nennt `status`, `candidates`, `testOnly`,
   `unreferenced`, `apiProtected` und `undecidable`. Die Zahl der
   Kandidaten entspricht der Anzahl ausgegebener Einträge. `status` behält
   die bestehende Bedeutung `complete` oder `partial`; ein partieller Scan
   darf keinen vollständigen Clean-Claim erzeugen.
5. Die Einträge werden nach Projekt und relativem Dateipfad gruppiert,
   damit lange Pfade nicht in jeder Zeile wiederkehren. Jeder Eintrag
   enthält nur `line`, einen kurzen Symbolnamen mit enthaltendem Typ,
   `symbolIdentifier`, `usage=test_only|unreferenced` und
   `confidence=high|low`. Eine einmalige Spaltenlegende ersetzt
   wiederholte Feldnamen pro Eintrag. Die `symbolIdentifier`-Werte gehen
   unverändert an `find_references`, `get_symbol_body` und
   `get_feature_context`.
   Ausführliche Gründe, Testreferenzen und Gegenprüfungsdetails werden
   erst über diese Folgetools abgerufen.
6. Die Antwort ist Content-only und auf **64 KiB (65.536 UTF-8-Bytes
   Content-Text)** begrenzt. Sie wird niemals still gekürzt und meldet
   bei Überschreitung einen expliziten Fehler **ohne Teilliste**, mit
   benötigter und erlaubter Bytezahl. Ein Scanner- oder Policy-Fehler
   wird ebenso deutlich gemeldet; null
   Kandidaten werden nur nach erfolgreichem Scan behauptet.
7. `verify` behält Verdict, Zähler, Ranking und Antwortbudget. Nur wenn
   `deadCode.truncatedBy > 0`, ergänzt es einen knappen, kopierbaren
   Hinweis auf `get_verify_advisories(category=dead_code)`.
8. Toolbeschreibung, `Docs/mcp/tools.md` und
   `.agents/rules/AiNetLinter-McpWorkflow.mdc` erklären knapp den
   Abrufzweck und die Folgeprüfung. Die Agentenregel nennt das Tool für
   vollständige Dead-Code-Reviews und behält die Pflicht zur
   Gegenprüfung jedes Kandidaten bei.

### Nicht

- Kein Cache, Cursor, Paging, Filter nach Projekt/Confidence/Usage oder
  frei wählbares `maxResults`.
- Kein erneuter Lint-/Score-/Magic-Value-Lauf im neuen Tool; keine Änderung
  am Gate-Verdict durch Advisories.
- Keine automatische Entfernung, Suppression oder Umklassifizierung von
  Dead Code. Keine neue Dead-Code-Erkennungsheuristik.
- Keine weiteren `category`-Werte in dieser Umsetzung.

## Agentenvertrag

Die MCP-Toolbeschreibung soll in etwa lauten:

> Listet alle Dead-Code-Advisories einer Solution kompakt mit direkt
> nutzbaren Symbol-IDs. `category`: derzeit nur `dead_code`. Prüfe
> Kandidaten vor Änderungen mit Symbol- und Referenz-Tools.

Der Agent nutzt `verify` zunächst als Gate. Bei
`deadCode.truncatedBy > 0` kann er einmal
`get_verify_advisories(targetPath, category="dead_code")` aufrufen.
Er bearbeitet oder bewertet Kandidaten anhand der kurzen Liste und ruft
Details gezielt ab. Der zweite Scan kann teuer sein; er ist eine bewusste
Anforderung der vollständigen Liste und startet keinen zusätzlichen
Verify-Gate-Lauf. Ergebnisse aus zwei Aufrufen können sich bei inzwischen
geändertem Quellstand unterscheiden; jeder Aufruf beschreibt seinen eigenen
Scanstand. Handoff-IDs dürfen nur im gültigen Analyse-Snapshot verwendet
werden. Ein Agent darf `test_only` nicht als „ohne Referenzen“ lesen.

## Verifikation

- **Rot zuerst:** Den bereits committeten synthetischen Integrationstest
  auf den konkreten Toolvertrag weiterentwickeln und vor Produktionscode
  rot ausführen. Er benötigt keinen vorherigen Verify-Aufruf, erzeugt mehr
  Kandidaten als `verify` zeigt und verlangt alle Kandidaten mit
  eindeutig navigierbaren Symbol-IDs.
- Ein weiterer fokussierter Rot-Test belegt, dass die vollständige Liste
  auch dann erscheint, wenn `verify` wegen Evidence-Limit und 4-KiB-Budget
  nur einen Teil zeigt. Kandidatenanzahl, `testOnly`/`unreferenced` und
  Zeilenzahl stimmen überein; keine Dubletten oder Auslassungen.
- Vertragstests prüfen ungültige Kategorie, leere erfolgreiche Liste,
  Content-only, stabilen Sortier-/Gruppierungsvertrag, Linked-File-IDs,
  `partial`/Fehlerstatus und explizites Scheitern bei zu großem Output
  ohne scheinbar vollständige Teilliste. Der Verify-Hinweis erscheint
  nur bei tatsächlicher Dead-Code-Trunkierung; Gate-Semantik bleibt gleich.
- Ein synthetischer Größenfall mit mindestens 714 Kandidaten und
  realistisch langen Projekt-, Pfad- und Symbolnamen prüft, ob die
  vollständige Liste tatsächlich innerhalb von 64 KiB bleibt. Er
  hängt von keinem der vier untersuchten Produkt-Repositories ab.
- Prüfzeitpunkt, Gate-Reihenfolge und Testauswahl richten sich
  ausschließlich nach `.agents/rules/AiNetLinter-Richtlinien.mdc` und
  `.agents/rules/AiNetLinter-TestRichtlinien.mdc`. Die für den
  MCP-Transport nötigen Integrationstests gehören zur Umsetzung dieses
  ausdrücklich beauftragten Vertrags.

## Arbeitsgedächtnis (nur Draft)

- **Ausgabelimit:** Der Nutzer hat 256 KiB als zu groß verworfen. Die
  Empfehlung von 64 KiB begrenzt den Kontextverbrauch deutlich, kann aber
  für sehr große Solutions eine vollständige Liste verhindern. Ohne
  Paging/Filter ist dieser Fall nur als expliziter Fehler lösbar. Die
  konkrete 64-KiB-Grenze wartet auf Nutzerentscheidung.
- **Historischer Konzeptkonflikt:**
  `tasks/deadcode-produktive-nutzung/Konzept.md` schloss damals ein zweites
  Dead-Code-Tool aus. Die dortige Erkennungssemantik bleibt maßgeblich;
  dieses neue Vorhaben ersetzt nur jene frühere Ausgabeentscheidung.
