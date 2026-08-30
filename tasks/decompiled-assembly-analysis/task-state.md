---
status: executing
task: decompiled-assembly-analysis
started_at: 2026-08-28T11:06:28+02:00
last_updated: 2026-08-30T11:10:00+02:00
rules_dir: .agents/rules
total_steps: 37
current_step: step-037
---

# Task State: decompiled-assembly-analysis

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 37 (regulär + Korrekturen)
- **Aktueller Schritt:** `step-037` (`issues`; Verifizierten Checkout
  bis Materialisierung und Publish fail-closed binden)
- **Roadmap:** siehe `roadmap.md`
- **Tech-Debt:** siehe `tech-debt.md`
- **Gestartet:** 2026-08-28T11:06:28+02:00
- **Zuletzt aktualisiert:** 2026-08-30T09:10:00+02:00
- **Initial-Prompt:** siehe `initial-prompt.md`

## Steps

| Step | Epic | Status | Title | Corrects | Coded | Reviewed | Commit |
|------|------|--------|-------|----------|-------|----------|--------|
| step-001 | EPIC-01 | done | Einheitlichen Analysis-Target-Vertrag und Dispatch umstellen | - | f14ff5c2 | issues → step-002 approved | f14ff5c2 |
| step-002 | EPIC-01 | done | MCP-Workflow-Regel auf den neuen Target-Vertrag synchronisieren | step-001 | 7cbc6d45 | approved | 7cbc6d45 |
| step-003 | EPIC-02 | done | Statische Assembly-Session mit Fingerprint, Decompilation und Roslyn-Snapshot | - | 0704b763 | issues → step-004 approved | 0704b763 |
| step-004 | EPIC-02 | done | Assembly-Session-Fundament korrigieren: Cache, Limits, Referenzen und Identität | step-003 | 639f0fc4 | approved | 639f0fc4 + 07d684ca + f6ba0ed8 |
| step-005 | EPIC-03 | done | Expliziten External-Source-Mappingvertrag mit strikter Validierung vorbereiten | - | 7d40cacb | issues → step-006 approved | 7d40cacb + b34b2147 + 692412ed |
| step-006 | EPIC-03 | done | Mapping-Diagnosevertrag und direkte JSON-Regressionen korrigieren | step-005 | c9d71c35 | approved | c9d71c35 + 5d084c9b + 07dc88cf |
| step-007 | EPIC-03 | done | Source-Snapshot-Identität und residente Registry mit injizierbarem Ergebnis | - | cbd79a51 | approved | cbd79a51 + 1c3d2b3c + 7da30606 |
| step-008 | EPIC-03 | done | Deterministische Source-Match-Auflösung über Project.AssemblyName | - | 9511b8f2 | approved | 9511b8f2 + c2ac1473 + a2062fb7 |
| step-009 | EPIC-03 | done | Source-backed Assembly-Context mit deterministischem Decompilation-Fallback verbinden | - | d2814147 | approved | d2814147 + 60c60e52 + aa900d52 |
| step-010 | EPIC-03 | done | Provider-/Registry-Selection für direkte Assembly-Tool-Unterstützung komponieren | - | 28b7b76d | issues → step-011 approved | 28b7b76d + 410550b4 + a92787eb |
| step-011 | EPIC-03 | done | Support-/Lease-Regressionen und Orchestrator-Testzuordnung korrigieren | step-010 | 6e38b4c2 | approved | 6e38b4c2 + d035772c + 65f1c564 |
| step-012 | EPIC-03 | done | Gemeinsame Host-Komposition für direkte Assembly-MCP-Tools verdrahten | - | db386bc4 | issues → step-013 approved | db386bc4 + 12b6dcce + 16ebeda5 |
| step-013 | EPIC-03 | done | Assembly-Host-Wiring und Session-Lifetime absichern | step-012 | 1cd279f0 | approved | 1cd279f0 + 6ba95124 + 723d2a3b |
| step-014 | EPIC-04 | done | Injizierbaren External-Source-Port für Gitea-Auth- und Transportfehler schärfen | - | 3f83c5f2 | approved | 3f83c5f2 + 804f00b0 + 0902a7b7 |
| step-015 | EPIC-04 | issues | Repository-Akquisitionsvertrag mit injizierbarem Gitea-Transport und sicherer Staging-Fassade | - | 3bd71a73 | issues → step-016 | 3bd71a73 + 966ed66a + b1dac89b |
| step-016 | EPIC-04 | blocked | Repository-Akquisitionsgrenze sicher korrigieren | step-015 | 4f49c0bd | blocked → step-017 | 4f49c0bd + b755c955 + 3be96cf1 |
| step-017 | EPIC-04 | blocked | Cancellation-Cleanup und Reparse-Capability-Gate | step-016 | 5d48472c | blocked → privileged rerun | 5d48472c + c7c21e84 + d7757f8f |
| step-018 | EPIC-04 | done | Repository-spezifische Capability-Nichtverfügbarkeit zum Decompilation-Fallback | step-017 | 2b95b3aa | approved | 2b95b3aa + 03589c9e + 784167d8 |
| step-019 | EPIC-04 | issues | Produktiven Git-over-HTTP-Transport mit injizierbarer Authentifizierung für den Default-Branch-Clone bauen | - | b8fb5471 | issues → step-020 | b8fb5471 + 195f29f4 + e5b3f7e3 |
| step-020 | EPIC-04 | issues | Git-Prozesslebenszyklus und statusbewusste Fehlerklassifikation korrigieren | step-019 | 2c2a2c01 | issues → step-021 | 2c2a2c01 + 446d5ff2 + cbb49754 |
| step-021 | EPIC-04 | issues | Git-Prozessbaum- und Timeout-Cleanup-Races korrigieren | step-020 | 51060014 | issues → step-022 | 51060014 + 59c63a3a + f80e5f45 |
| step-022 | EPIC-04 | issues | Native Startfehler und Test-Cleanup fail-closed absichern | step-021 | 872b4855 | issues → step-023 | 872b4855 + f5063d5a + fc061950 |
| step-023 | EPIC-04 | done | Prozessbaum-Fallback und Handle-Cleanup vollständig fail-closed schließen | step-022 | d1b633d0 | approved | d1b633d0 + 5b22d16a + 30b13647 |
| step-024 | EPIC-04 | issues | Erfolgreiches Acquirer→Snapshot-/Workspace-Wiring mit besitzgebundener Lifetime | - | 428cc4b3 | issues → step-025 | 428cc4b3 + f781b127 + 3e726048 |
| step-025 | EPIC-04 | done | Registry-/Snapshot-Lifetime und exception-sicheres Multi-Owner-Cleanup korrigieren | step-024 | 74fc0056 | approved | 74fc0056 + acdfe70e |
| step-026 | EPIC-04 | issues | Persistente Repository-Cache-Generation aus erfolgreichem Clone atomar veröffentlichen | - | da9882f4 | issues → step-027 | da9882f4 + 8a87f06a |
| step-027 | EPIC-04 | issues | Fail-closeden Generation-Publish und Testisolation korrigieren | step-026 | c5d64c42 | issues → step-028 | c5d64c42 + 732737dd |
| step-028 | EPIC-04 | done | Deterministische Read-back- und Lock-Lifetime-Nachweise ergänzen | step-027 | 83e52560 | approved | 83e52560 + d3d17fe1 |
| step-029 | EPIC-04 | issues | Cache-backed Initial Acquisition aus validierter Generation | - | 82692da0 | issues → step-030 | 82692da0 + c0abdcdf |
| step-030 | EPIC-04 | issues | Cache-Reuse-Nachweise und Step-029-Result korrigieren | step-029 | e9bf8025 | issues → step-031 | e9bf8025 + 2510db5e |
| step-031 | EPIC-04 | done | Step-030-Gatebefunde und Nachweise korrigieren | step-030 | 552ef4d4 + 1d15a5b4 | approved | 552ef4d4 + 1d15a5b4 + d8cff007 |
| step-032 | EPIC-04 | issues | Validated Refresh/Fetch in neue Cache-Generation | - | 59d979b7 | issues → step-033 | 59d979b7 + a16a421c |
| step-033 | EPIC-04 | issues | Konfigurierbare Cache-Root-/Refresh-Policy mit Fresh/Stale-Vertrag und Step-032-Evidenzabschluss | step-032 | 0c6ab50e + c6787c12 | issues → step-034 | 0c6ab50e + c6787c12 + d57f5aab |
| step-034 | EPIC-04 | issues | Strikter CacheRoot-Vertrag und fail-closed Konfigurationsweitergabe bis zum Assembly-Tool | step-033 | fcad25e5 + 1dd59128 | issues → step-035 | fcad25e5 + 1dd59128 + ff5fb2e5 |
| step-035 | EPIC-04 | done | ConfigurationFailure unabhängig von Diagnosen terminal bis zum Assembly-Tool propagieren | step-034 | 5c830e44 + 8182b992 | approved | 5c830e44 + 8182b992 + c4ee413c |
| step-036 | EPIC-04 | issues | Gitea-Source-of-Truth mit Clean-Checkout und transparentem degraded Refresh-Vertrag absichern | - | 377b5360 + 39fb9fba | issues → step-037 | 377b5360 + 39fb9fba + c7efaae4 |
| step-037 | EPIC-04 | issues | Verifizierten Checkout bis Materialisierung und Publish fail-closed binden | step-036 | 093f9d7a + 04e37bea | issues → step-038 | 093f9d7a + 04e37bea + 078c3e15 |

