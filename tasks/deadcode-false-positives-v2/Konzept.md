---
status: draft
---

# Dead-Code-Advisories mit kleinem, prüfbarem Signal

## Intention

AiNetLinter soll in großen, beliebigen .NET-Solutions wenige und nachvollziehbare Dead-Code-Prüfkandidaten liefern. Ein Kandidat bedeutet weiterhin **manuell gegenprüfen**, niemals automatisch löschen. `verify` bleibt ein Quality-Gate für Lint-Verstöße und weist nur knapp auf die getrennt abrufbaren Dead-Code-Ergebnisse hin.

## Befund und Entscheidung

Der vollständige Planner-Snapshot vom 26.09.2026 enthält 543 Kandidaten: 310 `test_only`, 233 `unreferenced` sowie 33 separat gezählte `undecidable`-Symbole. Die Gegenprüfung im Ordner `temp/ainetlinter-deadcode-v2` belegt lebendige Testhilfen, JSON-/Dapper-/XAML-Properties und Framework-Einstiege unter den Meldungen. Zugleich blieben 23 bereits in v1 bestätigte tote Symbole im v2-Snapshot `unreferenced`. Die enge Suche nach referenzlosen Typen und Methoden bleibt deshalb erhalten; die breit streuende Suche nach ungelesenen Daten-Membern und die Ausgabe unsicherer Nutzungsrollen entfallen.

Der aktuelle Dead-Code-MCP-Vertrag veröffentlicht keine Confidence-Stufen. `high`/`low` in einer Kurzmeldung würde eine nicht belegte Sicherheit suggerieren. Die Kurzmeldung nennt daher nur die beobachtete Kandidatenzahl und die Scanabdeckung.

## Scope

### Muss

- **Kandidaten:** Nur explizit deklarierte Typen und gewöhnliche Methoden einschließlich Extension-Methoden aus eindeutig produktiven Projekten. Ein Typ wird einmal als Gruppe gemeldet; seine Methoden erscheinen dann nicht zusätzlich. Ein Kandidat hat im vollständig ausgewerteten Solution-Kontext keine relevante statische Referenz außerhalb seiner Deklaration und keine konkrete bekannte Laufzeitbindung. Referenzen aus Tests zählen als Nutzung und unterdrücken den Kandidaten. Die bestehende Projektrollenbestimmung und `ProjectRoles`-Konfiguration gelten weiter; bei unbekannter Rolle wird keine Totcodebehauptung ausgegeben.
- **Kontrakte und Laufzeitbindung:** Interface-Member und deren Implementierungen sowie Overrides werden als einzelne Methoden nicht gemeldet. Compiler-Einstiegspunkte, generierte Deklarationen, Konstruktoren und die unten ausgeschlossenen speziellen Member bleiben außerhalb der Kandidatenmenge. Echte Attribute `System.Runtime.CompilerServices.ModuleInitializerAttribute` und `Microsoft.JSInterop.JSInvokableAttribute` schützen die markierte Methode und ihren Typ. Die Erkennung vergleicht semantisch den vollqualifizierten Attributtyp, nicht bloße Namen. Projekte können über `DeadCode.EntryPointAttributes` weitere vollqualifizierte Attributtypen **zu diesen zwei Defaults hinzufügen**. Das ermöglicht zum Beispiel `Microsoft.SemanticKernel.KernelFunctionAttribute`, ohne es fest in die allgemeine Engine einzubauen.
- **Statische Referenzen:** Eine semantisch aufgelöste Methodengruppe zählt auch ohne `InvocationExpression`, insbesondere bei Delegate-Konvertierung und Minimal-API-Registrierung. Überladungen werden einzeln aufgelöst. Ist die Bindung nicht eindeutig, entsteht daraus kein Kandidat. Die vorhandenen konservativen Grenzen für Reflection, DI, Razor/XAML, Generatoren, externe Nutzer und fehlende semantische Abdeckung gelten für die verbleibenden Typen und Methoden. `closed_solution` und `external_library` behalten ihre Bedeutung.
- **Ausgabe:** `verify` zeigt keine Dead-Code-Symbolzeilen und setzt dafür kein `next=review_now`. Es zeigt `status=complete|partial|unavailable` und die Zahl der **beobachteten** Kandidaten. Bei vorhandenem Snapshot nennt es `get_verify_advisories(category=dead_code, continuationToken=<Token>)`, sonst den Abruf ohne Token. `targetPath` ist dabei dasselbe Ziel wie bei `verify`. Bei `partial` oder `unavailable` darf `0` nicht als Entwarnung erscheinen; Abdeckung und Grund bleiben sichtbar. Gate-Verstöße und andere Advisory-Kategorien behalten ihre bisherigen Ausgaben und ihre Priorität innerhalb des Antwortbudgets. Dead Code verändert weiterhin weder Score noch Verdict noch `violationCount`.
- **Detailabruf:** `get_verify_advisories(category=dead_code)` liefert die Kandidaten mit unverändert weiterverwendbaren `symbolIdentifier`-Werten, Gegenprüfhinweis, Scanabdeckung und Pagination. Ein Token aus `verify` öffnet genau dessen Snapshot ohne neuen Scan. Ohne Token wird ein noch gültiger, zur Solution, Konfiguration und zum angeforderten Solution-Scope passender vollständiger Snapshot wiederverwendet; sonst beginnt ein neuer Scan. Ein abgebrochener Scan wird ausdrücklich als `partial` ausgewiesen, selbst wenn er null Kandidaten fand. `listCompleteness` bleibt von `scanCompleteness` getrennt.
- **Entkernung und Verträge:** `test_only`, `undecidable` und Confidence erscheinen weder als Dead-Code-Befunde noch als entsprechende Zähler oder Detailzeilen. Interne Ungewissheit unterdrückt Kandidaten weiterhin. Nicht mehr benötigte Daten-Member-Analyse und ausschließlich daran hängende Heuristiken, Modelle, Konfigurationsfelder, Tests und Dokumentation werden entfernt; gemeinsam benötigte Nutzungs- und Abdeckungslogik bleibt. Die öffentliche Dokumentation, `ainetlinter-rules.json` und Agentenregeln beschreiben nur den neuen, tatsächlich implementierten Vertrag.

