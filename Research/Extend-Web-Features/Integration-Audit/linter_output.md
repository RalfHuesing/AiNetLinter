# Run: 2026-06-24 11:46:33
# AiNetLinter - 257 violations

| Regel | Gesamt | Prod | Tests | Struktur |
|---|---:|---:|---:|:---:|
| RAZOR_MaxComponentParameterCount | 57 | 57 | 0 |  |
| BanPublicNestedTypes | 43 | 40 | 3 |  |
| MaxPublicMembersPerType | 34 | 24 | 10 |  |
| JS_EnforceJsModules | 30 | 30 | 0 |  |
| RAZOR_BanInlineTernaryInAttributes | 18 | 18 | 0 |  |
| JS_MaxJsLineCount | 13 | 13 | 0 |  |
| StaticTestSentinel | 13 | 13 | 0 |  |
| MaxBoolParameterCount | 12 | 7 | 5 |  |
| MaxPartialClassFiles | 9 | 9 | 0 | ⚠ |
| CSS_MaxCssSelectorComplexity | 6 | 6 | 0 |  |
| CSS_PreferScopedCss | 6 | 6 | 0 |  |
| RAZOR_MaxMarkupNestingDepth | 6 | 6 | 0 |  |
| BanBlockingTaskAccess | 5 | 0 | 5 |  |
| AIContextFootprint | 1 | 1 | 0 | ⚠ |
| CSS_MaxCssLineCount | 1 | 1 | 0 |  |
| MaxMethodParameterCount | 1 | 1 | 0 |  |
| MaxSwitchArms | 1 | 0 | 1 |  |
| RAZOR_MaxControlFlowBlocks | 1 | 1 | 0 |  |

## Handlungsanweisung

Analysiere die Violations im Kontext der Architektur und Coding-Richtlinien dieses Projekts.
Projektkonfiguration erkannt: `.cursor/rules` — Architektur-Constraints und Regeln dort beachten.

**Schritt 1 — False-Positive-Prüfung (PFLICHT vor jeder Änderung)**
Prüfe für jede Violation: Ist das ein echter Verstoß oder ein False-Positive, der durch die Architektur des Projekts gerechtfertigt ist?
Konfigurationsoptionen erkunden:
  `AiNetLinter --docs configuration`
Bei vermutetem False-Positive: Nutzer explizit informieren, Optionen mit Empfehlung nennen, Einverständnis einholen — BEVOR du etwas änderst.

**Schritt 2 — Behebung echter Violations**
Reihenfolge: Code-Fix → Konfigurationsanpassung → Suppression-Kommentar (letztes Mittel, nur nach Nutzer-Freigabe).

> ⚠ **Strukturelle Regeln** (MaxPartialClassFiles, AIContextFootprint, MaxPublicMembersPerType) erfordern oft tiefgreifende Architektureingriffe. **Frage den Nutzer VOR der Umsetzung** — nicht eigenständig beginnen.

## Regellegende

### RAZOR_MaxComponentParameterCount — 57 Verstösse [agent-context]
**Warum:** Ein Komponenten-Aufruf mit vielen Parametern ist das Markup-Aequivalent zu 'MaxMethodParameterCount'. Agenten verlieren die Zuordnung von Werten zu Parametern und generieren haeufig falsch geordnete oder vergessene Bindings.

**Fix-Alternativen:**
- **Parameter-Objekt einfuehren**: Verwandte Parameter in einem 'record' buendeln ('<MyComp Config="@cfg" />').
- **Oeffentliche API reduzieren**: Nicht zwingend benoetigte Properties aus der Komponente entfernen.
- **Suppression** (bei Legacy-Komponenten): `@* ainetlinter-disable RAZOR_MaxComponentParameterCount *@`.

**Konfiguration:** `rules.json → Web.Razor.MaxComponentParameterCount`

### BanPublicNestedTypes — 43 Verstösse [agent-context]
**Warum:** Nested Typen (auch `internal`) erscheinen nicht in Dateilisten und Grep-Ergebnissen auf Namespace-Ebene. Agenten lokalisieren sie über File-Lookup nicht, halluzinieren FQNs (`Outer.Inner` statt `Inner`) und duplizieren sie unbemerkt.

**Fix-Alternativen:**
- **Top-Level-Typ extrahieren** (bevorzugt): Typ in eigene `.cs`-Datei im selben Ordner verschieben. Bei Namenskonflikt Hostnamen als Prefix: `DataTableColumnDefinition` statt `ColumnDefinition`.
- **Privat machen**: Wenn der Typ ausschließlich klassenintern genutzt wird — auf `private nested` reduzieren (nur wenn `BanPublicNestedTypesAllowPrivate: true`).
- **In Host-Datei als Top-Level verschieben**: Als Top-Level-Typ direkt über oder unter der Host-Klasse in derselben Datei — nur für sehr kleine Hilfstypen sinnvoll.

**Konfiguration:** `rules.json → Global.NestedTypeExemptSuffixes`

> ⚠ Bei > 5 betroffenen Typen: Nutzer fragen. Externe Referenzen auf `HostClass.NestedType` sind Breaking Changes — Scope prüfen.

### MaxPublicMembersPerType — 34 Verstösse [agent-context]
**Warum:** Breite API-Fläche erhöht die Wahrscheinlichkeit, dass Agenten existierende Methoden übersehen und duplizieren. Der Agent wählt aus dem sichtbaren Ausschnitt, nicht der vollständigen Klasse.

**Fix-Alternativen:**
- **Klasse nach Verantwortlichkeit aufteilen**: z. B. Command/Query, Read/Write, Domain/Infrastructure als separate Klassen.
- **Facade-Prinzip**: Hilfsmethoden auf `private` oder `internal` reduzieren — nur die echte öffentliche API exponieren.
- **Extension-Methoden auslagern**: Optional-/Hilfsmethoden als `*Extensions`-Klasse im selben Namespace (Suffix `Extensions` ist per Default exempt).
- **State-Objekt**: Zusammengehörige Properties in ein dediziertes `record`-Zustandsobjekt auslagern.

**Konfiguration:** `rules.json → Metrics.MaxPublicMembersPerType | Ausnahmen via PathOverrides`

> ⚠ Oft ein SRP-Signal. Vor größerem Refactoring Nutzer fragen und Architektur-Constraints (`.cursor/rules`, `CLAUDE.md`) lesen.

