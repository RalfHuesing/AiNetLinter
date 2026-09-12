# Roadmap und Implementierungsplan: Verschlankung der Integrationssuite

## Arbeitsvertrag

Ziel ist eine deutlich schnellere Integrationssuite, ohne Testaussagen zu verlieren. Varianten, Edge-Cases, Formatter-, Budget- und Validierungslogik werden in FastTests verlagert. Integrations- und E2E-Tests behalten nur reale Systemgrenzen und repräsentative Verträge.

Diese Datei ist zugleich Roadmap, Umsetzungsreihenfolge und Abnahmeprotokoll. Jeder Eintrag beginnt als `[ ]`. Er darf erst nach der tatsächlich ausgeführten Arbeit und dem jeweils angegebenen Nachweis in `[X]` geändert werden. Ein gesetztes `[X]` bedeutet: Code, Tests und Dokumentation des Schritts sind vollständig, nicht nur begonnen.

Nicht Ziele dieser Aufgabe:

- Testaussagen ersatzlos löschen oder Assertions abschwächen.
- Reale Prozess-, Wire-, Dateisystem-, MSBuild-/Roslyn-Lade- oder Assembly-Metadaten-Verträge als FastTests deklarieren.
- Dogfood- oder Performance-Tests stillschweigend aus dem Standard-Gate entfernen.
- Fremde, bereits vorhandene Working-Tree-Änderungen ändern, stagen oder committen.

## Ausgangslage und Leitentscheidung

Die Analyse vom 2026-09-12 ergab 127 C#-Dateien in `AiNetLinter.IntegrationTests` (ca. 760 KB), davon 71 MCP-Dateien (ca. 580 KB). Es existieren 447 `[Fact]`-/`[Theory]`-Deklarationen. Die Zahlen sind Strukturindikatoren, keine gemessenen Laufzeiten.

Die Zuordnung folgt diesen Regeln:

| Behauptung des Tests | Zielprojekt | Erforderlicher Nachweis |
| --- | --- | --- |
| Reine Validierung, Mapping, Formatter, Budget-/Trunkierungsprojektion, Ergebnisinhalt | `AiNetLinter.FastTests` | In-Memory-Daten; weder Prozess noch MSBuild-Laden |
| Tool-Verhalten mit künstlichem `McpCodeGraphServer`-/Solution-Snapshot | `AiNetLinter.FastTests` | Bestehenden In-Memory-Testkontext verwenden |
| Reales Dateisystemverhalten | `AiNetLinter.IntegrationTests` | Isoliertes Verzeichnis über `TestTempDirectory` |
| Reales MSBuild-/Roslyn-Laden, Assembly-Metadaten oder dekompilierter Quellursprung | `AiNetLinter.IntegrationTests` | Repräsentativer End-to-End-Fall |
| MCP-Prozess, JSON-RPC-Frames, Daemon- oder Thin-Client-Lifecycle | `AiNetLinter.IntegrationTests` | Wenige repräsentative Wire-/Lifecycle-Verträge |
| Vollständiges Repository/Dogfood oder Performance-Messung | separates explizites Policy-Gate | Bewusste, dokumentierte Gate-Entscheidung |

Die FastTests besitzen einen technischen Schutz: `Process`, `Microsoft.Build`, `MSBuildWorkspace` und `SourceFileCatalog.LoadAsync` dürfen dort nicht referenziert oder zur Laufzeit geladen werden. Ein Test darf deshalb nicht durch bloßes Verschieben zur FastTest werden; seine Fixture und Abhängigkeiten müssen ebenfalls leichtgewichtig sein.

## Zielbild

1. Pro MCP-Boundary existiert ein kleiner, echter Integrationsvertrag: erfolgreicher Aufruf, mindestens ein repräsentativer Fehlervertrag sowie bei Bedarf ein Handoff oder eine echte Zielart.
2. Jede zusätzliche Parameterkombination, jeder Cap, jeder Retry und jede Payload-Projektion liegt als FastTest beim verantwortlichen Validator, Scanner, Formatter oder Tool.
3. Jeder entfernte Integrationstest ist in einer Umzugstabelle einer konkreten FastTest-Methode zugeordnet; kein Verhalten wird nur durch einen allgemeineren Test „vermutlich“ abgedeckt.
4. Die Standard-Integration läuft ohne Variantenballast; Dogfood und Performance werden anschließend bewusst behandelt, aber nicht versehentlich umklassifiziert.