## Aktueller Wiederaufnahmevermerk

Step 024 wurde durch Review `3e726048` zunächst nicht freigegeben. Der
Befund `MAJOR-001` betraf ausschließlich `SourceSnapshotRegistry.Dispose()`:
Nach der ersten Snapshot-Dispose-Exception wurden weitere Snapshots nicht
mehr entsorgt, obwohl das Registry-Dispose-Flag bereits gesetzt war.

Step 025 wurde deshalb als Korrektur mit `corrects: step-024` aufgenommen
und ist mit Review `acdfe70e` genehmigt. Der vollständige Registry-Durchlauf,
die deterministische Fehleraggregation/-weitergabe und die Regression mit
mehreren Snapshots sind damit abgeschlossen; Step 025 wird nicht erneut
geöffnet.

Auf Nutzeranweisung wurde Step 026 vor dem Coder einem strengen Split-Gate
unterzogen. Der bisherige Gesamtblock war wegen Cache-Konfiguration,
Cache-Identität/Manifest, Refresh-/Fetch-Transport, Generationen,
Integritätsprüfung, atomarem Pointer und Konkurrenzsynchronisation fachlich
zu breit für einen stabilen Agentenkontext. Der revidierte Step 026 behandelt
nur die persistente Cache-Key-/Manifest-/Generation-Erzeugung und atomare
Current-Veröffentlichung aus dem bestehenden erfolgreichen Clone-/Acquirer-
Ergebnis über einen injizierbaren lokalen Writer. Der request-eigene Checkout
bleibt beim Acquirer; Snapshot-/Workspace-Ownership und Registry-Cleanup aus
Steps 024/025 bleiben die Anschlussgrenze.

Cache-backed Initial Acquisition/Reuse mit validiertem Current-Pointer und
neuem Request-Checkout ist ein eigenes Folgepaket. Fetch/Refresh,
Refresh-Policy und neue Generation bei fälliger Aktualisierung folgen erst
danach. CacheRoot-/Refresh-Konfiguration, Host-/MCP-Wiring,
Dirty-/Health-Policy, transitive Referenzen, Retention/GC und EPIC-05 bleiben
ebenfalls außerhalb dieses Steps.

### Wiederaufnahme nach Step-026-Review (2026-08-29)

Der Kritiker-Review `8a87f06a` hat Step 026 als `issues` zurückgegeben.
Die drei MAJOR-Findings betreffen die unvollständige Same-Key-Sperrlifetime
für Rollback/Cleanup, eine nicht vollständig fail-closed/bounded
Read-back-Prüfung sowie persistente Default-Writer-Ausgaben in Tests.

Auf Nutzeranweisung wurde Step 027 als zusammenhängendes Korrekturpaket
mit `corrects: step-026` geplant und aktiviert. Sein primärer Vertrag ist
ein fail-closed und isolierter atomarer Generation-Publish unter
Synchronisierung des Cache-Keys. Die drei gekoppelten Schichten sind
Lock-/Rollback-Lifetime, unabhängige bounded Manifest-/Content-Prüfung und
der testisolierte Writer-Anschluss.

Step 027 wurde umgesetzt, aber im Review `732737dd` wegen zweier MAJOR-
Nachweislücken nicht freigegeben: Der A/B-Race-Test erzwingt die kritische
Interleaving-Reihenfolge nicht, und die bounded Manifest-/Inventar-Matrix
deckt die geforderten malformed-input- und Inventar-Limitfälle nicht
vollständig ab. Die Produktions-Lock-/Rollback- und Read-back-Korrekturen
bleiben erhalten; Step 027 wird nicht erneut geöffnet.

### Wiederaufnahme nach Step-027-Review (2026-08-29)

Auf Nutzeranweisung wurde Step 028 als einzelner Korrektur-Step mit
`corrects: step-027` geplant und auf `planned/in_progress` gesetzt. Der
primäre Vertrag ist der deterministische Nachweis von atomic Generation
Publish unter adversarial bounded Read-back. Das Paket enthält höchstens
drei gekoppelte Schichten: einen internen TCS-/Semaphore-Test-Seam für die
Lock-/Interleaving-Grenze, die Manifest-/Inventar-Malformed-Input-Matrix
und die Wiederverwendung der bestehenden lokalen Fixtures/Assertions.

Der Coder darf die Produktionslogik aus Step 027 nicht neu gestalten. Eine
interne, per Read-Aufruf isolierte Stream-Seam ist nur zulässig, wenn der
Growth-Fall ohne sie nicht deterministisch nachweisbar ist; Runtime-Default,
Fail-closed-Semantik und In-Process-Lock-Grenze bleiben unverändert. Die
Roadmap wird im Fix-Modus nicht geändert. Es gibt in diesem Planer-Schritt
keine Tests oder Produktionsänderungen; nächster sicherer Übergabepunkt ist
ein neuer Coder-Agent.

## Config

```text
max_fix_rounds_per_step: 3
soft_step_checkin_interval: 40
max_batch_items: 8
max_batch_diff_lines: 40
build_command: dotnet build
test_command: dotnet test src/AiNetLinter.FastTests --filter Category=Unit
target_branch: main
model_planer: nicht festgelegt
model_coder: nicht festgelegt
model_kritiker: nicht festgelegt
```

## Abbruch-/Pause-Bedingungen

- Korrektur-Kettenbudget: maximal 3 Korrekturen pro Kette.
- Weicher Check-in: bei jedem 40. Step vor dem nächsten Step.
- Ein `blocked`-Step pausiert den Loop zur Nutzerklärung.
- DRY-, MagicValues- und DeadCode-Tech-Debt wird in diesem Task proaktiv,
  architektonisch sinnvoll und automatisch an größere laufende Pakete
  angehängt; kein künstlicher Einzel-Sweep.

## Aufgelöster Blocker-Kontext

Der vollständige Integration-Gate-Lauf bleibt wegen drei bestehenden
`DuplicateCode`-Befunden in Testdateien außerhalb des Step-Scopes blockiert:

- `AssemblyAnalysisSessionTests.EmitAssembly` gegenüber
  `AssemblyAnalysisToolTests.EmitAssembly`
- `TextOf` in den beiden Wiring-Contract-Testklassen
- `WaitForConditionAsync` in den beiden Wiring-Contract-Testklassen

Die Nutzerentscheidung lag vor: Die drei bestehenden DRY-Befunde durften im
laufenden Korrekturpaket behoben werden. Der neue Coder hat sie zusammen mit
den übrigen Step-004-Funden behoben; Build und beide vollständigen Nicht-
Stress-Gates sind grün. Ein Kritikerlauf ist wegen des anschließenden
Nutzer-Halts noch nicht erfolgt.

