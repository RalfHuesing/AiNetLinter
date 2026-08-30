---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 037
corrects: step-036
title: "Verifizierten Checkout bis Materialisierung und Publish fail-closed binden"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T05:36:00+02:00
related_to:
  - ../step-036/step-plan.md
  - ../step-036/step-result.md
  - ../step-036/step-review.md
---

# Step 037: Verifizierten Checkout bis Materialisierung und Publish fail-closed binden

## Bezug und Bündelungsentscheidung

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04`; Gitea bleibt die Source of Truth, der lokale Cache
  bleibt eine validierte und besitzgeschützte Zwischenstufe.
- **Korrekturziel:** Step 036, Codercommits `377b5360` und `39fb9fba`,
  Review `c7efaae4`.
- **MAJOR-001:** `git status --porcelain=v1 --untracked-files=all`
  erfasst ignorierte lokale Dateien nicht; der anschließende Cache-Tree-
  Walk kopiert sie dennoch.
- **MAJOR-002:** Zwischen letzter Git-/HEAD-Prüfung und
  `CopySource`/`OpenSolutionAsync` besteht kein bindender Nachweis, dass der
  materialisierte Inhalt unverändert dem verifizierten Commit entspricht.
- **MINOR:** `FailureAfterCleanup` verliert den typisierten `Dirty`-Wert und
  projiziert ihn als `Unverified`.

Alle drei Findings liegen an derselben Checkout-Trust-/Ownership-/
Materialisierungsgrenze. Sie werden als ein vertikales Korrekturpaket
behandelt: Git-Status und Ignore-Semantik liefern die Eingangsattestation,
dieselbe Attestation wird bis Cache- und Workspace-Materialisierung gebunden,
und die daraus entstehenden Failure-/Health-Zustände werden durch Acquirer,
Provider und Selection erhalten. Ein Status-, Assertion- oder Audit-only-
Step würde diese Grenze nicht schließen.

## Split-Gate und Kontextbudget

Der Step hat genau einen Primärvertrag und drei eng gekoppelte Schichten:

1. **Trust-Attestation:** Nur ein ownership-validierter Checkout mit
   cleanem, einschließlich ignorierter Einträge geprüftem Git-Status,
   sicherer erwarteter HEAD-Revision und sicherem Solution-Pfad darf als
   `Verified` gelten.
2. **Materialisierungsbindung:** Cache-Tree-Kopie und
   `OpenSolutionAsync` müssen an dieselbe verifizierte Revision gebunden
   sein. Der Coder wählt dafür eine im bestehenden Prozess-/Checkout-
   Vertrag realistische Lösung; mindestens eine unmittelbare Prüfung vor
   und eine Prüfung nach der jeweiligen Kopier-/Öffnungsgrenze muss
   Mutation-after-validation erkennen. Eine erkannte Drift rollt Publish
   zurück bzw. verwirft den Snapshot und bereinigt den neuen Checkout.
3. **Typed Propagation:** `Dirty` bleibt `Dirty`, `Unverified` bleibt
   `Unverified`; `Verified`/`Degraded`/`Unavailable`, Last-good,
   `CurrentChanged` und die bisherigen Fallbacks bleiben über Acquisition,
   Provider und Selection konsistent.

`max_initial_files: 12`

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/codemap.md`
2. `tasks/decompiled-assembly-analysis/step-036/step-result.md`
3. `tasks/decompiled-assembly-analysis/step-036/step-review.md`
4. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs`
5. `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`
10. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`

