# Prüfgruppe A – Health & Handshake

## Auditstand und Methode

Read-only-Agenten-Audit am 11.09.2026 gegen den laufenden MCP-Daemon (Version
`1.0.193`). Source-Target: `C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx`.
Als Assembly-Target wurde per `rg --files -uu` die vorhandene produktive
Assembly `C:\Daten\Entwicklung\Ralf\AiNetLinter\artifacts\publish\AiNetLinter.dll`
ermittelt. Es wurden keine Builds, Tests oder Codeänderungen ausgeführt.

Konkrete echte MCP-Calls:

- `get_server_health({})`
- `get_server_health({ targetPath: "...\\AiNetLinter.slnx" })`
- `get_server_health({ targetPath: "...\\AiNetLinter.dll" })`
- `get_server_health({ targetPath: "...\\AiNetLinter.slnx", includeDiagnostics: true, maxDiagnostics: 2 })`
- `get_server_health({ targetPath: "...\\AiNetLinter.dll", includeDiagnostics: true, maxDiagnostics: 2 })`
- `report_observability_feedback({ feedbackType: "confusing_output", ... })` ohne Target
- freie Exploration: fehlendes Target (`targetPath: ""`), nicht existentes
  `.slnx`, globales `includeDiagnostics: true`, Wiederholungs-Call nach Fehler

## Soll-Ist-Abgleich

| Konzept-/Kriteriumsbereich | Ist | Bewertung |
|---|---|---|
| Globaler Health ohne Target | Erfolgreich; globale Aggregate ohne Fehler. | erfüllt |
| Source-Health | Source wird als `origin: source`, Snapshot `fresh: true`, Status `ok/complete` und `next: null` ausgegeben. | teilweise erfüllt |
| Source-Capabilities/Lint-Status | Keine `capabilities`-Eigenschaft und kein expliziter Lint-Status in `structuredContent` oder Text. | Major |
| Assembly-Health | `origin: assembly`/`originKind: decompiled`, `completeness: partial`, `errorPhase: decompilation` sowie Diagnosesummen und `next=request_detail` vorhanden. | teilweise erfüllt |
| Assembly-Lint-Unterstützung | Kein explizites `lint: unsupported` (weder in `capabilities` noch als maschinenlesbares Feld). | Major |
| `includeDiagnostics=true` | Für konkretes Source-Target bleibt die Antwort korrekt `complete` ohne Samples; für Assembly erscheinen bis zu 2 Samples, mit `truncatedBy`-Signal. Global wird die Option ohne Target ignoriert/unterdrückt. | überwiegend erfüllt |
| Observability-Feedback | Ohne Target erfolgreich bestätigt; `received`, Titel, Typ, Severity und UTC-Zeit strukturiert geliefert. | erfüllt |
| Navigation/Envelope | Zielgebundene Antworten besitzen konsistent `navigation.target`, `snapshot`, `status`, `next`; Source `next=null`, Assembly `next` als Aktion. | erfüllt |
| Fehlerqualität | Falsches bzw. leeres Target liefert `INVALID_ARGUMENT`, `fieldPath: $.targetPath`, konkrete Handlungsanweisung, keinen Stacktrace. | erfüllt |
| SNR | Zielgebundener Health-Text wiederholt globale Daemon-/Verbindungsmetadaten und sämtliche Daemon-Keys, obwohl nur ein Target angefragt wurde. | Minor |

## Findings

### [Major] Befund A-01: Source-Health weist Lint-Fähigkeit nicht aus

- **Tool:** `get_server_health`
- **Evidenz:** Call `get_server_health({targetPath: "C:\\...\\AiNetLinter.slnx"})` liefert in `structuredContent.navigation` nur `target.origin="source"`, `snapshot.kind="source"` und `status.operation="ok"`; das gesamte StructuredContent enthält kein `capabilities`-Feld und keinen `lint`-Status.
- **Problem:** Der Prüfkatalog verlangt für ein gültiges Solution-Target eine Capability-Aussage einschließlich Lint-Status. Ein Agent kann aus Health nicht maschinenlesbar erkennen, ob Lint verfügbar/unterstützt ist; er muss dies aus einem anderen Tool oder implizit aus dem Target ableiten. Das schwächt den in `Konzept.md` geforderten Status-/Capability-Vertrag.
- **Reproduktion:** 1. MCP-Server aufrufen. 2. `get_server_health` mit absolutem `AiNetLinter.slnx` ausführen. 3. `structuredContent` nach `capabilities` bzw. `lint` durchsuchen – nicht vorhanden.
- **Empfehlung:** Eine knappe, maschinenlesbare Capability-/Lint-Aussage im vereinbarten Health-StructuredContent ergänzen und durch tools-/Contracttests absichern; keine Statusinformation nur in Freitext verstecken.

