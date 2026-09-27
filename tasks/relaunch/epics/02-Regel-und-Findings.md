# Epic 2 – Regel, Finding und Änderungsvergleich

Dieses Epic definiert den Vertrag für künftige C#-Review-Regeln, die neutrale Startregel und den Lebenszyklus von Findings. Eingaben stehen in [Epic 1](01-Eingaben-und-Host.md), Persistenz in [Epic 3](03-Storage-und-Berichte.md).

## Regelvertrag

`IReviewRule` liefert einen `RuleDescriptor` mit stabiler Regel-ID, Titel, positiver ganzzahliger `behaviorVersion`, Optionsschema mit Defaults und Validierung sowie Zweck, Messung und Review-Fragen für den Katalog. `ExecuteAsync(ReviewContext, RuleOptions, CancellationToken)` gibt erst nach vollständiger Analyse ein unveränderliches `RuleResult` zurück. Regelobjekte halten keinen veränderlichen Zustand zwischen Runs. `ReviewContext` enthält die einmal geladene Roslyn-`Solution`, die validierte Projektwurzel und gemeinsam nutzbare Analysezugriffe. Der Runner ruft aktivierte Regeln in aufsteigender Regel-ID nacheinander auf. Eine Ausnahme oder ein Abbruch vor vollständiger Analyse lässt den gesamten Scan scheitern; eine leere Ergebnisliste bedeutet ausschließlich „vollständig analysiert und kein Treffer“.

Eine Regel liefert Finding-Entwürfe mit dem unten definierten Zuordnungsschlüssel, Startzeile, nichtleerem Begründungstext, einer Map numerischer Metriken, mindestens einem Evidenz-Eintrag, ursächlichem Quellcode-Snapshot, kanonischem Vergleichstext, positiver `fingerprintVersion` und der Menge relevanter repo-relativer Quelldateien. Diese Menge enthält mindestens die primäre `sourcePath` und jede weitere `.cs`-Datei, deren Inhalt das Finding oder seinen Vergleichstext beeinflusst. Evidenz enthält `sourcePath`, 1-basierte `line`, `label`, `detail` und `snippet` als Strings. Die Regel bestimmt Aussage, Metriken, Evidenz, Snapshot-Umfang und Normalisierung des Vergleichstexts. Sie schreibt weder Dateien noch Markdown, berechnet keine finale Finding-ID und implementiert keine Zustandsübergänge. Der Runner validiert Entwürfe, vergibt IDs, berechnet Fingerprints und Datei-Hashes und erzeugt Storage und Berichte generisch. Pro Zuordnungsschlüssel ist höchstens ein Entwurf erlaubt.

Der Regeldeskriptor ist zugleich die einzige Quelle für Konfigurationsvalidierung, Katalog und JSON-Vorlage. Eine neue fachliche Regel besteht aus ihren Dateien und Tests unter einem eigenen Regelordner plus einer expliziten DI-Registrierung im Host. Runner, CLI, MCP, Berichtsformat, Store-Schema und Kataloggenerator werden dafür nicht geändert. Eine Änderung der Regelbedeutung, ihrer Messung, Evidenz oder Vergleichsnormalisierung erhöht `behaviorVersion`; reine Doku- oder interne Strukturänderungen tun das nicht.

## Startregel und Test-Fixierung

Die einzige produktiv registrierte Regel heißt `template-noop` und hat `behaviorVersion = 1` sowie ein leeres Optionsschema `{}`. Sie liefert immer ein vollständiges, leeres `RuleResult`, unabhängig vom Quelltext. Sie demonstriert Registrierung, Konfiguration, Katalog und vollständige Null-Finding-Runs; sie ist keine Qualitätsregel. Der Katalog kennzeichnet sie ausdrücklich als technische Vorlage. Es gibt im ersten Produktstand keine fachliche Regel.

Die IntegrationTests registrieren stattdessen beziehungsweise zusätzlich eine ausschließlich dort definierte `FixtureFindingRule` mit ID `fixture-finding`, `behaviorVersion = 1` und einer nichtleeren String-Option `scenario` mit Default `base`. Sie liefert aus einer kleinen, kontrollierten C#-Test-Solution zwei reproduzierbare Findings und erlaubt gezielte Änderungen von Vergleichstext, Optionen und Verhaltensversion. Das testet den gesamten Finding- und Verdict-Zyklus, ohne eine unbewiesene Produktregel einzuführen. Die produktive Anwendung lädt weder Test-Assemblies noch Testregeln dynamisch.

## Identität

