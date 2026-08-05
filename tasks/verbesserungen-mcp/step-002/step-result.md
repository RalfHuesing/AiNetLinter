---
status: done
type: step-result
task: verbesserungen-mcp
step: 002
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-05T07:23:00Z
code_commit_hash: 7f4d6ba
status_after: done
blocker_category: n/a
---

# Result Step 002: Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsaechlich zum Laufen bringen

## Zusammenfassung

Die drei `Microsoft.CodeAnalysis.*`-Paketreferenzen in `AiNetLinter.csproj`
wurden von `5.3.0` auf `5.6.0` angehoben (`dotnet list package --outdated`
vorab erneut geprueft: `5.6.0` ist weiterhin die aktuelle Stable-Version,
identisch zur im Plan dokumentierten Verifikation). Die drei Tests in
`SourceFileCatalogBlazorPartialTests` wurden umbenannt und ihre Assertions
umgekehrt — sie belegen jetzt das korrekte Verhalten (Razor-Generator laeuft,
`ComponentBase` als Basistyp aufgeloest, kein `CS0115`). Eine Abweichung vom
Plan bei Test 3 (`get_file_skeleton`): der Basistyp wird dort weiterhin nicht
angezeigt, siehe „Abweichungen vom Plan".

## Geänderte Dateien

- `src/AiNetLinter/AiNetLinter.csproj` — drei `PackageReference`-Versionen (`Microsoft.CodeAnalysis.CSharp`, `.Workspaces.MSBuild`, `.CSharp.Workspaces`) von `5.3.0` auf `5.6.0`.
- `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` — alle drei Tests umbenannt + Assertions umgekehrt, Klassenkommentar auf korrekten Zustand umformuliert.
- `src/AiNetLinter.Tests/Fixtures/BlazorPartialMiniFixtureWorkspace.cs` — geprueft, keine Änderung noetig (wie im Plan erwartet; `SiteViewCsPath` wird weiterhin von Test 1 gebraucht).

## Commit

- **Code-Commit-Hash:** `7f4d6ba`
- **Message:**
  ```
  fix(roslyn): Microsoft.CodeAnalysis-Pakete auf 5.6.0 anheben [verbesserungen-mcp]

  Die im lokalen .NET-SDK gebuendelte Razor-Source-Generator-Assembly ist
  gegen Microsoft.CodeAnalysis(.CSharp) 5.5.0 gebaut; das bisherige Pinning
  auf 5.3.0 in AiNetLinter.csproj verhinderte, dass die CLR den
  Generator-Typ im Prozess laden konnte (0 Generatoren statt 1 -
  FileLoadException wurde von Roslyns Analyzer-Loader verschluckt). Nach
  dem Bump auf 5.6.0 (aktuell neueste Stable-Version) fliesst die vom
  Razor-Generator erzeugte zweite Partial-Deklaration (": ComponentBase")
  korrekt in die Compilation ein - kein CS0115 mehr auf den
  override-Lifecycle-Methoden von .razor.cs-Codebehind-Klassen.

  Die drei Tests aus SourceFileCatalogBlazorPartialTests, die bisher den
  Bug-Zustand belegten, wurden umbenannt und ihre Assertions umgekehrt:
  sie pruefen jetzt das korrekte Verhalten. Der get_file_skeleton-Test
  prueft nur noch das Verschwinden des Compile-Fehler-Hinweises, nicht
  mehr die Sichtbarkeit des Basistyps - die Skeleton-Extraktion liest je
  Datei ausschliesslich die dort syntaktisch deklarierte Basisliste,
  unabhaengig vom Bump.

  Refs: tasks/verbesserungen-mcp/step-002
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün (0 Warnung(en), 0 Fehler)
dotnet test AiNetLinter.slnx  → grün (1257 Tests, 0 Fehler, inkl. der 3 umgekehrten)
```

Paket-Version tatsächlich eingetragen: `5.6.0` für alle drei
`Microsoft.CodeAnalysis.*`-Pakete. `dotnet list package --outdated` gegen
`src/AiNetLinter/AiNetLinter.csproj` vor dem Eintragen erneut ausgeführt:
`5.6.0` ist weiterhin die neueste verfügbare Stable-Version (Stand
2026-08-05) — identisch zur im Plan dokumentierten Verifikation, kein
zwischenzeitliches Update. Nebenbei sichtbar (nicht Teil dieses Bumps):
`ExCSS` 4.3.1→4.3.2, `Microsoft.Build.Framework`/`Microsoft.NET.StringTools`
18.7.1→18.8.2, `ModelContextProtocol` 2.0.0→2.1.0, `System.CommandLine`
2.0.9→2.0.10 sind ebenfalls veraltet — außerhalb des Scopes dieses Steps,
nicht angefasst.

## Abweichungen vom Plan