## Haltvermerk (erledigt)

Der Nutzer hatte angewiesen, unmittelbar nach Abschluss dieses Steps zu
stoppen. Der Coder wurde geschlossen; zu diesem Zeitpunkt wurden Kritiker,
weitere Steps, Global-Audit und `task-summary.md` nicht ausgeführt.

## Wiederaufnahme

Auf Nutzeranweisung wurde der Task am 2026-08-28T16:42:56+02:00 fortgesetzt.
Ein neuer Kritiker prüfte Step 004 und genehmigte ihn (`f6ba0ed8`); dieser
Sub-Agent wurde anschließend geschlossen. Für jeden weiteren Rollenaufruf
wird erneut ein neuer Sub-Agent gestartet.

Der erste EPIC-03-Plan wurde vor dem Coder durch das Split-Gate korrigiert:
Step 005 enthält jetzt nur Mapping/Validierung, Pfadauflösung, Diagnosen,
Provider-Port und zugehörige Tests/Doku (`a71465fa`). Snapshot-Identität,
Registry sowie Session-/MCP-Anbindung bleiben ein späteres vertikales Paket.

Der Kritiker fand in Step 005 ein In-Scope-DRY-Duplikat in drei Diagnose-
Hilfsmethoden sowie eine gekoppelte Ungenauigkeit bei doppelten JSON-Feldern
und fehlende direkte Regressionstests (`692412ed`). Step 006 bündelt diese
Befunde als eine kontextbegrenzte Korrekturrunde.

Step 006 wurde durch den neuen Kritiker genehmigt (`07dc88cf`). Die Diagnose-
Fabrik ist zentralisiert, die Duplicate-/Missing-Semantik ist eindeutig und
die direkten Regressionen sind abgedeckt. EPIC-03 bleibt für den nächsten
Snapshot-/Registry-/Session-Schnitt offen.

Step 007 ist als nächster kontextbegrenzter EPIC-03-Schnitt aktiviert
(`bc65e87f`). Er umfasst ausschließlich Source-Snapshot-Identität, eine
residente In-Memory-Registry mit Leases und das injizierbare Provider-Ergebnis;
vollständiges Solution-Matching, Session-/MCP-Wiring und Gitea bleiben
Folgepakete.

Step 007 wurde durch den neuen Kritiker genehmigt (`7da30606`), ohne Findings
oder neue Tech-Debt-Einträge. Die Snapshot-Identitäts- und Registry-Grenze ist
damit abgeschlossen; EPIC-03 bleibt für Source-Matching und die spätere
Session-/MCP-Anbindung offen.

Step 008 ist als nächster kontextbegrenzter EPIC-03-Schnitt aktiviert
(`cf93b2fa`). Er umfasst ausschließlich die deterministische Zuordnung eines
expliziten Assembly-Alias zu `Project.AssemblyName` innerhalb eines geleasten
Source-Snapshots mit `matched`/`no-match`/`ambiguous`, Evidence und Confidence.
Session-/MCP-Wiring, Gitea und transitive Referenzen bleiben Folgepakete.

Step 008 wurde durch den neuen Kritiker genehmigt (`a2062fb7`). Der bestehende
Exact-DRY-Fund zur Drive-Path-Prüfung bleibt als `TD-001` im Tech-Debt-Index
offen, weil die gemeinsame Ablage zwei bereits abgeschlossene Vertragsgrenzen
berühren würde und aktuell kein sicherer Auto-Fix ist.

Step 009 ist als nächster kontextbegrenzter EPIC-03-Schnitt aktiviert
(`22490501`). Er verbindet ein bereits gematchtes, geleastes Source-Projekt
mit dem Assembly-Context und erhält bei `no-match`, `ambiguous`,
`unavailable` oder nicht nutzbarem Source-Projekt den bestehenden statischen
Decompilation-Fallback. Provider-Akquisition und MCP-Registrierung bleiben
Folgepakete.

Step 009 wurde durch den neuen Kritiker genehmigt (`aa900d52`). Die Source-
Fallback-Grenze ist damit abgeschlossen. `TD-002` (zentralisierte Origin-
Werte) und `TD-003` (Prüfung des internen Origin-Alias) sind als bewusst
architektonische Folgeprüfungen im Tech-Debt-Index dokumentiert.

Step 010 ist als nächster kontextbegrenzter EPIC-03-Schnitt aktiviert
(`cb21e221`). Er komponiert Loader, Provider, Snapshot-Registry, Match-/Source-
Selection und den direkten Assembly-Tool-Support inklusive Lease-Scope. MCP-
Registrierungen, Daemon-Wiring, Gitea und Netzwerk bleiben Folgepakete.

Der Kritiker fand in Step 010 zwei In-Scope-Abnahmelücken (`a92787eb`): die
fehlende statische Testzuordnung des Orchestrators und fehlende direkte Tests
der Support-/Lease-Grenze für Matched, NoMatch, Ambiguous und Fehlerpfade.
Step 011 bündelt diese Korrekturen als ein kontextbegrenztes Testpaket.

Step 011 wurde durch den neuen Kritiker genehmigt (`65f1c564`). Die direkte
Support-/Lease-Grenze ist mit den geforderten Zuständen abgedeckt und der
StaticTestSentinel ist über `@covers` erfüllt. `TD-004` dokumentiert den
verbleibenden gemeinsamen Snapshot-Testfixture-Builder als architektonische
Testinfrastruktur-Folgeprüfung.

Step 012 ist als nächster kontextbegrenzter EPIC-03-Schnitt aktiviert
(`352a8115`). Er verdrahtet eine hostlebenslange Composition für Loader,
Provider, Snapshot-Registry und Orchestrator an die beiden direkten Assembly-
MCP-Tools; `AnalysisToolCall`, weitere Hostpfade, Gitea, Netzwerk und
transitive Referenzen bleiben gemäß Plan außerhalb.

Der Kritiker fand in Step 012 zwei In-Scope-Wiring-Lücken (`16ebeda5`): der
registrierte source-backed Callback und die Mehrfach-Session-Lifetime der
Daemon-Composition waren nicht direkt nachgewiesen. Step 013 bündelt diese
Regressionen als kontextbegrenzte Korrekturrunde.

Step 013 wurde durch den neuen Kritiker genehmigt (`723d2a3b`). Der echte
registrierte Callback ist source-backed verifiziert, dieselbe Daemon-
Composition überlebt mehrere Sessions und wird erst am Hostende freigegeben;
`TD-004` ist durch die gemeinsame Testfabrik erledigt.

EPIC-03 wurde durch Steps 005 bis 013 vollständig abgeschlossen und in der
Roadmap markiert. Step 014 ist als erster kontextbegrenzter EPIC-04-Schnitt
aktiviert (`e40bef38`): typisierte Gitea-nahe Auth-/Transportfehler am
bestehenden Provider-Port und deterministische Test-Doubles; echte
Akquisition, Clone/Fetch/Refresh und Source-of-Truth bleiben Folgepakete.

Step 014 wurde durch den neuen Kritiker genehmigt (`0902a7b7`), ohne neue
Tech-Debt-Funde. Die typisierte Failure-Grenze ist abgeschlossen; EPIC-04
bleibt für die echte Repository-Akquisition und atomare Source-of-Truth-
Veröffentlichung offen.

Step 015 wurde als nächstes größeres, kontextbegrenztes EPIC-04-Paket geplant
(`1cdd0598`). Es umfasst den injizierbaren `IGiteaRepositoryTransport`, eine
besitzende und pfadgeschützte initiale Staging-/Clone-Fassade sowie
deterministische netzwerkfreie Tests. Produktiver Gitea-/Git-/HTTP-Transport,
Credential-Bindung, Fetch/Refresh, Cache und atomare Source-of-Truth-
Veröffentlichung bleiben Folgepakete. Der Coder muss die vertragliche Grenze
beibehalten und DRY-, MagicValues- und DeadCode-Tech-Debt nur passend und
architektonisch sinnvoll innerhalb dieses Pakets behandeln.

