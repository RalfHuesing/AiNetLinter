---
status: done
type: step-result
task: decompiled-assembly-analysis
step: "019"
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T07:25:50+02:00
code_commit_hash: b8fb5471d8ae8583c73eddd5e86c75a4751230e7
status_after: done
blocker_category: n/a
---

# Result Step 019: Produktiven Git-over-HTTP-Transport mit injizierbarer Authentifizierung für den Default-Branch-Clone bauen

## Zusammenfassung

Der bestehende `IGiteaRepositoryTransport` besitzt jetzt einen produktiven,
injizierbaren Git-over-HTTP(S)-Adapter für einen initialen
`--single-branch`-/`--no-tags`-Clone ohne Branchauswahl; Git bestimmt den
Default-Branch und `rev-parse --verify HEAD` muss eine valide 40- oder
64-stellige Hex-Revision liefern. Ein kurzlebiger Credential-Resolver speist
Username und Secret ausschließlich über einen kontrollierten Child-Process-
Credential-Helper-Kanal ein; Prompt, globale Git-Konfiguration und vererbte
`GIT_*`-Umgebung sind für den Prozess kontrolliert. Exitcode-, Auth-/Access-,
Repository-, Netzwerk-, Timeout- und Protokollfehler werden zentral auf die
bestehenden Failure-Klassen und Diagnosecodes projiziert; Cancellation bricht
den Prozessbaum kontrolliert ab und bleibt `OperationCanceledException`.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceCredentialResolver.cs` (neu) — flüchtiger, disposabler In-Memory-Credential-Vertrag ohne Mapping-, Result- oder Persistenzfelder.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` (neu) — asynchroner Child-Process-Executor mit sicherer Argumenttrennung, kontrollierter Umgebung, Timeout, Prozessbaum-Abbruch sowie stdout/stderr-/Exitcode-Auswertung.
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs` (neu) — Default-Branch-Clone, Promotion aus dem temporären Clone-Verzeichnis, HEAD-Revision und Secret-freie Ergebnisprojektion hinter `IGiteaRepositoryTransport`.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` — zentrale Git-Process-Fehlerklassifikation und Wiederverwendung der bestehenden Failure-/Diagnoseprojektion; Step-018s 1314-/Reparse-Regel blieb unverändert.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs` (neu) — deterministische Executor-/Credential-Doubles für Erfolg, Single-Branch-/No-Tag-Argumente, Revision, Secret-Nichtleck, typisierte Fehler, Timeout, Cancellation/Abbruch, invaliden HEAD und Acquirer-Cleanup.
- `tasks/decompiled-assembly-analysis/step-019/step-plan.md` — Status auf `done (pending audit)` gesetzt.
- `tasks/decompiled-assembly-analysis/step-019/step-result.md` (neu) — dieses Resultat nach Template.

## Commit

- **Code-Commit-Hash:** `b8fb5471d8ae8583c73eddd5e86c75a4751230e7`
- **Message:**
  ```
  feat(mcp): Git-Transport absichern [decompiled-assembly-analysis]

  Implementiere Default-Branch-Clone, Laufzeit-Credentials, Fehlerprojektion und Cancellation.

  Refs: tasks/decompiled-assembly-analysis/step-019
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Code-Commit.

