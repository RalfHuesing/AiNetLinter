# AiNetLinter - Projekt-Roadmap

Diese Roadmap dokumentiert den aktuellen Entwicklungsstand des `AiNetLinter`-Projekts und teilt die Features in logische Epics und Kapitel auf. Sie dient als Arbeitsgrundlage für die schrittweise Implementierung.

---

## Epic 1: Bootstrapping & Infrastruktur
- [x] Initialisierung der Projektstruktur mit `.slnx` (Solution) und `.csproj`
- [x] Einrichtung der globalen AI-Richtlinien (`.cursor/rules/AiNetLinterRichtlinien.mdc`)
- [x] Definition der Konfigurationsstruktur (`LinterConfig.cs`)
- [x] Definition der Fehlermodelle (`RuleViolation.cs`)
- [x] Implementierung des CLI-Einstiegspunkts (`Program.cs`) mit Argument-Parsing
- [x] Setup des xUnit v3 Testprojekts (`AiNetLinter.Tests`) und Integration in die Solution

---

## Epic 2: Core Roslyn Rules Implementation
- [x] **Regel: EnforceSealedClasses** – Zwingt konkrete Klassen zu `sealed`
- [x] **Regel: AllowDynamic** – Verbietet `dynamic` Typisierung
- [x] **Regel: AllowOutParameters** – Verbietet `out`-Parameter
- [x] **Regel: MaxLineCount** – Validiert maximale Zeilenanzahl pro Datei
- [x] **Regel: MaxMethodParameterCount** – Validiert Parameterlimit pro Methode
- [x] **Regel: MaxCyclomaticComplexity** – McCabe-Komplexität über Roslyn analysieren
- [x] **Regel: MaxCognitiveComplexity** – Kognitive Komplexität nach SonarSource-Standard analysieren

---

## Epic 3: Project & Solution Parsing
- [x] Parse moderne `.slnx`-Dateien (XML-basiert), um enthaltene Projekte zu extrahieren
- [x] Parse klassische `.sln`-Dateien, falls vorhanden
- [x] Parse `.csproj`-Dateien, um alle kompilierten `.cs`-Quelldateien zu identifizieren
- [x] Ignorieren von generierten oder transienten Code-Dateien (z. B. `obj/`, `bin/`, `.vs/`)

---

## Epic 4: CLI Interface & AI-Actionable Output
- [x] Ausgangs-Exit-Codes definieren (0 = Erfolg, 1 = Regelbrüche, >1 = Fatale Fehler)
- [x] Strukturierte, maschinenlesbare AI-Fehlermeldungen auf `stdout` ausgeben
- [x] Unterstützung von Verbose-Logging (`--verbose` oder `-v`)

---

## Epic 5: Self-Testing CLI Integration (Dogfooding)
- [x] Erstellung einer zentralen `rules.json` für den Eigenlauf des Tools
- [x] Implementierung von Integrationstests, die den kompilierten Linter (`AiNetLinter.dll` / `.exe`) auf die eigene Codebase loslassen
- [x] Automatisches Einbinden des Linters in den `dotnet test` Build-Prozess (Integrationstest führt CLI auf gesamtem src/ Ordner aus)

---

## Epic 6: Future Capabilities (Roadmap)
- [x] **Namespace-Kopplung (Vertical Slices):** Verbot von unerlaubten slice-übergreifenden Abhängigkeiten (mittels ForbiddenNamespaceDependencies)
- [x] **Maschinenlesbare Verträge (Contracts):** Unterstützung strukturierter Typ-Verträge (durch Prüfung von *ValueObject Suffix)
- [x] **Traceability-Graphen:** Analyse von Seiteneffekten bei Code-Änderungen (Generierung von Mermaid-Projekt-Abhängigkeitsgraphen)
- [x] **Static Test Sentinel:** Statische Test-Präsenzprüfung für hochrelevante Codeabschnitte

---

