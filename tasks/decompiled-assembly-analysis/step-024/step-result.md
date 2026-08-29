---
status: done
type: step-result
task: decompiled-assembly-analysis
step: 024
corrects: null
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T13:48:30+02:00
code_commit_hash: 428cc4b328cacf53285c1837af3d2fd309c2ec0e
status_after: done
blocker_category: n/a
---

# Result Step 024: Erfolgreiches Acquirer-zu-Snapshot-Wiring mit Checkout-Lifetime

## Zusammenfassung

Der bestehende Acquirer kann jetzt über den internen
`GiteaExternalSourceProvider` einen vollständig materialisierten
`ExternalSourceSnapshot` liefern. Der Provider übernimmt die bestehende
Fehlerprojektion unverändert, reicht Cancellation als echte
`OperationCanceledException` weiter und verwirft jeden nicht vollständig
validierten Snapshot fail-closed.

Die Materialisierung öffnet ausschließlich die validierte Solution im
Checkout über den zentral registrierten Design-Time-
`MSBuildWorkspace`. Die Identität wird exakt aus kanonischer Mapping-URL,
geladener Revision und repository-relativem Solution-Pfad gebaut.

Der Snapshot besitzt den Checkout bis zum eigenen oder Registry-Dispose.
Workspace und Checkout werden idempotent in dieser Reihenfolge bereinigt;
ein Workspace-Dispose-Fehler verhindert den anschließenden Checkout-Cleanup
nicht. Vor der Besitzübertragung räumen Provider und Materializer alle
bereits erworbenen lokalen Ressourcen begrenzt auf.

## Geänderte Dateien

Produktion:

- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs` (neu) — Acquirer-zu-Provider-Adapter, bestehende Fehlerprojektion, Identity-Prüfung und fail-closed Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceSnapshotMaterializer.cs` (neu) — interner testbarer Materializer-Vertrag.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs` (neu) — lokale MSBuildWorkspace-Materialisierung mit WorkspaceFailed-/Leer-Solution-Fail-Closed-Pfad.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceCheckoutOwner.cs` (neu) — minimale interne Ownership-Grenze zur Begrenzung des transitive Host-Footprints.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs` — Snapshot-Owner, Dispose-Reihenfolge und aggregierte Cleanup-Fehlerbehandlung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs` — konkreter Checkout-Handle implementiert die interne Owner-Grenze.
- `src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs` — zentrale interne MSBuildWorkspace-Erzeugung für Loader und Materializer gemeinsam nutzbar gemacht.

Tests/Test-Infrastruktur:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs` (neu) — fünf lokale Component-Regressionsfälle für Erfolg/Identity, Acquirer-Fallback, Materialisierungsfehler, Cancellation und fehlenden Owner.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs` (neu) — zwei lokale Integrationstests für echten MSBuildWorkspace-Erfolg und fehlende Solution.
- `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs` — bestehende Snapshot-Fixture um Revision und optionalen Owner erweitert.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs` — vorhandene lokale Acquirer-Test-Infrastruktur um den gemeinsamen BaselineMini-Solution-Copy ergänzt.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` — privaten Fixture-Copy durch die gemeinsame bestehende Test-Infrastruktur ersetzt.

Nicht geändert wurden `AssemblySourceSelectionOrchestrator`,
`AssemblyAnalysisHostComposition`, MCP-Registrierung, Transport-/Credential-,
Prozessbaum- und Native-Verträge sowie `task-state.md`, `roadmap.md` und
`tech-debt.md`.

## Abnahmekriterien-Nachweis

1. Der erfolgreiche Acquirer-Fall liefert genau einen verfügbaren Snapshot;
   der Provider-Test prüft den nicht verworfenen Snapshot und die direkte
   Acquirer-/Materializer-Kette.
2. URL, Revision und Solution-Pfad werden über
   `SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision)` exakt
   und ohne erneutes HEAD-Lesen verwendet.
3. Der Materializer nutzt den zentralen Workspace-Pfad und öffnet nur
   `checkout.SolutionPath`; es gibt keinen Assembly-Load, keine Reflection-
   Ausführung, keinen Restore, Build oder Testlauf von Checkout-Code.
4. Die lokale Integration prüft, dass der Checkout vor Snapshot-Dispose
   existiert und nach doppeltem Snapshot-Dispose genau einmal verschwindet;
   der Provider-Test prüft zusätzlich Registry-Dispose und Owner-Ablehnung.
5. Acquirer-Fehler werden über
   `ExternalSourceProviderFailureProjection.FromUnavailableAcquisition`
   projiziert; die vorhandene ProviderUnavailable-/1314-/Reparse-/HTTP-/Git-
   Klassifikation und Diagnosebegrenzung bleiben im Acquirer-/Transportpfad.
6. WorkspaceFailed, leere/ungültige Materialisierung, Snapshot-Validierungs-
   fehler und Cancellation erzeugen keinen verfügbaren partiellen Snapshot.
   Cancellation und ihr Token werden unverändert weitergereicht; lokale
   Ressourcen werden vor Besitzübertragung bereinigt.
7. Alle neuen Tests sind netzwerkfrei und verwenden
   `TestTempDirectory`, `IsolatedFixtureLease`, die bestehende
   `ExternalSourceSnapshotTestFactory` und `BaselineMini`; kein Gitea, Git,
   Remote oder ad-hoc OS-Temp-Pfad wurde eingeführt.
8. Build, Nicht-Stress-Gates und die relevanten MCP-/Qualitätsprüfungen sind
   grün; Host-Wiring und Prozess-/Handle-Native-Sequenz bleiben unverändert.

