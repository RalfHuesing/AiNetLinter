# Roadmap – Kompakte MCP-Handoff-IDs

- [X] 1. Ist-Stand, vollständige Verdrahtungsinventur und offene Scope-Lücken auditieren
- [X] 2. Verbleibende Source-Producer und -Consumer (`metrics_lookup`, Spezial-Consumer) verdrahten
- [X] 3. Assembly-Producer und -Consumer vollständig auf opaque Handles umstellen
- [X] 4. Öffentlichen Vertrag, Renderer- und Toolketten-Tests einschließlich Edge-Cases schließen
- [X] 5. Dokumentation, Tokenmessung und vollständige Wire-/Inventurprüfung abschließen
- [ ] 6. Gesamt-Audit mit proaktiver Finding-Behebung und Release-Gate

## Durchführungsprotokoll

### 1. Ist-Stand, vollständige Verdrahtungsinventur und offene Scope-Lücken auditieren

- Audit-Skript deckt zusätzlich `GetSymbolBodyTool` und `FindImplementationsTool` ab.
- Befund: 7 Stellen offen; interner-ID-Fallback bei Counter-Fehlern als Vertragslücke erfasst.
- Kein Build: nur PowerShell-/Markdown-Änderung; `git diff --check` grün.

### 2. Verbleibende Source-Producer und -Consumer (`metrics_lookup`, Spezial-Consumer) verdrahten

- `metrics_lookup` und `find_duplicates.helperSymbol` restaurieren/erzeugen opaque Handles.
- Interne-ID-Fallbacks in Renderern entfernt; Counterfehler werden propagiert.
- Build, `verify(changes)` und 1.832 Unit-FastTests grün; Audit-Skript braucht Anpassung an die neue zentrale API.

### 3. Assembly-Producer und -Consumer vollständig auf opaque Handles umstellen

- `get_assembly_context` und Assembly-Referenzsuche restaurieren Handles am Eingang.
- Toolketten für Inspect/Search/Extensions bis zum Context gegen `h:` und Altformatfehler abgesichert.
- Build, `verify(changes)` und 1.838 Unit-FastTests grün.

### 4. Öffentlichen Vertrag, Renderer- und Toolketten-Tests einschließlich Edge-Cases schließen

- Ungültige Groß-/Kleinschreibungs- und erweiterte Handles werden zentral abgewiesen.
- Finale Content-Pipeline verhindert interne-ID-Austritte; Assembly-Call-Tree-Kette ergänzt.
- Audit 12/12 Producer, 17/17 Consumer; Build, `verify(changes)` und 1.844 Unit-FastTests grün.

### 5. Dokumentation, Tokenmessung und vollständige Wire-/Inventurprüfung abschließen

- MCP-Dokumentation erklärt Handles, Neustart, Counter-State, Reset und Wiederermittlung.
- Reproduzierbare Messsuite belegt ≥25 % UTF-8-, ≥70 % ID-Zeichen- und Tokenersparnis bei Inhaltsgleichheit.
- Audit 12/12 und 17/17 PASS; Build, `verify(changes)`, 1.845 Unit- und 6 Doku-Integrationstests grün.

### 6. Gesamt-Audit mit proaktiver Finding-Behebung und Release-Gate

- Ausstehend.
