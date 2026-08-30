---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 038
corrects: step-037
title: "Checkout-Trust-Attestation bis Materialisierung und Publish unveränderlich und lock-gebunden absichern"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T07:00:17+02:00
related_to:
  - ../step-037/step-plan.md
  - ../step-037/step-result.md
  - ../step-037/step-review.md
---

# Step 038: Checkout-Trust-Attestation bis Materialisierung und Publish unveränderlich und lock-gebunden absichern

## Ziel und Primärvertrag

Step 038 ist ein einzelnes, großes Korrekturpaket für die von Step 037
offengebliebene Checkout-Trust-/Ownership-/Materialisierungsgrenze. Die drei
Befunde werden bewusst gemeinsam bearbeitet: Der Statusparser erzeugt die
Eingangsattestation, die Attestation muss denselben vertrauenswürdigen Inhalt
bis zur Materialisierung binden, und der Test-Transport muss diesen
Produktionsvertrag unverfälscht sichtbar machen.

Der Primärvertrag lautet:

> Eine Git-Trust-Attestation ist nur gültig, wenn die relevante
> Statusauswertung strukturell vollständig und erwartbar ist, die Attestation
> unveränderlich und an Ownership sowie eine exklusive Materialisierungs-/Lock-
> Lebensdauer gebunden bleibt, und erst nach tatsächlich sicher kopierter,
> geöffneter oder publizierter Quelle freigegeben wird. Fehlende oder
> unbewertbare Evidenz bleibt fail-closed typisiert.

Konkret gilt für den Statusparser: Nur eine semantisch leere, framing-freie
Auswertung ist Clean. Eine alleinige Leerzeile, führende oder innere leere
Records, unvollständige Git-Records und unerwartete Statusformen sind
`Unverified`; ein gültiger abschließender Zeilentrenner nach mindestens einem
vollständigen Record ist lediglich Framing. `Dirty` bleibt für tatsächlich
geänderte, ignorierte oder sonst nicht erlaubte Dateien erhalten, während
`Unverified` für fehlende, malformed oder nicht bewertbare Evidenz reserviert
bleibt. Der alleinige, exakt erlaubte Ownership-Marker bleibt der positive
Sonderfall.

Die Lösung muss die Attestation nicht nur am Anfang und Ende erneut prüfen.
Sie muss Ownership, Revision, Attestation und einen exklusiven Lock/Lease bis
über die tatsächlich relevanten Operationen tragen: Cache-Copy, Manifest- und
Inventory-Erzeugung, Readback, Pointer-Publish sowie Workspace-Open und die
Übergabe des Snapshot-/Checkout-Lease. Ein erneuter Check ist zusätzliche
Evidenz, ersetzt aber keine Bindung über das TOCTOU-Fenster. Rollback,
Cancellation und Cleanup geben den Lock deterministisch frei.

Die Korrektur ist ein vertikales Paket und kein Parser-only-, Assertion-only-
oder Audit-only-Step. Eine Aufteilung würde genau die Vertragslücke zwischen
Statusbewertung, Besitzbindung und Materialisierung erneut ermöglichen.

## Aktueller Stand und Grenzen

Die Planbasis sind die Step-037-Codercommits `093f9d7a` und `04e37bea`.
Die bestehende Review-/Orchestrator-Historie bleibt nachvollziehbar:
`c7efaae4` ist der vorausgehende Issues-Review, `078c3e15` der aktuelle
Step-037-Kritikerstatus und `aaf16bfb` der Orchestrator-Statuscommit.

Die folgenden Grenzen wurden per projektgebundenem `ainetlinter`-MCP sowie
physischer Dateiprüfung verifiziert:

| Bereich | Aktuelle Grenze | Kritischer Einstieg |
| --- | --- | --- |
| Status | `ExternalSourceRepositoryCheckoutStatus.cs`, physisch 136 Zeilen; Typ `:10-136` | `AssessStatus :74-101`; direktes Status-Testziel `GiteaGitRepositoryCheckoutStatusTests.cs`, 116 Zeilen |
| Attestation | `ExternalSourceCheckoutAttestation.cs`, physisch 264 Zeilen; Typ `:27-253`, Exception `:255-264` | `VerifyCheckoutAsync :44-63`, `FromTransport :95-128`, `ForTesting :145-164`; vorhandene Attestation-Tests 162 Zeilen |
| Checkout-Besitz | `ExternalSourceRepositoryAcquisitionModels.cs`, physisch 232 Zeilen | `ExternalSourceCheckoutHandle :63-123`, Ownership-/Cleanup-Token `:19-61` |
| Policy/Propagation | `ExternalSourceRepositorySourcePolicy.cs`, physisch 239 Zeilen; Typ `:18-239` | `FailureAfterCleanup :129-161`; MCP meldet dort keine statisch zugewiesenen Tests |
| Acquirer | `ExternalSourceRepositoryAcquirer.cs`, physisch 494 Zeilen; Typ `:13-494` | `CompleteTransportResultAsync :141-195`, `PublishCacheAndCreateResultAsync :197-249`, `ValidateCheckout :387-438`; Footprint 2289/2500 |
| Cache-Publish | `ExternalSourceRepositoryCacheWriter.cs`, physisch 485 Zeilen; Typ `:18-476` | `PublishGeneration :145-202`, Attestation-Validierung `:423-435`, Test-Seams `:478-485` |
| Cache-Copy | `ExternalSourceRepositoryCacheStorage.cs`, physisch 497 Zeilen | `CopySource :15-43`, `CopyFile :157-200`, Source-Validierung `:365-386`; nur drei physische Zeilen Headroom |
| Refresh | `ExternalSourceRepositoryCacheRefresh.cs`, physisch 414 Zeilen; Typ `:32-414` | `PrepareRefreshCheckoutAsync :175-234`, Publish-/Last-good-Pfade `:236-321` |
| Workspace | `ExternalSourceSnapshotMaterializer.cs`, physisch 113 Zeilen; Typ `:14-101`, Exception `:103-113` | `MaterializeAsync` ab `:16`; Open vor dem abschließenden Check |
| Provider/Selection | `GiteaExternalSourceProvider.cs` physisch 211; `AssemblySourceSelectionOrchestrator.cs` physisch 184 | Provider-Typ `:11-211`; Orchestrator `:23-111`, Scope `:113-184` |
| Test-Fassade | `ExternalSourceRepositoryAcquirerTestTransport.cs`, physisch 91 Zeilen; Typ `:13-91` | `AttachTestAttestation :78-90` ergänzt derzeit fehlende Attestations automatisch |

Nahe der Zeilengrenze liegende Produktionsdateien (`CacheStorage` und
`Acquirer`) werden nicht weiter mit allgemeiner Hilfslogik aufgefüllt. Falls
ein neuer Lease-/Lock-Typ erforderlich ist, soll er in einer fokussierten,
internen Datei wie `ExternalSourceCheckoutMaterializationLease.cs` liegen.
Es gibt keinen globalen McpToolResults- oder Reparse-Umbau.

## Kontextbudget

`max_initial_files: 12`

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/codemap.md`
2. `tasks/decompiled-assembly-analysis/step-037/step-result.md`
3. `tasks/decompiled-assembly-analysis/step-037/step-review.md`
4. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs`
5. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutAttestation.cs`
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
10. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs`

### `read_on_demand` (innerhalb des Gesamtbudgets)

1. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`
2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs`

`ExternalSourceRepositoryCacheRefresh.cs`,
`ExternalSourceRepositoryCacheMaterializer.cs`,
`GiteaExternalSourceProvider.cs`, `AssemblySourceSelectionOrchestrator.cs`
und die gezielten Testdateien werden danach ausschließlich MCP- und
Call-Chain-geführt geöffnet, sobald der Coder die konkrete Bindungsstelle
erreicht. Sie gehören zum Paket, werden aber nicht in die ersten zwölf Dateien
gedrängt.

## MCP-Symbolgrenzen und Arbeitsreihenfolge

Vor jeder semantischen C#-Entscheidung verwendet der Coder im Projektroot
`C:/Daten/Entwicklung/Ralf/AiNetLinter` zuerst `get_feature_context` und
`get_symbol_body`; anschließend sind für die tatsächlich berührte Kette
`find_references`, `get_impact`, `dependency_graph` und `get_test_context`
gezielt einzusetzen. Die relevanten Symbolgrenzen sind:

- `AiNetLinter.Mcp.Assemblies.ExternalSourceRepositoryCheckoutStatus`
  einschließlich `AssessStatus`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceCheckoutAttestation` und
  `AiNetLinter.Mcp.Assemblies.ExternalSourceCheckoutVerification`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceCheckoutHandle`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceRepositorySourcePolicy`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceRepositoryAcquirer`.