### `read_on_demand` (2 Dateien, zusammen höchstens 12 initiale Dateien)

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs`

Nach den projektgebundenen MCP-Abfragen öffnet der Coder die betroffenen
Provider-/Selection- und Testausschnitte gezielt; die vollständige Solution
wird nicht pauschal geladen. `GiteaExternalSourceProvider.cs`,
`AssemblySourceSelectionOrchestrator.cs` sowie bestehende Testdateien sind
damit kein initialer Kontext, sondern nur bedarfsgebundene Anschlusslektüre.

## Aktueller Projektzustand (JIT-Kontext)

- `ExternalSourceRepositoryCheckoutStatus.cs` ist 114 Zeilen lang. Der
  Statusprozess verwendet heute `--untracked-files=all` ohne
  `--ignored=all`; `HasUnexpectedChanges` erlaubt ausschließlich die
  einzelne `.ainetlinter-owner`-Zeile.
- `GiteaGitRepositoryTransport.cs` umfasst 483 physische Zeilen (425 Type
  LOC). Clone und Fetch prüfen Status/HEAD nur an den Transportgrenzen;
  danach geben Acquirer und Cache-/Providerpfad den veränderlichen
  Checkoutpfad weiter.
- `ExternalSourceRepositoryAcquirer.cs` umfasst 479 physische Zeilen (429
  Type LOC, AI-Context-Footprint 1.942/2.500). Er validiert Ownership,
  Reparse-Sicherheit, Solution-Pfad und Transport-Trust, besitzt aber noch
  keine eigenständige Mutation-after-validation-Bindung.
- `ExternalSourceRepositoryCacheStorage.cs` liegt mit 499 Zeilen an der
  Dateigrenze; `CopySource` läuft über alle regulären Dateien und
  `ValidateSourceCheckout` prüft Ownership/Reparse/Solution, nicht Git-
  Ignore- oder Commit-Drift. `ExternalSourceRepositoryCacheWriter.cs` hat
  451 Zeilen und ruft die Kopie vor Pointer-Publish auf.
- `ExternalSourceRepositorySourcePolicy.cs` hat 184 Zeilen; der aktuelle
  `FailureAfterCleanup` reicht den Transport-Trust nicht an die
  Acquisition-Failure-Fabrik weiter. Eine direkte statische Testzuordnung
  für die Policy fehlt.
- `ExternalSourceRepositoryCacheRefresh.cs` hat 410 Zeilen und bewahrt bei
  stale Fehlern den validierten Last-good-Commit. `ExternalSourceSnapshot-
  Materializer.cs` hat 91 Zeilen und öffnet die Solution direkt aus dem
  Checkout; der Provider prüft danach nur Snapshot-/Identity-/Ownership-
  Invarianten.

Aktuelle fokussierte Testgrenzen: `GiteaGitRepositoryCheckoutStatusTests.cs`
107 Zeilen, `GiteaGitRepositoryTransportTests.cs` 438,
`ExternalSourceRepositoryAcquirerTests.cs` 491,
`ExternalSourceRepositoryCacheRefreshTests.cs` 496,
`GiteaExternalSourceProviderTests.cs` 242,
`AssemblyAnalysisToolSupportDegradedTests.cs` 81 und
`AssemblyAnalysisToolSupportTests.cs` 482. Die Integration-
`ExternalSourceSnapshotMaterializerTests.cs` ist 90 Zeilen lang. Die drei
grenznahen Dateien (`CacheStorage` 499, `CacheRefreshTests` 496,
`AcquirerTests` 491) werden nicht mit Inline-Matrizen überladen; neue
Trust-/Mutation-Matrizen gehören in fokussierte neue Testdateien.

## Intention

Nach diesem Step kann kein ignoriertes, untracked, dirty oder zwischen
Attestation und Materialisierung mutiertes Checkout unbemerkt als
`Verified`-Source, Cachegeneration oder Snapshot veröffentlicht werden.
Der sichere Inhaltspfad bleibt trotzdem durch Last-good/Degraded,
CurrentChanged-Reuse, Cleanup/Cancellation und statische Decompilation
vollständig anschlussfähig; Dirty wird an keiner Resultatgrenze in
Unverified verwischt.

## Konkrete Änderungen

### 1. Git-Status und Trust-Attestation — Status/Transport/Policy

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs:10-114`
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs:108-224`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs:45-121`
- bei notwendiger Entkopplung ein neuer fokussierter interner Verifier in
  `src/AiNetLinter/Mcp/Assemblies/`

- Erweitere den nicht-interaktiven Statusaufruf um die Git-Semantik für
  ignorierte Einträge (z. B. `--ignored=all`) und parse die Ausgabe
  fail-closed. Nur die exakt erlaubte Ownership-Markierung darf weiterhin
  als erwartetes Artefakt passieren; `!!`-Einträge, ignorierte Dateien,
  modified/untracked/renamed/conflicted Zustände und unparsebare Statusdaten
  dürfen keinen cleanen Trust erzeugen.
- Halte Diagnosecodes und Trustwerte typisiert und geheimnisfrei. Ein
  Status-/HEAD-Prozessfehler bleibt `Unverified`; ein erkannter dirty oder
  ignorierter Eintrag bleibt `Dirty`. Die bestehende Credential-Umgebung,
  Timeout-, Cancellation-, 1314- und Reparse-Semantik wird wiederverwendet.
