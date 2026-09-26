# Dead-Code-Prüfkandidaten

`verify` ergänzt sein Quality-Gate um einen begrenzten Advisory. Ein Kandidat
ist eine Aufforderung zur Gegenprüfung, keine Löschfreigabe. Auch `private`
und null statische Referenzen beweisen keine sichere Entfernbarkeit.
Advisories verändern weder Score noch Gate-Verstöße oder Verdict. Im
Dead-Code-Content gibt es keine Confidence-Stufen.

## Ergebnis und Symbolumfang

Die Entscheidung erfolgt anhand der API-Policy, produktiver Nutzung und
konkret vorhandener ungeklärter Nutzungskanäle. Bekannte produktive Nutzung
unterdrückt eine Kandidatenzeile. `external_library` schützt außerdem die
effektiv externe API, ohne eine tatsächliche Nutzung zu behaupten.

- `unreferenced`: keine relevante Nutzung erkannt.
- `test_only`: relevante Leser/Aufrufer nur aus echten Testprojekten. Diese
  Kandidaten bleiben ausdrücklich erhalten; Testdeklarationen selbst werden
  nicht zur Bereinigung vorgeschlagen.
- `no_production_read` steht im Grund von Feld-/Property-Kandidaten. `writes`
  zählt erkannte Zuweisungsstellen, `testReads` die Testleser. Damit bleibt
  `test_only` neben fehlender produktiver Lesung sichtbar. Die Zählung umfasst
  statische Referenzstellen; Initialisierer und implizite Schreibvorgänge sind
  keine vollständige Schreibstatistik.
- `undecidable`: ein konkret einschlägiger Kanal oder eine Rolle ist offen.
  Die Standardantwort nennt Anzahl und gruppierte Gründe; der gespeicherte
  Snapshot enthält abrufbare Symboldetails. Das ist keine unbearbeitete Arbeit.

Eigenständige Kandidaten sind Typen einschließlich Records, Structs,
Interfaces, Enums und Delegates, gewöhnliche Methoden einschließlich
Extension-Methoden und expliziter Implementierungen sowie Felder, Konstanten
und Properties. Ganze verwaiste Typen bilden eine Gruppe; ihre Member werden
nicht zusätzlich ausgegeben. Membernutzung einschließlich reduzierter
Extension-Aufrufe zählt für den Eltern-Typ. Rein interne Aufrufe halten einen
sonst verwaisten Typ nicht allein am Leben. Eine Implementierungsdeklaration
ist kein Aufruf ihres Vertrags; ein lebendes Delegationsziel schützt keinen
unaufgerufenen Wrapper. Identische Literale verwenden keine Konstante.

Konstruktoren, Accessoren, Operatoren, Finalizer, Events, Indexer, einzelne
Enum-Werte und generierte Deklarationen sind keine eigenen Kandidaten.
Dadurch fehlen insbesondere isoliert überzählige Konstruktoren und tote
Enum-Werte. Ihre Referenzen werden trotzdem ausgewertet. Lokale Variablen
und `CS0162` gehören nicht zum öffentlichen Advisory-Umfang.

## Konkrete Nutzungsregeln

Die Tabelle beschreibt die implementierte begrenzte Analyse. Die positive
Gegenprobe gehört jeweils zur Regel; Namen und Pfade bestimmter Produkte
sind keine Whitelist.

