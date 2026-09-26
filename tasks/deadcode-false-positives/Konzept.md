---
status: ready
---

# Dead-Code-Advisory: verbindliche Konzeptentscheidung

## 1. Ziel und Grenzen

AiNetLinter liefert einem Entwicklungsagenten eine kleine Liste von
**Prüfkandidaten für produktiv ungenutzten Code**. Der Scanner bestätigt
weder Dead Code noch eine sichere Löschbarkeit. Erst der Agent entscheidet
nach Gegenprüfung von Nutzung, Laufzeitbindungen und Verträgen über eine
Entfernung. Advisories beeinflussen das Quality-Gate nicht.

Agentische Refactorings hinterlassen Methoden mit ausschließlich Testaufrufen,
verwaiste Wrapper, ungelesene Member und ganze Typen. Diese Kandidaten müssen
auffindbar bleiben. Maßstab ist der Nutzen pro Laufzeit und Ausgabemenge,
nicht Vollständigkeit oder eine willkürlich gesetzte Präzisionsquote.

Dieses Dokument beschreibt das **Soll-Verhalten**, nicht bereits implementierte
Funktionen. Die Entscheidungen sind verbindlich. Keine Produktausnahmen,
kein Löschen im Zielrepository, keine Erweiterung auf lokale Variablen oder
`CS0162`. Der umsetzende Agent soll die Regeln anwenden, nicht die
Grundsatzentscheidung erneut öffnen.

**Ready:** Es sind keine fachlichen Nutzerentscheidungen offen. Die
Umsetzung kann nach Beauftragung beginnen. Interne Datenstrukturen und
konkrete Optionsnamen wählt der umsetzende Agent konsistent zum Bestand.
`ready` bezeichnet ausschließlich das Konzept, nicht eine fertige Umsetzung.

## 2. Evidenz und Aussagekraft

Vor der Umsetzung lesen:

- `tasks/deadcode-false-positives/Analyse.md`.
- `C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\temp\ainetlinter-deadcode\`:
  `README.md`, `befund.md`, `roslyn-hinweise.md` und `laufzeit/`.
- `C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\temp\deadcode-entfernen\sicher-entfernen.md`.

Der Snapshot enthält 714 Kandidatenzeilen, darunter 403 `test_only` und
311 `unreferenced`, mit Duplikaten. Die Entfern-Liste ist eine manuell
gegengeprüfte Arbeitsliste, kein Nachweis bereits erfolgter Löschung mit
anschließend bestandenem Build und Tests. Ihr Ausschluss von `test_only`
ist keine Scanneranforderung. Die pauschale Bewertung dieser Treffer als
Fehlalarme in den Befundnotizen wird ausdrücklich nicht übernommen.

Die Dateien unter `laufzeit/` enthalten Scan-Ausgaben, keine isolierte
Messung der Dead-Code-Laufzeit. Die unten genannten Budgets sind
Produktentscheidungen, keine gemessenen Leistungszusagen.

Zusätzliche MCP-Gegenprüfung am 2026-09-26 belegt reale dynamische Kanäle:

- `SiteHandlerAssemblyScanner.RegisterHandlers` wird aus der produktiven
  Registrierung aufgerufen. Er enumeriert übergebene Assemblies mit
  `GetTypes()`, filtert nach Basistyp, Interface beziehungsweise Attribut
  und registriert oder aktiviert passende Typen.
- `SqlOptimisticContribMutationModel` enumeriert öffentliche Instance-Properties
  und wertet Attribute aus. Produktive SQL-Update-/Delete-Pfade verwenden
  das Modell. Dynamische Property-Nutzung ist hier tatsächlich vorhanden.

Diese Namen dienen nur als Gegenbeispiele, niemals als Literale einer
Whitelist. Die synthetischen Reflection-Fälle aus `Analyse.md` zeigen
Möglichkeiten, aber keine allgemeine Häufigkeit in Repositories.
`Entscheidungen-und-Tests.md` enthält einen verworfenen Zwischenstand;
bei widersprüchlichen Empfehlungen gilt dieses Konzept.

## 3. Entscheidung pro Symbol

| Ergebnis | Bedingung | Ausgabe |
| --- | --- | --- |
| Unterdrückt: Nutzung | Produktive statische, compilererzeugte oder konkret zugeordnete indirekte Nutzung ist vorhanden. | Keine Kandidatenzeile. |
| Unterdrückt: API-Schutz | Symbol gehört zur extern nutzbaren API eines `external_library`-Projekts. | Keine Kandidatenzeile; behauptet keine tatsächliche Nutzung. |
| `undecidable` | Vorhandener Nutzungskanal könnte konkret dieses Symbol betreffen, lässt sich aber nicht zuordnen; oder notwendige Referenzrolle/Abdeckung ist unbekannt. | Standardmäßig Anzahl und gruppierter Grund; Details auf Abruf. |
| Prüfkandidat | Keine relevante produktive Nutzung gefunden, kein API-Schutz und keine konkret einschlägige ungeklärte Bindung. | Symbol, Ort, Nutzungsgrund und gezielter Gegenprüfhinweis. |

Reihenfolge: Scan-Umfang bestimmen, API-Schutz prüfen, Nutzung einschließlich
indirekter Bindungen prüfen, konkrete Unklarheiten prüfen, erst dann einen
Kandidaten bilden. Wegen Zeitablauf nicht fertig analysierte Symbole sind
**offene Scanarbeit**, keine Kandidaten und keine fachlich geprüften
`undecidable`-Fälle.

Bei Feldern und Properties unterdrückt eine gewöhnliche statische Zuweisung
allein die Meldung nicht: Hier sind Leser beziehungsweise eine konkrete
indirekte Vertragsbindung maßgeblich. Bei Methoden zählen Aufrufe und andere
Bindungen wie Methodengruppen; reine Implementierungsdeklarationen nicht.

Ein bloßes „Reflection wäre denkbar“ erzeugt weder Unterdrückung noch
`undecidable`. Die Abwesenheit beliebiger dynamischer Nutzung muss nicht
bewiesen werden. Umgekehrt machen `private` und null Roslyn-Referenzen ein
Symbol nicht sicher löschbar. Confidence-Werte wie `high` entfallen im
Advisory; Prüfpriorität und Sicherheit dürfen nicht vermischt werden.

Die Nutzungsangaben bedeuten:

- `unreferenced`: keine relevante statische Referenz gefunden.
- `test_only`: relevante statische Nutzung ausschließlich durch echte Tests;
  ein Kandidatensignal, kein pauschaler Fehlalarm.
- `no_production_read`: Feld oder Property hat keinen produktiven Leser.
  Schreibstellen und Testleser getrennt angeben; „nur geschrieben“ nicht
  als „keine Referenzen“ beschreiben.

Referenzlage und fehlende Lesung sind getrennte Merkmale: Ein Feld kann
`test_only` und zugleich `no_production_read` sein. Im Ausgabegrund beide
Informationen erhalten, aber weiterhin nur eine Kandidatenzeile ausgeben.

Kommentare und gleiche Literalwerte sind keine Symbolnutzung. Namenbasierte
Verweise benötigen passenden Kontext; ein beliebiger gleichlautender String
schützt nicht alle gleichnamigen Symbole.

## 4. Symbolumfang und Gruppierung

**Eigenständige Kandidaten:** ganze Klassen, Records, Structs, Interfaces,
Enums und Delegates; gewöhnliche Methoden einschließlich Wrapper,
Überladungen und Extension-Methoden; Konstanten, Felder und Properties.
Sichtbarkeit ist in `closed_solution` kein Ausschlussgrund.

Kandidatendeklarationen stammen aus produktiven Projekten. Echte Testprojekte
liefern Nutzungsbelege, aber keine zu bereinigenden Testmethoden-Kandidaten.
Produktionscode mit ausschließlich Testnutzung bleibt ausdrücklich im Scope.

**Keine eigenständigen Kandidaten in diesem Umfang:** Konstruktoren,
Accessoren, Operatoren, Finalizer, einzelne Enum-Werte, Events, Indexer und
generierte Deklarationen. Damit gehen unter anderem isoliert überzählige
Konstruktoren und tote Enum-Werte verloren. Dafür entfallen die beobachtete
Konstruktor-Flut und zusätzliche Konventionsanalyse. Ganze verwaiste Typen
bleiben sichtbar. Ausgeschlossene Arten gelten nicht als nachgewiesen
benutzt; die Ausgabe muss den gewählten Symbolumfang nennen.

Der Ausschluss betrifft nur eigene Kandidatenzeilen. Nutzungen aus diesen
Membern und ihre Bindungen zählen weiterhin für andere Symbole und den
Eltern-Typ; das gilt insbesondere für generierten Code und Operatoren.

Für Typen zählt auch die Nutzung ihrer Member, einschließlich reduzierter
Extension-Aufrufe. Testnutzung eines Members macht den Typ mindestens
testbenutzt. Interne Aufrufe innerhalb eines ansonsten verwaisten Typs
halten diesen nicht allein am Leben. Compiler- und Framework-Einstiege
schützen den zugehörigen Typ.

Eine lebende Zielmethode schützt keinen unaufgerufenen Wrapper. Eine
Implementierungsdeklaration ist kein Aufruf eines Interface-Members.
Eigene Interface-Member und Implementierungen ohne Nutzung dürfen als
gemeinsamer Vertrags-Kandidat erscheinen.

Pro Symbol nur eine Meldung. Ein verwaister Typ wird mit seinen enthaltenen
Membern gruppiert. Bekannte abhängige Member dürfen unter einem Wurzelkandidaten
zusammengefasst werden; die vollständige Entfern-Gruppe ermittelt der Agent.
Keine globale Erreichbarkeitsanalyse als Voraussetzung für bereits nützliche
Wurzelkandidaten verlangen.

## 5. Indirekte Nutzung: konkrete Regeln

| Kanal | Unterdrücken, wenn ... | Unentscheidbar, wenn ... | Nicht pauschal schützen |
| --- | --- | --- | --- |
| Compiler-Einstieg | Einstiegspunkt oder `[ModuleInitializer]` erkannt ist; auch Eltern-Typ schützen. | benötigte Compilerinformationen fehlen. | alle statischen Typen |
| Virtueller Dispatch | produktiver Aufruf des Vertrags oder produktive Übergabe/Registrierung beim Framework plus passender Hook belegt ist. | Typ konkret an einen nicht auflösbaren Dispatch-Kanal übergeben wird. | alle Overrides oder Interface-Implementierungen |
| Konventionen | Registrierung und Signatur passen, etwa `UseMiddleware<T>` plus `InvokeAsync(HttpContext, ...)`. | vorhandene Registrierung nicht zugeordnet werden kann. | Methoden allein wegen ihres Namens |
| DI/Aktivator | konkrete Registrierung/Aktivierung den Typ betrifft; nur gebundene Einstiegsmember schützen. | Typmenge oder Auswahl vorhandener Aktivierung ungeklärt ist. | sämtliche Methoden registrierter Typen |
| `GetTypes()` | Assemblymenge, Filter und anschließende Registrierung/Aktivierung einen Typ erfassen. | vorhandene Kette nicht auflösbar ist; anhand bekannter Assemblies/Filter verbleibende Menge markieren. | ganze Solution wegen eines `GetTypes()`-Vorkommens |
| Member-Reflection | Empfängertyp, Name/Filter, BindingFlags und Operation das Member erfassen. | vorhandener Zugriff etwa einen dynamischen Namen verwendet; passende Member möglicher Empfängertypen betreffen. | private Felder wegen theoretischer Reflection |
| Mapper/Serializer/Binder | produktiver Datenvertrag und geltende Konvention/Attribute das Member erfassen. Auch vertragliche Schreibbindung schützt vor einem Dead-Code-Advisory. | konkreter Datenkanal vorhanden, dessen Membermenge aber ungeklärt ist. | alle öffentlichen Properties, Records oder DTO-ähnlichen Typen |
| Razor/XAML/JS/Konfiguration | generierter Code oder aufgelöste Typ-, Member-, Event-, Binding- oder Attached-Property-Verweise vorliegen. | konkret passende Bindung mangels Typ-/Kontextauflösung offen bleibt. | ganze Komponenten, ViewModels oder Projekte |

Framework-APIs und Attribute semantisch identifizieren; kurze Namen allein
sind kein belastbarer Match. Eigene Attribute werden durch ihren vorhandenen
Leser relevant, nicht durch eine Produkt-Whitelist. Eine Paketreferenz,
ein Attribut oder `IOptions<T>` allein beweist keine konkrete produktive
Membernutzung. Bei mehreren nicht unterscheidbaren DI-Konstruktoren keine
bestimmte Auswahl behaupten; das rechtfertigt keinen Schutz normaler Methoden.

Vorhandenen Generatoroutput als Referenzquelle berücksichtigen, auch wenn
generierte Deklarationen keine Kandidaten sind. Markup und relevante
namensbasierte Verweise einmal je Snapshot begrenzt indexieren. Fehlender
Razor-Generatoroutput allein sperrt nicht alle Komponentenmethoden: Ohne
Treffer in der begrenzten Markup-/JS-Prüfung und ohne konkret offenen Kanal
bleibt eine gewöhnliche Methode prüfbar. Wurden die dafür benötigten Dateien
gar nicht geprüft, ist die Abdeckung dagegen offen.

Bei Records Parameter und Property korrekt zuordnen, aber Schreib- und
Lesenutzung nicht blind vereinigen. Ein Konstruktorargument ist keine
Property-Lesung. Benutzte Gleichheit, Hashing, Deconstruction oder Ausgabe
können implizite Membernutzung begründen, auch für nichtpositionale
Auto-Properties. Die bloße Existenz synthetisierter Methoden schützt nicht
alle Properties. Bei konkret ungeklärter Verwendung gilt `undecidable`.

## 6. API- und Testrollen

- `closed_solution`: öffentliche Symbole bleiben prüfbar; ausschließlich
  Testnutzung bleibt Kandidatensignal. Fehlende externe Consumer werden
  durch den Geltungsbereich angenommen, nicht bewiesen.
- `external_library`: effektiv extern zugängliche API einschließlich
  geschützter Erweiterungspunkte und Implementierungen extern nutzbarer
  Verträge schützen. `public` auf einem nicht zugänglichen Typ reicht nicht.
  Nichtöffentliche Details bleiben prüfbar. Bekannte Friend-Assembly-Grenzen
  berücksichtigen; fehlende relevante Consumer nicht als gescannte
  Referenzabwesenheit behandeln.
- Projekt-Overrides bleiben möglich. Globaler Default bleibt
  `closed_solution`; keine dritte API-Surface einführen.
- Testrollen aus verlässlichen Projekteigenschaften oder expliziter
  Rollenkonfiguration ableiten. Datei-, Ordner- und Routennamen wie `Test`
  reichen nicht. Host-Referenzen zählen standardmäßig als produktive
  statische Referenzen; das behauptet keine Laufzeiterreichbarkeit.
  Tatsächlich gemischte, nicht trennbare Rollen sind `undecidable`.
- Indirekte Bindungen übernehmen die Produktions-/Testrolle ihres Kanals.
  Reflection in Tests ist keine Produktionsnutzung.
- Ein kommentierter Erweiterungspunkt ist ein Hinweis zur Vertragsprüfung
  des Agenten, keine automatische produktive Nutzung.

## 7. Verbindliche Gegenbeispiele

Die Zuordnung gilt nach der vorliegenden Gegenprüfung. Neue konkrete
Gegenbelege dürfen einen Einzelfall ändern; Symbolnamen ersetzen keine
Regeln. „Erhalten“ bedeutet Prüfkandidat, nicht bestätigte Löschbarkeit.

| Fall | Erwartetes Ergebnis und Grund |
| --- | --- |
| `SqlDateTimeMapping` mit `ToUtcDateTime` | Typ-Kandidat erhalten, als eine Gruppe. |
| Vier `RemoveSite`-Fassaden, `SiteSqlRelativePath.TryValidate` | Wrapper erhalten; lebendes Ziel schützt die Hülle nicht. |
| `DataTable.RefreshServerDataAsync`, `Scheduler.ExecuteUndoAsync` | Öffentliche Methoden erhalten; dokumentierte C#-/Razor-/JS-Prüfung findet keine Bindung. |
| `BuildForSchedulerQueryAsync` | Wurzelkandidat erhalten; private Hülle und Request-Record als Gruppe durch Agenten erschließbar. |
| `_persistence`, `FirstSeenUtc`, `LastSeenUtc`, `PageRoute`, `CircuitBinding.Username`/`AttachedUtc` | Fehlende produktive Lesung bleibt Kandidatensignal; konkrete Record-/Datenbindung beachten. |
| `SqlMigrationJournal.TableName` | Konstante erhalten; gleiche SQL-Literale benutzen nicht dieses Symbol. |
| `HandlerResponseExtensions.ToApiResponse`, testbenutzte `ISqlQueryExecutor`-Überladungen | `test_only` erhalten; Extension-Typ nicht als völlig unreferenziert melden. |
| `ISqlTransactionContext.QueryAsync` und Implementierung | Vertrags-Kandidaten erhalten; Implementierungen sind keine Aufrufe. |
| `MainLayout.OnInitialized`, registrierte Converter-/Options-Hooks, Middleware-`InvokeAsync` | Bei zugeordneter Frameworkbindung unterdrücken. |
| `SchedulerTerminDetailsBridgeModule` | Methode und Typ durch Modulinitialisierung schützen. |
| `WizardText.Back`, XAML-Code-Behind und Attached Properties | Aufgelöste Markupbindung schützt betroffene Symbole. |
| `RangeIdentity.*`, `DebounceKey.ConfigKey` | Dokumentierte Schlüsselverwendung begründet implizite Lesung. |
| Dapper-Spalten/Rowversion und tatsächlich serialisierte API-Properties | Konkreter dynamischer Datenvertrag schützt; unaufgelöster Vertrag ist unentscheidbar. |
| Host-Member unter `Components/Pages/Test` | Nicht allein durch Pfad als `test_only` behandeln. Die 39 High-Treffer sind keine 39 nachgewiesen unerreichbaren Symbole. |

Pauschale Ausschlüsse aller öffentlichen Member, Records, Interfaces,
Komponenten oder Typen bei Reflection sind untersagt: Sie verlieren mehrere
der oben aufgeführten positiven Fälle.

## 8. Scan-Scope, Aufwand und Ausgabe

- `changes` untersucht geänderte Deklarationen **und bisherige Ziele
  entfernter oder umgebogener Nutzungen**, auch in unveränderten Dateien.
  Entfernte Registrierungs-/Markupbindungen berücksichtigen. Fehlt die
  Vergleichsbasis, eingeschränkte Abdeckung ausdrücklich ausweisen.
- `solution` untersucht zusätzlich ältere Kandidaten im gewählten
  Symbolumfang. Keine Zusage vollständiger Dead-Code-Erkennung.
- Beide Scopes prüfen Referenzen solutionweit einschließlich Testrollen
  und relevanter generierter Quellen. Kleiner Deklarationsscope erlaubt
  keine entsprechend kleine Referenzsuche.
- Nutzungs-/Bindungsinformationen je Snapshot wiederverwenden. Kein voller
  `FindReferencesAsync`-Lauf für jedes Member. Teure Vertiefung nur für
  vorselektierte Kandidaten. Nach Änderungen keine veralteten Negativbefunde
  aus dem Cache übernehmen.
- Keine globale Erreichbarkeits- oder beliebig tiefe dynamische
  Datenflussanalyse. Unaufgelöste konkrete Grenzen sichtbar machen.
- Defaultbudget: **10 Sekunden zusätzlicher Advisory-Aufwand im normalen
  Verify**, **60 Sekunden beim ausdrücklich angeforderten Solution-Advisory**.
  Notwendige Advisory-Vorbereitung zählt mit; bereits geladene Daten
  wiederverwenden. Explizites `verify(scope: solution)` verwendet das
  Solution-Budget, ebenso ein eigenständig angeforderter vollständiger
  Advisory-Scan über `get_verify_advisories`. Folgeseiten lesen ausschließlich
  den vorhandenen Snapshot. Budgets zentral konfigurierbar machen; sie begrenzen
  nicht die übrige Gate-Laufzeit.
- Budgetablauf beendet die Advisory-Arbeit und liefert einen partiellen
  Stand; kein unbegrenzter Hintergrundscan. Pagination ist keine
  Laufzeitbegrenzung.
- Standardausgabe: maximal **20 eindeutige Kandidatengruppen und 8 KiB**,
  nur vollständige Einträge. Weitere Ergebnisse desselben Snapshots auf
  Abruf ohne erneuten Scan. Zuerst nachweislich neu verwaiste Symbole,
  danach ganze Typen/Gruppen und kleine Wrapper; `test_only` nicht pauschal
  nachrangig behandeln. Gleiche Priorität deterministisch ordnen.
- Pro Kandidat: direkt weiterverwendbares `symbolIdentifier`, Ort,
  Nutzungsgrund und konkreter Gegenprüfhinweis. Wiederholte Scope-/Pfadangaben
  gruppieren. Content-only-MCP-Vertrag beibehalten.
- **Scanabdeckung und Ausgabekürzung getrennt ausweisen:** angeforderter und
  bearbeiteter Scope, ausgeschlossene Symbolarten, bearbeitete/offene Arbeit,
  Zeitverbrauch/Abbruchgrund sowie gezeigte/zurückgehaltene Kandidaten.
  Unbekannte Gesamtmengen als unbekannt ausweisen. Die letzte Ausgabeseite
  macht einen partiellen Scan nicht vollständig. Null Treffer bei partiellem
  Scan sind keine Entwarnung.

## 9. Dokumentation ist verpflichtender Teil der Umsetzung

Der umsetzende Agent muss die Dokumentation mit dem **tatsächlich
implementierten Verhalten** synchronisieren. Keine Soll-Funktion vorzeitig
als verfügbar beschreiben. Erforderlich sind:

- `Docs/linter/configuration.md`: API-Surface, Projekt-/Testrollen,
  Zeit-/Ausgabebudgets und relevante Konfiguration einschließlich Defaults.
- `Docs/linter/cli.md` und betroffene MCP-Beschreibungen unter `Docs/`:
  Scan-Scope, Kandidatenstatus, Gegenprüfung, Fortsetzung und klare Trennung
  zwischen partiellem Scan und gekürzter Ausgabe.
- `ainetlinter-rules.json`: tatsächliche Optionen/Defaults synchron halten;
  zugehörige Schema-/Regelbeschreibungen bei Änderungen ebenfalls anpassen.
- Betroffene Agentenregeln, insbesondere
  `.agents/rules/AiNetLinter-McpWorkflow.mdc`: neue Advisory-Semantik und
  korrekte Interpretation der Vollständigkeitsmarker übernehmen.

**Für jede Unterdrückungsregel, Unentscheidbarkeitsregel und Einschränkung
des Scan-Umfangs muss die Doku knapp beantworten:**

1. Welches konkrete Nutzungssignal löst die Regel aus?
2. Welchen Fehlalarm verhindert sie, und warum reicht die reine
   C#-Referenzsuche hier nicht?
3. Welche positiven Kandidaten bleiben erhalten beziehungsweise welche
   Funde gehen durch den bewusst eingeschränkten Umfang verloren?

Beispiel für die erforderliche Erklärung: „Registrierte Middleware wird
über `InvokeAsync` per Frameworkkonvention aufgerufen; deshalb schützt die
Registrierung diesen Einstieg trotz fehlendem C#-Aufrufer. Eine gewöhnliche
Methode gleichen Namens ohne Registrierung bleibt prüfbar.“ Allgemeine
Sätze wie „Frameworks berücksichtigen“ oder „False Positives vermeiden“
allein reichen nicht.

Auch erklären: `test_only` ist ein nützlicher Prüfkandidat, `private` kein
Löschbeweis, Reflection sperrt nicht automatisch die ganze Solution und
eine Ausgabelimitierung spart keine Scanzeit.

## 10. Produktunabhängige Verhaltensnachweise

Diese Anforderungen gelten für die spätere Umsetzung; in der Konzeptphase
werden keine Tests implementiert oder ausgeführt.

- Für jede zu behebende False-Positive-Ursache zuerst einen isolierten
  xUnit-v3-Reproduktionstest mit erwarteter korrekter Einstufung schreiben
  und sein Scheitern am bisherigen Verhalten nachweisen. Bereits vorhandene
  passende Reproduktionstests wiederverwenden. Erst danach den Fehler beheben.
  Ist ein Fall bereits korrekt, als grünen Schutztest behalten; keinen
  künstlichen Rot-Nachweis erzeugen.
- Zusätzlich positive Erkennung absichern: tatsächlich verwaiste Symbole
  müssen weiterhin Prüfkandidaten sein. Ein Test darf nicht bereits deshalb
  bestehen, weil gar keine Kandidaten mehr ausgegeben werden.
- Kleine eigenständige Beispiel-/Dummy-Solutions beziehungsweise
  Roslyn-Fixtures in der vorhandenen Testinfrastruktur erzeugen. Keine
  Planner-/SAN-Projekte, Produktnamen, absoluten Entwicklerpfade oder
  externe Dienste als Testabhängigkeit. Temporäre Dateien ausschließlich
  über `AiNetLinter.TestKit.TestTempDirectory` anlegen.
- Bei frameworkabhängigen Regeln neutrale Beispieltypen gegen die passende
  tatsächliche Framework-API testen. Bei Metadaten-Dispatch muss die Basis
  tatsächlich aus einer Metadatenreferenz kommen. Ein selbst erfundenes
  gleichnamiges Attribut darf eine semantische Frameworkbindung nicht ersetzen.
- Konkrete Ergebnisse prüfen: Kandidat, Nutzungsunterdrückung, API-Schutz,
  `undecidable` oder offene Scanarbeit. Ein leerer Output allein belegt weder
  korrekte Unterdrückung noch korrekte Unsicherheitsbehandlung.

Die Fallgruppen aus Abschnitt 7 werden in folgende neutrale Szenarien
übersetzt. Je Regel die passende Gegenprobe im gleichen Nutzungskontext
prüfen, damit eine pauschale Ausschlussregel den Test nicht erfüllen kann:

| Zu verhindernder Fehlalarm / Grenze | Positive Erkennung beziehungsweise Gegenprobe |
| --- | --- |
| Host-Aufruf aus Ordner `Features/Test` | Aufruf nur aus echtem Testprojekt bleibt `test_only`; Testdeklaration selbst ist kein Kandidat. |
| Typnutzung über Extension-Methode oder Compiler-Einstieg | Verwaister Hilfstyp bleibt Kandidat; testbenutzter Extension-Typ erhält korrekte Rolle; Typ/Member nicht doppelt ausgeben. |
| Framework-Hook, Metadaten-Override, DI- oder Middlewarebindung | Gewöhnliche unbenutzte Methode auf demselben Typ bleibt Kandidat; eigener unbenutzter Interface-Vertrag bleibt prüfbar. |
| Gefilterter Assembly-Scan mit Registrierung/Aktivierung | Nicht passender verwaister Typ bleibt Kandidat; nicht auflösbarer konkreter Filter ergibt begrenztes `undecidable`. |
| Produktiver Zugriff per `GetField` oder `GetProperties` | Nicht betroffener nur geschriebener Member bleibt Kandidat; Reflection nur aus Tests schützt nicht als Produktionsnutzung. |
| Serializer-/Mapper-/Binder-Vertrag | Ungebundene ungelesene Property bleibt Kandidat; gleichnamiges fremdes Attribut schützt nicht pauschal. |
| Markup-/Generatorbindung, einschließlich Attached Property | Öffentliche Methode ohne Bindung bleibt Kandidat; konkretes unauflösbares Binding ist unentscheidbar. |
| Implizite Record-Lesung über benutzte Gleichheit/Hashing | Nur geschriebene Komponente ohne solchen Nutzungskanal bleibt Kandidat; Konstruktorargument zählt nicht als Leser. |
| Externe API und zugängliche Erweiterungspunkte | Dasselbe öffentliche Symbol in `closed_solution` bleibt prüfbar; nicht zugängliches Implementierungsdetail ist nicht pauschal geschützt. |
| Lebendes Delegationsziel oder gleicher Literalwert | Unaufgerufener Wrapper, ungenutzte Überladung und Konstante bleiben Kandidaten. |
| Entfernte letzte Nutzung bei unveränderter Deklarationsdatei | `changes` findet das bisherige Ziel; fehlende Vergleichsbasis wird als Abdeckungslücke sichtbar. |
| Budgetablauf, Ausgabelimit und Snapshot-Fortsetzung | Partielle Analyse bleibt partiell, ungeprüfte Symbole sind keine Kandidaten, letzte Ausgabeseite erzeugt keine vollständige Scanbehauptung. |

Budget-/Abbruchverhalten deterministisch prüfen, ohne fragile Tests auf
zufällige reale Maschinenlaufzeit. Die tatsächliche Laufzeitmessung bleibt
ein separater Nachweis. Testebenen, Ausführung und Gates richten sich
ausschließlich nach den unten genannten Projektregeln; die Matrix verlangt
keinen separaten Serverstart für jede fachliche Variante.

## 11. Abschlusskriterien

Die Umsetzung erfüllt dieses Konzept nur, wenn positive Gegenbeispiele als
Kandidaten beziehungsweise Gruppen auffindbar bleiben, belegte Bindungen
nicht als Kandidaten erscheinen und konkret unaufgelöste Grenzen separat
sichtbar werden. Die Entfern-Liste bleibt eine Gegenprobe, keine Löschfreigabe.
Laufzeit und Abdeckung sind gegen den bisherigen Zustand nachvollziehbar
auszuweisen. Präzisionsangaben benötigen eine belegte Stichprobe; es gibt
keine vorgegebene Zielquote.

Die Verhaltensnachweise aus Abschnitt 10 und die erklärende
Dokumentationssynchronisation aus Abschnitt 9 gehören zur vollständigen
Umsetzung. Reine Listenverkürzung, pauschale Unterdrückung oder ausschließlich
am Produktrepository geprüfte Sonderfälle erfüllen das Konzept nicht.

Für Entwicklungs- und Prüfabläufe gelten ausschließlich
`.agents/rules/AiNetLinter-Richtlinien.mdc` und
`.agents/rules/AiNetLinter-TestRichtlinien.mdc`. Dieses Konzept führt keinen
abweichenden Gate-Ablauf ein. In der aktuellen Konzeptphase werden nur
Dokumente geändert, kein Scanner- oder Testcode.
