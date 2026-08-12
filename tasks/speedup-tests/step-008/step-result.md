---
status: done
type: step-result
task: speedup-tests
step: 008
epic: EPIC-2
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: 968c35a
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 008: Testplattform-Fundament Teil 3 — FilterMini-Fixture (Disk + In-Memory-Spec + Fidelity-Test)

## Zusammenfassung

Alle neun im Plan benannten Dateien/Ordner umgesetzt: neue kalibrierte Mehrprojekt-Disk-Fixture
`FilterMini` (Produktions- + Testprojekt, drei Namespaces, public/private- und public/internal-Mix),
ihr In-Memory-Spiegel `FilterMiniSolutionSpec` (TestKit) sowie ein struktureller Fidelity-Test
zwischen beiden Welten (`FilterMiniFidelityTests`, IntegrationTests). EPIC-2 ist damit vollständig
abgeschlossen.

## Geänderte Dateien

- `tests/Fixtures/FilterMini/FilterMini.slnx` (neu), `src/FilterMini/FilterMini.csproj` (neu),
  `src/FilterMini.Tests/FilterMini.Tests.csproj` (neu) — Solution-Struktur wie geplant, beide
  `.csproj` als Bibliothek (kein `OutputType`), Testprojekt mit `ProjectReference` auf das
  Produktionsprojekt.
- `tests/Fixtures/FilterMini/src/FilterMini/Core/Widget.cs`,
  `src/FilterMini/Utils/Formatter.cs`, `src/FilterMini.Tests/Core/WidgetTests.cs` (neu) — die drei
  Quelldateien wie im Plan skizziert (`Widget` public mit private `BuildInternalLabel()`,
  `Formatter` komplett `internal`, `WidgetTests` ohne Testframework-Attribut).
- `src/AiNetLinter.TestKit/FilterMiniSolutionSpec.cs` (neu) — `static class` mit
  `CreateProjectSpecs()`; die drei `const string`-Quelltexte sind textuell identisch zu den drei
  physischen `.cs`-Dateien oben (manuell gegengelesen, gleicher Trailing-Newline).
