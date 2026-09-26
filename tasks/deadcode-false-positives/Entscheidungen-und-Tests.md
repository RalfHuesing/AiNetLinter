# Historischer Zwischenstand: Dead-Code-Ausgabe stark begrenzen

Die Empfehlung dieses Zwischenstands, Typen und Member ganz aus der
Dead-Code-Ausgabe zu entfernen, wurde nach der Diskussion über agentische
Refactorings verworfen. Maßgeblich für die laufende Konzeptplanung ist
`Konzept.md` (`status: draft`). Die Gegenbeispiele und Rot-Tests hier
bleiben als Analysebelege erhalten; die früheren Produktentscheidungen
unten sind keine Umsetzungsfreigabe.

## Warum die bisherige Strategie nicht zuverlässig werden kann

Für ein beliebiges C#-Repository ist „`SymbolFinder` findet keine
Referenz“ nur eine Aussage über indexierte, statisch gebundene C#-Syntax.
Ein produktiver Aufruf kann über `dynamic`, Reflection, einen generischen
Mapper (`typeof(T).GetProperties()`), Serializer, ORM, DI-Container,
Framework-Konvention, XAML/Razor, Konfiguration, generierten Code oder eine
andere Assembly erfolgen. Auch **private Felder** können per Reflection
gelesen werden. `confidence=high` auf einem privaten Symbol ist deshalb
kein Dead-Code-Beweis. Die sechs isolierten Rot-Tests unten enthalten zwei
Reflection-Fälle ohne jede direkte Symbolreferenz.

Diese Kanäle vollständig und herstellerunabhängig negativ zu beweisen, ist
mit Roslyn-Quellreferenzen nicht möglich. Ein Katalog bekannter Frameworks
würde nur einzelne Fälle heilen und neue dynamische Mapper weiter übersehen.
Für das gewünschte Präzisionsziel muss die Menge der Meldungen kleiner
werden, auch wenn dadurch wirklich toter Member-Code unerkannt bleibt.

## Prüfung der vorgeschlagenen Typ-/Member-Grenze

Die Beschränkung auf Klassen, Interfaces, Records und deren Funktionen
verkleinert die Ergebnisliste, erhöht aber **nicht** die Beweiskraft eines
fehlenden Roslyn-Aufrufs. Der zusätzliche Rot-Test `ReflectionDiscoveredClass`
enthält einen produktiven `Program.Main`, der alle Assembly-Typen enumeriert,
konkrete Implementierungen von `IPlugin` per `Activator.CreateInstance`
erzeugt und ausführt. `HiddenPlugin` wird trotzdem als tote Klasse gemeldet.
Sein Typname kommt nur in seiner Deklaration vor. Dasselbe Grundproblem gilt
für per Namen oder Attribut gefundene Interfaces, Records, Structs, Enums
und Delegates sowie deren Methoden, Konstruktoren und Properties.

Der öffentliche API-Fall ist anders: Bei `external_library` ist ein
öffentliches Symbol wegen möglicher externer Consumer **kein sicherer
Fund**. Das ist eine richtige Ausschlussregel. Bei `closed_solution` dürfen
öffentliche Symbole grundsätzlich geprüft werden, aber „closed“ schließt
Reflection, `dynamic`, Mapper oder Konfiguration **innerhalb** der Solution
nicht aus. Auch `private` ist kein Sicherheitsbeweis, wie der `GetField`-Test
zeigt. Eine universelle Regel „nur Typen und ihre Member“ kann daher unter
dem Präzisionsziel null solche Dead-Code-Meldungen liefern. Wenn die
Produktoberfläche genau auf diese Symbole begrenzt bleiben soll, ist eine
leere Dead-Code-Liste ehrlicher als unsichere Kandidaten. Ein optionales
Werkzeug für **fehlende statische Referenzen** kann die Hinweise weiterhin
bereitstellen, mit einem anderen Namen und Vertrag.

## Verbindliche Produktentscheidung für die nächste Implementierung

1. **Keine symbolweiten Dead-Code-Advisories aus bloß fehlenden Referenzen.**
   Standard-`verify` und `get_verify_advisories(category=dead_code)` sollen
   Klassen, Konstruktoren, Methoden, Properties, Felder, Events und
   Record-Komponenten nicht als Dead Code melden, wenn die einzige Evidenz
   eine leere statische Referenzliste ist. Das gilt auch für `test_only`,
   `confidence=low` und `confidence=high`.