- `AiNetLinter.Mcp.Assemblies.LocalExternalSourceRepositoryCacheWriter`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceRepositoryCacheRefresh`.
- `AiNetLinter.Mcp.Assemblies.ExternalSourceSnapshotMaterializer`.
- `AiNetLinter.Mcp.Assemblies.GiteaExternalSourceProvider`.
- `AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator`.
- `AiNetLinter.FastTests.Mcp.Assemblies.ExternalSourceRecordingTransport`.

Nach Änderungen sind nur die berührten Assemblies-/Trust-Slices auf
Violations, Duplikate, Magic Values, Dead Code und `safeguard` zu prüfen.
Kein globaler Audit und kein Tech-Debt-Sweep gehört in diesen Step.

## Vertikales Korrekturpaket

### 1. Statusgrammar und unveränderliche Attestation

Betroffen sind `ExternalSourceRepositoryCheckoutStatus.cs:37-101`, die
Status-/HEAD-Verwendung im Transport und
`ExternalSourceCheckoutAttestation.cs:27-164`.

- `AssessStatus` erhält eine explizite Record-Grammatik ohne
  `RemoveEmptyEntries`-Semantik. Leere Records werden als unerwartet erkannt;
  nur erlaubtes finales Framing nach vollständigen Records wird akzeptiert.
- `?? .ainetlinter-owner` bleibt exakt als Ownership-Sonderfall erlaubt.
  `!!`, andere untracked/ignored/modified Records, malformed Codes und
  inkonsistente Paarungen bleiben typisiert `Dirty` beziehungsweise
  `Unverified` gemäß ihrer Evidenzklasse.
- `--ignored=all`, Timeout, Exit-Code, Truncation, Stderr-/Prozessfehler,
  Credential-Redaction und safe revision semantics bleiben erhalten.
- Die Attestation wird über unveränderliche Werte für Checkout-Pfad, sichere
  Revision, Ownership und den Materialisierungs-Lease gebunden. Ein
  `ForTesting`-Wert darf nur noch durch den Test explizit erzeugt werden; kein
  Produktionspfad und kein Test-Transport ergänzt ihn implizit.
- `ExternalSourceRepositoryTransportResult` darf für negative Vertragsfälle
  weiterhin eine fehlende Attestation darstellen, aber jeder positive Pfad
  muss bei fehlender Attestation fail-closed abbrechen.

### 2. Exklusive Ownership-/Lock-Bindung durch Copy, Open und Publish

Betroffen sind `ExternalSourceRepositoryAcquisitionModels.cs:63-123`,
`ExternalSourceRepositoryCacheWriter.cs:145-202,345-435`,
`ExternalSourceRepositoryCacheStorage.cs:15-43,157-200,365-386`,
`ExternalSourceRepositoryCacheMaterializer.cs:11-99`,
`ExternalSourceSnapshotMaterializer.cs:14-101` sowie die Provider- und
Refresh-Call-Chain.

- Ein bestehender oder neuer interner Lock-/Lease-Primitive wird an
  `ExternalSourceCheckoutHandle` und dieselbe Attestation gebunden. Er wird
  vor der ersten relevanten Verifikation erworben und erst nach sicherer
  Materialisierung, erfolgreichem Pointer-Readback beziehungsweise nach
  Snapshot-/Workspace-Übergabe freigegeben.
- Im Cache-Pfad umfasst die Lebensdauer mindestens Source-Copy, Hashing,
  Manifest/Inventory, Readback, `TryPublishPointer` und die
  `BeforePointerPublishedAsync`-/Rollback-Semantik. Kein Hook und keine
  zweite Prüfung darf ein ungeschütztes Fenster eröffnen.
- Im Workspace-Pfad umfasst sie `OpenSolutionAsync`, Projektvalidierung,
  abschließende Attestation und die Übergabe des Snapshot-/Checkout-Lease.
  Mutation zwischen Vorprüfung und Open führt zu einem typisierten
  Unsafe-Source-Ergebnis, nicht zu einem Snapshot.
- Eine commitgebundene, unveränderliche Quellrepräsentation oder ein
  tatsächlich exklusiver Checkout-Materialisierungs-Lease ist zulässig, sofern
  Copy/Open/Publish durchgehend daran gebunden bleiben. Rechecks bleiben
  defense-in-depth und dürfen die Lock-Bindung nicht ersetzen.
- Bei Mutation, Ownership-Verlust, Lock-Fehler, Cancellation oder Exception
  werden Teilgenerationen, Workspace und temporäre Ownership deterministisch
  bereinigt; der vorherige Current-Pointer bleibt unverändert. Die
  1314-/Reparse-Semantik wird nur weiterverwendet, nicht global verändert.

### 3. Typisierte Weitergabe und unverfälschte Test-Fassade

Betroffen sind `ExternalSourceRepositoryAcquirer.cs:141-249,387-438`,
`ExternalSourceRepositoryCacheRefresh.cs:175-321`,
`ExternalSourceRepositorySourcePolicy.cs:116-161`,
`GiteaExternalSourceProvider.cs`,
`AssemblySourceSelectionOrchestrator.cs` sowie
`ExternalSourceRepositoryAcquirerTestTransport.cs:59-90`.

- `ExternalSourceRecordingTransport` gibt das Callback-Ergebnis unverändert
  zurück; `AttachTestAttestation` und jede gleichwertige automatische
  Vervollständigung entfallen.
- Positive Test-Fakes erzeugen Attestations explizit und benennen diesen
  Vertragsbedarf. Ein direkter Regressionstest beweist, dass ein fehlender
  Produktionswert nicht durch die Test-Fassade zu Clean gemacht wird.
- `Dirty` wird an allen Ownership-/Refresh-Grenzen als `Dirty` erhalten;
  malformed, fehlende oder nicht verifizierbare Evidenz bleibt `Unverified`.
  `Verified`, `Degraded`, `Unavailable`, Last-good und CurrentChanged werden
  nicht durch generische Fehlerprojektion überschrieben.
- Cleanup-/Cancellation-Pfade sowie positive Clean-, NoMatch-, Ambiguous-,
  ProviderUnavailable-, RepositoryCapabilityUnavailable- und
  ConfigurationFailure-Fallbacks bleiben durch die bestehende
  `SourcePolicy`-/Selection-Semantik erhalten.

## Deterministische Regressionen und Testisolation

Die Race-Regressionen werden in neuen fokussierten Testdateien angelegt, wenn
die bestehenden Dateien dadurch über ihre aktuelle Grenze wachsen würden.
Verwendet werden `TestTempDirectory`, vorhandene
`IsolatedFixtureLease`-Hilfen sowie lokale `TaskCompletionSource`-,
`SemaphoreSlim`- oder äquivalente Barrieren. Keine Zeitverzögerungen, keine
globalen Collection-/Parallelisierungsdeaktivierungen und keine gemeinsamen
statischen Temp-Pfade.

Abzudecken sind mindestens:

- Statusmatrix für `""`, alleinige/führende/innere Leerrecords, gültiges
  finales CRLF-Framing, alleinigen Ownership-Marker, mehrere gültige Records,
  `!!`, ignorierte Dateien, andere untracked/modified Records und malformed
  Status; dabei keine Secrets in Diagnosen.
- Cache-Race an der echten Bindungsgrenze: Attestation, deterministische
  Mutations-/Lock-Barriere, Copy/Hash/Manifest/Readback/Pointer und Rückgabe.
  Erwartet werden typed UnsafeSource, kein neuer Current-Pointer, unveränderte
  Last-good-Generation, Cleanup und freigegebener Lock.
- Workspace-Race um `OpenSolutionAsync` und die Snapshot-Übergabe. Erwartet
  werden kein Snapshot und kein weiterlebender Checkout-Lease bei Mutation,
  inklusive Cleanup und Cancellation.
- Test-Transport ohne automatische Attestation: explizit positive Fakes,
  explizit fehlender Wert und fail-closed Acquirer-/Provider-Ergebnis.
- Refresh-/Policy-Matrix für `Dirty` versus `Unverified`, Last-good/
  `Degraded`, `CurrentChanged`, Cleanup/Cancellation sowie die positiven
  Fallback-Verträge. Credential-Isolation und bekannte echte
  `ERROR_PRIVILEGE_NOT_HELD`-/1314-Skips bleiben unverändert.

Die Testdateien sind aktuell unter anderem: `ExternalSourceRepositoryAcquirerTests.cs`
(491 Zeilen), `ExternalSourceRepositoryCacheAcquirerTests.cs` (336),
`ExternalSourceRepositoryCacheWriterTests.cs` (390),
`ExternalSourceRepositoryCacheWriterReadBackTests.cs` (394),
`GiteaExternalSourceProviderTests.cs` (242),
`GiteaGitRepositoryTransportTests.cs` (440),
`GiteaGitRepositoryCheckoutStatusTests.cs` (116),
`ExternalSourceRepositoryCheckoutAttestationTests.cs` (162),
`ExternalSourceSnapshotMaterializerTests.cs` (96) und
`ExternalSourceRepositoryCacheRefreshTests.cs` (498). Die bereits vollen
oder nahezu vollen Dateien werden nicht durch eine breite Matrix aufgebläht;
neue Grenzfall- und Race-Suiten bleiben lokal und fokussiert.

## Abnahmekriterien

1. Der Statusparser akzeptiert keine leeren, führenden oder inneren Records
   als Clean; nur die exakt leere Clean-Auswertung und gültiges finales
   Framing nach vollständigen Records bestehen, mit unveränderter typisierter
   Dirty-/Unverified-Semantik.
2. Jede positive Attestation ist unveränderlich an erwartete Revision,
   Ownership und einen exklusiven Materialisierungs-/Checkout-Lease gebunden;
   ein bloßer Vorher-/Nachher-Recheck ohne durchgehende Bindung genügt nicht.
3. Cache-Copy, Hash/Manifest/Inventory, Readback und Pointer-Publish laufen
   unter dieser Bindung; Mutation oder Lock-/Ownership-Verlust führt
   fail-closed zu typed UnsafeSource, ohne neuen Current-Pointer und mit
   erhaltenem Last-good sowie deterministischem Cleanup.
4. Workspace-Open, Projektvalidierung und Snapshot-/Checkout-Übergabe laufen
   unter derselben Bindung; Mutation, Cancellation oder Fehler erzeugen keinen
   falschen Snapshot und hinterlassen keinen Lease-/Temp-Rest.
5. Acquirer, Refresh, Provider und Selection propagieren fehlende,
   `Dirty`- und `Unverified`-Attestations typisiert; der Test-Transport gibt
   Ergebnisse unverändert zurück und ergänzt niemals fehlende Attestations.
6. `Verified`/`Degraded`/`Unavailable`, Last-good/CurrentChanged,
   Cleanup/Cancellation, positive Clean-/Fallback-Verträge,
   Credential-Isolation und 1314-/Reparse-Semantik bleiben regressionsfrei;
   globale McpToolResults- und Reparse-Änderungen fehlen.
7. Die neuen deterministischen Tests sind isoliert, nicht stressgetaggt,
   verwenden keine echten Netzwerk-, Credential- oder Assembly-Ladeaktionen,
   und die Änderungen bleiben auf die Checkout-Trust-/Ownership-/
   Materialisierungsgrenze einschließlich opportunistischer lokaler
   DRY-/Magic-Value-/Dead-Code-Bereinigung beschränkt.
8. `dotnet build`,
   `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
   `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
   sind grün; `Stress` wird nicht ausgeführt. Der Result-Vermerk nennt
   exakte Testzahlen, bekannte 1314-Skips, Cleanup-/Leak-Ergebnis und die
   scoped Nachprüfungen.

## Out of scope

- Host-/MCP-Health-Wiring, Retention/GC und transitive Referenzen.
- EPIC-05, Step-035-Reopen und ein Refresh-/Fetch-Neudesign außerhalb der
  Trust-/Materialisierungsgrenze.
- Globale McpToolResults-, Reparse-, Magic-Value-, Dead-Code- oder
  Tech-Debt-Sweeps.
- Echte Netzwerk-, Credential- oder Assembly-Ladeaktionen sowie Stress-Tests.

Die `roadmap.md` bleibt unverändert: Dieser Step ist eine Fix-Mode-Fortsetzung
mit `corrects: step-037`; eine neue Roadmap-Entscheidung ist nicht zwingend.

## Definition of Done für den Folge-Coder

- [ ] Die acht Abnahmekriterien sind mit Produktionsänderungen und
      deterministischen Regressionen erfüllt.
- [ ] Bestehende relevante positive, Fallback-, Last-good-, Cancellation-,
      Credential- und 1314-/Reparse-Verträge sind nachgewiesen.
- [ ] Scoped MCP-Prüfungen für Violations, Duplikate, Magic Values, Dead Code
      und `safeguard` sind dokumentiert; kein globaler Sweep wurde ergänzt.
- [ ] `step-result.md` enthält exakte Verifikation, Testzahlen, bekannte
      Skips, Cleanup-/Leak-Ergebnis und die tatsächlich verwendeten
      Produktions-/Testgrenzen; `codemap.md` wird nur bei neuen relevanten
      Symbolen aktualisiert.
- [ ] Der Coder hinterlässt seinen Commit auf `main`; danach wird dieser
      frische Coder geschlossen und ein neuer, separater Kritiker gestartet.

## Sicherer Handoff

Die Planer-Session hat keine Produktionsänderungen, Tests, Coder- oder
Kritikerarbeit ausgeführt und keine bestehenden Agenten wiederverwendet. Der
nächste sichere Übergabepunkt ist ein frischer Coder-Agent auf `main` mit
diesem Plan als Startvertrag. Er liest zuerst die zehn `read_first`-Dateien,
fragt die genannten Symbole projektgebunden per MCP ab und öffnet die
`read_on_demand`- sowie Provider-/Refresh-/Testdateien nur entlang der
konkreten Call-Chain. Nach dem Coder wird dieser geschlossen; der Kritiker
muss ebenfalls frisch gestartet werden.