Der Zuordnungsschlüssel ist das Tupel `(ruleId, projectPath, sourcePath, subjectId, discriminator)`. `projectPath` ist der repo-relative Pfad der besitzenden `.csproj`, `sourcePath` der repo-relative Pfad einer analysierten `.cs`, `subjectId` eine von der Regel stabil gebildete Kennung ihrer betroffenen Einheit und `discriminator` ein innerhalb dieser Einheit eindeutiger, stabiler Treffername. Alle fünf Werte sind nichtleer. Für symbolbezogene Regeln ist Roslyns `DocumentationCommentId` die empfohlene `subjectId`; dateibezogene Regeln können `file` verwenden. Die Regel dokumentiert ihre Wahl im Katalog. Doppelte Schlüssel im selben Scan sind `ANALYSIS_FAILED`. Dateien außerhalb der Projektwurzel sind nach Epic 1 unzulässig.

Eine neue Zuordnung erhält eine zufällige 128-Bit-ID `F-` plus 32 kleingeschriebene Hexzeichen. Derselbe Schlüssel behält seine ID über Änderungen des Vergleichstexts hinweg. Ändert sich ein Teil des Schlüssels, entsteht eine neue ID; eine alte Freigabe wird nie automatisch übertragen. Die Regel muss `subjectId` und `discriminator` so wählen, dass sachlich gleiche Fälle stabil bleiben und verschiedene Fälle nicht kollidieren.

## Fingerprint und Snapshot

Die zentrale `FingerprintService` erhält den von der Regel gelieferten kanonischen Vergleichstext und ihre positive ganzzahlige `fingerprintVersion`. Sie kodiert zuerst die Version als 32-Bit-Big-Endian-Zahl, danach die Länge des UTF-8-Texts als 32-Bit-Big-Endian-Zahl und dann die UTF-8-Bytes. SHA-256 darüber ergibt `sha256:` plus 64 kleingeschriebene Hexzeichen. Der Dienst nimmt keine implizite Code-Normalisierung vor. Eine Regel kann gemeinsame Roslyn-Helfer für Token- oder Symbolnormalisierung verwenden; deren Wirkung gehört in ihre eigene Spezifikation und Tests. `behaviorVersion` und wirksame Optionen werden getrennt vom Code-Fingerprint verglichen. Der Vergleichstext wird nicht gespeichert.

Der Snapshot ist der von der Regel gelieferte ursächliche Quelltext, UTF-8 ohne BOM mit LF-Zeilenenden. Er muss den gemeldeten Fall ohne weitere Quellsuche nachvollziehbar machen; bei einer Methodenregel kann das die vollständige Methodendeklaration sein. Der Runner berechnet SHA-256 über die vollständigen Bytes aller als relevant gemeldeten Quelldateien für die Stale-Prüfung. Die Fixture-Regel liefert echte Ausschnitte aus ihrer Test-Solution; `template-noop` liefert keine Snapshots.

## Zustandsautomat

Kanonische Zustände sind `open`, `accepted`, `false-positive` und `resolved`. `reopened` und `updated` sind Ereignistypen, keine Zustände. „Verschwunden“ ist Alltagssprache für `resolved`. Die wirksame Vergleichsversion ist `(fingerprintVersion, fingerprint, behaviorVersion, effectiveOptions)`. Bei unterschiedlichem Tupel gilt eine alte Freigabe nicht mehr.

| Vorher | Vollständiger Scan der aktiven Regel | Ereignis | Nachher |
| --- | --- | --- | --- |
| kein Schlüssel | Befund vorhanden | `new`, neue ID und Snapshot | `open` |
| `open` | vorhanden, Vergleichsversion gleich | keines | `open` |
| `open` | vorhanden, Vergleichsversion anders | `updated`, gleiche ID und Snapshot | `open` |
| `accepted` / `false-positive` | vorhanden, Vergleichsversion gleich | keines | unverändert, im Bericht unterdrückt |
| `accepted` / `false-positive` | vorhanden, Vergleichsversion anders | `reopened`, gleiche ID und Snapshot | `open` |
| `resolved` | vorhanden | `reopened`, gleiche ID und Snapshot | `open` |
| jeder aktive Zustand | Befund fehlt | `resolved`, gleiche ID | `resolved` |
| `resolved` | fehlt weiterhin | keines | `resolved` |

Eine deaktivierte oder nicht gescannte Regel verändert ihre Findings nicht. „Befund fehlt“ umfasst sowohl Code-Löschung als auch eine nicht mehr erfüllte Regelbedingung; der Store unterscheidet diese Gründe nicht. Ein Urteil zu einem aktuell `open`, `accepted` oder `false-positive` stehenden Finding setzt den gewünschten Entscheidungszustand. Ein identisches Urteil ist idempotent; ein Wechsel zwischen `accepted` und `false-positive` erzeugt ein neues Entscheidungsereignis, sofern Fingerprint, Quelle und Konfiguration aktuell sind. Für `resolved` wird kein Urteil angenommen. Es gibt kein manuelles `fixed` und keine zeitliche Wiedervorlage.