### JS_EnforceJsModules — 30 Verstösse [agent-context]
**Warum:** Blazors Dynamic Import erwartet Module; globale Script-Dateien sind nicht isoliert importierbar. Zuweisungen an 'window' erzeugen unvorhersehbare Seiteneffekte bei KI-Edits.

**Fix-Alternativen:**
- **ES6-Export hinzufuegen**: 'export function myHelper() { ... }' oder 'export { myHelper };'.
- **Dynamic Import nutzen**: 'await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./myModule.js")'.
- **Suppression** (bei Legacy-Bridge): `// ainetlinter-disable JS_EnforceJsModules`.

**Konfiguration:** `rules.json → Web.Js.EnforceJsModules`

### RAZOR_BanInlineTernaryInAttributes — 18 Verstösse [agent-context]
**Warum:** Ternary-Ausdruecke innerhalb von HTML-Attributwerten erzeugen Mixed-Context zwischen HTML-String-Kontext und C#-Expressions-Kontext. Agenten muessen beide Kontexte gleichzeitig aufloesen und produzieren typische Fehler (fehlende Anfuehrungszeichen, vertauschte Klammern).

**Fix-Alternativen:**
- **Attributwert in Property berechnen**: 'private string CssClass => isActive ? "base active" : "base";' und dann 'class="@CssClass"'.
- **Hilfsmethode verwenden**: 'GetCssClass(bool isActive)' in der Code-Behind-Datei.
- **Suppression** (bei trivialen Bedingungen): `@* ainetlinter-disable RAZOR_BanInlineTernaryInAttributes *@`.

**Konfiguration:** `rules.json → Web.Razor.BanInlineTernaryInAttributes`

### JS_MaxJsLineCount — 13 Verstösse [agent-context]
**Warum:** Lange JavaScript-Dateien uebersteigen das lesbare Kontextfenster. Blazor-Interop-Dateien sollen minimal bleiben — komplexe Logik gehoert in C#.

**Fix-Alternativen:**
- **Logik nach C# migrieren**: Komplexe Berechnungen in C#-Methoden verschieben (Handler im IJSObjectReference).
- **Datei aufteilen**: Mehrere kleine ES6-Module mit klarer Verantwortung pro Datei.
- **Custom Values uebergeben**: Daten via Parameter an die exportierte Funktion uebergeben statt im Closure zu kapseln.

**Konfiguration:** `rules.json → Web.Js.MaxJsLineCount (Web.IsEnabled muss true sein)`

### StaticTestSentinel — 13 Verstösse [test-coverage]
**Warum:** Komplexe Typen ohne Testabdeckung sind für Agenten eine Black Box — sie können keine Regression bei Änderungen erkennen.

**Fix-Alternativen:**
- **Testklasse anlegen**: `{Name}Tests.cs` im entsprechenden Test-Projekt.
- **`typeof(T)`-Referenz**: `typeof(FooClass)` in einer Testklasse — `EnableTestSentinel` erkennt das als Sentinel.
- **`// @covers T`-Kommentar**: In einer bestehenden Testklasse ergänzen.

### MaxBoolParameterCount — 12 Verstösse [agent-context]
**Warum:** `DoWork(true, false)` trägt an der Call-Site keine semantische Information — der Agent ordnet Flags falsch zu und macht Aufruffehler.

**Fix-Alternativen:**
- **Parameter-Object** (bevorzugt): `record WorkOptions(bool WithLogging, bool ForceRefresh)` — die Call-Site wird selbsterklärend.
- **Enum**: Bei zwei oder mehr Flags, die verschiedene Modi darstellen, ein Enum statt bool-Kombination.
- **Named Arguments** (kurzfristig): `DoWork(withLogging: true, forceRefresh: false)` — kein Strukturumbau nötig, rein syntaktische Verbesserung.
- **Separierte Methoden**: Wenn die Pfade fachlich distinct sind — `ProcessSingle()` / `ProcessBatch()` statt `Process(bool isBatch)`.

### MaxPartialClassFiles — 9 Verstösse [agent-context]
**Warum:** Agenten sehen nur die aktuell geöffnete Datei. Invarianten, Felder und Methoden aus anderen Partial-Dateien derselben Klasse sind unsichtbar — der Agent erkennt Konflikte nicht und dupliziert Logik.

**Fix-Alternativen:**
- **Eigenständige Klassen extrahieren** (bevorzugt): Logik aus Partials in dedizierte, fachlich benannte Klassen auslagern — z. B. `FooCommandHandler`, `FooQueryHandler`, `FooValidator`.
- **Facade-Klasse**: Wenn Partials verschiedene Subsysteme bedienen, eine schlanke Fassadenklasse pro Subsystem einführen.
- **Namespace-basierte Trennung**: Verwandte Methoden in eigenständige Klassen im selben Namespace verschieben statt Partials.
- **Interface** (nur wenn Projektregeln es erlauben): Wenn Partials verschiedene Rollen abbilden — Interfaces extrahieren und Implementierungen trennen.

**Konfiguration:** `rules.json → Metrics.MaxPartialClassFiles | Ausnahmen via PathOverrides`

> ⚠ Partials aufzulösen ist ein tiefgreifender Architektureingriff. **Nutzer ZWINGEND fragen bevor du beginnst** — die gewählte Alternativarchitektur muss dem Projektstil entsprechen.

### CSS_MaxCssSelectorComplexity — 6 Verstösse [agent-context]
**Warum:** Verschachtelte CSS-Selektoren sind fuer Modelle schwer zuzuordnen — der Agent matcht die Hierarchie falsch und erzeugt inkonsistente Styles.

**Fix-Alternativen:**
- **Scoped CSS verwenden**: Wurzel-Selektor '.my-component' reicht; Verschachtelung entfaellt.
- **Spezifitaet reduzieren**: IDs, !important und tief verschachtelte Klassen vermeiden.
- **Selektor aufteilen**: Statt '.card > .header .title' zwei separate Regeln fuer '.card-header' und '.card-title'.

**Konfiguration:** `rules.json → Web.Css.MaxCssSelectorComplexity (Web.IsEnabled muss true sein)`

### CSS_PreferScopedCss — 6 Verstösse [agent-context]
**Warum:** Globale CSS-Regeln sind fuer Agenten nicht lokalisierbar — eine Aenderung an '.card' wirkt sich auf alle Komponenten aus, ohne dass der Agent die Konsequenzen ueberblickt (Butterfly-Effekt).

