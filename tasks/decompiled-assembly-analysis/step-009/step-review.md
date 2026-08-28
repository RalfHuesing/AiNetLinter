---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 009
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T20:22:04+02:00
verdict: approved
tech_debt_ids: [TD-002, TD-003]
---

# Review Step 009: Source-backed Assembly-Context mit deterministischem Decompilation-Fallback verbinden

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: Commit `d281414747751b1370e5079f797742c34ebc1378` setzt den Source-Selection-Vertrag, die Factory-Projektion, Provenienzformatierung und fünf gezielte Regressionen um; `codemap.md` blieb gemäß Step-Auftrag unverändert.
- [x] Rules-Konformität: MCP-Violations liefern im Assembly- und Tool-Scope 0 Treffer; keine Runtime-Ladung, Reflection-Ausführung, Netzwerk- oder externe Ausführung wurde eingeführt.
- [x] Logische Korrektheit: Nur konsistente `Matched`-Auswahlen mit lebender Lease, Snapshot-Identity, vorhandenem Projekt und Compilation werden source-backed; alle geprüften Fallback-Zustände nutzen die bestehende Session-Pipeline ohne Lease-/Workspace-Übernahme.
- [x] Konzept-Treue: Source-Auflösung, Target-Identity, read-only Snapshot-Grenze, Consumer-Trennung und transparenter Decompilation-Fallback entsprechen dem Konzept; Provider-, Registry-Lookup-, MCP-, Gitea- und transitive Folgepakete bleiben außerhalb.
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün; Stress nicht ausgeführt

## Befund

### Plan-Erfüllung

Der tatsächliche Diff erfüllt den Step-Plan einschließlich der fünf Factory-/Ownership-Tests; die minimale `SourceSnapshotLease.IsDisposed`-Beobachtung ist read-only und verändert den bestehenden Lifecycle nicht.

### Rules-Konformität

Die statischen MCP-/Auditprüfungen und der Diff bestätigen immutable Request-/Selection-Werte, zentrale Test-Temp-Infrastruktur, getrennte Ownership sowie das Ausbleiben verbotener Runtime-, Netzwerk- und Plugin-Infrastruktur.

### Logische Korrektheit

`Matched` projiziert ausschließlich die gelieferte `ProjectId`-Compilation und bewahrt PE-Target-Identity, Referenzen, Hash, Source-Provenienz und `Generation=0`; `NoMatch`, `Ambiguous`, null/unavailable, Identity-Mismatch, disposed Lease und fehlendes Projekt fallen deterministisch auf `decompiled` zurück, während beide Formatter den Decompilation-Hinweis nur bei `IsDecompiled` ausgeben.

### Konzept-Treue (Ebene 4)

Die Umsetzung verwendet Source nur als read-only Roslyn-Symbolquelle, hält den externen Consumer getrennt und führt weder Source-Akquisition noch automatische Projektwahl, Assembly-Laden oder eine vorgezogene Provider-/MCP-Komposition ein.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisContextFactoryTests" → grün (5 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1911 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler; 2 m 32 s)
```

Stress-Tests wurden nicht ausgeführt. Der MCP-Impact-Aufruf für den Commit meldete trotz lokal vorhandenem Commit-Diff einen leeren Diff; die Diff-Prüfung erfolgte deshalb direkt über `git show`.

## Tech-Debt-Einträge aus diesem Review

- `TD-002` (siehe `tech-debt.md`) — Der neue Origin-Wert `source-backed` ist als untypisierte Zeichenkette nicht zentral mit den bestehenden Herkunftswerten gebündelt.
- `TD-003` (siehe `tech-debt.md`) — `AssemblyOrigin.Kind` ist im statischen Scope unreferenziert; eine Entfernung braucht wegen möglicher interner Vertrags-/Serializer-Nutzung eine separate Prüfung.

Die DRY-Prüfungen fanden keine neuen Cluster; die bereits konsolidierte `AppendOrigin`-Logik und `TD-001` wurden nicht erneut aufgerissen. Der Low-Confidence-Hinweis zur absichtlich vorbereiteten, noch nicht vom Folgeadapter aufgerufenen Service-Request-Überladung ist kein Dead-Code-Finding.