Der Kritiker hat Step 015 mit `b1dac89b` nicht genehmigt. Die Korrektur wird
als ein gebündelter Step 016 geplant: typed Ausnahmeabbildung mit sicherem
Cleanup, nachgelagerte Cancellation-Prüfung, Diagnose-Redaktion,
belastbarere Ownership-/Reparse-Sicherung und Zentralisierung des
Dateisystem-Exception-Helpers. Die fünf Findings bilden gemeinsam die
Sicherheits- und Besitzgrenze der Akquisitionsfassade; es wird kein
unabhängiger Mini-Sweep daraus. Die grünen Gesamt-Gates bleiben als
Ausgangsnachweis dokumentiert.

Step 016 ist als kontextbegrenzte Korrekturrunde aktiviert (`5aca2bd8`). Die
acht Abnahmekriterien bleiben innerhalb derselben Akquisitionsvertragsgrenze:
typisierte Ausnahmeabbildung, Cancellation, geheimnisfreie Diagnosen,
Ownership-/Reparse-/Cleanup-Sicherung, direkte Windows-Regressionen und
DRY-Zentralisierung. Ein unabhängiger Tech-Debt-Sweep sowie externe Adapter,
Cache, Refresh und Snapshot-Lifecycle bleiben ausgeschlossen.

Der neue Kritiker bestätigt Step 016 mit `3be96cf1` weiterhin als `blocked`:
Der echte Symlink-/Reparse-Test kann ohne `SeCreateSymbolicLinkPrivilege`
oder Developer Mode nicht ausgeführt werden, wodurch das vollständige
FastTests-Gate rot bleibt. Zusätzlich verwirft der Cancellation-Pfad noch
den Cleanup-Status. Der Blocker wird nicht durch Attributsimulation,
Privilegienänderung oder eine abgeschwächte Assertion umgangen; ein
Folgeplan muss die Cleanup-Beobachtbarkeit korrigieren und die echte
Reparse-Regression unter berechtigter Umgebung unverändert erhalten.

Step 017 ist als nächste kontextbegrenzte Folgekorrektur aktiviert
(`8bfb0974`). Es behandelt ausschließlich die Cleanup-Statusweitergabe im
Cancellation-Pfad und ein test-only Capability-Gate, das nur Win32 1314
überspringen darf. Der echte Symlink-/Reparse-Test bleibt unverändert und
muss unter berechtigter Umgebung ohne Skip bestehen; Privilegienänderungen,
Fake-Reparse-Assertions und weitere EPIC-04-Verträge bleiben ausgeschlossen.

Step 017 ist technisch umgesetzt (`5d48472c`, Doku `c7c21e84`), bleibt aber
bis zum privilegierten Lauf des echten Reparse-Tests `blocked`. Auf dem
aktuellen Host besteht der FastTests-Lauf mit 1.966 Tests und einem
expliziten Win32-1314-Skip; der Skip ist kein Sicherheitsnachweis. Die
Cancellation-Cleanup-Weitergabe ist korrigiert. Der nächste zulässige Schritt
ist ein neuer Kritikerlauf unter einer Umgebung mit Developer Mode oder
`SeCreateSymbolicLinkPrivilege`, ohne Code-Abschwächung.

Der Kritiker bestätigt Step 017 mit `d7757f8f` inhaltlich, aber weiterhin als
`blocked`: Cleanup-Logging, unveränderte Cancellation-/Token-Weitergabe,
geheimnisfreie Diagnosen und das ausschließlich auf Win32 1314 begrenzte
Capability-Gate sind korrekt. Der echte Symlink-/Reparse-Test wurde auf
diesem Host übersprungen; FastTests sind daher 1.966 bestanden plus 1 Skip,
Integration 360/360. Vor der Task-Fortsetzung ist ein privilegierter Lauf
ohne Skip unter Developer Mode oder `SeCreateSymbolicLinkPrivilege`
erforderlich.

Nutzerentscheidung zur Fortsetzung: Fehlende Symlink-Capability soll nicht
global alle externen Repositories sperren. `ERROR_PRIVILEGE_NOT_HELD (1314)`
oder ein tatsächlich erkannter Reparse-Checkout soll repository-spezifisch
als nicht verfügbare Source behandelt werden; der bestehende statische
Decompilation-Fallback soll greifen. Repositories ohne Reparse-Anforderung
sollen normal weiterlaufen. Diese Änderung wird in einem neuen vertikalen
Folge-Step geplant; der echte privilegierte Reparse-Test bleibt als separater
Sicherheitsnachweis erhalten.

Step 018 ist als nächster kontextbegrenzter EPIC-04-Schnitt aktiviert
(`374ae403`). Er projiziert `ERROR_PRIVILEGE_NOT_HELD (1314)` und tatsächlich
erkannte Reparse-Checkouts repository-spezifisch als `ProviderUnavailable`
mit geheimnisfreiem `RepositoryCapabilityUnavailable`-Diagnosecode in den
bestehenden Decompilation-Fallback. Eine globale Capability-Sperre sowie
Systemprivilegienänderungen bleiben ausgeschlossen; erfolgreiches
Acquirer-/Snapshot-Wiring folgt separat.

Step 018 wurde durch den neuen Kritiker mit `784167d8` genehmigt. Die
Nutzerentscheidung ist damit umgesetzt: `ERROR_PRIVILEGE_NOT_HELD (1314)`
und tatsächlich erkannte Reparse-Checkouts werden repository-spezifisch als
`ProviderUnavailable` mit `RepositoryCapabilityUnavailable` projiziert; der
statische Decompilation-Fallback bleibt aktiv. Normale Repositories werden
nicht global gesperrt. Der transparente lokale Symlink-Skip bleibt als nicht
ausgeführter Sicherheitsnachweis dokumentiert, ist aber kein Laufzeitblocker.

Step 019 ist als nächster kontextbegrenzter EPIC-04-Schnitt aktiviert
(`d8a2797c`). Er umfasst den produktiven, injizierbaren Git-over-HTTP-
Transport für einen initialen Default-Branch-Clone, sichere Laufzeit-
Credential-Auflösung, typisierte Prozess-/Auth-/Transportfehler,
Cancellation und deterministische netzwerkfreie Tests. Refresh, Fetch,
persistenten Cache, atomare Source-of-Truth-Veröffentlichung und Provider-/
Snapshot-Erfolg-Wiring bleiben Folgepakete.

Der Kritiker hat Step 019 mit `e5b3f7e3` nicht freigegeben. Die drei
zusammengehörigen Findings werden in Step 020 gebündelt: bounded und
ausnahmesicherer Git-Prozesslebenszyklus mit Prozessbaum-Abbruch, direkter
Real-Executor-Nachweis für `ProcessStartInfo`/Umgebungsisolation/Termination
sowie strukturierte, statusbewusste HTTP-/Git-Fehlerklassifikation. Der neue
DRY-Fund ist als `TD-005` dokumentiert; Refresh, Cache und Source-of-Truth
bleiben außerhalb dieser Korrektur.

Step 021 ist als kontextbegrenzte Korrekturrunde aktiviert (`626f0eb0`). Das
Paket schließt die Prozessbesitzgrenze gegen Parent-exit-/Grandchild-Pipe-
Races und stellt die Timeout-/Linked-CTS vor `Process.Start()` bereit. Direkte
lokale Child-/Grandchild-Regressionen sichern die Grenze; HTTP-/Git-
Klassifikation, Credentials, 1314-/Reparse-Fallback und alle späteren
Refresh-/Cache-/Snapshot-Verträge bleiben unverändert.

Der Kritiker hat Step 020 mit `cbb49754` nicht freigegeben. Die beiden
kritischen Befunde liegen an derselben Prozessbesitzgrenze und werden in
Step 021 gebündelt: ein Parent-exit-/Grandchild-Pipe-Race mit möglichem
offenem Prozessbaum sowie ein Timeout-CTS-Fenster nach `Process.Start()`,
das einen gestarteten Prozess zurücklassen könnte. `TD-005` wurde im Review
als erledigt bestätigt; die Fehlerklassifikation und der 1314-/Reparse-
Fallback bleiben unverändert.

