# Epic 4 – Aufbau, Tests und Abnahme

Dieses Epic legt Repository-Struktur, Abhängigkeitsrichtung und prüfbare Abnahme fest. [Eingaben](01-Eingaben-und-Host.md), [Regelvertrag](02-Regel-und-Findings.md) und [Speicherung](03-Storage-und-Berichte.md) definieren das Verhalten.

## Projekt- und Dateistruktur

Das neue Repository enthält genau diese vier Projekte; weitere Projekte brauchen einen konkreten Grund:

```text
AiNetReview.slnx
global.json
Directory.Build.props
README.md
Docs/
src/
  AiNetReview.Core/
    AiNetReview.Core.csproj
    Configuration/      ReviewConfig, Validator
    Analysis/           SolutionLoader, ReviewRunner, ReviewContext
    Rules/              IReviewRule, RuleDescriptor, RuleRegistry
      TemplateNoOp/     TemplateNoOpRule
    Findings/           FindingDraft, FindingIdentity, FingerprintService, StateMachine
    Storage/            IFindingStore, JsonFindingStore, Ereignis- und Manifestmodelle
    Reporting/          MarkdownReportWriter
    Catalog/            CatalogWriter
  AiNetReview/
    AiNetReview.csproj
    Program.cs
    Bootstrap/          ServiceRegistration
    Cli/                ReviewCommand, CatalogCommand
    Mcp/                ReviewTools, OperationStore
tests/
  AiNetReview.FastTests/
    AiNetReview.FastTests.csproj
    Configuration/ Analysis/ Rules/ Findings/ Storage/ Reporting/
  AiNetReview.IntegrationTests/
    AiNetReview.IntegrationTests.csproj
    FixtureRules/       FixtureFindingRule
    Fixtures/           kleine generierte C#-Solutions
    Cli/ Mcp/ Storage/ Performance/
```

Alle C#-Namespaces spiegeln diesen Pfad ab: etwa `AiNetReview.Core.Configuration`, `AiNetReview.Core.Rules.TemplateNoOp`, `AiNetReview.Core.Storage`, `AiNetReview.Bootstrap`, `AiNetReview.Mcp`, `AiNetReview.FastTests.Findings` und `AiNetReview.IntegrationTests.FixtureRules`. Die Struktur darf innerhalb eines Bereichs wachsen, aber fachlich verschiedene Bereiche werden nicht in den Root-Namespace gelegt. Pro Datei steht normalerweise ein Haupttyp. Die Fixture-Regel bleibt ausschließlich im Testprojekt.

`AiNetReview.Core` enthält keine Referenz auf den ausführbaren Host und keine MCP- oder CLI-Abhängigkeit. Der Host referenziert den Core und ist die einzige Composition Root. FastTests referenzieren den Core; IntegrationTests referenzieren Core und Host. `global.json` pinnt das .NET-10-SDK; `Directory.Build.props` aktiviert Nullable und Warnungen als Fehler. CI baut die Solution und führt FastTests und kleine IntegrationTests aus.

## DI und Regel-Erweiterung

Der Host verwendet `Microsoft.Extensions.DependencyInjection` für die wenigen langlebigen Dienste: Solution-Lader, Runner, Registry, Store, FingerprintService, Berichts- und Katalogwriter sowie MCP-Operationsverwaltung. Die validierte Konfiguration wird pro Aufruf aus der JSON erzeugt und dem Runner als unveränderlicher Wert übergeben. Die Composition Root registriert genau eine produktive Regel explizit als `IReviewRule`: `TemplateNoOpRule`. `RuleRegistry` erhält `IEnumerable<IReviewRule>` und weist doppelte IDs sowie ungültige Deskriptoren beim Start zurück. Aktivierte Regeln werden aus dieser Registry anhand der JSON-Konfiguration gewählt und nach ID sortiert. Es gibt weder Assembly-Scanning noch dynamisches Nachladen.

`ServiceRegistration` trennt die Registrierung der gemeinsamen Dienste von der einzelnen produktiven Regelregistrierung. `Program` kombiniert beides und startet CLI oder MCP. Die CLI- und MCP-Adapter akzeptieren einen bereits gebauten Provider und ihre Ein-/Ausgabeströme; IntegrationTests bauen damit denselben Host-Kern mit `FixtureFindingRule`. Es gibt keine Testabzweigung in `Program` und keinen produktiven Schalter zum Laden von Testregeln.

Konkrete Regeln und Dienste sind standardmäßig `sealed`. `IReviewRule` und kleine Dienst-Interfaces bilden die Austauschpunkte; keine abstrakte Regel-Basisklasse und keine Vererbungshierarchie. Wiederverwendete Roslyn- oder Fingerprint-Logik wird als Helfer oder injizierter Dienst komponiert. So bleiben Datenfluss und Aufrufziel beim Lesen sichtbar. DI dient auch dazu, in Tests `FixtureFindingRule` statt oder zusätzlich zu `TemplateNoOpRule` zu registrieren. Die Regel erhält Abhängigkeiten per Konstruktor; sie fragt keinen Service Locator ab. `ReviewContext` wird pro Run vom Runner erzeugt und ist kein globaler, veränderlicher DI-Dienst.

Eine neue fachliche Regel benötigt einen Ordner unter `src/AiNetReview.Core/Rules/<Regelname>/`, gezielte FastTests und genau eine zusätzliche Registrierungszeile in `ServiceRegistration`. Sie liefert ihren Deskriptor, Optionen, Dokumentation und Finding-Entwürfe selbst. `catalog` erzeugt daraus Referenz und Beispiel-JSON; weder zentrale Schema-Switches noch regelbezogene Änderungen an CLI, MCP, Store oder Markdownwriter sind zulässig. Das Entfernen einer Regel löscht ihren Ordner, ihre Tests und ihre Registrierungszeile; Konfigurationen mit ihrer ID werden danach klar als `INVALID_INPUT` abgewiesen.