- **Test 3 (`get_file_skeleton`) prüft die Basistyp-Sichtbarkeit nicht,
  entgegen der Code-Skizze im Plan.** Der Plan sah vor:
  `Assert.Contains(": ComponentBase", text, ...)`. Empirisch (per
  `Assert.Fail(text)`-Zwischenschritt zur Inspektion des tatsächlichen
  Skeleton-Markdowns verifiziert, danach wieder entfernt) zeigt
  `get_file_skeleton` für `SiteView.razor.cs` auch nach dem Paket-Bump
  **keinen** Basistyp — der zugrunde liegende Renderer
  (`SkeletonSyntaxWalker.BuildTypeInfo`,
  `src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:113`) liest
  `BaseTypes` ausschließlich aus `node.BaseList` der Syntax **dieser
  einen Datei** — unabhängig davon, ob im selben Symbol über ein anderes
  Partial (hier: die Razor-generierte zweite Deklaration) semantisch ein
  Basistyp aufgelöst wird. Das ist eine allgemeine, von Blazor
  unabhängige Eigenschaft der Datei-Skeleton-Extraktion (gilt für jede
  über mehrere Dateien gesplittete `partial class`, bei der nur eine
  Datei die Basisliste deklariert) und **nicht** durch den
  Roslyn-Paket-Bump beeinflussbar — Ändern würde eine Änderung an
  `SkeletonSyntaxWalker.cs` erfordern, was der Plan explizit nicht
  vorsieht und außerhalb des Scopes dieses Steps liegt. Ich habe daher
  Test 3 umbenannt zu
  `GetFileSkeleton_SiteViewRazorCs_NoLongerReportsCompileError` und
  geprüft, was tatsächlich durch den Bump behoben wird: das Verschwinden
  von `"Compile-Fehler"`/`"CS0115"` im Skeleton-Output (das ist weiterhin
  korrekt und verifiziert). Die Assertion zur Basistyp-Sichtbarkeit habe
  ich ersatzlos gestrichen statt sie künstlich grün zu bekommen — kein
  Symptom-Fixing, sondern Anpassung an eine empirisch widerlegte
  Plan-Annahme (Regel „Widerspricht das … dokumentiere das unter
  Abweichungen vom Plan, ändere nicht einfach stillschweigend den
  Ansatz").
  - **Konsequenz für die Konzept-Definition-of-Done:** Der
    Konzept-Schnell-Check-Punkt „`get_file_skeleton(SiteView.razor.cs)`
    kein CS0115, Basisklasse `ComponentBase` sichtbar" ist damit nur zur
    Hälfte erreicht — CS0115 ist weg, aber die Basisklasse wird in
    `get_file_skeleton` (anders als in `get_index_scope` und direkt in
    der `Compilation`, siehe Test 1+2) nicht angezeigt. Das ist eine
    reale Feature-Lücke in `GetFileSkeletonTool`/`SkeletonSyntaxWalker`,
    keine Auswirkung des Paket-Bumps — überlasse ich dem Kritiker zur
    Einordnung (neuer Tech-Debt-Eintrag oder neuer Konzept-Punkt?).

## Beobachtungen

- Siehe oben unter „Abweichungen vom Plan" — die Feature-Lücke in
  `SkeletonSyntaxWalker` (Basistyp nur aus der eigenen Datei-Syntax, nicht
  aus dem semantisch aufgelösten Symbol) betrifft nicht nur Blazor-Partials,
  sondern jede über mehrere Dateien gesplittete `partial class`, bei der nur
  eine Datei die Basisliste deklariert. Guter Kandidat für einen eigenen
  Tech-Debt-Eintrag durch den Kritiker.
- Weitere veraltete Pakete (`ExCSS`, `Microsoft.Build.Framework`,
  `Microsoft.NET.StringTools`, `ModelContextProtocol`, `System.CommandLine`)
  wurden von `dotnet list package --outdated` mit angezeigt — bewusst nicht
  angefasst, außerhalb des Scopes dieses Steps.
- Die Rest-Unschärfe „Versions-Kopplung an lokales SDK" aus dem Plan
  bestätigt sich unverändert: dieser Fix ist umgebungsabhängig vom lokal via
  `MSBuildLocator` gefundenen .NET-SDK (aktuell `10.0.302`).

## Bekannte Unschärfen

- Die empirische Verifikation der Basistyp-Abwesenheit in Test 3 wurde per
  temporärem `Assert.Fail(text)`-Zwischenschritt durchgeführt (danach
  entfernt, working tree war zwischenzeitlich nicht dauerhaft verschmutzt) —
  analog zum im Plan beschriebenen Diagnose-Vorgehen des Planers selbst.
- Wie im Plan dokumentiert: P2 „Rausch-Hinweis eindämmen" ist nur gegen die
  synthetische `BlazorPartialMini`-Fixture verifiziert, nicht gegen eine
  reale Solution.
- `.agents/rules/AiNetLinter.mdc` wurde vor diesem Coder-Lauf nicht
  verändert vorgefunden (anders als in step-001 dokumentiert) — kein
  Fund hier.
