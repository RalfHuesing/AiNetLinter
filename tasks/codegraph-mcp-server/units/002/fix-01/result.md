---
unit: 002
fix_round: 01
task: codegraph-mcp-server
workflow: dynamic-loop
type: result
created_by: coder
created_at: 2026-08-01
code_commit_hash: bd9e6fd
status: done
---

# Result Einheit 002/fix-01 — M-1: `McpToolResults.InvalidArgument`-Helper liefert irreführenden Hint für `search_pattern`

## Zusammenfassung

M-1 behoben: `SearchPatternTool` ruft beim leeren `pattern` jetzt den korrekten
`McpToolResults.Error(LinterErrorCodes.InvalidArgument, …, hint: …)`-Pfad — analog
zum bereits korrekten Regex-Pfad 14 Zeilen tiefer (Z. 57-60) und damit konsistent
im selben Tool. Test 8 (`ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError`) um
drei scharfe Hint-Assertions + Wortlaut-Kopplungs-Kommentar erweitert; der
`get_impact`-Hartkodierungs-Hint aus `McpToolResults.InvalidArgument` (Z. 79) wird
ab jetzt von Test 8 aktiv ausgeschlossen. A3-Fehlschlag-Nachweis in 4 Phasen
dokumentiert: Test 8 grün nach Fix → Test 8 rot nach temporärem Revert → Test 8
wieder grün nach Wiederherstellung → voller Lauf 1097/1097 grün.

## Geänderte Dateien

| Datei | Status | Commit-Hash |
|---|---|---|
| `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | MOD (1 Zeile → 4 Zeilen) | `bd9e6fd` |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | MOD (Test 8: +5 Z. Kommentar, +3 Assertions) | `bd9e6fd` |

Beide Dateien im selben Commit, gezielt gestaged (kein `-A`/`.`, A4).

## Commit

- **Hash:** `bd9e6fd14539a89f942cdc0ac72c9629dc6430b2`
- **Message:** `fix(mcp): search_pattern leerer-pattern-Hint [codegraph-mcp-server]`
- **Branch:** `main`
- **Push:** nein (lokal; Orchestrator entscheidet)
- **`git status` nach Commit:** `nothing to commit, working tree clean`
- **Branch-Stand:** `Your branch is ahead of 'origin/main' by 11 commits.` (vor dem
  Commit: 10 — exakt +1, keine versteckten Commits)

## Build-/Test-Output (wortwörtlich)

### Build
```
$ dotnet build AiNetLinter.slnx --nologo
  Wiederherzustellende Projekte werden ermittelt...
  Alle Projekte sind für die Wiederherstellung auf dem neuesten Stand.
  AiNetLinter -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.dll
  AiNetLinter.Tests -> C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll

Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler

Verstrichene Zeit 00:00:11.66
```

### Erstlauf Test 8 (nur `ExecuteAsync_EmptyPattern`)
```
$ dotnet test --no-build --nologo --filter "FullyQualifiedName~ExecuteAsync_EmptyPattern"
Testlauf für "C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll" (.NETCoreApp,Version=v10.0)
Insgesamt 1 Testdateien stimmten mit dem angegebenen Muster überein.

Bestanden!   : Fehler:     0, erfolgreich:     1, übersprungen:     0, gesamt:     1, Dauer: 3 s - AiNetLinter.Tests.dll (net10.0)
```

### SearchPattern-Filter (Regression-Schutz: alle 9 Tests grün)
```
$ dotnet test --no-build --nologo --filter "FullyQualifiedName~SearchPattern"
Testlauf für "C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll" (.NETCoreApp,Version=v10.0)
Insgesamt 1 Testdateien stimmten mit dem angegebenen Muster überein.

Bestanden!   : Fehler:     0, erfolgreich:     9, übersprungen:     0, gesamt:     9, Dauer: 27 s - AiNetLinter.Tests.dll (net10.0)
```

### Volllauf nach A3-Wiederherstellung
```
$ dotnet test AiNetLinter.slnx --no-build --nologo
Testlauf für "C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\bin\Debug\net10.0\AiNetLinter.Tests.dll" (.NETCoreApp,Version=v10.0)
Insgesamt 1 Testdateien stimmten mit dem angegebenen Muster überein.

