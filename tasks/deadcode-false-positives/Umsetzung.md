# Umsetzung und Nachweise

Ausgangsstand: `b640b2163e6b031ca7b2beb65a6353352bbba51f`, sauberer Working Tree.
Serielle Umsetzung ohne Subagenten. Die im Konzept genannten externen
Evidenzdateien waren zugänglich und wurden ausschließlich gelesen.

## Verhalten

Der Advisory verwendet einen solutionweiten Nutzungsindex statt einer
vollständigen Roslyn-Referenzsuche pro Member. Testrollen, generierte Quellen
und konkrete indirekte Bindungen fließen ein. Verwaiste Typen werden
gruppiert; Wrapper, `test_only`, Konstanten und fehlende produktive Leser
bleiben sichtbar. Zeitbudget, offene Arbeit, fachliche Unsicherheit und
Ausgabelimit sind getrennt. Verify-Fortsetzungen lesen denselben Snapshot.

Konfiguration, MCP-Beschreibungen, CLI-Dokumentation und Agentenregeln sind
synchronisiert. Die vollständige Regel-/Grenzentabelle steht in
[`Docs/mcp/dead-code.md`](../../Docs/mcp/dead-code.md).

## Reproduktionen und Gegenproben

Isoliert rote Reproduktionen wurden vor ihren Korrekturen nachgewiesen:

- Produktionsaufruf unter `Features/Test`, Metadaten-Override, Modulinitialisierer,
  Record-Schlüssel, generische Property-Reflection, private Feld-Reflection und
  gefilterte Assembly-Aktivierung aus den vorhandenen Regressionstests.
- Echter JSON-Vertrag, bedingtes `JsonIgnore`, Configuration-Binder und
  Options-Registrierung, jeweils mit ungebundenem Nachbarmember.
- Testrolle generischer Reflection, fehlender Friend-Consumer und externe
  Vertragsimplementierung mit weiterhin prüfbarem privaten Detail.
- XAML-Datenbindung, konkret ungeklärter Binding-Kontext, Razor-Event und
  Razor-Parameter mit ungebundenen Gegenproben.
- Attributgefilterte Member-Reflection, dynamischer Reflection-/Assemblyfilter
  und berechnete Record-Property, die nicht von Gleichheit gelesen wird.
- Partielle Klassen, unbekannte Deklarationsrolle, fehlende Änderungsbasis,
  entferntes letztes Nutzungsziel, Budgetabbruch vor Vorbereitung und getrennte
  Ausgabevollständigkeit. Testleser bleiben neben fehlender produktiver Lesung sichtbar.

Bereits korrektes Verhalten blieb als grüner Schutztest erhalten, insbesondere
echte WPF-Attached-Property-Bindung und Referenzen aus generiertem Code.
Middleware und weitere bestehende Positivfälle wurden nicht künstlich rot
gemacht. Frameworktests verwenden tatsächliche Framework-Metadaten. Die
WPF-Referenzassembly wird als Testfixture aus dem offiziellen .NET-Referenzpaket
bezogen; die Produktassembly erhält keine WPF-Abhängigkeit.

Ältere Erwartungen an Konstruktor-/Event-Kandidaten, pfadbasierte Testrollen,
Typ-/Member-Doppelmeldungen und Confidence-Ausgabe wurden an die verbindliche
Konzeptentscheidung angepasst. `CS0162` bleibt ausdrücklich außerhalb dieses
Advisorys. Es wurde kein Test entfernt, um einen fachlichen Fehler zu verdecken.

Budgetabbruch während des Scans ist zusätzlich mit einer steuerbaren Uhr
abgesichert: ein Dokument bearbeitet, eines offen, nur der geprüfte Kandidat
sichtbar; die letzte Ausgabeseite bleibt als partieller Scan gekennzeichnet.
Snapshot-Pagination prüft 2.000 eindeutige Einträge einschließlich unveränderter
Handoff-IDs, ohne erneuten Scan und ohne Duplikate.

## Laufzeit und Abdeckung

Eine einmalige kleine Vergleichsmessung am 26.09.2026 verwendete dieselbe
bereits geladene Roslyn-Fixture mit 9 Dokumenten und 80 Methoden:

| Referenzverfahren | Gemessene Zeit | Identisches Ergebnis |
| --- | ---: | --- |
| Bisheriges `FindReferencesAsync` je Methode | 27,113 ms | 40 benutzt, 40 unbenutzt |
| Gemeinsamer Index einschließlich Aufbau | 5,369 ms | 40 benutzt, 40 unbenutzt |

Das vergleicht den Referenzalgorithmus, nicht den vollständigen alten und neuen
Scanner. Solution-Laden, große reale Solutions und sämtliche indirekten Kanäle
sind damit nicht vermessen. Keine Präzisionsquote oder Skalierungszusage.
Auf Nutzerhinweis wurden weitere Messungen beendet und der Messlauf aus den
regulären Tests entfernt. Reale Nutzung soll weiteres Nachschärfen bestimmen.

Vor dem Abschlussgate bestanden 114 einschlägige FastTests. Das finale Gate
bestand: `dotnet build` für alle vier Projekte mit null Fehlern/Warnungen,
`verify(scope="solution")` mit `pass`, Score 10 und null Verstößen sowie
`pwsh scripts/test-fast.ps1` mit 2.811 bestandenen Tests, null Fehlern und
null übersprungenen Tests (8 Sekunden gemeldete Testdauer).
Die drei älteren Konstruktor-Handoff-Tests folgen jetzt vom Typgruppen-Advisory
zur Klassenstruktur; ihre Prüfung unveränderter Konstruktor-Identifier und
Referenz-/Kontextauflösung bleibt erhalten.

## Bewusste Grenzen

Die Analyse verfolgt keine beliebig tiefen dynamischen Datenflüsse und beweist
keine globale Laufzeiterreichbarkeit. Komplexe Reflectionketten, individuelle
Mapper-/Serializeroptionen, dynamische JS-Aufrufe und externe Content-/Consumer-
Quellen bleiben Gegenprüfaufgaben. Konkrete bekannte Unklarheiten erscheinen
getrennt; fehlende Vorbereitung oder Zeitablauf ergeben offene Scanarbeit.
Ein kompletter Scan bezeichnet den implementierten Umfang, keine vollständige
Dead-Code-Erkennung. Es wurden keine Änderungen an externen Repositories und
keine Löschungen im analysierten Produkt vorgenommen. Kein Push; keine
Integration- oder Stress-Testläufe.
