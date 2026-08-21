---
status: Nochmal 360Grad Audit machen
type: konzept
project_kind: brownfield
estimated_scope: small
priority: P3
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: "1:1 uebernommen aus tasks/mcp-agenten-effizienz/07_tools-list-cachehinweise-setzen.md (Konsolidierung 2026-08-21)"
---

# Cache-Hinweise für die statische Toolliste setzen

## Ziel

Im MCP-2026-07-28-Pfad soll `tools/list` einen positiven `ttlMs`-Wert und `cacheScope: public` liefern. Standardkonforme Hosts dürfen dadurch die unveränderliche Toolliste wiederverwenden, statt sie während derselben Serverversion unnötig erneut abzurufen.

## Warum / Kontext

Der aktuelle Server liefert reproduzierbar:

```json
{
    "ttlMs": 0,
    "cacheScope": "private",
    "tools": [
        /* 26 Einträge */
    ]
}
```

Die Toolcollection ist nach Prozessstart statisch. `reload_config` ändert Regeln, aber weder Toolnamen noch Input-Schemas. Die MCP-Spezifikation 2026-07-28 führt Cache-Hinweise für Listenresultate genau für diesen Fall ein. Quelle: [MCP 2026-07-28 – List results are cacheable](https://blog.modelcontextprotocol.io/posts/2026-07-28/#list-results-are-cacheable).

Der Effekt ist clientabhängig: Die Hints erlauben Caching, erzwingen es aber nicht und garantieren keine Modelltoken-Ersparnis. Der C#-SDK-Client 2.2.0 dokumentiert sogar, dass sein High-Level-Overload selbst nicht cached. Diese Grenze muss in der Dokumentation stehen.

## Scope

### Must-have

- Über `McpServerOptions.Handlers.ListToolsHandler` ein leeres ergänzendes `ListToolsResult` mit Cache-Hinweisen liefern; die eigentlichen Tools bleiben in `ToolCollection`.
- `TimeToLive = TimeSpan.FromMinutes(5)` als bewusst konservative Projektpolicy setzen.
- `CacheScope = CacheScope.Public`, solange die Liste nicht nutzer-/authabhängig gefiltert wird.
- Vor Implementierung ein Charakterisierungstest schreiben, der nachweist, dass SDK 2.2.0 Handler-Resultat und `ToolCollection` kombiniert, ohne Tools zu verlieren oder zu duplizieren.
- Raw-Wire-Test im modernen Protokoll auf `ttlMs = 300000`, `cacheScope = public` und eindeutige vollständige Toolnamen.
- Legacy-Protokollverhalten unverändert lassen.
- Builder um eine schmale `WithHandlers`- oder `WithListToolsCachePolicy`-Methode erweitern; keine Handlerkonfiguration quer in `McpServerCommand` verteilen.

### Non-Goals

- Kein eigener Cache im Server oder Client.
- Keine Toollisten-Pagination bei nur 26 Tools.
- Keine dynamischen Toolprofile.
- Kein Workaround durch Fork/Änderung des MCP-SDKs.
- Kein positives TTL für `ainetlinter://overview`, weil dessen Statusanteil (`LoadState`, Configpfad) pro Read frisch sein soll.

## Sicherheitsbedingung

`public` ist nur korrekt, solange Toolliste und Schemas für alle Aufrufer identisch sind. Einen Test oder Kommentar direkt an der Policy platzieren:

```text
Wenn zukünftig Auth-/Mandantenfilterung die Toolliste beeinflusst, CacheScope auf private setzen
oder die Policy an den gefilterten Handler verschieben.
```

## Abbruchkriterium

Falls der Charakterisierungstest zeigt, dass SDK 2.2.0 Cache-Hinweise eines ergänzenden `ListToolsHandler` verwirft oder die `ToolCollection` nicht korrekt kombiniert:

1. keinen Reflection-/Wire-Hack bauen,
2. keinen SDK-Fork erstellen,
3. Befund mit minimaler Reproduktion dokumentieren,
4. Task als durch SDK blockiert melden.

Damit bleibt die Umsetzung innerhalb des aktuellen Tech-Stacks.

## Tests

- Modernes `server/discover` gefolgt von modernem `tools/list`.
- Ergebnis hat positive TTL exakt 300.000 ms und `public`.
- Toolnamen sind eindeutig und identisch zur registrierten Collection.
- Zweiter Request liefert dieselbe deterministische Toolreihenfolge.
- Legacy-`initialize` + `tools/list` bleibt kompatibel.
- `reload_config` ändert die Liste nicht.

## Definition of Done

- Standardkonforme Cache-Hints stehen auf dem modernen Wire.
- Keine Tools fehlen oder sind doppelt.
- Clientabhängigkeit ist dokumentiert; keine garantierte Tokenersparnis wird behauptet.
- Kein neuer Cache- oder Infrastruktur-Stack wurde eingeführt.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

