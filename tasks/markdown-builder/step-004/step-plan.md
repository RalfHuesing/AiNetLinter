---
status: open
type: step-plan
task: markdown-builder
step: 004
corrects: null
title: "EPIC-02 Welle 2 — drei Generators-Callsites auf MarkdownBuilder umstellen + Table(MarkdownTableBuilder) produktiv"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19
related_to:
  - step-001/step-plan.md    # API-Grundlage: MarkdownTableBuilder + beide Table-Überladungen + EscapeCell-Vertrag
  - step-002/step-plan.md    # zeilenweise Public-API (BuildHeaderLine/BuildSeparatorLine/BuildRowLine) + DoD-präzisierung
  - step-003/step-plan.md    # Muster: file-only `table.AppendTo(sb)` vs. `mb.Table(t => ...)` Callback vs. `mb.Table(instance)` Instance-Überladung
---

# Step 004: EPIC-02 Welle 2 — drei Generators-Callsites auf MarkdownBuilder umstellen + Table(MarkdownTableBuilder) produktiv

## Bezug

- **Task:** `markdown-builder`
- **Epic:** `EPIC-02` aus `roadmap.md` — *„Verbleibende Callsites umstellen + HotspotSectionFormatter entfernen"*. Nach Welle 1 (`step-003`, `107b2682`, `approved`) sind `HotspotSectionFormatter`, `ListRulesCommand` und `GetSymbolBodyTool` abgehakt; **Welle 2 (dieser Step)** migriert die drei verbleibenden `Generators`-Callsites und macht die `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung produktiv (obsoleted TD-002).
- **Konzept-Referenz:** `konzept.md` §3 Prio 5 (`RepoPlaybookGenerator.AppendAgentPriority`), §3 Prio 7 (`AgentRulesGenerator.AppendMetricsTable`), §3 Prio 8 (`AgentRulesGenerator.AppendCompoundSuppressions`). §3 API-Erweiterung am Ende (Begründung der `Table(MarkdownTableBuilder)`-Überladung).

## Aktueller Projektzustand (JIT-Kontext)

EPIC-01 abgeschlossen (`step-001` `fc603681` + `step-002` `b1a39ab1`, beide `approved`); EPIC-02 Welle 1 abgeschlossen (`step-003` `107b2682`, `approved` — siehe `step-003/step-review.md`). Build sauber, FastTests 1428/1428 grün, IntegrationTests Stichproben-getrieben grün, Dogfood sauber.

Der für diesen Step relevante Bestand (gelesen, nicht geraten):

- **`src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` Z.313–335** — `private static void AppendAgentPriority(StringBuilder sb, List<RuleViolation> waveReadyViolations, Config config)`. Heading (`## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)` + Leerzeile) bereits in `sb`. Tabellen-Pattern: 3-Spalten-Tabelle `Intent | Offene Verstöße (wave-ready) | Regeln` mit Header `| :--- | ---: | :--- |` (Left / Right / Left). Sonderfall `intentGroups.Count == 0` → literal `| - | 0 | Keine offenen Verstöße |` als einzelne Row. Foreach-Schleife `sb.AppendLine($"| {Intent} | {Count} | {Rules} |")` für `intentGroups.Count > 0`. `Rules` ist `string.Join(", ", distinct-and-sorted-RuleNames)` (kein `|` enthalten, aber dynamischer Inhalt — Konzept §3 Prio 5 hebt das als Sonderfall hervor). `sb.AppendLine()` für Trailing-Blank.

- **`src/AiNetLinter/Generators/AgentRulesGenerator.cs` Z.258–269** — `private static void AppendMetricsTable(StringBuilder sb, Config config)`. Heading (`## Grenzwerte (Produktion)`) bereits in `sb`. 3-Spalten-Tabelle `Regel | Limit | Praxis` mit Header `| :--- | :---: | :--- |` (Left / Center / Left). Foreach über `RuleRegistry.All.Where(r => r.IsMetric)`; Cell-Content enthält **bewusst** Inline-Markdown-Formatierung: `` `{metric.RuleId}` `` (Backticks) und `$"**{val}**"` (Bold). Konzept §3 Prio 7 stellt explizit klar, dass `EscapeCell` Backticks/Bold nicht antastet — `|`-Escaping ist die einzige Escaping-Notwendigkeit, und `RuleId`/`AgentHint` enthalten in der Praxis kein `|`. Trailing-Blank via `sb.AppendLine()`.