**Fix-Alternativen:**
- **Scoped CSS**: Globale Regeln in gleichnamige '.razor.css'-Datei der Komponente extrahieren.
- **Globale Datei klein halten**: Nur Resets, Custom Properties und Font-Definitionen in der globalen Datei belassen.
- **Suppression** (bei wenigen, klar globalen Regeln): `/* ainetlinter-disable CSS_PreferScopedCss */`.

**Konfiguration:** `rules.json → Web.Css.PreferScopedCss | Web.Css.PreferScopedCssMinRuleCount`

### RAZOR_MaxMarkupNestingDepth — 6 Verstösse [agent-context]
**Warum:** Tiefe HTML-Hierarchien fuehren bei Agenten zu Tag-Mismatch-Halluzinationen — falsch geschlossene oder verschobene Elemente. KIs koennen die Tag-Hierarchie ueber mehrere Ebenen nicht zuverlaessig rekonstruieren.

**Fix-Alternativen:**
- **Innere Bereiche extrahieren**: Komplexe Sub-Bereiche in eigene Blazor-Komponenten mit klar definierter API verschieben.
- **Flachere Struktur anstreben**: Wiederkehrende Container-Klassen als CSS-Klasse statt als verschachteltes DIV.
- **Suppression** (bei semantisch notwendiger Verschachtelung): `@* ainetlinter-disable RAZOR_MaxMarkupNestingDepth *@`.

**Konfiguration:** `rules.json → Web.Razor.MaxMarkupNestingDepth`

### BanBlockingTaskAccess — 5 Verstösse [agent-resilience]
**Warum:** Blockierende Task-Zugriffe blockieren ThreadPool-Threads und sind in SynchronizationContext-Umgebungen (ASP.NET Classic, WPF) deadlock-anfaellig. Agenten produzieren dieses Muster systematisch wenn sie synchrone Methoden mit async-APIs verbinden.

**Fix-Alternativen:**
- **'await task'**: Methode zu 'async Task' umwandeln und await verwenden — loest das Problem vollstaendig.
- **Aufrufkette async machen**: Von der blockierenden Methode nach oben migrieren bis alle Aufrufer async sind.
- **'BanBlockingTaskAccessAllowInMain: true'**: Fuer Programm-Einstiegspunkte die kein async Main haben.
- **Suppression** (letztes Mittel): '// ainetlinter-disable BanBlockingTaskAccess' fuer unvermeidliche Stellen.

**Konfiguration:** `rules.json → Global.BanBlockingTaskAccess | BanBlockingTaskAccessAllowInMain | BanBlockingTaskAccessAllowInTests`

### AIContextFootprint — 1 Verstoß [agent-context]
**Warum:** Ein zu großer transitiver Code-Footprint bedeutet: der Agent braucht das volle Kontextbudget für eine einzige Klasse. Er sieht nie den vollständigen Kontext und übersieht Invarianten.

**Fix-Alternativen:**
- **Schlankes Interface einführen**: Die größten Abhängigkeiten (s. Details) hinter einem minimalen Interface verstecken — reduziert den transitiven Footprint direkt.
- **Klasse aufteilen**: Klasse nach Verantwortlichkeiten teilen und die Teile separat halten — jeder Teil hat kleineren Footprint.
- **Abhängigkeit kapseln**: Statt direkter Abhängigkeit eine Facade oder ein Data-Transfer-Objekt übergeben.

**Konfiguration:** `rules.json → Metrics.MaxAIContextFootprint | Ausnahmen via PathOverrides`

> ⚠ Interfaces einführen kann Architekturentscheidungen ändern. Nutzer fragen ob Interfaces im Projekt erlaubt sind.

### CSS_MaxCssLineCount — 1 Verstoß [agent-context]
**Warum:** Lange CSS-Dateien uebersteigen das lesbare Kontextfenster. Agenten verlieren bei Diffs die Uebersicht und erzeugen Style-Konflikte ('Lost in the Middle').

**Fix-Alternativen:**
- **Aufteilen nach Feature**: CSS-Datei in mehrere themenspezifische Dateien zerlegen (z. B. layout.css, typography.css).
- **Scoped CSS verwenden**: Komponenten-Styles in gleichnamige '.razor.css'-Datei extrahieren — Blazor scopped automatisch.
- **Custom Properties konsolidieren**: Design-Tokens als CSS-Variablen in einer kleinen 'tokens.css'.

**Konfiguration:** `rules.json → Web.Css.MaxCssLineCount (Web.IsEnabled muss true sein)`

### MaxMethodParameterCount — 1 Verstoß [agent-context]
**Warum:** Viele Parameter erhöhen die Wahrscheinlichkeit, dass Agenten Argumente in falscher Reihenfolge übergeben oder Pflichtparameter übersehen.

**Fix-Alternativen:**
- **Parameter-Object** (bevorzugt): `record WorkOptions(bool WithLogging, bool ForceRefresh)` — die Call-Site wird selbsterklärend.
- **Builder-Pattern**: Für optionale Parameter mit vielen Kombinationen.
- **Methode aufteilen**: Wenn Parameter verschiedene Anwendungsfälle kodieren — separierte Methoden mit eindeutigen Namen.

### MaxSwitchArms — 1 Verstoß [general]
Keine spezifische Anleitung hinterlegt — behebe gemäß Projektrichtlinien.

### RAZOR_MaxControlFlowBlocks — 1 Verstoß [agent-context]
**Warum:** Viele @if/@foreach/@switch-Bloecke signalisieren zu viel konditionale Render-Logik. Agenten koennen bei komplexem konditionalen Rendering nicht vorhersagen, welche HTML-Elemente tatsaechlich ausgegeben werden.

**Fix-Alternativen:**
- **Teilbereiche extrahieren**: Konditionale Bereiche in eigene Komponenten mit klar definierten Parametern auslagern.
- **Render-Fragments verwenden**: '@ChildContent' / 'RenderFragment' fuer flexible Wiederverwendung.
- **Suppression** (bei Legacy-Komponenten): `@* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@`.

**Konfiguration:** `rules.json → Web.Razor.MaxControlFlowBlocks`

## Strukturelle Verstöße
> ⚠ Diese Violations erfordern Architekturentscheidungen. **Nutzer VOR Beginn fragen.**

