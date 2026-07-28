---
type: prompt
category: dev
version: 0.1
status: draft
---

# AGENTS.md für dieses Projekt erzeugen

## Zweck

Du wirst in einem beliebigen Projekt ausgeführt, direkt im Chat
referenziert — ohne weiteren Text danach nötig. Deine Aufgabe: eine
projekt-spezifische `AGENTS.md` im Projekt-Root erzeugen (oder ein
Update für eine bestehende vorschlagen), die andere KI-Coding-Sessions
beim Einstieg in dieses Projekt orientiert — Build-/Test-Commands, wo
die Coding-Konventionen liegen, wie hier committet wird.

Diese Datei enthält keine Platzhalter — der gesamte Kontext (welches
Projekt, welcher Stack, welche Konventionen) ergibt sich vollständig aus
dem, was du im aktuellen Arbeitsverzeichnis vorfindest.

## Schritt 1 — Kontext sammeln

### Rules-Verzeichnis erkennen

Prüfe zwei Kandidaten (projekt-root-relativ): `.agents/rules/` und
`.cursor/rules/`.

- **Genau einer existiert:** automatisch übernehmen, keine Rückfrage.
- **Beide oder keiner existieren:** frag den Nutzer explizit und offen
  (auch ein dritter, hier nicht gelisteter Pfad oder „keine
  Konventionen" sind gültige Antworten).

### Build-/Test-Commands ableiten

Aus den vorhandenen Projekt-Dateien ableiten, nicht raten:

- `.csproj`/`.sln` → `dotnet build` / `dotnet test`
- `pyproject.toml`/`pytest.ini` → `pytest`
- `package.json` mit `test`-Script → `npm test` / `pnpm test`
- `Cargo.toml` → `cargo build` / `cargo test`
- `go.mod` → `go build ./...` / `go test ./...`
- Sonstige/uneindeutige Fälle: zusätzlich `.github/workflows/**` (o. ä.
  CI-Konfiguration) prüfen. Bleibt es unklar: **fragen**, nicht raten.

### Commit-/PR-Konventionen

Aus `CONTRIBUTING.md`, `README.md` oder — falls vorhanden — den letzten
Commits (`git log`) ableiten: Conventional Commits? Feste Sprache?
Feste Präfixe?

### dev-loop-Erkennung (optional)

Suche nach einem Ordner mit `README.md` + `task-loop/orchestrator.md`
(Signatur dieses Scaffolding-Ansatzes) — irgendwo im Projekt, häufig
unter `.agents/Agent-Scaffolding/` o. ä. Gefunden: relativen Pfad dorthin
für Schritt 4 notieren. Nicht gefunden: einfach weglassen, keine
Rückfrage nötig.

## Schritt 2 — Bestehende `AGENTS.md` prüfen

**Existiert im Projekt-Root schon eine `AGENTS.md`:**
- Lies sie, vergleiche mit dem, was du in Schritt 1 ermittelt hast.
- Fass das Delta kurz zusammen (was würde sich ändern/ergänzen) und
  **frag explizit**, bevor du sie überschreibst.

**Existiert noch keine:** direkt weiter zu Schritt 3, keine Rückfrage
nötig.

## Schritt 3 — Nur relative Pfade, nie absolute

**Harte Regel, keine Empfehlung:** Jeder Pfad in der erzeugten
`AGENTS.md` ist relativ zum Projekt-Root. Niemals einen absoluten Pfad
hineinschreiben (kein `C:\Users\...`, kein `/home/...`, kein
`/Users/...`) — das macht die Datei für jeden anderen, der das Projekt
auscheckt, falsch und nutzlos.

- **Schlecht:** `Coding-Regeln: C:\Daten\Projekt-X\.agents\rules\`
- **Gut:** `Coding-Regeln: .agents/rules/`

Prüf das aktiv nach dem Schreiben — auch dann, wenn dir selbst im
Kontext (z. B. Arbeitsverzeichnis-Angabe deines Werkzeugs) ein absoluter
Pfad vorliegt.

## Schritt 4 — Inhalt/Struktur

Nimm nur Abschnitte auf, für die in Schritt 1 tatsächlich etwas gefunden
wurde — keine leeren Platzhalter-Sections für Dinge, die nicht
zutreffen.

- **Setup/Build:** Build-Command(s)
- **Tests:** Test-Command(s)
- **Code-Style/Konventionen:** Pointer auf das erkannte
  Rules-Verzeichnis (relativ!) — oder kurzer Hinweis, dass keins
  gefunden wurde
- **Commit-/PR-Konventionen:** was in Schritt 1 ermittelt wurde
- **Struktur-Hinweise:** 1-2 Sätze, was grob wo liegt, falls beim
  Erkunden offensichtlich geworden (keine vollständige Architektur-Doku)
- **dev-loop-Pointer** (nur falls in Schritt 1 gefunden): ein Satz mit
  dem relativen Pfad, z. B. „Für mehrstufige Aufgaben (Audits,
  Refactorings, Features): siehe
  `.agents/Agent-Scaffolding/dev-loop/README.md`."

## Schritt 5 — Rückmeldung

Nach dem Schreiben: kurz zusammenfassen, was in die Datei gekommen ist
(Fazit zuerst, keine Textwüste) und den Pfad zur erzeugten `AGENTS.md`
nennen.
