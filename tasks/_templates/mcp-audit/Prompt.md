# MCP-Audit: Regieanweisung & Ausführungs-Prompt

Dieses Dokument definiert die verbindlichen Regeln und den Ablauf für die Ausführung eines vollständigen, realen MCP-Server-Audits von **AiNetLinter**.

---

## 1. Oberste Grundsätze & Sicherheitsregeln

1. **Strikter Read-Only-Betrieb:**
   - Keine Quellcode-Änderungen an AiNetLinter oder an den Zielprojekten.
   - Keine Builds (`dotnet build`) und keine automatisierten Tests (`dotnet test`) starten.
   - Der Audit ist eine reine Funktions-, UX- und Schnittstellenprüfung der laufenden MCP-Tools.
2. **HARTE REGEL – Absoluter Schutz von Drittanbieter-IP & Copyright:**
   - **Niemals** echte Firmennamen, Produktnamen, Bibliotheksnamen oder Installationspfade (z. B. Drittanbieter-Software, Kundenanwendungen, proprietäre DLL-Namen oder reale Programmpfade) in Markdown-Dateien, Berichte, Findings, Logs oder Commit-Nachrichten schreiben!
   - Vor Beginn des Audits weist der Orchestrator jedem übergebenen Testziel ein anonymes Label zu (z. B. `LOCAL-01`, `LOCAL-02`, `FALSE-01`, `SOURCE-01`).
   - In allen Berichten und Finding-Beschreibungen dürfen **ausschließlich** diese Labels verwendet werden.
   - Code-Ausschnitte aus dekompilierten Fremd-Assemblies dürfen nur stark pseudonymisiert/abstrahiert zitiert werden (z. B. `MyNamespace.MyClass.MyMethod()`), niemals echte geschützte Geschäftslogik oder proprietäre Bezeichner.
3. **Ausschließlich negative Befunde dokumentieren:**
   - Erfolgreiche Aufrufe erzeugen kein Rauschen in den Berichten, sondern werden in `FlightPlan.md` einfach mit `[x]` abgehakt.
   - In den Gruppenberichten werden **ausschließlich echte Befunde** (Fehler, Budgetverletzungen, Signal-Rausch-Schwächen, Handoff-Brüche, irreführende Fehlermeldungen, fehlende Navigation) dokumentiert.
4. **Klassifizierung:**
   - Jeder Befund erhält eine ID (z. B. `A-01`, `B-02`, `C-01`) und eine Severity:
     - `[Critical]`: Blockiert Kernabläufe oder Chaining-Ketten vollständig; falscher Envelope (`isError=false` bei Fehler); Crash.
     - `[Major]`: Erschwert die agentische Arbeit erheblich; Budgettreue verletzt; Handoff erfordert manuelles Parsing; irreführende Fehlermeldung; mangelnde Datensparsamkeit.
     - `[Minor]`: Ergonomie-Schwäche, fehlende optionale Handoff-ID, uneinheitliche Beschreibungen.

---

## 2. Multi-Agenten-Architektur (Lösung des Schreibkonflikts)

Um Dateikonflikte (File-Locking / Race Conditions) zwischen parallel oder seriell arbeitenden Subagenten vollständig auszuschließen, gilt eine **strikte dateibasierte Trennung**:

```text
tasks/<audit-ordner>/
├── FlightPlan.md                    <-- Orchestrator: Zentrale Checkliste & Fortschritt
├── README.md                        <-- Orchestrator: Konsolidiertes Gesamturteil & Rangliste
├── gruppe-a-health-handshake.md     <-- Subagent A (schreibt nur hier)
├── gruppe-b-discovery.md            <-- Subagent B (schreibt nur hier)
├── gruppe-c-symbol-chaining.md      <-- Subagent C (schreibt nur hier)
├── gruppe-d-qualitaet-metriken.md   <-- Subagent D (schreibt nur hier)
└── gruppe-e-konsistenz-recovery.md  <-- Subagent E (schreibt nur hier)
```

### Rollenverteilung:
1. **Orchestrator (Haupt-Agent):**
   - Weist Targets die anonymen Labels zu und erfasst sie im Kopf von `FlightPlan.md`.
   - Spawnt für jede der fünf Gruppen (A–E) einen fokussierten Subagenten mit dem spezifischen Prüfauftrag.
   - Nimmt nach Abschluss der Subagenten die Kurzstatus entgegen, aktualisiert `FlightPlan.md` (`[x]`) und erstellt die konsolidierte Endauswertung in `README.md`.
2. **Gruppen-Subagenten (Subagent A bis E):**
   - Erhalten vom Orchestrator: die Target-Pfade, die zugehörigen anonymen Labels und ihren jeweiligen Aufgabenblock aus `FlightPlan.md`.
   - Rufen die MCP-Tools real auf.
   - Dokumentieren ihre Ergebnisse **ausschließlich in ihrer eigenen Gruppendatei** (`gruppe-<x>-*.md`).
   - Geben dem Orchestrator als Abschlussmeldung lediglich eine 3-zeilige Zusammenfassung zurück (Anzahl Critical/Major/Minor).

---

## 3. Befund-Format für Subagenten

In jeder Gruppendatei werden negative Befunde nach folgendem Standard erfasst:

```markdown
### [Severity] ID: Kurzer prägnanter Titel

- **Betroffenes Tool / Schema**: `tool_name` (Parameter: `paramName`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly) / `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `tool_name(targetPath="...", ...)`
- **Beobachtung / Ist-Verhalten**:
  - Was wurde zurückgegeben? (Statuscode, Envelope, Fehlermeldung, JSON-Auszug)
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Warum behindert oder verwirrt das einen konsumierenden Agenten? Welcher Kontrakt wird verletzt?
- **Empfehlung**:
  - Konkreter Vorschlag zur Behebung (z. B. Typhinweis, Envelope-Flag, Retry-Budget).
```

---

## 4. Typischer Aufruf im Chat

Wenn dieser Ordner als neuer Task kopiert wurde (z. B. nach `tasks/2026-09-meinaudit/`), startet der Nutzer den Audit einfach mit:

> *"Führe den MCP-Audit in `tasks/2026-09-meinaudit/FlightPlan.md` aus.*
> *Targets:*
> *- Source: `C:\...\AiNetLinter.slnx` -> Label `SOURCE-01`*
> *- Assembly: `C:\...\Fremd.dll` -> Label `LOCAL-01`*
> *- Negativ-Assembly: `C:\...\Unmanaged.exe` -> Label `FALSE-01`*
> *Befolge strikt die Regeln aus `Prompt.md` (Readonly, IP-Schutz, separate Gruppendateien)."*
