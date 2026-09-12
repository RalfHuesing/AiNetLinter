# 04 – get_file_skeleton

## 1. Tool-Steckbrief & Agenten-Rolle
- **Name:** `get_file_skeleton`
- **Agenten-Priorität:** Prio 04 (Hoch – Schneller struktureller Überblick über eine C#-Datei ohne Body-Overhead)
- **Hauptzweck:** Liefert die Gliederung einer Datei (Typen, Interfaces, Member-Signaturen) ohne Quelltextrümpfe. Unterstützt Batching über mehrere Dateien.
- **Wichtigste Parameter:**
  - `targetPath` (Pflicht, String): Absoluter Pfad zur `.sln`/`.slnx` oder `.dll`.
  - `filePaths` (Pflicht, Array von Strings): Relative oder absolute Dateipfade.
  - `maxResponseBytes` (Optional, Int, Default 24576): UTF-8-Nutzlastgrenze.

---

## 2. Test-Samples & Rohe Tool-Outputs

### Sample A: Einzeldatei mit Interface (`ILinterEngineConfig.cs`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "filePaths": ["src/AiNetLinter/Configuration/ILinterEngineConfig.cs"]
}
```
- **Tool-Output (Rohdaten):**
```text
# AiNetLinter — Skeleton Map

> Typen: 1 | Member: 11 | Pfad: src/AiNetLinter/Configuration/ILinterEngineConfig.cs

---

## AiNetLinter.Configuration

### ILinterEngineConfig — `src/AiNetLinter/Configuration/ILinterEngineConfig.cs`

```csharp
GlobalConfig Global { get; }
MetricsConfig Metrics { get; }
TestSentinelConfig TestSentinel { get; }
FileFiltersConfig FileFilters { get; }
UiSeparationConfig UiSeparation { get; }
WebConfig Web { get; }
IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; }
IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; }
IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; }
IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; }
string? SolutionBasePath { get; }
```
```

### Sample B: Batch-Abfrage zweier Dateien
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "filePaths": [
    "src/AiNetLinter/Configuration/ILinterEngineConfig.cs",
    "src/AiNetLinter/Models/RuleViolationFactory.cs"
  ]
}
```
- **Tool-Output (Rohdaten):**
```text
# AiNetLinter — Skeleton Map

> Typen: 1 | Member: 11 | Pfad: src/AiNetLinter/Configuration/ILinterEngineConfig.cs

---

## AiNetLinter.Configuration

### ILinterEngineConfig — `src/AiNetLinter/Configuration/ILinterEngineConfig.cs`

```csharp
GlobalConfig Global { get; }
MetricsConfig Metrics { get; }
TestSentinelConfig TestSentinel { get; }
FileFiltersConfig FileFilters { get; }
UiSeparationConfig UiSeparation { get; }
WebConfig Web { get; }
IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; }
IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; }
IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; }
IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; }
string? SolutionBasePath { get; }
```

---

# AiNetLinter — Skeleton Map

> Typen: 1 | Member: 1 | Pfad: src/AiNetLinter/Models/RuleViolationFactory.cs

---

## AiNetLinter.Models

### RuleViolationFactory `static` — `src/AiNetLinter/Models/RuleViolationFactory.cs`

```csharp
public static RuleViolation Create(string filePath, string ruleName, string details, string guidance)
```
```

### Sample C: Nicht existierende Datei
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "filePaths": ["src/AiNetLinter/NonExistentFile.cs"]
}
```
- **Tool-Output (Rohdaten):**
```text
[ERROR]: RESOURCE_NOT_FOUND: Datei 'src/AiNetLinter/NonExistentFile.cs' nicht in der Solution gefunden.
  context: src/AiNetLinter/NonExistentFile.cs
  hint:    Pfad relativ zum Solution-Verzeichnis angeben (Forward- oder Backslash), 'find_symbol' zur Orientierung nutzen.
