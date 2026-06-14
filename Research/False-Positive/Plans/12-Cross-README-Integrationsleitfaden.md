# Plan 12 — Cross-Cutting: README Integrationsleitfaden

**Priorität:** Parallel zu den anderen Plänen, abschließend nach jedem implementierten Plan aktualisieren  
**Regeln:** [`.cursor/rules/AiNetLinterRichtlinien.mdc`](../../../.cursor/rules/AiNetLinterRichtlinien.mdc) — Update-Pflicht bei Feature-Änderungen

---

## Ziel

Das README muss nach Implementierung der False-Positive-Korrekturen **vollumfänglich** sein, sodass ein Integrator (oder ein LLM/Agent) anhand von `AiNetLinter.exe` und README das Tool selbständig in ein Projekt integrieren kann — ohne weitere Rückfragen.

---

## Betroffene Dateien

| Datei | Aktion |
|-------|--------|
| `README.md` | Vollständig überarbeiten/erweitern |
| `rules.json` | Als Referenz-Vorlage aktualisieren |
| `.cursor/rules/AiNetLinter.mdc` | Automatisch regeneriert (nach `--sync-cursor-rules`) |

---

## Anforderungen an die README-Struktur

### Pflicht-Abschnitte (neu oder erweitert)

```
# AiNetLinter

## Überblick
## Schnellstart (5 Minuten)
## CLI-Referenz
## rules.json — vollständige Konfigurationsreferenz
  ### Global-Schalter
  ### Metriken (Metrics)
  ### MagicValues-Konfiguration [NEU — Plan 02]
  ### FileFilters-Konfiguration [NEU — Plan 08]
  ### TestSentinel-Konfiguration
  ### ProjectOverrides
  ### ForbiddenNamespaceDependencies
## Profil-Strategien
  ### platform-default
  ### platform-ai-strict
  ### WPF-Profil [NEU]
  ### Blazor-Profil [NEU]
## Integration durch LLM/Agent
## SARIF / CI-Integration
## Suppression-Mechanismus
## Baseline-Workflow
## Codegraph und Playbook
## ROADMAP
```

---

## Schnellstart-Abschnitt (konkrete Anforderungen)

Muss 5 Schritte umfassen:

```markdown
## Schnellstart

### 1. Download
AiNetLinter.exe herunterladen von GitHub Releases.

### 2. Basis-Analyse
```powershell
AiNetLinter.exe --path C:\MyProject --config rules.json
```

### 3. Minimale rules.json
```json
{
  "Global": { ... },
  "Metrics": { ... }
}
```

### 4. Cursor-Regeln synchronisieren
```powershell
AiNetLinter.exe --path C:\MyProject --config rules.json --sync-cursor-rules
```

### 5. Playbook generieren
```powershell
AiNetLinter.exe --path C:\MyProject --config rules.json --playbook
```
```

---

## CLI-Referenz (vollständig)

Alle Flags mit Beschreibung, Typ und Beispiel:

| Flag | Beschreibung | Beispiel |
|------|-------------|---------|
| `--path` | Pfad zur .sln oder Projektordner | `--path C:\MyProject` |
| `--config` | Pfad zur rules.json | `--config rules.json` |
| `--wave-ready` | Nur Dateien analysieren die nicht `// ainetlinter-disable-all` haben | |
| `--sync-cursor-rules` | Cursor-Rules-Datei aus rules.json regenerieren | |
| `--playbook` | Playbook-MD für Agenten generieren | |
| `--only-changed` | Nur git-geänderte Dateien analysieren | |
| `--baseline` | Baseline-JSON-Pfad zum Vergleich | |
| `--write-baseline` | Baseline aus aktuellem Lauf schreiben | |
| `--sarif` | SARIF-Report ausgeben | |
| `--exit-code` | Exit-Code 1 bei Verstößen (CI) | |
| `--disable-all` | Alle Dateien mit `// ainetlinter-disable-all` kommentieren | |
| `--remove-disable-all` | `// ainetlinter-disable-all` Kommentare entfernen | |

