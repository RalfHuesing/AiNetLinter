# Finding: Resource `ainetlinter://rules`

Stand: 2026-09-07. Nur `FetchMcpResource` (`user-AiNetLinter`), kein Build, kein Test, keine anderen Tools.

URI-Muster laut Auftrag: `ainetlinter://rules{?projectRoot}` mit absolutem, URL-kodiertem `projectRoot`.

---

## 1. Schema-Kurzfazit

`ainetlinter://rules` ist eine **projektgebundene Markdown-Resource** der **effektiven** AiNetLinter-Regelkonfiguration — kein Roh-Dump von `ainetlinter-rules.json`.

Mit gültigem `projectRoot` kommt `text/markdown` mit:

- Projektroot und Konfigurationsquelle (hier: `Sample.Project.Tests.Logic\ainetlinter-rules.json`)
- Tabelle **Aktive Regeln** (Name, Intent, Severity, Kurzbeschreibung, Config-Zeiger)
- Tabelle **Effektive Schwellwerte** (Limit, Status aktiv/deaktiviert, Config-Zeiger)
- Liste **Deaktivierte Regeln** (nur Namen)
- Kurzblock **Weitere effektive Overrides** (Anzahlen, keine Musterliste)

Query-Parameter `projectRoot` ist **pflicht**. Ohne Query und mit unbekanntem Pfad antwortet der Server nicht mit einem Schema-Hinweis, sondern mit **Resource not found**.

---

## 2. Calls

Server: `user-AiNetLinter`. Kein `downloadPath`.

| # | URI | Ergebnis |
|---|-----|----------|
| A | `ainetlinter://rules?projectRoot=C%3A%5CDaten%5CEntwicklung%5CSample%5CSample.Project` | **OK**, `text/markdown`, effektive Regelzusammenfassung |
| B | `ainetlinter://rules` (ohne Query) | **Fehler:** `MCP resource not found: ainetlinter://rules` |
| C | `ainetlinter://rules?projectRoot=C%3A%5CDoesNotExist%5CWrongProjectRoot` | **Fehler:** `MCP resource not found: ainetlinter://rules?projectRoot=C%3A%5CDoesNotExist%5CWrongProjectRoot` |

Call A (Auszug, Kopf):

- Projektroot: `C:\Workspace\Sample.Project`
- Grundlage: „aktueller atomarer Config-Snapshot des adressierten Projekt-Keys“
- Aktive Regeln u. a. `EnforceNoSilentCatch`, `BanAsyncVoid`, `BanBlockingTaskAccess`, `DetectAndBanPhantomDependencies`, `EnforceSealedClasses`, `StaticTestSentinel`
- Schwellwerte u. a. `MaxLineCount=600`, `MaxMethodLineCount=60`, `AIContextFootprint=5000`; `MaxDirectoryChildren=0` und `MaxLinqChainLength=0` als **deaktiviert** markiert
- Deaktiviert namentlich u. a. `EnforceXmlDocumentation`, `EnforceNamespaceDirectoryMapping`, `CSS_PreferScopedCss`
- Overrides: **1** Projekt-Muster, **30** Pfad-Muster — Inhalte nicht expandiert, Verweis auf `ainetlinter-rules.json`

---

## 3. Verdict

**Brauchbar für Agenten-Steering, wenn `projectRoot` korrekt ist.** Die Resource verdichtet die effektive Policy auf Tabellen statt die volle JSON-Regeldatei auszuliefern. Ohne gültigen Query-Pfad ist sie nicht auffindbar; die Fehlermeldung unterscheidet nicht zwischen „URI falsch / Query fehlt“ und „Projekt unbekannt“.

---

## 4. Schwere

**Mittel (Nutzbarkeit der Fehlerpfade), nicht blocker für den Happy Path.**

- Happy Path: klar, strukturiert, agententauglich.
- Fehlerpfade: gleiche Klasse `MCP resource not found` für fehlende Query **und** falschen Root — Agent kann Query-Pflicht vs. Integrations-/Pfadfehler nicht trennen. Kein `PROJECT_NOT_INITIALIZED`, kein Hinweis auf erwartetes Query-Schema.

---

## 5. Nutzbarkeit

**Hoch**, sobald der absolute, URL-kodierte `projectRoot` bekannt ist.

Geeignet um:

