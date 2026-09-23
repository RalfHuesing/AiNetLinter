---
status: draft
---

# Dead-Code-Kandidaten mit Blazor-Referenzen bewerten

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
- `DeadCodeAdvisoryScanner.IsSymbolUnreferencedAsync` begrenzt die Suche für
  `private`-Symbole auf Dokumente der Typdeklaration; für andere
  Sichtbarkeiten sucht er solutionweit. Ob dadurch im echten Blazor-Projekt
  ein generierter Aufruf verloren geht, muss ein Rot-Test belegen. Das leere
  Standardergebnis von `find_references` beweist dies allein nicht.
- `tests/Fixtures/BlazorPartialMini` lädt bereits ein echtes Razor-Projekt.
  Es ist die Testbasis; `KnowHowToAI` bleibt reines Recherchebeispiel.

## Scope

### Muss

- Für Member in einer `.razor.cs`-Partial-Klasse zählen semantisch aufgelöste
  Referenzen aus Razor-generierten C#-Dokumenten als Nutzung. Mindestens
  private Methoden und Properties sowie eigene Event- und Child-Callbacks
  sind abgedeckt. Die Referenzsuche bleibt solutionweit; nur die geprüften
  Deklarationen folgen dem bestehenden Verify-Änderungsscope.
- Die Optimierung der privaten Referenzsuche darf generierte
  Partial-Dokumente nicht ausschließen. Für `.razor.cs`-Member ist
  `SymbolFinder.FindReferencesAsync(symbol, solution, ct)` ohne
  Dokumenteinschränkung zulässig; andere private Member behalten zunächst
  ihren bisherigen Dokumentscope. Keine Namenstextsuche in Markup oder `obj`
  als Nutzungsbeweis.
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
- Keine Änderung an Testreferenz-Definition, externer API-Oberfläche,
  Konfigurationsschema, CLI, `verify`-Scopes oder Commit-Range-Analyse.
- Keine Änderung am externen `KnowHowToAI`-Repository.

## Technischer Vertrag und Code-Anker

1. `src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeAdvisoryScanner.cs`:
   `IsSymbolUnreferencedAsync` entscheidet über Referenzlosigkeit.
   `ClassifyConfidence`/`AddDeadSymbol` behandeln fehlende Razor-Evidenz.
   Als Code-Behind gilt eine Partial-Typdeklaration `Foo` in `Foo.razor.cs`
   mit gleichnamiger `Foo.razor` im selben Verzeichnis. Generierte Dokumente
   über Roslyn (`Project.GetSourceGeneratedDocumentsAsync`) pro Projekt
   einmal ermitteln und über den relativen Komponentenpfad sowie den
   Partial-Typ zuordnen; ein bloß gleicher Dateiname in einem anderen
   Verzeichnis zählt nicht. Keine Generator-Ausführung pro Member.
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

- Zuerst einen isolierten roten Regressionstest mit echtem Razor-Projekt
  schreiben und den Fehler vor der Korrektur festhalten. Die Fixture
  `tests/Fixtures/BlazorPartialMini` erhält `@onclick="Handler"`, eine
  gelesene Property (`@Title`), einen Child-Callback mit
  `EventCallback`-Parameter (`OnConfirm="Handler"`) und einen unbenutzten
  privaten Kontrollmember.
- Der Test weist für gebundene Member eine Referenz in `*_razor.g.cs` per
  `SymbolFinder` oder `find_references(includeGenerated=true)` nach.
  `DeadCodeAdvisoryScanner.ScanAsync` und
  `VerifyAdvisoryProjector.CollectAsync` melden sie nicht als `dead_code`.
  Der unbenutzte Kontrollmember bleibt Kandidat. Das belegt die Korrektur
  und den Erhalt der Erkennung.
- Ein Fall ohne auswertbares generiertes Razor-Dokument erwartet `low` und
  einen Grund für unvollständige Evidenz statt `high`. Ein
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

- Empfehlung: nur Code-Behind-Member; Komponententypen über Routen und Tags
  brauchen eigene Semantik und Tests.
- Empfehlung: Bei fehlender Razor-Generierung `low` mit klarer Unsicherheit,
  damit der Kandidat sichtbar bleibt.
- Gezielte Integrationstests mit echter Blazor-Fixture sind Teil der Abnahme;
  der Repo-Workflow verlangt für ihren Lauf einen ausdrücklichen Auftrag.