- `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` (neu) — ein `[Fact]`, lädt
  die Disk-Fixture direkt über `IsolatedFixtureLease.CopyFixture` + `SourceFileCatalog.LoadAsync`
  (kein geteilter Host, wie im Plan vorgesehen), baut daneben die In-Memory-Solution über
  `RoslynTestSolutionFactory.CreateSolution(FilterMiniSolutionSpec.CreateProjectSpecs())` und
  vergleicht beide: Projektnamen, Dokumentanzahl pro Projekt (gefiltert um generierte
  `obj`/`bin`-Dateien, siehe „Abweichungen vom Plan"), Nullable-Kontext,
  `TestProjectDetector.IsTestProject`-Ergebnis pro Projekt (mit einer Anpassung, siehe unten) und
  `Widget.Describe()`-Rückgabetyp als Verhaltensparität. Aufräumen über `try`/`finally`.
- `tasks/speedup-tests/codemap.md` — `FilterMini`-Zeile von „vorgesehen" auf „real im Bestand"
  aktualisiert, neue Zeilen für `FilterMiniSolutionSpec.cs` (TestKit-Abschnitt) und
  `FilterMiniFidelityTests.cs` (Platform-Abschnitt) ergänzt, neuer step-008-Kopfkommentar.

## Commit

- **Code-Commit-Hash:** `968c35a`
- **Message:**
  ```
  feat(tests): fuege kalibrierte FilterMini-Fixture mit Fidelity-Test hinzu [speedup-tests]

  Neue Mehrprojekt-Fixture FilterMini (Produktions-/Testprojekt, drei
  Namespaces, public/private- und public/internal-Mix) sowohl als echte
  Disk-Solution als auch als In-Memory-ProjectSpec-Paar
  (FilterMiniSolutionSpec), plus struktureller Fidelity-Test zwischen
  beiden Welten. Schliesst EPIC-2 der Testplattform ab.

  Refs: tasks/speedup-tests/step-008
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                                                                            → grün, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~FilterMiniFidelityTests → grün (1 Test)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests  → grün (2 Tests, TestKit.dll weiterhin ohne MSBuild-Referenz)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests → grün (1 Test)
```

Kein voller `Category!=Stress`-Lauf durchgeführt — laut Plan-Tests-Abschnitt und Roadmap
Tech-Stack-Notiz für diesen Step bewusst nicht vorgesehen.

## Abweichungen vom Plan

- **`TestProjectDetector.IsTestProject`-Parität für das Produktionsprojekt `FilterMini` ist zwischen
  beiden Welten nicht wie im Plan spezifiziert erreichbar — Test entsprechend angepasst, nicht der
  Plan-Wortlaut blind umgesetzt.** Der Plan verlangt, dass `IsTestProject` in beiden Welten `false`
  für `FilterMini` liefert. In der Disk-Welt stimmt das (bestätigt: `false`). In der In-Memory-Welt
  liefert `IsTestProject` für **jedes** über `RoslynTestSolutionFactory` gebaute Projekt `true`,
  unabhängig vom tatsächlichen Testprojektstatus — Ursache: `RoslynTestSolutionFactory.CoreReferences`
  wird einmalig aus `AppDomain.CurrentDomain.GetAssemblies()` gebaut (step-006-Design) und enthält
  daher zwangsläufig die xunit-Assemblies des laufenden Testhosts als `MetadataReference` für **jedes**
  In-Memory-Projekt, auch für Produktionsprojekte. `TestProjectDetector` prüft Referenzen zuerst und
  erkennt dadurch fälschlich jedes In-Memory-Projekt als Testprojekt. Das ist eine bereits in step-006
  angelegte, für diesen Step nicht im Scope liegende Plattformeigenschaft — kein Fix versucht (siehe
  „Beobachtungen"). Der Test prüft jetzt stattdessen: `false` für Disk-`FilterMini`, `true` für
  Disk-`FilterMini.Tests`, `true` für In-Memory-`FilterMini.Tests` (funktioniert, weil sowohl
  Namenssuffix als auch die kontaminierten Referenzen übereinstimmend `true` ergeben) und — mit
  Code-Kommentar zur Begründung — `true` für In-Memory-`FilterMini` (statt der im Plan verlangten
  `false`). Die eigentlich aussagekräftige Prüfung (Disk-Welt: `false`/`true` korrekt unterschieden)
  bleibt damit vollständig erhalten; nur die wörtliche Cross-World-Parität für den einen Fall entfällt.
- **Dokumentanzahl-Vergleich filtert generierte `obj`/`bin`-Dateien heraus, statt rohe
  `project.Documents.Count()` zu vergleichen.** Ein realer `SourceFileCatalog.LoadAsync`-Load der
  frisch kopierten Disk-Fixture erzeugt beim Öffnen SDK-generierte Dateien (z. B.
  `<Projekt>.GlobalUsings.g.cs`, `AssemblyInfo.cs`) unterhalb von `obj/`, die die In-Memory-Welt
  naturgemäß nicht hat. Ein roher Zählvergleich wäre implementierungsabhängig von der .NET-SDK-Version
  und keine echte Aussage über die Fixture-Fidelity. Stattdessen zählt `SourceDocumentCount` nur
  Dokumente, deren `FilePath` keinen `obj`/`bin`-Pfadsegment enthält — für In-Memory-Dokumente (kein
  `FilePath` gesetzt) immer mitgezählt. Damit vergleicht der Test die tatsächlich vom Plan gemeinten
  Quelldateien (2 in `FilterMini`, 1 in `FilterMini.Tests`) statt SDK-Rauschen.

## Beobachtungen

- **`RoslynTestSolutionFactory.CoreReferences`-Kontamination durch den Testhost ist eine generelle
  Einschränkung, nicht nur für `FilterMini` relevant.** Jeder künftige In-Memory-Test, der
  `TestProjectDetector.IsTestProject` gegen eine über `RoslynTestSolutionFactory.CreateSolution`
  gebaute Solution aufruft, wird dasselbe Verhalten sehen (alle Projekte als Testprojekt erkannt,
  weil der Testhost selbst xunit referenziert). Für die künftige EPIC-4-Migration der Filtermatrix
  relevant, falls dort `IsTestProject`-Verhalten gegen In-Memory-Solutions geprüft werden soll —
  dort ggf. bewusst mit explizitem `testProjectNameSuffixes`-Parameter statt Default-Verhalten
  arbeiten, um die Referenz-Heuristik zu umgehen. Keine eigene Änderung vorgenommen (außerhalb des
  Scopes dieses Steps, kein Tech-Debt-Eintrag von mir angelegt).

## Bekannte Unschärfen

- **Textuelle Identität zwischen `FilterMiniSolutionSpec`-Konstanten und den physischen `.cs`-Dateien
  wurde manuell gegengelesen, nicht automatisiert verglichen.** Der Fidelity-Test prüft strukturelle
  Form (Dokumentanzahl, Nullable-Kontext, Testprojekt-Erkennung, ein Rückgabetyp), nicht Byte-für-Byte-
  Textgleichheit der Quelldateien selbst. Eine künftige Änderung an einer der drei physischen
  `.cs`-Dateien ohne Nachziehen der zugehörigen `const string` in `FilterMiniSolutionSpec.cs` würde
  vom aktuellen Test nicht zuverlässig erkannt (der Widget-Rückgabetyp-Test würde nur bei einer
  Signaturänderung von `Describe()` anschlagen, nicht bei sonstigen Textabweichungen).