### Nicht

- Keine Dead-Code-Kandidaten für Felder, Konstanten, Properties, Record-Komponenten, Events, Indexer, Enum-Werte, lokale Variablen oder Testprojekt-Deklarationen. Damit entfällt auch der echte Fund `_persistence`; die bekannte Fehlalarmklasse der serialisierten und gebundenen Daten-Member wiegt schwerer.
- Kein besonderer Testhilfe-Modus, keine Confidence-Einteilung, keine automatische Löschung und keine projektspezifischen Namen oder Pfade in der Engine.
- Keine Änderung am Planner-Quellcode. Dessen Snapshot dient nur als lesende Gegenprobe.

## Verifikation

- Isolierte xUnit-v3-Regressionen reproduzieren zuerst die belegten Fehlalarme. Danach belegen sie: lebendige Methodengruppen (`AuthJwtSigningDisabled`/`MapPost`), `JsonConverter.Read`/`Write`-Overrides, Interface-Slots, `JSInvokable` und konfiguriertes `KernelFunction` fehlen; ein wirklich unreferenzierter Typ und eine unreferenzierte Überladung bleiben; Testreferenzen und ungelesene Wire-Properties erscheinen nicht.
- Vertragstests prüfen die reine Dead-Code-Kurzmeldung in `verify`, unveränderte Gate-Daten, Snapshot-Fortsetzung samt `h:`-IDs und Pagination sowie die ausdrückliche Teilabdeckung bei Zeitablauf. Ein frischer Detailabruf nach einem vollständigen Verify-Solution-Scan darf nicht erneut 60 Sekunden arbeiten und leer als scheinbar sauber enden.
- Der große Planner-Snapshot wird lesend gegen die neue Fassung geprüft: Belegte unreferenzierte Methoden und Typen ohne Kontraktbindung wie `SqlDateTimeMapping` bleiben; die belegten Fehlalarmklassen verschwinden. Die Zahl ist eine Messgröße, kein starres Ziel. Build, MCP-Verify und Tests folgen den verbindlichen Repo-Gates erst während der späteren Umsetzung.

## Arbeitsgedächtnis (nur Draft)

Empfehlung zur Freigabe: den eng gefassten Scanner behalten. Ein vollständiges Streichen würde die belegten unreferenzierten Methoden und Typen verlieren. Die alternative Aufnahme privater Felder brächte vor allem `_persistence` zurück, erhöht aber Scope und Unsicherheit der Member-Analyse. Vor `status: ready` ist die ausdrückliche Freigabe dieses Scopes erforderlich.
