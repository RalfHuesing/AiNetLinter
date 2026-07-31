---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-07-31
open_questions: []
---

# Konzept: Granularer Bypass-Modus für Suppressions (`--ignore-suppressions`)

## Ziel (Was)

Einführung des CLI-Schalters `--ignore-suppressions`, mit dem während des Linter-Laufs Suppressions (sowohl dateiweite als auch inline) in Quelldateien für ausgewählte Sprachklassen (`all`, `cs`/`c#`, `razor`, `js`, `css`) komplett ignoriert/deaktiviert werden können. Zusätzlich soll in allen Berichten und CLI-Outputs transparent im Header ausgewiesen werden, ob und für welche Sprachen dieser Bypass-Modus aktiv ist.

## Warum / Kontext

Zur genauen Qualitätsmessung und zur Ermittlung des tatsächlichen "Technical Debts" (Code-Schulden) ohne Verfälschung durch temporäre oder permanente Suppressions (`// ainetlinter-disable ...`, `@* ... *@`, `/* ... */`) wird eine Möglichkeit benötigt, Suppressions beim Scan temporär auszuschalten, ohne die Quelldateien physisch verändern zu müssen.

## Scope

### Muss-Haben

- CLI-Option `--ignore-suppressions` mit Unterstützung für:
  - Aufruf ohne Argument (Default: `all`)
  - Kommagetrennte oder beistandene Werteliste (z. B. `--ignore-suppressions cs,razor` oder `--ignore-suppressions=c#,razor`).
- Normalisierung und Validierung der Sprachen (`all`, `cs`/`c#`, `razor`, `js`, `css`). Unbekannte Eingaben führen zu einer klaren Fehlermeldung.
- Kanonische Darstellung in Ausgaben: Sowohl `cs` als auch `c#` werden als Input akzeptiert, in Header-Outputs wird kanonisch `cs` verwendet (Vermeidung von Shell-Zeichenproblemen mit `#`).
- Vollständige Ignorierung aller Suppressions (dateiweit & inline) in allen betroffenen Dateien der gewählten Sprachklassen.
- Transparente Ausweisung des aktiven Ignore-Modus im Header von CLI-Outputs, Debt-Report (`DebtReportBuilder`) und Playbook-Output (z. B. `[Ignore-Suppressions: cs, razor]`).
- Unit- & Integrationstests für alle Parameterkombinationen und Sprachklassen.
- Aktualisierung von `Docs/configuration.md`, `Docs/ROADMAP.md` und `README.md`.

### Nice-to-Have (optional, spätere Iteration)

- Keine speziellen Zusatz-Optionen außerhalb der Spezifikation in der Roadmap.

### Non-Goals (bewusst NICHT Teil davon)

- Dauerhaftes Entfernen/Löschen von Suppression-Kommentaren aus Quelldateien (dafür existiert `--remove-disable-all`).
- Unterstützung für Sprach-Wildcards oder Regex in `--ignore-suppressions`.

## Zielplattformen / Technischer Rahmen

- **C# / .NET 10** CLI Tool (`AiNetLinter`).
- **System.CommandLine**: Registrierung der Option in `CliOptionFactory.cs` & `CliOptions.cs`.

## Verworfene Alternativen

- **Separater Boolean-Flag `--ignore-all-suppressions`:** verworfen, da die Spezifikation in der Roadmap explizit eine granulare Sprachauswahl (`cs`, `razor`, `js`, `css`) fordert.
- **Reines `c#` als exklusiver Bezeichner:** verworfen zugunsten von `cs` (mit `c#` als Parsing-Alias), da `#` in Unix- und manchen Windows-Shells unbeabsichtigt als Kommentarzeichen gewertet werden kann, wenn Anführungszeichen fehlen.

## Wo im Projekt

- [CliOptions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliOptions.cs): Option-Definition & Record-Erweiterung für ParsedArgs.
- [CliOptionFactory.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliOptionFactory.cs): Erstellung und Konfiguration der `--ignore-suppressions` System.CommandLine Option.
- [LinterArgs.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/LinterArgs.cs): Argument-Parsing & Validierung/Normalisierung der übergebenen Sprachen.
- [SuppressionScanner.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Suppression/SuppressionScanner.cs): Filterung / Überspringen der Scans je nach aktiver Ignore-Sprachauswahl.
- [DebtReportBuilder.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Output/DebtReportBuilder.cs): Header-Ausgabe des aktiven Ignore-Modus in Berichten.

## Entdeckte Mängel/Redundanzen

- **SuppressionScanner & Web-Analyzer Entkopplung**
  - **Gefunden:** Web-Analyzer (JS/CSS/Razor) und C#-Analysen verarbeiten Suppressions aktuell an leicht unterschiedlichen Stellen.
  - **Vorschlag:** Einführung einer zentralen `IgnoreSuppressionsFilter`- oder `SuppressionMode`-Struktur, die konsistent von allen Analyzer-Komponenten abgefragt werden kann.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben).

## Wie (grober Ansatz)

1. CLI-Option `--ignore-suppressions` in `CliOptions` und `CliOptionFactory` integrieren (`Arity = ArgumentArity.ZeroOrMore` oder `ZeroOrOne` mit Parsing).
2. Enum oder ValueObject `IgnoreSuppressionsMode` (mit Flags oder `HashSet<LanguageKind>`) zur Repräsentation des Bypass-Zustands definieren.
3. Einbinden der Logik in den Analyse-Pipeline-Standard (Auswertung bei `SuppressionScanner` und den jeweiligen Web/C#-Analyzern).
4. Erweitern des Output- / Header-Renderings in CLI & Debt-Report um den `[Ignore-Suppressions: ...]` Hinweis.
5. Unit-Tests schreiben & Dokumentation (`ROADMAP.md`, `configuration.md`, `README.md`) aktualisieren.

## Definition of Done / Erfolgskriterien

- `dotnet test` läuft 100% grün durch.
- `--ignore-suppressions` (ohne Wert) ignoriert alle Suppressions in C#, Razor, JS und CSS.
- `--ignore-suppressions cs,razor` ignoriert nur Suppressions in C# und Razor-Dateien; JS und CSS behalten ihre Suppressions.
- Berichte und CLI-Outputs enthalten im Header `[Ignore-Suppressions: <sprachen>]`.
- Dokumentation (`ROADMAP.md`, `configuration.md`, `README.md`) ist aktualisiert.

## Offene Punkte

*Keine. Das Konzept ist vollständig geklärt und bereit für die Umsetzung.*