- Zentralisiere die wiederholte „verifiziert bis Materialisierung“-Entscheidung
  in einem kleinen, internen Vertrags-/Verifier-Helper oder einer
  verifizierungsbewussten Checkout-Lifetime. Keine zweite konkurrierende
  Statusparser-Kopie und keine Ausweitung von
  `ExternalSourceRepositoryFailurePolicy` zur God-Class.

### 2. Commit-Bindung bis Cache-Publish und Snapshot-Materialisierung

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs:15-114,365-386`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs:53-246`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:166-233`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs:115-247`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs:14-83`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs:20-190`

- Binde die materialisierte Quelle an die vom Transport attestierte
  Revision. Bevor `CopySource` bzw. `OpenSolutionAsync` beginnt, muss der
  reservierte Checkout ownership-, clean-/ignore- und erwartete HEAD-
  verifiziert sein; nach dem Kopieren/Öffnen und unmittelbar vor dem
  endgültigen Erfolg bzw. Pointer-Publish muss dieselbe Bindung erneut
  nachgewiesen werden. Falls der bestehende Git-Prozessvertrag eine
  commitgebundene unveränderliche Repräsentation zulässt, darf diese als
  Quelle verwendet werden; ein bloßer Ownership-Marker oder ein reines
  Vorab-Boolean genügt nicht.
- Ein festgestellter Drift muss typed und fail-closed als unsicherer
  Materialisierungs-/Publishpfad enden: keine neue Generation als gültig
  veröffentlichen, keinen neuen `current`-Pointer stehen lassen, keinen
  Snapshot/Lease als Erfolg zurückgeben und den request-eigenen Checkout
  auch bei Fehler, Cancellation und Rollback bereinigen. Bereits bestehende
  Generationen und der alte Pointer bleiben unverändert.
- Die vorhandene bounded `CopyFile`-/Hash-/Read-back- und Pointer-Rollback-
  Logik wird wiederverwendet. Der Coder darf sie um eine Trust-Bindung
  ergänzen, aber keine neue unbounded Dateiwalk- oder Cache-Identität
  einführen und `CacheStorage.cs` nicht über die 500-Zeilen-Grenze wachsen
  lassen.
- `ExternalSourceSnapshotMaterializer` darf bei veränderter Quelle keinen
  Workspace-/Snapshot-Erfolg liefern. Workspace-Cleanup, Provider-Cleanup
  und Cancellation behalten ihre bisherige Fehlerpriorität; die
  statische Decompilation darf nur nach einem nicht-terminalen, sicheren
  Providerfehler greifen.

### 3. Acquisition-/Provider-/Selection-Propagation

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs:109-190`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs:80-184`
- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs:25-148`
- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs:120-188`
- falls nötig die bestehenden schmalen Resultat-/Provider-Interfaces

- Übernimm den Trustwert des Transportfehlers in `FailureAfterCleanup` und
  die Acquisition-Failure-Fabrik. `Dirty` muss bei gleicher Failure-
  Ursache als `Dirty` sichtbar bleiben; nur tatsächlich nicht auswertbare
  oder noch nicht attestierte Zustände dürfen `Unverified` sein. Die
  `Verified`-/`Degraded`-/`Unavailable`-Invarianten und die
  Last-good-Revision bleiben unverändert.
- Ein verifikationsbedingter Fehler nach einem stale Refresh bewahrt den
  validierten alten Commit ausschließlich als `Degraded`-Metadatum; er
  veröffentlicht weder den mutierten neuen Checkout noch einen Snapshot.
  Ohne Last-good bleibt der Zustand `Unavailable`. `CurrentChanged` darf
  weiterhin nur einen inzwischen frischen und selbst validierten Current
  wiederverwenden.
- Provider und Selection reichen den neuen sicheren Zustand und die
  redigierten Diagnosen weiter. `ProviderDegraded` bleibt nicht-terminal
  und nutzt den statischen Decompilation-Fallback; `ConfigurationFailure`
  bleibt diagnoseunabhängig terminal und wird nicht durch die neue
  Trustprüfung verändert.

## Tests

- [ ] `GiteaGitRepositoryCheckoutStatusTests` erweitert die Matrix um
      `--ignored=all`, `!!`-Einträge, erlaubte Ownership-Markierung,
      modified/untracked und nicht auswertbare Statusausgaben.
