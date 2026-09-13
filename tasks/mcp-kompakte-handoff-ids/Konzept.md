---
status: draft
execution_mode: requires_user_decision
open_questions:
  - "Soll ein kompakter Handle nach einem Daemon-Neustart bewusst ablaufen oder persistent wiederherstellbar sein? Empfehlung: bewusst ablaufen lassen."
  - "Soll die kanonische v2-Handoff-ID weiterhin als öffentliche Eingabe akzeptiert werden? Empfehlung: ja, als kompatibler Fallback."
---

# Konzept: Kompakte MCP-Handoff-Handles

## Ziel und Problem

AiNetLinter soll Symbol- und Handoff-Referenzen in MCP-Antworten deutlich
tokenärmer machen, ohne semantische Navigation, Target-Bindung,
Snapshot-Prüfung oder Tool-Komposabilität zu verlieren.

Die heutige Handoff-ID enthält zwei opaque, selbstprüfbare Token für Target und
Snapshot sowie eine Roslyn-`DocumentationCommentId`. Sie ist damit zugleich
Adressierung, Versionsprüfung und Symbolbeschreibung. Das ist robust und
zustandslos, bläht aber Antworten mit vielen Symbolen erheblich auf.

Live-Messungen am laufenden MCP-Server gegen `AiNetLinter.slnx`:

| Aufruf | Antwortzeichen | Zeichen in Handoff-IDs | Anteil |
|---|---:|---:|---:|
| `find_symbol(namePatterns: ["Mcp"], kind: "class", maxResults: 50)` | 11.899 | 4.945 | 41,6 % |
| `find_references(McpToolResults, maxResults: 50)` | 10.534 | 4.000 | 38,0 % |
| `get_file_skeleton(McpToolResults.cs)` | 12.057 | 6.730 | 55,8 % |

Im Skeleton enthielten 45 Einträge unterschiedliche Handoff-IDs. Ihre mittlere
Länge lag bei rund 150 Zeichen; die längste hatte 346 Zeichen. In
`find_references` wurde dieselbe 80-Zeichen-ID in allen 50 Zeilen wiederholt.

MCP-Content wird dem aufrufenden Modell als Kontext gegeben. Ein residenter
Server-Cache spart daher **keinen** Modellkontext, solange dieselbe lange ID im
sichtbaren Content wiederholt wird. Lange Listen dürfen weiterhin fachliche
Evidenz enthalten; die technische Folgeadressierung muss darin aber nicht den
größten Anteil ausmachen.

## Kernentscheidung: Bedeutung und Adresse trennen

Die Nutzerthese trifft zu, mit einer Präzisierung: Die aktuelle ID ist eine
Mischform.

```text
s:t2ABLfltQnPTGsPibw8now:1CNegpJZi6T8cTprXxsrVQ:
M:AiNetLinter.Mcp.McpToolResults.WithNavigation(...)
\--- opaque Target ----/ \--- opaque Snapshot --/ \------ Symbolsemantik ------/
```

Die ersten zwei 22-Zeichen-Teile haben für das Modell keinen fachlichen
Erklärungswert. Der letzte Teil enthält Typ-, Member- und teilweise
Parameternamen, ist aber als Follow-up-Parameter zu lang und zeigt häufig eine
unfreundliche Roslyn-Syntax.

Künftig sollen zwei getrennte Felder bzw. Textbestandteile ausgegeben werden:

```text
method AiNetLinter.Mcp.McpToolResults.WithNavigation(
  CallToolResult result, AnalysisTarget? target, int maxResponseBytes, ...)
  — src/AiNetLinter/Mcp/McpToolResults.cs:…; handoffId: `h:7Qm4Ks9vA1`
```

- **Semantische Anzeige** bleibt explizit: Symbolart, verständlicher
  qualifizierter Name bzw. Signatur und, wenn für die Entscheidung nützlich,
  relativer Pfad plus Position.
- **Handoff-Handle** ist bewusst anonym und kurz. Es ist ausschließlich die
  kopierbare Maschinenadresse für Folgeaufrufe.
- Die serverseitige Zuordnung enthält weiterhin die vollständige kanonische
  Identität; das Modell muss sie weder sehen noch rekonstruieren.

Die Hausnummer-Analogie gilt damit: Die Antwort sagt weiterhin, *wer* an der
Adresse wohnt; `h:…` ist nur die kurze, eindeutige Adresse für den nächsten
Aufruf. Ein Modell darf weder aus Anzeige noch Handle eine Identität erraten.

## Vorgeschlagene Architektur

### Zentrale Source of Truth