## Umsetzungsscheiben

### 0. Vorbereitung, Schutz des Working Trees und Messkonzept

- [ ] Vor der ersten Codeänderung `git status --short`, `git diff --name-only` und den aktuellen HEAD erfassen; die bereits fremd geänderten MCP-Dateien ausdrücklich aus dem eigenen Staging ausschließen.
- [ ] Für jede betroffene Klasse eine Umzugstabelle anlegen: bisherige Testmethode, Behauptung, neue oder erweiterte FastTest-Methode, verbliebener Integrationsvertrag und Begründung.
- [ ] Eine wiederholbare Laufzeitbasis per TRX für beide Non-Stress-Testprojekte festlegen und vor der ersten Umzugsscheibe einmal erfassen. Die Baseline enthält Dauer, Anzahl und fehlgeschlagene Tests; sie ist kein Performance-Benchmark einzelner Tests.
- [ ] Prüfen, dass alle künftigen FastTest-Fixtures die FastTests-Dependency-Guards einhalten und weder Prozess noch MSBuild-/Katalogladen verwenden.

Abnahme: Die Umzugstabelle ist vollständig für alle nachfolgenden Scheiben; Baseline und Working-Tree-Abgrenzung sind nachvollziehbar dokumentiert.

### 1. Reine Response- und Validierungslogik sofort verlagern

Diese Scheibe enthält Tests, deren aktueller Methodenrumpf keine reale Boundary berührt.

- [ ] Die Theorie `ValidateOptions_NonPositiveDiagnosticLimit_ReturnsFieldAwareInvalidArgument` aus `GetServerHealthToolTests` nach `FastTests/Mcp/Tools/ServerMaintenance` verschieben oder dort fachlich passend ergänzen.
- [ ] Die sechs `Build_*`-Fälle aus `GetServerHealthToolTests` als FastTests beim `GetServerHealthResponseBuilder` verlagern: Compact/Detailed-Ansicht, leere Diagnosen, Session-Limit, Partial-Status und zielgerichtete Assembly-Auswahl.
- [ ] Die drei `DiagnosticsProjection_*`-Fälle aus `AssemblyAnalysisDispatcherCapabilityTests` in einen FastTest neben `AssemblyAnalysisResponseLimits` überführen: Deduplizierung, Trunkierungsgrund und globales Sample-Budget.
- [ ] Die bisherigen Integrationstestmethoden erst entfernen, wenn die zugeordneten FastTests exakt dieselbe fachliche Aussage einschließlich StructuredContent und Text-Projektion belegen.
- [ ] Den kleinsten passenden FastTest-Lauf für die neuen Klassen ausführen und die FastTests-Dependency-Guards prüfen.

Verbleibt als Integration: alle `ExecuteAsync_*`-Health-Fälle mit realem geladenem Snapshot sowie alle Assembly-Dispatcher-Fälle mit synthetisch kompilierten Assemblies.

Abnahme: Keine reine Builder-/Validator-/Projektionslogik aus dieser Scheibe bleibt im Integrationstestprojekt zurück.

### 2. MCP-Argumentvarianten aus dem echten Host entkoppeln

`McpServerArgumentValidationE2ETests` enthält rund 30 Varianten über den gemeinsam gestarteten `ReadOnlyMcpHostFixture`. Der Prozessvertrag ist real, die Mehrzahl der Variationen jedoch nicht.