### AIContextFootprint
- San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerBindings.cs:12
  SchedulerBindings (6377 > 5000)
    + SchedulerExternalDropCoordinator (488)
    + SchedulerExternalDropRequest (488)
    + ExternalDropValidatedContext (488)
    → Top-3 transitive Abhängigkeiten reduzieren oder Facade einführen

### MaxPartialClassFiles
- San.smart.Planner.Platform/Components/Pages/SiteView/SitePageSplitLayout.PageUserConfig.cs:6
  Der partial-Typ 'San.smart.Planner.Platform.Components.Pages.SitePageSplitLayout' ist auf 4 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerJsInterop.Callbacks.cs:6
  Der partial-Typ 'San.smart.Planner.Platform.Components.UI.Scheduler.SchedulerJsInterop' ist auf 3 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Admin/Ai/Patches/SiteAdminAiMetadataPatches.Component.cs:9
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Admin.Ai.SiteAdminAiMetadataPatches' ist auf 12 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Admin/Ai/Plugin/SiteAdminAiPlugin.Component.cs:9
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Admin.Ai.SiteAdminAiPlugin' ist auf 8 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Domains/Firmenkalender/FirmenkalenderSchedulerHandler.Commands.cs:9
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.Firmenkalender.FirmenkalenderSchedulerHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Domains/Mitarbeiterkalender/MitarbeiterkalenderSchedulerHandler.Commands.cs:9
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.Mitarbeiterkalender.MitarbeiterkalenderSchedulerHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Domains/MitarbeiterPlantafel/MitarbeiterPlantafelSchedulerHandler.Commands.cs:10
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.MitarbeiterPlantafel.MitarbeiterPlantafelSchedulerHandler' ist auf 9 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Handlers/Scheduler/SchedulerSiteComponentHandler.Commands.cs:10
  Der partial-Typ 'San.smart.Planner.Platform.Handlers.Scheduler.SchedulerSiteComponentHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- San.smart.Planner.Platform/Infrastructure/Sql/SqlExecutor.cs:8
  Der partial-Typ 'San.smart.Planner.Platform.Infrastructure.Sql.SqlExecutor' ist auf 3 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

## Violations nach Datei

### Produktion (151 Dateien)

#### San.smart.Planner.Platform/Auth/AuthRegister.cs
- Z.90 BanPublicNestedTypes — Der Typ 'AuthRegister.RegisterEndpointRequest' ist ein internal nested Type.

#### San.smart.Planner.Platform/Auth/UiCookieHandshake.cs
- Z.61 BanPublicNestedTypes — Der Typ 'UiCookieHandshake.UiCookieHandshakeRequest' ist ein internal nested Type.

#### San.smart.Planner.Platform/Components/Admin/Ai/AiChatComposer.razor
- Z.5 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 10 Parameter (erlaubt: 5).
- Z.19 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 9 Parameter (erlaubt: 5).
- Z.29 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).
- Z.40 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiChatMessageList.razor
- Z.35 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.52 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiChatMessageList.razor.cs
- Z.7 StaticTestSentinel — Die Klasse 'AiChatMessageList' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 7), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/Ai/AiChatPanelCoordinator.cs
- Z.11 MaxPublicMembersPerType — 'AiChatPanelCoordinator' hat 19 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSessionArchivePanel.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 7 Ebenen (erlaubt: 6).
- Z.8 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).
- Z.31 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.44 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.63 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.63 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudIconButton>' hat 6 Parameter (erlaubt: 5).
- Z.64 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.70 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudIconButton>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSiteWorkspaceLayout.razor
- Z.18 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 9 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSiteWorkspaceState.cs
- Z.9 MaxPublicMembersPerType — 'AiSiteWorkspaceState' hat 39 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSiteWorkspaceToolbar.razor
- Z.4 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).
- Z.13 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).
- Z.22 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSiteWorkspaceToolbar.razor.cs
- Z.14 StaticTestSentinel — Die Klasse 'AiSiteWorkspaceToolbar' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 7), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSsmsSqlPanel.razor
- Z.28 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 6 Parameter (erlaubt: 5).
- Z.62 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Ai/AiSsmsSqlPanel.razor.cs
- Z.16 StaticTestSentinel — Die Klasse 'AiSsmsSqlPanel' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 9), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsTreeItem.razor
- Z.10 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.20 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 6 Parameter (erlaubt: 5).
- Z.21 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.42 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<AppSettingsTreeItem>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsTreeView.razor
- Z.6 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<AppSettingsTreeItem>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsWorkspaceLayout.razor
- Z.45 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudDrawer>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsWorkspaceLayout.razor.cs
- Z.6 StaticTestSentinel — Die Klasse 'AppSettingsWorkspaceLayout' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 5), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsWorkspaceState.cs
- Z.6 MaxPublicMembersPerType — 'AppSettingsWorkspaceState' hat 21 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/Admin/AppSettings/AppSettingsWorkspaceToolbar.razor
- Z.18 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/AppSettingsLeafField.razor
- Z.5 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 9 Parameter (erlaubt: 5).
- Z.17 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSwitch>' hat 7 Parameter (erlaubt: 5).
- Z.27 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/ConnectionStringIdSelect.razor
- Z.3 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/ConnectionStringNameDialog.razor
- Z.3 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/ConnectionStringNameDialog.razor.cs
- Z.9 StaticTestSentinel — Die Klasse 'ConnectionStringNameDialog' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/PlatformAuthHandlersEditor.razor
- Z.6 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/AppSettings/Editors/SerilogWriteToEditor.razor.cs
- Z.9 StaticTestSentinel — Die Klasse 'SerilogWriteToEditor' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 5), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Admin/Sites/AdminSiteCreateDialog.razor
- Z.9 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 8 Parameter (erlaubt: 5).
- Z.17 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 7 Parameter (erlaubt: 5).
- Z.24 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 8 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Admin/Sites/AdminSiteCreateDialog.razor.cs
- Z.57 BanPublicNestedTypes — Der Typ 'AdminSiteCreateDialog.AdminSiteCreateDialogResult' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/Layout/LayoutTestMainLayout/LayoutTestMainLayout.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 7 Ebenen (erlaubt: 6).

#### San.smart.Planner.Platform/Components/Layout/MainLayout/MainLayout.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 7 Ebenen (erlaubt: 6).

