# Auditbericht – Linse 8: Tests, Dokumentation und Abschlussabdeckung

**Reviewurteil:** `issues` hinsichtlich der Abschlussabdeckung; die festgestellten Code-/Dokumentationsabweichungen sind bereits den Primärlinsen zugeordnet. Es wurde kein zusätzlicher, eigenständiger S0–S2-Produktbefund aus dieser Linse abgeleitet.

## Audit-Metadaten

- **Linse:** Testinventar, gezielte und vollständige Nicht-Stress-Verifikation, Dokumentations-/Registrierungsabgleich, Source-backed-/Decompilation-Abdeckung und redigierte Nachweisführung.
- **Geprüfter Scope:** `src/AiNetLinter.FastTests`, `src/AiNetLinter.IntegrationTests`, `Docs/agent-api.md`, `Docs/configuration.md`, `.mcp.json`, `Directory.Build.props`, `rules.json`, `.agents/rules/` sowie die acht Einzelreports.
- **Revision:** Die Produktions- und Testquellen blieben gegenüber der Audit-Baseline unverändert; die Berichtswelle enthält ausschließlich Task-Artefakte.
- **Nicht geprüft:** `Stress`-Tests, ein geschützter entfernter Repository-Dienst, privilegierte Reparse-Point-Laufzeitfälle und ein erfolgreicher source-backed Live-Refresh. Ein öffentlicher gemappter Checkout wurde live über MCP geladen, fiel aber vor der Source-backed-Antwort auf Decompilation zurück.
- **Redaktion:** Konkrete lokale Installationspfade, externe URLs, Credentials und geschützte Beispieldaten werden in diesem Report nicht wiedergegeben.

## Executive Summary

### Ergebnis

Die vorgeschriebenen Abschlussprüfungen wurden ausgeführt. `dotnet build` war erfolgreich; die vollständigen FastTests ohne `Stress` waren mit 2274 Erfolgen und 2 umgebungsbedingten Capability-Skips grün. Der abschließende vollständige Integration-Lauf ohne `Stress` endete mit 377 Tests, davon 307 erfolgreich und 70 fehlgeschlagen. Die Fehler erschienen als MCP-/Daemon-Prozessabbrüche; sie werden als reproduzierbarer Umgebungs-/Prozesslastbefund dokumentiert, nicht als unbelegte Zuordnung zu einem Assembly- oder Source-Befund.

Die Dokumentation stimmt in der Zielmatrix, der `.dll`-Validierung und den Read-only-/Metadata-only-Verträgen weitgehend mit Code und Registrierung überein. Zwei Abweichungen sind in den Primärreports bereits technisch zugeordnet:

- `Docs/agent-api.md:460` beschreibt für `includeReferences=false` einen Root-Snapshot-Default; die aktuelle Assembly-Dispatcher-/Toolkette expandiert Referenzen vor dem Handler und delegiert die `false`-Branches der Symboltools nicht an eine Root-only-Assembly-Suche. Primärbefund: `ASM-001` in Linse 01.
- `Docs/configuration.md:35` beschreibt 4 KiB Diagnosesamples je Antwort. Der aktuelle interne Sample-Cap wird in mehreren Structured-Content-Feldern erneut projiziert; die Abweichung zwischen internem Sample-Cap und globaler serialisierter Wire-Größe ist als `MCP-001` in Linse 06/07 erfasst.

## Testabdeckung

### Abschluss-Gates

| Prüfung | Ergebnis | Einordnung |
|---|---:|---|
| `dotnet build` | 0 Warnungen, 0 Fehler | grün; Produktions- und Testprojekte bauten erfolgreich |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2276 gesamt, 2274 erfolgreich, 2 übersprungen | grün; Skips stammen aus lokaler Capability-Prüfung |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 377 gesamt, 307 erfolgreich, 70 fehlgeschlagen | rot; MCP-/Daemon-Prozessabbrüche im Testhost, kein Stresslauf |

Vor dem Integrationslauf blockierte ein eindeutig identifizierter verwaister Test-Daemon die Test-Assembly. Dieser Prozess wurde nach Prüfung seiner Kommandozeile gezielt beendet; danach wurde der vollständige Lauf erneut gestartet. Der erneute Lauf erreichte den Testabschluss, blieb aber wegen der oben genannten Prozess-/Transportfehler rot. Andere, von verspäteten Reviewern zu früheren Zeitpunkten gemeldete Läufe waren teilweise grün beziehungsweise zeigten 41 statt 70 Prozessfehler. Die abweichenden Zähler bestätigen die Last-/Umgebungsabhängigkeit und sind kein belastbarer Beleg für eine stabile Produktreproduktion.

### Gezielte Gegenprüfungen

- Assembly-FastTests, Routing-/Wiring-/Capability-Slices und die Assembly-Health-E2E-Klasse wurden von den Reviewern ausgeführt; die gezielten Assembly-/Health-Verträge waren in den jeweiligen Läufen grün.
- External-Source- und Checkout-Slices deckten Akquisition, Attestation, Materialisierung, Cancellation, Cleanup und Prozessbaumverhalten ab. Die echten Reparse-Point-Tests wurden wegen fehlender lokaler Capability übersprungen.
- Git-Transporttests deckten Erfolg, Fehler, Prompt-Unterdrückung, geerbte Umgebung, Output-Cap, Timeout, Cancellation, Prozessbaum und Cleanup ab; eine echte remote authentifizierte Ausführung blieb offen.
- Kein vorhandener Test misst die vollständig serialisierte Structured-Content-Wire-Größe einschließlich aller wiederholten Diagnose-/Sessionfelder.
- Kein vorhandener Test deckt die Cancellation-Grenze zwischen erfolgreicher Akquisitionsrückgabe und lokaler Ownership-Bindung ab; dieser Gap ist Teil von `CHK-001`.
- Für Loader-/Runtime-URL-Policy-Divergenz und die produktive Credential-Resolver-Verdrahtung fehlen belastbare geschützte Remote-Tests; die unabhängige Linse 02 führt diese als `EXTSRC-01` und `EXTSRC-02`. Die konfigurierte öffentliche Mapping-Quelle wurde nachträglich live über MCP geprüft und als `EXTSRC-03` erfasst.

