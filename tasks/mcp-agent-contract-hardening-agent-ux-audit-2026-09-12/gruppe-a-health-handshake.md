# Gruppe A – Health & Handshake

## Ziel und Scope

Prüfung der laufenden MCP-Runtime aus Sicht eines konsumierenden Agenten. Abgedeckt sind `get_server_health` (global, Source-Ziel `SOURCE-01`, Assembly-Ziel `LOCAL-01`, optionale Diagnosen) und `report_observability_feedback`. Bewertet wurde gegen Contract v2 im Konzept sowie die Kriterien Signal/Rauschen, Response-Struktur, Fehler- und Completeness-Signaling. Es wurden weder Code, Build noch Tests verändert oder ausgeführt.

## Geprüfte Szenarien

- Globaler Health-Aufruf ohne Ziel; zusätzlich mit `includeDiagnostics=true` ohne Ziel.
- Zielgebundener Source-Health für `SOURCE-01`, mit und ohne Diagnosen.
- Zielgebundener Assembly-Health für `LOCAL-01`, mit und ohne zwei angeforderte Diagnosebeispiele.
- Ein einmaliges, sachlich begründetes Observability-Feedback zum festgestellten Handshake-Problem.
- Freie Erkundung: identische Source-Health-Anfrage zweimal nacheinander.

## Positive Nachweise

- Der globale Health-Aufruf ist ohne Ziel gültig und liefert maschinenlesbare Server- und Session-Aggregate.
- `LOCAL-01` meldet den dekompilierten Ursprung und eine begrenzte Session; mit aktivierten Diagnosen wurden zwei Beispiele, Gesamt-/Shown-Counts und der Trunkierungsgrund strukturiert geliefert.
- Die beiden identischen Source-Aufrufe hielten Contract-Version, Zielherkunft und `navigation.status` stabil. Die veränderlichen Telemetriewerte sind als Live-Zustand erwartbar.
- `report_observability_feedback` akzeptierte die Pflichtfelder und bestätigte strukturiert, dass das Feedback entgegengenommen wurde; kein Zielpfad war erforderlich.

## Findings

### [Major] Befund 1: Ziel-Health veröffentlicht keine Capability-Entscheidung

- **Tool(s)**: `get_server_health`
- **Evidenz**: `get_server_health(targetPath=SOURCE-01)` liefert Contract-v2-Navigation mit `origin=source`, aber kein `capabilities`-Objekt und keinen Lint-Status. `get_server_health(targetPath=LOCAL-01)` liefert `origin=assembly`/dekompiliert, aber ebenfalls keinen Lint-Status.
- **Problem**: Ein Agent kann aus dem Handshake nicht verlässlich entscheiden, ob er Lint-Tools nutzen darf oder im Assembly-Modus mit `lint=unsupported` planen muss. Das verfehlt den Pflichtprüffall und die deklarierte Capability-Orientierung.
- **Reproduktion**: Beide zielgebundenen Aufrufe ohne Spezialparameter ausführen und `structuredContent.navigation.capabilities` prüfen.
- **Empfehlung**: Eine eindeutige, maschinenlesbare Capability-Matrix im zielgebundenen Health-Vertrag anbieten, einschließlich des Lint-Status.

### [Major] Befund 2: Assembly-Health hat konkurrierende Completeness-Signale

- **Tool(s)**: `get_server_health`
- **Evidenz**: Für `LOCAL-01` steht fachlich `assembly.completeness=partial`, während der alleinige Contract-Status `navigation.status.completeness=truncated` meldet; der Text zeigt beide Begriffe zusätzlich.
- **Problem**: Ein Agent kann nicht ohne Vertragswissen unterscheiden, ob die Analyse partiell ist, nur die Health-Antwort gekürzt wurde oder beides gilt. Das widerspricht dem Contract-v2-Prinzip eines Status-Owners und schwächt Completeness-Signaling.
- **Reproduktion**: Assembly-Health ohne Diagnosen aufrufen und die beiden Completeness-Felder vergleichen.
- **Empfehlung**: Response-Vollständigkeit und Analysequalität/-zustand mit klar getrennten, eindeutig benannten Feldern ausgeben; im Text nur die maßgebliche Recovery wiederholen.

### [Major] Befund 3: Globaler Health enthält nicht aggregierte Laufzeitidentitäten

- **Tool(s)**: `get_server_health`
- **Evidenz**: Der globale Call gibt neben begrenzten Aggregaten prozess- und verbindungsbezogene Einzelwerte sowie Repository-/Profilmetadaten aus.
- **Problem**: Diese Daten helfen einem Agenten nicht beim Health-Handshake, erhöhen aber Rauschen und legen unnötige Betriebsdetails offen. Das Konzept erlaubt hier begrenzte Daemonaggregate, nicht solche Einzelidentitäten.
- **Reproduktion**: Globalen Health ohne Ziel ausführen und `daemon` sowie den Markdown-Kopf prüfen.
- **Empfehlung**: Den globalen Vertrag auf handlungsrelevante Aggregate und explizit benötigte Statusinformationen reduzieren.

## Freie Beobachtungen

- `includeDiagnostics=true` ohne Ziel wird transparent nicht aktiviert (`diagnosticsIncluded=false`); die Toolbeschreibung erklärt die Zielbindung. Ein kurzer maschinenlesbarer Grund wäre für Erstnutzer noch eindeutiger, ist aber kein eigener Befund.
- Wiederholte zielgebundene Health-Calls ändern erwartungsgemäß Staleness-Telemetrie. Die stabilen Contract- und Statusfelder machen die Antwort trotz dieses Live-Zustands gut vergleichbar.

## Grenzen

Die Prüfung betrifft die aktuell verbundene laufende Runtime, nicht einen frisch aus diesem Arbeitsstand erzeugten Host. Es wurden nur die erlaubten lokalen Assemblyfälle verwendet; externe Herkunft, Pfade, Identitäten und Rohantworten sind nicht in diesem Bericht enthalten.
