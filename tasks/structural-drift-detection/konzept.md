---
workflow: konzept-workflow
status: ready
rules_dir: .agents/rules
project_kind: brownfield
estimated_scope: large
last_updated: 2026-08-19
open_questions: []
---

# Konzept: Strukturelle Drift-Erkennung in `find_duplicates`

## Ziel

`find_duplicates` wird um den On-Demand-Modus `mode="structural"` erweitert. Der Modus erkennt semantisch ähnliche, aber tokenbasiert unterschiedliche Hilfsmethoden (Typ-4-/Intended-Duplication) anhand eines deterministischen Roslyn-Strukturprofils und bildet daraus nachvollziehbare Kandidatencluster.

Das bestehende Tool bleibt der einzige MCP-Einstiegspunkt für DRY- und Drift-Audits. Die Erweiterung erzeugt keine automatische `DuplicateCode`-Violation und verändert weder das bestehende Clone-Default-Verhalten noch `mode="refactoring-drift"`.

## Warum

Die vorhandene Clone-Erkennung erkennt Copy-Paste- und umbenannte Token-Klone über N-Gramme, während der Refactoring-Drift-Modus gezielt gegen einen bereits bekannten Helper sucht. Es fehlt die Suche nach unabhängig entstandenen Hilfsfunktionen gleicher Absicht, bei denen Namen, Literale und einzelne Syntaxvarianten voneinander abweichen.

Ein realer Befund sind mehrere Mapper für Roslyn-Typarten: `GetClassStructureTool.GetTypeKindDescription(INamedTypeSymbol)`, `GetNamespaceTreeScanner.DescribeTypeKind(INamedTypeSymbol)`, `FindSymbolTool.DescribeKind(ISymbol)` und `DeadCodeFilters.GetNamedTypeKindString(TypeKind)`. Diese Kandidaten sollen durch den neuen Modus sichtbar und anschließend fachlich geprüft werden, statt eine automatische und potenziell falsche Konsolidierung zu erzwingen.

Die Codebase hat bereits eine gemeinsame `DuplicateDetectionEngine`, eine etablierte Cluster-/Trunkierungssemantik, `scopeDir`/`scopeType`-Filter und Fast-/Integrationstest-Infrastruktur. Die neue Funktion soll diese Bausteine wiederverwenden und keine parallele Tool- oder Scanarchitektur schaffen.

## Wo im Projekt

| Bereich | Relevanz |
| --- | --- |
| `src/AiNetLinter/Core/DuplicateDetection/` | Gemeinsame Engine für Clone-Detection, Refactoring-Drift und die neue Strukturprofil-Extraktion sowie Ähnlichkeitsberechnung. |
| `src/AiNetLinter/Mcp/Tools/DuplicateDetection/` | Bestehender Mode-Dispatch, Argumentauflösung, Ergebnisbegrenzung und Text-/Structured-Output von `find_duplicates`. |
| `src/AiNetLinter/Mcp/DuplicateDetectionToolRegistrations.cs` | Öffentlicher MCP-Toolvertrag mit den vorhandenen Parametern und der Nutzungsbeschreibung. |
| `src/AiNetLinter/Mcp/ServerInstructions.cs` und `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` | Kurzbeschreibungen, die Agenten beim MCP-Handshake und über die Overview-Resource erhalten. |
| `src/AiNetLinter.FastTests/Core/DuplicateDetection/` | In-memory-/Roslyn-Tests für Engine, Profilerzeugung, Clusterbildung und False-Positive-Schutz. |
| `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetection/` | Component-Tests für Mode-Parsing, Argumentfehler, Text- und Structured-Output. |
| `src/AiNetLinter.IntegrationTests/Mcp/` | MCP-End-to-End- und Live-Repository-Tests für Registrierung und Vertrag. |
| `Docs/agent-api.md`, `Docs/integration.md`, `README.md`, `Docs/ROADMAP.md`, `Docs/configuration.md`, `.agents/skills/drift-audit/SKILL.md` | Öffentliche Toolreferenz, Agentenworkflow, Projektübersicht, Roadmap, Konfigurationsvertrag und DRY-Audit-Playbook. |

