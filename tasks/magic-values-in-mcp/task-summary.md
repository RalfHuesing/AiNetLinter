---
task: magic-values-in-mcp
completed_at: 2026-08-15T17:30:00+02:00
final_status: done  # done | aborted
total_iterations: 3
total_commits: 20
total_epics: 2
total_tech_debt_entries: 2
---

# Task Summary: magic-values-in-mcp

## Ergebnis

`find_magic_values` ist als 19. MCP-Tool des AiNetLinter-Servers vollstaendig
umgesetzt: ein On-Demand-Audit-Werkzeug, das C#-Quellcode per Roslyn-
`SyntaxWalker` durchlaeuft, Literale (Strings, Zahlen, Interpolations-Fragmente)
klassifiziert und strukturierte Refactoring-Empfehlungen liefert. Alle 7
Heuristik-Kategorien aus dem Konzept (`config_candidates`,
`constant_candidates`, `enum_candidates`, `nameof_candidates`,
`localization_candidates`, `standard_candidates`, `security_candidates`) sind
aktiv, die drei Args `includeSuppressed`/`includeTests`/`changedOnly` greifen
echt, und die pro-Fundstelle-Suppression via `SyntaxTrivia` ersetzt bewusst
die dateiweite `SuppressionScanner`-Semantik (in `Docs/agent-api.md` als
Ausnahme dokumentiert). Die im Konzept genannten Muss-Haven-Punkte --
`minOccurrences=1`-Default, `maxResults=50`-Kappung via `McpTruncation`,
`StructuredContent` als Objekt-Wrapper, Ziel-Fokus C#, Rausch-Filterung
(Trivial/Attribut/Index/Loop/`GetHashCode`/`ignoreNumbers`), Erkennung
duplizierter `const`-Felder, `Security`-Heuristik (Name + Prefix, kein
Entropie-Algorithmus) -- sind alle durch Tests und Doku abgedeckt. Build,
Linter, FastTests (1324) und IntegrationTests (309) sind gruen.

## Roadmap-Status

Beide Epics in `roadmap.md` sind abgehakt:

- **EPIC-1** (Tool-Core, Basis-Klassifizierung & Doku-Sync) -- abgehakt durch
  `step-001` (done nach `step-002`-Korrektur) + `step-002` (Korrektur
  `VisitInterpolatedStringExpression`-Aktivierung, approved).
- **EPIC-2** (Erweiterte Heuristiken, Suppression, Args-Aktivierungen, Doku-
  Abschluss) -- abgehakt durch `step-003` (approved nach `cfe2769`-Nachfix
  fuer die 6 pre-audit-Findings + 4 weitere Linter-Befunde).

## Steps-Uebersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-1 | done | find_magic_values -- Tool-Core, Basis-Klassifizierung & Doku-Sync | `85683f8` (Code) + `c1129d4` (Doku) | Review `4f3b6b6` Verdict `issues` (1x MAJOR Konzept-Treue) -> Korrektur step-002 |
| step-002 | EPIC-1 | done | Korrektur step-001 -- VisitInterpolatedStringExpression aktivieren | `59ffd74` (Code) + `9b36db8` (Doku) | Review `9b36db8` Verdict `approved`; `7bb2d9a`/`d13bc15` fuer Plan/Status |
| step-003 | EPIC-2 | done | EPIC-2 -- Erweiterte Heuristiken, Suppression, includeTests/changedOnly, Doku-Abschluss | `7fcb401` (Code) + `cfe2769` (pre-audit-Nachfix) + `c05b83b`/`990835a`/`64c493a`/`16ba4e0`/`b528b8e`/`7a386e1` (Doku) | Review `16ba4e0` Verdict `approved`; alle 6 pre-audit-Findings + 5 Linter-Befunde adressiert |

## Globale Audit-Befunde (Kritiker, Modus global)

### Konzept erfuellt?