## Build-/Test-Output

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaGitRepositoryTransportTests" --no-restore → grün (12 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.FastTests --filter Category=Unit --no-restore → grün (1361 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.FastTests --filter Category=Component --no-restore → grün (619 bestanden, 1 Skip, 620 gesamt)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1980 bestanden, 1 Skip, 1981 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler, 0 übersprungen)
```

Der einzige Skip ist der bestehende repository-spezifische Windows-Reparse-
Test, wenn die Umgebung `ERROR_PRIVILEGE_NOT_HELD` (1314) nicht bereitstellt;
dieser Zustand bleibt der bestehende `ProviderUnavailable`-/Decompilation-
Fallback und wurde nicht global vorgeprüft. Stress-Tests wurden nicht
ausgeführt.

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Die technische Prozess-Executor-Injektion ist
als separater interner Seam gekapselt, damit der fachliche Port
`IGiteaRepositoryTransport` und der Credential-Resolver schmal bleiben. Es
wurden keine Pakete ergänzt und kein Host-/Provider-/Acquirer→Snapshot-
Wiring, Refresh, Fetch, persistenter Cache oder Source-of-Truth-Publishing
geöffnet. `task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md`
blieben unverändert.

## Beobachtungen

- AiNetLinter-MCP wurde mit absolutem `projectRoot`
  `C:/Daten/Entwicklung/Ralf/AiNetLinter` für Feature-Context, Symbolkörper,
  Referenzen, Impact, Violations und die Qualitätsaudits verwendet. Die fünf
  geänderten Code-/Testdateien haben jeweils 0 Violations; der scoped
  Safeguard besteht mit 8,89/10. Der einzige Safeguard-Treffer ist der
  bestehende `AIContextFootprint`-Hinweis in `DaemonHostCommand.cs`.
- Der exakte Produktions-DRY-Scan im Assembly-Scope fand 0 Cluster bei 253
  gescannten Methoden; der Refactoring-Drift-Scan für den neuen zentralen
  Failure-Code-Helper fand 0 Kandidaten bei 253 Methoden.
- Der DeadCode-Scan fand ausschließlich die zwei bekannten Low-Confidence-
  Kandidaten `AssemblyOrigin.Kind` und
  `AssemblySourceSelectionOrchestrator.CreateFromSettings`; im neuen Paket
  entstand kein neuer DeadCode-Befund.
- Der MagicValues-Scan des gesamten Assembly-Scopes meldete 83 bestehende
  bzw. absichtlich protokollgebundene Kandidaten in 29 Dateien. Im neuen
  Transport bleiben vier einmalige Git-Protokoll-Literale (`--single-branch`,
  `--no-tags`, `rev-parse`, `--verify`) bewusst lokal lesbar; die drei festen
  Credential-Umgebungs-/Helper-Bezeichner sind als Nicht-Secret-Protokollnamen
  begründet unterdrückt. Es wurden keine Secret-Werte in Mapping, Argumenten,
  Result, Diagnosen, Logs oder Testausgaben materialisiert.
- Der Transport übernimmt keinen fremden Staging-Cleanup. Er promoted nur
  einen erfolgreichen, reparse-freien Clone; der Acquirer bleibt Eigentümer
  von Checkout, Cleanup und der Prüfung des Solution-Pfads. Dadurch kann ein
  fehlgeschlagener oder revisionsloser Transport kein verfügbares Ergebnis
  liefern, während der bestehende Ownership-Cleanup halbfertige Staging-
  Verzeichnisse entfernt.

## Bekannte Unschärfen

- Die Testdoubles verifizieren den Prozess-/Credential-Vertrag ohne Git,
  Gitea, Remote, Netzwerk oder externen Host. Die reale Git-Installation und
  provider-spezifische HTTP-/stderr-Varianten werden erst im späteren Host-
  Anschluss relevant und sind bewusst nicht Teil dieses Steps.
- Die Fehlerklassifikation nutzt wenige bekannte, intern gehaltene Git-/HTTP-
  Marker nur zur Typisierung; die rohen Ausgaben verlassen den Adapter nicht.
  Weitere lokalisierte oder provider-spezifische Git-Fehlertexte brauchen bei
  einer späteren Integration eine eigene, kontextbegrenzte Entscheidung.
- Der Credential-Resolver ist nur ein Laufzeit-Port. Sichere externe
  Credential-Speicherung, Profilverwaltung, Provider-/Host-Injektion und
  Acquirer→Snapshot-Lifetime bleiben Folge-Schnittstellen.

## Falls Status `blocked`

Nicht zutreffend.