## Wie

### Toolvertrag und Kompatibilität

- `find_duplicates` erhält ausschließlich den zusätzlichen Wert `structural` für den bestehenden Parameter `mode`; `clone` bleibt Default, `refactoring-drift` bleibt unverändert.
- Der neue Modus verwendet die vorhandenen Parameterbezeichnungen und ihre Semantik: `scopeDir`, `scopeType`, `minTokens`, `similarityThreshold` und `maxResults`.
- Insbesondere werden weder `scopeFilter` statt `scopeDir` noch `minStatements` statt `minTokens` eingeführt. Ein eigener `helperSymbol` ist für `structural` weder erforderlich noch auszuwerten.
- Kandidaten bleiben On-Demand-Ergebnisse. `DuplicateCodeChecker`, `safeguard` und automatische Lint-Violations bleiben auf dem bestehenden tokenbasierten Verhalten; damit wird die höhere False-Positive-Unsicherheit semantischer Ähnlichkeit nicht als Regelverstoß ausgegeben.
- Text- und Structured-Output müssen die Modusart, gescannte Methoden, Clusteranzahl, Trunkierung, Score, Ähnlichkeitsstufe, Mitglieder (Pfad, Zeile, Signatur) und ein menschenprüfbares Strukturprofil transportieren. Das Schema wird so erweitert, dass Clone- und Refactoring-Drift-Consumer kompatibel bleiben.

### Strukturprofil und Ähnlichkeit

- Für jede bereits für Duplicate-Detection zugelassene Methode bzw. lokale Funktion wird ein unveränderliches Strukturprofil aus Syntax und `SemanticModel` gewonnen. Die bestehenden Ausschlüsse für generierten Code, permanente Pfade, `scopeDir`, `scopeType` und triviale Körper bleiben wirksam.
- Das Profil enthält mindestens: normalisierte Parameter- und Rückgabetypen, Kontrollflussform, aufgelöste Zieltypen bei `switch`/Pattern-/Member-Interaktionen sowie grobe Verhaltensmarker (Literal-Klasse/-Anzahl, Reinheit ohne Instanz-State/Mutation/I/O, Rückgabeform).
- Die Profilinformationen werden in einen transparenten, gewichteten Sparse-Feature-Vektor überführt. Die Ähnlichkeit wird deterministisch per Cosine-Similarity ermittelt; externes RAG, Embeddings, Netzwerkzugriff oder Reflection sind ausgeschlossen.
- Die Suchphase soll die vorhandene Cluster-Semantik weiterverwenden: Paare oberhalb der Mindestschwelle bilden transitive Cluster, Cluster werden nach Score stabil sortiert und mittels `maxResults` begrenzt.
- `similarityThreshold` behält die vorhandenen Werte `exact`, `near` und `fuzzy` als Ausgabefilter. Für den Structural-Modus werden drei getrennte, in `rules.json` kalibrierbare Cosine-Schwellenwerte eingeführt; sie werden nicht mit den Jaccard-Schwellenwerten `DuplicateCodeExact/Near/FuzzyThreshold` geteilt und verändern daher weder Clone-Erkennung noch Lint-Kalibrierung.
- Der Implementierungsplan kalibriert Featuregewichte und die drei Structural-Schwellenwerte an synthetischen Positiv-/Negativfällen sowie am Live-Repository. Ein Kandidat ist stets eine Prüfungsempfehlung, kein automatisches Refactoring.

### Test- und Qualitätsstrategie

