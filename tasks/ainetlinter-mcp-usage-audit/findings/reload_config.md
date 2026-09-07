# Finding: `reload_config`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test. `rules.json` wurde **nicht** geschrieben; `configPath` zeigte nie auf eine andere gültige Regeldatei als die bereits aktive.

## 1 Schema-Kurzfazit

Beschreibung steuert den Happy Path **richtig**: nach Änderung an `rules.json` neu einlesen, ohne Server-Neustart. Ohne `configPath` kommt der Rules-Pfad aus `ainetlinter.project.json`; mit `configPath` temporärer Override. Ungültiger Pfad/JSON: bisherige Konfiguration bleibt aktiv. Zielvertrag: `targetType='project'`, `targetPath` absolut/kanonisch. Assembly **ausdrücklich unsupported**.

JSON-Schema schwächer als die Prosa:

| Parameter | Typ | Pflicht | Bemerkung |
|---|---|---|---|
| `targetType` | string | ja | Kein Enum. Validator akzeptiert `project` **und** `assembly`, obwohl das Tool Assembly ablehnt. |
| `targetPath` | string | ja | Absoluter Projektroot. |
| `configPath` | string \| null | nein, Default `null` | Kein Hinweis im Schema, dass der Pfad absolut sein muss; kein `exists`/`json`. |

Keine Limits, kein Cursor, keine Diagnostics-Flags. Fehlercodes stehen nur in den Live-Antworten, nicht im Schema.

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Alle erreichten AiNetLinter-Calls **schnell** (kein Timeout). Keine Truncation-Parameter im Schema, keine Truncation beobachtet.

| # | Absicht | Argumente | Größe | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path, kein Override | `project` + Root, ohne `configPath` | 3 Zeilen, ~280 Zeichen | Erfolg. Vorher/Nachher dieselbe Datei `…\Tests.Logic\AiNetLinter\rules\platform-default.rules.json`, **18** aktivierte Regeln, **unveraendert**. |
| 2 | Pflichtfelder fehlen | `{}` | 1 Zeile | Generisch: `An error occurred invoking 'reload_config'.` Kein Code, kein Hint. |
| 3 | Assembly + echte externe Assembly | `assembly` + `Example.External.Process.dll` | ~6 Zeilen | `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED` + Hint auf `project` / andere Roslyn-Abfrage. |
| 4 | Ungültiger Override | `configPath` = nicht existierende `does-not-exist-rules.json` | ~5 Zeilen | `CONFIG_NOT_FOUND`. Hint: bisherige Konfiguration bleibt aktiv. |
| 5 | Pflicht `targetType` fehlt | nur `targetPath` | 1 Zeile | Wie Call 2: generischer Invoke-Fehler. |
| 6 | Pflicht `targetPath` fehlt | nur `targetType=project` | 1 Zeile | Wie Call 2. |
| 7 | Relativer `targetPath` | `Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: muss absolut sein. Hint nennt fälschlich `'project' oder 'assembly'`. |
| 8 | Ungültiges `targetType` | `bogus` | ~3 Zeilen | `INVALID_ARGUMENT`: muss exakt `'project'` oder `'assembly'` sein — Assembly wird hier als zulässig beworben. |
| 9 | Kein JSON als Override | `configPath=AGENTS.md` | ~5 Zeilen | `CONFIG_INVALID` (ungueltiges JSON?). Hint: bisherige Konfiguration bleibt aktiv. |
| 10 | Unterordner statt Root | `…\Sample.Project\Sample.Project` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` — sucht `ainetlinter.project.json` im Unterordner, liefert JSON-Vorlage. |
| 11 | Leeres `configPath` | `configPath=""` | wie Call 1 | **Erfolg**, identisch Happy Path. Leerstring = kein Override. |
| 12 | Verzeichnis als `configPath` | `configPath=…\Docs` | ~5 Zeilen | `CONFIG_NOT_FOUND` (nicht „ist ein Verzeichnis“). Bisherige Config bleibt. |
| 13 | Case / Canonical | `targetType=Project`, `targetPath` kleingeschrieben | wie Call 1 | Erfolg, 18 Regeln unverändert. |
| 14 | Relatives `configPath` | `platform-default.rules.json` | ~5 Zeilen | `CONFIG_NOT_FOUND` auf den Relativstring. Kein `INVALID_ARGUMENT`, keine Auflösung gegen Projektroot. |
| 15 | Extra-Arg | wie Call 1 plus `bogusParam=true` | wie Call 1 | Erfolg, Extra-Arg still ignoriert. |
| 16 | Override = aktuelle Rules | `configPath` = derselbe `platform-default.rules.json`-Pfad | wie Call 1 | Erfolg, Vorher/Nachher identisch, 18 Regeln unverändert. Kein Write. |
| 17 | Leerer `targetPath` | `targetPath=""` | ~3 Zeilen | `INVALID_ARGUMENT`: Parameter ist erforderlich. (Besser als Call 2/5/6.) |
| 18 | `assembly` + Projektverzeichnis | `assembly` + Root (kein DLL) | ~3 Zeilen | `INVALID_ARGUMENT`: Assembly-Pfad muss vorhandene Datei sein, Hint `.dll`/`.exe`. **Nicht** `ASSEMBLY_TARGET_UNSUPPORTED`. |
| 19 | Explizit `configPath=null` | wie Call 1 | wie Call 1 | Erfolg, identisch Happy Path. |
| — | Fremder Root (Cursor-Wrapper) | `project` + `C:\Workspace\DoesNotExist.Platform` | — | **Nicht bei AiNetLinter angekommen.** Cursor Auto-review hat den Call blockiert (Pfad außerhalb des genannten Projektroots). Workaround: Call 10. |

