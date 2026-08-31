# Audit-Report 02: Decompiled Assembly Inspection & Extension-Methoden

**SubAgent:** SubAgent 2 (Assembly Inspection & Extensions)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Geprüfte Tools:** `inspect_assembly`, `find_assembly_extensions`  
**Test-Ziele (anonymisiert):** `Vendor.Pps.RealTimeData.dll`, `Vendor.Data.dll`, `Vendor.Rewe.Buchungserfassung.dll`

---

## 1. Getestete Szenarien & Ergebnisse

### 1.1 `inspect_assembly`
- **Volle Assembly-Inspektion (ohne Typfilter):**
  - Liefert Identität, Version, Kultur, Namespaces, Referenzen (Tiefe, Zustand, Pfad), Referenz-Sessions und API-Typen mit Member-Signaturen.
  - Decompiler-Provenienz (`decompiled`), Hash, Confidence (`medium`) und Trust (`untrusted`) werden sauber im Header ausgewiesen.
- **Gezielte Typ-Filterung (`typeName="ArtikelDisposition"`, `exactTypeName=true`):**
  - Findet exakt den gewünschten Typen.
- **Sichtbarkeitsfilter (`publicOnly=true` vs `publicOnly=false`):**
  - `publicOnly=true`: Liefert nur öffentliche Klassen und Konstruktoren/Methoden.
  - `publicOnly=false`: Schaltet erfolgreich um und liefert auch private/interne Felder (z. B. `_artikelnummer`), Properties und Hilfsmethoden.
- **Member-Filterung (`memberName`, `memberNames`):**
  - Funktioniert einwandfrei zur selektiven Extraktion einzelner Signaturen.

### 1.2 `find_assembly_extensions`
- **Erkennung von Extension-Methoden:**
  - Findet z. B. in `Vendor.Data.dll` zuverlässig die 4 Extension-Methoden (`RenewAccessToken`, `OpenWithRetry`, `GetVersionFromUSysSetup`).
  - Weist den Grund (`Kein auflösbarer Consumer-Typ angegeben`) und den Status `not_decidable` transparent aus.
- **Extension-Name-Filter (`extensionName="RenewAccessToken"`):**
  - Reduziert die Treffermenge präzise auf 1 Extension.
- **Token-Footprint:**
  - `find_assembly_extensions` fasst Referenzen vorbildlich zusammen (`Referenzen: 32 von 33 (gekürzt)`, `Referenz-Sessions: 32 von 98 (gekürzt)`) und vermeidet unnötigen Token-Ballast.

---

## 2. Befunde & Optimierungspotenziale

### Befund ASM-001 (S1 / U1 / P1): Massiver Token-Bloat durch unkonditionale Referenz-Listen in `inspect_assembly`
- **Beschreibung:** Selbst wenn ein Agent mit `typeName="ArtikelDisposition"` und `exactTypeName=true` gezielt nach einer einzigen Klasse mit 1 Member sucht, gibt `inspect_assembly` die vollständige Liste aller 32 Referenzen und 32 Referenz-Sessions mit detaillierten Fehlermeldungen und CS-Diagnosen aus.
- **Messung:** 
  - Gesamtnutzlast: 18.396 Bytes (~4.500 Tokens).
  - Anteil des angeforderten Typs: ~300 Bytes (< 2% der Antwort!).
  - Über 98% der Payload bestehen aus statischen, sich bei jedem Aufruf wiederholenden Referenz-Details.
- **Auswirkung:** Extrem hoher Token-Verbrauch im Agenten-Kontext bei wiederholten Typabfragen derselben Assembly.
- **Empfehlung:** Wenn Typ- oder Member-Filter (`typeName`, `memberName`, `namespace`) aktiv sind, sollte die Referenz-Sektion standardmäßig als kompakte Zusammenfassung (1 Zeile) ausgegeben werden – analog zu `find_assembly_extensions`. Nur bei ungefilterter Übersicht oder explizitem Flag `includeReferences=true` soll die vollständige Liste gerendert werden.
- **Klassifizierung:** Schweregrad `S1` (Kritisch/Token-Ökonomie), Umfang `U1` (Komponente), Dringlichkeit `P1`.

### Befund ASM-002 (S2 / U1 / P2): Mangelnde Vorfilterung von `receiverType` bei `not_decidable` in `find_assembly_extensions`
- **Beschreibung:** Wird `receiverType="SqlConnection"` übergeben, werden dennoch alle 4 Extensions (inklusive `GenericDeviceExtensions.GetVersionFromUSysSetup` für `GenericDevice`) zurückgegeben. Da ohne Consumer-Projekt keine Roslyn-Typkonvertierung möglich ist, markiert das Tool alle Extensions als `not_decidable` und listet sie ungefiltert auf.
- **Auswirkung:** Der Agent erhält Treffer, die syntaktisch offensichtlich nichts mit dem gesuchten Receiver zu tun haben.
- **Empfehlung:** Wenn die semantische Roslyn-Prüfung `not_decidable` liefert, sollte ein heuristischer Name-Match auf den Parametertyp des ersten Methodenarguments angewendet werden, um offensichtlich unpassende Typen auszublenden.
- **Klassifizierung:** Schweregrad `S2` (Mittel), Umfang `U1` (Komponente), Dringlichkeit `P2`.

---

## 3. Fazit SubAgent 2
Die Decompiler-Engine und Extension-Erkennung arbeiten inhaltlich präzise und schnell. Die Behebung von ASM-001 (Kompaktierung der Referenz-Sektion bei Typfiltern) ist eine der wirksamsten Maßnahmen zur Senkung des Token-Verbrauchs im gesamten MCP-Server.