---

## Vollständige `rules.json`-Referenz

Jede Option mit Typ, Default-Wert und Ein-Zeilen-Erklärung:

### GlobalConfig:

| Schlüssel | Typ | Default | Beschreibung |
|-----------|-----|---------|-------------|
| `EnforceSealedClasses` | bool | `true` | Konkrete Klassen müssen sealed sein |
| `AllowUnsealedPartialClasses` | bool | `false` | Partial-Klassen ohne sealed erlauben (WPF) |
| `SealedClassExemptSuffixes` | string[] | `[]` | Name-Suffixe für nicht-sealed Basisklassen [NEU Plan 03] |
| `AllowDynamic` | bool | `false` | `dynamic` erlauben |
| `AllowOutParameters` | bool | `false` | `out`-Parameter erlauben |
| `AllowTryPatternOutParameters` | bool | `true` | `out` in Try*-Methoden erlauben |
| `EnforceValueObjectContracts` | bool | `true` | *ValueObject muss record/readonly struct sein |
| `EnableTestSentinel` | bool | `true` | StaticTestSentinel aktiv |
| `EnforcePascalCase` | bool | `true` | PascalCase für öffentliche Typen/Methoden |
| `EnforceSemanticNaming` | bool | `true` | Keine generischen Namen in öffentlichen Signaturen |
| `EnforceNullableEnable` | bool | `true` | #nullable enable am Dateianfang |
| `EnforceNoSilentCatch` | bool | `true` | Stille Catches verboten |
| `AllowCancellationShutdownCatch` | bool | `true` | Stille Cancellation-Exception-Catches erlaubt |
| `EnforceNoMagicValues` | bool | `false` | Magic Values verboten |
| `EnforceExplicitStateImmutability` | bool | `false` | Strikte Immutability (Strict-Profile) |
| `ImmutabilityExemptSuffixes` | string[] | `["Dto","Entity","Model",...]` | Suffix-Ausnahmen für Immutability |
| `ImmutabilityExemptPatterns` | string[] | `[]` | Wildcard-Muster-Ausnahmen |
| `ImmutabilityExemptBaseTypes` | string[] | `[]` | Basistyp-Ausnahmen (Blazor/WPF) [NEU Plan 05] |
| `ImmutabilityAllowPrivateBackingFields` | bool | `false` | Private `_`-Felder exempt [NEU Plan 05] |
| `EnforceResultPatternOverExceptions` | bool | `false` | throw → Result<T> (Strict) |
| `ResultPatternAllowThrowInNamespaceSuffixes` | string[] | `[]` | Namespace-Suffixe wo throw erlaubt [NEU Plan 11] |
| `ResultPatternAllowCatchRethrow` | bool | `true` | bare throw; in Catch immer erlaubt [NEU Plan 11] |
| `EnforceStrictBoundaryForBusinessLogic` | bool | `false` | I/O in Business Logic verboten |
| `EnforceNamespaceDirectoryMapping` | bool | `false` | Namespace = Ordnerpfad |
| `NamespaceDirectoryMappingMode` | string | `"exact"` | exact/suffix-match/contains-all [NEU Plan 09] |
| `NamespaceDirectoryMappingIgnorePathSegments` | string[] | `[]` | Ignorierte Pfad-Segmente [NEU Plan 09] |
| `NamespaceDirectoryMappingRequiredTrailingSegments` | int | `2` | Trailing Segmente für suffix-match [NEU Plan 09] |
| `DetectAndBanPhantomDependencies` | bool | `false` | Nicht auflösbare using verboten |
| `PreventContextDependentOverloads` | bool | `false` | Primitive Überladungen verboten |
| `RequireExplicitTruncationHandling` | bool | `false` | Truncation-Handling für async I/O |
| `AllowedEmptyReads` | bool | `false` | Leere Lesoperationen erlaubt |

### MetricsConfig:

| Schlüssel | Typ | Default | Beschreibung |
|-----------|-----|---------|-------------|
| `MaxLineCount` | int | 500 | Max. Zeilen pro Datei |
| `MaxMethodLineCount` | int | 42 | Max. Code-Zeilen pro Methode |
| `MaxMethodParameterCount` | int | 4 | Max. Parameter pro Methode |
| `MaxCyclomaticComplexity` | int | 5 | Max. McCabe-Komplexität |
| `MaxCognitiveComplexity` | int | 5 | Max. Kognitive Komplexität |
| `ComplexityNearMissTolerance` | int | 0 | Toleranzbereich für Near-Miss [NEU Plan 06] |
| `ExcludeSwitchDispatcherCases` | bool | `false` | Switch-Dispatcher exempt [NEU Plan 06] |
| `SwitchDispatcherMaxCaseBodyLines` | int | 3 | Max. Case-Zeilen für Dispatcher [NEU Plan 06] |
| `MaxInheritanceDepth` | int | 2 | Max. Vererbungstiefe |
| `InheritanceDepthFrameworkPrefixes` | string[] | `[]` | Framework-NS ignorieren [NEU Plan 01] |
| `MaxMethodOverloads` | int | 3 | Max. Überladungen pro Methodenname |
| `MaxConstructorDependencies` | int | 5 | Max. Konstruktor-Parameter |
| `ConstructorDependencyIgnoreTypePrefixes` | string[] | `[]` | Framework-Typen ignorieren [NEU Plan 07] |
| `MaxDirectoryDepth` | int | 4 | Max. Ordnertiefe ab csproj |
| `MaxAIContextFootprint` | int | 5000 | Max. transitive Codezeilen |
| `MinCognitiveComplexityForTest` | int | 3 | Min. Komplexität für TestSentinel |
| `AggregatePartialClassLineCount` | bool | `false` | Partial-Klassen aggregiert zählen |

---

## Integration durch LLM/Agent

Dieser Abschnitt erklärt, wie ein LLM oder Agent AiNetLinter nutzen soll:

```markdown
## Integration durch LLM/Agent

### Workflow für Agenten

1. **Vor einer Änderung:** Codegraph und Playbook lesen
   ```
   Docs/codegraph.md       — Abhängigkeitsgraph
   Docs/playbook.md        — Architektur-Status, Top-Verstöße
   .cursor/rules/AiNetLinter.mdc — Aktive Regeln und Limits
   ```

2. **Nach einer Änderung:** Linter ausführen
   ```powershell
   AiNetLinter.exe --path . --config rules.json
   ```

3. **Verstöße interpretieren:**
   - Verstöße mit `intent: agent-context` haben hohe LLM-Relevanz
   - Verstöße mit `intent: agent-resilience` (EnforceNoSilentCatch) direkt beheben
   - Verstöße mit `intent: architecture` nur mit Rücksprache beheben

4. **Suppression bei unvermeidbaren Verstößen:**
   ```csharp
   // ainetlinter-disable EnforceNoSilentCatch
   catch (Exception) { }
   
   catch (Exception ignored) { }  // Alternativ: Variable "ignored" benennen
   ```

### Zwei-Stufen-Modell

| Profil | Zweck |
|--------|-------|
| `platform-default` | Produktiv — Agenten können Verstöße direkt beheben |
| `platform-ai-strict` | Teleskop — zeigt Zielrichtung, keine Pflicht zum sofortigen Fix |
```

---

## Profil-Vorlagen