Der Kritiker hat Step 021 mit `f80e5f45` nicht freigegeben. Die ursprünglichen
Races sind behoben, aber zwei Restbefunde werden in Step 022 gebündelt:
native Ergebnisse von `TerminateProcess`/`WaitForSingleObject` müssen
fail-closed geprüft und Cleanup-Fehler unter Erhalt der Primär-Exception
sichtbar gemacht werden; außerdem muss das Integration-Test-`finally` das
bounded Ende von Parent und Grandchild verifizieren. Keine neue fachliche
Grenze und kein separater Mini-Sweep.
Der Kritiker hat Step 022 mit `fc061950` nicht freigegeben. Step 023 bündelt
die drei Befunde an derselben Prozessbesitzgrenze: `TryManagedFallback` darf
einen beendeten Parent nicht ohne Grandchild-Nachweis als Erfolg akzeptieren,
`SafeHandle.ReleaseHandle` muss `CloseHandle`-Fehler sichtbar behandeln, und
die In-Scope-Duplikate `IsUsableHandle`/`CombineFailures` müssen zentralisiert
werden. Die bestehenden Tests, HTTP-/Git-Klassifikation und Reparse-
Fallbacksemantik bleiben erhalten.

Step 023 wurde durch den neuen Kritiker mit `30b13647` genehmigt. Die
Prozessbaum-/Job-Lifetime ist fail-closed bounded abgesichert, Parent-Exit wird
nicht ohne Grandchild-Nachweis als Erfolg akzeptiert, Handle-Cleanup bleibt
idempotent und sichtbar, und die In-Scope-DRY-Helfer sind zentralisiert. Es
wurden keine neuen Tech-Debt-Funde aufgenommen. Build, fokussierte Tests sowie
beide vollständigen Nicht-Stress-Gates sind grün; der echte Symlink-/Reparse-
Test bleibt auf diesem Host transparent wegen Win32 1314 übersprungen.

Der Kritiker hat Step 024 mit `3e726048` nicht freigegeben. `SourceSnapshot-
Registry.Dispose()` beendet die Snapshot-Entsorgung bei der ersten Exception,
obwohl das Dispose-Flag bereits gesetzt ist; weitere Snapshots mit Workspace-
und Checkout-Besitz könnten dadurch leaken und ein Retry ist verhindert. Das
wird als ein zusammenhängendes Ownership-Korrekturpaket mit aggregiertem
Cleanup-Fehlerpfad und deterministischer Regression behoben. Die übrigen
Provider-, Materializer-, Fallback-, Test- und Scope-Grenzen bleiben erhalten.

Step 025 wurde durch den neuen Kritiker mit `acdfe70e` genehmigt. Die Registry
entnimmt ihre Snapshots terminal unter Lock, entsorgt alle Besitzer außerhalb
des Locks trotz Einzel-Exceptions in deterministischer Reihenfolge und gibt
Einzel- bzw. Mehrfachfehler sichtbar weiter. Der Regressionstest bestätigt
zwei Snapshots, erfolgreichen Cleanup des zweiten Besitzers und einen
bounded/idempotenten Folge-Dispose. Es wurden keine neuen Tech-Debt-Funde
aufgenommen; Build und beide vollständigen Nicht-Stress-Gates sind grün, der
echte Symlink-/Reparse-Test bleibt transparent wegen Win32 1314 übersprungen.

Der Kritiker hat Step 026 mit `8a87f06a` nicht freigegeben. Drei zusammen-
gehörige Befunde bleiben innerhalb der Write-through-Publish-Grenze: Das
Same-Key-Lock schützt Rollback und Cleanup nicht vollständig gegen
konkurrierende Publishes; die Read-back-Validierung akzeptiert in einem
verkürzten Manifest-/Content-Fall unzulässige Daten und ist an einzelnen
TOCTOU-Größenprüfungen nicht ausreichend bounded; außerdem schreibt der
Default-Writer in Tests unter `AppContext.BaseDirectory` persistente
Generationen und verletzt dadurch Testisolation. Step 027 bündelt genau diese
Lock-/Cleanup-, unabhängige Manifestvalidierungs- und isolierte Writer-
Korrekturen; Provider-/Snapshot-/Refresh-/Transport-/Native-Grenzen bleiben
unverändert.

Der Kritiker hat Step 027 mit `732737dd` nicht freigegeben. Die Produktions-
korrekturen für Lock-Lifetime, generation-aware Rollback, unabhängige bounded
Read-back-Prüfung und Runtime-/Test-Writer-Isolation sind erfüllt. Es fehlen
jedoch ein deterministisch erzwungener A/B-Race-Test für die kritische
Interleaving-Reihenfolge sowie eine vollständige Testmatrix für Pointer-,
Manifest- und Inventargrenzen (Oversize, ungültiges UTF-8, Trunkierung,
Wachstum, unbekannte/doppelte Felder und Limits). Diese beiden eng gekoppelten
Testlücken werden als ein Step-028-Korrekturpaket geschlossen; Produktions-
und Scope-Grenzen bleiben unverändert.

## Laufender Nutzerhinweis zur Paketgröße und Task-Dauer (2026-08-29)

Der Nutzer hat wegen der inzwischen nahezu 30 Stunden laufenden Task-Dauer
klargestellt, dass die Folge-Steps größer geschnitten werden müssen. Künftige
Kritiker-Findings, Testlücken und passendes Tech-Debt werden deshalb in das
größtmögliche noch kontextstabile vertikale Paket gebündelt. Resultat- oder
Einzel-Assertion-Korrekturen erhalten keinen eigenen Mini-Step, wenn sie mit
dem betroffenen Produktionsvertrag und dessen Regressionen zusammen erledigt
werden können. Die Context-Grenzen bleiben ein Compact-Schutz, keine
Zielgröße. Frische Sub-Agenten und das Schließen erledigter Agenten bleiben
unverändert verpflichtend; der laufende Step-030-Coder wird nicht
unterbrochen.

Step 028 wurde durch den neuen Kritiker mit `d3d17fe1` genehmigt. Der
Race-Nachweis erzwingt die kritische A/B-Interleaving-Reihenfolge determinis-
tisch über TCS/Semaphore ohne Sleeps; die Read-back-Matrix prüft Pointer,
Manifest und Inventory gegen Oversize, ungültiges UTF-8, Trunkierung,
Wachstum/TOCTOU, unbekannte/doppelte Felder und alle relevanten Limits.
Gültige Current-Einträge bleiben in den Negativfällen unverändert. Es wurden
keine neuen Tech-Debt-Funde aufgenommen; Build und beide vollständigen
Nicht-Stress-Gates sind grün, der echte Symlink-/Reparse-Test bleibt
transparent wegen Win32 1314 übersprungen.

## Wiederaufnahme nach Step-028-Review (2026-08-29)

Step 028 ist genehmigt und bleibt der abgeschlossene technische Vorgänger.
Es gab keine Produktionsänderung nach seinem Abschluss, keinen neuen
Testlauf und keine Coder-/Kritikerarbeit in dieser Planer-Wiederaufnahme.
`step-029/step-plan.md` ist deshalb als `planned/in_progress` eingetragen;
der nächste sichere Übergabepunkt ist ein neuer Coder-Agent.

Step 029 bearbeitet als genau einen primären Vertrag die cache-backed Initial
Acquisition aus einer strikt validierten persistenten Generation. Die drei
gekoppelten Schichten sind Current-/Manifest-/Inventory-Read und Validierung,
frische request-owned Checkout-Lease mit Materialisierung sowie
Acquirer-Auswahl mit Clone-/Write-through-Fallback und Tests. Die bestehende
Generation bleibt cache-eigen; Snapshot-/Workspace-Ownership, Registry-
Cleanup und der Write-through-Publish-Vertrag aus Steps 024 bis 028 werden
nicht neu entworfen.

