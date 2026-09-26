# Dead-Code-Advisories: Befund und Ursachen

Stand: 2026-09-26. Analysiert wurde der vorhandene Snapshot unter
`C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\temp\ainetlinter-deadcode`.
Das fremde Repository wurde nicht geändert. Die dortigen `h:`-Kennungen sind
sessiongebunden; für Gegenprüfungen wurden Symbole neu gesucht.

## Maßstab und Datenqualität

Für `closed_solution` heißt Dead Code: keine produktive Nutzung innerhalb der
geschlossenen Solution. Testprojekte liefern keine produktive Nutzung. Auch
öffentliche Member können daher tot sein. Bei `external_library` ist eine
öffentliche API ohne interne Aufrufer allein kein Dead-Code-Beweis.

Der Snapshot enthält 714 Kandidatenzeilen: 403 `test_only`, 311
`unreferenced`. Die Rohdatei enthält 697 unterschiedliche, **groß-/kleinschreibungsgenau**
verglichene Handoff-IDs (392 `test_only`, 305 `unreferenced`); 14 IDs kommen
zweimal, eine viermal vor. Diese IDs dienen hier nur zum Zählen im selben
Snapshot, nicht als dauerhafte Symbolidentität. Die fachliche Einordnung
erfolgte stichprobenartig, **nicht** für alle 697 Symbole. 40 Zeilen haben
`confidence=high`: 39 `test_only` und das Feld `_persistence` als einziger
`unreferenced`-Treffer. `verify(scope: solution)` bestand im fremden Repo mit
Score 10.0 und 0 Regelverstößen; Advisories sind davon unabhängig.
Ein isolierter Test mit `GetField(..., NonPublic)` zeigt, dass auch ein
privates Feld ohne statische Referenz produktiv gelesen werden kann und vom
Scanner trotzdem `confidence=high` erhält. Die Einstufung ist daher kein
generell sicherer Dead-Code-Nachweis.

Die Quelle für diese Zahlen ist `laufzeit/_deadcode-alle.tsv`; Befundnotizen
stehen in `befund.md` und `roslyn-hinweise.md` desselben externen Ordners.
Beide Notizen nennen **alle** `test_only`-Treffer Fehlalarme. Das widerspricht
der hier vorgegebenen Definition: Ein Member, das ausschließlich ein echtes
Testprojekt aufruft, ist für `closed_solution` produktiv ungenutzt. Die
Behauptung „Referenz vorhanden“ reicht nicht für „produktiv benutzt“.

## Bestätigte Fehlalarme

| Fall | Gegenbeleg zur Advisory | Ursache im aktuellen Scanner |
| --- | --- | --- |
| Pfadsegment `Test` trotz belegter produktiver Einstiegskante | Der isolierte Rot-Test ruft `Counter.Next()` aus `Program.Main` auf. Die Feldlesung steht in `Features/Test/Counter.cs` und wird trotzdem als `test_only` klassifiziert. Im externen Hostprojekt liest `DataTableUserConfigLayoutTestPageBase` sein Feld in derselben Datei (Zeilen 88 und 95); ob diese Seite produktiv erreichbar ist, ist damit **nicht** bewiesen. Alle 39 `test_only/high`-Zeilen liegen im Hostprojekt; 113 `test_only`-Zeilen deklarieren Symbole unter `Components/Pages/Test`. Das ist eine Prüfmenge, keine bestätigte FP-Zahl. | `ClassifyReferenceRole` stuft Referenzen durch `TestDetector.IsTestFile(document.FilePath)` als Test ein, selbst wenn `IsTestProject` falsch ist. Ein Pfad kann die produktive Erreichbarkeit widerlegen, ohne sie zu prüfen. |
| Laufzeit-Dispatch über Metadaten-Basistyp | `MainLayout.OnInitialized` ist ein Blazor-Override; `find_references` zeigt keinen Quellaufruf. Im isolierten Test wird `Stream.Read` über `Stream.CopyTo` aufgerufen und trotzdem als `unreferenced` gemeldet. | `GetRelatedReferenceSymbols` nimmt `OverriddenMethod` bereits auf. `SymbolFinder.FindReferencesAsync` findet aber keinen Aufruf, der nur in einer referenzierten Assembly beziehungsweise zur Laufzeit erfolgt. Die vorhandene Verwandtschaftskante allein genügt nicht. |
| Modulinitialisierer | `SchedulerTerminDetailsBridgeModule.Initialize` trägt `[ModuleInitializer]`; der Typ selbst wird als tot gemeldet. Der isolierte Rot-Test reproduziert das mit `ModuleHook`. | `DeadCodeWhitelist` schützt die attributierte Methode, aber `CheckAndRecordTypeAsync` beurteilt den statischen Eltern-Typ anhand seiner eigenen Textreferenzen und der Referenzen seiner Member. Der vom Compiler erzeugte Startaufruf fehlt. |
| Record-Schlüssel | `RangeIdentity.SiteSlug` wird als `unreferenced` gemeldet, obwohl `RangeIdentity` als Cache-Schlüssel dient. Der isolierte Rot-Test reproduziert dies mit `Dictionary<Key, int>.ContainsKey`. | Positions-Property und Konstruktorparameter sind getrennte Symbole; `Equals` und `GetHashCode` sind compilererzeugt. Der Scanner zählt diese implizite Lesung nicht. |
| XAML-Namen | `WizardText.Back` erscheint in `WizardShell.xaml:74` als `{x:Static wizard:WizardText.Back}`. Weitere Beispiele sind `x:Class`, Bindings und Attached Properties im Setup-Projekt. | `SymbolFinder` indexiert XAML-Text nicht als C#-Referenz. Ohne verlässlich einbezogenen XAML-Generator-Output oder einen XAML-Index ist die Negativaussage unvollständig. Dieser Fall ist im vorliegenden Schritt noch kein Rot-Test. |
| Framework-Konventionen und Aktivierung | `CorrelationLogScopeMiddleware.InvokeAsync(HttpContext)` hat keinen direkten C#-Aufrufer; Middleware-Pipeline ruft per Konvention auf. DI-Konstruktoren werden von Aktivatoren erzeugt, JSON-/Dapper-Converter-Overrides vom jeweiligen Framework aufgerufen. | Typreferenz oder Registrierung erzeugt keine `ObjectCreationExpression` beziehungsweise keinen Methodenaufruf auf dem konkreten Symbol. Die Bindung muss aus Registrierung, Basistyp, Interface, Attribut oder generiertem Code belegt werden. Diese Fälle sind noch nicht durch isolierte Tests abgedeckt. |
| Generischer Mapper und private Reflection | Der neue Rot-Test enumeriert Properties mit `typeof(T).GetProperties()`: `Payload.Value` wird produktiv gelesen, ohne dass sein Name am Aufruf steht. Ein zweiter Rot-Test liest `_state` per `GetField` trotz `private`; der Scanner meldet das Feld mit `high`. | Statische Symbolreferenzen und die bloße Suche nach Membernamen in Strings können diese allgemeinen Laufzeitkanäle nicht ausschließen. |