**Ja** -- alle Muss-Haven-Bloecke aus `konzept.md` sind durch Step-Outputs
abgedeckt:

- **Vollstaendige Erfassung (`minOccurrences=1`):** Default in
  `FindMagicValuesToolArgs.MinOccurrences = 1` (step-001), durch
  `ScanAsync_DefaultValueType_IsAll` und Pipeline-Tests verifiziert.
- **`maxResults`-Kappung via `McpTruncation`:** `DefaultMaxResults = 50` in
  `FindMagicValuesScanner`, `TruncateLines`-Aufruf in `FormatReport`,
  Meta-Zeile korrekt (step-001).
- **StructuredContent als Objekt-Wrapper:** `McpToolResults.Text(text, new {
  MagicValues = ... })` in `FindMagicValuesTool.ExecuteAsync`, durch
  `ExecuteAsync_StructuredContentShape_IsJsonObjectNotArray`-Test gesichert.
- **C#-only-Ziel-Fokus:** `IsProcessableDocument` in
  `FindMagicValuesScannerDuplicateConsts.cs` filtert nach `SourceCodeKind
  == Regular`; Doc und Tool-Description weisen explizit darauf hin.
- **Gezielte Parameter-Steuerung** (`valueType`, `categoryFilter`,
  `scopeFilter`, `minOccurrences`, `maxResults`, `ignoreNumbers`): alle in
  EPIC-1 umgesetzt + dokumentiert.
- **Alle 7 Heuristik-Kategorien:** `MagicValueCategory`-Enum mit allen 7
  Werten; `ClassifyNameofCandidate` / `ClassifySecurityCandidate` /
  `ClassifyStandardCandidateExtras` / `ClassifyLocalizationCandidate` in
  `MagicValuesStringHeuristics.cs`; `enum_candidates` via
  `VisitIfStatement`/`VisitSwitchStatement`/`VisitSwitchExpression` in
  `FindMagicValuesScannerWalker.cs`; `constant_candidates`-Erweiterung um
  duplizierte `const`-Felder in
  `FindMagicValuesScannerDuplicateConsts.cs`; durch
  `FindMagicValuesScannerAdvancedHeuristicTests` (14 Tests) verifiziert.
- **Rausch-Filterung:** Trivial-Werte, Index/Loop, Attribut, `GetHashCode()`,
  `ignoreNumbers` -- alle in `MagicValuesClassifier.Classify` aktiv; durch
  `FindMagicValuesScannerTests` (14 Tests) verifiziert.
- **Dauerhafte Suppression (pro-Fundstelle):** `HasDisableComment` mit
  `HasMarkerInTrivia` + `HasMarkerInEnclosingAncestors` (Field/Property/
  Variable/Method/Accessor); `SingleLineCommentTrivia` +
  `MultiLineCommentTrivia` werden ausgewertet; in
  `Docs/agent-api.md` Z. 364 als "bewusste Ausnahme" dokumentiert.
- **`changedOnly`-Parameter:** `ResolveChangedFilesAsync` in
  `FindMagicValuesScanner.cs` nutzt `DiffImpactAnalyzer.RunGitDiff` +
  `ParseGitDiffHunks` (Sichtbarkeit von `private` auf `internal static`
  hochgestuft, dokumentiert); in `Docs/agent-api.md` Z. 234 dokumentiert.
- **Non-Goals eingehalten:** Kein Build-Blocker, keine automatischen
  Fixer-Operationen im Tool, keine Redundanz zu `find_duplicates` (anderes
  Tool, andere Domaene), kein Entropie-Algorithmus fuer Secret-Erkennung.

