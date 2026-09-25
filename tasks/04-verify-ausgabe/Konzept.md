---
status: ready
---

# Konzept: Kompakter Abruf von Verify-Advisories

## Intention

`verify` bleibt ein kurzer Qualitäts-Gate-Aufruf. Ein Agent, der Dead Code
systematisch abbauen will, kann bei Bedarf eine gesonderte, schmale Liste
der Kandidaten einer Solution abrufen und einzelne Symbole mit den
vorhandenen Tools gegenprüfen. Die Liste enthält alle Kandidaten, soweit
sie in das feste Ausgabelimit passen, und meldet jede Kürzung ausdrücklich.
Sie ist statische Review-Evidenz, keine Löschanweisung. Der neue Aufruf
speichert keine Ergebnisse zwischen und führt keinen vollständigen
Verify-Gate-Lauf aus.

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
3. Die Antwort enthält so viele **vollständige** Kandidateneinträge wie
   innerhalb von 64 KiB Platz haben, ohne Duplikate. Wenn alle hineinpassen,
   enthält sie jeden ermittelten Kandidaten genau einmal. Weder
   `EvidenceLimit` noch das 4-KiB-Budget von `verify` gelten dafür. Die
   Reihenfolge ist deterministisch: Confidence (`high` vor `low`), dann
   Projekt, relativer Pfad, Zeile und Symbolkennung. Bei Linked Files
   bleiben projektverschiedene Symbole unterscheidbar.
4. Eine kompakte Kopfzeile nennt `status`, `candidates`, `testOnly`,
   `unreferenced`, `apiProtected`, `undecidable`, `shown` und
   `truncatedBy`. `candidates` zählt das Scanergebnis;
   `shown` zählt die ausgegebenen Einträge und
   `truncatedBy = candidates - shown`. `status` beschreibt den Scan mit
   der bestehenden Bedeutung `complete` oder `partial`, **nicht** die
   Vollständigkeit der Liste. `truncatedBy > 0` benennt die
   Ausgabegrenze ausdrücklich. Ein partieller Scan darf keinen
   vollständigen Clean-Claim erzeugen.
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
   Content-Text)** begrenzt. Sie fügt nur ganze Einträge hinzu. Bei
   Überlauf bleiben Kopfzeile und passende Einträge erhalten;
   `truncatedBy > 0` und ein knapper Hinweis sagen ausdrücklich, dass
   weitere Kandidaten nicht angezeigt werden. Ein Scanner- oder
   Policy-Fehler wird deutlich gemeldet; null Kandidaten werden nur
   nach erfolgreichem Scan behauptet. Sollte nicht einmal die Kopfzeile
   und ein Eintrag hineinpassen, folgt statt einer irreführenden leeren
   Liste ein expliziter Größenfehler.
7. `verify` behält Verdict, Zähler, Ranking und Antwortbudget. Nur wenn
   `deadCode.truncatedBy > 0`, ergänzt es einen knappen, kopierbaren
   Hinweis auf `get_verify_advisories(category=dead_code)`.
8. Toolbeschreibung, `Docs/mcp/tools.md` und
   `.agents/rules/AiNetLinter-McpWorkflow.mdc` erklären knapp den
   Abrufzweck und die Folgeprüfung. Die Agentenregel nennt das Tool für
   breite Dead-Code-Reviews und behält die Pflicht zur
   Gegenprüfung jedes Kandidaten bei.

### Nicht

- Kein Cache, Cursor, Paging, Filter nach Projekt/Confidence/Usage oder
  frei wählbares `maxResults`. Bei `truncatedBy > 0` bietet dieses Tool
  keinen Zugriff auf die ausgelassenen Kandidaten; diese Grenze ist
  Bestandteil des beschlossenen Vertrags.
- Kein erneuter Lint-/Score-/Magic-Value-Lauf im neuen Tool; keine Änderung
  am Gate-Verdict durch Advisories.
