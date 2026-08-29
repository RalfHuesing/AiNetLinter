---
status: executing
task: decompiled-assembly-analysis
started_at: 2026-08-28T11:06:28+02:00
last_updated: 2026-08-29T17:25:29+02:00
rules_dir: .agents/rules
total_steps: 27
current_step: step-027
---

# Task State: decompiled-assembly-analysis

## Übersicht

- **Task-Status:** `executing`
- **Steps gesamt:** 27 (regulär + Korrekturen)
- **Aktueller Schritt:** `step-027` (planned/in_progress; Korrektur von
  `step-026` für Lock-/Rollback-Lifetime, fail-closed Read-back und
  Testisolation)
- **Roadmap:** siehe `roadmap.md`
- **Tech-Debt:** siehe `tech-debt.md`
- **Gestartet:** 2026-08-28T11:06:28+02:00
- **Zuletzt aktualisiert:** 2026-08-29T17:25:29+02:00
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
| step-027 | EPIC-04 | in_progress | Fail-closeden Generation-Publish und Testisolation korrigieren | step-026 | - | planned → coder → critic | - |

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

Step 027 steht damit auf `planned/in_progress`; der nächste sichere
Übergabepunkt ist ein neuer Coder-Agent. Es gibt keine Produktionsänderung
aus diesem Planer-Schritt, keinen Roadmap-Bedarf und keinen Push.

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
