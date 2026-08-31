---
status: ready
---

# Konzept: 360-Grad-Audit der externen Assembly-Analyse

## 1. Ziel und Nutzen

Dieses Vorhaben prüft die aktuell implementierte Funktionalität rund um die
Analyse lokaler .NET-Assemblies, die statische Decompilation und die optionale
Auflösung externer Quellen über einen Git-Checkout. Der Audit soll belastbar
zeigen, ob Verhalten, Fehlerverträge, Lebenszeiten, Agenten-Nutzbarkeit,
Token-Effizienz und Dokumentation den tatsächlichen Anforderungen entsprechen.

Das Ergebnis ist kein Patch, sondern ein nachvollziehbarer Tech-Debt-Bestand:
jede Feststellung erhält reproduzierbare Belege, eine Schweregradklasse und
eine Umfangsklasse. Nicht nachweisbare Vermutungen werden als offene
Abdeckung oder Limitation gekennzeichnet und nicht als Befund ausgegeben.

## 2. Verbindlicher Scope

### Im Scope

- MCP-Zielrouting für `targetType=assembly`, absolute DLL-Pfade,
  Pfadvalidierung und Verhalten bei ungültigen oder nicht erreichbaren Zielen.
- Metadata-only-Verhalten: kein Laden und keine Ausführung der Zielassembly,
  inklusive Referenzauflösung, partieller Ergebnisse, Diagnosen, Limits,
  Trunkierung und Herkunftskennzeichnung.
- Statische Decompilation als Fallback, explizite Source-Zuordnung als
  bevorzugter Pfad sowie die Konsistenz von Typ-, Member-, Parameter-,
  Symbol-, Referenz- und Call-Tree-Antworten.
- Konfigurationsmodell und Validierung für externe Quellen, Mapping-Auswahl,
  Mehrdeutigkeiten, Provider-Auswahl, nicht verfügbare Provider und sichere
  Behandlung von Zugangsdaten.
- Git-Transport und Repository-Akquisition: Argumente, Umgebungsvariablen,
  Prompts, Timeout, Abbruch, Prozessbaum, Exit-Codes, Diagnose-Redaktion,
  Checkout-Verifikation und Revisionsbindung.
- Source-Checkout- und Snapshot-Lebenszeit: Besitz, Cleanup, Reparse-Points,
  Pfadgrenzen, atomare Veröffentlichung, Wiederverwendung, Refresh,
  Kapazitätsgrenzen, Idle-TTL, parallele Zugriffe und Fehler nach bereits
  erfolgreicher Teilphase.
- MCP-Komposition, Tool-Registrierung, Toolbeschreibungen, Wire-Modelle,
  Fehlersemantik, Session-/Generation-Status und Eignung für Agenten.
- Token-Effizienz der Agentenoberfläche: progressive Exploration, sinnvolle
  Defaults, strukturierte Nutzlasten, Trunkierungs- und Suffizienz-Hinweise,
  Diagnosegrößen und vermeidbare Antwortduplikation.
- Testabdeckung, Testklassifikation, fehlende End-to-End-Verträge sowie die
  Synchronität und sprachliche Harmonie von `README.md`, relevanten Dateien in
  `Docs/`, `rules.json`, Toolbeschreibungen und Agentenregeln.

### Nicht im Scope

- Jede Änderung an Produktionscode, Tests, Konfiguration oder bestehender
  Dokumentation.
- Refactorings, Bereinigung von Tech Debt oder das Abschwächen von
  Assertions. Empfehlungen dürfen nur als nicht implementierte
  Remediation-Hypothese im Report stehen.
- Analyse oder Ausführung von COM- oder sonstigen nicht unterstützten
  Binärformaten als Funktionsversprechen.
- Speicherung von Zugangsdaten, vollständigen externen URLs, lokalen
  Installationspfaden oder anderen sensiblen Daten in Reports, Commits oder
  dem Tech-Debt-Report.
- Ungezielte Gesamtanalyse des restlichen Linters. Befunde außerhalb des
  Scopes werden nur aufgenommen, wenn sie die geprüfte Funktion oder die
  Agenten-Nutzbarkeit direkt beeinträchtigen.