| Signal | Wirkung und verhinderter Fehlalarm | Erhaltene Kandidaten / Grenze |
| --- | --- | --- |
| Roslyn-Einstiegspunkt oder echtes `ModuleInitializerAttribute` | Schützt Einstieg und Eltern-Typ, obwohl kein gewöhnlicher Aufrufer existiert. | Andere statische Hilfstypen und normale Methoden bleiben prüfbar; gleichnamige eigene Attribute schützen nicht. |
| Produktiver Vertragsaufruf | Referenzen auf Interface-/Basismember zählen auch für Implementierungen. | Unaufgerufene eigene Verträge und Wrapper bleiben prüfbar. |
| Konkrete Instanz in einem Metadatenaufruf, bekannte DI-/Options-/Hosted-Service-Registrierung | Ordnet Metadaten-Overrides und Interface-Hooks zu, die das Framework indirekt aufrufen kann. | Gewöhnliche zusätzliche Methoden bleiben prüfbar. Die begrenzte Regel beweist nicht die Erreichbarkeit jedes einzelnen Hooks. |
| Echtes `UseMiddleware<T>` und öffentliche Instanzmethode `Invoke`/`InvokeAsync` mit `HttpContext` und `Task` | Schützt den konventionsgebundenen Einstieg. | Gleicher Methodenname ohne passende Registrierung/Signatur bleibt prüfbar. |
| `typeof(...).Assembly.GetTypes()`, bekannter Interface-/Basistypfilter und `Activator.CreateInstance` | Ordnet die passende nichtabstrakte Typmenge zu. | Nicht passende Typen bleiben Kandidaten. Zusätzliche unbekannte Filter oder fehlende Aktivierung ergeben `assembly_filter_or_activation` innerhalb der begrenzten Menge. Beliebige interprozedurale Assembly-Flüsse werden nicht bewiesen. |
| `typeof(T).GetField(s)`/`GetProperty(ies)`/`GetMethod(s)` mit konkretem Namen, Flags und Reflectionoperation | Ordnet Member anhand Typ, Art, Sichtbarkeit und Instanz-/Static-Flags zu; konkrete generische Aufrufe liefern `T` und Kanalrolle. | Nicht ausgewählte Member bleiben Kandidaten. Dynamische Namen/Flags, fehlende Operation oder nicht aufgelöste Auswahl ergeben `reflection_selection`. |
| Direkter `IsDefined(typeof(Attribute), ...)`-Filter in einer Reflection-`foreach` | Berücksichtigt eigene Attribute über ihren tatsächlichen Leser. | Unmarkierte Properties bleiben prüfbar; komplexe Filter werden nicht pauschal als produktive Nutzung gewertet. Die Auswertung ist keine allgemeine Datenflussanalyse. |
| Echtes `JsonSerializer` mit konkretem generischem Vertrag | Bindet öffentliche Instanz-Properties. Dauerhaftes `JsonIgnore` schließt aus, bedingtes Ignorieren nicht. | Fremde gleichnamige Attribute und ungebundene Typen schützen nicht. Benutzerdefinierte Converter, verschachtelte Verträge und Optionsdatenfluss bedürfen zusätzlicher Gegenprüfung. |
| Echtes `ConfigurationBinder.Get<T>` oder `Bind` mit erkennbarem Typ | Bindet öffentliche schreibbare Properties; vertragliches Schreiben genügt. | Private/ungebundene Properties bleiben prüfbar. Ein Options-Callback ergibt `configuration_options`; bloßes `IOptions<T>` schützt nichts. |
| Generischer Aufruf von Metadaten-`Dapper.SqlMapper` | Kennzeichnet Properties des konkreten Datenvertrags als `mapper_columns_or_custom_mapping`, da Spalten/Mapping offen sind. | Andere Typen werden nicht gesperrt. Keine Behauptung, alle Properties seien tatsächlich gemappt. |
| Benutzte Record-Gleichheit, Hash-/Collection-Key-Verwendung oder Ausgabe | Berücksichtigt implizite Nutzung gespeicherter Properties; Ausgabe kann weitere Properties lesen. | Bloße Konstruktion oder Existenz synthetisierter Methoden schützt nicht. Berechnete Properties werden durch Gleichheit allein nicht geschützt. |
| XAML `x:Static`, aufgelöster DataContext-Bindingpfad oder Event am erkannten Elementtyp | Schützt die zugeordneten Member ohne direkten C#-Aufruf. | Gleiche Strings und ungebundene Nachbarmember bleiben Kandidaten. Fehlender Binding-Kontext ergibt `markup_binding_context` für passende Propertynamen im Projekt. |
| XAML Attached Property mit echtem Metadatentyp `System.Windows.DependencyProperty` | Bindet Feld sowie `Get...`-/`Set...`-Methoden des Owners. | Normale Nachbarmethoden bleiben Kandidaten; nicht aufgelöste Attached-Property-Konventionen bleiben unentscheidbar. |
| Razor-Event/Binding am zugeordneten Code-Behind; Component-Tag oder `@page` mit echter `ComponentBase` | Bindet benannte Member, Framework-Hooks, Inject-Properties und konkret gesetzte echte Parameter. | Ungebundene Methoden/Parameter bleiben prüfbar. Mehrdeutige Typzuordnung wird nicht als eindeutige produktive Nutzung behandelt. Fehlender Generatoroutput allein sperrt keine Komponente. |
| JS `DotNet.invokeMethod[Async]` mit Assembly- und Methoden-ID | Ordnet echtes `JSInvokableAttribute` zu. | Beliebige gleichlautende Strings schützen nicht. Dynamische und Instanz-JS-Aufrufe benötigen zusätzliche Gegenprüfung. |
| Vorhandene normale/generated Roslyn-Dokumente | Referenzen zählen einschließlich ausgeschlossener generierter Deklarationen. | Generierte Symbole selbst sind keine Bereinigungskandidaten. Nicht verfügbare semantische Dokumente lassen den Scan partiell. |
| Fehlender relevanter Friend-Consumer | `friend_consumer_missing` statt behaupteter Referenzabwesenheit für interne Symbole. | Private Implementierungsdetails bleiben prüfbar. |
| Unbekannte Referenz-/Deklarationsrolle | `reference_role_or_binding` bzw. `declaration_role` statt Test-/Produktionsannahme. | Eindeutig produktive Nutzung genügt weiterhin zur Unterdrückung. |

