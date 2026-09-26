# Dead-Code-Prüfkandidaten

[Aufrufvertrag](tools.md#gate-und-advisories) · [API-Policy und Budgetkonfiguration](../linter/configuration.md#dead-code-advisory)

Offline abrufen: `ainetlinter --docs mcp-dead-code`. Dokumentationsübersicht: `ainetlinter --docs index`.

`verify` ergänzt sein Quality-Gate um einen begrenzten Dead-Code-Scan. Die
Ergebnisse sind Prüfkandidaten, keine Löschfreigabe. Sie verändern weder Score
noch Verdict oder Verstoßzahl.

## Kandidaten

Gemeldet werden ausschließlich explizit deklarierte Typen und gewöhnliche
Methoden einschließlich Extension-Methoden aus eindeutig produktiven
Projekten. Ein verwaister Typ wird als Gruppe ausgegeben; seine Member werden
dann nicht einzeln gemeldet. Testreferenzen zählen als Nutzung. Testdeklarationen
werden nicht gescannt. Unbekannte Projekt- oder Referenzrollen sowie
unvollständige Referenzabdeckung unterdrücken Kandidaten.

Felder, Konstanten, Properties, Record-Komponenten, Events, Indexer,
Enum-Werte und lokale Variablen gehören nicht zum Kandidatenumfang. Ebenso
ausgeschlossen sind Konstruktoren, Accessoren, Operatoren, Finalizer und
generierte Deklarationen. Interface-Verträge, Implementierungen und Overrides
werden nicht als einzelne Methoden gemeldet. Eine statisch aufgelöste
Methodengruppe zählt auch ohne direkten Aufruf, etwa bei Delegate-Konvertierung
oder Endpoint-Registrierung.

Einstiegspunkt-Attribute `System.Runtime.CompilerServices.ModuleInitializerAttribute`
und `Microsoft.JSInterop.JSInvokableAttribute` schützen die markierte Methode
und ihren deklarierenden Typ. `DeadCode.EntryPointAttributes` ergänzt diese
Defaults um vollqualifizierte Attributtypen.

`DeadCode.DefaultApiSurface` steuert den Umgang mit extern sichtbarer API.
Standard ist `closed_solution`; `external_library` schützt effektiv externe
API. Referenzen werden solutionweit geprüft. Bekannte Laufzeitbindungen durch
Reflection, DI, Markup und Generatoren bleiben Schutzsignale, soweit sie
statisch erkannt werden. Dynamische Namen und externe Consumer sind nicht
vollständig analysierbar und müssen manuell gegengeprüft werden.

## Ausgabe und Abdeckung

`verify` zeigt für Dead Code nur Status, beobachtete Kandidatenzahl,
Abdeckung oder Abbruchgrund und einen Abrufhinweis. Es gibt keine
Confidence-Stufen, `test_only`- oder `undecidable`-Zeilen oder entsprechende
Zähler. Ein vollständiger Scan ohne Kandidaten ist keine globale Aussage über
Laufzeitbindung oder externe Nutzung.

Ein von Verify ausgegebener `continuationToken` öffnet exakt dessen Snapshot
über `get_verify_advisories(targetPath, category="dead_code",
continuationToken=...)`. Ohne Token wird ein gültiger vollständiger
Solution-Snapshot bei passender Solution-Version und Konfiguration
wiederverwendet; andernfalls beginnt ein neuer Scan. Ein `operationToken` holt
das Ergebnis eines laufenden Aufrufs ab und ist kein Seitentoken.

Detailseiten enthalten verbleibende Kandidaten mit kopierbaren `h:`-IDs und
Gegenprüfhinweisen. `scanCompleteness` beschreibt die Analyseabdeckung;
`listCompleteness` beschreibt nur, ob alle gespeicherten Einträge ausgegeben
wurden. Ein partieller Scan bleibt auch bei null Kandidaten `partial`.
Pagination startet keinen neuen Scan. Token verfallen nach 30 Minuten Leerlauf
oder bei Server-Neustart.

Das zusätzliche Advisory-Budget beträgt standardmäßig 10 Sekunden bei
`verify` und 60 Sekunden bei einem expliziten Solution- oder Detailabruf,
einschließlich Vorbereitung. Das Budget begrenzt weder Gate-Analyse noch
Solution-Laden.
