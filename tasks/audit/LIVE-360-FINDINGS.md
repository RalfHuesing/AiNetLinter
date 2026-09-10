# Live-360-MCP-UX-Befunde

## Kontext

Die Befunde wurden ausschliesslich lesend am laufenden MCP-Server mit einem zweiten Source-Projekt und punktuellen Gegenproben erhoben. Sie beschreiben allgemeine Vertragsprobleme; das zweite Target machte sie nur sichtbar. Keine Dateien, Tests, Builds, Commits oder Feedback-Aufrufe wurden in diesem Nachaudit ausgefuehrt.

`Major`: Ein Coding-Agent kann bei einer normalen Analyse falsche Schlussfolgerungen ziehen, unerwartet grosse Antworten erhalten oder einen Handoff nicht stabil fortsetzen. `Minor`: Die Funktion bleibt nutzbar, aber der Vertrag ist missverstaendlich.

## Prioritaet 1 – Routing und Fehler-UX

### L360-001 – Globaler Health-Call gibt Detaildaten aus

- **Schweregrad:** Major
- **Tool:** `get_server_health`
- **Reproduktion:** Bei vorhandener Assembly-Session global ohne `targetPath`, aber mit `includeDiagnostics=true` aufrufen.
- **Ist:** Die Antwort kann Session-, Herkunfts-, Diagnose- und Detailmetadaten statt nur Aggregate enthalten.
- **Soll:** Der globale Vertrag bleibt aggregiert; Detaildaten gibt es nur mit konkretem `targetPath` oder das Detailflag ist global unzulaessig.
- **Agentenwirkung:** Schlechter SNR, unerwartete Antwortgroesse und keine stabile Erwartung an globale Health-Aufrufe.
- **Loesungsansatz:** Detailflags vor globalem Dispatch ablehnen oder Builder dort strikt auf Aggregate begrenzen. E2E-Routenmatrix global/Source/Assembly testen.

### L360-002 – Diagnose-Limit ist im Source-Routing nicht verbindlich

- **Schweregrad:** Major
- **Tool:** `get_server_health`
- **Reproduktion:** Source-Target mit `includeDiagnostics=true, maxDiagnostics=0` oder negativ aufrufen.
- **Ist:** Erfolg mit still auf Default gesetztem Limit; andere Routen weisen denselben Wert feldgenau ab.
- **Soll:** In jeder Route `INVALID_ARGUMENT` mit `$.maxDiagnostics`.
- **Agentenwirkung:** Ein bewusst gesetztes Response-Budget ist nicht verlaesslich.
- **Loesungsansatz:** Gemeinsame Validierung vor jeder Source-/Assembly-/Global-Verzweigung. Test muss Daemon und Stdio einschliessen, nicht nur den Handler.

### L360-009 – Pflichtfeld und Array-Elementfehler umgehen die Fehler-UX

- **Schweregrad:** Major
- **Tools:** mehrere zielgebundene Tools
- **Reproduktion A:** `targetPath` ganz weglassen. **B:** In einem String-/Number-Array ein Element falschen JSON-Typs uebergeben.
- **Ist:** Generischer fataler Invoke-Fehler ohne Code, Feldpfad oder Korrekturhinweis.
- **Soll:** Recoverable `INVALID_ARGUMENT`, `isError=false`, Feldpfad inklusive Array-Index und erwarteter JSON-Typ.
- **Agentenwirkung:** Der Agent kann seine Eingabe nicht gezielt korrigieren.
- **Loesungsansatz:** Serverweiten Argumentfilter um fehlende Pflichtparameter und rekursive Array-Elementvalidierung erweitern; ueber den echten MCP-Transport testen.

## Prioritaet 2 – Begrenzung und Vollständigkeit

### L360-003 – Namespace-Praefix ist nicht normalisiert

- **Schweregrad:** Major
- **Tool:** `get_namespace_tree`
- **Reproduktion:** Sichtbaren Unter-Namespace ohne abschliessenden Punkt als `namespacePrefix` aufrufen und mit derselben Eingabe plus Punkt vergleichen.
- **Ist:** Ohne Punkt entsteht eine vollstaendig wirkende Leermenge, mit Punkt ein grosser Teilbaum.
- **Soll:** Semantisch gleiche Praefixe mit oder ohne Punkt verhalten sich gleich; exakter Namespace und Teilbaum sind klar definiert.
- **Agentenwirkung:** Progressive Namespace-Erkundung bricht ohne sichtbaren Fehler ab.
- **Loesungsansatz:** Eingabe normalisieren; Fixture mit exaktem Namespace und Unterraeumen verwenden.

### L360-004 – Response-Budget wird nicht durchgesetzt

- **Schweregrad:** Major
- **Tools:** mindestens `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`
- **Reproduktion:** Sehr kleines positives `maxResponseBytes` an eine grosse Antwort geben.
- **Ist:** Text und StructuredContent bleiben praktisch vollstaendig; Navigation meldet `complete`.
- **Soll:** Budget begrenzt die ausgelieferte Nutzlast oder wird nicht angeboten. Begrenzung muss Gesamt-/Shown-Werte, `truncated` und `next` ausweisen.
- **Agentenwirkung:** Kontext- und Wire-Kosten sind nicht steuerbar.
- **Loesungsansatz:** Gemeinsamen Response-Budget-Adapter vor Serialisierung nutzen; Text und StructuredContent aus derselben sichtbaren Teilmenge erzeugen.