#### San.smart.Planner.Platform/Components/Layout/Navigation/SiteNavigationMenu.razor
- Z.4 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudNavLink>' hat 6 Parameter (erlaubt: 5).
- Z.17 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudNavLink>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Layout/Navigation/SiteNavigationMenuNode.razor
- Z.11 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudNavLink>' hat 6 Parameter (erlaubt: 5).
- Z.35 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudNavLink>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Layout/ReconnectModal/ReconnectModal.razor.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/Components/Pages/Auth/Login.razor
- Z.20 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 7 Parameter (erlaubt: 5).
- Z.27 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 7 Parameter (erlaubt: 5).
- Z.38 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/Pages/Auth/Register.razor
- Z.20 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).
- Z.26 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).
- Z.32 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).
- Z.42 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/Pages/Auth/Register.razor.cs
- Z.7 StaticTestSentinel — Die Klasse 'Register' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Pages/Home/Home.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 7 Ebenen (erlaubt: 6).

#### San.smart.Planner.Platform/Components/Pages/SiteView/SitePageSplitLayout.PageUserConfig.cs
- Z.6 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Components.Pages.SitePageSplitLayout' ist auf 4 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Components/Pages/SiteView/SitePageSplitLayoutDragSetup.cs
- Z.15 BanPublicNestedTypes — Der Typ 'SitePageSplitLayoutDragSetup.Context' ist ein public nested Type.
- Z.24 BanPublicNestedTypes — Der Typ 'SitePageSplitLayoutDragSetup.Result' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/Pages/SiteView/SitePageSplitLayoutUserConfig.cs
- Z.19 BanPublicNestedTypes — Der Typ 'SitePageSplitLayoutUserConfig.FractionCaches' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/Pages/SiteView/SiteView.razor
- Z.33 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.55 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.56 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.61 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/Pages/SiteView/SiteViewUiState.cs
- Z.13 MaxPublicMembersPerType — 'SiteViewUiState' hat 24 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/DataTableUserConfigFilterLayoutTestPage.razor.cs
- Z.14 StaticTestSentinel — Die Klasse 'DataTableUserConfigFilterLayoutTestPage' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/DataTableUserConfigLayoutTestPage.razor.cs
- Z.14 StaticTestSentinel — Die Klasse 'DataTableUserConfigLayoutTestPage' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/LoginFieldsLayoutTestPage.razor
- Z.18 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).
- Z.24 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/SchedulerResourceColumnsSiteViewLayoutTestPage.razor.cs
- Z.14 StaticTestSentinel — Die Klasse 'SchedulerResourceColumnsSiteViewLayoutTestPage' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/SchedulerResourceColumnsUserConfigLayoutTestPage.razor.cs
- Z.13 StaticTestSentinel — Die Klasse 'SchedulerResourceColumnsUserConfigLayoutTestPage' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/Pages/Test/Layout/SplitPaneFillHeightLayoutTestPage.razor
- Z.15 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTable.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 7 Ebenen (erlaubt: 6).
- Z.10 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudDataGrid>' hat 6 Parameter (erlaubt: 5).
- Z.22 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.
- Z.45 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<DataTableTemplateColumn>' hat 6 Parameter (erlaubt: 5).
- Z.61 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<DataTableColumnFilterPanel>' hat 6 Parameter (erlaubt: 5).
- Z.70 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableBindings.cs
- Z.10 MaxPublicMembersPerType — 'DataTableBindings' hat 21 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableColumnFilterLogic.cs
- Z.8 BanPublicNestedTypes — Der enum 'DataTableColumnFilterLogic.FilterKeyboardAction' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableColumnFilterPanel.razor
- Z.21 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudSelect>' hat 9 Parameter (erlaubt: 5).
- Z.56 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 10 Parameter (erlaubt: 5).
- Z.59 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableColumnFilterPanel.razor.cs
- Z.8 StaticTestSentinel — Die Klasse 'DataTableColumnFilterPanel' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 6), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableFilterOperators.cs
- Z.8 BanPublicNestedTypes — Der Typ 'DataTableFilterOperators.OperatorOption' ist ein internal nested Type.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableServerDataFilterPlanner.cs
- Z.11 BanPublicNestedTypes — Der Typ 'DataTableServerDataFilterPlanner.FilterPlanResult' ist ein internal nested Type.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableServerDataSortPlanner.cs
- Z.11 BanPublicNestedTypes — Der Typ 'DataTableServerDataSortPlanner.GridSortInput' ist ein internal nested Type.
- Z.13 BanPublicNestedTypes — Der Typ 'DataTableServerDataSortPlanner.SortPlanResult' ist ein internal nested Type.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableUiState.cs
- Z.13 MaxPublicMembersPerType — 'DataTableUiState' hat 32 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableUserConfig.cs
- Z.19 BanPublicNestedTypes — Der Typ 'DataTableUserConfig.AppliedLayout' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/DataTable/DataTableUserConfigHydrator.cs
- Z.12 BanPublicNestedTypes — Der Typ 'DataTableUserConfigHydrator.GridContext' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/Form/DynamicForm.razor
- Z.12 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTabs>' hat 6 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormFieldEditor.razor
- Z.1 RAZOR_MaxMarkupNestingDepth — HTML-Verschachtelungstiefe betraegt 8 Ebenen (erlaubt: 6).

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormIconField.razor
- Z.6 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<DynamicFormTextFieldWithButton>' hat 10 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormRepeaterCoordinator.cs
- Z.129 BanPublicNestedTypes — Der Typ 'DynamicFormRepeaterCoordinator.RepeaterRendererHostRequest' ist ein internal nested Type.

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormRepeaterRenderer.cs
- Z.15 BanPublicNestedTypes — Der Typ 'DynamicFormRepeaterRenderer.Host' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormSelectOptionsLoader.cs
- Z.14 BanPublicNestedTypes — Der Typ 'DynamicFormSelectOptionsLoader.Request' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/Form/DynamicFormTextFieldWithButton.razor
- Z.15 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 9 Parameter (erlaubt: 5).
- Z.29 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudIconButton>' hat 7 Parameter (erlaubt: 5).
- Z.40 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudIconButton>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Form/MaterialSymbolPickerDialog.razor
- Z.20 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudTextField>' hat 11 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Form/MaterialSymbolPickerDialog.razor.cs
- Z.6 StaticTestSentinel — Die Klasse 'MaterialSymbolPickerDialog' hat eine hohe Relevanz (max. Kognitive Komplexitaet: 4), aber es wurde keine Testabdeckung gefunden.