**Nicht geprüft (harte Grenze):** `configPath` auf eine *andere gültige* JSON/Rules-Datei. Ein erfolgreicher temporärer Override würde die Live-Session für parallele Agenten umbiegen. Call 16 nur dieselbe bereits aktive Datei.

## 3 Verdict

**Teilweise wie beschrieben.** Kernvertrag hält: Reload ohne Datei-Write, Vorher/Nachher-Zeile, 18 Regeln unverändert, Fehlerpfade `CONFIG_NOT_FOUND` / `CONFIG_INVALID` lassen die alte Config stehen, Assembly an echter DLL wird abgelehnt.

Abweichungen: JSON-Schema und Fehler-Hints behandeln `assembly` als gültiges `targetType`; bei Assembly-Pfad, der keine Datei ist, greift der generische DLL-Validator **vor** `ASSEMBLY_TARGET_UNSUPPORTED`. Fehlende Required-Keys im JSON → generischer Invoke-Fehler. Relatives `configPath` ist nicht analog zu `targetPath` (kein Absolut-Zwang, nur `CONFIG_NOT_FOUND`). Extra-Args still. Leeres `configPath` ist undokumentiert gleich `null`.

## 4 Schwere

**`friction`**

Happy Path ist korrekt, klein und für den dokumentierten Zweck brauchbar. Reibung sitzt in Schema/Hints/`targetType`-Validierung und im Error-Shape bei fehlenden Pflichtfeldern — nicht in falschen Rule-Counts oder stiller Config-Mutation. Nicht `broken` (Reload und Stay-Active funktionieren). Nicht `degraded` (keine Token-Last, keine Trunkierung, IDs unwichtig).

## 5 Nutzbarkeit

**ja** — nach einer `rules.json`-Änderung (durch den Menschen, nicht durch diesen Agenten) genau das richtige Tool, ohne Server-Neustart.

Workaround: Beschreibung lesen, nie `targetType=assembly`; `targetPath` absolut auf den Repo-Root mit `ainetlinter.project.json`; `configPath` weglassen oder absolut auf **dieselbe** Rules-Datei; fehlende Pflichtfelder nicht am generischen Invoke-Text debuggen. `PROJECT_NOT_INITIALIZED`-Vorlage nicht wörtlich in einem Unterordner anlegen.

## 6 Bugs

| Befund | Art | Call |
|---|---|---|
| Fehlende Required-Keys → generischer Invoke-Fehler statt `INVALID_ARGUMENT` | Error-Shape | 2, 5, 6 |
| Leerer `targetPath` dagegen maschinenlesbar `INVALID_ARGUMENT` | Inkonsistenz Key fehlt vs. `""` | 17 vs. 6 |
| `targetType=bogus` / Relativpfad-Hint: `'project' oder 'assembly'` obwohl Assembly unsupported | Beschreibung vs. Validator | 7, 8 |
| `assembly` + Verzeichnis → DLL-Pfadfehler, erst echte DLL → `ASSEMBLY_TARGET_UNSUPPORTED` | Validierungsreihenfolge | 18 vs. 3 |
| Relatives `configPath` → `CONFIG_NOT_FOUND` auf den Relativstring, nicht Absolut-Check | UX / Vertrag | 14 |
| Verzeichnis als `configPath` → `CONFIG_NOT_FOUND` statt „ist ein Verzeichnis“ | Error-Qualität | 12 |
| Unbekannte Parameter still ignoriert | Still-Ignore | 15 |
| `PROJECT_NOT_INITIALIZED` legt nahe, `ainetlinter.project.json` im **falschen** Ordner zu erzeugen | Hint-Risiko | 10 |
| Cursor Auto-review blockt `targetPath` außerhalb des Workspace vor AiNetLinter | Wrapper, nicht Server | Fremd-Root |

