# Verbesserungen für die Analyse fremder .NET-Assemblies

## Problemstellung

Der Assembly-MCP-Pfad kann eine lokal vorhandene .NET-Assembly lesen, Typen
finden und verfügbare Methodenkörper aus einer dekompilierten Roslyn-Ansicht
bereitstellen. Für eine allgemeingültige Untersuchung fremder Assemblies
bestehen jedoch mehrere fachliche und ergonomische Lücken:

1. **Referenzrauschen:** Bei einer Suche mit aktivierter Referenzauflösung
   erscheinen Laufzeit-, Framework- und weitere Referenzsymbole gemeinsam mit
   den Symbolen der Ziel-Assembly. Das erschwert die Beantwortung einer Frage
   zur Ziel-Assembly und kann große Antworten abschneiden. Standardmäßig sollen
   nur Symbole der Ziel-Assembly ausgegeben werden.
2. **Fehlende gezielte Cross-Assembly-Navigation:** Die Metadaten einer
   Assembly enthalten Referenzen, aber der Analyseablauf bietet keine klar
   getrennte, kompakte Sicht auf referenzierte Assemblies und deren
   Auflösungsstatus. Für einen Aufruf über mehrere Assemblies muss erkennbar
   sein, welche Referenz geladen werden kann und an welcher Grenze die Analyse
   endet.
3. **Unklare Herkunft von Methodenkörpern:** Eine dekompilierte Darstellung
   darf nicht wie eine Originalquelle wirken. Herkunft, Vertrauensstufe und
   mögliche Abweichungen müssen direkt am Methodenkörper sichtbar sein.
4. **Schwierige Symbolauswahl bei Überladungen:** Lange, generationsgebundene
   Symbol-IDs sind fehleranfällig. Eine Suchantwort muss deshalb einen direkt
   wiederverwendbaren Identifikator und die eindeutige Signatur liefern.
5. **Unzureichende Vollständigkeitsbewertung:** Eine Assembly kann trotz
   fehlender Referenzen lesbare Methodenkörper besitzen. Umgekehrt kann ein
   scheinbar erfolgreicher Treffer durch fehlende Abhängigkeiten fachlich
   unvollständig sein. Die Einschränkung muss pro Ergebnis und nicht nur auf
   Session-Ebene erkennbar sein.
6. **Keine allgemeine Persistenzsicht:** Direkte SQL-Strings sind auffindbar,
   aber Speicherung kann ebenso über Repositories, ORMs, Dateien,
   Serialisierung, Netzwerkaufrufe oder Methoden in Referenz-Assemblies
   erfolgen. Die Untersuchung darf nicht von sprechenden Methodennamen
   abhängen.

## Zielbild

Der MCP soll bei einer fremden Assembly standardmäßig eine fokussierte,
statische und nachvollziehbare Analyse liefern:

- Die Ziel-Assembly bildet den Root-Scope.
- Framework- und Referenzsymbole bleiben ausgeblendet, solange sie nicht für
  eine angeforderte Navigation benötigt werden.
- Referenzauflösung ist ein kontrollierter Folge-Schritt und kein impliziter
  Vollimport.
- Jeder Befund unterscheidet zwischen direkt nachgewiesen, indirekt verfolgt
  und nicht bestimmbar.
- Dekompilierte Darstellung wird niemals als Originalquelle bezeichnet.
- Die Analyse bleibt read-only und führt keine Assembly aus.

## Vorschläge

### 1. Root-Scope als verbindlicher Standard

Für `inspect_assembly`, `find_symbol`, `get_symbol_body`,
`find_references`, `get_call_tree` und `get_impact` sollte der Standard immer
die angegebene Ziel-Assembly sein. Referenzsymbole dürfen nur erscheinen,
wenn sie:

- Teil eines ausdrücklich angeforderten Cross-Assembly-Pfades sind,
- zur Auflösung eines Zielknotens zwingend benötigt werden oder
- über einen expliziten Filter angefordert wurden.

Die Antwort sollte den Scope ausweisen:

```json
{
  "scope": {
    "rootAssembly": "...",
    "includedAssemblies": ["..."],
    "excludedFrameworkAssemblies": true
  }
}
```

Die konkreten Namen und Pfade bleiben ausschließlich in der MCP-Antwort und
werden nicht in dauerhafte Task-Dokumente übernommen.

