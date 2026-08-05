---
status: open
type: step-plan
task: verbesserungen-mcp
step: 002
title: "Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsaechlich zum Laufen bringen"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05
related_to: [step-001]
---

# Step 002: Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsaechlich zum Laufen bringen

## Bezug

- **Task:** `verbesserungen-mcp`
- **Epic:** `EPIC-01` aus `roadmap.md` — Blazor-Symbolgraph-Integration
  (P1). Dieser Step deckt den **zweiten und letzten Teil** des Epics ab:
  den eigentlichen Fix, nachdem `step-001` die Fixture + drei
  Bug-dokumentierende (gruene) Tests angelegt hat. Dieser Step kehrt
  diese drei Tests um (sie belegen danach das **korrekte** Verhalten)
  und schliesst damit sowohl P1 („Blazor-Partials") als auch den
  Fixture-verifizierbaren Teil von P2 („Globaler Rausch-Hinweis
  eindaemmen") ab — siehe „Aktueller Projektzustand" unten, warum beide
  automatisch mit demselben Fix zusammenfallen.
- **Konzept-Referenz:** `Konzept.md` Scope „P1 — Blazor-Partials" (Muss:
  „volle Integration, kein Workaround") + „P2 — Globaler Rausch-Hinweis
  eindaemmen" (Muss: „entfaellt vermutlich automatisch nach P1") +
  „Definition of Done" Schnell-Check-Punkte 1 und 2 (`get_index_scope`
  kein 1322-Errors-Hinweis mehr; `get_file_skeleton(SiteView.razor.cs)`
  kein `CS0115`, Basisklasse `ComponentBase` sichtbar).

## Aktueller Projektzustand (JIT-Kontext)

**Root Cause empirisch verifiziert (nicht nur vermutet) — mit einem
temporaeren, wieder verworfenen Diagnose-Test gegen die
`BlazorPartialMini`-Fixture aus `step-001`:**

- `step-001/step-plan.md` „Notes" markierte die Ursache explizit als
  offene Recherchefrage: ob `LinterEngine.CreateWorkspaceProperties()`
  (`src/AiNetLinter/Core/LinterEngine.cs:86-93`, setzt u. a.
  `DesignTimeBuild=true`, `SkipCompilerExecution=true`,
  `RunAnalyzers=false`) die Ursache ist. **Das ist widerlegt:** Ein
  Diagnose-Test hat `project.AnalyzerReferences` direkt nach
  `MSBuildWorkspace.OpenSolutionAsync` inspiziert — mit `RunAnalyzers=true`
  **und** mit `RunAnalyzers=false` sind exakt dieselben 18
  Analyzer-Referenzen geladen, darunter
  `Microsoft.CodeAnalysis.Razor.Compiler.dll` (die Razor-Source-Generator-
  Assembly). `RunAnalyzers`/`SkipCompilerExecution`/`DesignTimeBuild`
  sind für dieses Symptom **irrelevant** — nicht anfassen.
- Auch `project.AdditionalDocuments` (enthaelt `SiteView.razor` korrekt),
  die globalen `AnalyzerConfigOptions` (`build_property.RootNamespace`,
  `build_property.RazorLangVersion` etc.) und die Datei-Metadaten
  (`build_metadata.AdditionalFiles.TargetPath`/`CssScope`) sind bereits
  vollstaendig und korrekt geladen — **kein** fehlendes MSBuild-Property,
  **kein** fehlendes `AdditionalFiles`-Item.