- [ ] Neue fokussierte lokale Tests für den gemeinsamen Verifier bzw. die
      Trust-Bindung erzwingen Mutation-after-validation deterministisch
      zwischen Attestation und Cache-Kopie sowie zwischen Attestation und
      Workspace-Öffnung. Erwartet werden typed Failure, kein veröffentlichter
      Generation-/Current-Erfolg, kein Snapshot/Lease und Cleanup.
- [ ] Direkte Acquisition-/Policy-Regressionen prüfen alle drei
      Checkout-Trustwerte; insbesondere bleibt ein Transportresultat mit
      `Dirty` nach `FailureAfterCleanup` `Dirty`, während `Unverified`
      unverändert `Unverified` bleibt.
- [ ] Refresh-Regressionen decken Last-good/Degraded, fehlenden Last-good-
      Stand, CurrentChanged-Reuse, Publish-/Cleanup-/Cancellation-Pfade und
      unveränderten alten Pointer ab; eine Mutation darf keinen stale
      Snapshot als Erfolg erzeugen.
- [ ] Provider-/Selection-/Assembly-Regressionen bestätigen den sichtbaren
      degraded/unavailable Zustand, fehlenden Snapshot bei Drift und den
      statischen Decompilation-Fallback. Positive `NoMatch`, `Ambiguous`,
      `ProviderUnavailable`, `RepositoryCapabilityUnavailable` und
      `ConfigurationFailure` bleiben Gegenproben.
- [ ] Alle Testroots verwenden `TestTempDirectory`; Fakes/Seams bleiben
      lokal und deterministisch, neue Testklassen werden nicht global
      serialisiert. Keine echte Netzwerk-/Credential-/Assembly-Ladeaktion
      und kein `Category=Stress`.

## Abnahmekriterien (8)

1. Der Git-Statusprozess verwendet die erforderliche Ignore-Semantik und
   unterscheidet clean, allowed ownership marker, ignored/untracked/dirty
   sowie nicht auswertbar deterministisch. Jeder nicht explizit erlaubte
   Status wird fail-closed abgewiesen; Diagnosewerte enthalten weder
   Credentials noch Rohprozessausgaben.
2. Nur ein ownership-validierter Checkout mit sicherer erwarteter HEAD-
   Revision und sicherem Solution-Pfad kann `Verified` werden. Ignorierte,
   untracked, dirty, unverified, unbesitzte oder reparse-unsichere Zustände
   erzeugen weder Source-Snapshot noch Cachegeneration noch Registry-Lease.
3. Die Cache-Materialisierung bindet Kopie, Manifest/Inventory und
   Pointer-Publish an dieselbe Trust-/Revision-Attestation. Eine
   deterministisch eingebrachte Mutation nach der Prüfung führt zu einem
   typed fail-closed Ergebnis, entfernt den neuen Generationstand und lässt
   den vorherigen `current`-Pointer unverändert.
4. Die Workspace-Materialisierung bindet `OpenSolutionAsync` an dieselbe
   Attestation. Mutation oder Drift vor/nach dem Öffnen erzeugt keinen
   Snapshot und keinen Lease-Erfolg; Workspace- und Checkout-Cleanup sowie
   Cancellation bleiben sichtbar und prioritätsrichtig.
5. `Dirty` wird vom Transport über Acquirer/FailureAfterCleanup bis zum
   Acquisition-/Providerresultat als `Dirty` erhalten; `Unverified` wird
   nicht als Ersatzklassifikation verwendet. `Verified`, `Degraded`,
   `Unavailable` und Last-good bleiben immutable und vertragskonform.
6. Stale-Refresh-Fehler behalten Last-good ausschließlich als `Degraded`,
   ohne neuen Snapshot/Checkout als Erfolg; fehlender Last-good bleibt
   `Unavailable`. `CurrentChanged` darf weiterhin nur einen sicheren fresh
   Current wiederverwenden. Positive Fallbacks und
   `ConfigurationFailure`-Terminalität inklusive `IsError == false` bleiben
   unverändert.
7. Änderungen bleiben auf diese Trust-/Ownership-/Materialisierungsgrenze
   beschränkt. `CacheStorage.cs` und alle bestehenden grenznahen Dateien
   bleiben unter 500 Zeilen; neue Status-/Verifier-/Mutationstests werden
   fokussiert strukturiert. Direkte DRY-/MagicValues-/DeadCode-Funde werden
   nur innerhalb dieses Pakets und ohne globalen Sweep bereinigt.