#### San.smart.Planner.Platform/Components/UI/Scheduler/Scheduler.razor
- Z.16 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<SchedulerResourceChrome>' hat 12 Parameter (erlaubt: 5).
- Z.35 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<SchedulerContextMenu>' hat 10 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Scheduler/Scheduler.razor.cs
- Z.13 MaxPublicMembersPerType — 'Scheduler' hat 19 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerBindings.cs
- Z.12 AIContextFootprint [→ strukturell] — SchedulerBindings (6377 > 5000)
- Z.12 MaxPublicMembersPerType — 'SchedulerBindings' hat 27 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerContextMenu.razor
- Z.8 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudPopover>' hat 8 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerJsInterop.Callbacks.cs
- Z.6 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Components.UI.Scheduler.SchedulerJsInterop' ist auf 3 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerJsInterop.cs
- Z.8 MaxPublicMembersPerType — 'SchedulerJsInterop' hat 20 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerLoadCoordinator.cs
- Z.248 MaxPublicMembersPerType — 'SchedulerLoadRequest' hat 25 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.
- Z.277 MaxPublicMembersPerType — 'SchedulerTimelineInitRequest' hat 20 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerResourceChrome.razor
- Z.29 RAZOR_BanInlineTernaryInAttributes — Ternary-Ausdruck im Attributwert gefunden.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerResourceChrome.razor.cs
- Z.10 MaxPublicMembersPerType — 'SchedulerResourceChrome' hat 20 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerResourceChromeCoordinator.cs
- Z.15 MaxPublicMembersPerType — 'SchedulerResourceChromeCoordinator' hat 16 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.
- Z.52 MaxBoolParameterCount — 'SyncResourceColumnsFromResolution' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.
- Z.354 MaxPublicMembersPerType — 'SchedulerResourceChromeState' hat 17 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerUiState.cs
- Z.12 MaxPublicMembersPerType — 'SchedulerUiState' hat 21 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Components/UI/Scheduler/SchedulerVisibleRange.cs
- Z.16 BanPublicNestedTypes — Der Typ 'SchedulerVisibleRange.VisibleRangeUtc' ist ein public nested Type.

#### San.smart.Planner.Platform/Components/UI/SiteComponentWrapper/SiteComponentWrapper.razor
- Z.1 RAZOR_MaxControlFlowBlocks — Datei enthaelt 9 Control-Flow-Bloecke (erlaubt: 8).
- Z.17 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 8 Parameter (erlaubt: 5).
- Z.41 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).
- Z.67 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 8 Parameter (erlaubt: 5).
- Z.91 RAZOR_MaxComponentParameterCount — Komponentenaufruf '<MudButton>' hat 7 Parameter (erlaubt: 5).

#### San.smart.Planner.Platform/Configuration/HandlerSettingsJsonKeys.cs
- Z.4 MaxPublicMembersPerType — 'HandlerSettingsJsonKeys' hat 41 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Configuration/PlatformAiOptions.cs
- Z.6 MaxPublicMembersPerType — 'PlatformAiOptions' hat 25 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Configuration/SiteComponentDataLoader.cs
- Z.21 BanPublicNestedTypes — Der Typ 'SiteComponentDataLoader.ResolveResult' ist ein public nested Type.

#### San.smart.Planner.Platform/Configuration/SiteModels.cs
- Z.83 MaxPublicMembersPerType — 'SiteComponent' hat 24 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Orchestration/AdminAiTurnArchiveWriter.cs
- Z.305 MaxPublicMembersPerType — 'AdminAiTurnArchiveManifest' hat 19 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Patches/SiteAdminAiMetadataPatches.Component.cs
- Z.9 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Admin.Ai.SiteAdminAiMetadataPatches' ist auf 12 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Plugin/SiteAdminAiPlugin.Component.cs
- Z.9 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Admin.Ai.SiteAdminAiPlugin' ist auf 8 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.
- Z.48 MaxBoolParameterCount — 'PatchComponentLayout' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.
- Z.293 MaxBoolParameterCount — 'PatchDataTableInteractiveFeatures' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Plugin/SiteAdminAiPlugin.cs
- Z.213 MaxBoolParameterCount — 'UpsertSiteParameter' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.
- Z.250 MaxMethodParameterCount — Die Methode 'FormatUpsertSiteParameterSuccess' hat 7 Parameter, davon 7 gewertet (erlaubt sind maximal 6); nicht mitgezählt: CancellationToken, ILogger*, IOptions*, IOptionsSnapshot*, IOptionsMonitor*, IHostEnvironment*, IWebHostEnvironment*, IConfiguration*, IServiceProvider*, IHttpContextAccessor*, HttpContext*, TimeProvider*, LinkGenerator*, ProblemDetailsService*.
- Z.333 MaxBoolParameterCount — 'AddOrUpdateDataTableComponent' hat 4 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Plugin/SiteAdminAiPlugin.Removals.cs
- Z.11 MaxBoolParameterCount — 'RemovePage' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Support/AiPluginExecutionContext.cs
- Z.14 MaxPublicMembersPerType — 'AiPluginExecutionContext' hat 17 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Support/SiteAdminAiDataTableColumnInput.cs
- Z.7 MaxPublicMembersPerType — 'SiteAdminAiDataTableColumnInput' hat 16 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Support/SiteAdminAiHandlerSettingsSerializer.cs
- Z.36 MaxBoolParameterCount — 'SerializeDataTable' hat 4 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Support/SiteAdminAiSchemaObjectResolver.cs
- Z.193 BanPublicNestedTypes — Der Typ 'SiteAdminAiSchemaObjectResolver.ResolvedSchemaObject' ist ein internal nested Type.
- Z.203 BanPublicNestedTypes — Der Typ 'SiteAdminAiSchemaObjectResolver.SchemaColumnRow' ist ein internal nested Type.