- Die **tatsaechliche Ursache:** `project.AnalyzerReferences` enthaelt
  `Microsoft.CodeAnalysis.Razor.Compiler.dll` zwar als Datei-Referenz,
  aber `AnalyzerFileReference.GetGenerators(project.Language)` liefert
  für genau diese eine Referenz **0 Generatoren** (alle anderen
  18 Referenzen liefern korrekt ihre `IncrementalGeneratorWrapper`-
  Instanzen). Direktes `Assembly.LoadFrom` derselben DLL + Reflection
  bestaetigt: Der Typ mit `[Generator]`-Attribut kann nicht geladen
  werden — `System.IO.FileLoadException: Could not load file or
  assembly 'Microsoft.CodeAnalysis(.CSharp), Version=5.5.0.0 ...  The
  located assembly's manifest definition does not match the assembly
  reference.` Grund: Das lokal installierte .NET-SDK
  (`10.0.302`, `dotnet --list-sdks`) buendelt eine
  `Microsoft.CodeAnalysis.Razor.Compiler.dll`, die gegen
  **`Microsoft.CodeAnalysis(.CSharp) 5.5.0-2.26118.1`** gebaut ist
  (verifiziert via
  `Microsoft.CodeAnalysis.Razor.Compiler.deps.json` im SDK-Verzeichnis),
  waehrend `src/AiNetLinter/AiNetLinter.csproj:13-16` `Microsoft.
  CodeAnalysis.CSharp`/`.Workspaces.MSBuild`/`.CSharp.Workspaces` auf
  **`5.3.0`** pinnt. Da diese aeltere Version bereits im Prozess geladen
  ist (aus AiNetLinter selbst, `AiNetLinter.Tests.csproj` referenziert
  Roslyn nur transitiv via `ProjectReference`), kann die CLR die vom
  Razor-Compiler benoetigte 5.5.0-Assembly nicht binden — Roslyns
  eigener Analyzer-Loader schluckt diesen Fehler pro Typ und meldet
  daher schlicht 0 gefundene Generatoren statt eines sichtbaren
  Fehlers. Das ist unabhaengig davon, ob man den `GeneratorDriver`
  manuell selbst antreibt (auch das getestet: 0 Diagnostics, 0 neue
  Syntax-Trees, `SiteView`-Basistyp bleibt `object`) — der Generator-Typ
  ist im Prozess schlicht nicht ladbar.