## Build-/Test-Output

```text
dotnet build
→ grün (0 Warnungen, 0 Fehler)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaExternalSourceProviderTests"
→ 5 bestanden, 0 übersprungen, 0 Fehler

dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceSnapshotMaterializerTests"
→ 2 bestanden, 0 übersprungen, 0 Fehler

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
→ 1.999 bestanden, 1 übersprungen, 0 Fehler; 2.000 gesamt

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
→ 370 bestanden, 0 übersprungen, 0 Fehler

Stress-Tests
→ nicht ausgeführt
```

Der einzige Skip ist der bestehende echte Reparse-Test wegen
`ERROR_PRIVILEGE_NOT_HELD (1314)`. Normale lokale Repositoryfälle liefen
grün; kein globaler 1314-Lockout wurde eingeführt.

## MCP-, DRY-, MagicValues- und DeadCode-Nachweis

- `get_feature_context`/`get_symbol_body` wurden vor und nach der Änderung
  mit absolutem `projectRoot` für Acquirer, Provider, Snapshot, Registry,
  Materializer-Seam und Workspace-Loader verwendet. Die neuen Produktions-
  typen liegen unter den bestehenden Größen-/Komplexitätsgrenzen und haben
  0 datei- bzw. symbolbezogene Violations.
- `find_references` findet fünf direkte Provider-Konstruktor-Aufrufer in
  `GiteaExternalSourceProviderTests`; der Snapshot-Typ hat 34 bekannte
  Verwendungen. Der `get_impact`-Aufruf auf dem Snapshot-Typ bestätigt die
  direkte Snapshot-/Registry-/Fixture-Kette. Der methodenspezifische Dispose-
  Resolver des MCP-Dienstes überweitete bei diesem Symbol auf die allgemeine
  `Dispose`-Familie; daraus wurde keine zusätzliche Architekturannahme
  abgeleitet.
- `safeguard` meldet `5,65/10`, PASS bei dem Test-Threshold `0,00`, mit drei
  bestehenden/out-of-scope Warnungen: zwei `MaxDirectoryChildren`-Befunde
  und der bestehende `DaemonHostCommand`-Footprint. Der durch den Owner-
  Übergang berührte Assembly-Host-Footprint liegt bei `2.499/2.500`.
- Scoped `get_violations` meldet 0 Verstöße für alle geänderten Produktions-
  und Testdateien. Der relevante Produktionsscope umfasst 315 Methoden;
  `find_duplicates` findet 0 exakte Clone-Cluster. Die 5 strukturellen
  Kandidatencluster sind bestehende, fachfremde Helper-/Result-/Native-
  Ähnlichkeiten; `refactoring-drift` für `ThrowDisposeFailures` findet 0
  Kandidaten. Die Testscopes melden 0 exakte Duplikate bei 83 bzw. 21
  Methoden.
- `find_magic_values` findet 0 neue Kandidaten in Provider, Materializer
  und Owner-Seam. `SourceSnapshotModels` meldet ausschließlich die sechs
  vorhandenen Validierungs-/Exception-Texte. Die Treffer in den neuen Tests
  sind testlokale Fixture-Namen, URL-/Revision-Testwerte und der absichtlich
  redigierte Secret-String; es wurde kein Produktions-Constants-Sweep
  ausgelöst.
- `find_dead_code` im Assemblies-Scope findet 36 Low-Confidence-Kandidaten,
  0 High-Confidence-Kandidaten; 34 davon sind bestehende native ABI-Felder,
  zwei weitere bestehende Symbole. Nichts davon wurde gelöscht.
- `search_pattern` findet in den neuen Materialisierungsdateien keine
  `Assembly.Load`-/`AssemblyLoadContext`-/Reflection-/Restore-/Build-/Test-
  oder Netzwerk-Clients. Die einzigen HTTP(S)-Treffer sind die erwarteten
  URL-Schemata der vorhandenen Snapshot-Identity-Normalisierung.

## Abweichungen und offene Risiken

- Der Snapshot speichert nicht den großen konkreten Checkout-Typ, sondern
  die minimale interne `IExternalSourceCheckoutOwner`-Schnittstelle; der
  bestehende `ExternalSourceCheckoutHandle` implementiert sie. Dadurch bleibt
  die semantische Owner-Übergabe exakt erhalten und der bestehende Host-
  Footprint bleibt unter dem Limit. Es wurde keine öffentliche API erweitert.
- Der Host verwendet weiterhin bewusst den
  `UnavailableExternalSourceProvider`; produktives Host-Wiring ist ein
  späterer eigener Vertrag und nicht Teil von Step 024.
- Die Materialisierung prüft diagnostische Workspace-Fehler und eine leere
  Solution fail-closed, führt aber keinen Restore, Build, Dirty-/Health- oder
  Integritätsvertrag ein. Diese Grenzen bleiben für Folgepakete offen.
- Workspace-Dispose-Fehler werden im Provider-Fallback nicht exponiert;
  Checkout-Cleanup bleibt über den bestehenden `CleanupState` beobachtbar.
  Direkte Snapshot-Dispose-Fehler werden nach dem Cleanup aggregiert bzw.
  mit erhaltener Exception-Information weitergereicht.

## Commit

- **Code-Commit-Hash:** `428cc4b328cacf53285c1837af3d2fd309c2ec0e`
- **Message:**
  ```
  feat: Source-Snapshot-Wiring mit Checkout-Lifetime umsetzen [decompiled-assembly-analysis]
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** folgt unmittelbar für dieses Step-Result.
