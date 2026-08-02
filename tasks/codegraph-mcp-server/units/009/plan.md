---
unit: 009
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-02
trigger: tasks/codegraph-mcp-server/state.md Block "Nächste Aktion (für 009)" — keine Coder-pflichtige Muss-Have-Einheit aus konzept.md mehr offen, daher folgerichtig: kleinste Tech-Debt-Schließung mit klarem Scope
---

# Plan Einheit 009 — TD-016a: 2 verbleibende Fixture-Workspaces auf `FixtureWorkspaceBase` umstellen

## 1. Ziel der Einheit

Die zwei seit TD-016-Teilschluss (Commit `6c872e4`, vor Einheit 007) noch
nicht auf `FixtureWorkspaceBase` migrierten Test-Fixture-Klassen
`CompileErrorMiniFixtureWorkspace` (71 Z.) und `GitImpactMiniFixtureWorkspace`
(166 Z.) konsolidieren: ihre `private static`-Helper `CopyFixture`,
`IsGeneratedPath` und `FindSolutionRoot` löschen, beide von
`FixtureWorkspaceBase : IDisposable` ableiten, eigenen Code behalten.
Damit wird TD-016a vollständig geschlossen und der Refactor aus 006/007
auf alle 4 Workspace-Klassen ausgedehnt — 0 verbleibende Duplikation
in der Test-Fixture-Schicht. Bezug: `tech-debt.md` TD-016a Eintrag
(Index-Zeile 46 + Body Z. 164-168), 007-Kritiker-Vorschlag
(`units/007/review.md` TD-Vorschläge), 006-Coder-Beobachtung
(`units/006/result.md` Abschnitt "Tech-Debt-Beobachtung").

## 2. Scope-Entscheidung mit Begründung

**Gewählt: B1 (TD-016a).**

**Warum gerade diese Wahl:**

- **Konzept-DoD ist vollständig erfüllt** (`konzept.md` Z. 590-660 DoD, alle
  Punkte abgehakt durch EPIC-01..08 approved in 001-008). Die einzig
  verbleibenden Konzept-Diskrepanzen aus 008 (Z. 539-552, 550, 564) sind
  explizit **User-pflichtig** (A7) — der Coder darf `konzept.md` nicht
  anfassen, also keine Coder-Einheit.
- **Die P0/P1-Rest-Erweiterungen** aus `konzept.md` Z. 207-324 sind alle
  explizit als „geplant" / „optional" markiert, nicht im DoD. Sie sind
  Coder-pflichtig, aber: A1 (Auto-Discovery) und A4 (Kaltstart) berühren
  `McpCodeGraphServer` — dort steht der Konstruktor **exakt am Limit**
  (5/5 Dependencies, TD-009). Eine Erweiterung dort triggert TD-009,
  was eine 2-teilige Einheit (Refactor + Feature) statt einer sauberen
  Einheit macht. A2/A3 (Verzeichnis-Sweep + `mtime`) sind gekoppelt und
  ebenfalls nicht trivial. A5 (--mcp-log) und A6 (`ILintConsole`) sind
  jeweils mit `McpServerOptionsFactory`-Footprint-Druck (TD-014) bzw.
  Konzept-Update-Pflicht (Z. 564) verbunden. A7 (Last-Fixture) hängt
  konzeptuell von A4 ab (Kaltstart messen). **Keiner davon ist die
  kleinstmögliche Coder-Einheit.**
- **B1 (TD-016a) ist die kleinste, am besten abgrenzbare echte
  Coder-pflichtige Arbeit**: 2 Dateien, ~1-2 h, eigenständiger Refactor
  ohne Berührung von Produktionscode, ohne Konzept-Edit, ohne
  Funktionsänderung. Schließt eine offene TD-Zeile, die der Kritiker
  in 007 explizit als „kann standalone laufen" markiert hat. Keine
  externen Abhängigkeiten, keine Konzept-Diskrepanz, keine
  Auswirkungen auf die Roadmap-Reihenfolge der P0/P1-Erweiterungen
  (die Reihenfolge C → B1 → A1 → A4 aus dem User-Hinweis gilt
  entsprechend — C ist User-pflichtig, also fängt die Coder-Liste
  mit B1 an).
- **Risiko-Bilanz**: CompileErrorMini ist trivial (kein eigener
  Dispose-Override nötig). GitImpactMini hat genau **eine** Stolperfalle
  (das Windows-spezifische `ClearReadOnlyAttributes(RootPath)` vor dem
  Löschen — siehe Z. 71-79 der aktuellen Datei). Beides ist im Vorgehen
  unten explizit adressiert, der Refactor selbst bleibt unter
  `MaxMethodLineCount` (60 Z.) und die Klassen unter `MaxLineCount`
  (500 Z.) mit deutlichem Puffer.

**Warum nicht die anderen Kandidaten:**

- **(A1) `rules.json`-Auto-Discovery** — P0 im Konzept, aber: berührt
  `McpServerCommand.ResolveConfig` (Z. 72-79) und damit potentiell
  `McpCodeGraphServer` (TD-009), und braucht eine Vermerk-Logik in
  `get_violations` (Test-Tool-Logik, neuer Test-Setup). Komplexer,
  mehr Synergierisiko. Bessere Wahl für 010.
- **(A4) Kaltstart entkoppeln** — wichtigste P0-Erweiterung, aber
  4-6 h, ändert `McpServerCommand.RunAsync` (Z. 29-43) und
  `McpCodeGraphServer`-Konstruktor, **triggert TD-009 zwingend**
  (ein 6. Dependency wird gebraucht für den Background-Load-Task).
  Sollte als 010/011 zusammen mit dem TD-009-Refactor in einer
  Doppeleinheit laufen, nicht isoliert.
- **(A2/A3) Verzeichnis-Sweep + `mtime`-Kurzschluss** — gekoppelt,
  3-4 h, Risiko: Projekt-Mapping über längsten gemeinsamen Pfad-Präfix
  ist nicht trivial. Ebenfalls besser für 011/012.
- **(A5) `--mcp-log` Call-Log** — 2-3 h, eigenständig, aber:
  berührt `McpServerOptionsFactory` (TD-014, 16 Z. Puffer) und ist
  damit selbst ein TD-014-Auslöser. Bessere Wahl als 010 nach
  B1, aber B1 zuerst, weil risikoärmer.
- **(A6) `ILintConsole` für MCP** — 3-4 h, strukturelle Lösung für
  stdout-Schutz. Konzept Z. 564 muss vor der Implementierung an
  Code-Stand angepasst werden (User-pflichtig, A7) — sonst
  implementiert der Coder auf Basis veralteter Annahmen. Erst
  nach der Konzept-Pflege-Einheit sinnvoll.
- **(A7) Last-Fixture + Messlauf** — hängt konzeptuell von A4 ab
  (Kaltstart messen), 4-6 h. Frühestens 2-3 Einheiten später.
- **(C) Konzept-Pflege** — 3 veraltete Stellen in `konzept.md`
  an Code-Stand anpassen (Z. 539-552 Tool-Status, Z. 550
  `get_impact`-Beschreibung, Z. 564 Kaltstart-Suggestion). **Nicht
  Coder-pflichtig** (A7 verbietet Konzept-Edits durch den Coder).
  Wenn der Orchestrator eine 009 hierfür aufrufen würde, müsste
  er den User direkt ansprechen, nicht den Coder-Agenten.