- Keine automatische Entfernung, Suppression oder Umklassifizierung von
  Dead Code. Keine neue Dead-Code-Erkennungsheuristik.
- Keine weiteren `category`-Werte in dieser Umsetzung.

## Agentenvertrag

Die MCP-Toolbeschreibung lautet knapp:

> Listet Dead-Code-Advisories einer Solution bis 64 KiB mit direkt
> nutzbaren Symbol-IDs und nennt ausgelassene Treffer. `category`:
> `dead_code`. Kandidaten vor Änderungen gegenprüfen.

Der Agent nutzt `verify` zunächst als Gate. Bei
`deadCode.truncatedBy > 0` kann er einmal
`get_verify_advisories(targetPath, category="dead_code")` aufrufen.
Er bearbeitet oder bewertet sichtbare Kandidaten anhand der kurzen Liste
und ruft Details gezielt ab. Bei `truncatedBy > 0` darf er die Liste
nicht als vollständig behandeln. Der zweite Scan kann teuer sein; er ist
eine bewusste Anforderung einer breiteren Liste und startet keinen zusätzlichen
Verify-Gate-Lauf. Ergebnisse aus zwei Aufrufen können sich bei inzwischen
geändertem Quellstand unterscheiden; jeder Aufruf beschreibt seinen eigenen
Scanstand. Handoff-IDs dürfen nur im gültigen Analyse-Snapshot verwendet
werden. Ein Agent darf `test_only` nicht als „ohne Referenzen“ lesen.

## Verifikation

- **Rot zuerst:** Den bereits committeten synthetischen Integrationstest
  auf den konkreten Toolvertrag weiterentwickeln und vor Produktionscode
  rot ausführen. Er benötigt keinen vorherigen Verify-Aufruf, erzeugt mehr
  Kandidaten als `verify` zeigt und verlangt bei einer unter 64 KiB
  liegenden Antwort alle Kandidaten mit eindeutig navigierbaren
  Symbol-IDs.
- Ein weiterer fokussierter Rot-Test belegt, dass die vollständige Liste
  auch dann erscheint, wenn `verify` wegen Evidence-Limit und 4-KiB-Budget
  nur einen Teil zeigt. Kandidatenanzahl, `testOnly`/`unreferenced` und
  Zeilenzahl stimmen überein; keine Dubletten oder Auslassungen.
- Vertragstests prüfen ungültige Kategorie, leere erfolgreiche Liste,
  Content-only, stabilen Sortier-/Gruppierungsvertrag, Linked-File-IDs,
  `partial`/Fehlerstatus und Kürzung nur an Eintragsgrenzen. Der
  Verify-Hinweis erscheint nur bei tatsächlicher Dead-Code-Trunkierung;
  Gate-Semantik bleibt gleich.
- Ein synthetischer Größenfall mit mindestens 714 Kandidaten und
  realistisch langen Projekt-, Pfad- und Symbolnamen prüft die
  64-KiB-Grenze, `shown + truncatedBy = candidates`, unverstümmelte
  Einträge und einen ausdrücklichen Kürzungshinweis. Er hängt von
  keinem der vier untersuchten Produkt-Repositories ab.
- Prüfzeitpunkt, Gate-Reihenfolge und Testauswahl richten sich
  ausschließlich nach `.agents/rules/AiNetLinter-Richtlinien.mdc` und
  `.agents/rules/AiNetLinter-TestRichtlinien.mdc`. Die für den
  MCP-Transport nötigen Integrationstests gehören zur Umsetzung dieses
  ausdrücklich beauftragten Vertrags.

## Abgrenzung zum früheren Konzept

`tasks/deadcode-produktive-nutzung/Konzept.md` schloss damals ein zweites
Dead-Code-Tool aus. Die dortige Erkennungssemantik bleibt maßgeblich;
dieses neue Vorhaben ersetzt nur jene frühere Ausgabeentscheidung.