### L360-005 – `metrics_tree.topN` hat zwei Bedeutungen

- **Schweregrad:** Major
- **Tool:** `metrics_tree`
- **Reproduktion:** Baum mit mehr Kindern als `topN` abfragen.
- **Ist:** Text zeigt Top-N mit Auslassung; StructuredContent alle Kinder; Navigation `complete`.
- **Soll:** Ein Parameter begrenzt beide Darstellungen gleich oder die Sichten haben klar verschiedene Parameter.
- **Agentenwirkung:** Text- und strukturierte Agenten erhalten semantisch verschiedene Antworten.
- **Loesungsansatz:** Sichtbaren Baum einmal erzeugen, beide Repräsentationen daraus ableiten, Zaehlwerte und Trunkierung ergaenzen.

### L360-007 – Grenzwerte werden uneinheitlich still korrigiert

- **Schweregrad:** Major
- **Tools:** mehrere Listen-, Hierarchie-, Body- und Traversierungstools
- **Reproduktion:** `maxResults`, `topN`, `maxMembers`, `maxBodyLines` oder `depth` auf `0` bzw. ausserhalb des Bereichs setzen.
- **Ist:** Manche Tools fehlern, andere machen still `1` daraus oder ignorieren die Begrenzung.
- **Soll:** Gleiche Parameterklasse, gleiche Vertragsregel: `INVALID_ARGUMENT` oder strukturierte `requested/effective/clamped`-Metadaten.
- **Agentenwirkung:** Limits eignen sich nicht als verlaessliche Steuerung fuer Agentenloops.
- **Loesungsansatz:** Zentrale Limit-Validatoren je Parameterklasse plus parametrisierte Vertragsmatrix ueber alle registrierten Tools.

## Prioritaet 3 – Symbolfilter und Chaining

### L360-006 – Symbolartfilter ist nicht strikt

- **Schweregrad:** Major
- **Tool:** `find_symbol`
- **Reproduktion:** Muster mit mehreren Symbolarten und `kind=Class` oder `kind=Method` abfragen.
- **Ist:** Antwort enthaelt auch abweichende Symbolarten.
- **Soll:** Jeder Treffer entspricht der angeforderten kanonischen Symbolart.
- **Agentenwirkung:** Folgeaufrufe koennen auf falschen Symbolarten starten.
- **Loesungsansatz:** Filter vor Zaehlung, Limit und Formatierung anwenden; Fixture mit Klasse, Record, Feld und Methode gleicher Namenswurzel.

### L360-008 – Skeleton und Feature-Context sind teilweise textgebunden

- **Schweregrad:** Major
- **Tools:** `get_file_skeleton`, `get_feature_context`
- **Ist:** Skeleton-IDs/Signaturen stehen nur im Markdown. Feature-Context nennt Caller im Text, aber ohne stabile Caller-ID und Position in StructuredContent.
- **Soll:** Strukturierte Dateien/Typen/Member/IDs beziehungsweise Caller-ID und Quellposition sind direkt an Folge-Tools uebergabefaehig.
- **Agentenwirkung:** Textparsing statt verlässlichem Tool-Chaining.
- **Loesungsansatz:** Additive DTOs einführen, Texte erhalten; E2E-Handoff-Test konsumiert die IDs direkt.

## Weitere Vertragsbefunde

### L360-010 – Health-Snapshot ist nicht korrelierbar

- **Schweregrad:** Minor
- **Ist:** Target-Health meldet `snapshot=unavailable`; direkt folgende Analyse meldet frischen Snapshot.
- **Loesungsansatz:** Health liefert bekannten Session-Snapshot oder kennzeichnet explizit, dass es keinen Snapshot erhebt.

### L360-011 – Ungueltige Enumwerte fallen still auf Defaults zurueck

- **Schweregrad:** Minor
- **Beispiele:** Unbekannte Severity im Violation-Filter und unbekanntes Call-Tree-Format.
- **Loesungsansatz:** `INVALID_ARGUMENT` oder mindestens `effectiveValue` und sichtbarer Fallback-Hinweis.

### L360-012 – Clamp-Metadaten sind nicht einheitlich

- **Schweregrad:** Minor
- **Ist:** Einige Tools zeigen nur im Text oder gar nicht, dass Tiefe begrenzt wurde.
- **Loesungsansatz:** Einheitliche Felder `requestedDepth`, `effectiveDepth`, `depthWasClamped` in StructuredContent.

### E-003 – `completeness` bleibt bewusst deferred

- **Schweregrad:** Deferred
- **Grund:** Der oeffentliche Vertrag nutzt `navigation.completeness` sowohl fuer Ergebnisvollstaendigkeit als auch fuer Verfuegbarkeitszustände. Eine Bereinigung ohne Migrationsvertrag waere breaking.
- **Folgeauftrag:** Nur als eigene Vertragsrevision mit Dokumentation, Schema-, Test- und Migrationsplanung behandeln.
