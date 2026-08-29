---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 033
corrects: step-032
title: "Konfigurierbare Cache-Wurzel und Refresh-Policy mit Step-032-Evidenzabschluss"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T00:30:00+02:00
related_to:
  - ../step-032/step-plan.md
  - ../step-032/step-result.md
  - ../step-032/step-review.md
  - ../step-031/step-review.md
---

# Step 033: Konfigurierbare Cache-Wurzel und Refresh-Policy mit Step-032-Evidenzabschluss

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` aus `roadmap.md` — Gitea bleibt die Source of Truth;
  lokale Cache-Generationen dürfen nur anhand eines expliziten, validierten
  Cache-/Refresh-Vertrags als aktuell oder fällig behandelt werden.
- **Vorgänger:** `step-026` bis `step-028` haben Publish, Manifest, Inventory,
  Generation und Current-Pointer abgesichert. `step-029` bis `step-031` haben
  Current-Reuse, Ownership und request-eigene Checkouts abgeschlossen.
  `step-032` hat Fetch/Refresh in eine neue Generation fachlich umgesetzt;
  Review `a16a421c` beanstandete daran ausschließlich die nicht am geprüften
  Commit reproduzierbaren Audit-/MCP-/Safeguard-/DRY-/Magic-Values-Nachweise.
- **Konzept-Referenz:** `Konzept.md`, Abschnitte zu
  `ExternalSources:CacheRoot`, `ExternalSources:RefreshIntervalMinutes`,
  validiertem Source-Cache, Fresh/Stale-Entscheidung, atomarer Generation und
  statischer Decompilation als Fallback.
- **Review-Referenz:** `step-032/step-review.md` (`a16a421c`, MAJOR-Nachweis-
  problem) und `step-031/step-review.md` (`d8cff007`, genehmigter Vorgänger).

## Primärer funktionaler Vertrag

Aus der geladenen `ExternalSources`-Konfiguration entsteht genau ein strikt
validierter Cache-Vertrag: eine kanonische externe Cache-Root und ein positives
Refresh-Intervall. Die Cache-Konstruktion verwendet diese Werte deterministisch
für die bereits in Step 032 implementierte Fresh/Stale-Entscheidung; bei
fehlender Konfiguration bleiben die bestehenden Defaults erhalten. Ein
angegebenes, ungültiges Cache-Feld führt fail-closed zu strukturierten
Diagnosen und darf nicht still auf den Default zurückfallen.

`CacheRoot` bezeichnet dabei die gemeinsame externe Cache-Elternwurzel, also
beispielsweise `<AppContext.BaseDirectory>/cache`; die Repository-Cache-
Generationen liegen darunter in einem benannten `source`-Unterordner. Der
direkte Writer-Konstruktor darf für bestehende Test- und interne Aufrufer
weiterhin eine bereits auf den Source-Unterordner zeigende Root erhalten. Diese
Unterscheidung verhindert eine Kollision mit dem bestehenden Batch-Cache und
macht die Besitzgrenze explizit.

## Split-Gate und Kontextbudget

Dieser Step ist ein größeres vertikales Funktionspaket, kein Audit-only- oder
Mini-Sweep-Step:

- **Primäre Verträge:** genau einer — Konfiguration → Cache-Root-/Refresh-
  Optionen → Fresh/Stale-Policy.
- **Gekoppelte Schichten:** höchstens drei:
  1. strikt validiertes Konfigurationsmodell und JSON-Lader,
  2. Cache-Optionen-Fabrik mit Writer-/Policy-Konstruktion,
  3. lokale Regressionen sowie reproduzierbarer Result-/Audit-Nachweis.
- **Abnahmekriterien:** genau acht, siehe unten.
- **Kontextbudget:** `max_initial_files: 12`, davon höchstens zehn
  `read_first`-Dateien. Der Coder liest zunächst genau diese zehn Dateien und
  darf höchstens zwei unmittelbar gekoppelte Dateien vor der Implementierung
  nachladen. Weitere Dateien werden erst bei einem konkreten Symbol- oder
  Testbezug gelesen.

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/codemap.md`
2. `tasks/decompiled-assembly-analysis/step-032/step-plan.md`
3. `tasks/decompiled-assembly-analysis/step-032/step-result.md`
4. `tasks/decompiled-assembly-analysis/step-032/step-review.md`
5. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
6. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs`
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshPolicy.cs`
10. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`

`ExternalSourceConfigurationLoaderTests.cs` und
`ExternalSourceRepositoryCacheRefreshTests.cs` sind die ersten beiden
`read_on_demand`-Dateien, sofern die zehn Dateien die konkrete Testnaht nicht
vollständig klären. Weitere sinnvolle Nachladeziele sind
`ExternalSourceRepositoryCacheModels.cs`,
`ExternalSourceRepositoryCacheReuse.cs`,
`ExternalSourceRepositoryCacheMaterializer.cs`,
`AssemblyAnalysisHostComposition.cs` und `Docs/configuration.md`; sie dürfen
nicht zu einem breiteren Architektur-Read führen.

## Aktueller Projektzustand (JIT-Kontext)

Der tatsächliche Code zeigt eine klare, noch nicht verbundene Lücke:

- `ExternalSourceConfiguration` enthält derzeit nur `Mappings`; der Loader
  akzeptiert im `ExternalSources`-Abschnitt nur `MappingsPath` und verlangt
  dieses Feld, sobald der Abschnitt vorhanden ist. Relative Mapping-Pfade
  werden bereits relativ zur Settings-Datei aufgelöst und strukturierte
  Diagnosen, Duplicate- und Unknown-Field-Prüfungen werden wiederverwendet.
- `LocalExternalSourceRepositoryCacheWriter` fällt derzeit auf
  `AppContext.BaseDirectory/cache/source` zurück und validiert die Root über
  `ExternalSourceRepositoryCacheContract.TryCanonicalizeAbsoluteRoot`.
  Diese Pfad- und Reparse-/Ownership-Grenze ist die vorhandene Sicherheits-
  quelle und darf nicht dupliziert werden.
- `ExternalSourceRepositoryCacheRefreshPolicy` injiziert bereits
  `TimeProvider` und ein positives `TimeSpan`; der Default beträgt 60 Minuten
  und die Grenze `now >= CreatedUtc + interval` ist für Step 032 etabliert.
  `ExternalSourceRepositoryAcquirer` baut Writer, Reader und Policy zusammen,
  erlaubt aber weiterhin explizite Test-Injektionen.
- `ExternalSourceRepositoryCacheRefresh` enthält den fachlich geprüften
  Fetch-/Publish-/Rollback-Vertrag aus Step 032. Dieser Algorithmus wird nicht
  neu entworfen; Step 033 führt nur die konfigurierte Policy und Root an die
  bestehende Konstruktion heran.
- `AssemblyAnalysisHostComposition`, `McpServerCommand`,
  `DaemonHostCommand`, `McpServerOptionsFactory` und die Registrierungen bauen
  aktuell keine konfigurierte Gitea-/Acquirer-Instanz aus den Settings. Dieses
  Host-/MCP-Wiring ist ein separater Vertrag und bleibt aus diesem Step heraus.

Die Planungs-Baseline der projektgebundenen MCP-Prüfungen wurde mit dem
absoluten `projectRoot`
`C:/Daten/Entwicklung/Ralf/AiNetLinter` erhoben: `ExternalSourceRepository`
hat im engen Violation-Scope 0 Violations; `find_duplicates` meldete bei
`minTokens=20`, `exact` 369 Produktions- und 140 Testmethoden ohne Cluster;
`find_dead_code` meldete im hohen Konfidenz-/Private-Internal-Scope 0
Dead-Code-Treffer. Der breite `Assemblies`-Safeguard lag bei 5,83/10 gegen
Threshold 8,00 und hatte drei bereits bestehende Befunde; der breite
Magic-Values-Lauf für `ExternalSourceRepository` umfasste 184 Treffer in 151
Einträgen. Diese Werte sind **Planungsbaseline**, kein vorweggenommener
Step-033-Ergebnisnachweis; der Coder muss nach der Implementierung mit exakt
dokumentierten Scopes und Optionen neu messen.

## Intention und Bündelungsbegründung

Step 032 hat die Refresh-/Fetch-Logik korrekt in die Generation-/Pointer-
Semantik eingehängt, aber die konfigurierte Root und das konfigurierte
Intervall waren ausdrücklich ausgenommen. Der nächste fachlich stabile Schnitt
ist daher die Konfiguration, die beide bereits vorhandenen Cache-Eingänge
gemeinsam versorgt. Ohne diese Schicht bleibt die Policy nur ein interner
Default und die im Konzept vorgesehene Source-of-Truth-Grenze ist nicht als
Anwendervertrag verfügbar.

Die Evidenzkorrektur aus Review `a16a421c` wird hier verpflichtend mitgeführt,
weil sie denselben Cache-/Refresh-Vertrag, dieselben lokalen Test- und MCP-
Scopes und denselben Result-Übergabepunkt betrifft. Sie wird nicht als
isolierter Audit-Step geführt: Produktionscode und lokale Regressionen liefern
den größeren funktionalen Kern, während die korrigierten Step-032-Nachweise
als Abschlussbedingung dieses Pakets angehängt werden. Der Kontext bleibt
stabil, weil die Korrektur auf die bereits gelesenen Step-032-Artefakte und die
zwei fokussierten Cache-/Konfigurationsgrenzen beschränkt ist; globale Audit-
Reparaturen oder eine neue Architekturprüfung werden nicht eröffnet.

## Konkrete Änderungen

### Schicht 1: Konfigurationsmodell und strikt fail-closed JSON-Laden

**Produktionsbereich:**
`src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` und
`src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`.

- Ein immutables internes Cache-Optionsmodell ergänzen, das die aufgelöste,
  absolute Cache-Elternwurzel und das als `TimeSpan` repräsentierte positive
  Refresh-Intervall trägt. Der Default bleibt 60 Minuten und die Default-
  Elternwurzel bleibt `<AppContext.BaseDirectory>/cache`; die effektive
  Repository-Root ist `<CacheRoot>/source`.
- `ExternalSources:CacheRoot` als optionalen, nichtleeren String und
  `ExternalSources:RefreshIntervalMinutes` als optionalen positiven integralen
  JSON-Wert aufnehmen. Relative `CacheRoot`-Werte werden deterministisch
  relativ zum Verzeichnis der geladenen `appsettings.json` aufgelöst; absolute
  Werte werden kanonisiert. Eine vorhandene Root muss nicht schon existieren,
  aber die bestehende Writer-Prüfung entscheidet weiterhin über Datei-,
  Reparse- und Besitzsicherheit beim Zugriff.
- Nur Werte innerhalb der konfigurierten Typ-/Wertegrenzen akzeptieren:
  Whitespace, null, String-/Boolean-/Fraction-Werte, 0, negative Werte und
  Overflow werden mit den bestehenden strukturierten Diagnosemustern als
  Fehler gemeldet. Ein ungültiger expliziter Wert darf nicht still den Default
  aktivieren. Unknown-/Duplicate-Field-Prüfungen werden auf die beiden neuen
  Namen erweitert; die bestehende `MappingsPath`-Pflicht und Mapping-Validierung
  bleiben unverändert.
- Bei fehlender Datei oder fehlendem `ExternalSources`-Abschnitt wird eine
  erfolgreiche Konfiguration mit den Defaults geliefert. Bei einem vorhandenen
  Abschnitt mit ungültigem Cache-Feld bleibt das Gesamtergebnis fail-closed;
  eine gültige Mapping-Datei macht den ungültigen Cache-Vertrag nicht gültig.
- Bestehende Pfadkanonisierung wiederverwenden oder an einer neutralen,
  gemeinsamen Stelle extrahieren. Keine zweite, leicht abweichende Root-
  Normalisierung und keine hardcodierte Umgebung.

**Dokumentation:** `Docs/configuration.md` muss den finalen JSON-Vertrag,
Default, Auflösungsbasis, Elternwurzel- versus `source`-Unterordnersemantik und
Diagnoseverhalten aufnehmen, weil eine Konfigurationsschnittstelle geändert
wird. `appsettings.json` nur dann ändern, wenn ein minimaler gültiger
Beispielabschnitt für die Dokumentation im bestehenden Projektmuster wirklich
erforderlich ist; kein Geheimnis und keine konkrete Maschinen-Root eintragen.

### Schicht 2: Konfigurations-zu-Cache-Konstruktion und Policy-Verbrauch

**Produktionsbereich:**
`src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs`,
`ExternalSourceRepositoryCacheWriter.cs`,
`ExternalSourceRepositoryCacheRefreshPolicy.cs` und
`ExternalSourceRepositoryAcquirer.cs`.

- Einen kleinen internen Konstruktionspfad bzw. eine Options-Fabrik am
  Acquirer-/Cache-Rand einführen, der die validierte Konfiguration in den
  Writer mit `<CacheRoot>/source` und in die bestehende Refresh-Policy mit dem
  konfigurierten Intervall übersetzt. Die konkrete Form darf eine immutable
  Options-/Bundle-Record oder eine interne Factory sein; sie darf weder ein
  neues DI-/Plugin-Framework noch eine zweite globale Konfiguration einführen.
- Den Namen des `source`-Unterordners zentral im Cache-Vertrag führen, damit
  kein neuer Magic Value in Loader, Writer und Tests entsteht. Der bestehende
  direkte Writer-Konstruktor und die expliziten `cacheWriter`, `cacheReader`
  und `refreshPolicy`-Injektionen im Acquirer bleiben für vorhandene Tests und
  kontrollierte interne Aufrufer erhalten.
- Eine ungültige Konfiguration darf den Konstruktionspfad nicht erreichen;
  `ExternalSourceConfigurationLoadResult.Succeeded` und strukturierte
  Diagnosen bleiben die vorgelagerte Grenze. Der neue Pfad muss dennoch im
  lokalen Test direkt exercised werden, damit kein ungenutztes Optionsobjekt
  oder Dead-Code-Vertrag entsteht.
- Die bestehende Fresh/Stale-Grenze, `TimeProvider`-Injektion, Fetch-Transport-
  Grenze, Generation-Publish, Expected-Current-Race und Rollback-/Fallback-
  Semantik aus Step 032 nicht ändern. Es geht um die Quelle der Root und des
  Intervalls, nicht um einen neuen Refresh-Algorithmus.

### Schicht 3: Lokale Regressionen und Evidenzabschluss

**Test- und Nachweisbereich:**
`src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`,
die bestehenden Cache-Refresh-/Acquirer-Tests sowie
`tasks/decompiled-assembly-analysis/step-032/step-result.md` und
`tasks/decompiled-assembly-analysis/step-033/step-result.md`.

- Die Loader-Tests um Defaults, relative und absolute `CacheRoot`, gültige
  Intervalle, Typ-/Grenzfehler, Duplicate-/Unknown-Felder und fail-closed-
  Verhalten ergänzen. Zentralen `TestTempDirectory` und bestehende
  strukturierte Diagnoseassertions verwenden.
- Einen lokalen Konfigurations-zu-Cache-Test ergänzen, der die effektive
  `<CacheRoot>/source`-Root und das konfigurierte Intervall beobachtbar macht.
  Fresh am exakten Grenzwert und Stale danach mit `TimeProvider`-Double,
  Recording-Transport und temporärer Root prüfen. Kein Remote-Zugriff, kein
  echter Git-/Gitea-Transport und kein Assembly-Laden.
- Bestehende Step-032-Regressionen für Current-Reuse, Refresh, Generation,
  Pointer, Ownership, Cleanup, 1314-/Reparse-Fallback sowie HTTP/Git-/Credential-
  und Process-/Native-Invarianten gezielt mitlaufen lassen; keine fachliche
  Neubewertung des bestehenden Refresh-Algorithmus einbauen.
- `step-032/step-result.md` als verpflichtenden Korrektur-Nachweis aktualisieren:
  die dortigen MCP-/Safeguard-/DRY-/Magic-Values-/Dead-Code-Angaben auf den
  tatsächlich geprüften Step-032-Commit (`59d979b76ea8cabb32a119db5341e4bce8955675`)
  beziehen, exakte Scopes/Optionen angeben, den Safeguard-Wert 5,83/10 und die
  drei bestehenden Befunde korrekt ausweisen, 369 Produktions- und 140
  Testmethoden nennen und den engen `ExternalSourceRepository`-Violation-
  Scope vom breiten `Assemblies`-Directory-Befund trennen. Ein
  `changedOnly`-Wert darf nur mit den tatsächlich verwendeten Dateipfaden und
  dem passenden Arbeits-/Commitzustand stehen; andernfalls ausdrücklich als
  aus einem sauberen HEAD nicht reproduzierbar kennzeichnen. Den bekannten
  leeren/fehlerhaften Git-Diff-Impact nicht als semantischen Impact-Nachweis
  ausgeben; symbolbasierte Impact-Aufrufe verwenden.
- `step-033/step-result.md` mit den finalen Test-, Build-, Skip-, MCP- und
  Auditwerten einschließlich Commit-Hash und exakten Parametern schreiben.
  Baseline und Ergebnis strikt trennen; keine Werte aus dem Plan kopieren,
  wenn sie nach der Änderung nicht erneut ausgeführt wurden.

## Scope

- `ExternalSources:CacheRoot` und `ExternalSources:RefreshIntervalMinutes`
  als ein validierter Cache-Konfigurationsvertrag.
- Deterministische Auflösung, fail-closed Diagnosen und Wiederverwendung der
  vorhandenen Root-/Reparse-/Ownership-Sicherheitsgrenze.
- Konfigurations-zu-Writer-/Reader-/Refresh-Policy-Konstruktion am Acquirer-
  Rand, ohne die bestehende Test-Injection zu verlieren.
- Fresh/Stale-Regression am konfigurierten Intervall, inklusive exakter
  Grenzentscheidung.
- Lokale Regressionen, vollständige nicht-Stress Quality Gates und die
  verpflichtende Step-032-Evidenzkorrektur.
- Konfigurationsdokumentation in `Docs/configuration.md`.

## Out of Scope

- `AssemblyAnalysisHostComposition`, `McpServerCommand`,
  `DaemonHostCommand`, `McpServerOptionsFactory`, Registrierungen und jede
  produktive Host-/MCP-Verdrahtung. Das ist ein eigenständiger Folge-Vertrag,
  der die neue interne Konstruktionsnaht später aktiviert.
- Änderungen am Fetch-/Refresh-/Publish-/Rollback-Algorithmus von Step 032,
  an Generation, Current-Pointer, Snapshot-, Workspace- oder Registry-
  Ownership.
- Retention, Garbage Collection, explizite Invalidierung und Telemetrie.
- Dirty-/unbuilt-Checkout, Health-/degraded-/Fallback-Policy und sichtbare
  Failure-/Health-Semantik als neues Endnutzer-Resultat.
- Provider-/Capability-Matrix, transitive Referenzen und EPIC-05.
- Assembly.Load, AssemblyLoadContext, Reflection-Ausführung oder sonstige
  Runtime-Ladung fremder Assemblies; die statische Decompilation bleibt
  unverändert.
- Remote-/Gitea-/Git-Netzwerkzugriffe in Tests sowie echte Credentials,
  Prozess- oder Native-Verifikation.
- Globale DRY-, Magic-Values- oder Dead-Code-Bereinigung und Änderungen an
  `TD-001` bis `TD-003`, sofern kein direkter Fehler im neuen Cache-Vertrag
  nachgewiesen wird.
- Produktionsänderungen, Testläufe oder Coder-/Kritikerarbeit in diesem
  Planer-Schritt selbst.

## Architekturgrenze

Die Grenze verläuft zwischen validierter Settings-Datei und der bestehenden
Cache-Acquisition. Die Configuration-Schicht liefert ein neutrales, immutable
Optionsobjekt; sie kennt weder Acquirer, Gitea, Roslyn noch Host-Registrierung.
Die Cache-Schicht übersetzt die Elternwurzel einmal in die Repository-
`source`-Root, erzeugt Writer/Reader/Policy und hält ihre bisherigen
Sicherheits- und Injektionsnähte. Der Refresh-Algorithmus bleibt unverändert.
Host/MCP darf die Naht in diesem Step nicht aufrufen. Dadurch bleibt der
Vertrag vertikal wertvoll und zugleich für das spätere Wiring eindeutig.

## Abnahmekriterien (maximal 8)

1. Fehlende Settings/fehlender `ExternalSources`-Abschnitt liefern weiterhin
   eine erfolgreiche Konfiguration mit den bestehenden Defaults; vorhandene
   `MappingsPath`-Semantik und Mapping-Diagnosen bleiben unverändert.
2. Gültige relative und absolute `CacheRoot`-Werte werden deterministisch als
   kanonische Cache-Elternwurzel aufgelöst; die effektive Source-Cache-Root ist
   genau `<CacheRoot>/source`, ohne Batch-Cache-Kollision und ohne duplizierte
   Pfad-/Reparse-Prüfung.
3. `RefreshIntervalMinutes` akzeptiert ausschließlich positive integrale Werte
   innerhalb der unterstützten `TimeSpan`-Grenze; fehlende Werte defaulten auf
   60 Minuten, ungültige Typen/Werte/Overflow sowie Unknown-/Duplicate-Felder
   erzeugen strukturierte Fehler und keinen stillen Fallback.
4. Der validierte Konstruktionspfad übergibt Root und Intervall an Writer und
   Refresh-Policy; explizite Writer-/Reader-/Policy-Testinjektionen bleiben
   kompatibel, und die neue Naht ist durch einen lokalen Test tatsächlich
   benutzt.
5. Fresh/Stale entscheidet mit dem konfigurierten Intervall am exakten
   Zeitgrenzwert deterministisch; Fetch, atomic Generation/Current-Pointer,
   Expected-Current-Race, Rollback und Fallback-Semantik aus Step 032 bleiben
   unverändert.
6. Die relevanten lokalen Regressionen sowie Build und beide vollständigen
   Nicht-Stress-Testprojekte sind grün; Tests bleiben netzwerkfrei und laden
   keine fremden Assemblies. Der repository-spezifische 1314-/Reparse-Fallback
   und HTTP/Git/Credentials/Process-/Native-Invarianten bleiben erhalten.
7. `step-032/step-result.md` enthält den reproduzierbaren Korrektur-Nachweis
   für den geprüften Step-032-Commit, und `step-033/step-result.md` enthält
   Resultate mit exakten Commit-, Scope-, Options-, Test- und Skip-Angaben;
   insbesondere keine unbelegte `changedOnly`-Behauptung.
8. Scoped MCP-Prüfungen für geänderte Configuration-/Cache-Symbole sowie
   scoped DRY/Magic-Values/Dead-Code-Läufe sind dokumentiert; neue Befunde im
   Scope werden behoben oder begründet. Breite bestehende Safeguard-Befunde
   werden nur korrekt abgegrenzt, nicht global bereinigt; kein neuer Tech-Debt-
   Eintrag wird ohne direkten Vertragsbezug erzeugt.

## Teststrategie

Der Coder führt erst die fokussierten Loader-/Cache-Tests aus und danach die
vorgeschriebenen Abschlussgates. Der Planer führt in diesem Turn keinen Test
aus.

- Fokussiert: die erweiterten
  `ExternalSourceConfigurationLoaderTests` sowie die bestehenden
  `ExternalSourceRepositoryCacheRefreshTests`, Acquirer-/Reuse-Tests mit
  Filter auf die neue Konfigurationsnaht.
- Build: `dotnet build` (Warnings-as-errors).
- Abschluss: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Nicht automatisch: `Category=Stress`.
- Alle neuen Testdoubles bleiben lokal; kein Remote-/Gitea-/Git-Netzwerk,
  keine echten Credentials und kein Runtime-Laden oder Ausführen von
  Assemblies.
- Der echte Win32-1314-/Reparse-Fall darf nur mit der bestehenden
  repository-spezifischen Skip-/Fallback-Regel transparent behandelt werden.

## MCP-, DRY-, Magic-Values- und Dead-Code-Disposition

Alle semantischen Prüfungen verwenden den absoluten
`projectRoot: C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` bleibt auf Text-
und Nicht-C#-Suche begrenzt.

- **Semantik/Impact/Violations:** Nach der Implementierung
  `get_feature_context`, symbolbasiertes `get_impact` und fokussiertes
  `get_violations` für die tatsächlich geänderten Configuration-/Cache-
  Symbole ausführen. Den bekannten Git-Diff-Impact nicht als Beleg verwenden.
- **Safeguard:** den exakt verwendeten breiten Scope und Threshold erneut
  ausführen und den bestehenden Assemblies-/Tasks-Befund als Baseline
  abgrenzen; kein globales Score-Reparaturpaket daraus machen.
- **DRY:** `find_duplicates` separat für betroffene Produktions- und Test-
  Verzeichnisse mit `minTokens=20`, `exact` und dokumentierten Scopes. Nur
  neue Root-/Interval-Konstanten, Parserlogik oder Factory-Duplikation im
  Scope beheben; die bestehende globale Zahl nicht als Step-Befund ausgeben.
- **Magic Values:** `find_magic_values` gezielt auf geänderte Configuration-
  und Cache-Symbole/Scopes mit expliziten Optionen laufen lassen. Neue
  Property-Namen, Diagnosecodes, `source`-Unterordner und Default-Intervall
  zentral benennen; bestehende Testdaten und fremde Befunde nicht global
  umformen.
- **Dead Code:** `find_dead_code` für die geänderten internen Options-/Factory-
  Symbole inklusive Tests, hoher Konfidenz und dokumentierter Accessibility
  ausführen. Unbenutzte neue Pfade entfernen; bestehende Low-Confidence-
  Kandidaten sind kein automatischer Cleanup-Auftrag.
- **Tech-Debt:** `tech-debt.md` bleibt unverändert, sofern der Coder keinen
  direkt durch diesen Cache-Vertrag neu erzeugten und nicht behebbaren Befund
  nachweist.

## Evidenzkorrektur als Abschlussnachweis

Die Review-Forderung wird nicht durch eine neue, unprüfbare Zahl ersetzt. Der
Coder muss die Step-032-Evidenz mit folgenden Regeln korrigieren:

1. Den geprüften Step-032-Commit und den aktuellen Step-033-Commit getrennt
   benennen.
2. Für jeden MCP-Lauf Server, absolutes `projectRoot`, Scope-Parameter,
   Schwellenwerte, Filter und Ergebniszahl notieren.
3. Die reproduzierten Step-032-Werte `Safeguard 5,83/10` bei Threshold `8,00`,
   drei bestehende Safeguard-Befunde, `369` Produktionsmethoden, `140`
   Testmethoden und `0` enge Violations korrekt ausweisen.
4. Den breiten Directory-Befund nicht als enge
   `ExternalSourceRepository`-Violation ausgeben.
5. `changedOnly` nur mit festgehaltenem Dateisatz und passendem Zustand
   verwenden; nach einem sauberen HEAD ist die alte, nicht reproduzierbare
   Behauptung ausdrücklich zu entfernen.
6. Step-033-Ergebnisse erst nach Code, Tests und finalem Commit eintragen;
   Planungsbaseline und tatsächliche Resultate klar labeln.

## Risiken und Gegenmaßnahmen

- **Unsichere oder missverständliche Root:** Parent-Root und Source-Root
  werden explizit benannt; absolute Kanonisierung, zentrale Writer-Prüfung,
  Reparse-/Ownership-Guards und lokale Pfadtests bleiben die letzte Grenze.
- **Stiller Default bei Tippfehlern:** explizite ungültige Cache-Felder machen
  den Load fehlgeschlagen; nur fehlende Felder defaulten.
- **Zeitgrenzen/Overflow:** nur positive ganze Minuten in einem begrenzten
  Wertebereich akzeptieren und mit injiziertem `TimeProvider` an der exakten
  Grenze testen.
- **Konstruktor- und Layer-Drift:** eine kleine immutable Factory/Bundle-Naht
  statt zusätzlicher globaler Abhängigkeiten; bestehende Test-Injektionen und
  Host-Wiring unverändert lassen.
- **Regression im Step-032-Vertrag:** keine Änderung an Fetch/Publish/Rollback;
  vorhandene Refresh-/Reuse-Tests und vollständige Nicht-Stress-Gates laufen.
- **Evidenzdrift:** Commit-Ziel, MCP-Parameter, Scopes und `changedOnly`-
  Dateisatz werden im Result festgehalten; Baselinewerte werden nicht als
  Abschlusswerte kopiert.
- **Kontextausweitung:** maximal zehn `read_first`-Dateien, höchstens zwei
  unmittelbare Nachladeziele und keine Host-/Health-/Retention-Abzweigung.

## Definition of Done

- [ ] Der eine Cache-Konfigurationsvertrag ist in den drei begrenzten Schichten
  umgesetzt und durch lokale Regressionen vertikal bewiesen.
- [ ] `CacheRoot`/`RefreshIntervalMinutes` sind dokumentiert, strikt validiert,
  fail-closed und ohne Root-/Source- oder Magic-Value-Duplikation.
- [ ] Die bestehende Step-032-Fresh/Stale-/Generation-/Ownership-Semantik und
  alle geforderten Sicherheitsinvarianten sind unverändert grün.
- [ ] `dotnet build` sowie beide vollständigen `Category!=Stress`-Gates sind
  grün; Stress wurde nicht automatisch ausgeführt.
- [ ] MCP-, DRY-, Magic-Values- und Dead-Code-Nachweise sind mit absoluten
  projectRoot-, Scope- und Optionsangaben reproduzierbar dokumentiert.
- [ ] `step-032/step-result.md` ist gemäß Review `a16a421c` korrigiert und
  `step-033/step-result.md` enthält den tatsächlichen Abschlussnachweis.
- [ ] Der Coder erstellt den fachlichen Commit; der Orchestrator führt danach
  Review, Audit und Statusübergang aus. Plan- und Statusdateien werden nicht
  vom Coder um fachfremde Folgepakete erweitert.
- [ ] `step-plan.md` wechselt nach erfolgreicher Prüfung auf
  `done (pending audit)`; ein nachfolgender Kritiker entscheidet über die
  Freigabe.

## Exakter Coder-Hand-off

Starte einen **neuen Coder-Agenten** für `decompiled-assembly-analysis`,
Step 033. Lies zuerst die zehn `read_first`-Dateien dieses Plans; lade nur die
zwei genannten Testdateien oder einen konkret begründeten Symbolbezug nach.
Implementiere ausschließlich den primären Vertrag
`ExternalSources:CacheRoot` + `ExternalSources:RefreshIntervalMinutes` bis zur
bestehenden Cache-Konstruktion und Fresh/Stale-Policy. Verwende die
Elternwurzel-/`source`-Semantik dieses Plans, erhalte Defaults, fail-closed
Diagnosen und alle bestehenden Test-Injection- und Sicherheitsgrenzen. Nutze
keine DI-/Plugin-Abstraktion und ändere kein Host-/MCP-Wiring.

Ergänze lokale netzwerkfreie Regressionen und die Konfigurationsdokumentation.
Führe die fokussierten Tests, `dotnet build` und beide vollständigen
Nicht-Stress-Gates aus. Führe anschließend mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` die symbolischen MCP-
Impact-/Violation- und scoped Safeguard-/DRY-/Magic-Values-/Dead-Code-Läufe
mit exakten Parametern aus. Aktualisiere als verpflichtenden letzten
Nachweis `step-032/step-result.md` für den geprüften Step-032-Commit und
schreibe `step-033/step-result.md` für den finalen Step-033-Commit; erfinde
keine `changedOnly`- oder Score-Werte. Halte 1314-/Reparse-Skips und alle
HTTP/Git/Credentials/Process-/Native-/statische-Decompilation-Invarianten
transparent fest. Ändere weder `task-state.md` noch `roadmap.md`, führe
keinen globalen Cleanup und keine Kritikerarbeit aus. Übergib danach den
Commit, die Testergebnisse und den sauberen Arbeitsbaum an den Orchestrator.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` — JIT-Workflow, statische
  Decompilation, sichere externe Quellen, Test-/Commit-Gates und begrenzter
  DRY/Magic-Values/Dead-Code-Scope.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — absolutes `projectRoot`,
  semantische MCP-Werkzeuge für C# und `rg` nur für Text.
- `.agents/rules/AiNetLinter.mdc` — aus `rules.json` erzeugte aktive
  Architektur- und Qualitätsregeln.
- `.agents/Agent-Scaffolding/AGENTS.md` — Rollen-/Handoff-Grenzen und
  Arbeitsbaumdisziplin.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  Korrektur-/Review-Übergabe und Statusfolge.

## Bekannte Ausnahmen

- Ein echter Win32-1314-/Reparse-Test darf nur gemäß bestehender
  repository-spezifischer Skip-/Fallback-Regel transparent übersprungen
  werden; das ist kein Freifahrtschein für einen allgemeineren Skip.
- Der breite Safeguard kann wegen bereits bestehender
  `MaxDirectoryChildren`-/Footprint-Befunde unter dem Threshold bleiben. Das
  ist zu dokumentieren und nicht in diesem Cache-Step global zu reparieren.
- Der Step-032-`changedOnly`-Nachweis darf nach sauberem HEAD als nicht
  reproduzierbar ausgewiesen werden, wenn kein exakter Dateisatz und Zustand
  vorliegen; eine nachträglich erfundene Rekonstruktion ist unzulässig.

## Notes

Die Root-/Policy-Konfiguration ist bewusst vor Health/Degraded und Host/MCP
gewählt: Sie vervollständigt direkt den im Konzept offenen Cache-Vertrag und
nutzt die vorhandenen Step-032-Eingänge. Health/Failure-Semantik, Dirty/Unbuilt,
Retention/GC/Invalidierung und Host-Wiring bleiben eigenständige Pakete mit
eigenen Abnahmekriterien. Der Plan enthält keine Produktions- oder
Teständerung; diese werden ausschließlich im nachfolgenden Coder-Schritt
ausgeführt.