Kein beobachtetes False Positive der Form „Config geändert“: Calls 1, 11, 13, 15, 16, 19 alle **18 Regeln unverändert**. Fehler 4, 9, 12, 14 sagen explizit Stay-Active. Kein Write an `rules.json`.

False Negative: kein Nachweis, ob *anderes gültiges* JSON als Override angenommen würde (bewusst nicht getestet).

## 7 Token/IDs

- **Tokens:** Sehr sparsam. Erfolg ≈ 3 Zeilen (Pfad + Zähler Vorher/Nachher). Fehler ≈ 3–12 Zeilen. Kein Rule-Dump, keine Violations.
- **IDs:** Keine Symbol-IDs, kein Cursor. Nutzlast enthält den **absoluten Rules-Pfad** und die Zahl aktivierter Regeln — für den Menschen/Folge-`view_file` brauchbar, für andere MCP-Tools keine ID-Kette.
- **Stabilität:** Dieselbe Rules-Datei und `18` in allen erfolgreichen Reloads dieser Session.
- Extra-Args und `configPath=""`/`null` vergrößern die Antwort nicht.
- StructuredContent in der Agent-Antwort nicht sichtbar; der Markdown-Dreizeiler reicht.

## 8 Roslyn-Wünsche

Nicht Roslyn-Semantik (kein Symbolgraph). Config-Reload bleibt ein Session-/Rules-Loader. Wünsche im selben statischen Rahmen:

1. Schema-Enum `targetType: "project"` **ohne** `assembly`; Tool-Fehler `TOOL_TARGET_UNSUPPORTED` / bestehendes `ASSEMBLY_TARGET_UNSUPPORTED` **vor** der DLL-Existenzprüfung.
2. Hints nicht mehr `'project' oder 'assembly'` bei diesem Tool.
3. Fehlende Required-Keys als `INVALID_ARGUMENT` mit Hint, analog zu `targetPath=""`.
4. `configPath`: Absolut-Zwang wie `targetPath`, oder Auflösung gegen Projektroot; Verzeichnis als eigener Code (`CONFIG_NOT_A_FILE`).
5. Unbekannte Parameter ablehnen statt still zu ignorieren.
6. `PROJECT_NOT_INITIALIZED`: Hint „Ziel ist kein Projektroot / eine Ebene höher“, nicht „lege hier eine Definitionsdatei an“.
7. Optional: in der Erfolgszeile `rulesHash` oder `rulesChanged: false`, damit ein Agent ohne Zähler-Vergleich merkt, ob sich etwas getan hat.

## 9 Phase 3

