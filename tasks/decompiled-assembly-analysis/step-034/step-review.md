# Step-034-Kritik

## Verdict

**issues**

Die beiden gemeldeten Hauptpfade funktionieren für die von den Tests erzeugten
Fehler: ein ungültiger CacheRoot wird vor der Kanonisierung verworfen, und der
reale Loader-zu-Tool-Pfad ruft keinen Provider und keine Registry auf. Der Step
kann dennoch nicht als approved gelten, weil der terminale
ConfigurationFailure-Status nicht tatsächlich explizit modelliert ist. Bei
einem zulässigen internen Failure-Ergebnis ohne Diagnosen fällt der Pfad wieder
auf NoMatch und damit auf die erfolgreiche statische Decompilation zurück.

## Prüfgegenstand und Kontext

Geprüft wurden die Coder-Commits
fcad25e5594923a362fffc113ed709c21d2a6535 und
1dd591283b61ce1cca1fce535a1f1c196bece440 mit absolutem
projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter. Die angeforderten
Regel-, Task- und Step-Dokumente wurden gelesen. Die im Initial-Prompt
referenzierte Datei .agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md
existiert in diesem Repository nicht und konnte daher nicht geprüft werden.

## Findings

### MAJOR — ConfigurationFailure hängt fälschlich von einer nichtleeren Diagnoseliste ab

ExternalSourceConfigurationLoadResult.Failure(...) akzeptiert in
src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs:123-125 eine
leere Diagnoseliste. Das Ergebnis hat dann korrekt Configuration == null und
Succeeded == false (:114-118).

AssemblySourceSelectionOrchestrator.ResolveAsync beendet diesen Fall zwar in
:47 vor Provider und Registry. AssemblySourceSelectionScope.Status bestimmt
den Status aber in :128-134 so:

1. kein Selection-Ergebnis,
2. kein Provider-Failure,
3. leere LoaderDiagnostics ergibt NoMatch,
4. sonst ConfigurationFailure.

Damit wird ExternalSourceConfigurationLoadResult.Failure([]) als NoMatch
klassifiziert. AssemblyAnalysisToolSupport prüft in
src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs:53-80
nur auf ConfigurationFailure; es ruft anschließend CreateContextAsync auf
und kann BuildResult mit einer OriginKind=decompiled-Antwort liefern. Das
verletzt die Step-034-Invariante und macht den Fail-closed-Schutz von einer
zufälligen Eigenschaft der Diagnoseerzeugung abhängig. Die aktuelle
ExternalSourceConfigurationLoader-Implementierung liefert zwar bei ihren
heutigen Fehlerstellen nichtleere Diagnosen, die interne Failure-Fabrik lässt
den kontraktwidrigen Zustand jedoch ausdrücklich zu.

Korrekturpaket: Den Config-Failure-Zustand als explizites, unveränderliches
Scope-Merkmal aus !configurationResult.Succeeded weitergeben oder die
Load-Result-/Scope-Modelle so absichern, dass der Status nicht aus der
Diagnoseanzahl inferiert wird. Einen Test für Failure([]) ergänzen: eigener
ConfigurationFailure-Status, kein Provider, keine Registry-Lease, kein
Context, kein BuildResult und ein policy-konformes, geheimfreies
IsError=false-Resultat. Success(ExternalSourceConfiguration.Empty) muss
weiterhin NoMatch und den gewöhnlichen statischen Fallback liefern.

### MINOR — Die Adversarial-Matrix beweist nicht alle dokumentierten URI-/UNC-Zweige

Die Implementierung in
src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs:236-315
verwirft die geprüften üblichen Formen korrekt: Schema/Doppelpunkt,
Query/Fragment, Device-Präfixe, reservierte Segmente, Dot-Segmente und
ungültige Segmentzeichen werden vor Path.GetFullPath geprüft. Der relative
Loader-Fall sowie echte Laufwerks- und UNC-Formen sind durch bestehende bzw.
neue Tests abgedeckt.