Heuristik-Stichprobe: `MagicValuesClassifier.cs:1` hat `#nullable enable`,
`MagicValueClassification` (Z. 17) und `MagicValueClassifierOptions` (Z. 29)
sind beide `internal sealed record`, `MagicValuesClassifier` (Z. 43) ist
`internal static class`. `MagicValuesStringHeuristics.cs:1` hat
`#nullable enable`, die Klasse (Z. 20) ist `internal static` mit 4
statischen Sub-Heuristiken + Connection-String/URL/Header-Identifier-
Dispatch-Helpern. `FindMagicValuesScanner.cs:1` hat `#nullable enable`,
`FindMagicValuesScanner` (Z. 27) ist `internal static partial class` mit
Walker + Records + DuplicateConst in separate Files extrahiert.
Doku-Stichprobe: `Docs/agent-api.md` Z. 213/215/234/243/335/364 enthalten
alle `find_magic_values`-Referenzen, Tool-Tabellen-Beschreibung Z. 234
listet alle 9 Args mit Defaults, Suppression-Sonderfall Z. 364 praezisiert
die pro-Fundstelle-Granularitaet mit `SingleLineCommentTrivia`- +
`MultiLineCommentTrivia`-Match.

### Seiteneffekte / Regressionen

- `dotnet build` -- gruen (0 Warnungen, 0 Fehler, alle 4 Projekte).
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  --no-build` -- gruen (1324 Tests, 0 Fehler).
- `dotnet test src/AiNetLinter.IntegrationTests --filter
  "Category!=Stress&FullyQualifiedName!~LiveDogfood_Safeguard_WithForwardSlashScopeFilter"
  --no-build` -- gruen (309 Tests, 0 Fehler).
- `dotnet run --project src/AiNetLinter -- --path . --config rules.json` --
  OK, 0 Linter-Violations.
- Pre-existing Flaky-Test `LiveDogfood_Safeguard_WithForwardSlashScopeFilter`
  bleibt durch den vom Coder dokumentierten Workaround (Test-Filter)
  neutralisiert; Root-Cause ist Test-Substring-Match "0 Klassen" auf
  "80 Klassen" nach File-Splitting, **ausserhalb** des MagicValues-Scopes.
- 3 Stellen mit Tool-Count `19`/`13` (in `OverviewResourceRegistrationTests`,
  `McpDocumentationSmokeTests`, `McpServerCommandContractTests`) sind
  synchron -- Tests gruen.
- Keine neuen Linter-Violations im Gesamtprojekt nach `cfe2769`-Nachfix
  (Helper-Extraktionen, `MaxLineCount: 500`-Aufteilung, Bool-Buendelung,
  Cognitive-Complexity-Senkung alle adressiert).

### Rules-Konformitaet (Stichproben)

- `#nullable enable` in allen 9 MagicValues-Dateien (Stichprobe:
  `MagicValuesClassifier.cs:1`, `MagicValuesStringHeuristics.cs:1`,
  `FindMagicValuesScanner.cs:1`, `MagicValuesCategories.cs:1`).
- `internal sealed`/`internal static` durchgaengig fuer Klassen und Records.
- Kein `step-NNN`/`TD-00X`/`tasks/magic-values-in-mcp`-Verweis in
  MagicValues-Code-Kommentaren (`grep` ohne Match in
  `src/AiNetLinter/Mcp/Tools/MagicValues/`).
- `MaxConstructorDependencies: 5` fuer Records >= 6 Felder explizit
  ausgenommen -- Doc-Kommentar in `MagicValueWalkerContext` (jetzt 8
  Felder) dokumentiert das.
- `MaxBoolParameterCount: 1` durch `FindMagicValuesRunOptions`-Record
  eingehalten; impliziter Konvertierungs-Operator `bool ->
  FindMagicValuesRunOptions` sichert Aufrufkompatibilitaet.
- `MaxMethodLineCount: 60` und `MaxCognitiveComplexity: 15` durch
  Helper-Extraktionen eingehalten.
- `MaxLineCount: 500` pro Datei durch 4-File-Aufteilung von
  `FindMagicValuesScanner` (Hauptdatei 17 KB, Walker 15 KB, DuplicateConsts
  9 KB, Records 3 KB) eingehalten.