Die betroffenen Scannerstellen sind
`src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeAdvisoryScanner.cs`:
`ClassifyReferenceRole`, `AnalyzeReferencesAsync`, `GetRelatedReferenceSymbols`,
`CheckAndRecordTypeAsync`, `ProcessMemberAsync`; ferner
`DeadCodeWhitelist.cs` und `RazorGeneratedEvidenceIndex.cs` im selben Ordner.
`RazorGeneratedEvidenceIndex` versieht Kandidaten mit einer Evidenzbewertung,
ersetzt aber keine bewiesene produktive Referenz.

## Echte Kandidaten und offene Grenzen

`XrmTermineSendTerminanfrageCommandHandler._persistence` wird nur beschrieben;
`find_references` zeigt keinen Leser. In Razor gibt es ebenfalls keinen Treffer.
Das ist nach manueller Gegenprüfung ein plausibler Kandidat, **kein
universell beweisbarer Dead-Code-Fall**: ein privater Reflection-Leser wäre
ohne Laufzeitevidenz weiter möglich. Ebenso wirken Wrapper wie
`DataTable.RefreshServerDataAsync` Kandidaten: für diese Methode gibt es
weder eine C#-Referenz noch einen `.razor`-Treffer, obwohl das interne
`DataTableBindings.RefreshServerDataAsync` benutzt wird. Die Abwesenheit
einer Referenz auf einen Wrapper darf nicht durch die Benutzung seines Ziels
ersetzt werden. Beide Beispiele gehören deshalb nicht in eine streng
beweisbasierte automatische Dead-Code-Liste.

`HandlerResponseExtensions` wird in der externen Befundnotiz als Fehlalarm
bezeichnet, weil seine Extension-Methode von Tests aufgerufen wird. Das
belegt **keine** produktive Nutzung. Der Typ kann in `closed_solution`
tatsächlich tot sein, wenn alle seine Methoden nur aus Testprojekten
aufgerufen werden. Dasselbe gilt für `ISqlQueryExecutor`-Überladungen, die
nur Tests aufrufen. Hier braucht es korrekte Klassifikation als `test_only`
und eine Symbolprüfung, keine pauschale Unterdrückung.

Öffentliche DTO-Properties, Optionen, Dapper-Spalten und dynamisch oder per
Reflection adressierte Member sind bei bloß fehlender C#-Referenz **nicht
entscheidbar**. Selbst eine niedrige Confidence macht die Aussage „Dead
Code“ nicht wahr. Umgekehrt ist ein kommentierter „Erweiterungspunkt“ ohne
heutige Nutzung nach der technischen Definition nicht automatisch lebendig;
seine Entfernung ist eine getrennte Vertrags- und Produktentscheidung.

Bei Code in einem produktiven Projekt mit Testnamen, Testordner oder Testroute
entscheidet weder der Projektstatus noch der Pfad allein. Eine belegte Kette
von einem produktiven Einstiegspunkt macht den konkreten Aufruf produktiv;
eine ausschließlich aus Test-Einstiegspunkten erreichbare Hilfsseite kann
Testcode sein. Wenn diese Trennung nicht zuverlässig gelingt, ist das Symbol
`undecidable` statt Dead Code. Für die 39 externen High-Confidence-Treffer
fehlt in dieser Analyse noch diese Erreichbarkeitsprüfung.

Die Beobachtungen sind auf den Snapshot beschränkt. Es wurde kein erneuter
vollständiger Scan des externen Repositories als Vergleichsmessung erzeugt.