**Fertig-Meldung wurde explizit erwogen und verworfen**: Das Konzept
selbst sagt Z. 664-665 zwar „*Keine blockierenden offenen Punkte*",
aber der User-Prompt selbst listet B1 als empfohlene nächste
Coder-Einheit und fragt mich explizit nach meiner Wahl mit
Begründung. Eine Fertig-Meldung wäre defensiv-konservativ und
würde eine dokumentierte, kleine, risikoarme TD-Schließung
ungenutzt lassen. Der User-Prompt hat klar gemacht: „deine Wahl,
deine Begründung" — und B1 ist die rationale Wahl.

## 3. Vor-der-Planung-Checks (was nachgelesen + was gefunden)

### 3.1 `FixtureWorkspaceBase.cs` (gelesen, 73 Z., seit Commit `6c872e4`)

- **Konstruktor** ist `protected FixtureWorkspaceBase(string fixtureFolderName, string tempPrefix)` — nimmt 2 String-Args, baut `TestTempDirectory` + `RootPath` + kopiert Fixture (Z. 17-23). Übernimmt damit exakt die 3 Helper, die in den 2 verbleibenden Klassen dupliziert sind.
- **`Dispose`** ist `public virtual void Dispose()` (Z. 27-31) — überschreibbar, ruft nur `_tempDir.Dispose()` + `GC.SuppressFinalize(this)`. Genau das, was GitImpactMini überschreiben muss, um vorher `ClearReadOnlyAttributes(RootPath)` aufzurufen.
- **`CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`** sind `protected static` (Z. 33-72) — für abgeleitete Klassen sichtbar, falls nötig (z. B. für `GitImpactMiniFixtureWorkspace.CalculatorPath`, das `RootPath` braucht — bekommt es über die Basis-Property).
- **`#nullable enable`** am Dateianfang — die abgeleiteten Klassen brauchen das auch (haben sie schon).
- **TestTempDirectory** ist im `TestTempDirectory.cs` (58 Z., `6c872e4`) — verwaltet das `Guid.NewGuid()`-Suffix und das spätere `Directory.Delete` — bereits der einzige Code-Pfad, der den Windows-Read-Only-Schutz braucht, **aber** GitImpactMini hat zusätzliche `git`-Objekte mit Read-Only-Attributen, die der TempDir-Helper nicht kennt → GitImpactMini-Override bleibt Pflicht.

### 3.2 `CompileErrorMiniFixtureWorkspace.cs` (gelesen, 71 Z.)

- `public sealed class CompileErrorMiniFixtureWorkspace : IDisposable` (Z. 10) → wird zu `: FixtureWorkspaceBase`.
- Konstruktor (Z. 12-17): 3 Zeilen Effektiv-Logik (FindSolutionRoot + Temp-Pfad + CopyFixture) — alles in den Basis-Konstruktor verschiebbar.
- `RootPath` (Z. 19) bleibt — wird von der Basis geerbt (selbe Signatur).
- `PathFor` (Z. 21) bleibt — spezifisch für CompileErrorMini.
- `Dispose` (Z. 23-29) löscht — wird von der Basis übernommen (kein Override nötig, CompileErrorMini hat keine Read-Only-Attribute zu clearen).
- 3 `private static`-Helper (Z. 31-70) — alle drei duplizieren wortgleich die Basis-Version. Komplett löschbar.
- Imports: `using System;` und implizit `using System.IO;` (für `Path`, `File`, `Directory`). Nach dem Refactor wird nur noch `using System;` und `using System.IO;` (für `Path.Combine` in `PathFor`) gebraucht — `using AiNetLinter.Tests.Fixtures;` (für `FixtureWorkspaceBase`) muss **hinzugefügt** werden (gleicher Namespace, also reicht das aktuelle `namespace AiNetLinter.Tests.Fixtures;` — kein expliziter `using` nötig, **Bonus**).

### 3.3 `GitImpactMiniFixtureWorkspace.cs` (gelesen, 166 Z.)

- `public sealed class GitImpactMiniFixtureWorkspace : IDisposable` (Z. 13) → wird zu `: FixtureWorkspaceBase`.
- Konstruktor (Z. 15-21): 5 Zeilen Effektiv-Logik (FindSolutionRoot + Temp-Pfad + CopyFixture + `InitializeGitRepoWithInitialCommit()`). Basis-Konstruktor übernimmt die ersten 3; `InitializeGitRepoWithInitialCommit()` bleibt im abgeleiteten Konstruktor (Hinter-`base(...)`-Aufruf).
- `RootPath` (Z. 23) wird von der Basis geerbt.
- `CalculatorPath` (Z. 25) bleibt — nutzt `RootPath` (jetzt von der Basis).
- `ChangeCalculatorAddBodyWithoutCommitting()` (Z. 31-38) bleibt — unverändert.
- `CommitCalculatorAddBodyChange()` (Z. 45-55) bleibt — unverändert.
- `Dispose` (Z. 57-64): `ClearReadOnlyAttributes(RootPath) + Directory.Delete(...)` — **kritisch**: das `ClearReadOnlyAttributes` MUSS vor dem Basis-`Dispose` laufen, sonst `UnauthorizedAccessException` beim Löschen der `.git`-Objekte. Wird zu `public override void Dispose() { ClearReadOnlyAttributes(RootPath); base.Dispose(); }`.
- `ClearReadOnlyAttributes` (Z. 71-79) bleibt — `private static`, ruft `File.SetAttributes` für alle Einträge.
- `InitializeGitRepoWithInitialCommit` (Z. 81-88) bleibt — nutzt `RunGit` mit `RootPath` (jetzt von der Basis).
- `RunGit` (Z. 90-124) bleibt — unverändert.
- 3 `private static`-Helper (Z. 126-165) — alle drei duplizieren wortgleich die Basis-Version. Komplett löschbar.
- Imports: aktuell `using System.Diagnostics; using System.Text;` (für `Process`/`StringBuilder` in `RunGit`). Nach Refactor: `using System;` und `using System.Diagnostics;` und `using System.IO;` und `using System.Text;` werden gebraucht (für `ProcessStartInfo`, `File`, `Path`, `StringBuilder`).

### 3.4 Bestehende Tests, die die 2 Fixtures benutzen (gezählt, NICHT modifiziert)

- `CompileErrorMiniFixtureWorkspace`: konsumiert in `McpServerCommandErrorHandlingTests.cs` (12 E2E-Tests, `Category=Integration`) — `McpCodeGraphServer` mit 3 BrokenClass-Files.
- `GitImpactMiniFixtureWorkspace`: konsumiert in `McpServerCommandGitImpactTests.cs` (mindestens 2 E2E-Tests für `get_impact` Git-Ref-Zweig, `Category=Integration`).
- Beide Test-Klassen sind nach 007 angelegt und verwenden die Fixtures in voller Funktionalität (Änderung der Datei, Commit, etc.) — A3 ist hier **automatisch** dadurch gesichert, dass die bestehenden Tests den Refactor auf Funktionalität verifizieren. Der zusätzliche Reflection-Test in Schritt 5 verifiziert die **strukturelle** Wirkung des Refactors (Vererbung + Helper-Entfernung), nicht die Funktionalität.

### 3.5 Konzept-Treue-Check (A7)

- `konzept.md` Z. 207-324 (P0/P1-Rest-Erweiterungen): nicht angefasst, A7-konform. Diese Einheit greift **keinen** P0/P1-Punkt auf — sie ist reine Test-Fixture-Hygiene außerhalb des Konzept-Scope (das Konzept erwähnt Fixtures nur implizit über die Test-Infrastruktur-Forderungen in Z. 104-107, 191-192, 624-629 — alle durch 006/007 erfüllt).
- `konzept.md` Z. 590-660 DoD: nicht berührt. Keine DoD-Punkte werden durch 009 erfüllt oder verletzt (TD-Schließungen sind orthogonal zum DoD).
- `konzept.md` Z. 316-324: Tool-vs-`rg`-Empfehlung (bereits in 008 umgesetzt) — irrelevant für 009.