Der Kritiker hat Step 029 mit `c0abdcdf` nicht freigegeben. Die Reuse-Logik
selbst erfüllt die Ownership-, Validierungs- und Fallback-Grenzen; das
Step-Result enthält jedoch falsche Testzahlen und einen nicht zulässigen
solutionweiten Audit-Claim. Zusätzlich beweisen zwei Reuse-Tests weder den
konkreten Publish-Aufruf noch den unveränderten Current-Generation-Namen vor
und nach dem Reuse. Step 030 bündelt die Result-Korrektur und diese konkreten
Publish-/Current-Assertions als ein Nachweispaket.

### Wiederaufnahme nach Step-029-Review (2026-08-29)

Step 030 ist als neuer, flacher Korrektur-Step mit `corrects: step-029`
geplant und auf `planned/in_progress` gesetzt. Er bleibt auf einen primären
Cache-Reuse-/Ownership-Nachweis mit drei gekoppelten Schichten begrenzt:
Result-/Audit-Korrektur, Recording-Reader-/Writer-/Current-Snapshot-
Assertions sowie lokale Reuse-/Fallback-Regressionen. Die vorhandenen
Acquirer-Seams für getrennten `cacheWriter` und `cacheReader` reichen aus;
eine Produktionsänderung ist nicht geplant.

Der Coder muss den initialen Publish-Erfolg explizit prüfen, anschließend
einen separaten Reader und in den Acquirer-Hit-Tests den vorhandenen
`RecordingCacheWriter` verwenden. `Request` muss leer und der Transport-
CallCount null bleiben. Der konkrete Current-Generation-Name wird vor dem
Reuse sowie nach Single-Hit, Handle-Dispose und parallelen Hits identisch
assertiert; der request-owned Checkout bleibt vom persistenten
`published.GenerationPath` getrennt. `step-029/step-result.md` wird auf den
geprüften Commit `82692da054136dd39f6a37d110926bb95b5d796c`, die realen
34/1/35-, 2060/2/2062- und 370/0/370-Stände, die beiden konkreten
Win32-1314-Skips und ausschließlich tatsächlich ausgeführte scoped Audits
korrigiert. `roadmap.md` bleibt im Fix-Modus unverändert.

Refresh/Fetch und Refresh-Policy, Cache-Konfiguration, Retention/GC/
Invalidierung, dirty/unbuilt/Health/degraded, Host-/MCP-Wiring,
Provider-/Snapshot-/Registry-Neudesign und EPIC-05 sind als Folgepakete
herausgelöst. TD-001 bis TD-003 bleiben unverändert. Der Plan enthält keine
Produktionsänderung und keine vorweggenommene Coder-/Kritikerarbeit.

Der Kritiker hat Step 030 mit `2510db5e` nicht freigegeben. Die Reuse-
Assertions sind fachlich erfüllt, aber die Abschlussverifikation ist rot:
`ExternalSourceRepositoryCacheAcquirerTests.cs` überschreitet mit 501 Zeilen
die Regelgrenze von 500, und der tatsächliche Integration-Lauf meldete 368
Bestanden, 2 Fehler, 370 gesamt. Step 029 und Step 030 enthalten zusätzlich
falsche Testzahlen sowie unzutreffende Violations-/Safeguard-Werte. Step 031
bündelt Testdatei-Regel, Integrationsfehler und Result-/Audit-Korrektur in
einem Qualitätspaket; es wird nicht in einzelne Mini-Steps zerlegt.

### Wiederaufnahme nach Step-030-Review (2026-08-29)

Auf Nutzeranweisung wurde Step 031 als neuer, größerer Korrektur-Step mit
`corrects: step-030` geplant und auf `planned/in_progress` gesetzt. Der
primäre Vertrag ist der reproduzierbare grüne Quality-Gate-Nachweis für den
genehmigten Cache-Reuse-Vertrag. Die drei gekoppelten Schichten sind die
regelkonforme Teststruktur, die konkrete Ursache der zwei roten
Integrationstests und die wahrheitsgemäße Result-/Audit-Evidenz.

Der vollständige Nicht-Stress-Integration-Lauf aus dem Step-030-Review
steht bei 368 bestanden, 0 Skips, 2 Fehlern und 370 gesamt. Ein fokussierter
TRX-Lauf reproduzierte genau diese beiden Fehler:

- `CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
  scheitert am Exit-Code 1, weil die geänderte
  `ExternalSourceRepositoryCacheAcquirerTests.cs` 501 statt höchstens 500
  vom Linter gezählte Zeilen hat.
- `McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults` scheitert
  am ausgegebenen Score `2,652253349573691` unter dem unveränderten Korridor
  `>= 5,0`; der aktuelle Violationszustand enthält denselben neuen
  `MaxLineCount`-Befund neben drei bestehenden Struktur-/Footprint-Befunden.

Die minimale Korrektur ist eine thematische Entzerrung: Die drei
Cache-Hit-/Reuse-Tests gehen in eine eigene nicht-partielle Testklasse,
bereits vorhandene Fixture-, Reader-/Writer-Double- und Assertion-Logik
wird einmalig in cache-spezifischem Test-Support geteilt. Die bestehende
Partialklasse erhält keine weitere Datei; Produktionscode, Regeln,
Assertions und Filtersemantik bleiben unangetastet. Eine externe Blockade
ist derzeit nicht nachgewiesen.

Nach der neuen Verifikation müssen `step-029/step-result.md` und
`step-030/step-result.md` ausschließlich die tatsächlich ausgeführten
Zahlen, Fehler, Skips und scoped MCP-Audits ausweisen. `roadmap.md` und
`tech-debt.md` bleiben im Fix-Modus unverändert. Der nächste sichere
Übergabepunkt ist ein neuer Coder-Agent mit
`tasks/decompiled-assembly-analysis/step-031/step-plan.md`.

Step 031 wurde durch den neuen Kritiker mit `d8cff007` genehmigt. Die
Cache-Reuse-Teststruktur liegt vollständig unter der MaxLineCount-500-Grenze
(Maximum 487), die beiden zuvor roten Integration-Gates und der vollständige
Integration-Lauf sind grün (370/370), und Step-029/030/031 dokumentieren nur
reale Test-, Skip- und scoped Auditwerte. Der einmalige unabhängige
ExternalSourceGitProcessExecutor-Timeout ist transparent als solcher
dokumentiert; es wurden keine neuen Tech-Debt-Funde aufgenommen.

### Wiederaufnahme nach Step-031-Review (2026-08-29)

Step 031 ist mit `d8cff007` genehmigt. Der validierte Current-Reuse-Vertrag,
seine request-eigene Checkout-Lifetime und die zugehörigen Build-, FastTest-
und Integration-Gates werden nicht erneut geöffnet. In dieser Planer-
Wiederaufnahme gab es keine Produktionsänderung, keinen Testlauf und keine
Coder-/Kritikerarbeit.

Step 032 ist als nächster neuer EPIC-04-Step auf `planned/in_progress`
gesetzt. Sein einziger primärer Vertrag ist die Staleness-Entscheidung plus
der sichere Fetch in einen neuen request-eigenen Checkout und die atomare
Veröffentlichung einer neuen Cache-Generation. Die Refresh-/Policy-Logik,
der injizierte Fetch-/Transport-Port und die Generation-/Rollback-Integration
bleiben bewusst ein gemeinsames größeres Paket.

Der nächste sichere Übergabepunkt ist ein neuer Coder-Agent mit
`tasks/decompiled-assembly-analysis/step-032/step-plan.md`. Cache-
Konfiguration, Retention/GC/Invalidierung, Dirty-/Unbuilt- und
Health-/degraded-Policy sowie Host-/MCP-Wiring sind ausdrücklich spätere,
eigenständige Verträge.

Der Kritiker hat Step 032 mit `a16a421c` nicht freigegeben. Die
Refresh-/Fetch-, Generation-, Pointer-, Ownership- und Cleanup-Logik ist
fachlich erfüllt; die im Step-Result dokumentierten MCP-/Safeguard-/DRY- und
Magic-Values-Nachweise sind jedoch am geprüften Commit nicht vollständig
reproduzierbar (u. a. Safeguard 5,83/10 bei Threshold 8,00, 369 Produktions-
und 140 Testmethoden sowie ein bestehender breiter Assemblies-Befund). Die
Evidenzkorrektur wird mit dem nächsten größeren Cache-/Refresh-Folgepaket
gebündelt, nicht als isolierter Mini-Step. Kein neuer Tech-Debt-Fund wurde
aufgenommen.

### Wiederaufnahme nach Step-032-Review (2026-08-30)

Auf Nutzeranweisung wurde ein neuer Planer-Agent gestartet; kein bestehender
Agent wurde wiederverwendet. Step 033 ist als größerer EPIC-04-Folge-Step auf
`planned/in_progress` gesetzt und trägt `corrects: step-032`. Der primäre
funktionale Vertrag ist die strikt validierte
`ExternalSources:CacheRoot`-/`RefreshIntervalMinutes`-Konfiguration bis zur
bestehenden Cache-Writer-/Refresh-Policy-Konstruktion und der deterministischen
Fresh/Stale-Entscheidung. Defaults, Source-of-Truth, Generation-, Pointer-,
Ownership-, HTTP-/Git-/Credentials-, Process-/Native- und statische
Decompilation-Invarianten bleiben erhalten.

`corrects: step-032` bezieht sich ausschließlich auf den MAJOR-Befund aus
Review `a16a421c`: Die Step-032-MCP-/Safeguard-/DRY-/Magic-Values-Evidenz muss
für den geprüften Commit reproduzierbar berichtigt werden. Refresh-/Fetch-,
Generation-, Pointer- und Cleanup-Logik werden nicht erneut geöffnet. Die
Evidenzkorrektur wird als verpflichtender Abschlussnachweis in das größere
Cache-/Refresh-Paket integriert, damit kein Audit-only-Mini-Step entsteht.
Der Kontext bleibt durch einen Primärvertrag, drei gekoppelte Schichten,
höchstens acht Abnahmekriterien, höchstens zehn `read_first`-Dateien und
`max_initial_files: 12` begrenzt.

Im Planer-Schritt wurden keine Produktionsänderungen vorgenommen und keine
Tests, Coder- oder Kritikerarbeit ausgeführt. Der nächste sichere
Übergabepunkt ist ein neuer Coder-Agent mit
`tasks/decompiled-assembly-analysis/step-033/step-plan.md`. Host-/MCP-Wiring,
Health-/degraded-/Dirty-/Unbuilt-Policy sowie Retention/GC/Invalidierung
bleiben eigenständige Folgepakete; Roadmap- und Statuspflege nach dem
Coder-Ergebnis verbleiben beim Orchestrator.

### Step-034-Planung (2026-08-30)

Ein neuer Planer-Agent wurde gestartet und nach Abschluss geschlossen;
kein bestehender Agent wurde wiederverwendet. Step 034 ist mit Plan
`6ebbc7c1` auf `planned/in_progress` gesetzt. Das größere vertikale Paket
verbindet die strikte rohe CacheRoot-/Optionsvalidierung mit der terminalen,
fail-closed Weitergabe eines Config-Failures bis zum Assembly-Tool sowie
adversarialen lokalen Pfad- und End-to-End-Regressionen. Die gültige
Refresh-/Factory-/Policy-Verdrahtung und die gewöhnlichen statischen
Decompilation-Fallbacks bleiben erhalten; Host-/MCP-Wiring, Health/Degraded,
Dirty/Unbuilt und Retention/GC bleiben außerhalb. Der nächste sichere
Übergabepunkt ist ein neuer Coder-Agent mit
`tasks/decompiled-assembly-analysis/step-034/step-plan.md`.

### Wiederaufnahme nach Step-033-Review (2026-08-30)

Der neue Kritiker hat Step 033 mit `d57f5aab` nicht freigegeben. Zwei
zusammengehörige MAJOR-Befunde bleiben offen: Die rohe `CacheRoot`-
Validierung akzeptiert weiterhin URI-/Credential-artige und reservierte
Segmentformen, obwohl die Dokumentation deren Ablehnung behauptet. Außerdem
kann ein fehlgeschlagener Config-Load über den bestehenden
`AssemblyAnalysisToolSupport`-/Orchestrator-Pfad noch als erfolgreicher
Decompilation-Fallback enden; der bestehende Test schreibt dieses Verhalten
fälschlich fest.

Step 034 muss deshalb als größeres gemeinsames Korrekturpaket geplant werden:
strikte rohe CacheRoot-/Optionsvalidierung, fail-closed Weitergabe bis zum
Tool-Ergebnis und lokale adversariale sowie End-to-End-Regressionstests. Die
erfüllte RefreshInterval-/Factory-/Policy-Verdrahtung, die korrigierte
Step-032-Evidenz und die bestehenden Green-Gates bleiben erhalten. Kein
Audit-only- oder Einzel-Assertion-Step wird daraus abgeleitet; der
Tech-Debt-Grundsatz für Dry/MagicValues/DeadCode bleibt innerhalb dieses
Pakets aktiv. Review-Gates: Build grün, Fast Nicht-Stress 2091 bestanden plus
2 bekannte Reparse-Skips, Integration Nicht-Stress 370/370, Stress nicht
ausgeführt; aktueller breiter Safeguard 5,80/10 bei Threshold 8,00 bleibt
ehrlich als FAIL dokumentiert.

### Wiederaufnahme nach Step-034-Review (2026-08-30)

Der frische Kritiker hat Step 034 mit `ff5fb2e5` nicht freigegeben. Der
verbleibende MAJOR-Befund liegt an derselben terminalen Config-Failure-
Grenze: Bei `Failure([])` wird `ConfigurationFailure` zu `NoMatch` und kann
erneut den erfolgreichen statischen Decompilation-Fallback erreichen. Zwei
gekoppelte MINOR-Nachweise sind ebenfalls offen: die URI-/UNC-Testmatrix ist
nicht vollständig/exakt genug und das Toolresultat behauptet die
`IsError=false`-Semantik nicht explizit genug.

Diese Befunde werden auf Nutzeranweisung als ein größeres Korrekturpaket
gebündelt, nicht als Assertion- oder Audit-only-Mini-Step. Der nächste Step
verstärkt den terminalen Statusmarker unabhängig von der Diagnosenanzahl,
schließt die adversariale URI-/UNC-Matrix und verankert die exakte
strukturierte Resultat-Policy. Die bereits grüne Implementierung, die
gewöhnlichen statischen Fallbacks, die bekannten Win32-1314-Skips und der
ehrliche Safeguard-FAIL bleiben als Regressionen erhalten. Der Kritiker
meldete Build grün, Fast Nicht-Stress 2123 bestanden plus 2 Skips,
Integration Nicht-Stress 370/370, Stress nicht ausgeführt und keine Leaks;
`tech-debt.md` blieb unverändert.

### Step-035-Planung (2026-08-30)

Ein neuer Planer-Agent wurde gestartet und nach Abschluss geschlossen; kein
bestehender Agent wurde wiederverwendet. Step 035 ist als größeres
Korrekturpaket mit `corrects: step-034` auf `planned/in_progress` gesetzt.
Der eine Primärvertrag ist die diagnoseunabhängige Terminalität eines
expliziten Config-Failures bis zum Assembly-Tool; die direkt gekoppelten
Schichten sind der immutable Selection-Statusmarker, die bestehende
Recoverable-/`IsError=false`-Resultatgrenze sowie die adversariale URI-/UNC-
und End-to-End-Testmatrix. `Failure([])` darf nicht als `NoMatch` in die
statische Decompilation fallen; `Success(ExternalSourceConfiguration.Empty)`
und die positiven NoMatch-, Ambiguous-, ProviderUnavailable- und Capability-
Fallbacks bleiben erhalten.

Der Plan begrenzt den initialen Kontext auf zehn `read_first`-Dateien und
zwölf Dateien einschließlich `read_on_demand`. Es gibt keine Roadmap-Änderung,
keine Step-034-Evidenz-Neubewertung außer direkten Resultatassertions, keine
globale `McpToolResults`-Änderung, keinen Stress-Test und keine Erweiterung
von Host-/MCP-Wiring, Health/Degraded, Dirty/Unbuilt, Retention/GC, Refresh/
Fetch, Reparse oder EPIC-05. Der nächste sichere Übergabepunkt ist ein neuer
Coder-Agent mit `tasks/decompiled-assembly-analysis/step-035/step-plan.md`;
nach dessen Abschluss wird er geschlossen und ein frischer Kritiker gestartet.

### Step-035-Abschluss (2026-08-30)

Step 035 wurde durch den frischen Coder in `5c830e44` implementiert und mit
`8182b992` dokumentiert. Der frische Kritiker hat ihn mit `c4ee413c`
genehmigt; alle beteiligten Sub-Agenten wurden danach geschlossen. Der
diagnoseunabhängige terminale `ConfigurationFailure` ist bis zum
Assembly-Tool abgesichert, die positiven statischen Fallbacks bleiben
erhalten, und die URI-/UNC-/Device-/Reserved-Matrix sowie die strukturierte
`IsError=false`-Resultatpolicy sind regressionsgeprüft.

Die Abschlussverifikation ist grün: Build ohne Warnungen/Fehler, FastTests
2.158 bestanden plus 2 bekannte Win32-1314-Reparse-Skips, IntegrationTests
370 bestanden, Stress nicht ausgeführt und keine Test-/Temp-Leaks. Der
Safeguard wurde sowohl mit Threshold 5 als PASS `5,66/10` als auch mit dem
unveränderten Threshold 8 als FAIL `5,66/10` dokumentiert; die bestehende
Baseline-Schuld wurde nicht schöngeschrieben. Scoped Tech-Debt erzeugte
keine neuen direkt zu behebenden Findings.

### Step-036-Planung (2026-08-30)

Ein neuer Planer-Agent wurde gestartet und nach Abschluss geschlossen; kein
bestehender Agent wurde wiederverwendet. Step 036 ist als neuer größerer
EPIC-04-Source-Policy-Step auf `planned/in_progress` gesetzt. Sein
Primärvertrag verbindet die Clean-/Dirty-/Unverified-Grenze des bereits
besitzgeschützten Gitea-Staging-Checkouts mit einer transparenten
`Verified`-/`Degraded`-/`Unavailable`-Refresh- und Provider-Semantik.

Der Step prüft keinen lokalen Checkout als konkurrierende Source of Truth und
führt keinen heuristischen Unbuilt-/Binary-Fingerprint-Modus ein. Ein
fehlgeschlagener stale Refresh darf den validierten alten `current`-Stand nur
als Last-good-Nachweis unter `Degraded` führen; er darf keinen stale Snapshot
als aktuell registrieren. Der statische Decompilation-Fallback bleibt aktiv,
`ConfigurationFailure` bleibt terminal und die bestehende Cache-/Pointer-/
Ownership-/Cleanup-/Cancellation-/1314-/Reparse-Semantik bleibt erhalten.

Host-/MCP-Health-Wiring, Retention/GC/Invalidierung, Telemetrie, transitive
Referenzen und EPIC-05 sind ausdrücklich außerhalb. Der initiale Kontext ist
auf zehn `read_first`-Dateien und zwölf Dateien einschließlich
`read_on_demand` begrenzt. Der scoped Assemblies-Audit ergab keine
Duplikat-Cluster und keinen hochkonfidenten Dead Code; die 109 breiten
Magic-Value-Treffer werden nicht global bearbeitet. `TD-001` bis `TD-003`
bleiben unverändert.

Im Planer-Schritt wurden keine Produktionsänderungen vorgenommen und keine
Tests, Coder- oder Kritikerarbeit ausgeführt. Der nächste sichere
Übergabepunkt ist ein frischer Coder-Agent mit
`tasks/decompiled-assembly-analysis/step-036/step-plan.md`; nach seinem
Abschluss wird er geschlossen und ein neuer, separater Kritiker gestartet.

### Wiederaufnahme nach Step-036-Review (2026-08-30)

Der frische Kritiker hat Step 036 mit `c7efaae4` nicht freigegeben. Zwei
zusammengehörige MAJOR-Befunde verletzen die neue Checkout-Trust-Grenze:
Ignorierte lokale Dateien passieren das Clean-Gate und können in Cache oder
Snapshot gelangen; außerdem besteht zwischen Git-Verifikation und
Materialisierung ein ungeschütztes TOCTOU-Fenster. Als direkt gekoppelter
MINOR-Befund wird `Dirty` im Acquirer zu `Unverified` abgeschwächt, wodurch
die typisierte Trust-Semantik an einer Ownership-Grenze verloren geht.

Diese Befunde werden als ein größeres Step-037-Korrekturpaket gebündelt,
nicht als Status- oder Audit-only-Mini-Step. Das Paket muss Ignore-/Status-
Semantik, atomare Verifikation bis Materialisierung und die eindeutige
Dirty-Klassifikation gemeinsam korrigieren; Last-good/Degraded,
CurrentChanged, Cleanup, positive Fallbacks und die vorhandenen 1314-Skips
bleiben regressionsgeschützt. Der Kritiker bestätigte Build sowie Fast
2.165 bestanden plus 2 bekannte Skips und Integration 370/370, Stress nicht
ausgeführt, ohne Leaks.

### Step-037-Planung und Aktivierung (2026-08-30)

Der neue Step-037-Plan wurde als einzelnes Korrekturpaket mit einem
Primärvertrag und drei gekoppelten Schichten aktiviert. Er begrenzt den
Coder-Kontext auf maximal zwölf initiale Dateien, zehn `read_first`-Dateien
und acht Abnahmekriterien. Der Plan umfasst Git-Ignore-/Status-Semantik,
die Trust-Bindung bis Cache- und Workspace-Materialisierung sowie die
typisierte Propagation durch Acquirer, Provider und Selection. Er fordert
deterministische lokale Regressionen für Mutation/TOCTOU, Cleanup,
Cancellation, Last-good/Degraded, CurrentChanged und positive Fallbacks.

Der Planer hat keine Produktionsänderungen, Tests oder Coder-/Kritikerarbeit
ausgeführt. Da es sich um eine Fix-Mode-Korrektur handelt, bleibt
`roadmap.md` unverändert. Der sichere nächste Übergabepunkt ist ein frischer
Coder auf `main`, der ausschließlich
`tasks/decompiled-assembly-analysis/step-037/step-plan.md` als Startvertrag
übernimmt, danach geschlossen und durch einen neuen Kritiker ersetzt wird.

### Wiederaufnahme nach Step-037-Review (2026-08-30)

Der frische Kritiker hat Step 037 mit `078c3e15` nicht freigegeben. Zwei
MAJOR-Befunde betreffen denselben sicherheitskritischen Vertrag: Der
Statusparser akzeptiert leere Records, wodurch ein nicht sauber bewerteter
Checkout als clean durchgehen kann; außerdem bindet der TOCTOU-Schutz den
Inhalt nicht exklusiv bis Copy/Open/Publish. Ein direkt gekoppelter MINOR-
Befund ist die Test-Transport-Fassade, die fehlende Attestations automatisch
per `ForTesting` ergänzt und damit den Produktionsvertrag im Test verdecken
kann.

Diese Befunde werden als ein größeres Step-038-Korrekturpaket gebündelt,
nicht als Audit-only- oder Assertion-Mini-Step. Der nächste Step muss den
Statusparser fail-closed machen, die Attestation bis zur tatsächlichen
Materialisierung/Publizierung in einer belastbaren Ownership-/Lock-Grenze
halten und die Test-Fassade so ändern, dass sie keine fehlende
Produktionsattestation ergänzt. Positive Clean-/Verified-, Last-good/
Degraded-, CurrentChanged-, Cleanup-/Cancellation- und Fallback-Verträge
bleiben erhalten. Der Kritiker bestätigte Build, Fast 2.174 bestanden plus
2 bekannte 1314-Skips, Integration 370/370, Stress nicht ausgeführt,
keine Leaks sowie keine neuen Tech-Debt-Einträge.
