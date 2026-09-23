---
status: draft
---

# Blazor-Evidenz für Dead-Code-Kandidaten absichern

## Intention

Ein Member darf nicht als unbenutzt erscheinen, wenn eine Blazor-Komponente es
über generierten Razor-C#-Code verwendet. Agenten sollen diese Evidenz sehen;
bei unvollständiger Razor-Analyse darf kein sicherer Löschschluss entstehen.
Das Ergebnis bleibt ein Advisory, keine Löschanweisung.

## Belegter Ausgangspunkt

- Im externen `KnowHowToAI` findet `find_references` für
  `ContentEditor.SaveAsync` ohne `includeGenerated` keine Aufrufstelle; mit
  `includeGenerated=true` liegt eine Aufrufstelle in `ContentEditor_razor.g.cs`
  vor. Für `KnowledgeTree.CreateChildNodeAsync` gilt dasselbe. Beide Methoden
  stehen in `.razor.cs`, ihre Bindungen in `.razor`.
- Der gezielte Test mit `tests/Fixtures/BlazorPartialMini` zeigt nach
  korrektem Blazor-Import: `SymbolFinder` findet die generierte Referenz,
  und `DeadCodeAdvisoryScanner.ScanAsync` meldet den gebundenen privaten
  Handler nicht, wohl aber einen unbenutzten Kontrollmember. Ein erster
  roter Lauf ohne `Microsoft.AspNetCore.Components.Web`-Import war eine
  ungültige Fixture: Razor schrieb `@onclick` als Markup-String, ohne
  semantische Handler-Referenz. Ein Scanner-False-Positive ist damit
  derzeit **nicht** belegt; `KnowHowToAI` bleibt Recherchebeispiel.
- Die belegte Lücke liegt in der Standardantwort von `find_references`:
  Ohne `includeGenerated` sind vorhandene Blazor-Aufrufe für Agenten
  unsichtbar. Zusätzlich fehlt bei nicht ladbarer Razor-Generierung eine
  verlässliche Confidence-Grenze für `.razor.cs`-Kandidaten.

## Scope

### Muss

- Das belegte Scanner-Verhalten wird durch Tests festgehalten: Semantisch
  gebundene Methoden und Properties aus `.razor.cs` erscheinen nicht als
  Dead-Code-Kandidaten; wirklich unbenutzte Member derselben Komponente
  bleiben sichtbar. Dies umfasst `@onclick`, eine gelesene Property und
  einen Child-`EventCallback`. Die bestehende private Referenzsuche wird
  dafür nicht vorsorglich umgebaut.
- Fehlt zu einer vorhandenen `.razor`-Datei das generierte Dokument im
  geladenen Projekt oder ist es nicht auswertbar, darf ein referenzloses
  Member der zugehörigen `.razor.cs` nicht `confidence: high` erhalten. Der
  Kandidat bleibt mit `confidence: low` und konkretem Grund zur
  unvollständigen Razor-Evidenz sichtbar. Bei auswertbarem generiertem
  Dokument mit null Referenzen gilt die normale Dead-Code-Bewertung.
- `find_references` behält `includeGenerated=false` als Standard. Ein leeres
  Ergebnis für ein `.razor.cs`-Member weist knapp auf
  `includeGenerated=true` als Gegenprüfung hin. `verify` zeigt bei einem
  verbleibenden `.razor.cs`-Kandidaten die Razor-Gegenprüfung im sichtbaren
  Grund. Die Scanner-Empfehlung fordert nicht pauschal `ask_user` vor der
  eigenständigen Prüfung.
- Dead-Code-Einträge bleiben Advisory-Kandidaten und beeinflussen weder
  Score noch Verdict.

### Nicht

- Keine Erkennung von Komponententypen über `<ComponentName>`, `@page` oder
  Reflection; der Task behandelt Code-Behind-Member.
- Keine Namenstextsuche in Markup oder `obj` als Nutzungsbeweis und keine
  Ausblendung eines Kandidaten allein wegen eines gleichnamigen Razor-Texts.
- Keine Änderung an Testreferenz-Definition, externer API-Oberfläche,
  Konfigurationsschema, CLI, `verify`-Scopes oder Commit-Range-Analyse.
- Keine Änderung am externen `KnowHowToAI`-Repository.

## Technischer Vertrag und Code-Anker

