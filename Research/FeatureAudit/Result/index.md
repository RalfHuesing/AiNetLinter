# AiNetLinter Feature Audit — Gesamtübersicht

Erstellt: 2026-06-20  
Evaluierte Features: 46 (17 Metriken, 20 Boolean-Regeln, 9 System-Features)  
Neue Feature-Vorschläge: 3 (N01–N03)

---

## Zusammenfassung

| Bewertung | Anzahl |
|-----------|--------|
| 🟢 Wertvoll | 35 |
| 🟡 Unpraktikabel | 11 |
| 🔴 Nutzlos | 0 |

Kein Feature ist nutzlos. Alle als unpraktikabel bewerteten Features haben einen validen Kerngedanken, sind aber in ihrer aktuellen Form zu restriktiv, zu schwach konfiguriert oder für den tatsächlichen Projektzuschnitt nicht relevant.

**Sofort handeln (höchste Priorität):**
1. M01 — MaxLineCount auf 500 absenken (aktuell 700 liegt an der empirischen Obergrenze)
2. M14 — MaxAIContextFootprint auf 2.500–3.000 absenken (5.000 transitiver Zeilen liegt weit über dem LLM-Aufmerksamkeitskorridor)
3. M06 — MaxInheritanceDepth auf 3 anheben oder Framework-Ausnahmen prüfen (aktuell 2 erzeugt False Positives für ASP.NET-Controller, EF-Entities, xUnit-Testklassen)
4. M07 — MaxMethodOverloads auf 5 anheben (aktuell 3 blockiert Standard-.NET-Async-Patterns mit CancellationToken)
5. M16 — MinCognitiveComplexityForTest auf 5 anheben (aktuell 3 erzeugt Warnungs-Flut für triviale Methoden)
6. F09 — EnablePerformanceProfiling auf `false` als Default setzen (Entwickler-Debugging-Funktion sollte nicht dauerhaft Dateien in Projektverzeichnissen erzeugen)
7. F08 — ForbiddenNamespaceDependencies mit konkreten Architektur-Verboten konfigurieren (aktuell leer = wirkungslos)

**Mittelfristig prüfen:**
1. R06 — AllowOutParametersInPrivateMethods: vollständige Ausnahme ist zu weit gefasst, auf `private static` oder Try-Präfix einschränken
2. F03 — Discovery-Commands um JSON-Output (`--list-rules --format json`) ergänzen für maschinenlesbare Agent-Integration
3. F02 — Auto-Fix auf weitere Regeln ausweiten (R07 ValueObject→record, R20 nested types → eigene Klasse)

**Neue Features mit stärkster Evidenz:**
1. N01 — BanAsyncVoid: async void (außer Event-Handler) verursacht unkontrollierbare Exceptions und ist das häufigste async-Anti-Pattern in LLM-generiertem Code
2. N02 — BanBlockingTaskAccess: .Wait() / .Result / .GetAwaiter().GetResult() auf Tasks verursachen Deadlocks; empirisch belegt als häufige LLM-Halluzination in async-Kontexten
3. N03 — MaxLinqChainLength: Lange LINQ-Chains erhöhen Cognitive Complexity ohne CC-Signal und erzeugen LM-CC-relevante Pfaddivergenz

---

## Matrix: Metriken

