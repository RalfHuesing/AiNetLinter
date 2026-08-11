---
name: drift-audit
description: Vier-Schritte-Playbook, um vor Epic-Abschluss aktiv nach DRY-Verstößen (Code-Duplikation) und Refactoring-Drift (existierender Helper wird nicht aufgerufen, sondern nachgebaut) zu suchen. Nutzt ausschließlich das projekteigene MCP-Tool find_duplicates.
---

# Skill: Drift-Audit

## Zweck

Code-Duplikation entsteht bei autonomer agentischer Entwicklung häufig, weil eine
bereits existierende Lösung nicht wiedergefunden wird — keine einzelne Lint-Regel
fängt das, weil es erst im Vergleich über die ganze Codebase sichtbar wird. Dieser
Skill leitet gezielt an, mit `find_duplicates` aktiv danach zu suchen, statt darauf zu
warten, dass es zufällig auffällt.

**Cadence:** Vor Abschluss eines Epics oder eines größeren Tasks einmal ausführen.
Für einzelne Steps innerhalb eines Tasks ist die Ausführung optional.

## Voraussetzung

Der MCP-Server `ainetlinter` ist verbunden (`.mcp.json`, siehe AGENTS.md §1) und hat
eine Solution geladen. Kein separates Setup nötig — `find_duplicates` ist eines der
registrierten MCP-Tools.

## Schritt 1 — Solution-weiter Scan

Rufe `find_duplicates` mit `scopeDir="src"` und `minTokens=20` auf (niedriger als der
Lint-Default 30 aus `rules.json`, damit dieser Audit gründlicher sucht als das
automatische Lint-Gate):

```
find_duplicates(scopeDir="src", minTokens=20)
```

Ergebnis ist eine nach `exact`/`near`/`fuzzy` gestaffelte Cluster-Liste (transitiv
ähnliche Methoden, keine isolierten Paare). `fuzzy`-Cluster bewusst nicht Teil dieses
Audits — zu viel Rauschen für eine manuelle Durchsicht, das deckt das automatische
Lint-Gate (`DuplicateCodeChecker`) ohnehin nicht ab.

## Schritt 2 — Pro `exact`-Cluster entscheiden

Für jeden Cluster mit `bucket=exact` (Jaccard-Score ≥ 0.95, praktisch identischer
Code):

- **Konsolidieren jetzt**, wenn die Extraktion in eine gemeinsame Methode/Klasse
  klein und risikoarm ist (wenige Aufrufstellen, keine divergierenden
  Zukunftspläne für die Cluster-Mitglieder).
- **Tech-Debt-Eintrag anlegen**, wenn die Konsolidierung den aktuellen Task-Scope
  sprengen würde oder architektonisches Ermessen braucht, das über eine mechanische
  Extraktion hinausgeht.

Keine dritte Option "ignorieren" ohne Begründung — ein `exact`-Cluster ist per
Definition nahezu identischer Code, das Vorkommen selbst ist bereits der Befund.

## Schritt 3 — Pro `near`-Cluster prüfen

Für jeden Cluster mit `bucket=near` (Score 0.80–0.95): strukturelle Ähnlichkeit ist
hier nicht automatisch Duplikation — 50 strukturell ähnliche, aber fachlich
unterschiedliche `Dispose()`-Implementierungen wären ein falsches Positiv. Sieh dir
1–2 Beispiel-Mitglieder des Clusters an (Datei:Zeile aus der Antwort), dann
entscheide wie in Schritt 2 (konsolidieren/Tech-Debt) oder verwirf den Cluster als
legitime, nur zufällig ähnliche Methoden.

## Schritt 4 — Optional: Refactoring-Drift-Check für auffällige Helper

Fällt dir in Schritt 1–3 ein Helper auf, der eigentlich zentral genutzt werden
sollte (z. B. eine Options-Builder-Methode, ein zentraler Validator), aber in einem
der Cluster mehrfach inline nachgebaut statt aufgerufen wird: prüfe gezielt, ob es
noch weitere, bislang unentdeckte Nachbau-Stellen gibt:

```
find_duplicates(mode="refactoring-drift", helperSymbol="<qualifizierter Name oder Datei:Zeile:Spalte des Helpers>")
```

`helperSymbol` akzeptiert dasselbe Format wie bei `find_references`/`get_impact`
(stabile DocumentationCommentId, `Datei:Zeile:Spalte` oder qualifizierter Name). Das
Ergebnis listet Methoden, die strukturell ähnlich zum Helper sind, ihn aber
nachweislich nicht aufrufen — explizit als **Kandidaten**, nicht als Verstöße
(False-Positive-Budget höher als bei Schritt 1–3, strukturelle Ähnlichkeit bedeutet
nicht zwingend Drift). Jeden Kandidaten manuell prüfen, bevor er auf den Helper
umgestellt wird.

## Was dieser Skill nicht tut

- Kein automatisches Umschreiben von Code — jeder Fund wird von dir bewertet, nicht
  automatisch behoben.
- Kein Ersatz für `DuplicateCodeChecker` (das Lint-Gate mit `minTokens=30` aus
  `rules.json`) — dieser Skill ist eine zusätzliche, gründlichere manuelle Runde,
  kein Ersatz dafür.
- Keine Naming-Drift-Erkennung (unterschiedlich benannte, aber strukturell ähnliche
  Bezeichner) — nicht Teil von `find_duplicates`, aktuell zurückgestellt.
