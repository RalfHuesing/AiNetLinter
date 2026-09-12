# AiNetLinter — MCP-Host-Integration

→ [MCP-Tools & Verträge](tools.md) | [MCP-Server & Daemon](server.md) | [MCP-Bootstrap](mcp-bootstrap.md) | [Linter-Projektintegration](../linter/integration.md) | [README](../../README.md)

AiNetLinter kann als **stdio-basierter MCP-Server** gestartet werden, um die Roslyn-basierte Solution-Analyse als granular abfragbare Tools für AI-Coding-Agenten bereitzustellen (Claude Code, Cursor, eigene Agent-Loops). Vollständige Tool-Referenz, Trunkierungs-Format und Error-Codes: [MCP-Tools & Verträge](tools.md).

---

## 1. Agent-Bootstrap für ein neues Projekt

Bei einem Auftrag wie „Integriere AiNetLinter in dieses Projekt“ soll der Agent den Bootstrap genau einmal pro Projekt lesen:

```text
ainetlinter://agent-guide
```

Der Leitfaden enthält den vollständigen Ablauf für die konkrete Solution- oder Assembly-Datei, die optionale benachbarte Regeldatei, MCP-Registrierung und die dauerhafte `AiNetLinter-McpWorkflow.mdc`. Der Bootstrap ist auch offline verfügbar:

```cmd
ainetlinter.exe --docs mcp-bootstrap
```

Die dauerhafte Regeldatei kann offline separat mit `ainetlinter.exe --docs mcp-rule` ausgegeben werden.

Nach erfolgreicher Einrichtung wird der Bootstrap nicht in jedem Arbeitskontext erneut ausgeführt. Die dauerhafte Regel enthält nur die bevorzugte MCP-Werkzeugwahl; die autarke CLI-Integration als Test-/CI-Quality-Gate bleibt davon getrennt und wird in der [Linter-Projektintegration](../linter/integration.md) beschrieben.

---

## 2. Registrierung im MCP-Host

### Claude Code / Cursor / Generic MCP Hosts
Standard-`mcpServers`-Block (in `mcp.json`, `.cursor/mcp.json` oder Claude Desktop Config):

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "ainetlinter",
      "args": ["--mcp-server"]
    }
  }
}
```

Für eine zweite, vom Default getrennte Daemon-Instanz kann die sichere Instanz-ID direkt in den MCP-Args angegeben werden:

```json
{
  "mcpServers": {
    "ainetlinter-beta": {
      "command": "ainetlinter",
      "args": ["--mcp-server", "--daemon-instance", "beta"]
    }
  }
}
```

Ohne `--daemon-instance` bleibt der Endpunkt `ainetlinter.analyzer.v1.<username>`. Mit `beta` wird er zu `ainetlinter.analyzer.v1.<username>.beta`; auch Startup-Gate und MRU-State werden pro Instanz getrennt. Die ID muss mit einem ASCII-Buchstaben beginnen, darf danach nur ASCII-Buchstaben, Ziffern, `.`, `_` und `-` enthalten und ist auf 32 Zeichen begrenzt. Sie wird invariant in Kleinbuchstaben normalisiert; `BETA` und `beta` verwenden deshalb denselben Endpunkt, dasselbe Startup-Gate und dieselbe MRU-State-Datei.

Der Pfad zur `ainetlinter`-Exe wird vom MCP-Host über `PATH` aufgelöst (oder über den host-spezifischen absoluten Pfad). **Kein expliziter `--path`- oder `--config`-Parameter nötig** — jeder zielgebundene Tool-Aufruf adressiert eine konkrete vorhandene `.sln`/`.slnx`/`.dll`/`.exe`-Datei über den absoluten `targetPath`.

Die laufend erzeugte Ausgabe von `ainetlinter://agent-guide` und `ainetlinter --docs mcp-bootstrap` enthält zusätzlich einen `## Laufzeitpfad des MCP-Servers`-Block mit dem tatsächlich ermittelten `command`-Pfad. Verwende diesen Block, wenn der Host `ainetlinter` nicht über `PATH` findet. Wird AiNetLinter über `dotnet` gestartet, enthält der Block die AiNetLinter-DLL als erstes Argument.