1. `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeAdvisoryScanner.cs`:
   `IsSymbolUnreferencedAsync` bleibt für belegte Razor-Referenzen der
   Referenzentscheid. `ClassifyConfidence`/`AddDeadSymbol` behandeln nur den
   neuen Fall fehlender Razor-Evidenz.
   Als Code-Behind gilt eine Partial-Typdeklaration `Foo` in `Foo.razor.cs`
   mit gleichnamiger `Foo.razor` im selben Verzeichnis. Generierte Dokumente
   über Roslyn (`Project.GetSourceGeneratedDocumentsAsync`) pro Projekt
   einmal ermitteln und über den relativen Komponentenpfad sowie den
   Partial-Typ zuordnen; ein bloß gleicher Dateiname in einem anderen
   Verzeichnis zählt nicht. Eine vorhandene generierte Datei gilt nur als
   auswertbar, wenn Roslyn ihr C#-Dokument und Semantikmodell liefert. Keine
   Generator-Ausführung pro Member.
2. `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeModels.cs`:
   Bestehende Felder `confidence`, `reason` und `countercheck` nutzen. Ein
   zusätzliches internes Statusmodell nur bei nachgewiesenem Bedarf;
   keinen neuen öffentlichen Parameter.
3. `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs` und
   Formatter: Leere Standardbefunde für `.razor.cs` erklären die bereits
   vorhandene Option `includeGenerated=true`. Andere Befunde bleiben knapp.
4. `src/AiNetLinter/Mcp/Tools/Verify/VerifyTool.cs`:
   `VerifyAdvisoryProjector` übernimmt die belegte Einstufung;
   `VerifyResponseFormatter.AppendAdvisories` bleibt im 4-KiB-Budget.
5. `Docs/mcp/tools.md` beschreibt Razor-Gegenprüfung und die Grenze bei
   fehlenden generierten Dokumenten. Agentenregeln auf Widersprüche prüfen;
   keine zweite allgemeine Advisory-Policy formulieren.

## Verifikation

- Die echte Fixture `tests/Fixtures/BlazorPartialMini` erhält
  `@using Microsoft.AspNetCore.Components.Web`, `@onclick="Handler"`, eine
  gelesene Property (`@Title`), einen Child-Callback mit
  `EventCallback`-Parameter (`OnConfirm="Handler"`) und einen unbenutzten
  privaten Kontrollmember. Ohne den Import ist `@onclick` in dieser Fixture
  keine semantische Referenz; ein solcher Test darf nicht als Scanner-FP
  ausgegeben werden.
- Der Test weist für gebundene Member eine Referenz in `*_razor.g.cs` per
  `SymbolFinder` oder `find_references(includeGenerated=true)` nach.
  `DeadCodeAdvisoryScanner.ScanAsync` und
  `VerifyAdvisoryProjector.CollectAsync` melden sie nicht als `dead_code`.
  Der unbenutzte Kontrollmember bleibt Kandidat. Das belegt die Korrektur
  und den Erhalt der Erkennung.
- Ein zuerst rot nachgewiesener Fall mit `.razor.cs` und vorhandenem
  `.razor`, aber ohne auswertbares generiertes Razor-Dokument erwartet
  `low` und einen Grund für unvollständige Evidenz statt `high`. Ein
  `find_references`-Vertragstest prüft den Hinweis bei leerem
  Standardergebnis und sein Ausbleiben bei sichtbaren Treffern.
- Reine Entscheidungs- und Formatierungsvarianten gehören in FastTests;
  die echte Razor-/MSBuild-Grenze in IntegrationTests. Temporärdateien nur
  über `AiNetLinter.TestKit.TestTempDirectory` oder die vorhandene Fixture.
  Rot-Test-First und Gates folgen den Repo-Regeln.

## Abgrenzung zum bestehenden Dead-Code-Konzept

`tasks/deadcode-produktive-nutzung/Konzept.md` behandelt Testreferenzen als
nicht produktive Nutzung und die externe API-Oberfläche. Beides bleibt dort.
Den Akzeptanzpunkt zu generierten Dokumenten übernimmt dieser Task konkret
für Blazor; die allgemeine Behandlung unentscheidbarer Referenzstellen
bleibt im alten Konzept. Beide Vorhaben berühren dieselbe Referenzprüfung,
aber mit getrennten fachlichen Entscheidungen.

## Arbeitsgedächtnis (nur Draft)

- Die drei Scope-Entscheidungen (nur Code-Behind-Member, `low` bei fehlender
  Razor-Evidenz und `find_references`-Hinweis) hat der Nutzer bestätigt.
- Der Luna-Repro im isolierten Worktree war nach Korrektur der Fixture grün:
  gebundener Handler nicht dead, Kontrollmember dead, generierte Referenz
  semantisch aufgelöst. Vor der Freigabe noch den Fall fehlender generierter
  Razor-Dokumente gezielt rot prüfen und die Confidence-Regel daran messen.