## Epic 7: Tokenizer- & Semantik-Optimierung (BPE & LSP)
- [x] **PascalCase-Validierung:** Statische Typprüfung, dass alle Klassen, Structs, Records, Interfaces, Methoden und Properties strikt in PascalCase geschrieben sind (optimiert die Token-Zerlegung von Byte-Pair-Encoding Tokenizern).
- [x] **XML-Doc-Obligatorium für Public APIs:** Zwingende Präsenz von `/// <summary>` Dokumentationen an allen öffentlichen Klassen und Methoden (damit AI-Agenten die Absicht über Language Server Protocol / LSP direkt im Kontext verstehen).
- [x] **Erkennung generischer Bezeichner:** Erkennung und Flagging von nicht-semantischen Parameternamen (z. B. `data`, `temp`, `obj`, `val`) in öffentlichen Methodenschnittstellen.

---

## Epic 8: Agent-Resilienz & Fehleranalyse (Compiler-Leitplanken)
- [x] **Nullable-Präsenzprüfung:** Überprüfung, ob `#nullable enable` in jeder Datei deklariert ist oder global erzwungen wird, um LLM-bedingte NullReferenceExceptions zu minimieren.
- [x] **Vermeidung stummer Catch-Blöcke (Silent Swallowing):** Warnung bei leeren `catch`-Blöcken oder bei Blocks, die Exceptions ohne Logging/Rethrow verschlucken (dies bricht die Fehlerkorrektur des agentischen Loops).
- [x] **Limitierung der Vererbungstiefe (MaxInheritanceDepth):** Begrenzung der Vererbungshierarchie (z. B. max. Tiefe von 2), um "Context Dispersion" zu verhindern (LLMs müssen nicht über mehrere Quelldateien hinweg vererbte Member rekonstruieren).

---

## Epic 9: Architektur-Bereinigung & Fehlerbehebung (Critical Architecture Updates)
- [x] **ClassMap Namespace-Awareness:** Erweitere die Klassen- und Vererbungserkennung so, dass Klassen anhand ihres vollqualifizierten Namens (Namespace + Klassenname) eindeutig identifiziert werden. Löst den Absturz-Bug (`Duplicate Key Exception` im `ToDictionary`) bei gleichnamigen Klassen in unterschiedlichen Namespaces auf.
- [x] **Konfigurierbarer Sentinel-Schwellenwert:** Mache den Kognitiven Komplexitäts-Schwellenwert (bisher hartcodiert auf `3`) in der `MetricsConfig` (z. B. `MinCognitiveComplexityForTest`) konfigurierbar, statt ihn fest im Code zu verankern.
- [x] **Robuste globale Nullable-Erkennung:** Erweitere die Erkennung globaler Nullable-Einstellungen so, dass sie rekursiv nach oben in `Directory.Build.props` und `.csproj` Dateien sucht und nicht beim ersten Fund einer leeren csproj die Suche abbricht.
- [x] **Laufzeit-Fehlerbehandlung für Dateizugriffe:** Reiche IO-Exceptions beim Lesen von Quellcodedateien als fatalen CLI-Fehler nach oben (Exit-Code `2` / stderr) anstatt sie als Regelverstöße im Ergebnisbericht unterzubringen.

---

## Epic 10: Erweiterte Analyse & CI/CD-Integration (Extensions & Best Practices)
- [x] **Syntaktische Typ-Analyse für verbotene Namespace-Kopplungen:** Durchsuche den Quellcode nach der Verwendung von vollqualifizierten Typnamen (in `QualifiedNameSyntax` und `MemberAccessExpressionSyntax`), die gegen die konfigurierten Namespace-Kopplungen verstoßen (auch wenn kein `using`-Statement verwendet wird).
- [x] **Sicherer Test Sentinel:** Stelle sicher, dass gefundene Testklassen tatsächliche Testmethoden (mit `[Fact]`, `[Theory]`, `[Test]` oder `[TestMethod]` Attributen) enthalten, um zu verhindern, dass leere Testdateien den Sentinel austricksen.
- [x] **SARIF CLI-Ausgabeformat:** Füge die Option `--format sarif` hinzu, um die Regelverstöße im standardisierten SARIF-Format (Static Analysis Results Interchange Format) auf `stdout` auszugeben, zur direkten Integration in GitHub Actions/GitLab CI.