Welle 3: AiNetLinter-Pfad/Symbol + Ansatz (read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`). Kein Symbolgraph; Session-/Schema-Vertrag.

### Fehlende Required-Keys → generischer Invoke-Fehler; `targetPath=""` dagegen `INVALID_ARGUMENT`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\ServerMaintenanceToolRegistrations.cs`; Validierung `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`
- **Symbol:** `ServerMaintenanceToolRegistrations.AddReloadConfig`; `AnalysisTargetResolver.Resolve`
- **Roslyn-Ansatz:** nicht anwendbar. Lambda hat `string targetType, string targetPath` ohne Default — fehlende Keys scheitern im MCP-SDK vor dem Handler (Cursor: `An error occurred invoking`). `targetPath=""` erreicht `Resolve` (`IsNullOrWhiteSpace` → `INVALID_ARGUMENT`). Ansatz: Parameter nullable machen und dieselbe `Resolve`-Prüfung nutzen, damit Key fehlt und `""` denselben Code liefern.

### `targetType=bogus` / Relativpfad-Hint nennt `'project' oder 'assembly'` obwohl Assembly unsupported

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; Vertrag `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs`
- **Symbol:** `AnalysisTargetResolver.Invalid` (Default-Hint); `McpToolRegistrationOptions.ProjectTargetContract` / `ReloadConfigTool`
- **Roslyn-Ansatz:** nicht anwendbar. `ResolveTargetType` akzeptiert beide Literale für alle Tools. Ansatz: Tool-spezifischen erlaubten `targetType`-Satz in `Resolve` übergeben (hier nur `project`); Default-Hint ohne `assembly`. JSON-Schema: `targetType` als Enum nur `project` (SDK leitet Schema aus dem Lambda ab — Enum-Typ oder Beschreibung, kein freies `string`).

### `assembly` + Verzeichnis → DLL-Existenzcheck vor `ASSEMBLY_TARGET_UNSUPPORTED`

- **Pfad:** `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\AnalysisTargetResolver.cs`; Ablehnung `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`
- **Symbol:** `ProjectAnalysisDispatcher.ExecuteProjectAsync`; `AnalysisTargetResolver.Resolve` (File.Exists / `IsSupportedAssemblyPath` vor Rückgabe); `ProjectAnalysisDispatcher.UnsupportedAssemblyTarget` → `AssemblyAnalysisResponse.Unsupported`
- **Roslyn-Ansatz:** nicht anwendbar. Reihenfolge: Resolver prüft Assembly-Datei, erst danach `if (target.TargetType == Assembly) return Unsupported…`. Ansatz: bei project-only-Tools `targetType=assembly` sofort `ASSEMBLY_TARGET_UNSUPPORTED` (wie Description/`ProjectTargetContract`), ohne `File.Exists`.

### Relatives `configPath` → `CONFIG_NOT_FOUND`; Verzeichnis → derselbe Code

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\ReloadConfigTool.cs`; Loader `src\AiNetLinter\Configuration\ConfigLoader.cs`
- **Symbol:** `ReloadConfigTool.ExecuteAsync` (`File.Exists(targetPath)` ohne `Path.IsPathFullyQualified`; Verzeichnis: `File.Exists` ist false); `ConfigLoader.TryLoadConfig`
- **Roslyn-Ansatz:** nicht anwendbar. Ansatz analog `AnalysisTargetResolver.ResolveCanonicalPath`: relatives `configPath` → `INVALID_ARGUMENT` (Absolut-Zwang) oder Auflösung gegen `lease.Definition`/`ainetlinter.project.json`. Vor `File.Exists`: `Directory.Exists` → eigener Code (`CONFIG_NOT_A_FILE`), nicht `CONFIG_NOT_FOUND`.

### Unbekannte Parameter still ignoriert

- **Pfad:** `src\AiNetLinter\Mcp\Registration\ServerMaintenanceToolRegistrations.cs` (Lambda `McpServerTool.Create`)
- **Symbol:** `AddReloadConfig` — keine lokale Property-Reject-Logik; Extra-Args verschluckt der MCP-SDK-Deserializer
- **Roslyn-Ansatz:** nicht anwendbar. Ansatz: unbekannte JSON-Member ablehnen (SDK-Option/`UnmappedMemberHandling` oder explizite Allow-List im Handler) → `INVALID_ARGUMENT`.

### `PROJECT_NOT_INITIALIZED` legt nahe, Definition im Unterordner anzulegen

- **Pfad:** `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`; Lease über `src\AiNetLinter\Mcp\Projects\ProjectToolCall.cs`
- **Symbol:** `ProjectDefinitionLoader.Load` / `NotInitializedTemplate`; `ProjectToolCall.ResolveLease`
- **Roslyn-Ansatz:** nicht anwendbar. `reload_config` least den Key; fehlt `ainetlinter.project.json`, kommt die Bootstrap-Vorlage. Ansatz für dieses Tool: Hint „Ziel ist kein Projektroot / eine Ebene höher“, Vorlage nicht wörtlich für den übergebenen Unterordner anbieten. (Cursor Auto-review bei Fremd-Root liegt außerhalb dieses Repos.)

### Leeres `configPath` = `null` (undokumentiert)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\ReloadConfigTool.cs`
- **Symbol:** `ReloadConfigTool.ExecuteAsync` (`string.IsNullOrWhiteSpace(configPath) ? defaultRulesPath : configPath`)
- **Roslyn-Ansatz:** nicht anwendbar. Entweder in Description/`configPath`-Schema festhalten oder `""` als `INVALID_ARGUMENT`.

### Optional: `rulesHash` / `rulesChanged` in der Erfolgszeile

- **Pfad:** `src\AiNetLinter\Mcp\Tools\ServerMaintenance\ReloadConfigModels.cs`; `ReloadConfigTool.cs`
- **Symbol:** `ReloadConfigPayload`; `ReloadConfigTool.BuildSummary` / `CountEnabledRules`
- **Roslyn-Ansatz:** nicht anwendbar. Hash über den gelesenen Rules-JSON-Text (oder kanonische Serialisierung von `Config`), Delta bereits in `EnabledRuleDelta`; Flag `rulesChanged: false` wenn Pfad+Zähler+Hash gleich.