### [Major] Befund A-02: Assembly-Health signalisiert fehlende Lint-Unterstützung nicht explizit

- **Tool:** `get_server_health`
- **Evidenz:** Call `get_server_health({targetPath: "C:\\...\\artifacts\\publish\\AiNetLinter.dll"})` liefert `originKind="decompiled"`, `loadState="partial"`, `errorPhase="decompilation"`, aber kein `capabilities.lint="unsupported"` bzw. äquivalentes Feld. Der Text nennt nur `Quelle: Dekompilat` und Diagnosen.
- **Problem:** Der Agent kann die Herkunft erkennen, aber nicht eindeutig anhand eines Capability-Feldes entscheiden, dass Lint im Assembly-Modus nicht unterstützt wird. `partial`/Decompilation-Fehler ist semantisch etwas anderes als „Lint unsupported“ und kann zu einem falschen Folgeversuch mit Lint-Tools führen.
- **Reproduktion:** 1. Existierende DLL verwenden. 2. `get_server_health` mit DLL-Target ausführen. 3. `structuredContent` prüfen – `origin` ist vorhanden, expliziter Lint-Unsupported-Status fehlt.
- **Empfehlung:** Assembly-Health um den expliziten Lint-Unsupported-Status in `capabilities` erweitern; Status und nächste Aktion müssen für einen Agenten eindeutig sein.

### [Minor] Befund A-03: Zielgebundene Health-Textantwort trägt unnötigen globalen Daemon-Kontext

- **Tool:** `get_server_health`
- **Evidenz:** Source-Call für ein einzelnes Target enthält im Text neben dem Projekt zusätzlich `Connections`, `PID`, `Daemon-Keys` mit drei vollständigen absoluten Pfaden und Daemon-Uptime. Die strukturierte Navigation begrenzt das Target korrekt, der Text jedoch nicht.
- **Problem:** Für die nächste Agentenentscheidung sind vor allem LoadState, Origin, Snapshot und Status relevant. Globale Keys/Prozessdaten erhöhen Rauschen und wiederholen den Target-/Daemon-Kontext; das steht im Spannungsfeld zur Konzeptforderung nach semantischer Dichte und geschützter Fachevidenz.
- **Reproduktion:** Einen zielgebundenen Source-Health-Call ausführen und Textantwort mit dem globalen Call vergleichen.
- **Empfehlung:** Bei zielgebundenen Antworten den Freitext auf das angefragte Target und kompakte Status-/Next-Informationen projizieren; globale Aggregation nur beim ungebundenen Call ausgeben.

## Positive Verifikationen

- Globaler Call ohne Target war fehlerfrei und liefert serverweite Aggregate (`projects: []`, Assembly-Sessions und Statusverteilung), ohne unzulässige zielgebundene Navigation.
- Source-Target lädt erfolgreich; `origin=source`, `snapshot.kind=source`, `fresh=true`, `status=ok`, `completeness=complete`, `next=null` sind konsistent strukturiert.
- Assembly-Target wurde tatsächlich als `origin=assembly`/`decompiled` erkannt; `partial` und `next=request_detail` sind sichtbar. Bei `includeDiagnostics=true` erscheinen Samples sowie `truncatedBy=[maxDiagnostics,messageLength]`.
- Fehlerfälle nennen das konkrete Feld: nicht existierendes Target und leerer String liefern `INVALID_ARGUMENT`, `fieldPath=$.targetPath`, verständliche Hinweise und keinen Stacktrace. Ein korrekter Source-Health-Call nach dem Fehler blieb unbeeinträchtigt.
- `report_observability_feedback` ohne Target bestätigte die Annahme strukturiert (`received=true`, `feedbackType`, `severity`, `timestampUtc`).

## Offene Grenzen

- Ein globaler `includeDiagnostics=true`-Call ohne Target liefert weiterhin nur Aggregate ohne Samples. Das ist laut Toolbeschreibung für globale Antworten zulässig; es wurde daher nicht als Fehler gewertet.
- Es war kein separates Lint-Capability-Feld in den Health-Antworten vorhanden; die Bewertung stützt sich auf den Pflichtkatalog und die Konzeptanforderungen, nicht auf vermutete interne Implementierung.
- Der Audit hat keine nicht-zielgebundenen Lint-Aufrufe oder vollständige Cross-Tool-Capability-Matrix durchgeführt; das gehört in andere Prüfgruppen.
