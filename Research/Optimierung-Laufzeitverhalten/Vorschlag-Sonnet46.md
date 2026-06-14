## Was tatsächlich Zeit kostet (zwei Phasen)

**Phase 1 — Workspace-Loading** (`MSBuildWorkspace.OpenSolutionAsync`):
MSBuild muss Projektdateien parsen, NuGet-Graph auflösen, Compilation-Objekte aufbauen — auch mit `DesignTimeBuild=true`. Bei größeren Solutions vermutlich der Hauptfresser.

**Phase 2 — Per-Datei-Analyse** (`GetSemanticModelAsync` + `LinterAnalyzer`):
Läuft parallel, aber für jede valide `.cs`-Datei.

Deine Checksum-Idee trifft nur Phase 2. Wenn Phase 1 dominiert, bringt sie wenig. Ohne Profiling ist unklar, was mehr wiegt.

---

## Haben wir dateiübergreifende Prüfungen? Ja, vier Stück

In `PostAnalysisChecks` ([src/AiNetLinter/Core/PostAnalysisChecks.cs:24](src/AiNetLinter/Core/PostAnalysisChecks.cs)):

| Prüfung | Cross-File-Natur |
|:---|:---|
| **TestSentinel** | Source-Klasse ↔ Test-Datei: wenn Test sich ändert, kann Violation verschwinden |
| **AIContextFootprint** | Transitive Typ-Abhängigkeiten: Datei A ändert sich → Klassen, die A nutzen, haben neuen Footprint |
| **InheritanceDepth** | Basisklasse in anderer Datei → Tiefe ändert sich |
| **PartialClassLineAggregator** | MaxLineCount aggregiert über alle Parts einer partial class |

---

## Deine Checksum-Idee — Bewertung

**Bringt es was?** Ja, für Phase 2, aber mit Einschränkungen:
- Für rein syntaktische Regeln (MaxLineCount, MaxMethodLineCount, Complexity, NullableEnable, SealedClasses, PascalCase…) — einfach: Cache-Treffer = fertig
- Für die vier Cross-File-Regeln: Naiver Cache würde False Negatives produzieren

**Konservativer Kompromiss** (einfach zu implementieren): Wenn *irgendeine* Datei eine geänderte Checksum hat → Cross-File-Checks für *alle* Dateien neu ausführen, nur Per-File-Violations aus Cache holen. Das ist korrekt und verliert kein Feature.

**Dein False-Positive-Punkt**: Das ist das echte Problem. Wenn eine Datei gecacht ist aber eine Suppression entfernt wurde, merkt der Cache das nicht — da die Suppression in derselben Datei steht, würde eine Checksum-Änderung die Datei aber sowieso invalidieren. Korrekt.

---

## Größerer Hebel: Syntaktische Fast-Path ohne Workspace

Fast alle Regeln brauchen gar kein `MSBuildWorkspace` — nur einen `CSharpSyntaxTree`:
```
MaxLineCount, MaxMethodLineCount, Complexity, NullableEnable, SealedClasses,
PascalCase, SemanticNaming, EmptyCatch, NamespaceMapping, PartialClass...
```

Nur diese brauchen echtes Semantic Model:
- **AIContextFootprint** (Typ-Auflösung nötig)
- **InheritanceDepth** (Typ-Hierarchie)
- **TestSentinel** (Typ-Referenzen aus Testdateien)

Idee: `--fast`-Modus der direkt `CSharpSyntaxTree.ParseText()` aufruft (Millisekunden pro Datei, kein MSBuild-Overhead) und semantische Checks weglässt. Mit Checksum-Cache obendrauf wäre der Fast-Path fast sofort.

---

## Priorisierung

| Ansatz | Aufwand | Erwarteter Gewinn | Feature-Verlust |
|:---|:---:|:---:|:---|
| Checksum-Cache (per-file) | mittel | 30–70% in Phase 2 | keiner, wenn Cross-File-Fallback |
| Syntaktischer Fast-Path | hoch | 80%+ wenn Phase 1 dominiert | AIContextFootprint, InheritanceDepth, TestSentinel |
| Profiling zuerst | gering | — gibt Richtung vor | — |

**Empfehlung**: Erst messen — ein einfaches Stopwatch-Log von `LoadAsync` vs. `AnalyzeSolutionAsync` zeigt sofort, wo die Zeit steckt und welche Investition sich lohnt.