Markup wird einmal je Projekt/Snapshot geprüft: Workspace-Dokumente und
physische `.xaml`, `.razor`, `.js` unter dem Projektverzeichnis, ohne
Build-/Paketverzeichnisse, verlinkte Verzeichnisse und verschachtelte fremde
Projekte. Grenze: 2.000 Markupdateien, 1 MiB je physischer Datei; zu große
Workspace-Texte werden ebenfalls begrenzt. Unlesbare, zu große oder nicht
auswertbare XAML-Dateien erzeugen eine Abdeckungslücke statt Negativbefunden.
Beliebige externe Content-Verzeichnisse und dynamisch erzeugte Markups sind
nicht vollständig erschließbar. Konfiguration wird über konkrete Binderaufrufe
erkannt, nicht über beliebige gleichlautende JSON-Schlüssel.

## Scope und Budget

`changes` nimmt geänderte Deklarationen und bisherige Referenzziele aus dem
Git-HEAD-Vergleich auf, auch in unveränderten Dateien. Entfernte Dateien,
Projekt-/Regelkonfiguration sowie Razor-/XAML-/JS-/JSON-Änderungen erweitern
den Scope konservativ auf die Solution. Alte C#-Referenzziele erhalten
Vorrang, danach folgen Typgruppen und Member, innerhalb gleicher Priorität
deterministisch nach Ort. Das ist keine globale Erreichbarkeitsanalyse.
Ohne Vergleichsbasis bleibt `changesBasis=unavailable` und der Scan partiell.

Beide Scopes bauen denselben solutionweiten Nutzungsindex einschließlich
Testrollen und generierter Quellen. Kein vollständiger
`FindReferencesAsync`-Lauf je Member. Ein neuer Scan erhält einen neuen Index;
Folgeseiten lesen ausschließlich den vorhandenen Snapshot.

Das zusätzliche Dead-Code-Budget beträgt standardmäßig 10 Sekunden bei
normalem Verify, auch bei konservativer Scope-Erweiterung, und 60 Sekunden
bei explizitem `verify(scope="solution")` oder neuem
`get_verify_advisories(category="dead_code")`. Vorbereitung gehört dazu.
Das Budget begrenzt weder Gate-Analyse noch Solution-Laden. Cancellation und
Dokumentgrenzen beenden die Advisory-Arbeit; kein anschließender Hintergrundscan.

## Ausgabe und Fortsetzung

Verify zeigt maximal 20 Kandidatengruppen innerhalb von 8 KiB UTF-8, nur
vollständige Einträge. Gate-Evidenz hat Vorrang. Die Zahlen sind beobachtete
Ergebnisse des Scans; bei partiellem Scan ist die Gesamtmenge weiterer Funde
unbekannt. `processedDocuments`/`openDocuments`, `elapsedMs`, `stopReason`,
`changesBasis` und `excludedKinds` beschreiben die Abdeckung. Offene Dokumente
sind keine geprüften `undecidable`-Symbole. Null Kandidaten bei partiellem Scan
sind keine Entwarnung.

Ein von Verify ausgegebener `continuationToken` öffnet denselben Snapshot
über `get_verify_advisories(targetPath, category="dead_code", continuationToken=...)`.
Ohne Token startet dieses Tool einen neuen Scan. Ein `operationToken` dient
vor dem Endergebnis nur zum Abholen des laufenden Aufrufs; er ist kein Seitentoken.

Detailseiten enthalten höchstens 64 KiB und führen Kandidaten plus
`undecidable`-Details (`population=candidates+undecidable_details`) auf.
`candidates` zählt nur Prüfkandidaten, `shown`/`offset`/`truncatedBy` die
Seitenpopulation. `scanCompleteness=partial` bleibt auch auf der letzten
Seite erhalten; `listCompleteness=complete` bedeutet ausschließlich, dass
alle gespeicherten Einträge ausgegeben wurden. Pagination spart keine Scanzeit.
Token verfallen nach 30 Minuten Leerlauf oder Server-Neustart. Dann verlangt
`INVALID_CONTINUATION_TOKEN` einen neuen Scan.

`symbolIdentifier=h:...` unverändert an `find_references`, `get_symbol_body`
oder `get_feature_context` übergeben. Den konkreten Gegenprüfhinweis beachten,
Laufzeitbindungen/Verträge klären und erst dann über eine Entfernung entscheiden.
Eine vollständige Dead-Code-Erkennung oder eine Präzisionsquote wird nicht zugesagt.