---

## 3. cwd-Verhalten & Start-Sequenzen

### cwd-Verhalten
Der MCP-Server benötigt für zielgebundene Aufrufe keinen Projektbezug im Host-`cwd`. Die konkrete vorhandene `.sln`/`.slnx`/`.dll`/`.exe`-Datei wird je Aufruf über den absoluten `targetPath` übergeben; die Endung bestimmt die Herkunft. Damit können mehrere Targets in einer Serverinstanz resident sein.

### Start-Sequenzen: initialize und server/discover
Der MCP-Transport-Handshake (`initialize`) antwortet **sofort** — die Lösung wird parallel im Hintergrund geladen. Damit erkennen Hosts mit kurzem Startup-Timeout den Server zuverlässig als „bereit", ohne auf die `MSBuildWorkspace.OpenSolutionAsync`-Latenz warten zu müssen.

`server/discover` antwortet sofort mit Server-Capabilities und demselben globalen Instructions-Text. Die globale Anleitung verweist bei Bedarf auf den einmaligen Bootstrap unter `ainetlinter://agent-guide`, danach auf `tools/list` und `ainetlinter://overview`; Tool-Schemas bleiben in `tools/list`. Der globale Text enthält keinen vollständigen Bootstrap und bleibt unter dem Engineering-Budget von 1.200 Bytes.