Geeignete, selektiv zu prüfende AiNetLinter-Vorlagen sind [SourceFileCatalogLoader](../../../src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs) für MSBuild/Roslyn, [LongRunningToolCallStore](../../../src/AiNetLinter/Mcp/LongRunningToolCallStore.cs) für Polling und [RuleRegistry](../../../src/AiNetLinter/Core/RuleRegistry.cs) für Registrierung. Die Vorlagen werden fachlich angepasst. Es gibt keinen zusätzlichen Daemon.

## Umsetzung

1. Vier Projekte, gemeinsame Build-Einstellungen, Composition Root, validierte Konfiguration und Solution-Lader anlegen.
2. Regelvertrag, Registry, `template-noop` und Kataloggenerator anschließen. Ein sichtbarer `NoOpFindingStore` ist nur in diesem Zwischenschritt erlaubt und darf kein Urteil als gespeichert bestätigen.
3. Generischen Finding-Abgleich, FingerprintService, JSON-Store und Markdown-Berichte mit der Test-Fixture implementieren. Ab hier ist `NoOpFindingStore` nur noch Test-Double.
4. CLI und drei MCP-Tools an denselben Runner anschließen; Polling, Locking, Abbruch und Stale-Prüfung integrieren.
5. FastTests, IntegrationTests, Dogfooding und Infrastrukturlasttest abschließen. Das bisherige AiNetLinter wird dadurch weder deaktiviert noch archiviert.

## Testebenen

`AiNetReview.FastTests` prüft Konfigurationsvalidierung, Registry-Duplikate, generische Finding-Identität, Fingerprint-Byteformat, jede Zeile des Zustandsautomaten, alle Store-Schemata, Konflikte, atomare Veröffentlichung und deterministische Markdown-Ausgabe. Die Tests verwenden kleine In-Memory- oder temporäre Finding-Entwürfe; zwei Findings derselben künstlichen Regel müssen unabhängig bleiben. Akzeptierte und falsche positive Fälle, Urteilskorrektur, wirksame Options- und Verhaltensversionsänderung, Resolving, Reopening und deaktivierte Regeln sind abgedeckt.

`AiNetReview.IntegrationTests` ruft CLI- und MCP-Adapter mit injizierter `FixtureFindingRule` über ihre echten Protokoll- und Handlergrenzen auf und prüft Findings, Berichte, Polling, Verdicts, Server-Neustart, Locking und Stale-Erkennung. Zusätzlich startet es die unveränderte produktive EXE als Prozess: `review` mit `template-noop` muss einen vollständigen Null-Finding-Run erzeugen, `catalog` muss Vorlage und Doku erzeugen, `mcp` muss starten und Status liefern. Weitere Fälle sind ungültige Solution, fehlende Referenzen, Pfadflucht, Git-loses Ziel und Prozessabsturz. Der Testadapter erlaubt das Einsetzen einer Registry nur im Testprozess; die produktive CLI besitzt keinen Fixture-Schalter.

Ein deterministischer Generator in `AiNetReview.IntegrationTests/Performance` erzeugt zur Testlaufzeit eine temporäre C#-Solution mit mindestens 180.000 belegten Codezeilen in mehreren Projekten. Belegt ist eine physische Zeile mit mindestens einem C#-Token außerhalb von Kommentartrivia. Der Test läuft als separate Kategorie `Performance` vor dem ersten Release auf Windows mit mindestens 4 vCPU und 16 GiB RAM. Die Infrastruktur muss mit `template-noop` in höchstens 10 Minuten und höchstens 6 GiB Peak-Private-Bytes vollständig laden, kompilieren und einen Run veröffentlichen. Gemessen werden Laufzeit, Peak-Speicher, Dokumente und analysierte Projekte. Dieser Test misst die Infrastruktur; jede später ergänzte fachliche Regel braucht bei Bedarf einen eigenen Lasttest. Reguläre CI führt die Performance-Kategorie nicht bei jedem Commit aus.

## Definition of Done

Der erste nutzbare Stand ist fertig, wenn:

- die produktive Registry genau `template-noop` enthält, eine gültige `ainetreview.json` einen vollständigen Null-Finding-Run erzeugt und fehlgeschlagene Analysen nie als „0 Findings“ gelten;
- eine neue Regel über `IReviewRule`, ihren Ordner und eine Registrierungszeile ergänzt werden kann, ohne Runner, Adapter, Store-Schema oder Berichtsgenerator zu ändern;
- CLI und MCP dieselbe JSON und denselben Runner verwenden; MCP-Läufe ohne Tool-Timeout pollbar sind;
- die Test-Fixture den vollständigen Zyklus `new → accepted/false-positive → reopened/updated → resolved` einschließlich Snapshot, Bericht und dauerhaftem Urteil nachweist;
- vollständige Run-Pakete und Entscheidungsereignisse Epic 3 entsprechen und fehlgeschlagene Läufe keinen gültigen Zustand ändern;
- FastTests, IntegrationTests und der separate Infrastrukturlasttest grün sind, das Tool seine eigene Solution erfolgreich analysiert und `catalog` zur Registry passende Dateien erzeugt;
- der echte Store aktiv ist. `storage=disabled` erfüllt dieses DoD nie.

Die Dokumentation gilt als implementierbar, wenn jedes Feld, jeder Zustand und jeder Fehlerfall der drei Vertrags-Epics durch einen Test oder eine eindeutige Validierungsregel abgedeckt ist. Fachliche Regeln und deren Grenzwerte gehören ausdrücklich nicht zu diesem DoD.