- **Fix verifiziert:** Testweiser Bump von `Microsoft.CodeAnalysis.CSharp`/
  `Microsoft.CodeAnalysis.Workspaces.MSBuild`/`Microsoft.CodeAnalysis.
  CSharp.Workspaces` in `AiNetLinter.csproj` von `5.3.0` auf `5.6.0`
  (aktuell neueste verfuegbare Stable-Version laut
  `dotnet list package --outdated`, > 5.5.0 des SDK-Bedarfs) behebt das
  Problem **vollstaendig und ohne jede Code-Aenderung** an
  `SourceFileCatalog.cs`/`LinterEngine.cs`/`McpCompileDiagnostics.cs`:
  - `project.GetCompilationAsync()` (der bereits von
    `McpCompileDiagnostics.GetProjectErrorsAsync`,
    `src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs:51`, genutzte
    **Standard-Pfad**, keine manuelle `GeneratorDriver`-Ansteuerung
    noetig) liefert danach `SourceGeneratedDocuments.Count() == 1` und
    `compilation.GetTypeByMetadataName("BlazorPartialMini.SiteView")
    .BaseType` = `Microsoft.AspNetCore.Components.ComponentBase`.
  - Die 3 Tests aus `step-001` (`SourceFileCatalogBlazorPartialTests`)
    wurden mit diesem Bump probehalber laufen gelassen: alle drei
    schlagen jetzt fehl — **weil das dokumentierte Bug-Verhalten nicht
    mehr eintritt** (kein `CS0115`, `GetIndexScope` liefert
    `.cs: 1 Dateien (voll vom Symbolgraph abgedeckt)` **ohne**
    „Hinweis:"-Praefix, `GetFileSkeleton` zeigt `: ComponentBase`).
    Das ist exakt das erwuenschte Ziel dieses Steps — die drei
    Assertions umkehren.
  - `dotnet build AiNetLinter.slnx` bleibt mit dem Bump **gruen (0
    Warnungen, 0 Fehler)** — keine Breaking-API-Probleme zwischen 5.3.0
    und 5.6.0 in diesem Projekt beobachtet. Ein voller `dotnet test`-Lauf
    (alle ~1257 Tests) wurde in dieser Planungsphase **nicht**
    durchgefuehrt (das ist Coder-Aufgabe, siehe „Definition of Done") —
    nur der gezielte Build- und Blazor-Test-Check.
  - Alle experimentellen Aenderungen (Diagnose-Testdatei,
    `AiNetLinter.csproj`-Bump) wurden nach der Verifikation
    zurueckgesetzt — der Working Tree war beim Abschluss der Planung
    wieder sauber (`git status --porcelain` leer). Dieser Step-Plan ist
    somit die erste tatsaechliche Umsetzung des Bumps.
- **Wichtige Erkenntnis fuer den Scope dieses Steps:** Das ist kein Bug
  in AiNetLinters eigenem Code, sondern eine Versions-Kopplung zwischen
  den unabhaengig voneinander versionierten Achsen „von AiNetLinter
  referenzierte Roslyn-NuGet-Pakete" und „vom lokal via `MSBuildLocator`
  gefundene .NET-SDK gebuendelte Razor-Compiler-Assembly". Das bedeutet
  auch: dieser Fix ist **umgebungsabhaengig** — ein neueres .NET-SDK auf
  einer anderen Maschine (z. B. CI, San.smart.Planner.Platform-
  Entwicklungsumgebung) koennte eine noch neuere
  `Microsoft.CodeAnalysis.Razor.Compiler`-Version buendeln und die
  Diskrepanz erneut auftreten lassen. Das ist eine dokumentierte
  Rest-Unschaerfe (siehe „Notes"), keine Bring-Schuld dieses Steps —
  „volle Integration" im Sinne des Konzepts bedeutet hier: den
  **echten** Razor-Generator ueber den **echten** Workspace-Pfad laufen
  lassen (kein gefaketer Basistyp, keine Diagnose-Unterdrueckung),
  nicht: Versions-Kompatibilitaet für alle zukuenftigen SDKs garantieren.
- **Bestehende Strukturen wiederverwendet, keine neuen erfunden:** Die
  Assertion-Formulierung `Assert.Contains(".cs: N Dateien (voll vom
  Symbolgraph abgedeckt)", ...)` fuer den Happy-Path von
  `get_index_scope` folgt exakt dem Muster aus
  `GetIndexScopeToolTests.ExecuteAsync_MixedFixture_ReturnsCsCountMarkedAsGraphCovered`
  (`src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs:36-45`).
  Kein neuer Test-Helper, keine neue Fixture noetig — `step-001` hat
  bereits alles Noetige angelegt.

## Intention

Nach diesem Step ist der Roslyn-Symbolgraph fuer Blazor-Partial-Klassen
korrekt: `SourceFileCatalog.LoadAsync` liefert eine `Compilation`, in der
die vom Razor-Source-Generator erzeugte zweite Partial-Deklaration
(`: ComponentBase`) tatsaechlich enthalten ist — identisch zu dem, was
`dotnet build` ohnehin produziert. Erreicht wird das **ausschliesslich**
durch einen Versions-Bump dreier `PackageReference`-Eintraege in
`AiNetLinter.csproj` (keine Logik-Aenderung an
`SourceFileCatalog.cs`/`LinterEngine.cs`/`McpCompileDiagnostics.cs` —
diese Dateien funktionieren bereits korrekt, sobald der Generator im
Prozess ueberhaupt ladbar ist). Die drei in `step-001` angelegten Tests
werden auf das nun korrekte Verhalten umgekehrt und dabei umbenannt
(die alten Namen behaupten fälschlich noch den Bug-Zustand).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/AiNetLinter.csproj` (Zeile 13, 15-16)

- **Was:** Die drei `PackageReference`-Versionen
  `Microsoft.CodeAnalysis.CSharp`, `Microsoft.CodeAnalysis.Workspaces.MSBuild`,
  `Microsoft.CodeAnalysis.CSharp.Workspaces` von `5.3.0` auf `5.6.0`
  anheben. **Vor dem Eintragen:** `dotnet list package --outdated`
  gegen `src/AiNetLinter/AiNetLinter.csproj` erneut ausfuehren — falls
  zwischenzeitlich eine neuere Stable-Version verfuegbar ist, diese
  statt `5.6.0` verwenden (die relevante Bedingung ist: neuer oder
  gleich der vom lokal installierten .NET-SDK gebuendelten
  `Microsoft.CodeAnalysis.Razor.Compiler`-Abhaengigkeit — bei Zweifel
  einfach die neueste verfuegbare Stable-Version nehmen, kein Grund,
  konservativer zu sein).
- **Warum:** Siehe „Aktueller Projektzustand" — das ist die tatsaechliche
  Fix-Ursache, empirisch verifiziert.

### Datei 2: `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` (komplett, alle 3 Tests + Klassenkommentar)

- **Was:**
  - Klassenkommentar (Zeilen 13-20) umformulieren: beschreibt jetzt den
    **korrekten** Zustand nach Razor-Generator-Integration statt den
    (ehemaligen) Bug — ID-frei, siehe `step-001`-Konvention.
  - `LoadAsync_BlazorPartialFixture_ReportsCS0115OnOverrideLifecycleMethod`
    → umbenennen (z. B.
    `LoadAsync_BlazorPartialFixture_ResolvesComponentBaseWithoutCompileErrors`)
    und Assertions umkehren: `diagnostics` fuer `SiteViewCsPath` entweder
    nicht vorhanden oder ohne `CS0115`
    (`Assert.False(errorsByFile.TryGetValue(...))` oder
    `Assert.DoesNotContain(diagnostics, d => d.Id == "CS0115")`, je
    nachdem was `GetErrorsByFileAsync` bei 0 Fehlern tatsaechlich
    zurueckgibt — pruefen und passend waehlen). Zusaetzlich (staerkere,
    Konzept-naehere Assertion): ueber `catalog.Solution` das Projekt/die
    `Compilation` holen und `compilation.GetTypeByMetadataName(
    "BlazorPartialMini.SiteView")?.BaseType?.ToDisplayString()` gegen
    `"Microsoft.AspNetCore.Components.ComponentBase"` pruefen.
  - `GetIndexScope_BlazorPartialFixture_ShowsAggregateCompileErrorHint`
    → umbenennen (z. B.
    `GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint`) und
    Assertions umkehren: `Assert.DoesNotContain("Hinweis:", text,
    StringComparison.Ordinal)` sowie (Muster aus
    `GetIndexScopeToolTests.ExecuteAsync_MixedFixture_
    ReturnsCsCountMarkedAsGraphCovered`) `Assert.Contains(".cs: 1 Dateien
    (voll vom Symbolgraph abgedeckt)", text, StringComparison.Ordinal)`.
  - `GetFileSkeleton_SiteViewRazorCs_MissesComponentBaseBaseType` →
    umbenennen (z. B.
    `GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseBaseType`) und
    Assertions umkehren: `Assert.Contains(": ComponentBase", text,
    StringComparison.Ordinal)`, `Assert.DoesNotContain("Compile-Fehler",
    text, StringComparison.Ordinal)`.
- **Warum:** Diese drei Tests sind exakt der in `step-001` angelegte
  Reproduktions-Beweis — jetzt der Verifikations-Beweis fuer den Fix.
  Kein neuer Test noetig, nur Umkehrung + Umbenennung (alte Namen
  waeren nach dem Fix faktisch falsch).

### Datei 3 (ggf.): `src/AiNetLinter.Tests/Fixtures/BlazorPartialMiniFixtureWorkspace.cs`

- **Was:** Keine Aenderung erwartet — nur pruefen, ob nach dem Bump noch
  alle Properties (`SiteViewCsPath`) gebraucht werden. Falls ja: nichts
  tun.
- **Warum:** Vollstaendigkeitshalber genannt, damit der Coder nicht
  vergisst kurz zu pruefen, ob die Fixture-Klasse selbst betroffen ist
  (erwartete Antwort: nein).

## Tests

- [ ] `SourceFileCatalogBlazorPartialTests.LoadAsync_BlazorPartialFixture_ResolvesComponentBaseWithoutCompileErrors`
      (umbenannt + umgekehrt, siehe oben — exakter Name kann beim Schreiben
      leicht abweichen, Kernaussage muss erhalten bleiben)
- [ ] `SourceFileCatalogBlazorPartialTests.GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint`
      (umbenannt + umgekehrt)
- [ ] `SourceFileCatalogBlazorPartialTests.GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseBaseType`
      (umbenannt + umgekehrt)
- [ ] Voller `dotnet test`-Lauf (alle ~1257+ Tests) weiterhin gruen —
      **besonders wichtig bei diesem Step**, da der Versions-Bump eine
      Kernabhaengigkeit ist, die potenziell viele andere Roslyn-nutzende
      Codepfade im ganzen Tool beruehren koennte (auch wenn der gezielte
      Build-Check in der Planungsphase bereits saubere 0
      Warnungen/Fehler zeigte). Bei unerwarteten Fehlschlaegen an
      anderer Stelle: nicht vorschnell auf Unabhaengigkeit vom Bump
      schliessen, erst gegenpruefen (z. B. `git stash` + Vergleichslauf
      ohne Bump).

## Definition of Done

- [ ] `AiNetLinter.csproj`-Bump umgesetzt (Datei 1)
- [ ] Alle drei Tests umbenannt + umgekehrt (Datei 2)
- [ ] `dotnet build` grün (0 Fehler/Warnungen)
- [ ] `dotnet test` (Volllauf) grün, inkl. der 3 umgekehrten Tests
- [ ] Kurzer manueller Blick auf `dotnet list package --outdated`
      dokumentiert im `step-result.md` (welche Version tatsaechlich
      eingetragen wurde und warum)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[verbesserungen-mcp]`)
- [ ] `step-002/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit v3 Pflicht pro
  Logik-Änderung (hier: Test-Umkehrung selbst ist die Änderung);
  Commit-Vorschlag am Ende jeder Antwort mit Datei-Änderungen (reiner
  Commit-Text, kein Shell-Befehl).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive
  (`TreatWarningsAsErrors`, gilt fuer `AiNetLinter.csproj` — beim Bump
  besonders relevant, da neue Roslyn-Version ggf. neue
  Compiler-Warnungen (z. B. neue Nullable-/Obsolete-Hinweise) einführen
  könnte, die dann als Fehler behandelt werden); sparsamer
  Kommentar-Einsatz, kein Task-/Step-/Epic-Bezug im Code-Kommentar.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3` — bei Testfehlern/langem
  Output `TestResults/latest.trx` auslesen statt Testlauf erneut
  unvollständig zu starten.
- `.agents/rules/AiNetLinter.mdc` — Grenzwerte gelten weiterhin fuer die
  geaenderte Testklasse (Methodenlaenge, Datei laenge etc. — die Klasse
  bleibt bei 3 Tests, sollte unproblematisch bleiben).

## Bekannte Ausnahmen

- Keine.

## Code-Skizze (optional)

```xml
<!-- src/AiNetLinter/AiNetLinter.csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.6.0" />
<PackageReference Include="Microsoft.Build.Locator" Version="1.11.2" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.6.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.6.0" />
```

```csharp
// src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs — Skizze Test 1 nach Umkehrung
[Fact]
public async Task LoadAsync_BlazorPartialFixture_ResolvesComponentBaseWithoutCompileErrors()
{
    using var fixture = new BlazorPartialMiniFixtureWorkspace();
    using var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

    var errorsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(catalog.Solution, CancellationToken.None);
    Assert.False(errorsByFile.TryGetValue(fixture.SiteViewCsPath, out _));

    var project = catalog.Solution.Projects.Single();
    var compilation = await project.GetCompilationAsync();
    var siteView = compilation!.GetTypeByMetadataName("BlazorPartialMini.SiteView");
    Assert.Equal("Microsoft.AspNetCore.Components.ComponentBase", siteView?.BaseType?.ToDisplayString());
}
```

## Notes

- **Warum keine Aenderung an `SourceFileCatalog.cs`/`LinterEngine.cs`
  trotz Konzept-Pointer darauf:** `Konzept.md` „Wo im Projekt" nennt
  diese Dateien als vermutliche Fundstellen — das war vor der
  Root-Cause-Recherche in dieser Planung eine berechtigte Vermutung.
  Nach empirischer Verifikation (siehe oben) ist klar: der Code dort ist
  bereits korrekt, das Problem liegt ausschliesslich in der
  Paket-Versionierung. Kein Widerspruch zum Konzept — „volle
  Integration, kein Workaround" wird durch den Versions-Bump erreicht,
  nicht durch eine Code-Änderung an diesen Dateien. Falls der Kritiker
  das anders sieht: die empirische Herleitung (inkl. der konkreten
  `FileLoadException`) ist oben vollstaendig dokumentiert und
  nachvollziehbar, nicht nur behauptet.
- **Rest-Unschaerfe „Versions-Kopplung an lokales SDK":** Sollte auf
  einer anderen Maschine (anderes .NET-SDK-Feature-Band) die gleiche
  Diskrepanz in umgekehrter Richtung auftreten (SDK buendelt eine noch
  neuere Razor-Compiler-Version als `5.6.0`), waere ein erneuter
  Versions-Bump noetig. Das ist eine reale, aber inhaerente
  Charakteristik jedes Tools, das MSBuildWorkspace + `MSBuildLocator`
  gegen ein beliebiges lokal installiertes SDK kombiniert — kein Fix
  in diesem Step kann das grundsaetzlich ausschliessen. Falls der Coder
  das fuer erwaehnenswert haelt: kurzer Hinweis im `step-result.md`
  reicht, kein Blocker. Kein Tech-Debt-Eintrag durch den Planer hier
  vorweggenommen — das entscheidet der Kritiker beim Review.
- **P2 „Rausch-Hinweis" nur im Fixture-Rahmen geprueft:** Dieser Step
  bestaetigt das Verschwinden des Hinweises nur fuer die synthetische
  `BlazorPartialMini`-Fixture (Konzept-konform, siehe „Verworfene
  Alternativen" — keine Verifikation gegen San.smart.Planner.Platform).
  Ob der reale 1322-Errors-Hinweis in der externen Solution durch
  denselben Mechanismus vollstaendig verschwindet, bleibt eine
  begruendete Annahme, keine in diesem Task ueberpruefbare Tatsache.
- **Diagnose-Vorgehen fuer Nachvollziehbarkeit dokumentiert, nicht als
  Datei hinterlassen:** Die Root-Cause-Verifikation in dieser Planung
  nutzte einen temporaeren Test (`AnalyzerFileReference.GetGenerators`,
  `Assembly.LoadFrom` + Reflection auf die Razor-Compiler-DLL,
  `AppDomain.CurrentDomain.GetAssemblies()`-Vergleich) sowie einen
  probeweisen `AiNetLinter.csproj`-Bump — beides wurde nach Abschluss
  der Verifikation vollstaendig zurueckgesetzt (`git status --porcelain`
  war leer). Der Coder muss diese Diagnose nicht wiederholen, die
  Kernaussage ist oben in „Aktueller Projektzustand" vollstaendig
  festgehalten.