- zu sehen, welche Regeln **aktiv** sind und mit welcher Severity
- Limits vs. deaktivierte Metriken (`0` = aus) zu lesen
- Intent-Gruppen zu erkennen (`agent-resilience`, `agent-context`, `architecture`, `aspnet-binding`, `test-coverage`, `general`)
- die physische `ainetlinter-rules.json` zu finden, ohne sie zu öffnen

Nicht geeignet als alleinige Policy-Quelle für:

- konkrete Path-/Projekt-Override-Muster (nur Zählung)
- Compound-Suppressions / kontextabhängige Limits (in Call A **nicht** enthalten)
- Ausnahme-Suffixe, Allowlists, exakte `Global.*`-Werte jenseits der Tabellenzeile

---

## 6. Bugs / Lücken

1. **Query-loser URI ist „not found“** statt „missing required query `projectRoot`“. Wirkt wie eine unbekannte Resource-ID.
2. **Falscher `projectRoot` ebenfalls „not found“** — dieselbe Fehlerklasse wie (1). Kein projektbezogener Fehlercode, kein Hinweis auf `ainetlinter://overview` oder Agent-Guide.
3. **Override-Inhalte absichtlich weggelassen** (30 Pfad-Muster). Für Agenten, die eine Datei gegen Overrides prüfen wollen, reicht die Resource nicht; sie müssen `ainetlinter-rules.json` extra lesen. Das ist eher Produktentscheidung als Crash, aber undokumentiert in der Resource selbst außer einem Satz.
4. **Compound-Limits fehlen** in der Markdown-Zusammenfassung (z. B. relaxiertes `MaxMethodLineCount` bei niedriger Komplexität). Effektive Policy ist damit unvollständig gegenüber `ainetlinter-rules.json` / Cursor-Rule `AiNetLinter.mdc`.
5. Kein MIME-/Schema-Feld „required query parameters“ in der Fehlerantwort — nur der URI-String im Fehlertext.

Kein Inhalt-Crash, keine Token-Explosion, keine falschen Limits im Happy-Path-Sample (sichtbare Zahlen wirken konsistent mit der bekannten Platform-Default-Policy).

---

## 7. Token / Hints

**Keine Token-Flut der Regeldatei.** Call A ist eine kompakte Markdown-Zusammenfassung (grobe Größenordnung: wenige Tausend Zeichen / niedrige vierstellige Tokenzahl), nicht das vollständige JSON.

Steering-Qualität:

- Positiv: Tabellen, Config-Zeiger (`ainetlinter-rules.json → Global.*` / `Metrics.*` / `Web.*`), Status aktiv/deaktiviert, Intent-Spalte, expliziter Verweis „Details stehen in der referenzierten ainetlinter-rules.json“.
- Positiv: Overrides als **Anzahl**, nicht 30 Musterblöcke — das verhindert genau die befürchtete Flut.
- Lücke: keine Compound-Suppressions, keine Ausnahme-Listen, kein „so bindest du diese Resource“-Hinweis im Erfolgspayload (Query-Pflicht steht nur implizit, weil ohne Query nichts kommt).

Hint für Agenten: immer `ainetlinter://rules?projectRoot=<absolut URL-kodiert>` verwenden; nacktes `ainetlinter://rules` ist tot.

---

## 8. Roslyn-Wünsche

- Fehler bei fehlendem/ungültigem `projectRoot` differenzieren: `MISSING_QUERY_PROJECT_ROOT` vs. `PROJECT_NOT_FOUND` / `PROJECT_NOT_INITIALIZED`, inkl. kurzem erwarteten URI-Muster.
- Optional: eine Zeile zu Compound-Suppressions (Regel, Bedingung, effektives Limit) oder explizit „Compound: siehe ainetlinter-rules.json“, damit die Resource nicht als vollständige effektive Policy missverstanden wird.
- Optional: Override-Muster als kurze Namensliste oder `get_violations`-Verweis, ohne die volle JSON zu dumpen.
- Resource-Katalog/`resources/list`: Query-Pflicht und Beispiel-URI sichtbar machen, damit Variante B nicht als „Server hat die Resource nicht“ gelesen wird.

---

## 9. Phase 3 (AiNetLinter-Quellzeiger)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch. Die Platform-`ainetlinter-rules.json` ist nur die Live-Quelle aus Call A, nicht der Handler.