Eine zentrale, daemon-residente Klasse (Arbeitsname
`CompactHandoffRegistry`) ist allein zuständig für die Abbildung:

```text
registriere(kanonische Handoff-Identität, Target, Snapshot, Art)
  -> h:<zufälliger-Base64Url-Handle>

auflösen(h:<…>, erwartetes Target, aktueller Snapshot)
  -> kanonische Handoff-Identität | definierter Fehler
```

Sie kapselt insbesondere:

- Handle-Erzeugung mit ausreichend großem, nicht erratbarem Namensraum
  (Empfehlung: mindestens 64 Bit, Base64Url; kein fünfstelliger Zähler und
  keine Bedeutung im Handle),
- Zuordnung zu Origin (`source`/`assembly`), kanonischem Target,
  Snapshot-Fingerprint und Roslyn-`DocumentationCommentId`,
- begrenzten Cache mit TTL und LRU-Eviction,
- atomare, nebenläufig sichere Registrierung und Auflösung,
- klare Fehlertypen für unbekannte/abgelaufene Handles,
  Target-Mismatch und stale Snapshot,
- einheitliche Darstellung und Parser-/Resolver-Einstiege für sämtliche
  Producer und Consumer.

Der bestehende `SymbolHandoffIdentifier` bleibt zunächst der kanonische,
selbstvalidierende Datenträger hinter dem Registry-Eintrag. Das vermeidet eine
gleichzeitige Neudefinition von Roslyn-Symbolauflösung und Handle-Lifecycle.

### Lebensdauer und Ownership

Empfohlene Semantik für den ersten Release:

- Die Registry gehört dem `DaemonHost`, nicht einem einzelnen Tool und nicht
  einem einzelnen `McpCodeGraphServer`-Aufruf.
- Ein Handle bleibt während der Daemon-Lebensdauer nutzbar, sofern es nicht
  per TTL/LRU evicted wurde.
- Nach Daemon-Neustart, TTL oder Eviction ist ein Handle absichtlich nicht mehr
  gültig. Der Server liefert einen recoverable, eindeutig benannten Fehler mit
  `next.kind=refine_scope` bzw. dem Hinweis, die Ermittlung erneut auszuführen.
- Bei der Auflösung prüft der Server weiterhin das übergebene `targetPath` und
  den aktuellen Snapshot. Ein bestehender Handle wird dadurch nicht zu einer
  Umgehung von `TARGET_MISMATCH` oder `STALE_SNAPSHOT`.
- Die Registry wird beim Daemon-Shutdown verworfen; es gibt keine persistente
  Ablage und keine Wiederherstellung aus MRU- oder Log-Dateien.

Diese Wahl passt zur bestehenden daemon-residenten Projekt- und Assembly-Cache-
Architektur. Persistenz wäre ein eigenständiger Sicherheits-, Invalidierungs-
und Datenschutzvertrag und ist kein nötiger Teil der Token-Optimierung.

## Vertragsumfang: vollständig durch Producer und Consumer

Der Umbau darf nicht nur `find_symbol` ändern. Er umfasst jede öffentliche
Stelle, die eine Handoff-ID ausgibt, sowie jede Stelle, die sie als
`symbolIdentifier` oder `symbolIdentifiers` konsumiert. Vor Umsetzung wird
eine codegestützte Inventur aller heutigen `handoffId`-, `id`- und
`SymbolHandoffIdentifier`-Projektionen als verbindliche Migrationsliste
erstellt.

Mindestens zu prüfen und über die zentrale Registry zu führen sind:

- Symbol-Discovery und -Struktur: `find_symbol`, `get_file_skeleton`,
  `get_class_structure`, `get_namespace_tree`, `metrics_tree` und verwandte
  Projektionen.
- Symbolnavigation: `get_symbol_body`, `find_references`, `get_call_tree`,
  `get_impact`, `get_type_hierarchy`, `find_implementations`,
  `dependency_graph`, `metrics_lookup`, `get_feature_context` und
  `get_test_context`.
- Assembly-Navigation: `inspect_assembly`, `find_assembly_extensions`,
  `search_assembly`, `get_assembly_context` sowie ihre Reference-/Call-Graph-
  Projektionen.
- Diagnostics/Verify, sofern deren `ref` tatsächlich eine Handoff-Identität
  trägt. Nicht-Symbol-Referenzen erhalten entweder eine klar getrennte
  Handle-Art oder bleiben ausdrücklich außerhalb dieses Vertrags.
- Alle Registrierungen, Dokumentationstexte, Agent-Guide-Hinweise und
  Fast-/Integrationstests, die das bisherige Wireformat `s:`/`a:` erwarten.

