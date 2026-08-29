# Step-033 Ergebnis: CacheRoot und Refresh-Policy anbinden

## Status

Der fachliche Step-033-Code ist im Commit
`0c6ab50e8e76c2d61f6a8f5e5ec088b963b7ea28` enthalten. Dieser Nachweis wurde
nach Code, Tests, Build und den scoped MCP-Prüfungen erstellt. Ein
nachfolgender Dokumentationscommit enthält ausschließlich diese Ergebnisdatei.
`task-state.md`, `roadmap.md` und `tech-debt.md` wurden nicht geändert.

## Vertragsabdeckung

- `ExternalSources:CacheRoot` ist optional, aber bei expliziter Angabe strikt
  validiert. Nichtleere relative Werte werden relativ zur Settings-Datei
  aufgelöst; absolute Werte werden kanonisiert. Leere/Whitespace-Werte,
  URI-artige Werte mit `://`, `.`-/`..`-Segmente und ungültige JSON-Typen
  werden fail-closed abgewiesen. Die Diagnose echo't weder den Eingabepfad
  noch mögliche Credentials.
- Die bestehende Root-/Reparse-/Ownership-Sicherheitsgrenze bleibt der letzte
  Guard. Die konfigurierte Cache-Elternwurzel ist genau `CacheRoot`, die
  effektive Repository-Source-Root genau `<CacheRoot>/source`; der
  `source`-Name ist im Cache-Vertrag zentralisiert.
- `ExternalSources:RefreshIntervalMinutes` akzeptiert ausschließlich positive
  integrale JSON-Werte von `1` bis zur ganzzahligen
  `TimeSpan`-Minutengrenze. Brüche, Exponenten, Bool/String/null, 0,
  negative Werte und Overflow werden ohne stillen Fallback abgewiesen.
- Fehlende Settings, ein fehlender `ExternalSources`-Abschnitt und fehlende
  optionale Cache-Felder behalten die bestehenden Defaults: Cache-Parent
  `<AppContext.BaseDirectory>/cache` und Refresh-Intervall 60 Minuten.
  Explizit ungültige Cache-Felder machen den gesamten ExternalSources-Load
  ungültig; unsichere Konfiguration wird nicht stillschweigend korrigiert.
- Der validierte Optionspfad führt Parent-Root und Intervall über die
  Cache-Options-Factory in Writer/Reader und Refresh-Policy. Die neue
  Acquirer-Factory nutzt diese Konstruktion lokal, ohne Host-/MCP-Wiring zu
  verändern. Bestehende Writer-/Reader-/Policy-Injektionen bleiben erhalten.
- Fresh/Stale verwendet das konfigurierte Intervall deterministisch. Der
  bestehende Publish-/Reuse-/Refresh-/Generation-/Pointer-/Ownership- und
  Cancellation-Vertrag einschließlich Current-Race, Reparse-/1314- sowie
  HTTP-/Git-/Process-/Native-Semantik wurde nicht umgebaut.

## Geänderte Dateien

- `Docs/configuration.md`
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheConfigurationTests.cs`
- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
- `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirerFactory.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheOptionsFactory.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshPolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
- `tasks/decompiled-assembly-analysis/step-032/step-result.md`
- `tasks/decompiled-assembly-analysis/step-033/step-result.md`

Alle geänderten Testdateien liegen unter `MaxLineCount=500`. Es wurden keine
Remote-, Gitea- oder Git-Netzwerkzugriffe ausgeführt und keine fremden
Assemblies geladen, restauriert, gebaut oder getestet.

## Deterministische lokale Nachweise

Die fokussierte Suite deckt Defaults, relative/absolute und ungültige
`CacheRoot`-Werte, geheime-freie Diagnosen, Intervallgrenzen, Mapping-/Loader-
Integration, `<CacheRoot>/source` sowie Fresh/Stale mit konfiguriertem
Intervall ab. Der konfigurierte Stale-Test verwendet einen festen
`TimeProvider`, ein 1-Minuten-Intervall und beobachtet Fetch, neue Generation
und request-eigenen Checkout ohne Delay.

| Lauf | Ergebnis |
|---|---:|
| fokussierte Loader-/Cache-/Refresh-Suite | 47 bestanden, 0 Skips, 47 gesamt |
| `dotnet build --no-restore` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore` | 2.091 bestanden, 2 Skips, 2.093 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore` | 370 bestanden, 0 Skips, 370 gesamt |
| Stress | nicht ausgeführt |

Die beiden echten, transparenten FastTest-Skips sind:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide betreffen `ERROR_PRIVILEGE_NOT_HELD (1314)` beim Erzeugen des realen
Reparse-/Symlink-Falls. Der Skip ist kein Sicherheitsnachweis; unter
entsprechender Win32-Berechtigung sind die Tests erneut auszuführen.

Nach den Testläufen waren keine aktiven `testhost`-, `vstest`- oder Test-
`dotnet`-Prozesse vorhanden. Drei vorhandene idle `dotnet MSBuild.dll`
Node-Reuse-Prozesse blieben unangetastet; es wurden keine Prozesse gelöscht.

## MCP- und Qualitätsnachweis

