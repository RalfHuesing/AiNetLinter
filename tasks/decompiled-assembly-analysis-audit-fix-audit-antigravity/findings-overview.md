# Befund-Übersicht & Audit-Synthese: AiNetLinter MCP-Server

**Datum:** 2026-08-31  
**Orchestrator:** Antigravity AI Orchestrator  
**Auditierte Version:** AiNetLinter MCP-Server v1.0.157 (Daemon-Modus)  
**Getestete Assemblys (anonymisiert):** `Vendor.Pps.RealTimeData.dll`, `Vendor.Data.dll`, `Vendor.Rewe.Buchungserfassung.dll`  
**Getestetes Source-Projekt:** `AiNetLinter`

---

## 1. Executive Summary & Gesamturteil

Der **AiNetLinter MCP-Server** präsentiert sich im Live-Test als ein **hochgradig ausgereiftes, performantes und agentengerechtes Analyse-System**. 

### Besondere Stärken:
- **Composite Exploration (`get_feature_context`):** Bündelt Deklaration, Metrik-Budget, Aufrufer, Test-Zuordnung und Violations in einem einzigen Turn (< 500 Tokens) und spart 3–4 separate LLM-Turns.
- **Strikte `isError`-Policy:** Saubere Trennung zwischen echten Server-Malfunctions (`IsError=true`) und benutzerseitig korrigierbaren Fehleingaben (`IsError=false` mit Fehlercode und Handlungshinweis).
- **Robuste Assembly-Dekompilation:** Schnelle, zuverlässige Erkennung von Symbolen, Typ-Hierarchien, Klassenstrukturen und Extension-Methoden über Third-Party-DLLs hinweg.
- **Anti-Looping-Hinweise:** Nahezu alle semantischen Tools signalisieren Vollständigkeit mit `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.`, was LLM-Halluzinationen und redundante Grep-Schleifen wirksam unterbindet.

### Haupt-Optimierungspotenziale:
- **Token-Ökonomie bei Assembly-Abfragen:** Zwei isolierte Stellen erzeugen massiven Token-Bloat:
  1. `inspect_assembly` sendet bei gezielten Typabfragen (`typeName="X"`) unverändert die vollständige 32-teilige Referenz- und Session-Liste (~18,4 KB / ~4.500 Tokens für <2% relevante Typdaten).
  2. `find_references(includeReferences=true)` gibt über 100 Zeilen ungedeckelte Version-Mismatch-Diagnosen aus (~24,8 KB / ~6.000 Tokens).

---

## 2. Konsolidierte Befundmatrix

| ID | Komponente / Tool | Schweregrad | Umfang | Dringlichkeit | Titel / Kurzbeschreibung |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **TOK-001** | `inspect_assembly` | **S1** | **U1** | **P1** | Unkonditionale Ausgabe vollständiger Referenz- & Sessionlisten bei aktivem Typ-/Memberfilter (~4.000 Tokens Bloat). |
| **TOK-002** | `find_references` | **S1** | **U1** | **P1** | Ungedeckelter Diagnose-Dump bei `includeReferences=true` (100+ Zeilen Facade-Version-Mismatches, ~6.000 Tokens Bloat). |
| **ASM-002** | `find_assembly_extensions` | **S2** | **U1** | **P2** | `receiverType` filtert bei `not_decidable` (ohne Consumer-Projekt) unpassende Parametertypen nicht syntaktisch heraus. |
| **DOC-001** | `Docs/agent-api.md` | **S2** | **U0** | **P2** | JSON-RPC Tool-Call-Beispiel für `find_symbol` enthält nicht die Pflichtparameter `targetType` und `targetPath`. |
| **DOC-002** | `get_file_tree` | **S3** | **U0** | **P3** | Diskrepanz zwischen Schema-Default (`maxResults=200`) und Beschreibungstext (`Default 100`). |
| **NAV-002** | `get_symbol_body` | **S3** | **U0** | **P3** | Fehlender Inline-Kommentar bei dekompilierten Metadata-only Method-Stubs. |
| **SRC-001** | Semantische Tools | **S3** | **U0** | **P3** | Inkonsistente Primär-Parameterbenennung in Doku (`symbol` vs `symbolIdentifier`). |
| **MET-001** | `get_hotspots` | **S3** | **U0** | **P3** | Fehlender expliziter Sortier- und Schwellwert-Parameter (`sortBy`, `minLineCount`). |

---

## 3. Detaillierte Befundbeschreibungen

### TOK-001 (S1 / U1 / P1): Referenz-Listen-Bloat in `inspect_assembly`
- **Problem:** Bei gefilterten Typ- oder Member-Inspektionen (`typeName="ArtikelDisposition"`, `exactTypeName=true`) werden die vollständigen 32 Referenzen und 32 Referenz-Sessions inklusive langer Decompiler-Diagnosen ausgegeben.
- **Messung:** 18.396 Bytes Antwortgröße. Der tatsächliche Typ belegt lediglich ~300 Bytes (< 2%).
- **Lösung:** Wenn `typeName`, `memberName` oder `namespace` übergeben werden, die Sektionen `Referenzen:` und `Referenz-Sessions:` standardmäßig als Einzeiler zusammenfassen (`Referenzen: 32 von 250 (gekürzt)`). Volle Listen nur bei ungefiltertem Aufruf oder explizitem `includeReferences=true`.

### TOK-002 (S1 / U1 / P1): Ungedeckelte Diagnosen bei `find_references(includeReferences=true)`
- **Problem:** Die Einbeziehung von Referenz-Assemblies führt zur zeilenweisen Ausgabe sämtlicher Netstandard/Framework-Versionsabweichungen im Fließtext.
- **Messung:** 24.815 Bytes, 103 Zeilen für eine Abfrage mit 0 Aufrufstellen.
- **Lösung:** Capping der `[Assembly-Diagnostic]`-Zeilen im Text-Formatter auf maximal 3–5 Zeilen mit zusammenfassender Summenzeile (`Diagnosen: 5 von 95 (gekürzt)`).

### ASM-002 (S2 / U1 / P2): `receiverType`-Filterung in `find_assembly_extensions`
- **Problem:** Wird `receiverType="SqlConnection"` übergeben, liefert das Tool ohne Consumer-Projekt alle Extension-Methoden (auch solche für `GenericDevice`), da alle als `not_decidable` klassifiziert werden.
- **Lösung:** Wenn Roslyn die Typkonvertierung nicht entscheiden kann (`not_decidable`), soll ein syntaktischer Namensabgleich auf den ersten Methodenparameter angewendet werden.

### DOC-001 (S2 / U0 / P2): Veraltetes Tool-Call-Snippet in `Docs/agent-api.md`
- **Problem:** Das JSON-RPC-Beispiel für `find_symbol` auf Zeile 731–742 enthält nur `namePatterns` und `maxResults`. Ohne `targetType` und `targetPath` schlägt der Aufruf fehl.
- **Lösung:** Aktualisierung des Snippets mit vollständigem Target-Block.