### 2. Referenzen separat und metadata-only auflisten

Zusätzlich zu einer Analyse sollte eine kompakte Referenzübersicht verfügbar
sein, beispielsweise über `get_assembly_references` oder eine klar getrennte
Option in `inspect_assembly`.

Empfohlene Informationen je Referenz:

```json
{
  "name": "...",
  "requestedIdentity": "...",
  "resolvedPath": "...",
  "resolution": "resolved|missing|version-mismatch|not-attempted",
  "framework": false,
  "sessionOpened": false
}
```

Dabei soll die reine Metadatenauflistung keine Referenz-Session öffnen. Die
bestehende Referenzexpansion bleibt ein gesonderter, expliziter Schritt.

### 3. Selektive Cross-Assembly-Navigation

Für einen sichtbaren Aufrufknoten sollte der Agent gezielt die benötigte
Referenz-Assembly öffnen können. Die Antwort muss pro Kante Herkunft und
Vollständigkeit tragen:

```json
{
  "from": "...",
  "to": "...",
  "origin": "root|reference",
  "referenceAssembly": "...",
  "resolution": "resolved|unresolved",
  "completeness": "complete|partial|unavailable"
}
```

Das vorhandene Session- und Generationsmodell kann dafür weiterverwendet
werden. Wichtig ist die Trennung zwischen „Referenz ist bekannt“ und
„Referenz wurde tatsächlich für die Navigation geladen“.

### 4. Herkunft am Body eindeutig machen

Die Body-Antwort sollte ein eindeutiges Provenienzfeld erhalten:

```json
{
  "sourceKind": "original|source-backed|decompiled|unavailable",
  "isOriginalSource": false,
  "confidence": "high|medium|low",
  "bodyAvailability": "available|unavailable"
}
```

Für dekompilierte Bodies ist im Text ein kurzer Warnhinweis erforderlich.
`contentMode=source` sollte nicht mehr für eine dekompilierte Darstellung
verwendet werden. Bestehende API-Kompatibilität kann durch ein zusätzliches
Feld hergestellt werden; die semantisch eindeutige Kennzeichnung muss jedoch
maßgeblich sein.

### 5. Symbol-Handles für sichere Folgeaufrufe

`find_symbol` sollte neben der Anzeige-Signatur einen aktuellen,
maschinenlesbaren Handle zurückgeben. `get_symbol_body` und die
Navigationswerkzeuge sollten diesen Handle unverändert akzeptieren.

Der Handle sollte:

- die aktuelle Assembly-Generation einschließen,
- Overloads eindeutig unterscheiden,
- nicht manuell aus langen Texten zusammengesetzt werden müssen,
- bei veralteter Generation eine erneute Suche mit konkretem Hinweis verlangen.

Zusätzlich sollte die Antwort eine stabile, lesbare Signatur liefern. Ein
fehlgeschlagener Folgeaufruf sollte die zuletzt passenden Kandidaten strukturiert
zur erneuten Auswahl zurückgeben.

### 6. Ergebnisbezogene Vollständigkeit und Diagnosewirkung

Neben dem Sessionstatus sollte jedes relevante Analyseergebnis ausweisen:

- ob der Body verfügbar ist,
- ob der betroffene Syntaxbaum parsebar ist,
- ob benötigte Parametertypen aufgelöst wurden,
- ob der Aufrufpfad an einer fehlenden Referenz endet,
- ob der Befund nur den Root-Scope umfasst.

Nicht relevante Fehler aus transitiven Framework-Quellen dürfen die Bewertung
eines fehlerfrei lesbaren Ziel-Bodies nicht pauschal abwerten. Umgekehrt muss
ein fehlender Typ auf dem konkreten Aufrufpfad die Antwort auf `partial` oder
`unavailable` setzen.

### 7. Allgemeine Persistenz- und Seiteneffekt-Suche

Für beliebige Assemblies sollte ein read-only Werkzeug oder Composite-Workflow
folgende Seiteneffekt-Senken erkennen und nachverfolgen:

- SQL `INSERT`, `UPDATE`, `DELETE`, Abfragen und Stored-Procedure-Aufrufe,
- ORM- und Repository-Schreiboperationen,
- Datei- und Konfigurationsschreibvorgänge,
- Serialisierung in persistente Formate,
- Netzwerk- oder Serviceaufrufe,
- Aufrufe in Referenz-Assemblies.