Die neue Matrix in
src/AiNetLinter.FastTests/Configuration/ExternalSourceCacheRootValidationTests.cs
verwendet für den URI-/Userinfo-Fall jedoch nur
https:/user:secret@example.invalid/cache (ein Slash nach dem Schema). Es
fehlen eigenständige Fälle für https://..., protocol-relative
//user:secret@host/... und die gewünschte Entscheidung für unvollständige
UNC-Formen gegenüber \\server\\share\\cache. Der aktuelle Code verwirft die
typischen ersten beiden Fälle zwar über die vorhandene Doppelpunkt-/@-Logik,
aber die Tests sichern diese Zweige nicht separat ab. Die gültigen UNC-Tests
belegen nur \\server\\share\\cache; sie belegen keine vollständig
definierte Klassifikationsgrenze für alle UNC-Formen.

Nächste Aktion: Die Matrix um diese expliziten Authority-/Userinfo- und
UNC-Fälle erweitern und für Loader, direkten Optionskonstruktor sowie die
Fabrik dieselbe erwartete Klassifikation festhalten. Das ist eine
Test-/Vertragspräzisierung, kein nachgewiesener zusätzlicher Netzwerkzugriff.

### MINOR — Das E2E-Assertion prüft IsError nicht exakt auf false

Die neue Regression und die angepasste bestehende Testgruppe verwenden
Assert.NotEqual(true, result.IsError) (unter anderem
AssemblyAnalysisConfigurationFailureTests.cs:56 und
AssemblyAnalysisToolSupportTests.cs:268). Das beweist nicht so präzise wie
Assert.False(...), dass der Rückgabewert der McpToolResults.Recoverable-
Policy entspricht. Der Produktionscode setzt in
src/AiNetLinter/Mcp/McpToolResults.cs:61-73 explizit isError: false, und
die Policy-Dokumentation ist damit korrekt; nur der neue Testnachweis sollte
die exakte Zusicherung verwenden.

## Positive Vertragsprüfung

- ExternalSourceConfigurationPath wird durch get_feature_context,
  get_symbol_body, find_symbol, find_references und get_impact als
  gemeinsamer Pfad-Helper mit Aufrufern am Loader
  (ExternalSourceConfigurationLoader.cs:239), am Optionsrand
  (ExternalSourceConfiguration.cs:26) und an der Cache-Fabrik
  (ExternalSourceRepositoryCacheOptionsFactory.cs:28) bestätigt.
- Die Rohprüfung erfolgt vor der Kanonisierung und ohne Datei-, Netzwerk-,
  Credential- oder Assembly-Ladeaktion. Direkte Optionsfehler sind generisch;
  der rohe CacheRoot und secret erscheinen nicht in der geprüften Diagnose
  bzw. Exception. Der konfigurierte Loader-Fallback für einen gültigen
  relativen CacheRoot und die gültigen Drive-/UNC-Fälle bleiben erhalten.
- Der reale Regressionstest verwendet TestTempDirectory, eine lokal
  emittierte Roslyn-DLL und
  AssemblySourceSelectionOrchestrator.CreateFromSettings. Er zeigt
  CacheRootInvalid, Configuration == null, Succeeded == false,
  Provider.CallCount == 0, Registry.ResidentCount == 0, keinen
  BuildResult-Aufruf, keinen decompiled-Text und ein Resultat über
  McpToolResults.Recoverable ohne StructuredContent. Die Scope-Instanz ist
  disposed. AssemblyTestHelper.EmitAssembly emittiert nur eine DLL und lädt
  sie nicht.
- Die gewöhnlichen Fallbacks bleiben durch die bestehende Gruppe in
  AssemblyAnalysisToolSupportTests.cs:243-331 belegt: NoMatch und
  Ambiguous erzeugen weiter statische Decompilation; die separaten
  Provider-/Capability-Fälle bleiben ebenfalls erhalten. Eine Textsuche fand
  keinen verbliebenen Test, der einen ungültigen Config-Load als erfolgreichen
  Fallback festschreibt.

## Reproduzierbare Verifikation

### Abschlussgates