`continuationToken` bleibt zunächst **kein** Symbol-Handoff: Er besitzt eigene
Paging-Semantik. Ob er separat verdichtet werden soll, ist erst nach der
Inventur zu entscheiden und darf den Symbol-Refactor nicht unsichtbar
vergrößern.

### Rückwärtskompatibilität

Empfehlung: Neue Antworten geben ausschließlich `h:…` aus. Eingaben
akzeptieren vorübergehend sowohl `h:…` als auch die bisherige kanonische
`s:`/`a:`-Form. Letztere behält damit ihre Stärke als zustandsloser Fallback
für gespeicherte Agentenartefakte, während neue Interaktionen tokenarm sind.

Ob und wann der v2-Fallback entfernt werden darf, ist eine bewusst zu
entscheidende Kompatibilitätsfrage.

## Konkrete Beispiele

### Beispiel 1: Symbolsuche

Heute (gekürzt):

```text
class AiNetLinter.Mcp.McpToolResults — src/AiNetLinter/Mcp/McpToolResults.cs:28;
handoffId: `s:t2ABLfltQnPTGsPibw8now:1CNegpJZi6T8cTprXxsrVQ:T:AiNetLinter.Mcp.McpToolResults`
```

Ziel:

```text
class AiNetLinter.Mcp.McpToolResults — src/AiNetLinter/Mcp/McpToolResults.cs:28;
handoffId: `h:7Qm4Ks9vA1`
```

Der Klassenname und der Pfad bleiben lesbar; nur die technische Adresse wird
kurz. Ein Folgecall verwendet exakt `h:7Qm4Ks9vA1`.

### Beispiel 2: lange Methodensignatur

Gemessen wurde eine 346 Zeichen lange Handoff-ID für
`McpToolResults.WithNavigation`. Die semantische Signatur soll angezeigt
bleiben, aber nicht innerhalb der Adresse stecken:

```text
method McpToolResults.WithNavigation(CallToolResult result,
  AnalysisTarget? target, int maxResponseBytes, …)
  — src/AiNetLinter/Mcp/McpToolResults.cs; handoffId: `h:Kx9m4N2pQe`
```

`get_symbol_body(symbolIdentifiers: ["h:Kx9m4N2pQe"])` löst serverseitig auf
die vollständige Symbolidentität auf, prüft Target und Snapshot und liefert
den Body.

### Beispiel 3: Referenzliste ohne redundante Wiederholung

Heute trägt jede der 50 Referenzzeilen dieselbe 80-Zeichen-ID des untersuchten
Symbols. Die Zeilen brauchen diese Wiederholung nicht, sofern sie keine
eigenständig navigierbaren Symbole repräsentieren.

Ziel:

```text
Referenzen auf class AiNetLinter.Mcp.McpToolResults; handoffId: `h:7Qm4Ks9vA1`
- src/AiNetLinter/Commands/McpServerCommand.cs:39
- src/AiNetLinter/Mcp/AnalysisTargetResolver.cs:154
- …
```

Das spart selbst vor der Handle-Einführung 49 Wiederholungen. Falls eine
Referenzzeile später selbst ein Folgeziel werden soll, erhält sie ihren eigenen
Handle statt den Handle des Wurzelsymbols zu wiederholen.

## Muss-Kriterien

- Jede neu ausgegebene Symbol-Handoff-ID ist kurz, opaque und zentral erzeugt.
- Jede relevante Tool-Eingabe akzeptiert den neuen Handle konsistent.
- Anzeige-Semantik wird nicht reduziert: Name/Signatur, Symbolart und bei
  Fundstellen der nützliche Pfad/Ort bleiben erhalten.
- Target- und Snapshot-Validierung bleiben semantisch identisch zu heute.
- Alle erwartbaren Fehlfälle sind recoverable und unterscheiden mindestens
  unbekannt/abgelaufen, falsches Target und veralteten Snapshot.
- Producer und Consumer verwenden keine separaten Ad-hoc-Maps.
- Die Registry ist begrenzt, thread-safe und beendet sich ohne Leaks mit dem
  Daemon.
- Toolantworten bleiben Content-only und ohne `structuredContent`.
- Redundante Root-Handoffs werden nicht pro Listenzeile wiederholt.

## Akzeptanzkriterien

- Ein mit `find_symbol`, Skeleton, Hierarchie, Referenz- oder Assembly-Tool
  ausgegebener `h:…`-Handle funktioniert in jedem erlaubten Folge-Tool mit
  passendem `targetPath`.
