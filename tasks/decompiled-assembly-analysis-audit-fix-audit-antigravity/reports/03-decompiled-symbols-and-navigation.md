# Audit-Report 03: Decompiled Symbols, Navigation, Call Trees & Method Bodies

**SubAgent:** SubAgent 3 (Symbols & Navigation)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Geprüfte Tools:** `find_symbol`, `get_symbol_body`, `get_class_structure`, `get_type_hierarchy`, `get_namespace_tree`, `find_references`, `get_call_tree`  
**Test-Ziele (anonymisiert):** `Vendor.Pps.RealTimeData.dll`, `Vendor.Data.dll`

---

## 1. Getestete Szenarien & Ergebnisse

### 1.1 `find_symbol` (Assembly-Target)
- Schnelle Identifikation von Klassen, Methoden und Typen in der dekompilierten Session (z. B. `namePatterns: ["ArtikelDisposition"]`).
- Liefert relative synthetische Dateipfade (`00006-Vendor_Pps_RealTimeData_ArtikelDisposition.cs:11`) und Typinformationen.

### 1.2 `get_symbol_body` (Assembly-Target)
- Gibt die dekompilierte Member-Signatur (z. B. `private static decimal CalculateBestand(...)`) aus.
- Provenienz-Header (`origin=decompiled`, `trust=untrusted`, Hash) und Vollständigkeitshinweise sind konsistent enthalten.

### 1.3 `get_class_structure` (Assembly-Target)
- Erzeugt eine vollständige Markdown-Tabelle über alle 14 Member der Klasse `ArtikelDisposition` (Konstruktoren, Methoden, Properties, private Felder).
- Filterung nach `kindFilter`, `nameFilter` und `sortBy` funktioniert fehlerfrei.

### 1.4 `get_type_hierarchy` (Assembly-Target)
- Löst Basisklassen (`BusinessProcessBase`, `object`) und externe Schnittstellen (`IBusinessProcess`, `IQueryBusinessProcessParameters`) präzise auf und kennzeichnet externe Typen transparent (`(extern, keine Datei im Repo)`).

### 1.5 `get_namespace_tree` (Assembly-Target)
- Liefert Projekt- und Namespace-Struktur der dekompilierten Assembly mit drill-down Fähigkeit.

### 1.6 `find_references` & `get_call_tree`
- Aufrufstellen-Traversierung und Call-Tree-Generierung (ASCII & Mermaid-Format `flowchart TD`) funktionieren auf der dekompilierten Session.

---

## 2. Befunde & Optimierungspotenziale

### Befund NAV-001 (S1 / U1 / P1): Unbegrenzter Diagnose-Dump bei `find_references(includeReferences=true)`
- **Beschreibung:** Wenn `find_references` mit `includeReferences=true` gegen eine dekompilierte Assembly aufgerufen wird, gibt das Tool sämtliche aufgetretenen Assembly-Version-Mismatch-Diagnosen zeilenweise im Textoutput aus (`[Assembly-Diagnostic] Kein identitätsgleicher Kandidat für 'System.Collections' ...`).
- **Messung:**
  - 103 Zeilen Textausgabe, 24.815 Bytes (~6.000 Tokens) bei einem einzigen Aufruf!
  - 95% der Antwort bestehen aus repetitiven Framework-Facade-Version-Mismatches (z. B. System.IO, System.Linq, System.Net), obwohl für das eigentliche Symbol keine Referenzen vorlagen.
- **Auswirkung:** Massive Belastung des LLM-Kontextfensters und Token-Vergeudung.
- **Empfehlung:** Analog zu `get_server_health` und `inspect_assembly` müssen auch `find_references` und `get_call_tree` ein striktes Limit für Diagnosezeilen im Fließtext einhalten (z. B. max. 3-5 Zeilen mit zusammenfassender Angabe `Diagnosen: 3 von 95 (gekürzt)`).
- **Klassifizierung:** Schweregrad `S1` (Kritisch/Token-Ökonomie), Umfang `U1` (Komponente), Dringlichkeit `P1`.

### Befund NAV-002 (S3 / U0 / P3): Fehlender Kontext-Hinweis bei Metadata-Only Method-Stubs in `get_symbol_body`
- **Beschreibung:** Da die Assembly dekompiliert im Metadata-Only-Modus vorliegt, liefert `get_symbol_body` nur die Signatur mit Semikolon (ohne Rumpf `{ ... }`).
- **Auswirkung:** Ein Agent könnte annehmen, dass es sich um eine abstrakte Methode oder Interface-Methode handelt, obwohl es eine implementierte konkrete Methode ist.
- **Empfehlung:** Ergänzung eines kurzen Hinweises: `// [Hinweis: Metadata-only Stub - Methodenrumpf erfordert Source-Backing oder CIL-Decompiler]`.
- **Klassifizierung:** Schweregrad `S3` (Minor DX), Umfang `U0` (Lokal), Dringlichkeit `P3`.

---

## 3. Fazit SubAgent 3
Die Navigation, Symbolauflösung, Typ-Hierarchie und Klassenstrukturierung über dekompilierte Assemblys sind funktional erstklassig. Die Eindämmung des ungefilterten Diagnose-Dumps bei `includeReferences=true` (NAV-001) ist dringend geboten, um die Token-Effizienz auf das gewohnte Spitzenniveau zu heben.