- [ ] Für jedes Tool die gegenwärtigen Fehlervarianten aus `McpServerArgumentValidationE2ETests` in der Umzugstabelle den verantwortlichen FastTest-Zielen zuordnen: `McpArgumentValidationFilterTests`, `McpBatchArgumentsTests` sowie die jeweiligen Tool-Validator-/Tool-Tests.
- [ ] Fehlende FastTests für Pflichtfelder, unbekannte Argumente, falsche Typen, Array-Elementtypen, nicht positive Limits, Obergrenzen und exakte `fieldPath`-Werte ergänzen.
- [ ] Die FastTests müssen sowohl Fehlercode als auch `fieldPath` und, wo bisher behauptet, die Navigation prüfen; nur den Text zu prüfen reicht nicht.
- [ ] Im Integrationstest genau drei repräsentative Fälle belassen: ein fehlendes Pflichtfeld mit Navigation, ein falsch typisiertes Feld mit indexiertem `fieldPath` und ein unbekanntes Tool mit Protokollfehler.
- [ ] Alle übrigen Varianten nach bestandenen FastTests aus `McpServerArgumentValidationE2ETests` entfernen und die Klasse auf die drei Wire-Verträge reduzieren.
- [ ] Den fokussierten FastTest- und den fokussierten Integrationstestlauf ausführen.

Abnahme: Jede entfernte E2E-Argumentvariante besitzt einen spezifischen FastTest; der verbleibende Integrationstest beweist weiterhin die komplette Fehlerhülle über echten MCP-Transport.

### 3. Tool-Verhalten auf repräsentative E2E-Verträge reduzieren

`McpServerToolBehaviorE2ETests` und `McpServerAssemblyHealthE2ETests` wiederholen viele Tool-spezifische Varianten über denselben Prozess.

- [ ] Für `McpServerToolBehaviorE2ETests` die Varianten zu FindSymbol, FindReferences, CallTree, NamespaceTree, ClassStructure, Skeleton, Hotspots, TypeHierarchy, Violations und SearchPattern den bereits vorhandenen FastTest-Bereichen zuordnen.
- [ ] In diesen FastTest-Bereichen fehlende Assertions für Filter vor Limit, „nicht gefunden“, Depth-Clamping, Response-Budget, Text-/StructuredContent-Gleichlauf und Navigation ergänzen.
- [ ] In `McpServerToolBehaviorE2ETests` nur einen erfolgreichen Standardaufruf je Boundary-Gruppe sowie mindestens einen echten Cross-Tool-Handoff belassen; keine bloßen Parameter- oder Cap-Varianten.
- [ ] Die Assembly-Health-Varianten zu Limits, `detailLevel`, ungültigen Array-Elementen und Budgetfehlern in die FastTests für Assembly-Tool, Response-Budget und Argumentvalidierung verlagern.
- [ ] In `McpServerAssemblyHealthE2ETests` mindestens einen echten `inspect_assembly`-Durchlauf, einen Health-Aufruf und einen Registrierungs-/Schema-Vertrag behalten.
- [ ] Je verschobenem Verhalten prüfen, dass Text, StructuredContent und Navigation im FastTest ebenso präzise abgedeckt sind wie zuvor im Wire-Test.
- [ ] Fokussierte FastTest- und Integrationstestläufe ausführen.

Abnahme: Der echte MCP-Prozess deckt Verträge und Zusammensetzung ab; Toolvarianten werden ausschließlich im schnellen, verantwortlichen Testbereich gepflegt.

### 4. Geladene Solution gezielt minimieren, nicht leugnen

`SearchPatternToolTests`, `FindMagicValuesToolTests`, `GetIndexScopeToolTests` und verwandte Klassen verwenden einen geladenen MSBuild-/Roslyn-Snapshot. Das macht ihre gegenwärtige Fixture legitim als Integration, aber nicht jede einzelne Variante.

- [ ] Für `SearchPatternToolTests` zwei bis drei reale Laden-/Ausführungsverträge festlegen: ein normaler Treffer, ein semantisch angereicherter Treffer und ein repräsentativer Fehler-/Completeness-Vertrag.
- [ ] Regex-, Filter-, Scope-, Exclusion-, Range- und Budgetvarianten in die bestehenden SearchPattern-Scanner-/Tool-FastTests mit In-Memory-Solution verlagern.
- [ ] Für FindMagicValues und GetIndexScope jeweils einen echten geladenen-Solution-Vertrag behalten; Heuristiken, Filter, Limits, Renderer und Payloadvarianten in die vorhandenen FastTest-Suiten überführen.
- [ ] Keine der FastTest-Fixtures darf `LoadedFixture`, `SourceFileCatalog.LoadAsync`, `MSBuildWorkspace` oder einen Prozess indirekt referenzieren.
- [ ] Nach jeder Klasse zuerst die neuen FastTests, dann den verbleibenden Integrationsvertrag ausführen.