- Ein Handle gegen ein anderes Target liefert `TARGET_MISMATCH`; ein Handle
  nach einer relevanten Source-/Assembly-Änderung liefert `STALE_SNAPSHOT`.
- Ein evicteter oder nach Neustart verwendeter Handle liefert den festgelegten
  recoverable Ablauf-Fehler samt erneuter Discovery-Anweisung.
- Die gemessenen drei Beispielantworten reduzieren die Handoff-Zeichen um
  mindestens 80 %, ohne Namen, Signaturen oder Fundorte aus der Anzeige zu
  entfernen.
- Die 50er-Referenzliste publiziert den Root-Handle nur einmal.
- Bestehende kanonische IDs bleiben, falls die offene Kompatibilitätsfrage wie
  empfohlen entschieden wird, als Eingabe funktionsfähig.
- Fast-Tests decken Registry, Parser, Target-/Snapshot-/Expiry-Fälle,
  Kollisionsbehandlung und Renderer ab; Integrationstests decken den echten
  MCP-Wire über mehrere Tools und Daemon-Lebenszyklen ab.

## Non-Goals

- Keine Änderung der Roslyn-Symbolauflösung oder ihrer fachlichen Ergebnisse.
- Kein Ersatz semantischer Anzeigenamen durch Handles.
- Keine persistente Handle-Datenbank, keine Telemetrie der Handles und keine
  Protokollierung vollständiger Handoff-Payloads.
- Keine ungetrennte Umstellung von `continuationToken` ohne eigene Analyse.
- Keine Änderung von Linter-Regeln, CLI-Konfiguration oder Analysealgorithmen,
  sofern sie nicht für den MCP-Vertrag zwingend notwendig ist.

## Risiken und Alternativen

| Option | Vorteil | Nachteil | Bewertung |
|---|---|---|---|
| Heutige kanonische IDs behalten | zustandslos, restartfest | großer Kontextverbrauch | nicht ausreichend |
| Nur IDs aus Listen de-duplizieren | kleinster Eingriff | Skeletons und einzigartige Symbole bleiben lang | sofort sinnvoll, aber nicht ausreichend |
| Deterministisch kürzere Hashes | kein Registry-Zustand | Symbolsignatur bleibt lang; Kollisions-/Vertragsfragen | geringe Wirkung |
| Zentraler daemon-residenter Handle | größte Einsparung, saubere Trennung von Anzeige und Adresse | TTL/Restart-Lifecycle nötig | empfohlen |
| Persistente Handle-Registry | Handles überleben Neustarts | komplexe Invalidierung und gespeicherte Codeidentitäten | vorerst nicht empfohlen |

## Verifikation und Dokumentation

Vor der Umsetzung werden als Baseline die realen MCP-Antworten der drei oben
genannten Beispiele erneut automatisiert gemessen: Gesamtzeichen, Zahl und
Zeichenanteil der Handles sowie sinnvolle Anzeigeinformationen. Tests messen
UTF-8-Bytes wie der bestehende Response-Budget-Vertrag; Tokenzahlen werden nur
für einen konkret benannten Ziel-Tokenizer als ergänzende Metrik geführt.

Nach der Umsetzung sind mindestens erforderlich:

- Fast-Tests für die zentrale Registry und alle Parser-/Resolverpfade,
- Integrationstests für Raw-MCP-Wire, Tool-zu-Tool-Handoff, Prozess-/Daemon-
  Neustart, Eviction sowie Source- und Assembly-Staleness,
- vollständige Non-Stress-Gates und `dotnet build`, einschließlich MCP-
  `verify`-Gate gemäß Projektregeln,
- Synchronisierung von `Docs/mcp/tools.md`, `Docs/mcp/server.md`,
  `Docs/mcp/integration.md`, Bootstrap-/Agent-Guide-Texten und sämtlichen
  Beispielen des Handoff-Vertrags.

## Arbeitsgedächtnis (nur Draft)

- Die bisherige ID wurde bewusst so entworfen, dass sie nach Cache-Eviction und
  Restart aus Target, Snapshot und DocCommentId erneut geprüft werden kann.
  Der Handle-Entwurf tauscht diese Eigenschaft gegen einen kontrollierten,
  klar kommunizierten Ablaufvertrag.
- Die Projektregel fordert Zero-Transformation für Folgecalls. Das bleibt
  erfüllt, wenn der exakt angezeigte `h:…`-String unverändert als
  `symbolIdentifier` genutzt wird.
- Die zentrale Klasse sollte generisch genug für Source und Assembly sein,
  aber nicht voreilig Paging- oder beliebige Diagnose-Token übernehmen.
