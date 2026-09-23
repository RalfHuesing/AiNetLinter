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
- Ein gezielter FastTest mit `Foo.razor`, `Foo.razor.cs` und einer normalen
  `RegularService.cs` scannt zwei Kandidatendokumente, aber keine generierten
  Dokumente. Aktuell erhalten beide privaten Code-Behind-Methoden und die
  normale C#-Kontrolle `high`. Die erwartete Einstufung ist `low` mit
  Razor-Unsicherheitsgrund für **beide** Code-Behind-Methoden und weiterhin
  `high` für die normale C#-Methode. Dieser Test ist vor Produktcodeänderung
  rot und belegt die zu korrigierende Confidence-Lücke.

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
  Member der zugehörigen `.razor.cs` nicht `confidence: high` erhalten.
  **Alle** referenzlosen Member dieser Komponente bleiben mit
  `confidence: low` und konkretem Grund zur unvollständigen Razor-Evidenz
  sichtbar; ohne generierte Semantik ist auch ein tatsächlich unbenutztes
  Member nicht sicher unterscheidbar. Bei auswertbarem generiertem Dokument
  mit null semantischen Referenzen gilt die normale Dead-Code-Bewertung.
- Dieselbe Confidence-Grenze gilt für Kandidaten aus Compiler-/IDE-Diagnosen
  in `DeadCodeMode.Locals` und `Both`, etwa ein privates `@ref`-Feld in einer
  `.razor.cs`. Der Herkunftsweg des Kandidaten darf keine widersprüchliche
  `high`-Aussage erzeugen.
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
- Ein bloßer Name in einem generierten Markup-String zählt nicht als
  semantische Referenz. Eine syntaktisch geschriebene, aber vom Razor-Compiler
  nicht gebundene Eventangabe schützt den Member nicht.
- Keine Änderung an Testreferenz-Definition, externer API-Oberfläche,
  Konfigurationsschema, CLI, `verify`-Scopes oder Commit-Range-Analyse.
- Keine Änderung am externen `KnowHowToAI`-Repository.

## Technischer Vertrag und Code-Anker

1. Neue interne Datei
   `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/RazorGeneratedEvidenceIndex.cs`
   im Namespace `AiNetLinter.Mcp.Tools.Verify.DeadCode`: Statuswerte
   `NotComponent`, `Available`, `Unavailable`. Als Code-Behind gilt nur eine
   Partial-Typdeklaration `Foo` in `Foo.razor.cs` mit `Foo.razor` im selben
   Verzeichnis. Beim Projektstart einmal
   `Project.GetSourceGeneratedDocumentsAsync(ct)` auswerten. Ein generiertes
   Dokument gehört nur dann zur Komponente, wenn sein normalisierter
   relativer Komponentenpfad und sein per SemanticModel aufgelöster
   Partial-Typ passen; derselbe Dateiname in einem anderen Ordner reicht
   nicht. Ohne passendes Dokument oder Semantikmodell: `Unavailable`.
   Mit passendem, auswertbarem Dokument: `Available`, selbst wenn dort nur
   ein Markup-String und keine Handler-Referenz steht. Kein `obj`-Dateilesen.
2. `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeAdvisoryScanner.cs`:
   Index einmal in `ScanProjectAsync` erstellen und für die Member dieses
   Projekts verwenden. `IsSymbolUnreferencedAsync` bleibt der semantische
   Referenzentscheid. Nur bei `Unavailable` wird ein ansonsten `high`
   eingestufter `.razor.cs`-Kandidat zu `low`; der Grund nennt fehlende
   Razor-Evidenz. `NotComponent` und `Available` behalten ihre bisherige
   Einstufung. `high`-Filter, `Summary.High`/`Low` und `Reason` aus der
   endgültigen Einstufung berechnen, ohne Generator-Arbeit pro Member.
   `DeadCodeAdvisoryDiagnosticsScanner.cs` verwendet denselben Index für
   diagnostikbasierte Kandidaten; kein zweites Razor-Erkennungsverfahren.
   Der `Unavailable`-Grund nennt „Razor-Referenzen nicht entscheidbar:
   generiertes C# fehlt oder ist nicht auswertbar“ und als Gegencheck die
   Razor-Generierung/Projektladung.
3. `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeModels.cs`:
   Die vorhandenen Felder `confidence`, `reason` und `countercheck` nutzen;
   keinen neuen öffentlichen Parameter. `recommendedNextAction` und
   `summary.next` fordern übereinstimmend zuerst `countercheck`, nicht
   pauschal `ask_user`.
4. `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs` und
   Formatter: Leere Standardbefunde für `.razor.cs` erklären die bereits
   vorhandene Option `includeGenerated=true`, etwa als einzelne
   `next: includeGenerated=true`-Zeile. Andere Befunde bleiben knapp.
5. `src/AiNetLinter/Mcp/Tools/Verify/VerifyTool.cs`:
   `VerifyAdvisoryProjector` übernimmt die belegte Einstufung;
   `VerifyResponseFormatter.AppendAdvisories` bleibt im 4-KiB-Budget.
6. `Docs/mcp/tools.md` beschreibt Razor-Gegenprüfung und die Grenze bei
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
  und den Erhalt der Erkennung. Ein getrennter Fall ohne den Web-Import
  belegt, dass der Name im generierten Markup-String den Member nicht
  fälschlich schützt.
- Ein zuerst rot nachgewiesener Fall mit `.razor.cs` und vorhandenem
  `.razor`, aber ohne auswertbares generiertes Razor-Dokument erwartet
  für alle Code-Behind-Member `low` und einen Grund für unvollständige
  Evidenz statt `high`; eine private Methode in `RegularService.cs` bleibt
  `high`. Der Test verwendet `TestTempDirectory` und eine Adhoc-Solution
  ohne Source Generator, prüft `DocumentsInScope=2` und ist aktuell rot.
  Ein diagnostikbasierter privater Code-Behind-Feldkandidat folgt derselben
  Low-Regel; eine normale C#-Diagnose behält ihre bisherige Einstufung.
  Ein `find_references`-Vertragstest prüft den Hinweis bei leerem
  Standardergebnis und sein Ausbleiben bei sichtbaren Treffern.
- Reine Entscheidungs- und Formatierungsvarianten gehören in FastTests;
  die echte Razor-/MSBuild-Grenze in IntegrationTests. Temporärdateien nur
  über `AiNetLinter.TestKit.TestTempDirectory` oder die vorhandene Fixture.
  Bei jedem Fixture-Test `DocumentsInScope > 0` belegen: der zentrale
  Dateifilter schließt Pfade mit Segment `worktrees` aus. Für diesen
  Integrationstest einen Checkout-Pfad ohne dieses Segment verwenden;
  ein leerer Scan ist kein grüner Nachweis. Rot-Test-First und Gates folgen
  den Repo-Regeln.

## Abgrenzung zum bestehenden Dead-Code-Konzept

`tasks/deadcode-produktive-nutzung/Konzept.md` behandelt Testreferenzen als
nicht produktive Nutzung und die externe API-Oberfläche. Beides bleibt dort.
Den Akzeptanzpunkt zu generierten Dokumenten übernimmt dieser Task konkret
für Blazor; die allgemeine Behandlung unentscheidbarer Referenzstellen
bleibt im alten Konzept. Beide Vorhaben berühren dieselbe Referenzprüfung,
aber mit getrennten fachlichen Entscheidungen.