```

---

## 3. Effizienz- & SNR-Bewertung (gemäß AiNetLinter-Richtlinien)
- **Token-Footprint:** Extrem klein (~600 Bytes / ~150 Token für Sample A; ~1.1 KB für Sample B).
- **Signal-to-Noise Ratio (SNR):** Sehr hoch für das Überblicken von C#-Dateien. Keine Implementierungsrümpfe, sauber formatierte C#-Signaturen.
- **Content-Only-Hard-Cut:** Konform. Ein einziger zusammenhängender Markdown-Block.

---

## 4. Composability & Chaining-Validierung (Input/Output-Passgenauigkeit)
- **Erzeugte Ausgabefelder:**
  - Dateipfad: z. B. `src/AiNetLinter/Configuration/ILinterEngineConfig.cs`
  - Typname: `ILinterEngineConfig`
  - Member-Signatur: z. B. `public static RuleViolation Create(...)`
- **KRITISCHER BEFUND (Vertragsbruch / Chaining-Lücke):**
  - **Tool-Beschreibung verspricht:** *"Der Content enthält stabile Handoff-IDs fuer direkte Folge-Calls an get_symbol_body."*
  - **Tatsächlicher Output:** Es werden **keine** `handoffId`-Einträge im Output generiert!
  - **Wirkung auf den Agenten:** Ein Agent kann aus dem Skeleton heraus **nicht** direkt per kopierbarer `handoffId` in `get_symbol_body`, `get_call_tree` oder `get_feature_context` springen. Er muss den vollqualifizierten Namen erraten (`AiNetLinter.Models.RuleViolationFactory.Create`) oder erst `find_symbol` zwischenschalten.
- **Chaining-Passgenauigkeit:**
  | Ausgabefeld | Folgetool | Erwarteter Input | Status | Befund |
  |---|---|---|---|---|
  | `handoffId` | `get_symbol_body` | `symbolIdentifiers` | ❌ Fehlt | **Nicht im Output vorhanden**, obwohl in Beschreibung behauptet! |
  | Dateipfad | Text-Editoren | Pfad | ✅ Ja | Relativer Pfad ist sauber nutzbar |
  | Methodenname | `get_symbol_body` | `symbolIdentifiers` | ⚠️ Heuristisch | Name muss manuell mit Typ & Namespace konkateniert werden |

---

## 5. Fazit & Architekturentscheidung (Offener Punkt)
- **Prädikat:** **Verbesserungsbedürftig (Chaining-Lücke)**
- **Stärken:**
  1. Sehr kompakte Übersicht zur schnellen Orientierung über Dateiinhalte.
  2. Batch-fähig für mehrere Dateien.
- **Architektonische Weichenstellung (Zur Abstimmung):**
  - **Befund:** Die Tool-Beschreibung verspricht: *„Der Content enthält stabile Handoff-IDs fuer direkte Folge-Calls an get_symbol_body.“* Derzeit fehlen diese Handoff-IDs jedoch in `SkeletonMarkdownRenderer.cs` vollständig (obwohl sie im Modell `SkeletonTypeInfo.Id` und `SkeletonMemberInfo.Id` bereits extrahiert vorliegen).
  - **Optionen zur Behebung:**
    1. **Typ-Level Handoff-ID (Token-effizient):** Handoff-ID nur an den Markdown-Headern der Typen ausgeben (z. B. `### ILinterEngineConfig — src/...; handoffId: s:...`). Spart Tokens und ermöglicht direkten Sprung auf den Typ.
    2. **Vollständige Member-Handoff-IDs:** Jeder Member erhält seine ID als Kommentar (z. B. `GlobalConfig Global { get; } // handoffId: s:...`). Ermöglicht direkten Member-Sprung, erhöht aber den Token-Footprint um das 2- bis 3-fache.
    3. **Doku-/Beschreibungs-Korrektur:** `get_file_skeleton` bleibt als minimaler, reiner Signatur-Überblick ohne IDs definiert; der Verweis auf Handoff-IDs wird aus der Beschreibung entfernt und auf `get_class_structure` bzw. `find_symbol` verwiesen.
  - **Entscheidung:** Erfordert Nutzer-Entscheidung bezüglich Token-Budget vs. Chaining-Granularität. Keine voreilige Codeänderung.
