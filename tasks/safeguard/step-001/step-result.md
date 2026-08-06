---
status: done (pending audit)
type: step-result
task: safeguard
step: 001
title: "SafeguardScanner mit deterministischer Score-Berechnung"
epic: EPIC-01
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
related_to:
  - konzept.md#muss-haven-punkte-4-6-8
---

# Step 001 Result: SafeguardScanner mit deterministischer Score-Berechnung

## Zusammenfassung

EPIC-01 umgesetzt: `SafeguardScanner.ComputeScoreAsync` als reine, deterministische
Score-Berechnungs-Funktion, die eine geladene Solution via `LinterEngine.RunAsync`
analysiert und einen 0-10-Score (gewichtete Komponenten Violations/CC/Footprint +
Sealed-Bonus) inkl. Top-Violations und Remediation-Hints liefert. 13 xUnit-v3-Tests
grün (10+ gemäß Plan DoD).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/SafeguardScanner.cs` (neu) — Scanner-Klasse + 6 Records
  (Parameter/ScoreResult/ViolationEntry/RemediationHint/SafeguardScoreResult/ScannedClass).
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` (neu) — 13 xUnit-v3-Tests
  inkl. AdhocWorkspace-Helper, ThrowingTextLoader-Fake, NullConsole-Stub.

## Code-Commit

- **Hash:** `afb6146`
- **Subject:** `feat(mcp): SafeguardScanner mit deterministischer Score-Berechnung [safeguard]`
- **Body:** enthält `Refs: tasks/safeguard/step-001`, Auflistung der Kernänderungen,
  Begründung der Gewicht-Anpassung, sowie Pflicht-`### Commit-Vorschlag`-Block.

## Build-Output

`dotnet build` → 0 Warnungen, 0 Fehler.

## Test-Output

- `dotnet test --filter FullyQualifiedName~SafeguardScannerTests` → 13/13 grün, 1 s.
- `dotnet test --filter Category=Unit` → 141/141 grün, 16 s.

## Abweichungen vom Plan

- **`BuildRemediation`-Signatur:** Plan-Variante war `IReadOnlyCollection<RuleViolation>`;
  umgesetzt als `IReadOnlyList<ViolationEntry>`. Grund: `BuildScoreResult` produziert
  bereits `ViolationEntry`-Records (für den JSON-Schema-Output in EPIC-02), ein
  Re-Mapping auf `RuleViolation` wäre redundant. Tests können so direkt mit
  `ViolationEntry`-Listen arbeiten, ohne synthetische `RuleViolation`s zu bauen.
- **Internes `ScannedClass`-Record:** Plan-Skizze sah `IReadOnlyCollection<INamedTypeSymbol>`
  in `BuildScoreResult` vor. Umgesetzt ist `IReadOnlyList<ScannedClass>` (eigener
  interner Record) als Daten-Container. Grund: `BuildScoreResult` muss isoliert
  testbar bleiben (Clamp-Test, Remediation-Test), ohne dass Tests ein echtes
  `INamedTypeSymbol` synthetisieren müssen. Die Plan-Skizze schrieb "Coder wählt
  die einfachere Variante" — `ScannedClass` ist die einfachere und entkoppelt die
  Score-Mathematik von Roslyn-Symbols.
- **Gewicht-Anpassung (`ViolationPenaltyUnit = 1.5`):** Plan-Default 0.1 wäre zu
  schwach — 1 Error (Severity 2) hätte nur 0.2 Penalty gedroppt, Score 9.8. Der
  Test `SingleViolation_LowersScoreBelowThreshold` verlangt Score < 8.0. Mit
  `ViolationPenaltyUnit = 1.5` ergibt 1 Error 3.0 Penalty → Score 7.0, klar
  unter 8.0. Severity-Skala 2/1/0.25 (Error/Warning/Info) aus Konzept übernommen,
  nur die Penalty-Einheit justiert. Anpassung im Commit-Body dokumentiert.