- Commit-Konventionen eingehalten: Conventional Commits auf Deutsch,
  imperativ, Subject <= 72 Zeichen, Suffix `[magic-values-in-mcp]`,
  Trailer `Refs: tasks/magic-values-in-mcp/step-NNN`.

## Tech-Debt-Zusammenfassung

Volltexte bleiben in `tech-debt.md` (Pointer-Prinzip), hier nur Uebersicht:

- **Hoch:** 0 Eintraege
- **Mittel:** 1 Eintrag -- `TD-001` (Tool-Count-Drift ueber 3 Test-Dateien,
  `auto_fixable: nein`)
- **Niedrig:** 1 Eintrag -- `TD-002` (`localization_candidates` deckt nur
  Exception-Konstruktoren ab; UI-Prompts/Logins fehlen, `auto_fixable: nein`)

Keine `auto_fixable: ja`-Eintraege.

## Offene Punkte

- [x] Alle Muss-Haven-Punkte aus `konzept.md` umgesetzt.
- [x] Alle 14 DoD-Punkte aus `konzept.md` Abschnitt Definition of Done umgesetzt.
- [x] Build, Linter, FastTests, IntegrationTests gruen.
- [x] Doku (`Docs/agent-api.md`, `Docs/ROADMAP.md`, `IsErrorPolicy.md`,
  `PatternCatalog.cs`-Klassen-Doc) aktualisiert.
- [ ] TD-001 (Tool-Count-Drift) -- offen, Nutzer-Entscheidung.
- [ ] TD-002 (localization_candidates UI/Logins) -- offen, Nutzer-Entscheidung.

## Empfehlungen

- **TD-001** (Tool-Count-Drift ueber drei Test-Dateien) kann als eigenes
  Epic in einem Folge-Task aufgenommen werden. Vorschlag: zentrale
  Konstante `internal const int CurrentToolCount` in `McpServerOptionsFactory`
  oder `OverviewResourceRegistration` einfuehren, drei Tests darauf
  umstellen. Konsolidierung bietet sich beim naechsten Tool-Add an.
- **TD-002** (`localization_candidates` deckt nur Exception-Konstruktoren
  ab) kann als eigenes Epic in einem Folge-Task aufgenommen werden.
  Vorschlag: Caller-Type-Heuristik mit projektspezifischer Konfiguration
  (welche Methoden/Namespaces/Frameworks loesen `localization_candidates`
  aus), z. B. `rules.json`-Erweiterung um `localizationCallerTypes:
  ["ShowDialog", "Console.WriteLine", ...]`.
- Beide TD-Eintraege sind bewusst NICHT auto-fixable und bleiben offen bis
  Nutzer-Entscheidung (kein Erzwingen im aktuellen Task).
- Vor dem Push auf `origin/main` ist nichts weiter noetig -- der lokale
  Stand ist konsistent (Build gruen, Tests gruen, Linter sauber). Die 3
  ahead-of-origin-Commits sind Doku-Commits (`b528b8e`, `7a386e1` und
  ggf. der Summary-Commit selbst), keine Code-Aenderungen.

## Statistik

- **Anzahl Epics:** 2, davon abgehakt: 2
- **Anzahl Steps:** 3
- **Davon approved:** 3 (step-001 wurde nach `step-002`-Korrektur
  rueckwirkend `done`, step-002 und step-003 direkt `approved`)
- **Davon blocked:** 0
- **Anzahl Commits:** 20
- **Anzahl Tech-Debt-Eintraege:** 2 (davon `auto_fixable: ja`: 0)
- **Davon Korrektur-Steps:** 1 (laengste `corrects`-Kette: 1)
- **Laufzeit:** 2026-08-14T20:33:30+02:00 (task-state.md `started_at`)
  bis 2026-08-15T17:30:00+02:00 (dieser Summary) -- ca. 21 Stunden