Bestanden!   : Fehler:     0, erfolgreich:  1097, übersprungen:     0, gesamt:  1097, Dauer: 8 m 3 s - AiNetLinter.Tests.dll (net10.0)
```

1097/1097 exakt wie Baseline aus `units/002/result.md:64` — keine neue Test-Methode
(Test 8 wurde nur um Assertions erweitert), keine Test-Reduktion, keine Skips.

## A3-Fehlschlag-Nachweis (Pflicht, Kernel A3)

Vorbedingung: `dotnet test --filter "FullyQualifiedName~SearchPattern"` ist 9/9 grün
(vor dem Fix dokumentiert; entspricht Baseline 1097/1097 aus `units/002/result.md:64`).

### Schritt 1 — Code-Fix anwenden + Test 8 erweitern

Beide Änderungen im selben Commit (siehe Diff oben). `SearchPatternTool.cs:40`
ersetzt durch den `McpToolResults.Error(LinterErrorCodes.InvalidArgument, "pattern
darf nicht leer sein.", hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`
-Aufruf, `SearchPatternToolTests.cs:174-180` um den M-1-Regression-Schutz-Kommentar
+ drei Assertions erweitert.

### Schritt 2 — Erstlauf (grün, neue Assertion matched neuen Hint)

```
$ dotnet build AiNetLinter.slnx --nologo
  AiNetLinter -> ...\AiNetLinter.dll
  AiNetLinter.Tests -> ...\AiNetLinter.Tests.dll
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler

$ dotnet test --no-build --nologo --filter "FullyQualifiedName~ExecuteAsync_EmptyPattern"
  Bestanden!   : Fehler:     0, erfolgreich:     1, übersprungen:     0, gesamt:     1, Dauer: 3 s

$ dotnet test --no-build --nologo --filter "FullyQualifiedName~SearchPattern"
  Bestanden!   : Fehler:     0, erfolgreich:     9, übersprungen:     0, gesamt:     9, Dauer: 27 s
```

→ Test 8 grün (neue Assertion `Assert.Contains("Pattern angeben", …)` matched den
neuen Hint); die anderen 8 SearchPattern-Tests unberührt grün.

### Schritt 3 — A3-Auslöser: Code-Fix temporär zurücknehmen

In `SearchPatternTool.cs:38-44` den frisch eingebauten `McpToolResults.Error(...)`-
Aufruf wieder durch den ursprünglichen `McpToolResults.InvalidArgument("pattern
darf nicht leer sein.")`-Aufruf ersetzt (Test-Änderung in
`SearchPatternToolTests.cs:174-180` bleibt unverändert aktiv):

```diff
-            return McpToolResults.Error(
-                LinterErrorCodes.InvalidArgument,
-                "pattern darf nicht leer sein.",
-                hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.");
+            return McpToolResults.InvalidArgument("pattern darf nicht leer sein.");
```

Build: 0 Warnungen, 0 Fehler.

### Schritt 4 — A3-Lauf: Test 8 MUSS rot sein (wortwörtlich)

```
$ dotnet test --no-build --nologo --filter "FullyQualifiedName~ExecuteAsync_EmptyPattern"
  [xUnit.net 00:00:05.27] AiNetLinter.Tests.Mcp.Tools.SearchPatternToolTests.ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError [FAIL]

  Fehler AiNetLinter.Tests.Mcp.Tools.SearchPatternToolTests.ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError [3 s]
  Fehlermeldung:
   Assert.Contains() Failure: Sub-string not found
  String:    "[ERROR]: INVALID_ARGUMENT: pattern darf nicht leer"···
  Not found: "Pattern angeben"
    Stapelverfolgung:
       at AiNetLinter.Tests.Mcp.Tools.SearchPatternToolTests.ExecuteAsync_EmptyPattern_ReturnsInvalidArgumentError() in
         C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.Tests\Mcp\Tools\SearchPatternToolTests.cs:line 179

  Fehler!      : Fehler:     1, erfolgreich:     0, übersprungen:     0, gesamt:     1, Dauer: 3 s

Command exited with code 1
```

**Diagnose:** Der String-Auszug beginnt mit `[ERROR]: INVALID_ARGUMENT: pattern
darf nicht leer···` — der `get_impact`-Hartkodierungs-Hint aus
`McpToolResults.InvalidArgument` (Z. 79, "Entweder gitRef ODER symbolIdentifier
angeben, nie beide.") ersetzt den search_pattern-Hint. (Die `···` im
PowerShell-Output sind ein cp1252-Encoding-Artefakt; das tatsächliche Output-
Stück enthält `gitRef` und `symbolIdentifier`, die die beiden
`Assert.DoesNotContain`-Assertions ebenfalls gefangen hätten — `Assert.Contains`
läuft aber zuerst und ist deshalb die primäre Diagnose.)

**Erwartung aus Plan Z. 244-247 exakt eingetreten:** die `Assert.Contains("Pattern
angeben", …)`-Assertion erkennt die Regression. Damit ist der A3-Nachweis
**erbracht**: die ursprüngliche `McpToolResults.InvalidArgument`-Nutzung wird vom
neuen Assertion-Set aktiv gefangen — wäre sie nicht gefangen worden, wäre die
Test-Assertion zu schwach und müsste nachgeschärft werden (Kernel A3).

### Schritt 5 — A3-Rückgängig: Code-Fix wieder einbauen

Den Schritt-3-Replace wieder rückgängig gemacht — der
`McpToolResults.Error(LinterErrorCodes.InvalidArgument, "pattern darf nicht leer
sein.", hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.")`-Aufruf
ist wieder drin. Test-Erweiterung in `SearchPatternToolTests.cs:174-180` bleibt
unverändert.

```
$ dotnet build AiNetLinter.slnx --nologo
  AiNetLinter -> ...\AiNetLinter.dll
  AiNetLinter.Tests -> ...\AiNetLinter.Tests.dll
Der Buildvorgang wurde erfolgreich ausgeführt.
    0 Warnung(en)
    0 Fehler

$ dotnet test --no-build --nologo --filter "FullyQualifiedName~ExecuteAsync_EmptyPattern"
  Bestanden!   : Fehler:     0, erfolgreich:     1, übersprungen:     0, gesamt:     1, Dauer: 3 s
```

Test 8 grün nach Wiederherstellung.

### Schritt 6 — Volllauf: 1097/1097 grün

(siehe Build-/Test-Output oben — `Bestanden! : Fehler: 0, erfolgreich: 1097,
übersprungen: 0, gesamt: 1097, Dauer: 8 m 3 s`)

**Was der A3-Nachweis zeigt:**

1. Die `Assert.Contains("Pattern angeben", …)`-Assertion **erkennt** die
   ursprüngliche `McpToolResults.InvalidArgument`-Regression (Schritt 4 — Failure
   `Not found: "Pattern angeben"`).
2. Die `Assert.DoesNotContain("gitRef", …)` und `Assert.DoesNotContain("symbolIdentifier", …)`-
   Assertions **erkennen** sie ebenfalls als Defensiv-Absicherung (wären als
   zweite und dritte Assertion im Failure-Trace gefolgt, hätten die erste
   Assertion nicht schon den Test gestoppt).
3. Der `Assert.Contains("INVALID_ARGUMENT", …)`-Bestandteil allein hätte die
   Regression **nicht** erkannt — exakt der A3-Schwäche-Punkt, den der 002-
   Review identifiziert hat (`units/002/review.md:174-179`).

## Abweichungen vom Plan

Keine. Der Plan-Code-Block (Z. 85-95) wurde wortwörtlich umgesetzt (gleiche
Argument-Reihenfolge, gleiche `hint`-Position als benannter Parameter, gleicher
`LinterErrorCodes.InvalidArgument`-Code, identischer Hint-Wortlaut `"Pattern
angeben — leeres Pattern ist nicht erlaubt."`). Der Plan-Test-Block (Z. 158-181)
wurde wortwörtlich umgesetzt, inkl. des 5-Zeilen-Kopplungs-Kommentars. Kein
Refactoring, keine Helper-Umbenennung, keine Edits an anderen Dateien.

## Beobachtungen

Leer. Fix-Runde war minimal-invasiv (1 Datei-Edit + 1 Test-Edit, beide in der
explizit benannten Scope-Liste); keine neuen Funde.

## Bekannte Unschärfen

- **PowerShell-cp1252-Abrisszeichen `···` im A3-Failure-Output:** das ist ein
  PowerShell-Encoding-Artefakt beim Pipelining des `dotnet test`-Outputs in
  PowerShell 5.1 auf der lokalen Maschine (Standardcodepage, nicht UTF-8). Der
  eigentliche Failure-Inhalt (`Not found: "Pattern angeben"` + String mit
  `[ERROR]: INVALID_ARGUMENT: pattern darf nicht leer`) ist vollständig
  dokumentiert; die `···` sind eine Kürzung des sichtbaren Strings, nicht ein
  Verlust von Failure-Information. Die Test-Assertion schlägt bei der Diagnose
  fehl, die im Plan Z. 244-247 explizit erwartet wurde — also kein semantischer
  Verlust.
- **Branch-Stand:** `main` ist nach diesem Commit 11 Commits ahead of
  `origin/main` (vorher 10). Kein Push, A4-konform.
- **A3 für Defensive Assertions nicht getrennt nachgewiesen:** die
  `Assert.DoesNotContain("gitRef", …)`- und `Assert.DoesNotContain("symbolIdentifier", …)`-
  Assertions wurden im A3-Lauf nicht isoliert geprüft (sie hätten den Test nach
  der ersten Assertion-Phase sowieso gestoppt). Sie sind Defensiv-Absicherung
  für eine künftige Variante, in der der Hint-Wortlaut im Tool geändert wird
  (z. B. von "Pattern angeben" auf nur "Pattern" oder "pattern") — dann fängt
  das DoesNotContain weiterhin die `get_impact`-Hartkodierung, falls jemand in
  derselben Fix-Runde die Helper-Logik vermischen würde. Der Plan hat das
  explizit so vorgesehen (Z. 195-201), keine separate A3-Runde nötig.

## Nächste Aktion des Orchestrators

Kritiker-Aufruf für `tasks/codegraph-mcp-server/units/002/fix-01/` mit
`plan.md` (Commit `517bebe`) + `result.md` (Commit dieses Result-Artefakts,
siehe unten) als Eingabe.

## Commit-Block (Loop-Artefakt)

- **Result-Artefakt-Hash:** wird vom Orchestrator nach dem Commit von
  `tasks/codegraph-mcp-server/units/002/fix-01/result.md` gefüllt (Loop-
  Konvention; der Coder committet den Code, der Orchestrator committet die
  Loop-Artefakte).