8. `dotnet build`,
   `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
   `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
   laufen grün. Das Resultat nennt exakte Zahlen, die bekannten
   Win32-1314-Reparse-Skips, Cleanup-/Temp-/Testhost-Leaks sowie scoped
   MCP-/Qualitätsnachweise und den unverändert ehrlichen Safeguard-Stand;
   Stress wird nicht ausgeführt.

## Testisolation und Verifikationsgrenzen

Die neuen Regressionen bleiben in `AiNetLinter.FastTests` als Unit-/Component-
Tests mit lokalen Fakes für Transport, Prozessausführung, Verifier und
Cache-Writer. Für den realen Workspace-Lifecycle wird nur die bestehende
isolierte Integration-Testinfrastruktur genutzt; alle Roots kommen aus
`TestTempDirectory`. Es gibt keinen direkten Zugriff auf
`Path.GetTempPath()`, keine echten Gitea-/HTTP-Prozesse, keine Credentials,
kein Assembly-Load und keine Änderung an globaler Testparallelität.

Die Mutation-Seams müssen die Reihenfolge explizit erzwingen, nicht nur eine
bereits vor dem Transportresultat veränderte Fixture liefern: Attestation →
Mutation → Copy/Open → erneute Prüfung. Bei einem Cache-Publish-Fall sind
Generation, Pointer und Checkout-Lifetime zu prüfen; beim Provider-Fall sind
Snapshot, Registry-Lease und statischer Fallback zu prüfen. Die beiden
bekannten echten Reparse-Fälle bleiben wegen `ERROR_PRIVILEGE_NOT_HELD`
(`1314`) hosttransparent als Skips dokumentiert; sie werden weder simuliert
noch global gesperrt.

## MCP-, DRY-, Magic-Values- und Dead-Code-Disposition

Vor dem Edit fragt der Coder die betroffenen Symbole mit dem
projektgebundenen MCP und absolutem
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ab: zuerst
`get_feature_context`/`get_symbol_body`, danach gezielt
`find_references`, `get_impact`, `dependency_graph` und
`get_test_context`. Nach dem Patch folgen scoped `get_violations`,
`find_duplicates`, `find_magic_values`, `find_dead_code` und `safeguard`;
keiner dieser Läufe wird als Ersatz für die Regressionen verwendet.

Der relevante Symbolumfang ist:

- `ExternalSourceRepositoryCheckoutStatus` und
  `GiteaGitRepositoryTransport`;
- `ExternalSourceRepositoryAcquirer`,
  `ExternalSourceRepositorySourcePolicy` und die Resultatmodelle;
- `ExternalSourceRepositoryCacheStorage`,
  `ExternalSourceRepositoryCacheWriter` und
  `ExternalSourceRepositoryCacheRefresh`;
- `ExternalSourceSnapshotMaterializer`,
  `GiteaExternalSourceProvider` und
  `AssemblySourceSelectionOrchestrator`.

Der Coder zentralisiert nur neue oder durch dieses Paket direkt duplizierte
Status-/Verifier-/Diagnosewerte. Bestehende TD-001 bis TD-005 werden nicht
automatisch in diesen Step gezogen; es gibt keinen globalen
Magic-Value-/Dead-Code-/Safeguard-Sweep und keinen neuen öffentlichen
MCP-/Host-/Health-Vertrag.

## Scope

- Vollständige Git-Status-/Ignore-Semantik für den besitzgeschützten
  Gitea-Staging-Checkout.
- Commit-/Trust-Bindung von Cache-Kopie, Manifest/Inventory, Pointer-Publish
  und Workspace-Materialisierung mit fail-closed Mutationserkennung.
- Typed Trust-Propagation durch Acquisition, Refresh, Provider und Selection.
- Lokale deterministische Regressionen für Dirty/Ignore/TOCTOU, Cleanup,
  Cancellation, Last-good/Degraded, CurrentChanged und positive Fallbacks.
- Paketbezogene DRY-/MagicValues-/DeadCode-Bereinigung, soweit sie direkt
  aus der neuen Trust-Bindung entsteht.

## Out of Scope

- Kein Host-/MCP-Health-Wiring, kein neues globales MCP-Resultatschema und
  keine Änderung an `McpToolResults`.