Tool-Calls, die während des Hintergrund-Loads eintreffen, erhalten in beiden Pfaden einen Loading-Info-Text (`[INFO]: Server laedt die Solution noch. ...`, kein Fehler); sobald der Load abgeschlossen ist, liefern dieselben Tools reguläre Ergebnisse. Details zu den drei Zuständen (`Loading` / `Loaded` / `LoadFailed`) siehe [MCP-Server & Daemon](server.md#2-drei-zustands-lifecycle-des-mcp-servers).

### Projektauflösung im MCP-Modus
Der MCP-Modus löst keine Solution aus dem Host-`cwd` auf und akzeptiert keine zusätzlichen Projektargumente. `--path` oder `--config` in der Registrierung führen zu einem deterministischen Startfehler. Stattdessen übergibt jeder Aufruf den absoluten `targetPath` der konkreten vorhandenen Datei. Unbekannte Tool-Properties werden gegen das aktuelle `tools/list`-Schema geprüft und als `invalid_argument` mit Feldnamen abgelehnt.

---

## 4. Tool-vs-`rg`-Empfehlung für Agent-Loops

Beginne bei physischer Discovery mit `get_file_tree`: Das read-only Tool liefert relative Pfade, Verzeichnis-/Extension-Aggregation und sichtbare Completeness-/Trunkierungsangaben auch für Dateien außerhalb des Roslyn-Solutionindexes. Für ein Assembly-Ziel verwendet der Agent den Source- oder dekompilierten SourceRoot der Assembly-Session; fehlt dieser, ist die Capability explizit unsupported. Die direkte Dateisystemroute benötigt für Projekt-Ziele weiterhin keinen registrierten Projekt-Key. `view=files` eignet sich als Folgeaufruf, wenn Pfade an `search_pattern` oder `get_file_skeleton` weitergegeben werden sollen. Ein valider Projekt-Target-Block genügt auch während eines laufenden oder fehlgeschlagenen Solution-Loads, weil die Enumeration unabhängig vom Roslyn-Snapshot arbeitet.

`get_file_tree` beschreibt eine physische Dateipopulation (`summary.scannedFileCount`), `get_index_scope` zusätzlich die Roslyn-Dokumentpopulation (`population.roslynDocumentCount`). Ausschluss- und Skip-Counts sind nach Ursache und Einheit getrennt und dürfen nicht zusammengezählt werden. Zielgebundene Antworten folgen Contract v2: `navigation.status.operation` und `navigation.status.completeness` sind getrennt; bei `RESPONSE_BUDGET_TOO_SMALL` den gemeldeten `minimumResponseBytes` mit identischem Request und Snapshot wiederholen. Ausschließlich der sichtbare Content zählt zum UTF-8-Budget.

Für die anschließende semantische Analyse sollten Agent-Loops folgende Reihenfolge einhalten:

Die Progressive-Disclosure-Regel gilt für breite Listen besonders strikt: mit kleinen `maxResults`-Werten und einem engen `scopeFilter`/`typeName` beginnen, die stabile Symbol-ID oder den passenden Typ ermitteln und erst danach Bodies, Referenzen oder weitere Detailflags anfordern. Für `get_hotspots` begrenzt `maxResults` die sichtbaren Einträge, `minLinePercentage` filtert die Auslastung (Default 80, Bereich 0–100); die Ausgabe ist nach absteigender Zeilenzahl und Pfad deterministisch sortiert.

1. **Zuerst** `get_file_tree(view: "summary")` für die Dateityp- und Routingübersicht, danach C#-Symbole mit `find_symbol` und den semantischen Folge-Tools. Das vermeidet, dass Nicht-C#-Dateien als leere C#-Symbolabfrage fehlinterpretiert werden.
2. **Für Nicht-C# oder Textsuche** (z. B. `.json`/`.yml`/`.md`/`.razor`/`.xaml`/`.html`/`.css` oder Konfigurations-/Kommentar-/String-Suche): `search_pattern` mit dem kanonischen `pattern` und `scopeType` (`production`, `tests` oder `all`); Include-/Exclude-Globs laufen über `includePatterns`/`excludePatterns`. Der Default ist `maxResults=20` und `maxResponseBytes=8192`; serverseitige Caps sind 2000 Treffer und 65536 Bytes. `completeness.totalCount`/`returnedCount`/`truncatedBy` sowie der eine `next`-Hinweis sind zu prüfen. Aliasfelder wie `query`, `searchPattern`, `fileFilter` und `includePattern` gehören nicht zum Vertrag. Für sichtbare C#-Treffer kann `enrichCSharp=true` die Syntax-/Symbolkategorie und eine stabile `symbolId` ergänzen; der Default bleibt `false`.
3. **Ergänzend** `rg` / `grep` für **C#-Symbole** nur dann, wenn eine semantische MCP-Abfrage nicht passt oder konkrete Text-/Dateiarbeit gefragt ist. Für reine Symbol-, Referenz- und Impact-Fragen bleibt MCP die bevorzugte Quelle.

Konkret:

- Feature-Kontext vor Edit abrufen (Deklaration, Metriken, Callers, Tests, Violations) → `get_feature_context(symbolIdentifier: "MyClass.MyMethod")`
- Statische Test-Zuordnung & Test-Methoden für ein Symbol finden → `get_test_context(symbolIdentifier: "MyClass")`
- Klassennamen suchen → `find_symbol(namePatterns: ["MyClass"], kind: "class")` oder bei genau einem Muster `find_symbol(namePattern: "MyClass", kind: "class")`; bei einem Assembly-Ziel Referenz-DLLs ausdrücklich mit `includeReferences: true` einbeziehen
- Methoden-Aufrufer finden → `find_references(symbolIdentifier: "MyClass.MyMethod", depth: 2)`; den Content-Marker `completeness` prüfen, bevor weitere Folgeaufrufe geplant werden. Bei einem Assembly-`targetPath` kann `find_references` mit `includeReferences: true` zusätzlich bounded Referenz-Assemblies und partielle Diagnostics einbeziehen. Eine Referenz-Handoff-ID mit `false` öffnet nur ihren Owner, nie Root, Geschwister oder eine Closure.
- Impact eines Symbols prüfen → `get_impact(symbolIdentifier: ..., depth: 2)`; den Content-Marker `completeness` prüfen, bevor weitere Folgeaufrufe geplant werden. Bei einem Assembly-`targetPath` ausschließlich `symbolIdentifier` verwenden: `gitRef` oder ein leerer Aufruf sind nicht zulässig. `includeReferences=false` bleibt root- beziehungsweise owner-only; `true` öffnet die bounded Referenz-Closure. Nur die im Content markierte Handoff-ID übernehmen.
- Treffer semantisch einordnen → `search_pattern(pattern: "MyClass", enrichCSharp: true)`; `semantic.resolution` prüfen und bei `ambiguous`/`unavailable` den Snapshot-/Projektbezug oder `find_symbol`/`get_feature_context` verwenden
- Metriken & Komplexität eines Symbols prüfen → `metrics_lookup(symbolIdentifiers: ["MyClass.MyMethod"])`
- Konfigwert in `.json` finden → `search_pattern(pattern: "MySetting")` (oder direkt `rg`, das ist hier äquivalent)
- TODO-Kommentare listen → `search_pattern(pattern: "TODO", isRegex: false)` (oder `rg "TODO"`)
- Text in einer externen Assembly suchen → `search_assembly(targetPath: "C:/libs/Library.dll", searchKind: "text", pattern: "Repository", maxResults: 20)`; für typische Persistenz-/Datenzugriffe `searchKind: "data_access"`, für HTTP/RPC/Socket/Prozessaufrufe `searchKind: "external_calls"`
- Lint-Stand einer Datei → `get_violations(scopeFilter: "src/MeinProjekt/Service.cs")`
- Produktions-Hotspots isolieren → `get_hotspots(scopeType: "production")`; `tests` und `all` sind ebenfalls möglich. Für Dateitypen und Pfade dient `get_file_tree(view: "summary")`.

---

## 5. Erstorientierung: Resources

Für eine neue Integration `ainetlinter://agent-guide` genau einmal ohne Target lesen. Danach liefert `ainetlinter://overview?targetPath=<url-encoded>` die Statuskarte des adressierten Solution-Keys. `ainetlinter://rules?targetPath=<url-encoded>` liefert zusätzlich die frisch aus dem effektiven Regel-Snapshot erzeugte Karte mit Herkunft, aktiven Regeln und Schwellwerten. Details: [MCP-Tools & Verträge](tools.md).

---

## 6. Mehrere parallele Server-Instanzen

Mehrere Daemon-Instanzen sind mit einem gemeinsamen Cache grundsätzlich unterstützt. Der konfigurierte Cache-Stamm wird mit einem stabilen Daemon-Profil deterministisch als Suffix versehen, etwa `cache.codex`. Gleiche Profile verwenden denselben prozesssicher gelockten Cache; Generationen werden über Writer-/Reader-Leases und Retention geschützt. Unterschiedliche Profile, etwa `codex-a` und `codex-b`, erzeugen getrennte Cache-Stämme und sind für bewusst isolierte Instanzen zu verwenden. Prozess-IDs sind keine Cache-Identität. Health und Assembly-Antworten zeigen keine Profil-, Generations-, Lock-, Lease- oder Cleanup-Details; private Repository-URLs und Credentials werden nicht ausgegeben. Bei stale oder nicht löschbaren Artefakten bleibt der Zustand als Quarantäne mit Ursache, Besitzer und TTL sichtbar und wird nicht als gültiger Checkout wiederverwendet.

Ein gleichzeitiger CLI-Lint-Lauf auf derselben Solution kollidiert nicht mit dem MCP-Server-Cache, weil `get_violations` den Disk-Cache umgeht. Assembly-Suche und die anschließenden Assembly-Navigationstools bleiben read-only.
