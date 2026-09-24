# Roadmap: Dead Code nach produktiver Nutzung beurteilen

> **Nachtrag nach Commit `396e6795`:** Punkt 2 dokumentiert die ursprüngliche
> Umsetzung mit `unknown` als Pflichtentscheidung. Der aktuelle Produktdefault
> ist `closed_solution`; `unknown` wird als Legacy-Wert beim Laden dorthin
> migriert. Der damalige Umsetzungsumfang bleibt als Verlauf erhalten.

Ursprünglicher fachlicher Vertrag: [Konzept.md](Konzept.md). Für das aktuell
implementierte Verhalten gilt [Docs/linter/configuration.md](../../Docs/linter/configuration.md).
Die Punkte werden
seriell bearbeitet. Jeder Umsetzungspunkt ist eine Agent-Session:
Ist-Stand mit AiNetLinter-MCP prüfen, eigene Änderungen samt
Verhaltenstests abschließen, die Gates nach
`.agents/rules/AiNetLinter-Richtlinien.mdc` ausführen, den Punkt
nach belegter Abnahme abhaken und nur eigene Dateien einschließlich
der Roadmap committen. Bei Punkt 1 gilt Red-Test-First.

## Testtakt

Die verbindliche Gate-Reihenfolge bleibt `dotnet build`,
`verify(targetPath)`, passender FastTests-Lauf. Für den
FastTests-Lauf nach einem erfolgreichen Build nutzt jeder Punkt
den unten festgelegten Filter und
`-AdditionalArgs '--no-build'`; der konkrete Aufruf lautet
`pwsh scripts/test-fast.ps1 -Filter '<Punktfilter>' -AdditionalArgs '--no-build'`.
Neu angelegte FastTests liegen
in den vom jeweiligen Filter erfassten Klassen oder Namespaces.
Ein unverändert grüner Testnachweis wird nicht erneut gestartet.
Vor Punkt 3 gibt es keinen vollständigen FastTests- oder
IntegrationTests-Lauf.

Nach der letzten Produktions-, Testcode- oder
Konfigurationsänderung in Punkt 3 folgt das vollständige
Abschlussgate der Projektregel und danach
`pwsh scripts/test-integration.ps1` ohne Filterargument.
IntegrationTests laufen seriell und werden nicht abgebrochen.
Bei einem Fehler gilt die Wiederholungsregel des Konzepts;
StressTests werden nicht ausgeführt.

- [x] **1 — Produktive Referenzsemantik im Scanner**
  - Intention: Test-only genutzter Produktionscode wird im
    solutionweiten Roslyn-Suchraum als Dead-Code-Kandidat erkannt.
  - Scope: Zuerst einen roten xUnit-v3-Test mit einer synthetischen
    Roslyn-Solution schreiben: Ein Produktionsprojekt deklariert
    `MarkdownBuilder.BulletList`, und nur
    `MarkdownBuilderTests` ruft den Member direkt auf. Das bildet
    die im Repository vorhandene Konstellation als dauerhafte
    Regression ab.
    `DeadCodeAdvisoryScanner` und sein Ergebnis-Modell so ändern,
    dass jede direkte sowie Interface-/Override-Referenz die Rollen
    `production`, `test` oder `unknown` erhält. Die Ergebnismatrix
    `live`, `undecidable`, `test_only`, `unreferenced` und die
    bestehenden Schutzmechanismen entsprechen dem Konzept.
    FastTests decken produktive Cross-Project- und Friend-Aufrufe,
    Test-Friend-Aufrufe, Testpfade, unbekannte Stellen und Linked
    Files ab. Kandidatendokumente und Referenzsuchraum bleiben
    getrennt.
  - Nicht: Keine API-Policy, kein neuer MCP-Aufruf, keine
    Verify-Formatänderung und keine Testcode-Dead-Code-Analyse.
  - Abnahme: Der Rot-Test ist grün; test-only ist Kandidat,
    produktive Referenz schützt, unbekannte Referenz erzeugt
    `undecidable` statt Kandidat. Interface-/Override- und
    Whitelist-/Suppression-Fälle bleiben korrekt. Incremental Gate
    nach Projektregel grün mit
    `-Filter 'FullyQualifiedName~AiNetLinter.FastTests.Mcp.Tools.DeadCode'`;
    eigener Commit. Der isolierte Rot-Test läuft vor der
    Produktionsänderung mit seinem exakten Testnamen ohne
    `--no-build`.

