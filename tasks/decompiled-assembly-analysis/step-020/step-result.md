---
status: done
type: step-result
task: decompiled-assembly-analysis
step: "020"
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T08:45:46+02:00
code_commit_hash: 2c2a2c01666e370694bc78f7b748d5a988219b4e
status_after: done
blocker_category: n/a
---

# Result Step 020: Git-Prozesslebenszyklus und statusbewusste Fehlerklassifikation an der Transportgrenze korrigieren

## Zusammenfassung

Der `ExternalSourceGitProcessExecutor` erfasst stdout/stderr jetzt mit einem
64-KiB-Limit, beobachtet beide Reader bounded und führt jeden Fehlerpfad nach
dem Prozessstart über einen gemeinsamen, fünf Sekunden begrenzten Cleanup mit
`Kill(entireProcessTree: true)`. Caller-Cancellation behält den Originaltoken,
Timeout bleibt als typed Result sichtbar und primäre Nicht-Cancellation-
Ausnahmen werden mit Cleanup-Fehlern angereichert statt verschluckt.

Die statusbewusste Git-/HTTP-Projektion erkennt 400/500 als
`InvalidResponse`, unterscheidet 401 ohne/mit Credential, 403 und 404 und
klassifiziert statuslose Netzwerkfehler nur aus eng begrenzter Git-Evidenz;
lokalisierte, unbekannte und bloße URL-/Texttreffer fallen konservativ auf
`InvalidResponse` zurück. Die gemeinsame strikte HTTP(S)-URL-Policy schließt
Userinfo, Query und Fragment aus; Produktion und Tests nutzen dieselbe
`ExternalSourceRepositoryTransportResult.Success`-Factory.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` — bounded
  Reader, Cleanup-Zeitbudget, Prozessbaum-Abbruch und getrennte
  Cancellation-/Timeout-/Primärausnahmepfade.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessContracts.cs` — aus
  dem Executor ausgelagerter Request-/Result-Vertrag samt benannter
  Result-Optionen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` —
  strikt kontextgebundene HTTP-Status- und Git-Evidenz sowie gemeinsame
  Repository-URL-Policy.
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs` — Nutzung der
  gemeinsamen URL-Policy und Success-Factory; Clone-/HEAD-/Credential- und
  1314-/Reparse-Semantik unverändert.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
  Mapping-URL-Prüfung auf die gemeinsame Policy umgestellt.
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` — gemeinsamer
  interner Success-Builder auf dem bestehenden Result-Typ.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs` —
  direkte 400/401/403/404/500-, Credential-, Netzwerk-, Timeout-,
  Protokoll-, lokalisierte und unbekannte Output-Regressionen.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` —
  gemeinsame Success-Factory sowie Query-/Fragment-/Userinfo-/Schema-URL-
  Regressionen.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs` —
  vier lokale Real-Executor-Tests für ProcessStartInfo, ArgumentList,
  Redirects, stdin, WorkingDirectory, GIT-Umgebung, bounded Output und
  Child-/Grandchild-Cleanup.
- `tasks/decompiled-assembly-analysis/step-020/step-plan.md` — Status auf
  `done (pending audit)` gesetzt.
- `tasks/decompiled-assembly-analysis/step-020/step-result.md` — dieses
  Resultat nach Template.

Nicht geändert wurden `task-state.md`, `roadmap.md`, `codemap.md` und
`tech-debt.md`.

## Commit