- Unit-/Component-Tests decken mindestens semantisch gleiche Mapper mit unterschiedlichen Namen/Literalen, abweichende aber gleichartige `switch`-Formen, unterschiedliche Rückgabetypen, unterschiedliche Zieltypen, stateful/IO-behaftete Methoden, lokale Funktionen, Scope-/Generated-Code-Filter, Trunkierung und ungültige Moduswerte ab.
- Tooltests prüfen die unveränderte Rückwärtskompatibilität von `clone` und `refactoring-drift`, den neuen Mode-Dispatch, Parameterignoranz bzw. -validierung sowie die neuen Structured-Output-Felder.
- Ein MCP-End-to-End-Test prüft die Registrierung und einen `structural`-Aufruf über den echten Toolvertrag. Ein Live-Repository-Test sichert mindestens einen bewusst geprüften Befund oder eine stabile, nicht leere/korrekt leere Ausführung ab, ohne fragile Score- oder absolute Trefferanzahlen festzuschreiben.
- Während der Umsetzung werden punktuell `get_feature_context`, `get_test_context`, `metrics_lookup`, `get_violations` und `get_impact` für geänderte Symbole genutzt. Abschluss: `dotnet build`, beide vollständigen Nicht-Stress-Testsuiten und ein erneuter `safeguard`-Lauf.

### Dokumentation und Agentenvertrag

- Nach der Implementierung wird jede Doku-Aussage gegen den tatsächlichen Codevertrag verifiziert. Aktualisiert werden mindestens `Docs/agent-api.md` (Parameter, Modi, Structured Output und Beispiele), `Docs/integration.md` (Tool-Orientierung), `README.md` (Tooltabelle), `Docs/ROADMAP.md` (erst nach Implementierung als abgeschlossen), `Docs/configuration.md` (nur falls Konfigurationswerte tatsächlich ergänzt werden), `ServerInstructions`, Overview-Resource und die registrierte Toolbeschreibung.
- Der projektinterne Drift-Audit-Skill wird um eine bewusste strukturelle Scan-/Triage-Stufe ergänzt. Er fordert die Prüfung von Kandidaten, keine automatische Konsolidierung.

## Scope

### Muss-Haben

1. `mode="structural"` als zusätzlicher, validierter Modus von `find_duplicates`, ohne Änderung der Defaults und des Verhaltens vorhandener Modi.
2. Deterministische, Roslyn-basierte Strukturprofile und Cosine-Ähnlichkeit für zulässige Methoden/lokale Funktionen; keine externen Abhängigkeiten und keine persistente Indexinfrastruktur.
3. Transitive, sortierte und begrenzte Kandidatencluster mit nachvollziehbarem Profil in Text- und Structured-Output.
4. Vollständige Fast-/Integrationstestabdeckung der neuen Engine- und MCP-Vertragspfade sowie Regressionstests der vorhandenen Modi.
5. Vollständige Synchronisation aller tatsächlich betroffenen Doku-, Toolregistrierungs-, Server-Instructions- und Overview-Stellen.
6. Aktiver DRY-, Dead-Code- und Magic-Value-Audit nach jeder wesentlichen Konsolidierungsphase und vor Task-Abschluss. Bestätigte, risikoarme Funde im betroffenen Bereich werden behoben; nicht sofort verantwortbar behebbare Funde werden mit Begründung in `tech-debt.md` erfasst.
7. Fachliche Triage und, soweit risikoarm, Konsolidierung der im Konzept genannten Roslyn-Typ-/Accessibility-Mapper sowie weiterer bestätigter Funde aus Clone- und Structural-Scan. Betroffene Aufrufstellen, Tests, Metriken und Dokumentation werden dabei mitgeprüft.

### Nice-to-Have

Keine. Erweiterte Fingerprinting- oder LSH/SimHash-Optimierungen werden nur nach einer separaten Priorisierungsentscheidung aufgenommen.

### Non-Goals

- Kein separates MCP-Tool und keine Ausweitung des MCP-Tool-Budgets.
- Keine automatische Codeumschreibung, automatische Zentralisierung oder automatische Lint-Violation für Strukturkandidaten.
- Keine Einbindung von LLMs, Embeddings, RAG, externen Vektorindizes oder Netzwerkdiensten.
- Keine Änderung der bestehenden tokenbasierten Clone-Algorithmik, ihrer `DuplicateCode`-Lint-Semantik oder der Refactoring-Drift-Helperauflösung, außer gemeinsamen, getesteten Infrastruktur-Refactorings.
- Kein SimHash/LSH in dieser ersten Ausbaustufe; bei nachgewiesenem Skalierungsbedarf ist dies ein separates Folgekonzept.