- Keine Retention, GC, explizite Invalidierung, transitive Referenzen,
  EPIC-05 oder eine gemeinsame Capability-Matrix.
- Kein Refresh-/Fetch-Neudesign außerhalb der Trust-/Materialisierungs-
  absicherung und keine neue Local-Origin-/Build-Fingerprint-Semantik.
- Keine echte Netzwerk-, Credential- oder Assembly-Ladeaktion; kein
  privilegierter Reparse-/Win32-Sweep und kein Stress-Test.
- Keine globale DRY-, MagicValues-, DeadCode- oder Safeguard-Bereinigung;
  `TD-001` bis `TD-005` bleiben unverändert.
- Keine Abschwächung von Last-good/Degraded/Unavailable,
  CurrentChanged, Cleanup/Cancellation, positiven Fallbacks oder
  `ConfigurationFailure`-/Reparse-1314-Semantik.
- Während dieses Planer-Schritts: keine Produktionsänderung, kein Testlauf,
  keine Coder-/Kritikerarbeit.

## Definition of Done für den Folge-Coder

- [ ] `step-036`-Review-Findings sind in einem gemeinsamen
      Trust-/Materialisierungsvertrag behoben.
- [ ] Ignorierte Dateien passieren kein Clean-Gate und gelangen nicht in
      eine veröffentlichte Generation oder einen Snapshot.
- [ ] Mutation-after-validation wird bis Cache-Publish und
      Workspace-Materialisierung deterministisch erkannt und fail-closed
      bereinigt.
- [ ] `Dirty` bleibt typisiert erhalten; `Unverified` wird nicht pauschal
      für Dirty-Failures verwendet.
- [ ] Last-good/Degraded/Unavailable, CurrentChanged, Cleanup/Cancellation,
      positive Fallbacks, 1314/Reparse und ConfigurationFailure bleiben
      regressionsgeschützt.
- [ ] Build und beide vollständigen Nicht-Stress-Gates sind mit exakten
      Zahlen, Skips, Leaks und scoped Qualitätsnachweisen dokumentiert.
- [ ] Der Coder aktualisiert `codemap.md` und `step-result.md`, committet
      Code/Tests sowie Doku separat; danach wird dieser Coder geschlossen
      und ein neuer, separater Kritiker gestartet.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — projektgebundene MCP-
  Symbol-/Impact-/Testabfragen vor ergänzender Textarbeit, absolute Ziele und
  scoped Qualitätsnachweise.
- `.agents/rules/AiNetLinter.mdc` — Datei-/Methodengrenzen, typed Resultate,
  keine stillen Fehler und keine neue DuplicateCode-/MagicValue-Struktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — fail-closed Result-Pattern,
  Ownership-/Cleanup-/Testisolation, Zero-Warning-Gate und proaktive
  paketbezogene DRY-/MagicValues-/DeadCode-Behandlung.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — genau
  ein frischer Coder und danach ein frischer Kritiker seriell; keine
  Agent-Wiederverwendung und kein Audit-/Mini-Step.

## Sicherer Handoff

Der nächste sichere Übergabepunkt ist ein neuer Coder-Agent auf `main` mit
`tasks/decompiled-assembly-analysis/step-037/step-plan.md`. Er liest zuerst
den Handoff, diesen Plan, den Step-036-Result-/Review-Kontext und die zehn
`read_first`-Dateien, führt die projektgebundenen MCP-Abfragen aus und
öffnet die zwei `read_on_demand`-Dateien sowie Provider-/Selection-Tests nur
bei konkretem Bedarf.

Die Implementierung beginnt am Status-/Ignore-Gate, etabliert dort die
einheitliche Trust-Attestation und führt sie anschließend ohne neuen
öffentlichen Vertrag durch Cache-Publish und Workspace-Materialisierung.
Der sichere Endzustand ist: keine Generation, kein Pointer-Erfolg, kein
Snapshot und kein Lease bei erkannter Drift; `Dirty` bleibt `Dirty`, ein
stale Fehler bleibt Last-good/Degraded oder ohne Last-good Unavailable. Nach
Coder-Code-/Doku-Commit wird der Coder geschlossen und ein frischer,
separater Kritiker gestartet. Dieser Planer hat keine Produktionsänderung,
keinen Testlauf und keine Coder-/Kritikerarbeit ausgeführt.