2. **Auch bisherige Compiler-/IDE-Diagnosen zu Membern nicht ungeprüft
   übernehmen.** Der aktuelle Diagnosepfad verwendet `CS0169`, `CS0414`,
   `IDE0051` und `IDE0052`. Diese sagen ebenfalls nichts Sicheres über
   Reflection, Mapper oder Laufzeitaktivierung. Das konkrete Feld
   `_persistence` ist wahrscheinlich tatsächlich unbenutzt, aber die
   Diagnoseklasse insgesamt ist nicht zuverlässig genug für eine
   universelle Dead-Code-Meldung.
3. **Nur positive, sprachsemantische Beweise in einer vollständigen,
   fehlerfrei gebauten Produktions-Compilation ausgeben.** Als erster
   geeigneter Kandidat gilt `CS0162` (nicht erreichbarer Anweisungsbereich)
   mit präziser Stelle und Konfigurationsbezug. Ein dynamischer Mapper kann
   einen nach C#-Kontrollfluss unerreichbaren Block nicht ausführen. Der
   heutige Scanner gibt `CS0162` noch nicht aus; die Umsetzung braucht einen
   eigenen Rot-Test und eine eng begrenzte Implementierung. Ein Verweis auf
   die analysierte Target-Framework-/Präprozessor-Konfiguration gehört zur
   Evidenz, wenn mehrere Compilations existieren.
4. **Ungenutzte lokale Bindungen getrennt prüfen.** `CS0168` und `CS0219`
   können „lokaler Wert wird nicht gelesen“ belegen. Die ganze Anweisung
   kann trotzdem einen Effekt haben; diese Diagnosen dürfen nicht als
   pauschale Löschanweisung oder als toter Member erscheinen. Ob sie unter
   `dead_code` oder einer eigenen Kategorie erscheinen, wird erst anhand
   eines expliziten Vertrags und Rot-Tests entschieden.
5. **Statische Referenzlücken können als separates Analysewerkzeug bestehen.**
   Falls agentische Nutzer solche Hinweise brauchen, heißen sie ausdrücklich
   „keine statische C#-Referenz gefunden“, nicht „Dead Code“. Sie dürfen
   weder den Dead-Code-Zähler noch eine vermeintliche Löschliste speisen.

`closed_solution` behält die Definition „keine produktive Nutzung, auch bei
public“; `external_library` schützt öffentliche API vor internen
Negativschlüssen. Diese Definition zwingt nicht dazu, alles Erfüllende zu
finden. Bei unbekannten Nutzungskanälen bleibt ein Symbol **unentschieden**
und wird nicht als Dead Code gemeldet. Aufrufe aus echten Testprojekten sind
keine produktive Nutzung, beweisen aber ebenso wenig die Abwesenheit von
Mappern und anderen Laufzeitnutzern. Ein Dateiname oder Pfadsegment `Test`
entscheidet die Rolle eines Aufrufs nicht allein.

## Einordnung der beobachteten Klassen

| Klasse | Sichere Entscheidung jetzt |
| --- | --- |
| Blazor-/Dapper-/JSON-Overrides, DI-Konstruktoren, Middleware | Keine Dead-Code-Meldung aus fehlender Quellreferenz. Framework-Dispatch kann sie verwenden. |
| Generische/dynamische Mapper, Reflection auf öffentliche **oder private** Member | Keine Dead-Code-Meldung aus fehlender Quellreferenz oder Member-Diagnose. Namenslose `GetProperties()`-Mapper sind nicht durch Stringsuche lösbar. |
| XAML/Razor, Optionen, serialisierte DTOs, Record-Gleichheit, ModuleInitializer | Keine Dead-Code-Meldung aus fehlender Quellreferenz. Der generierte oder indirekte Nutzungskanal zählt. |
| `Components/Pages/Test` im Hostprojekt | Weder pauschal Produktion noch pauschal Test. Ohne belegte Einstiegskette keine Entscheidung. |
| Nur aus Testprojekten aufgerufene Überladung | Für `closed_solution` produktiv ungenutzt, aber wegen offener dynamischer Nutzung trotzdem kein sicherer Dead-Code-Befund. |
| `_persistence`, unbenutzte Wrapper und Konstanten | In der Gegenprüfung plausibel tot; daraus folgt keine allgemeine sichere Melderegel. Eine manuelle Prüfung kann sie weiterhin finden. |