- **`SafeguardScannerParameters`-Record-Felder:** `ScopeFilter` und `CancellationToken`
  sind Pflicht-Felder (Plan-konform). Tests verwenden einen `CreateParameters`-Helper
  bzw. benannte Argumente, um die explizite Angabe von `ScopeFilter: null,
  CancellationToken: CancellationToken.None` zu kapseln.

## Beobachtungen

- **`LinterAnalyzer`-Reuse:** Der Plan erlaubt optional den Wechsel auf
  `LinterAnalyzer.Classes`/`ClassInfo`-Reuse für die Klassen-Aggregation. Verworfen:
  Die direkte Roslyn-Walk-Variante (Klassen-Deklarationen via
  `root.DescendantNodes().OfType<ClassDeclarationSyntax>()`) ist ~30 Zeilen lang,
  bleibt entkoppelt vom internen LinterAnalyzer-API und liefert die drei
  benötigten Werte (CC, Footprint, IsSealed) ohne Mehraufwand. Coupling-Bewertung
  zugunsten Direkt-Walk entschieden.
- **`EnumerateConcreteClasses` ist `private` (nicht `internal`):** Plan hat
  `EnumerateConcreteClasses` als privaten Helper angedeutet. Es ist die einzige
  Stelle, an der Roslyn-Symbols angetastet werden — wenn `BuildScoreResult`
  in EPIC-02 erweitert wird (z. B. um weitere Score-Komponenten), kann sie
  promotions-auf `internal` dann nachgeholt werden.
- **Fehlende `GetViolationsScannerTests.cs`:** Im Plan als "Tech-Debt-Beobachtung
  für den Kritiker" markiert — diese Datei existiert auch nach diesem Step
  weiterhin nicht. SafeguardScanner etabliert jetzt das Pattern
  (AdhocWorkspace-Helper + ThrowingTextLoader + IClassFixture-Nutzung), das
  ein zukünftiger `GetViolationsScannerTests` übernehmen könnte.
- **`nameof` für `RuleName`-Vergleiche vermeidbar:** `ResolveHintForRule` arbeitet
  mit `LinterRuleIds.MaxLineCount`-Konstanten statt mit String-Literalen. Pattern
  konsistent mit dem Rest des Projekts.

## Bekannte Unschärfen

- **Gewicht-Justierung noch nicht an Konzept-Daten kalibriert:** Die Penalty-Einheit
  1.5 ist so gewählt, dass sie die Test-Erwartungen erfüllt. Eine echte
  Kalibrierung an realen Codebases fehlt — gehört in eine spätere EPIC-Phase,
  wenn der Scanner gegen mehrere Production-Repositories läuft.
- **CC-Berechnung pro Klasse nicht-`Max`-akkumuliert:** Die CC-Komponente nimmt
  aktuell nur den **Max**-Wert der Methoden-CC pro Klasse. Alternative wäre
  die Summe oder der Durchschnitt — Plan-Vorgabe ist "Mittelwert der
  MaxCognitiveComplexity aus allen Klassen-Symbols", was wir exakt umsetzen.
  Falls der Planer den "Mittelwert" anders meint (z. B. Mittelwert aller
  Methoden-CCs über alle Klassen statt Mittelwert der Maxima), wäre das
  eine Folge-Anpassung — aktuell dokumentiert in der Score-Mathematik.
- **`Severity` aus `EffectiveSeverity` vs. `RuleRegistry`:** `ResolveSeverity`
  nutzt zuerst `EffectiveSeverity` (überschreibt Konfig-seitig), fällt dann auf
  `RuleRegistry.TryResolve` zurück, final auf "warning". Reihenfolge
  konsistent mit `GetViolationsScanner.ResolveSeverity`.
- **Kein Coverage-Threshold-Mechanismus:** Der Plan erwähnt die 4
  Score-Komponenten; weitere Komponenten (z. B. Test-Coverage) sind out of
  scope. EPIC-02/03 können die `ScannedClass`-Datenquelle um solche Werte
  erweitern.

## Modell-Info

- `coded_by_model`: MiniMax-M3
- `coded_by_model_knowledge_cutoff`: 2026-01
