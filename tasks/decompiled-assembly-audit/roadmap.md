# Ausführungsstand: Assembly-Unterstützungs-Audit

status: executing
current_epic: 4
last_checkpoint: Epic 3 abgeschlossen; Analyse-Checkpoint wird gesichert
current_debt_item: none
debt_attempts: 0

## Primäraufgabe

Prüfe die lokale Assembly-Unterstützung des AiNetLinter-MCP anhand der
aktuellen Implementierung, Verträge und redigierten Prüffälle und liefere
acht eigenständige, priorisierte Befundberichte.

## Epics

1. **Öffentliche MCP-Verträge und Discoverability** — Registrierung, Schemas,
   Annotationen, Defaults, Capability-Matrix, Progressive Disclosure und
   Dokumentationskonsistenz. Abhängigkeiten: keine. Bereiche:
   `AssemblyAnalysisToolRegistrations` und öffentliche Tool-/Response-Verträge.
   Muss-Kriterien: aktuelle Verträge gegen Code und MCP-Schema prüfen;
   Befunde nach Kategorie, Priorität und Größe ordnen; Evidence-/Scope-
   Abschnitt und Unsicherheiten ausweisen. Verifikation: gezielte MCP-
   Abfragen und Quelltext-/Dokumentlesung, keine Tests/Builds. Status:
   done (Analysebericht abgeschlossen; unabhängiger Review auf Nutzeranweisung
   übersprungen).
2. **Decompilation und semantischer Snapshot** — Metadata-only-Garantie,
   dekompilierte Dokumente, Syntax/Bodies, Generics, Attribute, Parameter,
   stabile IDs und source-backed-Abgrenzung. Abhängigkeiten: Epic 1.
   Verifikation: Assembly- und Symbol-/Strukturabfragen, Quelltextlesung.
   Status: done (Analysebericht abgeschlossen; Review auf Nutzeranweisung
   übersprungen).
3. **Referenzen, Source Selection und Diagnosen** — Auflösung, Source-
   Auswahl, fehlende/inkompatible Referenzen, Herkunft, Trust, Partialität
   und Diagnoseprojektion. Abhängigkeiten: Epics 1–2. Verifikation:
   redigierte Origin-Abfragen für GIT-01 und LOCAL-01 bis LOCAL-03 sowie
   Quelltext-/Testvertragsanalyse. Status: done (Analysebericht abgeschlossen;
   Review auf Nutzeranweisung übersprungen).
4. **Session-, Cache- und Lebenszeitsemantik** — Fingerprints, Generationen,
   Cache, Refresh, Leases, Cancellation, Eviction, TTL, Disposal und
   Parallelität. Abhängigkeiten: Epic 2. Verifikation: MCP-Health-/Session-
   Sicht und Quelltext-/Testanalyse; keine Laufzeitänderung. Status: in_progress.
5. **Navigation und fachliche Query-Korrektheit** — Assembly-fähige
   Symbolgraph- und Strukturtools, Root-/Referenzgrenzen, Caller-/Calltree-
   Semantik, Extensions und Trunkierung. Abhängigkeiten: Epics 2–3.
   Verifikation: gezielte Navigationstools und Quelltext-/Testvertragsanalyse.
   Status: open.
6. **Response-, Token- und Laufzeiteffizienz** — Budgets, Reduktionsreihenfolge,
   Text-/JSON-Konsistenz, Diagnose-Samples, Referenzlimits und Worst-Case-
   Payloads. Abhängigkeiten: Epics 3–5. Verifikation: gezielte MCP-
   Payloads, Budget-/Limit-Code und vorhandene Tests, ohne Testausführung.
   Status: open.
7. **Betrieb, Sicherheit und Fehlerverhalten** — Pfade, Dateitypen, native/
   beschädigte/wechselnde Dateien, Nichtausführung, redigierte Fehler,
   Health/Observability und Fail-Closed. Abhängigkeiten: Epics 2–6.
   Verifikation: FALSE-01 und passende negative/Fehlerpfade per MCP sowie
   Quelltextanalyse. Status: open.
8. **Test- und Dokumentationsnachweis** — Abdeckung kritischer Verträge,
   Lücken, irreführende Erwartungen und spätere Verifikation. Abhängigkeiten:
   Epics 1–7. Verifikation: read-only Analyse bestehender Tests und Doku.
   Status: open.

## Abschluss-Checkliste aus dem Konzept

- [ ] Acht separate Epic-Berichte mit Evidence-/Scope-Abschnitt erstellt.
- [ ] Befunde nach `Bug`, `Optimierung`, `Missing Feature`, danach Priorität,
      Vertrauen und Größe geordnet; leere Kategorien begründet.
- [ ] Assembly-Tools und assembly-fähige Folgeabfragen gegen aktuelle
      Implementierung und MCP-Verträge geprüft.
- [ ] Decompilation, Referenzen, Fallback/Diagnosen, Lebenszeit, Sicherheit,
      Response-Budget und Agentennutzbarkeit abgedeckt.
- [ ] GIT-01 redigierter Origin-Nachweis sowie LOCAL-01 bis LOCAL-03
      Decompilation-Nachweis erstellt.
- [ ] FALSE-01 als sicherer, recoverable Negativfall nachgewiesen.
- [ ] Keine externen Assembly-Identitäten in versionierten Ergebnissen,
      Commit-Texten oder Vorschlägen; lokale Prüffall-Matrix nicht übernommen.
- [ ] Explizit dokumentiert, welche Nachweise nur gelesen und welche MCP-
      Abfragen tatsächlich ausgeführt wurden.
- [ ] Keine Code-, Build-, Test- oder Dokumentationsänderungen vorgenommen.
- [ ] Redaktionsprüfung und Gitignore-Prüfung der lokalen Prüffall-Matrix
      durchgeführt.

## Tech-Debt

Actionable Befunde werden ausschließlich in `tech-debt.md` geführt.