Abnahme: Real geladene Lösungen beweisen weiterhin die Laden-Grenze; die Fallmatrix läuft ohne MSBuild im FastTestprojekt.

### 5. Bewusst unverändert lassen: echte Boundary-Tests

- [ ] Die Daemon-, Thin-Client-, Lifecycle- und JSON-RPC-Frame-Tests in `IntegrationTests/Mcp/Daemon`, `McpServerCommandJsonRpcFramingTests` und `McpToolAnnotationsWireTests` gegen die Umzugstabelle prüfen und als Integration bestätigen.
- [ ] CLI-Prozess-Tests, echte Dateisystemeffekte, `MsBuildFixtureHost` sowie Tests mit synthetisch kompilierten Assemblies als Integration bestätigen, sofern ihre jeweilige Aussage die reale Grenze betrifft.
- [ ] Bei gemischten Klassen nur die reinen Helper-/Formattermethoden extrahieren; den echten Boundary-Fall nie in FastTests nachbilden.

Abnahme: Für jeden nicht verschobenen Bereich ist die Boundary-Begründung in der Umzugstabelle festgehalten.

### 6. Dogfood- und Performance-Policy separat entscheiden

Die Kategorien `Dogfood` und `Performance` sind aktuell nicht `Stress`; damit laufen sie im Standardfilter `Category!=Stress` mit.

- [ ] Laufzeiten und Aussage der Dogfood-/Performance-Klassen separat erfassen.
- [ ] Entscheiden und dokumentieren, ob sie absichtlich Teil des Non-Stress-Gates bleiben oder als explizites manuelles/nächtliches Gate geführt werden sollen.
- [ ] Diese Kategorieentscheidung nur mit ausdrücklicher fachlicher Freigabe umsetzen; sie ist keine automatische Folge der FastTest-Migration.

Abnahme: Die Gate-Policy ist explizit, die zugehörigen Befehle sind dokumentiert und die Tests bleiben erhalten.

### 7. Vollständige Verifikation und Abschluss

- [ ] Alle verschobenen Tests gegen die Umzugstabelle prüfen: keine alte Integrationstestmethode ohne Ersatz, kein Ersatz ohne ursprüngliche Behauptung.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` erfolgreich ausführen.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` erfolgreich ausführen.
- [ ] `dotnet build` erfolgreich und warnungsfrei ausführen.
- [ ] TRX-Daten vor/nach der Umstellung vergleichen: Gesamtdauer, Anzahl Tests und auffällige Klassen. Das Ergebnis als Messwert dokumentieren, ohne aus einer einzelnen Messung eine Garantie abzuleiten.
- [ ] `git diff --check` und eine auftragsbezogene Diff-Prüfung durchführen; ausschließlich eigene Pfade stagen und gemäß Projektregel committen.

Abnahme: Beide Non-Stress-Gates und der Build sind grün, die FastTests-Guards bestehen, die Umzugstabelle ist vollständig und die Integrationslaufzeit ist anhand der TRX-Vergleiche nachvollziehbar reduziert.

## Künftige Pflege

Bei jedem neuen MCP-Test zuerst die Behauptung formulieren. Nur wenn sie einen echten Prozess-, Wire-, Datei-, MSBuild-/Roslyn-Lade- oder Assembly-Vertrag benötigt, gehört sie in `IntegrationTests`. Alle anderen Varianten beginnen als FastTest. Bei Zweifel wird zunächst ein FastTest plus ein einzelner repräsentativer Boundary-Vertrag angelegt, nicht dieselbe Fallmatrix auf beiden Ebenen.
