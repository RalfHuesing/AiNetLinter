## Primäre Einstiegspunkte

- Assembly-Only-MCP-Werkzeuge: `inspect_assembly` und
  `find_assembly_extensions`.
- Assembly-Target-Dispatch und gemeinsame read-only Assembly-Session.
- Folgeabfragen auf Assembly-Snapshots: Symbol-, Struktur-, Metrik- und
  Abhängigkeitswerkzeuge.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/Assemblies/` — Decompilation, Sessions, Registry,
  Referenzen und External-Source-Pfade.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` — Registrierung, Dispatch,
  Services, Filter und Response-Projektion.
- Konkrete Symbole werden durch den ersten Analyse-Agenten per MCP verifiziert.

## Aufrufer und Abhängigkeiten

- MCP-Tool-Registrierung → Assembly-Dispatch/Service → Registry/Session →
  Snapshot/Compilation → Response-Budget und Structured Content.
- Optionale Referenz-Sessions und External-Source-Provider sind bounded und
  read-only zu prüfen.

## Relevante Tests, Konfiguration und Dokumentation

- Assembly-FastTests und Assembly-Integrationstests als read-only Evidenz.
- `ainetlinter.project.json`, `rules.json`, `Docs/agent-api.md`,
  `Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md` und
  `README.md`, soweit assembly-relevante Verträge betroffen sind.
- Lokale Prüffall-Matrix unter `temp/` nur auf Existenz/Gitignore und zur
  redigierten Ausführung verwenden; konkrete Identitäten nicht dokumentieren.

## Invarianten, Risiken und Unsicherheiten

- Metadata-only: Zielassemblies und deren Methoden werden nicht geladen oder
  ausgeführt.
- Absolute Pfade, bounded Antwortbudgets, sichtbare Herkunft/Trust/
  Completeness und kontrollierbare Fehlerzustände.
- Source-backed versus decompiled darf nicht aus Mapping allein abgeleitet
  werden; GIT-01 benötigt mehrere unabhängige Origin-Signale.
- MCP-Antworten können partiell oder trunkierter sein; keine globale
  Negativaussage aus einem Root-Snapshot ableiten.
- Externe Identitäten und Pfade dürfen nicht in versionierte Artefakte,
  Logs oder Commit-Texte gelangen.

## Verifikation

- Noch nicht ausgeführt; der erste Agent dokumentiert MCP-Tool, Parameter,
  Scope, Ergebnis und Frische je Prüfung.
