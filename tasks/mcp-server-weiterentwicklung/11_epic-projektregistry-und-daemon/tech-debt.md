---
task: 11_epic-projektregistry-und-daemon
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-23T14:05:41+02:00
---

# Tech-Debt-Log: 11_epic-projektregistry-und-daemon

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` + `src/AiNetLinter/Configuration/ConfigLoader.cs` | mittel | nein | Defekte (lesbare, aber ungültige) rules.json fällt im künftigen Registry-Pfad stumm auf Defaults zurück — kein deterministischer Fehlervertrag dafür |
| TD-002 | `src/AiNetLinter/Configuration/ConfigLoader.cs` | niedrig | nein | Diagnosen von TryLoadConfig gehen hart auf Console.Error (Kanal nicht injizierbar) — Misch-Thema erst mit dem Daemon (Epic B) |
| TD-003 | `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` | niedrig | nein | Load(null/leerer projectRoot) löst implizit cwd-relativ auf — Ankerregel formal verletzt bis der Wiring-Guard existiert |

## Einträge

### TD-001 — Defekte Regeldatei: stummer Default-Fallback im Registry-Pfad [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs:17` (`MaterializeRules` → `ConfigLoader.TryLoadConfig`)
- **Befund:** `ConfigLoader.TryLoadConfig` gibt bei lesbarer, aber ungültiger `rules.json`
  `null` zurück (stderr-Diagnose + Rückgabewert null — der `isRequired`-Parameter betrifft
  nur leere Pfadangaben, nicht defekte Inhalte); `MaterializeRules` fängt das mit
  Defaults ab. Im Batch-Pfad ist das gepinnt korrekt. Im späteren Registry-Pfad
  (`ProjectInstanceFactory.Create`, `isRequired: true`) lädt ein Projekt mit DEFEKTER
  rules.json damit stumm mit Default-Regeln weiter — genau die stille Fehl-Bindung,
  gegen die das Epic gebaut wird. Konzept A.5 kennt dafür keinen Fehlercode
  (`RULES_NOT_FOUND` deckt nur fehlende Dateien). Der Coder hat die Lücke im
  step-result.md selbst gemeldet; Verifikation per `get_symbol_body` bestätigt sie.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 — das Batch-Verhalten
  ist Bestands-/Step-Vertrag; ein eigener Vertrag für defekte Regeldateien im Registry-Pfad
  (z. B. neuer Code oder sichtbare Markierung) ist eine Konzept-relevante
  Vertragsentscheidung, keine mechanische Korrektur.
- **Vorschlag:** Im Wiring-Step von Epic A entscheiden: entweder deterministischer
  Fehlervertrag für parse-defekte Regeldateien im Registry-Pfad (z. B. `RULES_INVALID`,
  ergänze Konzept A.5) oder mindestens sichtbare Markierung des Default-Fallbacks im
  Tool-Antwortpfad (`UsedDefaultConfig=true` auswerten). Bis dahin dokumentiert dieser
  Eintrag die bekannte Lücke.
- **Auto-Fixable:** nein — Verhaltens- und Vertragsentscheidung mit Architektur-Ermessen.
- **Status:** offen  # offen | erledigt | verworfen

### TD-002 — Diagnosekanal von ConfigLoader nicht injizierbar [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Configuration/ConfigLoader.cs` (`TryLoadConfig`, drei `Console.Error.WriteLine`-Stellen)
- **Befund:** `TryLoadConfig` schreibt Diagnosen direkt auf `Console.Error` — kein
  injizierbarer Ausgabekanal. Solange nur der Batch-Prozess läuft, unkritisch. Bedient der
  Daemon (Epic B) mehrere Projekte/Verbindungen in einem Prozess, mischen sich diese
  Meldungen untereinander und mit dem Protokoll-/Antwortpfad, ohne Zuordnung zu
  Verbindung/Key. Vom Coder im step-result.md gemeldet; per `get_symbol_body` bestätigt.
- **Warum nicht sofort gefixt:** Bestandscode außerhalb des Step-Scopes; eine Injektion
  (z. B. `ILintConsole`/Channel-Parameter durchreichen) ändert interne Signaturen und
  betrifft mehrere Call-Sites — eigenständige Entscheidung.
- **Vorschlag:** Mit dem Epic-B-Ausbau von Health/Observability prüfen, den
  Diagnosekanal zu injizieren und ins Observability-Log je Verbindung zu führen.
- **Auto-Fixable:** nein — API-/Signaturänderung mit Integrationsentscheidung.
- **Status:** offen  # offen | erledigt | verworfen

### TD-003 — Loader ohne Root-Guard: cwd-relative Restauflösung bis zum Wiring [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` (`Load`, `projectRoot ?? string.Empty`)
- **Befund:** Bei null/leerem/Whitespace-`projectRoot` baut der Loader einen relativen
  Definitionsdatei-Pfad; die Existenzprüfung läuft dann implizit gegen den Prozess-cwd —
  formal ein Verstoß gegen die Ankerregel A.2 („nie zum cwd"). Bis zum Wiring-Step ist der
  Pfad unerreichbar (`projectRoot` wird dort Pflicht UND absolut sein:
  `PROJECT_ROOT_REQUIRED`/`PROJECT_ROOT_INVALID` auf Argumentebene); vom Coder im
  step-result.md als „Bekannte Unschärfe" gemeldet und plan-konform bewusst so belassen.
- **Warum nicht sofort gefixt:** Der Step-Plan legt die Root-Validierung ausdrücklich auf
  die Argumentebene des Wiring-Steps; ein eigener Guard im Loader wäre Doppelvalidierung
  bzw. eine Vertragsänderung in diesem Step.
- **Vorschlag:** Der Wiring-Step muss garantieren, dass kein `Load`-Aufruf mit leerem
  Root erfolgt; dort sinnvoll einen Contract-Test ergänzen, der das absichert (z. B. über
  die Argumentvalidierung vor dem ersten Registry-Zugriff).
- **Auto-Fixable:** nein — Verhaltensfrage (wo validiert wird) mit Test-Entscheidung.
- **Status:** offen  # offen | erledigt | verworfen