Alle semantischen MCP-Läufe verwendeten den absoluten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wurde nur für
Text-/Dateisuche verwendet.

- `get_feature_context` für `ExternalSourceCacheOptions`,
  `ExternalSourceRepositoryCacheOptionsFactory` und
  `ExternalSourceRepositoryAcquirerFactory` ergab jeweils 0 Violations.
  Die gemessenen Type-LOC/AI-Footprints waren 27/323, 19/968 und 19/1856.
  Der Acquirer blieb nach dem Factory-Split unter 500 Zeilen; der gesamte
  fokussierte Violation-Scope blieb sauber.
- `get_violations` mit den Scopes
  `src/AiNetLinter/Configuration/ExternalSourceConfiguration` und
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository` ergab jeweils 0
  Violations.
- `find_duplicates` mit `minTokens=20`, `mode=exact`,
  `normalizeIdentifiers=false`, `maxResults=100` ergab 0 Cluster in
  `src/AiNetLinter/Configuration` (76 Produktionsmethoden),
  `src/AiNetLinter/Mcp/Assemblies` (370 Produktionsmethoden),
  `src/AiNetLinter.FastTests/Configuration` (71 Testmethoden) und
  `src/AiNetLinter.FastTests/Mcp/Assemblies` (141 Testmethoden).
- `find_magic_values` mit `valueType=all`, `category=all`,
  `minOccurrences=1`, `maxResults=200`, `includeTests=true`,
  `includeSuppressed=false`, `changedOnly=false` ergab 40 Treffer/40 unique
  Einträge im Configuration-Scope und 8 Treffer/8 unique Einträge im
  Cache-Scope. Das sind scoped Befunde einschließlich bestehender
  Diagnose-/Vertragskonstanten; ein changed-only-Claim wird nicht erhoben.
- `find_dead_code` mit `accessibility=private_internal`,
  `confidence=high`, `kind=all`, `includeTests=true`, `mode=members` und
  `maxResults=200` ergab 0 Kandidaten im Configuration- und im Cache-Scope.
- Der breite `safeguard`-Lauf für `scopeFilter=src/AiNetLinter/Mcp/Assemblies`,
  `minScore=8`, `maxViolations=100` ergab 5,80/10 bei Threshold 8,00:
  FAIL, drei breite Struktur-/Footprint-Befunde. Dazu gehören aktuell der
  `MaxDirectoryChildren`-Befund für `Assemblies` (58 Einträge), der bereits
  bestehende `DaemonHostCommand`-Footprint (2975 > 2500) und der breite
  Task-Directory-Befund (41 Einträge). Diese Befunde werden nicht als enge
  Cache- oder Repository-Violations ausgegeben und nicht global bereinigt.
- Symbolbasierte `get_impact`-Läufe wurden für Options-, Loader-, Factory-
  und Acquirer-Symbole verwendet. Der bekannte leere Git-Diff-Impact aus
  Step 032 wird nicht als semantischer Nachweis verwendet; das zugehörige
  Observability-Feedback und die Einschränkung sind in der korrigierten
  Step-032-Ergebnisdatei ehrlich dokumentiert.

## Korrektur des Step-032-Nachweises

`tasks/decompiled-assembly-analysis/step-032/step-result.md` bezieht die
reproduzierbare Evidenz nun ausdrücklich auf den geprüften Step-032-Commit
`59d979b76ea8cabb32a119db5341e4bce8955675` und trennt Baseline von Ergebnis.
Korrigiert dokumentiert sind:

- Safeguard 5,83/10 bei Threshold 8,00: FAIL;
- 369 Produktionsmethoden und 140 Testmethoden im dokumentierten DRY-Lauf;
- der breite Assemblies-/Directory-Befund einschließlich des bestehenden
  `MaxDirectoryChildren`-Befunds gegenüber dem engen
  `ExternalSourceRepository`-Violation-Scope mit 0 Violations;
- kein unbelegter changed-only-Magic-Value-Claim: ohne konkreten Dateisatz
  und passenden Zustand wird er nicht behauptet;
- der leere `get_impact`-Git-Diff und das Observability-Feedback als
  Einschränkung, nicht als semantischer Impact-Nachweis;
- keine neuen globalen DRY-, Magic-Values-, Dead-Code- oder Tech-Debt-Claims.

## Scope und offene Risiken

Nicht verändert wurden Host-/MCP-Wiring, Provider-/Snapshot-/Registry-Design,
EPIC-05, Retention/GC/Invalidierung/Telemetrie, Dirty-/Health-/degraded-/
Failure-Policy, der Fetch-/Transport-/Credential-/Native-/Process-Vertrag und
der Assembly-Cache-Umbau. `task-state.md`, `roadmap.md`, `tech-debt.md` und
Agent-Regeln wurden nicht synchronisiert.

Die neue interne Acquirer-Factory bildet die Konfigurationsnaht ab; die
produktive Host-/MCP-Aktivierung bleibt bewusst ein späterer Folge-Vertrag.
Der breite Safeguard bleibt wegen bestehender Directory-/Footprint-Befunde
unter dem Schwellenwert. Die beiden privilegierten 1314-Reparse-Fälle sind
weiterhin die einzige Testausnahme im Nicht-Stress-FastTestlauf.