## Entdeckte Mängel und Redundanzen

| Fund | Quelle | Entscheidung / Umgang im Task |
| --- | --- | --- |
| 42 Clone-Cluster im Produktionsscan bei `minTokens=20`, darunter nahezu identische Response-Builder, Optionen-Factories und Konfigurationsapplier | `find_duplicates`-Audit vom 2026-08-19 | Im Plan aktiv triagieren. Risikoarme, eindeutig zusammenführbare Funde in oder unmittelbar neben den berührten Bereichen beheben; größere Architekturentscheidungen mit Begründung in `tech-debt.md` dokumentieren. |
| Mehrere Roslyn-Typ-/Kind-Mapper (`GetTypeKindDescription`, `DescribeTypeKind`, `DescribeKind`, `GetNamedTypeKindString`) | Ausgangsfall des Features | Primärer Validierungsfall für den Structural-Scan; Konsolidierung nur nach Signatur-, Aufrufer- und Verhaltensprüfung. |
| Accessibility-Mapper in `SymbolVisibilityResolver` und `DeadCodeFilters` | Ausgangsfall des Features | Als zweiter Validierungsfall prüfen und bei identischem Verhalten auf einen gemeinsamen Helper umstellen. |
| Kein High-Confidence-Dead-Code im Produktionsscope | `find_dead_code`-Audit vom 2026-08-19, 467 Symbole | Nach strukturellen Refactorings erneut ausführen; kein präventives Löschen ohne konkreten neuen Befund. |
| 47 Magic-Value-Einträge bei mindestens zwei Vorkommen, unter anderem Duplicate-Code-Schwellenwerte und wiederholte Kategorien/Diagnosetexte | `find_magic_values`-Audit vom 2026-08-19 | Jeden im berührten Bereich liegenden Fund fachlich bewerten. Echte, wiederholte Fach-/Konfigurationswerte zentralisieren; absichtlich unabhängige Diagnose- oder Testliterale nicht mechanisch abstrahieren und Entscheidung dokumentieren. |
| Defekter Verweis auf `dev-loop/templates/konzept.md` | Konzept-Workflow-Prüfung vom 2026-08-19 | Über `report_observability_feedback` gemeldet. Kein Bestandteil der Feature-Implementierung, sofern der Nutzer ihn nicht explizit in den Scope nimmt. |

## Definition of Done

- `find_duplicates(mode="structural")` ist implementiert, registriert, dokumentiert und liefert ausschließlich manuell prüfbare Kandidatencluster.
- Die vorhandenen Modi `clone` und `refactoring-drift` sind verhaltenskompatibel; der Linter erzeugt weiterhin keine automatische Violation aus Structural-Funden.
- Positiv-, Negativ-, Filter-, Trunkierungs-, Structured-Output- und MCP-Registrierungstests sind vorhanden und grün.
- Die in diesem Konzept genannten DRY-, Magic-Value- und Dead-Code-Audits wurden nach der Implementierung ausgeführt; bestätigte, verantwortbar behebbare Funde sind behoben oder nachvollziehbar als Tech Debt dokumentiert.
- Alle tatsächlich geänderten öffentlichen Tool-/Konfigurationsverträge sind objektiv in `README.md`, `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` (falls anwendbar) und `Docs/ROADMAP.md` dokumentiert; generierte Agentenregeln sind bei `rules.json`-Änderungen synchronisiert.
- `dotnet build` sowie `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` laufen ohne Fehler und Warnungen durch.
- Der Abschluss-Drift-Audit ist ausgeführt und das Task-Verzeichnis enthält die Entscheidungen zu nicht sofort behobenen Befunden.
- Alle Commits folgen Conventional Commits auf Deutsch, imperativ. Das Konzept wird erst nach ausdrücklicher Nutzerbestätigung auf `ready` gesetzt und danach dem Drift-Loop übergeben.
