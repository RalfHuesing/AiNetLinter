# Roadmap: Blazor-Evidenz für Dead-Code-Kandidaten absichern

Verbindlicher Vertrag: [Konzept.md](Konzept.md). Die Punkte laufen in dieser
Reihenfolge; jeder Code-Punkt endet grün und mit einem eigenen Commit. Für
Build, MCP-Verify und FastTests gilt
`.agents/rules/AiNetLinter-Richtlinien.mdc`. Den gezielten Integrationstest
in Punkt 1 und den abschließenden vollständigen FastTest-Lauf ausführen.
`KnowHowToAI` bleibt unverändert.

- [x] **1. Echte Blazor-Bindungen als Regression belegen**
  - Intention: Ein semantisch gebundener Code-Behind-Member darf nicht als
    Dead Code erscheinen; ein unbenutzter Kontrollmember muss sichtbar bleiben.
  - Scope: `tests/Fixtures/BlazorPartialMini` um Web-Import, `@onclick`,
    `@Title`, Child-`EventCallback` und unbenutzten privaten Member ergänzen.
    In `src/AiNetLinter.IntegrationTests/Mcp/Tools/Verify/` einen Test für
    generierte Referenzen per `SymbolFinder`, Scanner und
    `VerifyAdvisoryProjector.CollectAsync` ergänzen. Einen getrennten
    Testfall ohne Web-Import und ohne andere gebundene Verwendung desselben
    Handlers anlegen, in dem dessen Name nur im generierten Markup-String
    steht. `DocumentsInScope > 0` in jedem Fixture-Scan prüfen;
    den gezielten Test in einem Checkout ohne Pfadsegment `worktrees` mit
    `pwsh scripts/test-integration.ps1 -Filter 'FullyQualifiedName~DeadCodeAdvisoryScannerBlazorIntegrationTests'`
    ausführen.
  - Nicht-Ziele: Keine Produktivcodeänderung, keine Aussage über
    Komponententypen, Routen oder bloße Namenstreffer.
  - Abnahme: Für gebundene Methode, Property und Callback ist je eine
    generierte semantische Referenz nachgewiesen; Scanner und Verify melden
    diese Symbole nicht als `dead_code`. Der unbenutzte Member bleibt
    Kandidat. Ohne Web-Import schützt der Markup-String den Handler nicht.
    Gezielter Integrationstest und vorgeschriebene Gates sind grün.

- [x] **2. Fehlende Razor-Evidenz konservativ einstufen**
  - Intention: Fehlende oder nicht auswertbare Razor-Generierung darf keinen
    `high`-Befund für referenzlose Code-Behind-Member erzeugen.
  - Scope: Zuerst einen isolierten roten FastTest mit `TestTempDirectory`,
    `.razor`, `.razor.cs`, `RegularService.cs`, Adhoc-Solution ohne Generator
    und `DocumentsInScope=2` schreiben. Danach
    `RazorGeneratedEvidenceIndex` und die Änderungen an
    `DeadCodeAdvisoryScanner`, `DeadCodeAdvisoryDiagnosticsScanner` und
    `DeadCodeScanContext` gemäß technischem Vertrag im Konzept umsetzen.
    FastTests für `Unavailable`, `Available`, gleichnamige Razor-Datei in
    anderem Verzeichnis, `confidence=high`-Filter sowie diagnostikbasierte
    Kandidaten in `Locals` und `Both` ergänzen.
  - Nicht-Ziele: Keine textbasierte Nutzungsheuristik, kein Lesen aus `obj`,
    keine Änderung der semantischen Referenzentscheidung oder der Regeln aus
    `tasks/deadcode-produktive-nutzung/Konzept.md`.
  - Abnahme: Der vorher rote Test ist grün: Beide referenzlosen
    Code-Behind-Methoden haben `low` mit Razor-Grund; die normale C#-Methode
    behält `high`. Diagnostik-Kandidaten folgen derselben Grenze. Ein
    auswertbares generiertes Dokument ohne semantische Referenz behält die
    normale Einstufung. Filter, Summary und Reason stimmen mit der
    endgültigen Einstufung überein; vorgeschriebene Gates sind grün.

- [x] **3. Gegenprüfung in MCP-Antworten sichtbar machen**
  - Intention: Agenten erkennen leere Standardreferenzsuchen und prüfen
    Advisory-Kandidaten vor einer Löschentscheidung eigenständig.
  - Scope: `FindReferencesTool` samt Formatter, Dead-Code-Resultat und
    `VerifyResponseFormatter.AppendAdvisories` nach Konzept anpassen.
    Vertragstests in den vorhandenen FastTests für leeres
    `.razor.cs`-Standardergebnis, Treffer, `includeGenerated=true`,
    `recommendedNextAction`, `summary.next` (auch bei Trunkierung) und
    sichtbaren Verify-Grund ergänzen. `Docs/mcp/tools.md` synchronisieren,
    Agentenregeln auf Widersprüche prüfen und das Abschlussgate aus den
    Repo-Regeln ausführen.
  - Nicht-Ziele: Kein neuer MCP-Parameter, keine CLI- oder
    Konfigurationsänderung, kein neuer Verify-Scope, keine Änderung an Score
    oder Verdict.
  - Abnahme: Nur der leere `.razor.cs`-Standardbefund enthält genau eine
    `next: includeGenerated=true`-Zeile. Bei vollständigen Kandidaten nennen
    beide Next-Action-Felder `countercheck`, bei Trunkierung `continue`;
    Verify zeigt den passenden Razor-Grund innerhalb seines 4-KiB-Budgets.
    `Docs/mcp/tools.md`, Tests und vollständiges Abschlussgate sind grün.

- [x] **Audit**
  - Intention: Die Umsetzung gegen das freigegebene Konzept und die echte
    Blazor-Grenze prüfen.
  - Scope: Code, Tests, Dokumentation, Gate-Nachweise und die drei
    Punkt-Abnahmen nur lesend prüfen. Befunde an den Orchestrator melden;
    dieser veranlasst bei Bedarf höchstens einen Korrekturpunkt.
  - Nicht-Ziele: Kein Produktivcode im Audit, keine Änderung am externen Repo.
  - Abnahme: Alle Muss-Kriterien und Nicht-Ziele des Konzepts sind abgeglichen;
    offene Abweichungen sind entweder korrigiert oder konkret benannt.