### WPF-Profil (`wpf.rules.json`):

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": true,
    "SealedClassExemptSuffixes": ["Base", "ViewModel"],
    "EnforceNoSilentCatch": true,
    "AllowCancellationShutdownCatch": true,
    "ImmutabilityExemptBaseTypes": ["ObservableObject", "ObservableRecipient", "INotifyPropertyChanged"]
  },
  "Metrics": {
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": ["System.", "System.Windows.", "Microsoft.UI."],
    "MaxConstructorDependencies": 5,
    "ConstructorDependencyIgnoreTypePrefixes": ["ILogger", "IOptions", "IHostEnvironment"]
  },
  "FileFilters": {
    "ExcludeFilePatterns": ["*.designer.cs", "*.g.cs"]
  },
  "TestSentinel": {
    "ExemptClassNameSuffixes": ["Converter", "Extensions", "Constants"],
    "ExemptWhenInheritsFrom": ["IValueConverter"]
  }
}
```

### Blazor-Profil (`blazor.rules.json`):

```json
{
  "Global": {
    "EnforceSealedClasses": true,
    "AllowUnsealedPartialClasses": true,
    "ImmutabilityExemptBaseTypes": ["ComponentBase", "LayoutComponentBase", "AuthenticationStateProvider", "BackgroundService"]
  },
  "Metrics": {
    "MaxInheritanceDepth": 2,
    "InheritanceDepthFrameworkPrefixes": ["Microsoft.AspNetCore.", "Microsoft.Extensions."],
    "ConstructorDependencyIgnoreTypePrefixes": ["ILogger", "IOptions", "IHttpContextAccessor"]
  },
  "FileFilters": {
    "ExcludeFilePatterns": ["*.g.cs", "*.generated.cs"]
  },
  "TestSentinel": {
    "ExemptWhenInheritsFrom": ["ComponentBase", "LayoutComponentBase"],
    "ExemptClassNameSuffixes": ["Extensions", "Constants"]
  }
}
```

---

## Suppression-Mechanismus

Vollständige Dokumentation:

```markdown
### Inline-Suppression (einzelne Zeile)
```csharp
// ainetlinter-disable EnforceNoSilentCatch
catch (Exception) { }
```

### Datei-Suppression (gesamte Datei)
```csharp
// ainetlinter-disable-all
```
(Am Dateianfang — überspringt die Datei komplett bei --wave-ready)

### Explizite Exception-Variable
```csharp
catch (Exception ignored) { }      // von EnforceNoSilentCatch ausgenommen
catch (Exception expected) { }     // ebenfalls ausgenommen
```
```

---

## Architektur-Hinweise / Was sich ändern muss

Die README-Überarbeitung ist kein Code-Change, aber nach jedem implementierten Plan aus 01–11 muss folgendes aktualisiert werden:

1. **rules.json** — neue Optionen als kommentiertes Beispiel
2. **README.md** — neue Option in Konfigurationsreferenz-Tabelle
3. **`.cursor/rules/AiNetLinter.mdc`** — wird durch `--sync-cursor-rules` automatisch regeneriert (Update-Pflicht laut `AiNetLinterRichtlinien.mdc`)
4. **ROADMAP.md** — implementierte Punkte als Done markieren

### Checkliste pro implementiertem Plan:

- [ ] Plan 01: `InheritanceDepthFrameworkPrefixes` in README-Tabelle + WPF-Profil
- [ ] Plan 02: `MagicValues`-Sektion + 3 Profile-Snippets
- [ ] Plan 03: `SealedClassExemptSuffixes` + WPF-Profil-Snippet
- [ ] Plan 04: `EnforceNoSilentCatch`-Behavior-Beschreibung aktualisieren
- [ ] Plan 05: `ImmutabilityExemptBaseTypes` + Blazor/WPF-Profil
- [ ] Plan 06: `ComplexityNearMissTolerance` + `ExcludeSwitchDispatcherCases`
- [ ] Plan 07: `ConstructorDependencyIgnoreTypePrefixes`
- [ ] Plan 08: `FileFilters`-Sektion komplett
- [ ] Plan 09: `NamespaceDirectoryMappingMode` + Modi-Beschreibung
- [ ] Plan 10: `TestSentinel.ExemptClassNameSuffixes` + Coverage-Muster
- [ ] Plan 11: `ResultPatternAllowThrowInNamespaceSuffixes`