### 3.6 Drift / Duplikate durch Blindheit

- **Drift**: keine — der `FixtureWorkspaceBase` existiert seit Commit `6c872e4` unverändert, ist die strukturelle Vorlage. Es gibt keine konkurrierende zweite „gemeinsame Fixture-Basis" woanders im Repo (verifiziert via Konzept-Spot-Check, der die 4 Workspace-Klassen explizit nennt). Die Migration ist die einzig mögliche Lösung.
- **Duplikate durch Blindheit**: keine — der Refactor nutzt die existierende Basis, baut keine neue Struktur.

### 3.7 Projektregeln-Check (A7, A8)

- `AiNetLinter.mdc` (`MaxMethodLineCount` 60, `MaxLineCount` 500, `EnforceSealedClasses`): beide Klassen sind `sealed` (unverändert); Methoden bleiben unter 60 Z. CompileErrorMini nach Refactor: ~25 Z. (Konstruktor + PathFor + Klasse), GitImpactMini nach Refactor: ~120 Z. (allein `RunGit` ist 35 Z., der Rest ist kleiner). Beide unter 500 Z. mit deutlichem Puffer.
- `AiNetLinterRichtlinien.mdc` §1 (Einfachheit, keine zweite Struktur): genau das wird umgesetzt — bestehende Basis wiederverwendet statt neue.