### Query-loser URI und falscher Root → dieselbe Klasse `MCP resource not found`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\RulesResourceRegistration.cs`; `src\AiNetLinter\Mcp\Registration\ProjectResourceLease.cs`; `src\AiNetLinter\Mcp\Projects\ProjectToolCall.cs`
- **Symbol:** `RulesResourceRegistration.Register` (`RulesUriTemplate = "ainetlinter://rules{?projectRoot}"`); `BuildTemplatedResult` → `ProjectResourceLease.Execute` (`throw new McpException`). Guard: `ProjectToolCall.GuardRequiredAbsoluteRoot` (`PROJECT_ROOT_REQUIRED`). Unbekannter Root: `ProjectRegistry.Lease` → `PROJECT_NOT_INITIALIZED`. Relative Roots werfen in `RulesResourceRegistrationTests.BuildTemplatedResult_UsesRulesUriAndSharedProjectGuards` bereits `McpException` — Cursor mappt Resource-Exceptions auf Not-Found.
- **Ansatz:** Identisch Overview: nackte URI `ainetlinter://rules` mitregistrieren; Fehler als `ReadResourceResult`-Markdown mit `LinterErrorFormatter.Format` statt Throw; Hint mit kodiertem Beispiel-URI und Verweis auf `ainetlinter://overview`. Kein neues Roslyn; derselbe Lease-Vertrag wie die Tools.

### Override-Inhalte nur als Anzahl (30 Pfad-Muster unsichtbar)

- **Pfad:** `src\AiNetLinter\Mcp\Registration\RulesResourceFormatter.cs`; Wiederverwendung `src\AiNetLinter\Generators\AgentRulesGenerator.cs`; Modell `src\AiNetLinter\Configuration\Config.cs`, `ProjectOverrideEntry.cs`
- **Symbol:** `RulesResourceFormatter.AppendProjectOverrides` (nur `config.ProjectOverrides.Count` / `PathOverrides.Count`); `AgentRulesGenerator.AppendProjectOverridesDelta` + `CollectOverrideParts` listen bereits Key + abweichende Limits
- **Ansatz:** Formatter um eine kompakte Namensliste erweitern: Dictionary-Keys (Glob/Projektname) plus `CollectOverrideParts` (oder extrahierte gemeinsame Hilfsfunktion). Eine Zeile pro Muster, kein volles JSON. Token-Deckel: bei vielen Pfaden erste N Keys + Restzahl + Satz „Details in ainetlinter-rules.json“. Reine Config-Projektion, kein Compilation-Walk.

### Compound-Limits fehlen in der Markdown-Zusammenfassung

- **Pfad:** `RulesResourceFormatter.cs`; `src\AiNetLinter\Generators\AgentRulesGenerator.cs`; `src\AiNetLinter\Configuration\CompoundSuppression.cs`; Defaults in `src\AiNetLinter\Configuration\MetricsConfig.cs`
- **Symbol:** `BuildMarkdown` ruft `AppendActiveRules` / `AppendThresholds` / `AppendDisabledRules` / `AppendProjectOverrides` — kein Compound-Zweig. `AgentRulesGenerator.AppendCompoundSuppressions` tabelliert bereits `TargetRule`, `WhenAllOf`, `RelaxedLimit`, `SeverityOverride`, `Reason` aus `config.Metrics.CompoundSuppressions`
- **Ansatz:** Dieselbe Tabelle (oder Aufruf der bestehenden Methode) nach den Schwellwerten einfügen. Daten liegen im atomaren `GetConfigSnapshot()`-`Config`, den die Resource schon nutzt. `CompoundSuppressionEvaluator` bleibt den Checkern/`metrics_lookup` — die Resource listet nur die konfigurierten Relaxationen, sie wertet keine Methoden aus.

### Fehlerantwort ohne Query-Schema / MIME

- **Pfad:** `RulesResourceRegistration.cs` (`McpServerResourceCreateOptions.Description`); Fehlerkanal `ProjectResourceLease`
- **Symbol:** Description nennt die Query-Pflicht bereits; der Live-404 kommt, bevor Description gelesen wird
- **Ansatz:** Wie oben: erfolgreicher Read mit Fehler-Body (kein Host-404) plus Katalog-Resource. Description allein ändert `FetchMcpResource` auf nacktes `ainetlinter://rules` nicht.