#### San.smart.Planner.Platform/Handlers/Admin/Ai/Support/SiteMetadataJsonKeys.cs
- Z.5 MaxPublicMembersPerType — 'SiteMetadataJsonKeys' hat 21 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Mutation/BouncerValidationCodes.cs
- Z.5 MaxPublicMembersPerType — 'BouncerValidationCodes' hat 22 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Admin/Mutation/SiteMutationSqlValidationPlanner.cs
- Z.14 BanPublicNestedTypes — Der Typ 'SiteMutationSqlValidationPlanner.Target' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Admin/Mutation/SiteMutationValidationSandbox.cs
- Z.34 BanPublicNestedTypes — Der Typ 'SiteMutationValidationSandbox.SandboxLease' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Admin/SiteFilesystemImporter.cs
- Z.18 BanPublicNestedTypes — Der Typ 'SiteFilesystemImporter.ImportResult' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/DataTable/DataTableHandlerSettings.cs
- Z.12 BanPublicNestedTypes — Der Typ 'DataTableHandlerSettings.ColumnDefinition' ist ein public nested Type.
- Z.38 BanPublicNestedTypes — Der Typ 'DataTableHandlerSettings.ComboboxOption' ist ein public nested Type.
- Z.41 BanPublicNestedTypes — Der Typ 'DataTableHandlerSettings.DataTableDragDropSettings' ist ein public nested Type.
- Z.80 BanPublicNestedTypes — Der Typ 'DataTableHandlerSettings.Parsed' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Domains/Firmenkalender/FirmenkalenderSchedulerHandler.Commands.cs
- Z.9 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.Firmenkalender.FirmenkalenderSchedulerHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Handlers/Domains/Mitarbeiterkalender/MitarbeiterkalenderSchedulerHandler.Commands.cs
- Z.9 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.Mitarbeiterkalender.MitarbeiterkalenderSchedulerHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Handlers/Domains/MitarbeiterPlantafel/MitarbeiterPlantafelCommandPhases.cs
- Z.15 BanPublicNestedTypes — Der Typ 'MitarbeiterPlantafelCommandPhases.ExtendItemValidated' ist ein public nested Type.
- Z.22 BanPublicNestedTypes — Der Typ 'MitarbeiterPlantafelCommandPhases.BelegungTimelineUpdateRowRequest' ist ein public nested Type.
- Z.31 BanPublicNestedTypes — Der Typ 'MitarbeiterPlantafelCommandPhases.ExtendEndUtcResolveRequest' ist ein internal nested Type.

#### San.smart.Planner.Platform/Handlers/Domains/MitarbeiterPlantafel/MitarbeiterPlantafelHandlerSettings.cs
- Z.12 BanPublicNestedTypes — Der Typ 'MitarbeiterPlantafelHandlerSettings.Parsed' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Domains/MitarbeiterPlantafel/MitarbeiterPlantafelSchedulerHandler.Commands.cs
- Z.10 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Domains.MitarbeiterPlantafel.MitarbeiterPlantafelSchedulerHandler' ist auf 9 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Handlers/Domains/MitarbeiterPlantafel/MitarbeiterPlantafelSchedulerHandler.cs
- Z.55 BanPublicNestedTypes — Der Typ 'MitarbeiterPlantafelSchedulerHandler.ForTests' ist ein internal nested Type.

#### San.smart.Planner.Platform/Handlers/Form/FormHandlerSettings.cs
- Z.42 BanPublicNestedTypes — Der Typ 'FormHandlerSettings.Parsed' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Form/FormOnloadQueryPhases.cs
- Z.13 BanPublicNestedTypes — Der Typ 'FormOnloadQueryPhases.Request' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Form/FormSqlResultMapper.cs
- Z.23 BanPublicNestedTypes — Der Typ 'FormSqlResultMapper.ApplyRowResult' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerHandlerSettings.cs
- Z.8 BanPublicNestedTypes — Der Typ 'SchedulerHandlerSettings.Parsed' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerResourceColumnLayout.cs
- Z.26 BanPublicNestedTypes — Der Typ 'SchedulerResourceColumnLayout.ResolvedColumn' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerResourceColumnSettings.cs
- Z.12 BanPublicNestedTypes — Der Typ 'SchedulerResourceColumnSettings.ColumnDefinition' ist ein public nested Type.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerResourceLayoutState.cs
- Z.21 MaxPublicMembersPerType — 'SchedulerResourceLayoutState' hat 18 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerSiteComponentHandler.Commands.cs
- Z.10 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Handlers.Scheduler.SchedulerSiteComponentHandler' ist auf 5 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Handlers/Scheduler/SchedulerTimelineJsOptions.cs
- Z.16 BanPublicNestedTypes — Der Typ 'SchedulerTimelineJsOptions.Parsed' ist ein public nested Type.

#### San.smart.Planner.Platform/Infrastructure/Sql/SqlExecutor.cs
- Z.8 MaxPartialClassFiles [→ strukturell] — Der partial-Typ 'San.smart.Planner.Platform.Infrastructure.Sql.SqlExecutor' ist auf 3 Dateien verteilt (erlaubt: 2). Agenten sehen nur die aktuelle Datei und übersehen Invarianten aus den anderen Dateien.

#### San.smart.Planner.Platform/Infrastructure/Sql/SqlOptimisticContribMutationModel.cs
- Z.145 BanPublicNestedTypes — Der Typ 'SqlOptimisticContribMutationModel.MutationProperty' ist ein internal nested Type.

#### San.smart.Planner.Platform/wwwroot/css/02-base.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 9 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.

#### San.smart.Planner.Platform/wwwroot/css/03-shell.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 27 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.
- Z.51 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-link .mud-nav-link-icon' ist zu komplex (Tiefe: 4, erlaubt: 3).
- Z.61 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-link:hover .mud-nav-link-icon' ist zu komplex (Tiefe: 4, erlaubt: 3).
- Z.72 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-link.active .mud-nav-link-icon,body.t…' ist zu komplex (Tiefe: 4, erlaubt: 3).
- Z.78 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-group .mud-nav-link' ist zu komplex (Tiefe: 4, erlaubt: 3).
- Z.94 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-group-header .mud-nav-link-icon,body.…' ist zu komplex (Tiefe: 4, erlaubt: 3).
- Z.104 CSS_MaxCssSelectorComplexity — CSS-Selektor 'body.theme-dense-saas .mud-drawer .mud-nav-group-header:hover .mud-nav-link-icon…' ist zu komplex (Tiefe: 4, erlaubt: 3).

#### San.smart.Planner.Platform/wwwroot/css/04-mudblazor.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 14 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.

#### San.smart.Planner.Platform/wwwroot/css/05-components.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 12 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.

#### San.smart.Planner.Platform/wwwroot/css/ai-site-workspace.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 52 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.
- Z.1 CSS_MaxCssLineCount — CSS-Datei hat 333 Zeilen (erlaubt: 300).