Ein möglicher Name ist `find_persistence_operations`. Die Ausgabe sollte
Operation, betroffene Ressource, enthaltendes Symbol, Aufrufpfad, Herkunft
und Vertrauensstufe getrennt liefern. Die Erkennung muss auf Syntax, Semantik
und bekannten API-Senken beruhen; reine Namenssuche darf nur als ergänzender
Hinweis gelten.

Beispiel für die fachliche Klassifikation:

| Klassifikation | Bedeutung |
| --- | --- |
| `direct` | Schreib- oder Leseoperation im sichtbaren Methodenkörper |
| `transitive` | Operation über einen verfolgten Aufrufpfad |
| `metadata-only` | Ressource aus Signatur oder Assembly-Metadaten abgeleitet |
| `unresolved` | Aufrufziel oder Referenz nicht verfügbar |

### 8. Verbesserter Standard-Workflow für Agenten

Der MCP-Server sollte im Tooltext und in den strukturierten Antworten einen
kurzen Folgeworkflow unterstützen:

1. Ziel-Assembly laden und Identität, Herkunft und Referenzen kompakt prüfen.
2. Root-Scope ohne Referenzexpansion nach passenden Typen und Symbolen
   durchsuchen.
3. Body und ausgehenden Call Tree des ausgewählten Symbols lesen.
4. Nur sichtbare externe Knoten selektiv über ihre Referenz-Assembly verfolgen.
5. Seiteneffekte und Persistenzoperationen klassifizieren.
6. Ergebnis mit direkter, transitiver und unbekannter Evidenz ausgeben.

Damit bleibt die Ausgabe klein, reproduzierbar und für andere Assemblies
übertragbar.

## Umsetzungsreihenfolge

1. **P1:** Root-Scope und Framework-Filter als Standard festlegen; Scope und
   Herkunft in Text und `structuredContent` synchron ausgeben.
2. **P1:** Referenzübersicht ohne Sessionexpansion ergänzen und
   Auflösungsstatus modellieren.
3. **P1:** Provenienzfelder für Bodies vereinheitlichen und die Bezeichnung
   dekompilierter Inhalte korrigieren.
4. **P2:** Symbol-Handles und Overload-Roundtrip zwischen `find_symbol` und
   `get_symbol_body` einführen.
5. **P2:** Ergebnisbezogene Vollständigkeit und Diagnosewirkung ergänzen.
6. **P2:** Selektive Cross-Assembly-Navigation auf Basis des vorhandenen
   Session-/Generationsmodells ergänzen.
7. **P2:** Allgemeine Persistenz-/Seiteneffekt-Suche als fokussierten
   Composite-Workflow umsetzen.

## Akzeptanzkriterien

- Eine Assembly-Symbolsuche zeigt standardmäßig keine Framework- oder fremden
  Referenzsymbole.
- Eine explizite Cross-Assembly-Navigation zeigt nur die tatsächlich
  einbezogenen Referenzen und deren Auflösungsstatus.
- Jeder Body weist eindeutig `original`, `source-backed`, `decompiled` oder
  `unavailable` aus.
- Ein `find_symbol`-Ergebnis kann ohne manuelles Bearbeiten als Folgeargument
  für `get_symbol_body` verwendet werden.
- Große Referenzgraphen erzeugen eine begrenzte, strukturierte Antwort mit
  sichtbarer Trunkierung statt eines ungerichteten Dumps.
- Fehlende Abhängigkeiten werden als fachliche Grenze und nicht als globale
  unbelegte Schlussfolgerung ausgegeben.
- Persistenzbefunde unterscheiden direkte, transitive und nicht auflösbare
  Operationen.

## Verifikation nach Umsetzung

Die Umsetzung sollte gezielte Fast- und Integrationstests für folgende Fälle
erhalten:

- Assembly mit vielen Framework-Referenzen,
- fehlende und versionsabweichende Referenz,
- expliziter Root-Only- und Cross-Assembly-Aufruf,
- überladene Symbole und Generationwechsel,
- dekompilierter Body ohne verfügbare Originalquelle,
- große Antworten mit begrenzter Referenz- und Diagnoseanzahl,
- direkte und indirekte Persistenz-Senken.

Für diesen Dokumentations-Task werden keine Builds oder Tests ausgeführt.