| ID | Feature | Aktuell | Bewertung | Zeitlich | Empfehlung |
|----|---------|---------|-----------|----------|------------|
| M01 | MaxLineCount | 700 | 🟢 | Zeitlos | Auf 500 absenken |
| M02 | MaxMethodLineCount | 60 (150 via CompoundSuppr.) | 🟢 | Zeitlos | Beibehalten |
| M03 | MaxMethodParameterCount | 4 (6 in Tests) | 🟢 | Zeitlos | Beibehalten |
| M04 | MaxCyclomaticComplexity | 12 | 🟢 | Zeitlos | Beibehalten |
| M05 | MaxCognitiveComplexity | 15 | 🟢 | Zeitlos | Beibehalten |
| M06 | MaxInheritanceDepth | 2 | 🟡 | Zeitlos | Auf 3 anheben oder Ausnahmen sicherstellen |
| M07 | MaxMethodOverloads | 3 | 🟡 | Offen | Auf 5 anheben |
| M08 | MaxConstructorDependencies | 5 | 🟢 | Zeitlos | Beibehalten |
| M09 | MaxDirectoryDepth | 4 | 🟢 | Zeitlos | Beibehalten |
| M10 | MaxDirectoryChildren | 0 (deaktiviert) | 🟡 | Offen | Deaktiviert lassen |
| M11 | MaxBoolParameterCount | 1 | 🟢 | Zeitlos | Beibehalten |
| M12 | MaxPartialClassFiles | 2 | 🟢 | Zeitlos | Beibehalten |
| M13 | MaxPublicMembersPerType | 15 | 🟢 | Zeitlos | Beibehalten |
| M14 | MaxAIContextFootprint | 5.000 | 🟢 | Modellspezifisch | Auf 2.500–3.000 absenken |
| M15 | MaxSwitchArms | 10 | 🟢 | Zeitlos | Beibehalten |
| M16 | MinCognitiveComplexityForTest | 3 | 🟢 | Zeitlos | Auf 5 anheben |
| M17 | CompoundSuppressions | 1 aktiv | 🟡 | Modellspezifisch | Beibehalten, keine weiteren Suppressions |

---

## Matrix: Boolean-Regeln

| ID | Feature | Aktuell | Bewertung | Zeitlich | Empfehlung |
|----|---------|---------|-----------|----------|------------|
| R01 | EnforceSealedClasses | true | 🟢 | Zeitlos | Aktiviert lassen |
| R02 | AllowDynamic (Verbot) | false | 🟢 | Zeitlos | Aktiviert lassen |
| R03 | AllowOutParameters (Verbot) | false | 🟢 | Zeitlos | Aktiviert lassen |
| R04 | AllowTryPatternOutParameters | true | 🟢 | Zeitlos | Aktiviert lassen |
| R05 | AllowCancellationShutdownCatch | true | 🟢 | Zeitlos | Aktiviert lassen |
| R06 | AllowOutParametersInPrivateMethods | true | 🟡 | Zeitlos | Aktiviert, langfristig enger fassen |
| R07 | EnforceValueObjectContracts | true | 🟢 | Zeitlos | Aktiviert lassen |
| R08 | EnableTestSentinel | true | 🟢 | Zeitlos | Aktiviert lassen |
| R09 | EnforcePascalCase | true | 🟢 | Zeitlos | Aktiviert lassen |
| R10 | EnforceXmlDocumentation | false | 🟢 | Modellspezifisch | Deaktiviert lassen |
| R11 | EnforceSemanticNaming | true | 🟢 | Zeitlos | Aktiviert lassen |
| R12 | EnforceNullableEnable | true | 🟢 | Zeitlos | Aktiviert lassen |
| R13 | EnforceNoSilentCatch | true | 🟢 | Zeitlos | Aktiviert lassen |
| R14 | EnforceMinimalApiAsParameters | false | 🟡 | Offen | Deaktiviert lassen |
| R15 | EnforceResultPatternOverExceptions | false | 🟡 | Offen | Deaktiviert lassen |
| R16 | EnforceExplicitStateImmutability | false | 🟡 | Offen | Deaktiviert lassen |
| R17 | PreventContextDependentOverloads | false | 🟡 | Offen | Deaktiviert lassen |
| R18 | EnforceNamespaceDirectoryMapping | true (suffix-match) | 🟢 | Zeitlos | Aktiviert lassen |
| R19 | DetectAndBanPhantomDependencies | true | 🟢 | Zeitlos | Aktiviert lassen |
| R20 | BanPublicNestedTypes | true | 🟢 | Zeitlos | Aktiviert lassen |

---

## Matrix: System-Features

