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
- [ ] **Namespace-Kopplung (Vertical Slices):** Verbot von unerlaubten slice-übergreifenden Abhängigkeiten
- [ ] **Maschinenlesbare Verträge (Contracts):** Unterstützung strukturierter Typ-Verträge
- [ ] **Traceability-Graphen:** Analyse von Seiteneffekten bei Code-Änderungen
- [ ] **Static Test Sentinel:** Statische Test-Präsenzprüfung für hochrelevante Codeabschnitte