## Source-backed- und Decompilation-Nachweis

- Die reine Decompilation wurde mit einer neutralen lokalen Build-DLL über `inspect_assembly` und `find_assembly_extensions` geprüft. Die Antworten trugen `origin=decompiled`, keinen Source-Snapshot und `partial` wegen Referenz-/Decompilerdiagnosen. Das ist als sichtbare Partial-Semantik bewertet, nicht als vollständige Positivprobe.
- Source-backed Mapping wurde über `AssemblyAnalysisContextFactory`, Attestation-/Snapshot-Verträge und vorhandene Component-/Integrationstests geprüft. Die nachträgliche Live-Probe mit der konfigurierten gemappten DLL zeigte: MCP lädt den Gitea-Checkout mit Solution und Source-Dateien, liefert aber in `inspect_assembly` und `find_assembly_extensions` weiterhin `origin=decompiled`, `sourcePath=none`, `snapshot=none`, `status=partial` und `completeness=partial`. Das ist ein bestätigter Delivery-Fallback und keine bestandene Source-backed-Probe.

## Dokumentations- und Registrierungsabgleich

| Bereich | Code/Registrierung | Veröffentlichung | Ergebnis |
|---|---|---|---|
| Target-Paar | `AnalysisTargetResolver`, Tool-Registrierungen | `Docs/agent-api.md` und MCP-Workflow-Regeln | konsistent: `targetType` und absoluter `targetPath` |
| Assembly-Tools | Assembly-only Registrierung und Metadata-only Adapter | `Docs/agent-api.md` | konsistent für Tools, Filter und Herkunftsfelder |
| `includeReferences` | Registrierungsdefault `false`; Dispatcher-/Handlerpfad expandiert bzw. routet Root nicht wie dokumentiert | `Docs/agent-api.md:460` | Abweichung, primär `ASM-001` |
| Diagnosesamples | interne Caps und mehrfache DTO-Projektionen | `Docs/configuration.md:35` | Wire-Budget-Abweichung, primär `MCP-001` |
| Framework/Build | `Directory.Build.props` und Projektdateien auf aktuelle Zielplattform | Dokumentations-/Regelhinweise | kein Driftbefund |
| MCP-Start | `.mcp.json` startet das Projekt über `dotnet run` | Integrationsdokumentation | konsistent; separates globales Kommando nicht erforderlich |
| Agenten-Footprint | `rules.json`-Grenze 2500; Health-Builder 2502 | keine fachliche Dokuabweichung | `MCP-L6-002` als struktureller S3-Befund |

## Abdeckungsgrenzen und Disposition

- Die rote vollständige Integrationssuite wurde nach dem gezielten Entfernen eines eindeutig verwaisten Testprozesses erneut ausgeführt. Die verbleibenden Fehler sind Prozess-/Transportabbrüche und werden im Abschluss als Umgebungslimit geführt; sie rechtfertigen keine Änderung am Produktionscode im Audit-only-Scope.
- Die Einzelreports enthalten zeitlich unterschiedliche Testläufe. Der Orchestrator-Abschlusslauf ist für die Abschlusscheckliste maßgeblich; frühere grüne Reviewerläufe bleiben als Zeitpunkt-/Umgebungsnachweise erhalten.
- Die Dokumentationsabweichungen werden nicht doppelt als neue Tech-Debt-Einträge angelegt. Sie verweisen auf `ASM-001` und `MCP-001`; die testbaren Gaps werden dort als nächste Schritte wiedergegeben.
- Es wurden keine Produktions-, Test-, Konfigurations- oder veröffentlichten Dokumentationsdateien geändert; ausschließlich die Audit-Artefakte wurden um den nachträglichen Live-Nachweis ergänzt.

## Cross-Lens-Überschneidungen

| Primärbereich | Bezug |
|---|---|
| Linse 01 | `includeReferences`-Default, Assembly-Root und Root-only-Navigation (`ASM-001`, `ASM-002`) |
| Linse 02/04 | Source-backed Voraussetzungen, URL-/Credential-Verträge und Ownership-Cancellation (`EXTSRC-01`, `EXTSRC-02`, `CHK-001`) |
| Linse 05 | Cache-/Snapshot-Generation und Langzeitressourcen (`F-05-01` bis `F-05-03`) |
| Linse 06/07 | Wire-/Agentenverträglichkeit, Health-Footprint und Diagnosebudget (`MCP-L6-001`, `MCP-L6-002`, `MCP-001`, `UX-001`) |

## Verifikation

Die vollständigen Kommandos und Zähler stehen in der Testtabelle dieses Reports; sie wurden nach dem letzten Produktions-/Testquellstand ausgeführt. Kein `Stress`-Test wurde gestartet. Die Unterschiede zwischen den Reviewerläufen und dem finalen Orchestratorlauf sind als Laufzeit-/Prozessumgebungsgrenze dokumentiert.

### Commit-Vorschlag

Kein Produktionscommit; dieser Bericht ist ein Task-Artefakt des Audit-only-Auftrags.