| ID | Feature | Bewertung | Zeitlich | Empfehlung |
|----|---------|-----------|----------|------------|
| F01 | Baseline / Ratchet | 🟢 | Zeitlos | Beibehalten |
| F02 | Auto-Fix (--fix / --dry-run) | 🟢 | Zeitlos | Beibehalten, ggf. erweitern |
| F03 | Discovery-Commands | 🟢 | Zeitlos | Beibehalten, JSON-Output ergänzen |
| F04 | ProjectOverrides | 🟢 | Zeitlos | Beibehalten |
| F05 | PathOverrides | 🟡 | Zeitlos | Beibehalten, nicht aktiv bewerben |
| F06 | UiSeparation (Blazor / WPF) | 🟢 | Zeitlos | Beibehalten |
| F07 | FileFilters | 🟢 | Zeitlos | Beibehalten |
| F08 | ForbiddenNamespaceDependencies | 🟢 | Zeitlos | Beibehalten, aktiv konfigurieren |
| F09 | EnablePerformanceProfiling | 🟡 | Zeitlos | Deaktivieren (Default auf false) |

---

## Matrix: Neue Feature-Vorschläge

| ID | Vorschlag | Typ | Priorität | Aufwand |
|----|-----------|-----|-----------|---------|
| N01 | BanAsyncVoid | Boolean-Regel | 🟢 | Gering |
| N02 | BanBlockingTaskAccess | Boolean-Regel | 🟢 | Gering |
| N03 | MaxLinqChainLength | Numerische Metrik | 🟡 | Mittel |

---

## Offene Fragen

1. **M06-Ausnahmen:** Sind alle relevanten Framework-Basisklassen (ASP.NET, EF, xUnit) korrekt in der Ausnahmeliste konfiguriert? Ohne praktische Verifikation lässt sich nicht beurteilen, wie viele False Positives die Regel aktuell erzeugt.

2. **M14-Kalibrierung:** Der Schwellenwert von 2.500–3.000 transitiven Zeilen ist eine Ableitung aus dem "Lost in the Middle"-Befund. Praktische Messung an einer konkreten Codebasis wäre nötig, um zu prüfen, ob legitime große Klassen darunter fallen.

3. **R19-Implementierungstiefe:** Prüft DetectAndBanPhantomDependencies tatsächlich Semantic-Model-Auflösung für `using`-Direktiven, oder nur Syntaxanalyse? Nur Semantic-Model-basierte Prüfung macht die Regel wirkungsvoll.

4. **F08-Konfiguration:** Welche Namespace-Abhängigkeitsverbote sollten für das AiNetLinter-Repo selbst gelten? Das ist die konkrete Entscheidung die vor der Aktivierung getroffen werden muss.

5. **Meta-Hypothese (Cluster H):** Direkte empirische Belege dafür, dass AiNetLinter-Compliance die Agenten-Fehlerrate messbar senkt, fehlen. Die gesamte Evaluation läuft über indirekte Kausalketten. Ein eigener Benchmark (AiNetLinter-konformes Projekt vs. nicht-konformes Projekt mit identischen Agent-Tasks) wäre die stärkste mögliche Validierung.

---

## Empfohlene nächste Schritte

- [ ] M01: MaxLineCount von 700 auf 500 senken
- [ ] M14: MaxAIContextFootprint von 5.000 auf 2.500–3.000 senken
- [ ] M06: MaxInheritanceDepth von 2 auf 3 anheben und Framework-Ausnahmeliste verifizieren
- [ ] M07: MaxMethodOverloads von 3 auf 5 anheben
- [ ] M16: MinCognitiveComplexityForTest von 3 auf 5 anheben
- [ ] F09: EnablePerformanceProfiling als Default auf `false` setzen
- [ ] F08: Mindestens zwei Namespace-Abhängigkeitsverbote für das eigene Projekt konfigurieren
- [ ] N01: BanAsyncVoid implementieren (Boolean-Regel, Gering-Aufwand)
- [ ] N02: BanBlockingTaskAccess implementieren (Boolean-Regel, Gering-Aufwand)
- [ ] F03: `--list-rules --format json` als maschinenlesbaren Output ergänzen
- [ ] N03: MaxLinqChainLength als mittelfristigen Kandidaten prüfen
- [ ] Meta-Hypothese: Kleinen empirischen Vergleichstest aufsetzen (zwei Codebasen, identische Agent-Tasks, messen ob Compliance die Fehlerrate senkt)