- [x] **2 — Projekt-API-Policy und Verify-Preflight**
  - Intention: Extern sichtbare APIs werden nur auf ausdrückliche
    Projektentscheidung hin geprüft oder geschützt; fehlende
    Entscheidungen sind im einzigen `verify`-Aufruf handlungsfähig.
  - Scope: `Config`, `ProjectOverrideEntry` und
    `ProjectConfigResolver` um exakt `DeadCode.DefaultApiSurface` und
    `ProjectOverrides.<Muster>.DeadCode.ApiSurface` ergänzen.
    `ConfigLoader`, `ConfigNormalizer` und `ConfigSyncer`
    auf korrekte Deserialisierung, Vererbung und Synchronisierung
    der neuen Properties prüfen und entsprechend anpassen.
    Fehlend/ungültig ergibt `unknown`; `PathOverrides` bleiben
    ohne Einfluss. Die Scanner-Policy schützt bei
    `external_library` effektiv extern sichtbare Symbole anhand
    Roslyn-Accessibility und der enthaltenden Typkette;
    `closed_solution` lässt sie mit `low` confidence prüfen.
    `verify` validiert vor dem Gate alle Kandidatenprojekte und
    gibt bei `unknown` den Content-only-Fehler
    `DEAD_CODE_API_SURFACE_NOT_CONFIGURED` mit `isError`,
    vollständiger Projektliste und kopierbarem Beispiel aus.
    Die bestehende Fehlerbehandlung des Advisory-Projektors darf
    diesen Fehler nicht verschlucken. Die eigene
    `ainetlinter-rules.json` erhält ausdrücklich
    `closed_solution`. `Docs/linter/configuration.md` und
    betroffene Agentenregeln werden im selben Slice synchronisiert.
  - Nicht: Kein globaler Produktdefault `closed_solution`,
    keine API-Allowlist, kein zusätzlicher Toolparameter und
    kein partielles Gate-Ergebnis bei Konfigurationsfehler.
  - Abnahme: FastTests belegen alle drei API-Werte, First-Match-
    Overrides, fehlende/ungültige Angaben, effektive Sichtbarkeit
    einschließlich `protected internal` und öffentlichem Member
    im internen Typ. Der Verify-Vertragstest belegt `verdict: error`,
    `isError`, Content-only, alle Projektnamen, Feldhinweis,
    Beispiele und fehlende Teilergebnisse. Incremental Gate grün
    mit `-Filter 'FullyQualifiedName~DeadCode|FullyQualifiedName~ProjectOverrideResolutionTests|FullyQualifiedName~ConfigNormalizerTests|FullyQualifiedName~AiNetLinter.FastTests.Mcp.Tools.Verify'`;
    eigener Commit.

- [x] **3 — Dead Code in beiden Verify-Scopes priorisiert ausgeben**
  - Intention: Ein `verify`-Aufruf liefert für `changes` oder
    `solution` einen knappen, unmittelbar prüfbaren
    Dead-Code-Abschnitt, ohne den Qualitäts-Gate-Verdict aus
    Kandidaten abzuleiten.
  - Scope: `VerifyAdvisoryProjector` ruft den Scanner in beiden
    Scopes mit `Accessibility=All`, `Confidence=Both`,
    `Kind=All`, `Mode=Members` und `IncludeTests=false`
    auf. `changes` begrenzt nur Deklarationen; `solution`
    betrachtet alle produktiven Deklarationen. Der Formatter
    gibt die im Konzept definierten Zähler, Statuswerte,
    Trunkierung, `next`, Nutzungstyp, Testreferenzzahl,
    `no_production_static_reference` und bei Kandidaten genau
    einmal den kurzen Fehlalarm-Hinweis aus. Jeder Kandidat
    erhält eine echte, direkt an `find_references` übergebbare
    `h:...`-Kennung des bestehenden Handoff-Mechanismus.
    Dead Code wird vor Magic Values gerankt; Gate-Felder
    bleiben unverändert. Scannerfehler ergeben
    `deadCode.status=unavailable` statt eines falschen
    Clean-Claims. Die MCP-/Verify-Dokumentation,
    `Docs/linter/cli.md` und betroffene Agentenregeln werden
    synchronisiert.
  - Nicht: Kein zweites Dead-Code-Tool, kein automatisches
    Löschen, kein Gate-`fail` für Kandidaten und keine
    Ausweitung auf lokale Compiler-/IDE-Diagnostik.
  - Abnahme: FastTests belegen beide Scopes, identische
    Liveness bei gleichem Deklarationsscope, Gate-`pass`
    trotz Kandidaten, Ranking, vollständige Zähler bei
    Antwortkürzung, `partial`/`unavailable`, Content-only,
    Fehlalarm-Hinweis genau einmal und Handoff-Komposition
    auch bei Linked Files/gleichnamigen Symbolen.
    Incremental Gate grün mit
    `-Filter 'FullyQualifiedName~DeadCode|FullyQualifiedName~AiNetLinter.FastTests.Mcp.Tools.Verify'`.
    Danach vollständiges Abschlussgate mit ungefiltertem
    `pwsh scripts/test-fast.ps1` und ein ungefilterter,
    serieller `pwsh scripts/test-integration.ps1`-Lauf grün;
    eigener Commit.

- [x] **Audit — Konzept und Anwendung abgleichen**
  - Intention: Ein unabhängiger, lesender Audit bestätigt die
    vollständige Umsetzung ohne neue Produktänderung.
  - Scope: Ein lesender Audit gleicht Konzept-Akzeptanz mit Code,
    Tests, der tatsächlichen `verify`-Antwort, Konfiguration,
    Dokumentation und Commits ab. Er prüft insbesondere test-only,
    externe API, unbekannte Referenzen, den einen Verify-Aufruf,
    den Fehlalarm-Hinweis sowie die Nachweise für FastTests
    und IntegrationTests. Der Orchestrator
    übernimmt das Audit-Ergebnis und schließt danach die
    Roadmap-Checkbox.
  - Nicht: Keine Implementierung im Audit. Ein belegter Mangel
    wird als konkreter Korrekturpunkt an die Umsetzung
    zurückgegeben.
  - Abnahme: Alle Muss-Kriterien und Gates sind nachweislich
    erfüllt; keine Checkbox wird allein aufgrund einer
    Behauptung geschlossen.