---

## Epic 11: Roslyn Workspace & Semantische Analyse (Roslyn Workspace Refactoring)
- [x] **Integration von MSBuildWorkspace & MSBuildLocator:** Binde die benötigten NuGet-Pakete ein und initialisiere den MSBuildWorkspace zur vollständigen Evaluierung der Solution-Struktur (.sln / .slnx).
- [x] **Umstellung auf Solution-weites Laden:** Ersetze das textbasierte Parsen einzelner Dateien durch das Laden der Solution in den Speicher und das Abfragen der `Compilation` und des `SemanticModel` pro Dokument.
- [x] **Semantische Vererbungstiefen-Prüfung:** Nutze `INamedTypeSymbol.BaseType` des semantischen Modells, um die exakte Vererbungshierarchie über Projektgrenzen hinweg ohne textbasierte Heuristiken zu ermitteln.
- [x] **Semantische Nullable-Prüfung:** Nutze `compilation.Options.NullableContextOptions`, um die Nullability-Einstellungen direkt vom Compiler auswerten zu lassen (inkl. Directory.Build.props und konditionaler Flags).
- [x] **Semantische Namespace-Kopplungs-Prüfung:** Analysiere Symbol-Referenzen über `SemanticModel.GetSymbolInfo`, um unerlaubte Namespace-Abhängigkeiten zuverlässig auf Typ- und Member-Ebene zu erkennen.
- [x] **Bereinigung von veraltetem Code:** Entferne obsolete textbasierte Heuristiken (manuelles Csproj-Parsing, manuelle Dateisuchen und String-basierte Namespace-Suchen).

---

## Epic 12: Audit Remediation & CLI Robustness
- [x] **Semantische Testerkennung:** Nutze `SemanticModel.GetSymbolInfo(attr).Symbol` in `LinterAnalyzer.cs`, um echten Namespace/Typ von Test-Attributen (`Xunit`, `NUnit`, `Microsoft.VisualStudio.TestTools.UnitTesting`) zu prüfen statt unzuverlässiger Textsuche.
- [x] **Consolidated Syntax Walk (Performance):** Führe `ClassCollector` und `LinterAnalyzer` zusammen, um Klasseninfos direkt beim ersten Syntax-Walk zu erheben und redundantes Syntax-Walking zu verhindern. Lösche die obsolete Klasse `ClassCollector.cs`.
- [x] **System.CommandLine Integration:** Ersetze das fragile manuelle CLI-Argument-Parsing durch die offizielle `System.CommandLine`-Bibliothek zur robusten Parameter- und Flag-Validierung.
- [x] **Robuste dynamic-Erkennung:** Überprüfe `dynamic` über das `SemanticModel` (`TypeKind.Dynamic`), um unberechtigte Fehlermeldungen bei lokalen Variablen namens `dynamic` zu vermeiden.
- [x] **Unterstützung für ainetlinter-disable:** Erlaube das Unterdrücken von Linter-Warnungen über inline Kommentare wie `// ainetlinter-disable [RuleName]` oder dateiweit.
- [x] **Projektbasierte Test-Dateierkennung:** Bestimme Testprojekte dynamisch durch Analyse ihrer referenzierten Test-Assemblies (`xunit`, `nunit` etc.) im MSBuild-Projekt, um fragile Dateipfad-Heuristiken abzulösen.
- [ ] **Sequentielles Projekt-Laden für riesige Solutions (Performance & RAM):** Möglichkeit implementieren, Projekte im MSBuildWorkspace sequentiell zu laden und zu entladen, um Out-of-Memory Exceptions bei großen Monolithen vorzubeugen.