#### San.smart.Planner.Platform/wwwroot/css/site-config-workspace.css
- Z.1 CSS_PreferScopedCss — Globale CSS-Datei enthaelt 19 Stil-Regeln (Schwellenwert: 5). Verschiebe komponentenspezifische Stile in eine '.razor.css'-Scoped-CSS-Datei, um den globalen Butterfly-Effekt bei KI-Edits zu eliminieren.

#### San.smart.Planner.Platform/wwwroot/js/amaChatInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/amaSsmsInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/datatableDragInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/datatableLayoutInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/fontsReady.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanConstants.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 346 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanDom.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanDropGeometry.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanExternalDrop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 248 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanInteraction.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 255 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 358 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanInteropCallbacks.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanItemInteraction.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 442 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanItemRemove.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanLayout.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 176 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanMapping.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 337 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanRegistry.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanResourceColumns.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 220 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanSearch.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanSelection.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanStage.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 450 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanTimeAxis.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 562 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanTimeAxisDraw.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 268 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanTimeSnap.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanTimeZone.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 166 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanViewport.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/san-timelineview/sanViewportPresets.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

#### San.smart.Planner.Platform/wwwroot/js/schedulerResourceChromeInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.
- Z.1 JS_MaxJsLineCount — JavaScript-Datei hat 395 Zeilen (erlaubt: 150). Komplexe Logik gehoert in C# (Blazor). Teile die Datei auf oder migriere Logik nach C#.

#### San.smart.Planner.Platform/wwwroot/js/sitePageSplitInterop.js
- Z.1 JS_EnforceJsModules — JavaScript-Interop-Dateien muessen als ES6-Module aufgebaut sein (mindestens ein 'export'-Statement). Blazors Dynamic Import erwartet Module; globale Script-Dateien koennen nicht per 'IJSRuntime.InvokeAsync' isoliert importiert werden.

### Tests (19 Dateien)

#### San.smart.Planner.Platform.Tests/Components/Admin/Ai/AiSiteWorkspaceStateHubTests.cs
- Z.141 BanBlockingTaskAccess — Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.
- Z.356 BanPublicNestedTypes — Der Typ 'AiSiteWorkspaceStateHubTests.FakeAiChatHubClient' ist ein internal nested Type.

#### San.smart.Planner.Platform.Tests/Components/Admin/Ai/AiWorkspaceBunitSupport.cs
- Z.233 BanPublicNestedTypes — Der Typ 'AiWorkspaceBunitSupport.FakeAiChatHubClient' ist ein internal nested Type.

#### San.smart.Planner.Platform.Tests/Components/Admin/Ai/FakeMudDialogService.cs
- Z.8 MaxPublicMembersPerType — 'FakeMudDialogService' hat 24 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.
- Z.136 BanBlockingTaskAccess — Blockierender Task-Zugriff '.Result' erkannt.

#### San.smart.Planner.Platform.Tests/Components/UI/DataTable/DataTableColumnFilterResolverTests.cs
- Z.8 MaxPublicMembersPerType — 'DataTableColumnFilterResolverTests' hat 22 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Components/UI/DataTable/DataTableTestSupport.cs
- Z.53 MaxBoolParameterCount — 'CreateSiteWithInteractiveFlags' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform.Tests/Components/UI/DataTable/DataTableUserConfigPersistTests.cs
- Z.17 MaxPublicMembersPerType — 'DataTableUserConfigPersistTests' hat 19 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Components/UI/Schedulers/SchedulerUserConfigKonvaSyncTests.cs
- Z.353 BanPublicNestedTypes — Der Typ 'SchedulerUserConfigKonvaSyncTestSupport.SchedulerUserConfigKonvaSyncHost' ist ein internal nested Type.

#### San.smart.Planner.Platform.Tests/Handlers/Admin/AdminSiteListSourceLabelTests.cs
- Z.8 MaxBoolParameterCount — 'Format_LiefertErwarteteAnzeige' hat 3 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform.Tests/Handlers/Admin/Ai/Exploration/AdminAiExplorationStubToolInvoker.cs
- Z.22 MaxSwitchArms — Switch-Expression hat 23 Arms (erlaubt: 10).
- Z.25 BanBlockingTaskAccess — Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.
- Z.54 BanBlockingTaskAccess — Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.

#### San.smart.Planner.Platform.Tests/Handlers/Admin/Ai/Exploration/AdminAiExplorationTestHost.cs
- Z.43 MaxBoolParameterCount — 'RunScenarioAsync' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.
- Z.54 MaxBoolParameterCount — 'CreateAsync' hat 2 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform.Tests/Handlers/Admin/Ai/SiteAdminAiPluginMutationTests.cs
- Z.9 MaxPublicMembersPerType — 'SiteAdminAiPluginMutationTests' hat 24 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Handlers/Admin/Ai/SiteAdminAiPluginTestSupport.cs
- Z.13 MaxBoolParameterCount — 'DataTableUpsert' hat 4 bool-Parameter (erlaubt: 1). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.

#### San.smart.Planner.Platform.Tests/Handlers/DataTableCellFormatTests.cs
- Z.7 MaxPublicMembersPerType — 'DataTableCellFormatTests' hat 18 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Handlers/DataTableHandlerSettingsTests.cs
- Z.12 MaxPublicMembersPerType — 'DataTableHandlerSettingsTests' hat 26 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Handlers/TimelineViewLabSchedulerHandlerTests.cs
- Z.14 MaxPublicMembersPerType — 'TimelineViewLabSchedulerHandlerTests' hat 19 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Infrastructure/Sql/SqlConnectionFactoryTests.cs
- Z.10 MaxPublicMembersPerType — 'SqlConnectionFactoryTests' hat 20 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Integration/Sage100/Sage100StsSettings.cs
- Z.11 MaxPublicMembersPerType — 'Sage100StsSettings' hat 17 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Support/DataTableUserConfigTestSupport.cs
- Z.19 MaxPublicMembersPerType — 'DataTableUserConfigTestSupport' hat 25 öffentliche Member (erlaubt: 15). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.

#### San.smart.Planner.Platform.Tests/Support/OllamaTestSupport.cs
- Z.57 BanBlockingTaskAccess — Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.

[INFO]: Performance-Messdaten erzeugt unter: C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\measurements\San.smart.Planner.Platform2\2026-06-24\San.smart.Planner.Platform2-2026-06-24-11-46-35-284-d9955820