Die bisherige Idee, nacheinander DI, XAML, Serializer und weitere Frameworks
in eine Negativsuche einzubauen, wird **nicht** als Voraussetzung für eine
universelle Dead-Code-Liste empfohlen. Solche Analysen sind nützlich für
positive Nutzungskanten und für ein separates Referenzanalysewerkzeug.

## Rot-Tests des aktuellen Schritts

`src/AiNetLinter.FastTests/Mcp/Tools/DeadCode/DeadCodeFalsePositiveRegressionTests.cs`
enthält kleine Roslyn-Solutions ohne Abhängigkeit vom fremden Repository.
Die Tests belegen sieben Fehlmeldungen und eine fehlende Meldung für den
engen, beweisbaren Bereich:

| Test | Erwartung und aktueller Fehler |
| --- | --- |
| `ProductionEntryPointUsesMemberUnderTestPath_IsProductionUse` | `Program.Main` ruft Code unter `Features/Test` auf; das Feld wird dort gelesen. |
| `MetadataOverrideUsedByFramework_IsNotDead` | `Stream.CopyTo` ruft das konkrete `Read`-Override. |
| `ModuleInitializer_IsAProductionRootForItsType` | Compiler/Runtime startet die attributierte Methode und damit den Typ. |
| `RecordKeyEquality_UsesPositionalProperty` | `Dictionary<Key, int>` nutzt `Equals`/`GetHashCode` des Records. |
| `GenericReflectionMapper_UsesPropertiesWithoutSymbolReferences` | Generischer Mapper enumeriert Properties mit `GetProperties()`; kein Membername steht am Aufruf. |
| `PrivateFieldReadThroughReflection_IsNotDead` | `GetField` liest ein privates Feld; der Scanner meldet es sogar mit `confidence=high`. |
| `ReflectionDiscoveredClass_IsNotDead` | `Program.Main` enumeriert Assembly-Typen und aktiviert `HiddenPlugin` über sein Interface; der Scanner meldet die Klasse als `unreferenced`. |
| `CompilerProvenUnreachableStatement_IsDeadCode` | Die Compilation meldet `CS0162` für eine Anweisung nach `return`; der Scanner gibt dafür noch keinen Dead-Code-Eintrag aus. |

Der vorherige Test zu `test_only` wurde entfernt: Er fixierte die heutige
Member-Ausgabe, die nach der obigen Entscheidung gerade entfallen soll.
Nach der letzten Teständerung war `dotnet build AiNetLinter.slnx`
warnungsfrei; inkrementelles `verify` bestand mit Score 10.0 und 0
Verstößen. Der neue gezielte Test schlug wie erwartet fehl: `HiddenPlugin`
wurde als `unreferenced` gemeldet. Der vollständige FastTests-Lauf endete
mit **8 fehlgeschlagenen und 2771 erfolgreichen Tests**; alle acht Fehler
stammen aus der Reproduktionsdatei. Das Abschluss-`verify(scope: solution)`
bestand mit Score 10.0 und 0 Verstößen und zeigte 468 bestehende
Dead-Code-Kandidaten in AiNetLinter selbst. Die roten Tests sind als
Reproduktion für die nächste Implementierung vorgesehen; Produktionslogik
wurde in diesem Schritt nicht geändert.

## Folgeschritt und Vertragsfolgen

Als nächstes einen Gegenfall mit erreichbarem Code zum `CS0162`-Rot-Test
ergänzen. Danach die Dead-Code-Ausgabe auf diesen beweisbaren Scope begrenzen
und die sieben False-Positive-Tests grün machen. Bestehende Tests, die `low`-Advisories für
Serializer/Callbacks oder `test_only`-Member erwarten, müssen an den neuen
Vertrag angepasst werden; sie dokumentieren den jetzigen Zustand, keinen
Beweis für toten Code.

Wenn die Implementierung CLI-/MCP-Verträge, Konfiguration oder Regeln ändert,
sind `Docs/linter/configuration.md`, `Docs/linter/cli.md`, Agentenregeln und
`ainetlinter-rules.json` synchron zu ändern. Die externe Planner-Solution
bleibt lediglich eine spätere Vergleichsmessung für die Zahl entfallener
Fehlmeldungen, keine Quelle produktbezogener Sonderfälle.