- **Code-Commit-Hash:** `2c2a2c01666e370694bc78f7b748d5a988219b4e`
- **Message:**
  ```text
  fix: Git-Prozesspfad absichern [decompiled-assembly-analysis]

  Begrenze Output-Drains und sichere den Prozessbaum bei Timeout, Cancellation und Fehlern.

  Ordne Git-/HTTP-Ausgaben statusbewusst ein und teile URL-Policy sowie Success-Factory.

  Refs: tasks/decompiled-assembly-analysis/step-020
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater Commit nach diesem Code-Commit.

## Build-/Test-Output

```text
dotnet test src/AiNetLinter.FastTests --filter Category=Unit → grün (1361 bestanden, 0 übersprungen, 1361 gesamt)
dotnet test src/AiNetLinter.FastTests --filter Category=Component → grün (633 bestanden, 1 Skip, 634 gesamt)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaGitRepositoryTransportTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" → grün (54 bestanden, 1 Skip, 55 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~ExternalSourceGitProcessExecutorTests → grün (4 bestanden, 0 übersprungen, 4 gesamt)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1994 bestanden, 1 Skip, 1995 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (364 bestanden, 0 übersprungen, 364 gesamt)
```

Der einzige Skip ist der bestehende repository-spezifische Reparse-Test,
wenn die Umgebung `ERROR_PRIVILEGE_NOT_HELD` (1314) nicht bereitstellt. Der
bestehende `ProviderUnavailable`-/Decompilation-Fallback blieb unverändert;
es wurde kein globaler Preflight eingeführt. Stress-Tests wurden nicht
ausgeführt.

## Prozess- und Credential-Sicherheitsnachweis

Der Real-Harness startet ausschließlich lokale `pwsh`-Skripte aus
`TestTempDirectory`; er verwendet weder Git noch Gitea, HTTP, Netzwerk,
Remote oder externe Hosts. Er prüft die echte `ProcessStartInfo`, sichere
`ArgumentList`-Übergabe mit Shell-Metazeichen, Redirects, nicht umgeleitetes
stdin, Arbeitsverzeichnis, Entfernung geerbter `GIT_*`-Variablen und eine
explizite marker-only Umgebung. Ein enger Environment-Lock und `finally`-
Cleanup stellen die ursprüngliche Umgebung wieder her; Child- und
Grandchild-IDs werden mit endlichen `TestWaiter`-Grenzen beobachtet und bei
Bedarf resilient beendet. Es werden keine Credentials oder Secret-Marker
verwendet; Testausgabe, Diagnosen und Result-Projektion enthalten keine
Rohmeldungen oder Secret-Werte.

## Abweichungen vom Plan

Der Plan sah den Real-Harness unter `FastTests` vor. Das dortige
`FastTestsDependencyGuardTests`-Gate verbietet jedoch direkte
`System.Diagnostics.Process`-Referenzen; der erste Unit-Lauf belegte diesen
Konflikt mit `TypeRef:System.Diagnostics.Process`. Daher liegt ausschließlich
der OS-/Prozessbaum-Harness unter `IntegrationTests` und ist dort als
`Integration` markiert. Die Fast-Test-Denylist blieb unverändert, der Harness
bleibt lokal und deterministisch; alle fachlichen Acceptance-Kriterien sind
abgedeckt. Weitere Planabweichungen gibt es nicht. Keine Pakete wurden ergänzt
und keine Provider-/Snapshot-/Host-/Refresh-/Fetch-/Cache- oder
Source-of-Truth-Grenze geöffnet.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- `get_feature_context`, `get_impact`, `find_references` und die vorherige
  Symbol-/Testkontextanalyse wurden mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Der Executor
  hat 448 Typzeilen, 406 Type-LOC und 0 Violations; die direkte Zuordnung
  zeigt vier Real-Executor-Tests. Scoped Violations sind in Produktion,
  `FastTests` und dem Integration-Harness jeweils 0.
- `safeguard` passierte mit 8,89/10. Der einzige angezeigte Hinweis ist der
  bekannte, außerhalb dieses Scopes liegende `DaemonHostCommand`-
  `AIContextFootprint`-Warnhinweis.
- `find_duplicates` fand im Produktionsscope 0 Cluster bei 223 Methoden und
  im berührten Fast-Testscope 0 Cluster bei 64 Methoden. Der URL-Klon und der
  doppelte Success-Builder sind durch gemeinsame Policy/Factory entfernt;
  kein unabhängiger Sweep wurde eröffnet.
- Der geänderte Produktionsscope meldete im `find_magic_values`-Audit 18
  Kandidaten in sechs Dateien. Die verbleibenden Befunde sind benannte
  Buffer-/Protokollkonstanten, bestehende lokalisierte Exception-Texte und
  etablierte Git-Argumente; die neuen HTTP-Statusprüfungen verwenden
  `HttpStatusCode` statt nackter Statuscode-Literale.
- `find_dead_code` fand nur die zwei bekannten Low-Confidence-Kandidaten
  `AssemblyOrigin.Kind` und
  `AssemblySourceSelectionOrchestrator.CreateFromSettings`; im neuen
  Prozess-/Transportpaket entstand kein Dead-Code-Fund. TD-001 bis TD-004
  wurden nicht ausgeweitet.

## Beobachtungen

- Der Executor beendet nach Prozessstart sowohl Timeout-/Cancellation- als
  auch Reader-/Wait-/sonstige Fehlerpfade über dasselbe bounded Cleanup und
  beobachtet fehlerhafte Output-Tasks, ohne auf unbounded `Task.WhenAll`-
  Drains zu warten.
- Die direkte Fehler-Matrix deckt 400, 401 ohne/mit Credential, 403, 404,
  500, widersprüchliche 401/404-Evidenz, statuslose Netzwerkfehler, Timeout,
  Protokollfehler sowie lokalisierte und unbekannte Ausgaben ab. Ein bloßes
  `404`, `not found`, `403` oder `unable to access` in URL/Text klassifiziert
  keinen HTTP-Status.
- Die strikte URL-Policy ist auf Acquirer und Transport begrenzt; die
  öffentliche Mapping- und Provider-/Snapshot-Grenze blieb unverändert.

## Bekannte Unschärfen

- Der Real-Harness beweist den tatsächlichen lokalen .NET-Prozesslebenszyklus
  mit `pwsh`, nicht eine echte Git-/Gitea-Installation oder provider-
  spezifische Remote-Ausgaben. Diese Verbindungen sind ausdrücklich nicht
  Teil dieses Steps.
- Die Fehlerklassifikation akzeptiert bewusst nur wenige vollständige,
  kontextgebundene Git-/HTTP-Zeilen und bekannte statuslose Netzwerkformen.
  Andere lokalisierte oder provider-spezifische Ausgaben bleiben konservativ
  `InvalidResponse`.
- Der 1314-Reparse-Skip hängt von der lokalen OS-Berechtigungsfähigkeit ab
  und wurde weder gefakt noch durch eine globale Vorabprüfung ersetzt.

## Falls Status `blocked`

Nicht zutreffend.