| Lauf | Ergebnis |
|---|---|
| dotnet build | **0 Fehler, 0 Warnungen** |
| dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore | **2123 bestanden, 2 übersprungen, 2125 gesamt** |
| dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore | **370 bestanden, 0 übersprungen** |
| fokussierter Step-034-Lauf | **47 bestanden, 0 übersprungen** |
| Stress | **nicht ausgeführt** |

Die zwei Fast-Test-Skips sind die bekannten Reparse-Point-Tests
ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains
und
ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed;
beide scheitern hostbedingt an Win32 ERROR_PRIVILEGE_NOT_HELD (1314), nicht an
der Step-034-Änderung. Nach den Läufen existierten keine testhost-/vstest-
Prozesse; die geprüften Repository-temp-/cache-Verzeichnisse waren leer bzw.
nicht vorhanden.

### MCP-/Qualitätsaudits

- Die geänderten Symbole wurden mit projektgebundenem MCP und absolutem
  projectRoot über get_feature_context, find_symbol, get_symbol_body,
  find_references, get_impact und safeguard geprüft. Die direkten
  geänderten Configuration-/Selection-/AssemblyAnalysis-Symbole haben keine
  direkten Linter-Verstöße; im breiten Assemblies-Scope bleibt nur die
  bestehende Directory-Warnung.
- find_duplicates, Produktionsscope, minTokens=20: Configuration 0/85,
  Assemblies 0/371, AssemblyAnalysis 1/50. Der eine exakte Cluster im
  AssemblyAnalysis-Scope sind die bestehenden, unabhängig typisierten
  FindAssemblyExtensionsTool-/InspectAssemblyTool-Entry-Points. Die
  strukturellen Läufe ergaben Configuration 0/89, Assemblies 4/423 und
  AssemblyAnalysis 3/56; die Kandidaten sind semantisch getrennte,
  bestehende Mapper-/Transport-/Diagnose-Helper, keine neue CacheRoot- oder
  Statusduplikation.
- find_dead_code mit private_internal, high, includeTests=true,
  mode=members: Configuration 0, Assemblies 0, AssemblyAnalysis 0.
- Ein find_magic_values-Lauf mit changedOnly=true ist am geprüften
  Commit nicht reproduzierbar: der Working Tree ist sauber, daher meldet das
  Tool keine geänderten Dateien. Die aktuellen unbeschränkten Scopes liefern
  Configuration 40 (davon 39 Constant-Kandidaten), Assemblies 107 und
  AssemblyAnalysis 1 (source-backed, bestehend außerhalb der geänderten
  Step-Dateien). Die im Coder-Ergebnis dokumentierten Werte 39/1/0 werden
  daher als Vor-Commit-Messung behandelt, nicht als aktueller changedOnly-
  Nachweis. Ein neuer sicherheitsrelevanter Magic Value wurde nicht gefunden.
- safeguard gegen Threshold 8,00 bleibt erwartungsgemäß ein ehrlicher
  bestehender FAIL: global 5,6595/10, Assemblies 5,7973/10,
  Configuration 5,50/10, AssemblyAnalysis 5,50/10. Die drei
  gemeldeten Ursachen sind die bestehende Assemblies-Directory-Größe, der
  bestehende DaemonHostCommand-Footprint und die bestehende
  Task-Directory-Größe. Sie sind im Step-Ergebnis als vorbestehende Schuld
  dokumentiert; daraus entsteht für Step 034 kein neuer Tech-Debt-Eintrag und
  kein globaler Sweep.

## Nächste Aktion

Die Befunde als ein Korrekturpaket bearbeiten: expliziten Config-Failure-Marker
bis zum Tool einführen, den Failure([])-Regressionstest ergänzen, danach die
Authority-/Userinfo-/UNC-Matrix vervollständigen und die MCP-Assertion auf
Assert.False schärfen. Erst nach diesem Paket kann Step 034 erneut als
approved bewertet werden. tech-debt.md wurde nicht verändert, da kein neuer
Tech-Debt-Fund neben dem blockierenden Vertragsdefekt nachgewiesen wurde.