## 4. Betroffene Dateien / Module

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs` | **ja** | 71 Z. → ~25 Z. (ca. -65 %) |
| `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` | **ja** | 166 Z. → ~125 Z. (ca. -25 %, `RunGit` 35 Z. + `ClearReadOnlyAttributes` 9 Z. + ctor-Override etc. bleiben) |
| `src/AiNetLinter.Tests/Fixtures/FixtureWorkspaceBase.cs` | nein (unverändert) | 0 Z. |
| `src/AiNetLinter.Tests/Fixtures/TestTempDirectory.cs` | nein (unverändert) | 0 Z. |
| `src/AiNetLinter.Tests/Fixtures/TD016aRefactorTests.cs` (NEU) | optional (empfohlen, A3-Sicherung) | ~30-50 Z., 1-2 Reflection-Tests |
| `tasks/codegraph-mcp-server/tech-debt.md` (TD-016a-Eintrag + Index) | **ja** | Status „geschlossen" + Body-Block analog TD-012/013/015 |
| `tasks/codegraph-mcp-server/units/009/result.md` (NEU) | **ja** (vom Coder) | Standard-Result-Protokoll mit A3-Block |
| `tasks/codegraph-mcp-server/state.md` | optional (Orchestrator-Sache) | 1 Block analog 004/005/007 in „Phase 2 — Loop-Protokoll" + Zähler-Update (1× Planer + 1× Coder + 1× Kritiker = 30/40 nach 009) |

**Keine** Änderungen an: `src/AiNetLinter/**` (Produktionscode), `Mcp/`-Modul, `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json`, `Docs/**`, `README.md`, `AiNetLinter.csproj`.

## 5. Konkretes Vorgehen (Schritt-für-Schritt, Coder hat keinen Planungsspielraum)

### Schritt 1 — `CompileErrorMiniFixtureWorkspace.cs` umstellen (~10 min Coder-Aufwand)

**Datei:** `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs`

**Vorgehen:**

1. Klassenkopf: `public sealed class CompileErrorMiniFixtureWorkspace : IDisposable` → `public sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspaceBase`
2. Konstruktor komplett ersetzen:
   ```csharp
   public CompileErrorMiniFixtureWorkspace()
       : base("CompileErrorMini", "ainetlinter-compile-error-mini")
   {
   }
   ```
3. `Dispose`-Methode (Z. 23-29) **komplett löschen** — `FixtureWorkspaceBase` erledigt das.
4. `RootPath`-Property (Z. 19) **löschen** — wird von der Basis geerbt (selber Name, selbe Sichtbarkeit `public`).
5. `PathFor` (Z. 21) **behalten** — tests-spezifisch, ruft jetzt das geerbte `RootPath` (Compiler-auflösbar, kein `this.` nötig).
6. Die drei `private static`-Helper `CopyFixture` (Z. 31-47), `IsGeneratedPath` (Z. 49-54), `FindSolutionRoot` (Z. 56-70) **komplett löschen**.
7. Keine `using`-Änderungen nötig (gleicher Namespace `AiNetLinter.Tests.Fixtures`).
8. Datei-Endform: 3 Properties/Konstruktoren, ~25 Z., mit `summary`-XML-Doc für die Klasse.

**Erwartete Endform (illustrativ, nicht wortwörtlich zu kopieren):**

```csharp
namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des CompileErrorMini-Fixtures fuer EPIC-06-Tests: enthaelt 3 intakte C#-
/// Klassen (ValidClassA/B/C) und 3 Klassen mit absichtlichen Compile-Fehlern (BrokenClassA/B/C).
/// MSBuildWorkspace laedt das Projekt vollstaendig (auch mit Syntax-/Semantik-Fehlern) und meldet
/// die Fehler ueber <c>Compilation.GetDiagnostics()</c> — auf dem die 006-Warnhinweis-Pfade in
/// den 9 MCP-Tools aufsetzen.
/// </summary>
public sealed class CompileErrorMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public CompileErrorMiniFixtureWorkspace()
        : base("CompileErrorMini", "ainetlinter-compile-error-mini")
    {
    }

    public string PathFor(string fileName) => Path.Combine(RootPath, "src", "CompileErrorMini", fileName);
}
```

### Schritt 2 — `GitImpactMiniFixtureWorkspace.cs` umstellen (~25 min Coder-Aufwand, inkl. Dispose-Override)

**Datei:** `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs`

**Vorgehen:**

1. Klassenkopf: `public sealed class GitImpactMiniFixtureWorkspace : IDisposable` → `public sealed class GitImpactMiniFixtureWorkspace : FixtureWorkspaceBase`
2. Konstruktor komplett ersetzen:
   ```csharp
   public GitImpactMiniFixtureWorkspace()
       : base("GitImpactMini", "ainetlinter-gitimpact-mini")
   {
       InitializeGitRepoWithInitialCommit();
   }
   ```
   `InitializeGitRepoWithInitialCommit()` läuft **nach** dem Basis-Konstruktor (Basis-ctor setzt `RootPath`), also funktioniert es korrekt mit dem geerbten `RootPath` (siehe `RunGit(... WorkingDirectory = RootPath ...)` in Z. 96).
3. `RootPath`-Property (Z. 23) **löschen** — wird von der Basis geerbt.
4. `CalculatorPath` (Z. 25) **behalten** — nutzt das geerbte `RootPath` (Compiler-auflösbar).
5. `ChangeCalculatorAddBodyWithoutCommitting()` (Z. 31-38) **behalten** — nutzt das geerbte `RootPath` über `CalculatorPath` (kein direkter Zugriff).
6. `CommitCalculatorAddBodyChange()` (Z. 45-55) **behalten** — nutzt `RunGit` mit `RootPath` (jetzt geerbt).
7. `Dispose` (Z. 57-64) **ersetzen** durch Override:
   ```csharp
   public override void Dispose()
   {
       ClearReadOnlyAttributes(RootPath);
       base.Dispose();
   }
   ```
   **Reihenfolge kritisch**: `ClearReadOnlyAttributes` MUSS vor `base.Dispose()` laufen, sonst `UnauthorizedAccessException` beim Löschen der `.git`-Objekte (gleiche Reihenfolge wie aktuell Z. 61-62). `RootPath` ist jetzt das geerbte Property der Basis.
8. `ClearReadOnlyAttributes` (Z. 71-79) **behalten** — `private static`, unverändert.
9. `InitializeGitRepoWithInitialCommit` (Z. 81-88) **behalten** — nutzt `RunGit` mit `RootPath` (jetzt geerbt).
10. `RunGit` (Z. 90-124) **behalten** — unverändert (außer dass es jetzt das geerbte `RootPath` nutzt; aktuell Z. 96 referenziert es bereits als `RootPath`, was im abgeleiteten Kontext das geerbte Property auflöst — kein Edit).
11. Die drei `private static`-Helper `CopyFixture` (Z. 126-142), `IsGeneratedPath` (Z. 144-149), `FindSolutionRoot` (Z. 151-165) **komplett löschen**.
12. `using`-Direktiven: aktuell `using System.Diagnostics; using System.Text;`. Nach Refactor: `using System;` (für `InvalidOperationException`, `ObjectDisposedException` falls nötig) **muss hinzugefügt werden** (für `File.SetAttributes`? Nein, das ist `System.IO`. Korrekt: `using System;` für `Exception`/`Guid`/`StringComparison.OrdinalIgnoreCase`? `StringComparison` ist in `System`, `Guid` in `System`. Tatsächlich ist im aktuellen Code `Guid` nirgendwo direkt verwendet — `Guid.NewGuid()` wird im Basis-Konstruktor aufgerufen, also nicht hier. `InvalidOperationException` in `RunGit` (Z. 107, 122) ist in `System`. Also `using System;` ist nötig. `using System.Diagnostics;` (für `Process`/`ProcessStartInfo`) bleibt. `using System.Text;` (für `StringBuilder`) bleibt. `using System.IO;` ist **nicht** explizit da, aber `Path.Combine`/`File.ReadAllText`/`File.WriteAllText`/`File.SetAttributes`/`Directory.EnumerateFileSystemEntries` werden benutzt — die werden über `global using` oder implizit aufgelöst? **Vorsicht**: das aktuelle File hat **kein** `using System.IO;` — schauen wir nochmal: Z. 1-3 zeigt `using System.Diagnostics; using System.Text;`. **Aha — das ist ein Pre-Existing-Bug**: aktuell kompiliert das File nur, weil es transitiv über `System.Diagnostics.Process.Start` (das `System.IO` für WorkingDirectory mitbringt?) **nicht** — `ProcessStartInfo` braucht `System.Diagnostics` (für `ProcessStartInfo`), `WorkingDirectory` ist `string`. `File.ReadAllText`/`File.WriteAllText`/`File.SetAttributes` brauchen `using System.IO;`. Möglich, dass der Compiler das über die impliziten `using`-Direktiven der `.csproj` (`<ImplicitUsings>enable</ImplicitUsings>`) auflöst. **Vermutung**: ja, AiNetLinter hat ImplicitUsings aktiv. **Coder verifiziert im Build**, dass keine zusätzlichen `using`-Direktiven nötig sind. Falls doch: nur das Nötigste hinzufügen, kein „aufgeräumter Import-Block".

**Erwartete Endform (illustrativ):**

```csharp
using System.Diagnostics;
using System.Text;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Isolierte Temp-Kopie des GitImpactMini-Fixtures mit einem echten, lokal initialisierten
/// Git-Repository ... (unverändert)
/// </summary>
public sealed class GitImpactMiniFixtureWorkspace : FixtureWorkspaceBase
{
    public GitImpactMiniFixtureWorkspace()
        : base("GitImpactMini", "ainetlinter-gitimpact-mini")
    {
        InitializeGitRepoWithInitialCommit();
    }

    public string CalculatorPath => Path.Combine(RootPath, "src", "GitImpactMini", "Calculator.cs");

    public void ChangeCalculatorAddBodyWithoutCommitting() { /* unverändert */ }

    public void CommitCalculatorAddBodyChange() { /* unverändert */ }

    public override void Dispose()
    {
        ClearReadOnlyAttributes(RootPath);
        base.Dispose();
    }

    private static void ClearReadOnlyAttributes(string rootPath) { /* unverändert */ }

    private void InitializeGitRepoWithInitialCommit() { /* unverändert */ }

    private void RunGit(string arguments) { /* unverändert */ }
}
```

### Schritt 3 — `tech-debt.md` aktualisieren (analog TD-012/013/015-Schließung aus 004/007)

**Datei:** `tasks/codegraph-mcp-server/tech-debt.md`

**Vorgehen:**

1. **Index-Zeile TD-016a** (Z. 46): Status-Block am Ende der Zeile von `offen` auf `geschlossen` ändern. Konkret:
   - **Alt:** `| TD-016a | src/AiNetLinter.Tests/Fixtures/{CompileErrorMini,GitImpactMini}FixtureWorkspace.cs | niedrig | Folge-Refactor aus TD-016: ... |`
   - **Neu:** gleiche Zeile, aber am Ende `| **geschlossen durch 009** |` (Status-Spalte gibt es nicht im Index, der Status wird im Body gehalten — siehe Schritt 2).
2. **Body TD-016a** (Z. 164-168): den `**Status:** offen` durch einen **Status-Block in der Form der bereits geschlossenen TD-003/012/013/015/016** ersetzen:
   ```markdown
   - **Status:** **geschlossen** durch Einheit 009 (Commit(s) aus 009/result.md):
     - `CompileErrorMiniFixtureWorkspace` von 71 auf ~25 Z. geschrumpft (Konstruktor delegiert
       an `FixtureWorkspaceBase`, duplizierte `CopyFixture`/`IsGeneratedPath`/`FindSolutionRoot`
       entfernt, `Dispose` von der Basis geerbt).
     - `GitImpactMiniFixtureWorkspace` von 166 auf ~125 Z. geschrumpft (Konstruktor delegiert
       an `FixtureWorkspaceBase`, `InitializeGitRepoWithInitialCommit()` als
       Post-Basis-Aktion, `Dispose` als `override` mit `ClearReadOnlyAttributes(RootPath)`
       vor `base.Dispose()`, duplizierte Helper entfernt).
     - `RunGit` und `ClearReadOnlyAttributes` bleiben in der abgeleiteten Klasse (sind
       GitImpactMini-spezifisch, nicht generisch).
     - Bestehende Tests grün (A3-Sicherung über die schon vorhandenen E2E-Tests in
       `McpServerCommandErrorHandlingTests.cs` und `McpServerCommandGitImpactTests.cs`).
     - Optional: `TD016aRefactorTests.cs` mit Reflection-Assertion auf die Vererbung +
       private-static-Entfernung (strukturelle A3-Sicherung gegen zukünftige Re-Drift).
   ```
3. **Frontmatter** (Z. 5): `last_updated: 2026-08-02 (TD-003 geschlossen durch 007, TD-016a neu aus 007-Review)` → `last_updated: 2026-08-02 (TD-016a geschlossen durch 009)`.
4. Keine weiteren Edits — `index`-Tabelle oben bleibt sonst unverändert.

### Schritt 4 — `units/009/result.md` schreiben (vom Coder)

**Standard-Result-Format** analog `units/002/result.md` / `units/007/result.md`:

- Summary (1-2 Absätze): was wurde refaktoriert, warum, was war die Stolperfalle
- **What changed** Tabelle: Diff-Größe pro Datei
- **Commit-Hashes** in der Reihenfolge: 1. Refactor-Commit (CompileErrorMini + GitImpactMini in einem), 2. optional Test-Commit (Reflection-A3), 3. `tech-debt.md`-Commit (TD-016a geschlossen), 4. `result.md`-Commit
- **A3-Nachweis pro neuem Test** (falls Reflection-Test in Schritt 5 angenommen):
  - **Build grün** vor Refactor (3. Stand-Fixtures)
  - **Test rot** nach Refactor ohne die Vererbung (z. B. wieder auf `: IDisposable` umstellen, dabei `CopyFixture` wieder zurück-kopieren)
  - **Test grün** nach Refactor mit Vererbung
- **Build-Verifikation** (Build 0/0, gezielter E2E-Slice grün, **kein Volllauf** in 009 — Begründung: 009 ändert nur Test-Fixture-Klassen, keine Produktion, keine Tool-Logik, keine Doku. Der nächste **echte** Code-Change (010) braucht dann wieder den Volllauf. Siehe AGENTS.md §2 wörtlich: „Volllauf nur für finale Verifikation" — eine reine Refactor-Einheit mit A3 über bestehende Tests **ist** die „finale Verifikation" für genau diese Änderung. ABER: zur **Sicherheit** und weil der User in 008 explizit auf den Volllauf Wert legt, wird in 009 **trotzdem** der Volllauf gefahren, dokumentiert mit Begründung. **Konkret im Coder-Plan**: 1. `dotnet build` 2. E2E-Slice `McpServerCommandErrorHandlingTests` + `McpServerCommandGitImpactTests` 3. **Volllauf `dotnet test AiNetLinter.slnx --no-build`** (1165+0/1165+0 grün, falls kein Reflection-Test; 1165+1/1165+1 grün, falls Reflection-Test angenommen). Begründung der 4. Schritt-Volllauf-Pflicht: gleiche Logik wie 008/fix-01, „bevor die Einheit als abgeschlossen gilt".)
- **Self-Lint**: nicht relevant (Test-Fixture-Dateien, nicht in der Lint-Standard-Suite)
- **Tech-Debt**: TD-016a geschlossen — auf den Body-Block verweisen

### Schritt 5 — Optionaler Reflection-Test in `TD016aRefactorTests.cs` (empfohlen, A3-strukturell)

**Strategie:** 1-2 Reflection-Tests, die **strukturell** verifizieren, dass die
beiden refaktorierten Klassen jetzt von `FixtureWorkspaceBase` erben und
keine eigenen `private static`-Methoden namens `CopyFixture`/`IsGeneratedPath`/
`FindSolutionRoot` mehr haben. Robust gegen versehentliche Re-Drift (jemand
kopiert die Helper wieder zurück, weil eine neue Anforderung das nahelegt).

**Neue Datei:** `src/AiNetLinter.Tests/Fixtures/TD016aRefactorTests.cs` (~30-50 Z.)

**Vorgeschlagene Test-Methoden (1-2, je nach Geschmack des Coders):**

```csharp
using System.Linq;
using System.Reflection;
using Xunit;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Strukturelle A3-Sicherung fuer TD-016a: verifiziert, dass die 2 ehemals
/// duplizierten Workspace-Klassen tatsaechlich von <see cref="FixtureWorkspaceBase"/>
/// erben und keine eigenen <c>CopyFixture</c>/<c>IsGeneratedPath</c>/<c>FindSolutionRoot</c>-
/// Helper mehr definieren. Verhindert, dass die Refactor-Wirkung versehentlich
/// rueckgaengig gemacht wird.
/// </summary>
public sealed class TD016aRefactorTests
{
    [Theory]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace))]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace))]
    public void Workspace_InheritsFromFixtureWorkspaceBase(System.Type workspaceType)
    {
        Assert.True(
            typeof(FixtureWorkspaceBase).IsAssignableFrom(workspaceType),
            $"{workspaceType.Name} erbt nicht von FixtureWorkspaceBase — TD-016a-Regression.");
    }

    [Theory]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "CopyFixture")]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "IsGeneratedPath")]
    [InlineData(typeof(CompileErrorMiniFixtureWorkspace), "FindSolutionRoot")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "CopyFixture")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "IsGeneratedPath")]
    [InlineData(typeof(GitImpactMiniFixtureWorkspace), "FindSolutionRoot")]
    public void Workspace_DoesNotDefineDuplicatedHelper(System.Type workspaceType, string helperName)
    {
        var hasOwnDefinition = workspaceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Any(m => m.Name == helperName && m.DeclaringType == workspaceType);

        Assert.False(
            hasOwnDefinition,
            $"{workspaceType.Name} definiert immer noch eine eigene {helperName}-Methode " +
            $"\u2014 TD-016a-Regression, Fixture-Helper dupliziert statt aus FixtureWorkspaceBase geerbt.");
    }
}
```

**Kategorie/Trait/Collection:**
- `[Trait("Category", "Unit")]` — keine Subprozesse, schnelle Reflection-Tests, <1 s.
- **Keine** `[Collection(...)]` nötig (kein Console-/Solution-Load).
- **Keine** `IClassFixture<...>` nötig.
- Datei `sealed` + `MaxLineCount: 500` mit deutlichem Puffer.

**A3-Methodik für die 2 Tests (Coder-Operationalisierung):**

1. **Baseline** (vor Refactor): Tests existieren noch nicht, sind also „nicht anwendbar". Der Coder baut den Test **zusammen mit** dem Refactor.
2. **Erstlauf** (nach Refactor + Test): `dotnet test --filter "FullyQualifiedName~TD016aRefactorTests"` → 2/2 grün.
3. **A3-Auslöser für Test 1** (Vererbungs-Test): temporär `GitImpactMiniFixtureWorkspace` wieder auf `: IDisposable` zurückstellen, Test-Datei unverändert.
4. **A3-Lauf Test 1**: `dotnet test --filter "FullyQualifiedName~Workspace_InheritsFromFixtureWorkspaceBase"` → **1 von 2 Assertions rot** (CompileErrorMini-Test grün, GitImpactMini-Test rot). Failure-Output: `Workspace_InheritsFromFixtureWorkspaceBase(workspaceType: typeof(GitImpactMiniFixtureWorkspace))` / `Assert.True() Failure: ... erbt nicht von FixtureWorkspaceBase`.
5. **A3-Auslöser für Test 2** (Helper-Entfernungs-Test): temporär in `CompileErrorMiniFixtureWorkspace` eine `private static void CopyFixture(...)` wieder einfügen (aus dem Basis-Code kopiert).
6. **A3-Lauf Test 2**: → **1 von 6 Assertions rot** (`Workspace_DoesNotDefineDuplicatedHelper(CompileErrorMiniFixtureWorkspace, "CopyFixture")` rot). Failure: `CompileErrorMiniFixtureWorkspace definiert immer noch eine eigene CopyFixture-Methode`.
7. **A3-Rückgängig**: Schritt 3 + Schritt 5 rückgängig machen. Tests grün.

**Vom Coder wortwörtlich im `result.md` zu protokollieren** mit den exakten Failure-Strings.

### Schritt 6 — `state.md` Block ergänzen (Orchestrator-Aufgabe, **nicht** Coder-Aufgabe)

Im Loop-Protokoll-Block analog zu Einheit 004/005/007/008. Wird vom Orchestrator nach dem Kritiker-`approved` ergänzt, **nicht** vom Coder (siehe `state.md:127-141` Konvention). Coder dokumentiert im `result.md` nur, dass `state.md` aktualisiert werden muss.

## 6. Erwartete Tests + A3-Methodik

### 6.1 Bestehende Tests (automatische A3-Sicherung)

Folgende Test-Klassen bleiben unverändert und müssen nach dem Refactor weiterhin grün laufen — das **ist** die A3-Sicherung für die funktionale Wirkung des Refactors:

| Test-Klasse | Fixture | Erwartetes Verhalten | Test-Kategorie |
|---|---|---|---|
| `McpServerCommandErrorHandlingTests.cs` (~12 Tests) | `CompileErrorMiniFixtureWorkspace` | Server startet, liefert Compile-Fehler-Warnhinweise in Tool-Responses, kein Crash | Integration |
| `McpServerCommandGitImpactTests.cs` (~2-3 Tests) | `GitImpactMiniFixtureWorkspace` | `get_impact` mit Git-Ref liefert korrekte Call-Sites gegen HEAD~1 | Integration |

**A3 hier**: wenn der Refactor das Fixture-Verhalten bricht (z. B. weil `ClearReadOnlyAttributes` nicht mehr greift und das Temp-Verzeichnis-Löschen in `Dispose` scheitert, oder `RootPath` nicht korrekt aufgelöst wird), werden diese Tests **rot**. **Genau das ist der A3-Pfad** — keine zusätzliche Mechanik nötig.

### 6.2 Neue Tests (optional, empfohlen — Schritt 5)

Siehe Schritt 5 oben: 2 Reflection-Tests in `TD016aRefactorTests.cs` (1 Theory mit 2 InlineData für die Vererbung, 1 Theory mit 6 InlineData für die Helper-Entfernung). **Unit**-Kategorie, <1 s, keine externen Abhängigkeiten.

**A3-Methodik**: siehe Schritt 5.7 oben (2 A3-Auslöser-Sequenzen für je 1 Test).

### 6.3 Pflicht-Verifikation Volllauf (für AGENTS.md §2)

- **Gezielter Slice** (schnell, während der Entwicklung): `dotnet test --filter "FullyQualifiedName~McpServerCommandErrorHandlingTests|FullyQualifiedName~McpServerCommandGitImpactTests"`
- **Volllauf** (vor Task-Beendigung): `dotnet test AiNetLinter.slnx --no-build`
  - Erwartung: 1165/1165 grün (vor 009) → 1165/1165 grün (nach 009 ohne Reflection-Test) oder 1166/1166 (nach 009 mit Reflection-Test).
  - Dauer: ~5-7 min (alle E2E-Tests parallel, Multi-Core aus 007/008-Erfahrung).
  - **Begründung der Volllauf-Pflicht**: AGENTS.md §2 wörtlich („Vor dem Beenden eines Tasks MUSS ein vollständiger Testlauf grün durchgeführt werden"). 009 ist eine **eigenständige Code-Änderung** (auch wenn nur Test-Fixtures), also gilt die Pflicht. Die A3-Sicherung über die bestehenden Tests ist gut, aber nicht so umfassend wie ein Volllauf — eine unentdeckte Regress in einer **anderen** Test-Klasse (die zufällig eine der 2 Fixtures anders benutzt, als ich beim Spot-Check gesehen habe) wäre möglich. Volllauf ist die Versicherung, dass die existierende 1165er-Suite strukturell intakt bleibt.

## 7. Plan-Abweichungen, die explizit erlaubt sind

- **Erlaubt:** Der Coder darf entscheiden, ob er den Reflection-Test aus Schritt 5 annimmt oder nicht. Wenn nicht, entfällt die neue Test-Datei und die Commit-Liste schrumpft auf 3 Commits (Refactor + tech-debt.md + result.md) statt 4. Beide Varianten sind im Plan vorbereitet. **Empfehlung des Planers: ja, weil strukturelle A3-Sicherung gegen Re-Drift einen echten Wert hat** (siehe TD-016 selbst: der initiale Refactor hat nur 2 von 4 Klassen migriert, weil es keine strukturelle Sicherung gab).
- **Erlaubt:** Der Coder darf die Reihenfolge der 4 Commits (oder 3 ohne Reflection-Test) anpassen, solange die Conventional-Commits-Konvention + `[codegraph-mcp-server]`-Suffix eingehalten wird und kein Push/Amend/-A passiert (A4).
- **Erlaubt:** Der Coder darf den `private static ClearReadOnlyAttributes(string rootPath)` in GitImpactMini bestehen lassen (nicht in die Basis heben) — Begründung: Read-Only-Attribut-Clear ist **spezifisch** für Git-Repos (die `.git`-Objekte sind schreibgeschützt), nicht generisch. Eine Verschiebung in `FixtureWorkspaceBase` würde bedeuten, dass **alle** Fixtures (auch CompileErrorMini, das nichts mit Git zu tun hat) den Clear ausführen — unnötige Arbeit, potenziell neue Bugs. **Klassischer Fall für A5-„Fertig-ist-fertig": die Helper bleiben, wo sie gebraucht werden.**
- **Erlaubt:** Falls beim Build ein `using System.IO;` o. ä. fehlt (siehe Schritt 2 Punkt 12), fügt der Coder **nur die nötigsten** Imports hinzu — kein Aufräumen des Import-Blocks (A5).
- **Nicht erlaubt:** Kein Edit an `konzept.md`, `kernel.md`, Rollen-Dateien, `.agents/rules/**`, `rules.json`, `Docs/**`, `README.md`, `AiNetLinter.csproj`, `src/AiNetLinter/**`-Produktionscode, `Mcp/`-Modul (A7, A8).
- **Nicht erlaubt:** Kein `git add -A` / `.` (A4), kein Push, kein Amend, kein History-Rewrite.
- **Nicht erlaubt:** Keine kosmetischen Edits an den 2 refaktorierten Klassen (Kommentar-Umschreibung, Reformat, „schöneres" Layout) — A5.

## 8. Bezug zu Projektregeln (minimal-invasiv, aber explizit)

- **`AiNetLinter.mdc`** (`MaxMethodLineCount: 60`, `MaxLineCount: 500`, `EnforceSealedClasses`): beide refaktorierten Klassen bleiben `sealed` (unverändert); alle Methoden unter 60 Z. (Konstruktoren 1-3 Z., `PathFor` 1 Z., `CalculatorPath` 1 Z., `Dispose` 4 Z., `RunGit` 35 Z., `ClearReadOnlyAttributes` 9 Z.); beide Klassen deutlich unter 500 Z. (CompileErrorMini ~25, GitImpactMini ~125).
- **`AiNetLinterRichtlinien.mdc` §1 (Einfachheit, monolithisch, keine zweite Struktur)**: Refactor nutzt die existierende Basis statt eine neue, dritte „Helper-Klasse" zu bauen. Im Gegenteil: er **reduziert** die existierende Duplikation — also maximale Einfachheit.
- **`AiNetLinterRichtlinien.mdc` §2 (kein DI-Container)**: irrelevant, Refactor ändert keine Service-Konstruktion.
- **`AiNetLinterRichtlinien.mdc` §4 (MCP-Dogfooding via C#-Test-Infrastruktur)**: irrelevant, kein MCP-Code angefasst.
- **`AiNetLinterRichtlinien.mdc` §5 (Result-Pattern, kein `throw`)**: nur indirekt relevant — GitImpactMini.RunGit wirft `InvalidOperationException` bei Git-Fehler (Z. 107, 122), bleibt unverändert. CompileErrorMini-Fixture wirft nirgendwo. Fixture-Klassen sind Test-Code, nicht Produktion.
- **`kernel.md` A3 (Tests müssen fehlschlagen können)**: durch die 2 Mechanismen (bestehende Tests in 6.1 + optionale Reflection-Tests in 6.2 / Schritt 5) **doppelt** gesichert. A3-Pfade im `result.md` wortwörtlich zu protokollieren.
- **`kernel.md` A4 (kein Push, kein Amend, gezielter `git add`)**: siehe Schritt 5/6 Commit-Konvention unten.
- **`kernel.md` A5 (Fertig ist fertig)**: nur die 3 Helper löschen, `Dispose` korrekt überschreiben, keine kosmetischen Edits. `RunGit` und `ClearReadOnlyAttributes` in GitImpactMini bleiben.
- **`kernel.md` A7 (kein Konzept-Edit)**: `konzept.md` wird nicht angefasst. Die 3 Konzept-Diskrepanzen aus 008 sind User-pflichtig und gehören in eine separate Konzept-Pflege-Einheit (nicht 009).
- **`kernel.md` A8 (Kernel und Rollen unantastbar)**: nicht angefasst.

## 9. Tech-Debt-Aktionen

### 9.1 Schließungen

- **TD-016a** wird in `tech-debt.md` auf **`geschlossen durch Einheit 009`** gesetzt (siehe Schritt 3 oben). Body-Block in der Form der bereits geschlossenen TD-003/012/013/015/016. Index-Zeile TD-016a bleibt im Index, der `**Status:**`-Marker wandert vom Body in den Status-Block.

### 9.2 Neue Einträge

- **Keine neuen TD-Einträge.** Begründung: der Refactor ist die Schließung selbst, kein Befund außerhalb des Scopes (analog TD-016-Teilschluss-Anmerkung). Wenn der Kritiker in der Review **neue** Befunde macht (z. B. „die 4. `private static ClearReadOnlyAttributes` in GitImpactMini könnte man auch noch woanders hin verschieben"), werden die als TD-Vorschläge in `review.md` vermerkt — Übernahme in `tech-debt.md` wie immer durch den Orchestrator nach `approved`, nicht durch den Coder.

### 9.3 Stand nach 009

- **Offene TD-Einträge nach 009:** TD-001, TD-002, TD-004, TD-005, TD-006, TD-007, TD-008, TD-009, TD-010, TD-011, TD-014. (TD-003, TD-012, TD-013, TD-015, TD-016, TD-016a geschlossen.)
- **Keine TD-Verschärfung** durch 009 (es werden keine Footprints geändert, keine Tool-Klassen angefasst, keine Registrar-Klassen modifiziert).

## 10. Risiken + Bewusst-NICHT-in-009

### 10.1 Risiken (mit Mitigation)

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|---|---|---|
| `ClearReadOnlyAttributes` in GitImpactMini wird vergessen, `base.Dispose()` löscht Verzeichnis mit Read-Only-`.git`-Objekten → `UnauthorizedAccessException` → Tests rot | **niedrig** (klar im Plan adressiert, Schritt 2.7) | **mittel** (ganze Test-Klasse rot, sofort sichtbar) | Schritt 2.7 ist explizit; A3-Pfad über bestehende Tests fängt es sofort |
| `RootPath` löst im abgeleiteten Kontext nicht auf das geerbte Property auf (z. B. weil der Compiler `this.RootPath` als `private` interpretiert) | **sehr niedrig** (`public` Property, einfacher Name, keine Verwechslung möglich) | **mittel** (Compile-Fehler in GitImpactMini) | Build schlägt fehl, sofort sichtbar; `using`/`namespace` sind identisch zur Basis |
| Bestehende Tests (`McpServerCommandErrorHandlingTests`, `McpServerCommandGitImpactTests`) vergessen, ein Fixture-Feld direkt statt über `RootPath` zu nutzen → Felder weg | **sehr niedrig** (Tests wurden in 006/007 angelegt, sind `IClassFixture`-basiert, nutzen nur die `RootPath`/`CalculatorPath`-Properties — kein direkter Zugriff auf interne Helper) | **niedrig** (Test kompiliert nicht) | Build schlägt fehl |
| CompileErrorMini-`PathFor(fileName)` löst `RootPath` nicht korrekt auf (Namespace-Konflikt mit `System.IO.Path.RootPath` o. ä. — gibt's nicht wirklich, aber als Denk-Anker) | **niedrig** (C#-Auflösung ist deterministisch) | **mittel** (Test rot) | Build schlägt fehl |
| Volllauf zeigt Regress in einer **unentdeckten** Test-Klasse, die eine der 2 Fixtures benutzt (nicht in meinem Spot-Check) | **sehr niedrig** (Suche via `rg "CompileErrorMiniFixtureWorkspace|GitImpactMiniFixtureWorkspace"` ist im Coder-Plan drin, Schritt 0 vor dem Refactor) | **niedrig** bis **mittel** | Volllauf fängt es; in dem Fall Fix in derselben Einheit, **kein** neuer `units/009/fix-01/` nötig, weil die Regress ja durch den Refactor selbst verursacht ist (kein Reviewer-Fehler) |

### 10.2 Bewusst NICHT in 009

- **Konzept-Diskrepanzen aus 008** (Z. 539-552, 550, 564) — explizit User-pflichtig (A7), gehören in eine separate Konzept-Pflege-Einheit, nicht in 009.
- **TD-016 selbst** (Initial-Refactor 2 von 4 Klassen, Commit `6c872e4`) — bereits geschlossen in 007 mit Teilschluss-Anmerkung; nur TD-016a ist die Folge.
- **TD-006** (Datei-Scan-Duplikation `GetIndexScopeScanner` vs. `WebFileCatalog`) — offen, aber anderes Modul, nicht Test-Fixture.
- **TD-008/TD-010** (`ILinterEngineConfig`-Refactor für `PathOverrides`-Pragmatik) — strukturell wertvoll, aber 4-6 h und nur sinnvoll, wenn die nächste P0/P1-Erweiterung `McpCodeGraphServer` sowieso anfasst (A1 oder A4). Gehört in 010/011 als Inline-Refactor, nicht als eigenständige Einheit.
- **TD-009** (McpCodeGraphServer-Konstruktor auf `record` umstellen) — siehe oben, gehört in 010/011 als Inline-Refactor vor A1 oder A4.
- **TD-014** (McpServerOptionsBuilder) — siehe oben, gehört in 010/011 vor `--mcp-log` (A5).
- **P0/P1-Rest-Erweiterungen A1-A7** — siehe Abschnitt 2, jede bekommt ihre eigene Einheit.
- **`get_symbol_body` + stabile Symbol-IDs** (P2-Backlog aus `tasks/codegraph-mcp-next/Konzept.md`) — explizit außerhalb dieses Tasks (`konzept.md` Z. 335-337), kein Scope.
- **Aktualisierung der `state.md` Zähler-Tabelle** und der „Phase 2 — Loop-Protokoll"-Block — Orchestrator-Aufgabe nach Kritiker-`approved`, nicht Coder-Aufgabe.
- **Push der Commits nach `origin/main`** — Orchestrator-Aufgabe nach `approved`, nicht Coder-Aufgabe (A4).

## 11. Synergien mit Folge-Einheiten (was muss in 010/011 mitgenommen werden, was kann warten)

### 11.1 Was 009 freischaltet / erleichtert

- **TD-016-Block ist jetzt vollständig abgeschlossen** (TD-016 + TD-016a). In zukünftigen Tech-Debt-Übersichten tauchen die 4 Workspace-Klassen nicht mehr als redundante Helper-Träger auf — das vereinfacht zukünftige Refactor-Planungen in der Test-Fixture-Schicht (z. B. wenn Last-Fixture-Generierung aus A7 weitere Fixture-Klassen braucht, ist das Muster `FixtureWorkspaceBase` → spezifischer Konstruktor + ggf. `Dispose`-Override jetzt sauber etabliert).
- **Coder hat das TD-016-Pattern einmal erfolgreich angewendet** — bei zukünftigen ähnlichen Refactors (z. B. wenn ein TD-007-`record`-Refactor für `TryApplyContentChange` ansteht, oder eine neue Tool-Klasse analog zum 4. Registrar gebraucht wird) ist das Pattern bekannt und der nächste Refactor kostet weniger Aufwand.
- **Test-Infrastruktur (`McpServerCommandErrorHandlingTests` + `McpServerCommandGitImpactTests`) bleibt sauber** — kein impliziter Wartungs-Drift durch verlorene `ClearReadOnlyAttributes`-Logik o. ä. (die ist jetzt explizit in GitImpactMini-`Dispose`-Override).

### 11.2 Was 010/011 selbst mitnehmen müssen (kein 009-Aufhänger, aber Kontext)

- **A1 (rules.json Auto-Discovery) als 010** — sollte in 010 mitgeplant werden: `McpServerCommand.ResolveConfig` (Z. 72-79) muss erweitert werden, ohne `McpCodeGraphServer` zu berühren (oder den TD-009-`record`-Refactor inline mitnehmen, wenn die Konstruktor-Parameterzahl reißt). Neue Test-Klasse analog `McpServerCommandRulesJsonAutoDiscoveryTests.cs` mit `IClassFixture<SymbolGraphMcpFixture>` — Test 1: `rules.json` neben der Solution wird gefunden (Verzeichnis-Layout: `SymbolGraphMini` braucht eine `rules.json` im Root, oder eine neue Mini-Fixture). Test 2: keine `rules.json` → `[WARN]` auf stderr **und** Vermerk in `get_violations`-Antwort. Volllauf-Pflicht.
- **TD-009-Inline-Refactor in 010 ODER 011** — der nächste P0-Schritt, der `McpCodeGraphServer` erweitert (A1 oder A4), sollte den Refactor von 5 Konstruktor-Params auf ein `McpCodeGraphServerOptions`-`record` mitnehmen — geschätzt 1-2 h extra in der jeweiligen Einheit, erspart eine eigenständige TD-009-Einheit.
- **Konzept-Pflege-Einheit (separat, User-pflichtig)** — die 3 Konzept-Diskrepanzen aus 008 (Z. 539-552, 550, 564) sollten in einer eigenen Mini-Einheit vom User selbst (nicht vom Coder, A7) editiert werden. Reihenfolge: irgendwann nach 009, vor oder parallel zu 010. Wenn der Orchestrator eine entsprechende User-Aktion triggern will, wäre der nächste sinnvolle Zeitpunkt: nach dem Push der 009-Commits, damit der `konzept.md`-Patch und der Code-Refactor im selben Release-Notes-Block erscheinen.

### 11.3 Was warten kann (post-011)

- TD-006 (Datei-Scan-Duplikation) — wird relevant, wenn Last-Fixture (A7) oder eine neue Web-Komponente (post-P0/P1) erneut Scan-Logik braucht.
- TD-007 (`TryApplyContentChange` `record`) — wird relevant, wenn `McpCodeGraphServer` ohnehin refaktoriert wird (TD-009).
- TD-008 / TD-010 (`ILinterEngineConfig`-Refactor) — wird relevant, sobald eine 3. Konfigurations-Property auf `McpCodeGraphServer` dazukommt.
- TD-014 (`McpServerOptionsBuilder`) — wird relevant vor A5 (`--mcp-log`).

---

## Konvention-Commits (vorgeschlagene Commit-Reihenfolge)

1. **`chore(tests): TD-016a fixture-base refactor (CompileErrorMini, GitImpactMini) [codegraph-mcp-server]`** — `git add src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (gezielt, kein `-A`/`.`, A4).
2. *(Optional, falls Reflection-Test angenommen)* **`test(tests): TD-016a refactor regression-sicherung [codegraph-mcp-server]`** — `git add src/AiNetLinter.Tests/Fixtures/TD016aRefactorTests.cs` (gezielt).
3. **`chore(debt): TD-016a geschlossen durch 009 [codegraph-mcp-server]`** — `git add tasks/codegraph-mcp-server/tech-debt.md` (gezielt).
4. **`chore(task): unit 009 result [codegraph-mcp-server]`** — `git add tasks/codegraph-mcp-server/units/009/result.md` (gezielt, inkl. `volllauf.log` falls als Anhang gewünscht, analog `units/008/result.md`).

**Push:** nein (A4). Working-Tree bleibt lokal bis zum Kritiker-`approved`. Branch `main`, kein `-A`/`.`, kein Amend, kein History-Rewrite (A4).

**Commit-Anzahl:** 3 minimal (ohne Reflection-Test) oder 4 mit Reflection-Test. Konsistent zum 001-008-Pattern.

## Erwartete Verdict-Optionen

- **`approved`:** Refactor sauber durchgezogen, CompileErrorMini + GitImpactMini korrekt von `FixtureWorkspaceBase` abgeleitet, `Dispose`-Override in GitImpactMini mit korrekter Reihenfolge, A3-Pfade dokumentiert (entweder über bestehende Tests in 6.1 oder zusätzlich über Reflection-Tests in 6.2), Volllauf 1165/1165 (oder 1166/1166) grün, `tech-debt.md` korrekt aktualisiert. → TD-016a ist formal geschlossen, 010 kann starten.
- **`issues`:** z. B. `ClearReadOnlyAttributes`-Reihenfolge falsch, `RootPath`-Auflösung in GitImpactMini-`RunGit` bricht, `using`-Imports fehlen, A3-Nachweis unvollständig, `tech-debt.md` Format-Inkonsistenz zu TD-003/012/013/015/016. → `009/fix-01/` (max 3 Fix-Runden pro Einheit laut `kernel.md` A1; aktueller Zähler: 0/3 für 009, also 3 verbleibend).
- **`blocked`:** Widerspruch zwischen Plan und `FixtureWorkspaceBase`-Realität aufgedeckt (z. B. die Basis ist gar nicht so allgemein wie angenommen), oder ein bestehender Test benutzt die duplizierten Helper doch direkt (würde beim Volllauf rot werden, aber der Coder kann den Test **in derselben Einheit** fixen, kein `blocked`-Grund), oder `McpServerCommand*Tests.cs` hat eine unerwartete Abhängigkeit. → Nutzer klärt (A6).

## Aufruf-Budget

Aktueller Stand: 27/40 (nach 008/fix-01, siehe `state.md:786`).
Mit 009: 1× Planer (jetzt) + 1× Coder + 1× Kritiker = 30/40.
Verbleibend: 10/40 für 010, 011, 012, ... (P0/P1-Rest-Erweiterungen).

---

## Sprache und Frontmatter

- **Sprache:** Deutsch, dem User-Stil entsprechend (informell, prägnant, technisch).
- **Frontmatter:** vollständig (unit, task, workflow, type, created_by, created_at, trigger).
- **Working-Tree nach Commits:** clean.
- **Branch:** `main`.
- **Push:** nein (Orchestrator-Aufgabe nach `approved`).