## 3. Aktuelle betroffene Strukturen

Die Prüfung orientiert sich am aktuellen Code und an den aktuellen
MCP-Verträgen, nicht an früheren Planungsständen. Zentrale Einstiegspunkte
sind:

- `src/AiNetLinter/Mcp/Assemblies/Analysis/` für Analyse-Sessions,
  Decompilation, Fingerprints, Referenzen, Quellenwahl, Ressourcenregister
  und Cache-Verträge.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` für Provider,
  Konfiguration der Repository-Akquisition, Git-Prozessausführung,
  Checkout-Sicherheit, Cache/Refresh und Snapshot-Materialisierung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`,
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/` und
  `src/AiNetLinter/Mcp/Registration/` für Toolverhalten, Navigation,
  Registration und Wire-Text.
- `src/AiNetLinter/Configuration/` für External-Source-Konfiguration,
  Mapping- und Pfadvalidierung.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/` und
  `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/` für schnelle
  Verträge und Zustandsmodelle.
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` sowie die allgemeinen
  MCP-Integrationstests für Prozess-, Wire- und Live-Verhalten.
- `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md`,
  `README.md`, `rules.json` und `.agents/rules/` für veröffentlichte und
  agentenbezogene Verträge.

## 4. Review-Organisation

Es werden ausschließlich unabhängige Kritiker/Reviewer eingesetzt. Kein
Reviewer darf einen Fix implementieren, fremde Reports ändern oder eine
Assertion zur Herstellung eines grünen Laufs abschwächen.

Die erste Welle besteht verpflichtend aus der maximal praktisch verfügbaren
Anzahl fachlich getrennter Reviewer. Die acht fachlichen Linsen bilden dabei
die Mindestabdeckung:

1. Assembly-Zielrouting, Decompilation, Metadata-only-Grenze und Referenz-
   bzw. Symbolnavigation.
2. External-Source-Konfiguration, Mapping-Auflösung, Provider- und
   Credential-Semantik.
3. Git-Transport, Prozesssicherheit, Timeout/Cancel, Prozessbaum und
   Diagnose-Redaktion.
4. Repository-Checkout, Attestation, Reparse-Points, Pfadschutz und
   Cleanup-/Ownership-Verträge.
5. Cache, Snapshot, Refresh, Generationen, Kapazitätsbudgets, TTL und
   Nebenläufigkeit.
6. MCP-Komposition, Tool-Schemas, Fehler-/Wire-Verträge und Sessionstatus.
7. Agentenfreundlichkeit, Token-Effizienz, Trunkierung, Defaults und
   Antwortkonsistenz.
8. Testmatrix, Dokumentationssynchronität, Harmonie, Abdeckungslücken und
   adversariale Ende-zu-Ende-Gegenprüfung.

Die Reviewer-Welle wird gleichzeitig gestartet. Jeder Reviewer arbeitet in
einer frischen, isolierten Arbeitsumgebung, legt genau eine eigene Datei unter
`tasks/decompiled-assembly-analysis-audit/reports/` an und committed diese
unmittelbar nach Fertigstellung. Dateinamen und Commit-Namen werden neutral
gehalten. Der abschließende Tech-Debt-Report ist naturgemäß erst nach Eingang
aller Einzelreports möglich und wird von einem zusätzlichen Reviewer ohne
Fix-Scope erstellt.

Die Parallelisierung darf keine gemeinsame Datei voraussetzen. Jeder
Reviewer besitzt eine klar abgegrenzte Primärlinse, darf aber bei sichtbarer
Querabhängigkeit einen Befund als Cross-Cutting markieren und auf die
betroffenen Komponenten verweisen.

## 5. MCP-first- und Gegenprüfungsregeln

Jeder Reviewer beachtet `.agents/rules/` vollständig, insbesondere die
MCP-Workflow-Regeln. Für C#-Semantik gilt:

- zuerst `get_file_tree` als Summary bzw. mit begrenzter Tiefe und danach
  `get_index_scope` zur Indexabdeckung;
- unbekannte Einstiegspunkte mit `find_symbol`, bekannte Kernsymbole mit
  `get_feature_context`, bei Bedarf gezielt mit `get_symbol_body`;
- Aufrufer, Abhängigkeiten, Tests und Impact mit
  `find_references`, `get_impact`, `dependency_graph` und
  `get_test_context` prüfen;
- Assembly-Verhalten mit `inspect_assembly` und, falls relevant,
  `find_assembly_extensions` metadata-only gegen neutrale lokale DLL-
  Varianten prüfen;
- vor dem Abschluss passend zum Risiko `get_violations`, `safeguard`,
  `find_duplicates`, `find_dead_code` und `find_magic_values` einsetzen;
- bei jedem zielgebundenen Call `targetType` und den absoluten
  `targetPath` setzen und `completeness`, Herkunft, Trunkierung und
  partielle Diagnosen in die Beweisbewertung einbeziehen.

`rg` und PowerShell dienen ergänzend für exakte Text-, JSON-, Markdown-,
Projekt- und Diff-Prüfungen. Sie ersetzen keine semantische MCP-Abfrage.
Nach einer vollständigen MCP-Antwort wird dieselbe semantische Frage nicht
redundant erneut aus dem Quelltext rekonstruiert; bei einer Lücke wird der
MCP-Scope gezielt verfeinert.

Für Live-Proben werden neutrale Rollen verwendet, zum Beispiel eine lokal
vorhandene DLL mit Source-Mapping und eine zweite DLL ohne Mapping. Die
konkreten Hersteller-, Produkt- und Installationsdaten bleiben aus allen
Reports und Dokumenten heraus. Fehlen lokale DLLs, ein erreichbarer Git-
Dienst oder eine gültige, credential-freie Konfiguration, wird das als
Abdeckungsgrenze mit exakter Ursache notiert, nicht als Funktionsfehler.

Für ein konfiguriertes External-Source-Mapping ist außerdem eine echte
MCP-Live-Probe verbindlich: Eine gemappte lokale DLL wird über mindestens
`inspect_assembly` und eine zweite Assembly-Funktion angefragt. Vor und nach
dem Aufruf werden der konfigurierte Cache, der erzeugte Repository-Checkout,
die konfigurierte Solution und mindestens eine Source-Datei geprüft. Der
Nachweis gilt nur dann als erfolgreich source-backed, wenn die MCP-Antworten
`origin=source-backed`, einen nichtleeren `sourcePath`, einen nichtleeren
Snapshot sowie den passenden Vertrauens-/Vollständigkeitsstatus ausweisen.
Ein heruntergeladener Checkout ohne diese Antwortfelder ist ausdrücklich ein
fehlgeschlagener Source-backed-Nachweis mit sicherem Decompilation-Fallback;
er darf nicht als bestandener Test verbucht werden. Provider-,
Materialisierungs- und Fallback-Diagnosen werden dabei getrennt erfasst.

## 6. Nachweis- und Reportvertrag

Jeder Einzelreport enthält:

- geprüfte Linse, Scope, verwendete Revision und nicht geprüfte Bereiche;
- eine kurze Executive Summary mit getrennten Aussagen zu Befunden,
  bestätigten Erwartungen und Abdeckungsgrenzen;
- pro Befund eine stabile neutrale ID, Titel, betroffene Komponente,
  erwartetes Verhalten, beobachtetes Verhalten, Auswirkung und konkrete
  Reproduktion;
- Belege mit Datei und Zeile oder MCP-Symbol, Toolparametern ohne Geheimnisse,
  relevanten strukturierten Feldern, Testnamen bzw. reproduzierbaren
  PowerShell-Kommandos sowie einer knappen Begründung, warum der Beleg die
  Aussage trägt;
- Schweregrad, Umfang, Beweissicherheit und gegebenenfalls
  Umgebungsabhängigkeit;
- eine nicht umgesetzte Remediation-Hypothese, nur wenn sie zur Einordnung
  des Tech Debts hilft;
- mögliche Überschneidungen zu anderen Linsen und eine abschließende
  Coverage-/Limitations-Tabelle.

### Klassifikation

Schweregrad:

- `S0`: Analyseweg unbenutzbar, Sicherheits-/Datenintegritätsrisiko oder
  reproduzierbarer Verlust eines gültigen Snapshots/Checkouts.
- `S1`: wesentliche End-to-End-Funktion oder Agentenvertrag bricht unter
  realistischen Bedingungen.
- `S2`: fachlich relevantes Fehlverhalten, unvollständige Semantik,
  irreführender Fehler-/Vollständigkeitsvertrag oder erheblicher
  Token-/Ressourcenverbrauch.
- `S3`: begrenzte Inkonsistenz, Dokumentationsabweichung, geringe Robustheits-
  oder Wartbarkeitslücke ohne unmittelbaren Funktionsverlust.

Umfang:

- `U1`: isolierter Pfad, Typ oder einzelner Vertrag.
- `U2`: mehrere eng gekoppelte Komponenten oder ein ganzer Lebenszyklus.
- `U3`: mehrere Tools, Sessions oder externe Ressourcen.
- `U4`: systemischer Agenten-, Dokumentations- oder Betriebsvertrag.

Beweissicherheit:

- `hoch`: direkt reproduziert oder durch MCP, Code und Test-/Wire-Beleg
  gemeinsam bestätigt.
- `mittel`: Codepfad und mindestens ein Gegenbeleg, aber eine externe
  Voraussetzung fehlt.
- `niedrig`: plausible Hypothese; darf nicht als bestätigter Befund in die
  Priorisierung eingehen.

### Tech-Debt-Report

Der abschließende Reviewer konsolidiert nur committed Einzelreports,
entfernt Duplikate ohne Informationsverlust, bewahrt die stärkste Evidenz,
markiert Widersprüche und gruppiert nach Schweregrad, Umfang, Lebenszyklus
und Agentenwirkung. Der Report enthält eine Coverage-Matrix, eine Liste
offener Reproduktionsbedingungen und eine priorisierte Arbeitsliste ohne
Implementierung. Er darf keine nicht belegte Funktionsbehauptung als
Tatsache darstellen.

## 7. Verifikation

Die Verifikation erfolgt in drei Ebenen:

1. Jeder Reviewer führt gezielte, read-only Prüfungen und passende bestehende
   Tests aus. Testausgaben werden bei Bedarf über TRX-Dateien aus
   `TestResults/` diagnostiziert; keine Ad-hoc-Testskripte oder Änderungen an
   der Produktkonfiguration.
2. Der Abschluss-Reviewer prüft die Report-Vollständigkeit, dedupliziert die
   Befunde und verifiziert zentrale Claims anhand des aktuellen Codes sowie
   der MCP-Antworten.
3. Vor Abschluss des Gesamtvorhabens werden `dotnet build` sowie die beiden
   in `AGENTS.md` vorgeschriebenen vollständigen Testläufe mit
   `Category!=Stress` ausgeführt. Ein roter Baseline-Lauf wird als
   reproduzierbarer Audit-Befund bzw. als Umgebungsgrenze dokumentiert; er
   wird nicht durch Codeänderungen behoben.

## 8. Risiken, Betriebs- und Bedrohungsmodell

- Externe Quellen sind untrusted input. Reports prüfen URL-, Pfad-,
  Reparse-Point-, Checkout- und Diagnosegrenzen sowie mögliche
  Credential-Leaks.
- Git ist ein externer Prozess. Prozessbaum, geerbte Umgebung, Prompts,
  Handles, Exit-/Timeout-/Cancel-Rennen und Cleanup werden als
  Sicherheits- und Zuverlässigkeitsgrenzen behandelt.
- Cache und Snapshots können über mehrere MCP-Aufrufe und Generationen
  hinweg leben. Ownership, Lease-Überlappung und Fehler nach Refresh sind
  explizit nachzuweisen.
- Toolantworten sind Agenteninput. Trunkierung, partielle Ergebnisse,
  Herkunft und negative Aussagen müssen so gekennzeichnet sein, dass kein
  Agent aus einer begrenzten Antwort eine globale Negativaussage ableitet.
- Zugangsdaten, Tokens und unredigierte externe Diagnosen werden niemals in
  interne Notizen, Reports, Commits oder Konsolidierungen geschrieben.
- Parallel arbeitende Reviewer dürfen keine fremden Arbeitsstände oder
  Dateien überschreiben. Der einzige geplante Schreibvorgang ist der eigene
  Report-Commit; die Produktbasis bleibt unverändert.

## 9. Muss-Kriterien

- Der Audit umfasst Assembly-Analyse, statische Decompilation, Source-
  Mapping, Git-Akquisition, Cache/Snapshot-Lebenszeit, MCP-Agentenvertrag,
  Token-Effizienz und Dokumentation.
- Es werden ausschließlich Kritiker/Reviewer eingesetzt; es gibt keine
  Fixes, keine Codeänderungen und keine Assertion-Abschwächung.
- Die erste Reviewer-Welle läuft gleichzeitig mit maximal praktikabler
  Parallelität in unabhängigen Umgebungen; die parallele Ausführung ist
  Pflicht und darf nicht in eine sequenzielle Reviewer-Welle umgewandelt
  werden.
- Jeder Reviewer committed genau seinen eigenen nachvollziehbaren Report
  sofort nach Fertigstellung.
- Alle Befunde sind nach Schweregrad und Umfang klassifiziert sowie mit
  reproduzierbaren, redigierten Belegen versehen.
- Der abschließende Tech-Debt-Report trennt bestätigte Befunde,
  Abdeckungsgrenzen, Widersprüche und spätere Arbeitsaufträge.
- Kein Report, kein Dokument und kein Commit-Text enthält die im Auftrag
  gesperrten Hersteller-/Produktbegriffe oder konkrete geschützte
  Beispieldaten; Beispiele werden abstrakt beschrieben.
- Die verpflichtenden Build-/Testnachweise und ihre Ergebnisse sind im
  Abschlussreport sichtbar.

## 10. Überprüfbare Akzeptanzkriterien

- Für jede der acht Review-Linsen existiert entweder ein eigener committed
  Report oder eine begründete, belegte Abdeckungsgrenze.
- Jeder gemeldete Befund enthält alle Felder des Reportvertrags; fehlende
  Reproduktion oder fehlende Evidenz verhindert seine Aufnahme als
  bestätigter Tech-Debt-Eintrag.
- Mindestens ein Reviewer prüft einen real gemappten Source-backed-Fall und
  einen reinen Decompilation-Fall mit `inspect_assembly`; bei einem
  konfigurierten Mapping werden zusätzlich Cache-Checkout, Solution und
  Source-Datei gegen die MCP-Herkunftsfelder abgeglichen. Die Antworten
  enthalten eine auswertbare Herkunfts- und Vollständigkeitsbewertung. Falls
  die Source-Materialisierung scheitert, wird der Download als Teilerfolg und
  die weiterhin gelieferte Decompilation als fehlgeschlagene Source-backed-
  Bereitstellung dokumentiert.
- Mindestens ein Reviewer prüft Git-Akquisition einschließlich Erfolg,
  Fehler, Cancel/Timeout und Cleanup anhand vorhandener Tests oder einer
  sicheren reproduzierbaren Probe.
- Die Konsolidierung weist jeden Einzelbefund genau einem Primärbereich zu,
  behält Querverweise und macht Duplikate/Widersprüche sichtbar.
- Ein Dokumentationsvergleich nennt jede Abweichung zwischen Code,
  Registration, Tests und veröffentlichter Beschreibung mit konkretem
  Beleg; reine Stilpräferenzen werden nicht als Fehler priorisiert.
- Die finale Verifikation ist entweder grün oder als reproduzierbarer
  Baseline-/Umgebungsbefund mit TRX- bzw. Konsolennachweis dokumentiert.

## 11. Bewusste Annahmen

### Annahmen

- Die aktuelle Arbeitsbasis ist die maßgebliche Wahrheit.
- Ein fehlender externer Dienst oder fehlende lokale DLLs blockieren den
  Gesamt-Audit nicht; sie begrenzen nur die entsprechende Live-Abdeckung.
- Reviewer dürfen vorhandene Tests, MCP-Abfragen und read-only lokale
  Hilfsprüfungen ausführen, aber keine neue Test- oder Produktionsdatei
  anlegen.
- Die Einzelreports und der Tech-Debt-Report liegen im angegebenen Taskordner;
  außerhalb davon werden keine Dateien verändert.
