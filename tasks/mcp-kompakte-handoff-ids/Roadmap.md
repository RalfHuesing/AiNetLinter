# Roadmap – Kompakte MCP-Handoff-IDs

- [X] 1. Ist-Stand, vollständige Verdrahtungsinventur und offene Scope-Lücken auditieren
- [X] 2. Verbleibende Source-Producer und -Consumer (`metrics_lookup`, Spezial-Consumer) verdrahten
- [X] 3. Assembly-Producer und -Consumer vollständig auf opaque Handles umstellen
- [X] 4. Öffentlichen Vertrag, Renderer- und Toolketten-Tests einschließlich Edge-Cases schließen
- [X] 5. Dokumentation, Tokenmessung und vollständige Wire-/Inventurprüfung abschließen
- [X] 6. Gesamt-Audit mit proaktiver Finding-Behebung und Release-Gate
- [X] 7. Rot-Test vom Marker-Check zum ausführbaren MCP-Handoff-Vertragsaudit erweitern
- [X] 8. Gefundene Toolketten reparieren und unabhängigen Abschlussaudit durchführen
- [X] 9. Assembly-Referenz-Handoffs bis zu allen Folge-Consumern reparieren
- [X] 10. Öffentlichen Handoff-Vertrag vollständig vereinheitlichen
- [X] 11. Harter-Schnitt-Audit und Release-Gate wiederholen
- [X] 12. MCP-Toolbeschreibungen für direkte Handle-Übergabe verdichten

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
- Toolketten für Inspect/Search/Extensions bis zum Context gegen `h:` und ungültige Eingaben abgesichert.
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

- Audit korrigierte den Counter-Fehlerpfad und aktualisierte reale Assembly-Toolketten.
- Audit-Skript PASS (12/12, 17/17); `verify(solution)` pass, 10.0, 0 Violations.
- Release-Gate grün: Build warnungsfrei, 2.646 FastTests und 235 IntegrationTests (je non-Stress).

### 7. Rot-Test vom Marker-Check zum ausführbaren MCP-Handoff-Vertragsaudit erweitern

- Matrix umfasst 12 Producer, 14 öffentliche Consumer-Felder und 7 Toolketten.
- Breite Wire-Suche plus ToolCollection-Schematest ergänzt; der damalige Daemon-Befund zu `context` ist nicht reproduzierbar.
- Audit ist erwartungsgemäß rot: genau `find_symbol → find_duplicates.helperSymbol` liefert `TARGET_MISMATCH`; kein Commit vor der Reparatur.

### 8. Gefundene Toolketten reparieren und unabhängigen Abschlussaudit durchführen

- Ursache behoben: `find_duplicates` reicht die restaurierte Target-Identität bis zum Resolver weiter.
- Reale MCP-E2E-Kette `find_symbol → find_duplicates` ergänzt; Audit PASS (12/14/7, alle Ketten grün).
- Release-Gate grün: Build, beide `verify`-Scopes, 2.648 FastTests und IntegrationTests non-Stress.

### 9. Assembly-Referenz-Handoffs bis zu allen Folge-Consumern reparieren

- Aktueller Daemon 1.0.204: Inspect → References → Body/Context/Call-Tree vollständig reproduzierbar grün.
- Cross-Assembly-Owner-Handoff ebenfalls korrekt; kein Rot-Fall im aktuellen HEAD belegbar.
- Kein spekulativer Fix oder Test ohne reproduzierbaren Fehler; nur MCP-Blackbox-Audit.

### 10. Öffentlichen Handoff-Vertrag vollständig vereinheitlichen

- Öffentlicher Vertrag, Fehlertexte, Tests und Dokumentation verwenden ausschließlich `h:`.
- Interne Navigation verwendet neutrale Kennungen und wird nie in MCP-Content gerendert.
- PowerShell-Audit entfernt; C#-Vertragstests sichern Ausgabe, Eingabe und Toolketten.

### 11. Harter-Schnitt-Audit und Release-Gate wiederholen

- Live-Prüfung bestätigt Source- und Assembly-Toolketten mit unverändert weitergegebenen Handles.
- Inventur des relevanten Source-, Test- und Doku-Scopes bestätigt ausschließlich den aktuellen Vertrag.
- Build, beide Verify-Scopes, 2.636 FastTests und 236 IntegrationTests grün.

### 12. MCP-Toolbeschreibungen für direkte Handle-Übergabe verdichten

- Alle Handle-Consumer nennen direkte Übergabe bevorzugt; Listen, Einzelwerte und Pflichtfelder bleiben präzise.
- Producer weisen knapp auf direkt nutzbare Handles hin; derselbe `targetPath` bleibt Teil jedes Folgeaufrufs.
- Frisches `tools/list`, reale Ketten, Build, `verify(changes)` sowie 14 Vertrags-/Doku-/E2E-Tests grün.
