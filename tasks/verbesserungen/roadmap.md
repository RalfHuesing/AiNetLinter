# Ausführungs-Roadmap

Primäraufgabe: Robuste und fokussierte Assembly-Analyse
Betriebsart: Großkonzept, ein fachliches Epic mit zwei lieferbaren Umsetzungspaketen und einer bewusst zurückgestellten Erweiterung
Status: executing
current_epic: EPIC-1
last_commit: noch nicht erstellt
current_debt_item: keines
debt_attempts: 0

## EPIC-1: Robuste Assembly-Analyse

Ziel: Fremde .NET-Assemblies werden source-first und cache-sicher analysiert; Antworten bleiben im Root-Scope und weisen Herkunft, Vollständigkeit, Evidenz sowie wiederverwendbare Symbol-Handles aus.

Abhängigkeiten: Bestehende Assembly-Session/Registry, Decompilation- und Repository-Caches, External-Source-Mapping, MCP-Assembly-Verträge und die vorhandene TestKit-Infrastruktur.

Betroffene Bereiche: `src/AiNetLinter/Mcp/Assemblies/`, Assembly-MCP-Tools und Modelle, External-Source-/Repository-Akquisition, Cache-Lifecycle und Cleanup, zugehörige Fast-/Integration-Tests sowie `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` und bei sichtbaren Meilensteinen `Docs/ROADMAP.md`.

Muss-/Akzeptanzkriterien: Die Muss-Kriterien 1–10 des Konzepts für gemeinsame Artefakte, Exklusivität, Veröffentlichung, Stall-/Abbruchverhalten, negative Source-Ergebnisse und Cleanup; die Muss-Kriterien 11–14 für Root-Scope, Provenienz, Vollständigkeit, generationsgebundene Symbol-Handles und synchrone, begrenzte MCP-Antworten.

Verifikation: Paket 1 mit gezielten Lock-, Source-first-, Fallback-, Manifest-, TTL-, Cleanup-, Wiederholungs- und End-to-End-Tests; Paket 2 mit Assembly-Contract- und Navigationstests. Nach dem letzten Codezustand: alle konzeptspezifischen Prüfungen, gezielte MCP-Impact-/Violation-Prüfungen, `dotnet build`, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` sowie der scope-nahe Audit.

Annahmen/offene Fragen: Lokales gemeinsames Dateisystem ist die vorausgesetzte Koordinationsgrenze. Ein noch gehaltener Stall-Lock wird nie automatisch übernommen; ein Betreiber beendet den blockierenden Prozess. Prozessübergreifende Tests werden nur aufgenommen, wenn die vorhandene Infrastruktur sie zuverlässig und ohne künstliche Testkopplung abbildet.

### Umsetzungspakete

- [ ] Paket 1 — Source-first und gemeinsame Artefakte: `open`
  - Ziel: Gültige attestierte Checkouts tatsächlich als source-backed Analyse verwenden; Artefakte und Repository-Snapshots pro fachlichem Schlüssel exklusiv, unveränderlich und cleanup-sicher erzeugen.
  - Abhängigkeiten: keine innerhalb dieses Epics.
  - Verifikation: passende Fast-/Integrationstests und gezielter `get_violations`-Nachweis nach der letzten Codeänderung.
- [ ] Paket 2 — Fokussierte Navigation: `open`
  - Ziel: Root-Scope, explizite Referenznavigation, Provenienz, Vollständigkeit und generationsgebundene Symbol-Handles vertraglich vereinheitlichen.
  - Abhängigkeiten: Paket 1 liefert stabile Session-/Generation-/Source-Herkunft.
  - Verifikation: Contract-, Navigation-, Trunkierungs- und generationsgebundene Folgeaufruf-Tests sowie gezielter `get_violations`-Nachweis.
- [ ] Paket 3 — Persistenz-/Seiteneffektanalyse: `deferred / non-goal`
  - Ziel: Eigenständiges Folgepaket; nicht Teil dieses Laufs und kein Startblocker.
  - Abhängigkeiten: spätere fachliche Beauftragung.
  - Verifikation: keine in diesem Lauf.

## Abschluss-Checkliste aus dem Konzept

- [ ] Fast-Tests für Lock-Exklusivität, Warten/Cancellation, Stall-Diagnose ohne Übernahme, Manifestvalidierung, negative TTL und Cleanup-Grenzen
- [ ] End-to-End-Nachweis eines gültigen Mappings mit `source-backed` als Ergebnisquelle und ohne gewählte Decompilation
- [ ] Gegenproben für die zulässigen strukturierten Fallback-Gründe und dekompilierte Herkunft
- [ ] Integrationstests für parallele DLL- sowie Repository-/Revisions-Aufrufe; prozessübergreifend nur bei verlässlicher Testinfrastruktur
- [ ] Abbruch zwischen Erzeugung und Veröffentlichung mit erfolgreicher Wiederherstellung
- [ ] Wiederholungsprüfungen für die Anzahl von Assembly-Artefakten und Checkout-Verzeichnissen
- [ ] Contract-Tests für Root-Scope, Referenzstatus, Provenienz, Symbol-Handles, Vollständigkeit und sichtbare Trunkierung
- [ ] Gezielte MCP-Impact-/Violation-Prüfungen nach Codeänderungen
- [ ] Scope-naher DRY-/Dead-Code-/Magic-Value-Audit
- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
