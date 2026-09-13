# Roadmap – Kompakte MCP-Handoff-IDs

- [X] 1. Ist-Stand, vollständige Verdrahtungsinventur und offene Scope-Lücken auditieren
- [ ] 2. Verbleibende Source-Producer und -Consumer (`metrics_lookup`, Spezial-Consumer) verdrahten
- [ ] 3. Assembly-Producer und -Consumer vollständig auf opaque Handles umstellen
- [ ] 4. Öffentlichen Vertrag, Renderer- und Toolketten-Tests einschließlich Edge-Cases schließen
- [ ] 5. Dokumentation, Tokenmessung und vollständige Wire-/Inventurprüfung abschließen
- [ ] 6. Gesamt-Audit mit proaktiver Finding-Behebung und Release-Gate

## Durchführungsprotokoll

### 1. Ist-Stand, vollständige Verdrahtungsinventur und offene Scope-Lücken auditieren

- Audit-Skript deckt zusätzlich `GetSymbolBodyTool` und `FindImplementationsTool` ab.
- Befund: 7 Stellen offen; interner-ID-Fallback bei Counter-Fehlern als Vertragslücke erfasst.
- Kein Build: nur PowerShell-/Markdown-Änderung; `git diff --check` grün.

### 2. Verbleibende Source-Producer und -Consumer (`metrics_lookup`, Spezial-Consumer) verdrahten

- Ausstehend.

### 3. Assembly-Producer und -Consumer vollständig auf opaque Handles umstellen

- Ausstehend.

### 4. Öffentlichen Vertrag, Renderer- und Toolketten-Tests einschließlich Edge-Cases schließen

- Ausstehend.

### 5. Dokumentation, Tokenmessung und vollständige Wire-/Inventurprüfung abschließen

- Ausstehend.

### 6. Gesamt-Audit mit proaktiver Finding-Behebung und Release-Gate

- Ausstehend.