- **`src/AiNetLinter/Generators/AgentRulesGenerator.cs` Z.175–196** — `private static void AppendCompoundSuppressions(StringBuilder sb, Config config)`. Heading (`## Compound Suppressions (kontextabhängige Limiten)` + Prose `Folgende Regeln gelten mit relaxiertem Limit wenn alle Bedingungen erfüllt sind:` + Leerzeile) bereits in `sb`. **5-Spalten-Tabelle** `Regel | Bedingung | Effektives Limit | Severity | Grund` mit Header `|:--|:--|:--|:--|:--|` (alle Left, Default). Foreach: dynamisch berechnete `condParts` (`c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}"`), gejoined zu `conditions` via `" AND "`. `limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert"`. `severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—"` (Em-Dash-U+2014 für „kein Override"). `reason = s.Reason ?? "—"`. Inline-Formatierung in Cells: `` `{s.TargetRule}` `` (Backticks), `$"**{s.RelaxedLimit}**"` (Bold), `` `{s.SeverityOverride}` `` (Backticks) — `EscapeCell` ist hier explizit der richtige Pfad (kein `|`/CR/LF in `Metric`/`TargetRule`/`Reason`-Strings). Trailing-Blank via `sb.AppendLine()`. **Early-Return:** `if (suppressions == null || suppressions.Count == 0) return;` — wenn keine Suppressions konfiguriert, wird weder Heading noch Tabelle emittiert (in Konzept §3 nicht explizit dokumentiert, aber im Code klar — der Coder respektiert das).

- **`src/AiNetLinter/Output/MarkdownBuilder.cs` Z.157–161** — die `Table(MarkdownTableBuilder instance)`-Überladung existiert, ist getestet, wird in `ViolationMarkdownFormatter.BuildSummaryTable` Z.97 bereits produktiv genutzt (Prio 3 in step-001). **TD-002 ist damit strenggenommen bereits obsolet** — der step-003-Result-Autor hatte das übersehen (siehe `step-003/step-result.md` Beobachtung „Table(Action<MarkdownTableBuilder>) ... TD-002 ... ist damit immer noch nicht obsolet"). Der Orchestrator-Auftrag für step-004 ist explizit: **diese Überladung auch in den `Generators`-Callsites produktiv einsetzen** — das macht das Pattern **konsistent** über die Codebase (nicht nur in einem Einzelfall), und liefert die expliziten Aufrufstellen, gegen die künftige Kritiker die API-Konsistenz verifizieren können.

**Bestehende Strukturen, die wiederverwendet werden:**

- `MarkdownTableBuilder` (mit `AddColumn`/`AddRow`/`EscapeCell`/`BuildHeaderLine`/`BuildSeparatorLine`/`BuildRowLine`/`FormatRow`/`AppendTo`/`Build`) — vollständig vorhanden, byte-stabil verriegelt durch 30 `MarkdownBuilderTests` (siehe `step-002/step-result.md`).
- `MarkdownBuilder.Table(MarkdownTableBuilder instance)` Z.157–161 — existiert und ist durch `TableCallback_und_InstanceUeberladung_GleicherOutput` getestet; produktiv genutzt bereits in `ViolationMarkdownFormatter.BuildSummaryTable`.
- `ColumnAlign.Right` für `Offene Verstöße (wave-ready)` (Prio 5), `ColumnAlign.Center` für `Limit` (Prio 7) — bereits in `MarkdownTableBuilder` Enum definiert.
- Bestehende Tests, die als Output-Vertrag gelten:
  - **`PlaybookGeneratorRound2Tests.BuildContentAsync_SortsIntentsAndRulesDeterministically`** Z.125–159 assertet byte-exakte Substrings `"| agent-context | 2 | MaxLineCount, MaxMethodLineCount |"`, `"| coupling | 1 | MaxConstructorDependencies |"`, `"| general | 1 | MaxSwitchArms |"` (3 Rows aus `AppendAgentPriority`); diese müssen nach der Migration unverändert grün sein. **Risiko:** keines — die Strings enthalten kein `|`, kein CR/LF, kein Leading/Trailing-Whitespace; `EscapeCell` ist ein No-Op, `MarkdownTableBuilder.BuildRowLine` produziert byte-identische `| a | b | c |`-Form.
  - **Übrige `PlaybookGeneratorRound2Tests`** nutzen nur `Assert.Contains` mit Headings/Substrings (`"AI Repository Playbook (Auto-Generated)"`, `"Result-Pattern-Nutzung:** 0"` etc.) — tolerant gegenüber Tabellen-Detail-Drift, kein Risiko.
  - **`SyncAgentRulesPolicyTests`** testet nur `ResolveBaseDirectory`/`ResolveAgentRulesPath`/`DetectBaselineUsage`/`RunSyncAgentRules_With*`/etc. — Pfad- und Detection-Logik, **nicht** Output-Inhalt von `AppendMetricsTable`/`AppendCompoundSuppressions`. Kein Risiko.
  - **Dogfood-Suite** `dotnet run -- --config rules.json --path .` emittiert `.agents/rules/AiNetLinter.mdc` und das Repo-Playbook. Der Coder MUSS die generierte `.mdc` und `tasks/markdown-builder/.../REPO_PLAYBOOK.md` (oder wo der Generator schreibt) **vor dem Commit mit der Vor-Step-Version vergleichen** — siehe DoD.

**Anti-Loop-Check gegen CodeMap (`tasks/markdown-builder/codemap.md`):**

- CodeMap Z.76 (`RepoPlaybookGenerator.cs` — *Prio 5 (EPIC-02)*) und Z.77 (`AgentRulesGenerator.cs` — *Prio 7 + 8 (EPIC-02)*) sind explizit für diesen Step markiert; keine früheren Entscheidungen widersprechen.
- CodeMap-Hinweis: `PathNormalizer.cs` wird von `RepoPlaybookGenerator.AppendAgentPriority` für Dateipfade benutzt (siehe `RepoPlaybookGenerator.cs:343`); die Migration ändert nichts an `PathNormalizer` — irrelevant für diesen Step, aber der Coder sollte den `Table(...)`-Aufruf nicht versehentlich in `PathNormalizer.cs` vermuten.
- **Wichtig:** `MarkdownTableBuilder` ist im step-002-Refactor Single-Source-of-Truth für Header-/Separator-/Row-Format. Die Migration der drei `Generators`-Callsites nutzt **nur** `MarkdownTableBuilder`-Methoden (kein Inline-`string.Join` + `align switch` — das war der Hauptbefund von `step-001/step-review.md` Finding #1, der durch `step-002` aufgelöst wurde und durch diesen Step nicht reaktiviert werden darf).

**Bestehende Konvention (verifiziert in `AiNetLinterRichtlinien.mdc` + `AiNetLinter.mdc`):** `sealed` auf konkrete Klassen (in diesem Step nicht relevant — keine neuen Klassen, `AppendAgentPriority`/`AppendMetricsTable`/`AppendCompoundSuppressions` sind `private static`); `#nullable enable` am Dateianfang (bereits in beiden `Generators`-Dateien Z.1); `MaxMethodLineCount: 60` (alle drei Methoden sind aktuell ≤23 Z., nach Migration ≤30 Z. — weit unter Limit); `MaxMethodParameterCount: 4` (`AppendAgentPriority` hat 3 Parameter `sb`/`waveReadyViolations`/`config` — bleibt; `AppendMetricsTable` und `AppendCompoundSuppressions` haben 2 Parameter — bleibt); xUnit v3 + `[Trait("Category", "…")]` (bestehende Tests, keine neuen); keine `// step-NNN`/`// EPIC-NN`/Task-IDs in Code-Kommentaren (Richtlinie §5); sparsame Kommentare; `Result<T>` bevorzugt (nicht relevant — reine Side-Effect-Methoden).

## Intention

Nach diesem Step sind die drei verbleibenden `Generators`-Callsites (`RepoPlaybookGenerator.AppendAgentPriority`, `AgentRulesGenerator.AppendMetricsTable`, `AgentRulesGenerator.AppendCompoundSuppressions`) auf `MarkdownTableBuilder` umgestellt — `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung wird in allen drei Methoden produktiv genutzt (per lokalem `MarkdownBuilder mb` + `mb.Table(table)` + `mb.AppendTo(sb)`, exakt das Pattern aus `ViolationMarkdownFormatter.BuildSummaryTable`), das macht das Pattern **konsistent über die Codebase** und obsoleted TD-002. Die `PlaybookGeneratorRound2Tests`-byte-exakten Substrings bleiben unverändert grün (kein Escaping-Drift), die generierten `.mdc`/Playbook-Bytes sind gegen die Vor-Step-Version strukturell identisch (Header, Separator, Spaltenreihenfolge, Sonderfall-Leer-Row, Reihenfolge, Leerzeilen), und alle bestehenden Tests bleiben grün. `RepoPlaybookGenerator.cs` braucht keinen neuen `using`-Import (`AiNetLinter.Output` ist bereits Z.13); `AgentRulesGenerator.cs` braucht `using AiNetLinter.Output;` (neu Z.11) für `MarkdownTableBuilder`/`ColumnAlign`/`MarkdownBuilder`.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` (Z.313–335, Prio 5)

- **Was:** In `AppendAgentPriority` Z.313–335 die rohen `sb.AppendLine("| … |")`-Zeilen Z.317, Z.318, Z.327, Z.332 durch ein `MarkdownTableBuilder`-Instanz-Pattern ersetzen:
  - Z.315–316 (`sb.AppendLine("## 5. …")` + `sb.AppendLine()`) bleibt **unverändert** (Heading ist nicht Teil der Tabellen-Migration).
  - Z.317–318 (`sb.AppendLine("| Intent | … | Regeln |")` + `sb.AppendLine("| :--- | ---: | :--- |")`) wird ersetzt durch:
    ```csharp
    var table = new MarkdownTableBuilder()
        .AddColumn("Intent")
        .AddColumn("Offene Verstöße (wave-ready)", ColumnAlign.Right)
        .AddColumn("Regeln");
    ```
  - Z.319–324 (`var intentGroups = … .ToList();`) bleibt **unverändert** (Daten-Vorbereitung).
  - Z.325–333 (`if (intentGroups.Count == 0) { … } else { foreach … }`) wird ersetzt durch:
    ```csharp
    if (intentGroups.Count == 0)
    {
        table.AddRow("-", 0, "Keine offenen Verstöße");
    }
    else
    {
        foreach (var group in intentGroups)
            table.AddRow(group.Intent, group.Count, group.Rules);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    ```
  - Z.334 (`sb.AppendLine();`) bleibt **unverändert** (Trailing-Blank nach Tabelle).
- **Warum:** Konzept §3 Prio 5 + Orchestrator-Auftrag („Instanz-Überladung produktiv nutzen — TD-002 obsoleted sich hier"). Das lokal erzeugte `mb` + `mb.Table(table)` + `mb.AppendTo(sb)` ist exakt das Pattern aus `ViolationMarkdownFormatter.BuildSummaryTable` Z.94–98 — konsistent über die Codebase. Der Sonderfall `intentGroups.Count == 0` ist eine literale Row (`-`, `0`, `Keine offenen Verstöße`); `0` ist `int` und wird via `ToString()` zu `"0"` in `AddRow`, byte-stabil zum Original. `EscapeCell` auf Header `"Intent"`, `"Offene Verstöße (wave-ready)"`, `"Regeln"` ist ein No-Op (kein `|`/CR/LF, kein Whitespace-Only). `EscapeCell` auf Cell-Values (`Intent` wie `"agent-context"`, `Count` als int, `Rules` wie `"MaxLineCount, MaxMethodLineCount"`) ist ein No-Op. **`PlaybookGeneratorRound2Tests.BuildContentAsync_SortsIntentsAndRulesDeterministically`-byte-exakte Substrings bleiben unverändert.**
- **Risiko-Hinweis:** Die `Rules`-Spalte enthält `string.Join(", ", distinct-and-sorted-RuleNames)` — RuleNames sind C#-Identifiers (`EnforceSealedClasses`, `MaxLineCount`, …), in der Praxis nie mit `|`. Falls in einer zukünftigen Config ein RuleName mit `|` konfiguriert würde (extrem unwahrscheinlich, da `|` in `RuleId` ein Konfig-Validierungsfehler wäre), würde `EscapeCell` ihn zu `\|` escapen — das ist genau der gewünschte Builder-Vertrag, der jetzt für `AppendAgentPriority` zum Tragen kommt (vorher: stillschweigend defekte Tabelle). Kein Action-Item.
- **Keine** `using`-Änderung: `using AiNetLinter.Output;` ist Z.13 bereits vorhanden.

### Datei 2: `src/AiNetLinter/Generators/AgentRulesGenerator.cs` (Z.175–196, Prio 8; Z.258–269, Prio 7)

- **Was (Prio 8 — `AppendCompoundSuppressions` Z.175–196):** In der Methode die rohen `sb.AppendLine("| … |")`-Zeilen Z.182, Z.183, Z.193 durch ein `MarkdownTableBuilder`-Instanz-Pattern ersetzen:
  - Z.178 (`if (suppressions == null || suppressions.Count == 0) return;`) bleibt **unverändert** (Early-Return).
  - Z.180–181 (Heading `## Compound Suppressions …` + Prose `Folgende Regeln gelten …\n`) bleibt **unverändert**.
  - Z.182–183 (Header + Separator) wird ersetzt durch:
    ```csharp
    var table = new MarkdownTableBuilder()
        .AddColumn("Regel")
        .AddColumn("Bedingung")
        .AddColumn("Effektives Limit")
        .AddColumn("Severity")
        .AddColumn("Grund");
    ```
  - Z.185–194 (Foreach mit `condParts`/`conditions`/`limit`/`severity`/`reason`-Berechnung + `sb.AppendLine(...)`) wird ersetzt durch:
    ```csharp
    foreach (var s in suppressions)
    {
        var condParts = s.WhenAllOf.Select(c =>
            c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}");
        var conditions = string.Join(" AND ", condParts);
        var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
        var severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—";
        var reason = s.Reason ?? "—";
        table.AddRow($"`{s.TargetRule}`", conditions, limit, severity, reason);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    ```
  - Z.195 (`sb.AppendLine();`) bleibt **unverändert** (Trailing-Blank).
- **Was (Prio 7 — `AppendMetricsTable` Z.258–269):** In der Methode die rohen `sb.AppendLine("| … |")`-Zeilen Z.261, Z.262, Z.266 durch ein `MarkdownTableBuilder`-Instanz-Pattern ersetzen:
  - Z.260 (`sb.AppendLine("## Grenzwerte (Produktion)");`) bleibt **unverändert**.
  - Z.261–262 (Header + Separator) wird ersetzt durch:
    ```csharp
    var table = new MarkdownTableBuilder()
        .AddColumn("Regel")
        .AddColumn("Limit", ColumnAlign.Center)
        .AddColumn("Praxis");
    ```
  - Z.263–267 (Foreach) wird ersetzt durch:
    ```csharp
    foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
    {
        var val = metric.GetMetricLimit != null ? metric.GetMetricLimit(config) : 0;
        table.AddRow($"`{metric.RuleId}`", $"**{val}**", metric.AgentHint);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    ```
  - Z.268 (`sb.AppendLine();`) bleibt **unverändert** (Trailing-Blank).
- **Warum (Prio 7 + 8):** Konzept §3 Prio 7 + §3 Prio 8 + Orchestrator-Auftrag („Instanz-Überladung produktiv nutzen"). Beide Methoden folgen demselben lokalen `mb`-Pattern wie Prio 5 (und wie `BuildSummaryTable` in der Codebase) — `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung wird in **drei weiteren Aufrufstellen** produktiv. **5-Spalten-Tabelle in Prio 8** ist byte-stabil: Header-Werte (`Regel`/`Bedingung`/`Effektives Limit`/`Severity`/`Grund`) und Separator (alle Left, Default) sowie Cell-Inhalte (`` `{s.TargetRule}` ``, `conditions` als `string.Join(" AND ", …)`, `$"**{s.RelaxedLimit}**"`, `` `{s.SeverityOverride}` ``, `s.Reason ?? "—"`) werden durch `EscapeCell` nicht transformiert (kein `|`/CR/LF, kein Whitespace-Only). **3-Spalten-Tabelle in Prio 7** analog: `EscapeCell` lässt Backticks/Bold unangetastet, `metric.RuleId`/`metric.AgentHint` enthalten in der Praxis kein `|`. `Limit`-Spalte ist `ColumnAlign.Center` → `":---:"` im Separator (exakt deckungsgleich mit Original `| :---: |`).
- **`using`-Änderung in `AgentRulesGenerator.cs`:** **neu** `using AiNetLinter.Output;` als Z.11 einfügen (zwischen `using AiNetLinter.Core;` Z.10 und der `namespace`-Deklaration). Notwendig für `MarkdownTableBuilder`, `MarkdownBuilder`, `ColumnAlign`. **Vor** dem Einchecken mit `rg "MarkdownTableBuilder|MarkdownBuilder|ColumnAlign" -n src\AiNetLinter\Generators\AgentRulesGenerator.cs` verifizieren, dass nach der Migration tatsächlich alle drei Symbole referenziert werden — sonst wäre der Import unnötig.
- **Risiko-Hinweis Sonderfall `s.WhenAllOf == null` (Prio 8):** Der Foreach-Code iteriert über `suppressions` (nicht null dank Early-Return Z.178) und für jeden Eintrag über `s.WhenAllOf.Select(...)`. Wenn `s.WhenAllOf` selbst leer ist (kein Eintrag in `WhenAllOf`), gibt `string.Join(" AND ", [])` einen Leerstring zurück, der via `EscapeCell` zu `"-"` wird (Whitespace-Only). Konsequenz: leere `Bedingung`-Cell wird als `-` emittiert statt als Leerstring. **Das ist eine Vertragskonsequenz** (gleiche Logik wie `ViolationMarkdownFormatter.BuildSummaryTable` Cell-Padding aus step-001/002), kein Drift. **Aktuell** emittierte der Originalcode für leere `WhenAllOf` ein `| … | |` (leere Cell zwischen Pipes); nach der Migration `| … | - |`. **Risiko:** niedrig — wenn ein Agent-Consumer auf den Leerstring angewiesen ist, gibt es keine Test-Abdeckung, die das belegen würde; der Vertrag ist `EscapeCell`-konform (`Whitespace-Only → "-"`). Der Coder **muss** das in `step-004/step-result.md` „Beobachtungen" dokumentieren (siehe DoD).
- **Risiko-Hinweis `c.AtMost == null && c.AtLeast == null` (Prio 8):** Konzept-Code nimmt an, dass mindestens eine der beiden Komponenten gesetzt ist (Schema-Validierung); falls nicht, würde `$"{c.Metric} ≤ "` (leerer Wert) emittiert. **Konfiguration-Validierung ist nicht Scope dieses Steps** — der Coder verhält sich exakt wie der Originalcode (`c.AtMost.HasValue ? … : …`), keine zusätzliche Validierung.

## Tests

- [ ] **Keine neuen Unit-Tests** — Begründung: keine _neue_ Builder-API wird eingeführt; `MarkdownTableBuilder`/`MarkdownBuilder.Table(MarkdownTableBuilder)`/`ColumnAlign.Center` sind bereits durch die 30 bestehenden `MarkdownBuilderTests` (step-001 + 6 step-002) verriegelt. Die drei Callsites emittieren **strukturell byte-stabile** Markdown-Tabellen (kein neuer Content, gleiche Header/Spalten/Sonderfall-Rows/Reihenfolge, nur das `sb.AppendLine("| … |")`-Bastel wird durch `MarkdownTableBuilder.AddRow` + `BuildRowLine` + `FormatRow` ersetzt — dieselbe Pipe-Konstruktion). Tests würden nur das testen, was die bestehenden Tests ohnehin schon testen.
- [ ] **Bestehende Tests, die als byte-stabile Verträge verifizieren (alle grün):**
  - [ ] `src/AiNetLinter.FastTests/Core/PlaybookGeneratorRound2Tests` — `BuildContentAsync_SortsIntentsAndRulesDeterministically` Z.125–159 (byte-exakte Substrings `| agent-context | 2 | MaxLineCount, MaxMethodLineCount |` etc.) — **verifiziert Prio 5 Byte-Stabilität**.
  - [ ] `src/AiNetLinter.FastTests/Commands/SyncAgentRulesPolicyTests` — alle Tests, insbesondere `RunSyncAgentRules_*`-Pfade, die `AiNetLinter.mdc` auf Disk schreiben und den Inhalt verifizieren — **verifiziert Prio 7 + 8 Byte-Stabilität im generierten `.mdc`**.
  - [ ] `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests` — 30/30 grün (Regression-Schutz für `MarkdownTableBuilder`/`MarkdownBuilder`/`ColumnAlign`).
  - [ ] `src/AiNetLinter.FastTests/Mcp/Tools/GetViolationsToolTests` — 16/16 grün (Regression-Schutz EPIC-01 Prio 2).
  - [ ] `src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests` — 31/31 grün (Regression-Schutz EPIC-01 Prio 3).
  - [ ] `src/AiNetLinter.FastTests/Mcp/Tools/GetHotspotsToolTests` — 11/11 grün (Regression-Schutz EPIC-02 Prio 4).
  - [ ] `src/AiNetLinter.FastTests/Commands/ListRulesCommandTests` — 8/8 grün (Regression-Schutz EPIC-02 Prio 6).
  - [ ] `src/AiNetLinter.FastTests/Mcp/Tools/GetSymbolBodyToolTests` — 6/6 grün (Regression-Schutz EPIC-02 Prio 10).
  - [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün (≥1428 Tests, inkl. aller oben genannten).
  - [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün (Stichproben-getrieben wie in step-001/002/003: `CliRepositoryDogfoodTests`, `BaselineCliTests`, `McpServerCommand*Tests` außer `metrics_lookup`-spezifische — die sind in step-005 dran).
  - [ ] `dotnet build` grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors = true`).
  - [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .` grün (Dogfood-Suite sauber; verifiziert, dass die generierten `.mdc` und Playbook-Bytes gegen die Vor-Step-Version strukturell identisch sind — manueller `git diff .agents/rules/AiNetLinter.mdc`-Vergleich **vor** dem Commit, siehe DoD).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" in Datei 1–2 umgesetzt (2 Dateien modifiziert: `RepoPlaybookGenerator.cs` Prio 5; `AgentRulesGenerator.cs` Prio 7 + 8 + 1 neuer `using AiNetLinter.Output;`-Import).
- [ ] `dotnet build` grün, ohne neue Warnings (`TreatWarningsAsErrors = true`).
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün (≥1428 Tests, inkl. `PlaybookGeneratorRound2Tests.BuildContentAsync_SortsIntentsAndRulesDeterministically` byte-stabil).
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün (Stichproben-getrieben).
- [ ] **`using AiNetLinter.Output;` in `AgentRulesGenerator.cs` Z.11 hinzugefügt** (zwischen `using AiNetLinter.Core;` und `namespace`-Deklaration); `RepoPlaybookGenerator.cs` unverändert (Import bereits Z.13).
- [ ] **Strukturelle Byte-Stabilität der generierten Markdown-Dateien** manuell verifiziert per `git diff .agents/rules/AiNetLinter.mdc tasks/markdown-builder/.../REPO_PLAYBOOK.md` (oder wo der Generator schreibt) gegen die Vor-Step-Version: Header, Separator, Spaltenreihenfolge, Sonderfall-Leer-Row, Reihenfolge, Leerzeilen müssen identisch sein. **Cell-Content-Diff** ist erlaubt, wenn er `EscapeCell`-konform ist (z. B. `Whitespace-Only` → `-`, was nur den hypothetischen `s.WhenAllOf == []`-Fall in Prio 8 betrifft — der Coder dokumentiert das in `step-result.md`).
- [ ] **Commit-Disziplin:** ein Commit (analog zu step-001/002/003 — keine Zwischen-Commits, weil die drei Callsites thematisch zusammengehören und eine Teil-Migration ohne Builder-Konsistenz keinen Review-Mehrwert bringt). Conventional Commit auf Deutsch, imperativ, Suffix `[markdown-builder]`, Body-Trailer `Refs: tasks/markdown-builder/step-004`. **Roadmap-Diff** wird vom Orchestrator separat angewendet (siehe Notes).
- [ ] **TD-002-Obsoleting in `tech-debt.md` Z.31–34:** Index-Eintrag TD-002 von „Status: offen" auf **„Status: obsolet (durch `step-004`)"** ändern, Kurzfassung ggf. präzisieren (siehe Vorschlag in den Notes). **Volltext-Eintrag** TD-002 Z.61–90: Status-Zeile auf „Status: **obsolet** — behoben in `step-004` (`AppendAgentPriority` + `AppendMetricsTable` + `AppendCompoundSuppressions` nutzen jetzt `MarkdownBuilder.Table(MarkdownTableBuilder)` per lokalem `mb`-Pattern; ursprünglich Prio 4 in `step-003` mit `Table(t => …)`-Callback umgesetzt, der die Instanz-Überladung ungenutzt ließ; nach `step-004` ist die Überladung in **vier** Callsites produktiv — `BuildSummaryTable` Prio 3, plus die drei `Generators`-Callsites in `step-004`). Hinweis: **die Tech-Debt-Index-/Volltext-Änderung kann im selben Commit** wie die Code-Migration erfolgen, weil sie reine Spiegelung der Code-Wahrheit ist (kein eigenständiger Refactor) — das ist konsistent mit der Konvention „auto_fixable: nein" → manuelle Pflege. **Falls** der Coder zögert (Orchestrator-Auftrag war „Tech-Debt-Index lesen", nicht „ändern"), dann den Status-Wechsel als expliziten DoD-Punkt im `step-result.md` „Beobachtungen" dokumentieren und dem Orchestrator zur separaten Bestätigung melden — nicht eigenmächtig ändern.
- [ ] **TD-001 bleibt unverändert** (nicht Scope dieses Steps — `ViolationMarkdownFormatter.cs:40` Header raw `sb.Append` ist in step-005 dran).
- [ ] **CodeMap-Update** durch den Coder im Doku-Commit: `RepoPlaybookGenerator.cs` und `AgentRulesGenerator.cs`-Einträge von „*Prio 5/7+8 (EPIC-02)*" auf „*umgebaut (EPIC-02, step-004, Prio 5/7+8)*" umschreiben (analog zum bestehenden `ListRulesCommand.cs` „*umgebaut (EPIC-02, step-003, Prio 6)*"-Eintrag in CodeMap Z.78). Der „*Prio 9 (EPIC-02)*"-Eintrag für `MetricsLookupFormatter.cs` (CodeMap Z.72) bleibt für step-005.
- [ ] **Beobachtung in `step-result.md`**: explizite Dokumentation (a) der `s.WhenAllOf == []`-EscapeCell-Konsequenz für Prio 8 (Cell wird `"-"` statt Leerstring) mit Verweis auf step-001-DoD-Präzisierung, (b) der konsistenten Pattern-Wahl `mb.Table(table)` über drei Callsites (Pattern-Konsistenz mit `BuildSummaryTable`), (c) der obsoleten TD-002 mit explizitem Hinweis auf die vier produktiven Aufrufstellen.
- [ ] `tasks/markdown-builder/step-004/step-result.md` geschrieben mit: tatsächlich committeter Hash, Build-/Test-Output, Beobachtungen, ggf. Abweichungen vom Plan.
- [ ] `status` in dieser `step-plan.md` von `open` auf `done (pending audit)` gesetzt.

## Rules-Refs

- **`.agents/rules/AiNetLinter.mdc`** — harte Code-Qualitätsmetriken: `MaxLineCount: 500` (beide `Generators`-Dateien <500 Z. ohnehin, kein Risiko); `MaxMethodLineCount: 60` (`AppendAgentPriority` aktuell 23 Z. → nach Migration ca. 28 Z.; `AppendMetricsTable` 12 Z. → ca. 17 Z.; `AppendCompoundSuppressions` 22 Z. → ca. 28 Z. — alle weit unter 60); `MaxMethodParameterCount: 4` (unverändert, alle drei Methoden ≤3 Parameter); `MaxCyclomaticComplexity: 12` / `MaxCognitiveComplexity: 15` (Foreach-Body + `if`/`else`-Verzweigung — beide Metriken unverändert zur Originalimplementierung); `Sealed-Pflicht` auf konkrete Klassen (in diesem Step nicht relevant — `RepoPlaybookGenerator` ist bereits `sealed` Z.23, `AgentRulesGenerator` ist `public static` Z.28 und braucht kein `sealed`).
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** §1 (Doku-Objektivität: nur Implementiertes dokumentieren, keine Superlative), §3 (Windows/PowerShell-Workflow, `dotnet test` TRX-Logging bei Failures), §5 (sparsame Code-Kommentare, **kein** Verweis auf `step-004`/`EPIC-02` im Code, Zero-Warning-Direktive), §6 (MCP-Dogfooding — C#-Symbol-Queries via `ainetlinter`-MCP-Server vor `rg`/`grep`).
- **Konvention Pflicht-Suffix `[markdown-builder]`** im Subject jedes Task-Commits (auch in Code-Commits), Kurzname = `tasks/markdown-builder` → `markdown-builder` (siehe `roadmap.md` Tech-Stack-Notiz Z.41).
- **Body-Trailer** `Refs: tasks/markdown-builder/step-004` (analog zu step-001/002/003).

## Bekannte Ausnahmen

- **`s.WhenAllOf == []`-Edge-Case in `AppendCompoundSuppressions` (Prio 8):** Wenn ein Suppression-Eintrag mit leerer `WhenAllOf`-Liste konfiguriert wäre (Schema-Validierung verlangt eigentlich ≥1 Eintrag — Konzept §3 Prio 8 zeigt das Foreach-Pattern ohne Validierung), würde `string.Join(" AND ", [])` einen Leerstring zurückgeben; `EscapeCell` würde ihn zu `"-"` machen. Der **Originalcode** emittierte an dieser Stelle einen Leerstring (nicht `-`); der **migrierte Code** emittiert `"-"`. Konsequenz: **1-Byte-Drift in einem hypothetischen Edge-Case**, der in der Praxis nicht auftritt (kein Test deckt ihn ab, kein `rules.json` konfiguriert ihn, der generierte `.mdc` enthält ihn nicht). Konsequenz ist `EscapeCell`-Vertrags-konform (Whitespace-Only → `-`, siehe `roadmap.md` Z.62 EPIC-01-DoD-Präzisierung) und konsistent mit dem Verhalten in `ViolationMarkdownFormatter.BuildSummaryTable` (Prio 3, gleiche `EscapeCell`-Konsequenz). **Aktion:** in `step-result.md` „Beobachtungen" dokumentieren; **kein** Test-Regressions-Schutz nötig (kein Test existiert).
- **TD-002-Obsoleting-Timing:** Der TD-002-Volltext in `tech-debt.md` Z.61–90 wurde im step-001-Review als „API-Reserve für EPIC-02" beschrieben. Nach `step-003` (Callback-Variante produktiv in `AppendHotspotSection`) und `step-004` (Instanz-Überladung produktiv in drei weiteren Callsites) ist die API-Reserve aufgebraucht. **Der Coder aktualisiert den TD-002-Eintrag** (Status: obsolet) **im selben Commit wie die Code-Migration** (Begründung: reine Spiegelung der Code-Wahrheit, kein eigenständiger Refactor — konsistent mit der `auto_fixable: nein`-Konvention). **Falls** der Coder die Status-Änderung nicht im Code-Commit machen will (Orchestrator-Auftrag war „Tech-Debt-Index lesen", nicht „ändern"), dann im `step-result.md` „Beobachtungen" explizit dokumentieren und dem Orchestrator melden — Orchestrator kann den Doku-Commit dann selbst anwenden.
- **Pre-Commit-Vergleich der generierten `.mdc`/Playbook-Bytes:** Der `dotnet run -- --config rules.json --path .` erzeugt eine neue `.agents/rules/AiNetLinter.mdc` (über `AgentRulesGenerator`-Pfade) und ein Repo-Playbook (über `RepoPlaybookGenerator`-Pfade). Der Coder MUSS vor dem Commit mit `git diff` verifizieren, dass die strukturellen Bestandteile (Header, Separator, Spaltenreihenfolge, Sonderfall-Leer-Row, Reihenfolge, Leerzeilen) identisch sind. Falls unerwartete Drifts auftreten (z. B. ein `RuleName` enthält ein `|`-Zeichen — sehr unwahrscheinlich, aber theoretisch möglich bei zukünftigen Config-Erweiterungen), ist das ein **gewünschter** Effekt von `EscapeCell` (sonst wäre die Tabelle defekt) — der Coder dokumentiert den Befund im Commit-Body.
- **`MetricsLookupFormatter.cs`-DoD aus step-003 Result Beobachtung „TD-001 wird in diesem Step NICHT angehängt":** TD-001 betrifft `ViolationMarkdownFormatter.cs:40` Header, **nicht** diesen Step. **Nicht** anpassen, Status bleibt „offen" — gehört zu step-005.

## Code-Skizze (optional)

**`RepoPlaybookGenerator.AppendAgentPriority` (Prio 5, ~28 Z. nach Migration):**

```csharp
private static void AppendAgentPriority(StringBuilder sb, List<RuleViolation> waveReadyViolations, Config config)
{
    sb.AppendLine("## 5. Empfohlene Agenten-Priorität (aus RuleMetadata + Counts)");
    sb.AppendLine();
    var table = new MarkdownTableBuilder()
        .AddColumn("Intent")
        .AddColumn("Offene Verstöße (wave-ready)", ColumnAlign.Right)
        .AddColumn("Regeln");
    var intentGroups = waveReadyViolations
        .GroupBy(v => RuleMetadataRegistry.Resolve(v.RuleName ?? "", config).Intent)
        .Select(g => new { Intent = g.Key, Count = g.Count(), Rules = string.Join(", ", g.Select(v => v.RuleName).Distinct().OrderBy(r => r, StringComparer.Ordinal)) })
        .OrderByDescending(x => x.Count)
        .ThenBy(x => x.Intent, StringComparer.Ordinal)
        .ToList();
    if (intentGroups.Count == 0)
    {
        table.AddRow("-", 0, "Keine offenen Verstöße");
    }
    else
    {
        foreach (var group in intentGroups)
            table.AddRow(group.Intent, group.Count, group.Rules);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    sb.AppendLine();
}
```

**`AgentRulesGenerator.AppendMetricsTable` (Prio 7, ~17 Z. nach Migration):**

```csharp
private static void AppendMetricsTable(StringBuilder sb, Config config)
{
    sb.AppendLine("## Grenzwerte (Produktion)");
    var table = new MarkdownTableBuilder()
        .AddColumn("Regel")
        .AddColumn("Limit", ColumnAlign.Center)
        .AddColumn("Praxis");
    foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
    {
        var val = metric.GetMetricLimit != null ? metric.GetMetricLimit(config) : 0;
        table.AddRow($"`{metric.RuleId}`", $"**{val}**", metric.AgentHint);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    sb.AppendLine();
}
```

**`AgentRulesGenerator.AppendCompoundSuppressions` (Prio 8, ~28 Z. nach Migration):**

```csharp
private static void AppendCompoundSuppressions(StringBuilder sb, Config config)
{
    var suppressions = config.Metrics.CompoundSuppressions;
    if (suppressions == null || suppressions.Count == 0) return;

    sb.AppendLine("## Compound Suppressions (kontextabhängige Limiten)");
    sb.AppendLine("Folgende Regeln gelten mit relaxiertem Limit wenn alle Bedingungen erfüllt sind:\n");
    var table = new MarkdownTableBuilder()
        .AddColumn("Regel")
        .AddColumn("Bedingung")
        .AddColumn("Effektives Limit")
        .AddColumn("Severity")
        .AddColumn("Grund");

    foreach (var s in suppressions)
    {
        var condParts = s.WhenAllOf.Select(c =>
            c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}");
        var conditions = string.Join(" AND ", condParts);
        var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
        var severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—";
        var reason = s.Reason ?? "—";
        table.AddRow($"`{s.TargetRule}`", conditions, limit, severity, reason);
    }
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    sb.AppendLine();
}
```

## Notes

- **Pattern-Wahl `mb.Table(table)` vs. `table.AppendTo(sb)` direkt:** Der Orchestrator-Auftrag war explizit *„`Table(MarkdownTableBuilder)`-Instanz-Überladung produktiv nutzen — TD-002 obsoleted sich hier"*. Drei Alternativen wurden evaluiert:
  1. **`table.AppendTo(sb)` direkt** (Prio 6 in step-003 hat das so gemacht) — minimal, aber nutzt die `Table(MarkdownTableBuilder)`-Methode nicht.
  2. **`var mb = new MarkdownBuilder(); mb.Table(table); mb.AppendTo(sb);`** (gewählt) — expliziter Aufruf der Instanz-Überladung, exakt das Pattern aus `ViolationMarkdownFormatter.BuildSummaryTable` Z.94–98, macht TD-002-Obsoleting sichtbar im Code.
  3. **Refactor Method-Signatur auf `MarkdownBuilder mb`** statt `StringBuilder sb` — invasiver, ändert 4 Aufrufer in `AgentRulesGenerator` + 1 in `RepoPlaybookGenerator` + deren jeweilige Top-Level-Methoden, jenseits dieses Steps.
  Wahl: **2**. Der eine zusätzliche `var mb` + 2 Method-Calls pro Methode ist akzeptabler Overhead für die explizite Sichtbarkeit der Instanz-Überladungs-Nutzung — die Step-DoD verlangt diese Wahl explizit.
- **TD-002 ist strenggenommen bereits obsolet (Korrektur der step-003-Beobachtung):** `ViolationMarkdownFormatter.BuildSummaryTable` Z.97 (`mb.Table(table)`) nutzt die Instanz-Überladung **bereits seit step-001** (Prio 3). step-003's Result-Autor hat das übersehen (siehe `step-003/step-result.md` Beobachtung „TD-002 ... ist damit immer noch nicht obsolet"). Step-004 macht das Pattern **konsistent** über die Codebase: nach diesem Step ist die Instanz-Überladung in **vier** Callsites produktiv (`BuildSummaryTable` + die drei `Generators`-Callsites), nicht nur in einer. **TD-002 wird trotzdem erst in step-004 obsolet** — der Index-Eintrag wurde in step-001 angelegt mit der expliziten Begründung „produktive Nutzung erst in EPIC-02 (Prio 4/5)" und der Verweis auf step-004 als Trigger. Die Status-Änderung von „offen" auf „obsolet" erfolgt also genau wie geplant.
- **Verifikations-Reihenfolge** (Empfehlung an den Coder):
  1. `dotnet build` (Typ-Check, vor allem wegen des neuen `using AiNetLinter.Output;` in `AgentRulesGenerator.cs`).
  2. `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~PlaybookGeneratorRound2Tests|FullyQualifiedName~SyncAgentRulesPolicyTests|FullyQualifiedName~MarkdownBuilderTests|FullyQualifiedName~ViolationMarkdownFormatterTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~ListRulesCommandTests|FullyQualifiedName~GetSymbolBodyToolTests"` (zielt auf alle hier betroffenen + Regressions-Schutz-Tests, <30 s).
  3. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` (volle FastTests-Suite, <10 s, ≥1428 Tests).
  4. **Manueller `git diff .agents/rules/AiNetLinter.mdc <playbook-pfad>`** (Vergleich der generierten Output-Bytes gegen die Vor-Step-Version — strukturelle Identität verifizieren).
  5. `dotnet run --project src/AiNetLinter -- --config rules.json --path .` (Dogfood-Suite, <5 s) — verifiziert, dass der eigene Linter im geänderten Code keine Verletzungen findet.
  6. `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests|FullyQualifiedName~BaselineCliTests|FullyQualifiedName~McpServerCommandContractTests|FullyQualifiedName~McpServerCommandStaleness|FullyQualifiedName~SourceFileCatalog"` (MCP + CLI-Byte-Verträge, ~60 s; **nicht** `metrics_lookup`-spezifische Tests, die sind in step-005 dran).
  7. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (volle Integration-Suite, ~120 s).
- **Commit-Pattern:** ein atomarer Commit über beide Dateien (logische Einheit — alle drei Callsites sind thematisch zusammengehörige `Generators`-Migrationen; ein Zwischen-Commit mit nur 1-2 Callsites würde den dritten Callsite ohne Builder-Pendant lassen und keinen Review-Vorteil bringen). Body-Bullet-Points analog zu step-003: Prio 5 / Prio 7 / Prio 8 jeweils mit „Was geändert" + „Warum" + „Pattern-Konsistenz mit `BuildSummaryTable` (Prio 3)". Refs-Trailer: `Refs: tasks/markdown-builder/step-004`.
- **Roadmap-Diff ist bereits angewendet** (siehe `tasks/markdown-builder/roadmap.md` Z.63–64 nach diesem Plan: `Welle 1 done (step-003, 107b2682, approved) ... Welle 2 in Arbeit → step-004`). Der Orchestrator committet den Roadmap-Diff gemeinsam mit dem step-plan-Commit (separat vom Code-Commit, analog zu step-002).
- **Kein Bedarf für `sealed`/`MaxMethodLineCount`-Anpassungen:** beide `Generators`-Klassen sind bereits `public sealed` (`RepoPlaybookGenerator`) bzw. `public static` (`AgentRulesGenerator` — `static` ist implizit sealed für Klassen-Members); alle drei Methoden sind `private static` und unter 30 Zeilen nach Migration.
- **MCP-Dogfooding-Hinweis (Richtlinien §1):** Der Coder kann `ainetlinter.find_symbol` für „AppendAgentPriority" / „AppendMetricsTable" / „AppendCompoundSuppressions" / „MarkdownTableBuilder" / „MarkdownBuilder" nutzen, um Aufrufstellen sicher zu identifizieren — `rg`/`grep` ist hier nur für die Verifikation des `using AiNetLinter.Output;`-Imports in `AgentRulesGenerator.cs` nötig.
