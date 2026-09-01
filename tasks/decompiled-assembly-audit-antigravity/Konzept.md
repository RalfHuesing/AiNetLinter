---
status: ready
---

# 360-Grad-Audit der dekompilierten Assembly-Unterstützung im AiNetLinter-MCP

## Verbindliche Copyright- und Redaktionsregel

Alle versionierten oder als Ergebnis weitergegebenen Dokumente, einschließlich
dieses Konzepts und aller späteren Finding-Berichte, dürfen keine Namen,
Namespaces, Typen, Member, Repository-Bezeichnungen, URLs, Solution-Namen oder
sonstige charakteristische Begrifflichkeiten der untersuchten externen
Assemblies enthalten. Gleiches gilt für Commit-Nachrichten, Commit-Vorschläge,
Überschriften, Dateinamen und Code-Kommentare.

Auf ausdrückliche Nutzeranweisung gibt es eine einzige lokale Ausnahme: Die
gitignorierte Arbeitsdatei
`../../temp/decompiled-assembly-audit-examples.md` darf die konkreten Namen und
Pfade der Prüffälle enthalten. Sie ist ausschließlich eine lokale
Ausführungsreferenz, kein Ergebnisdokument und kein Commit-Inhalt. Ihr Inhalt
wird nicht in das Konzept, Finding-Berichte, Logs oder Commit-Texte kopiert;
vor einem Commit ist zusätzlich zu prüfen, dass die Datei weiterhin von
`.gitignore` erfasst wird.

Die fünf externen Prüffälle werden deshalb ausschließlich mit opaken Labels
bezeichnet: `GIT-01` für den per externer Konfiguration zugeordneten Git-Fall
`LOCAL-01`/`LOCAL-02` für die beiden DLL-Vergleichsfälle, `LOCAL-03` für den
managed-EXE-Fall und `FALSE-01` für den bewusst nicht-.NET-Fall. Rohes
MCP-Output, dekompilierter Code, externe Pfade,
Repository-URLs und externe Symbolnamen werden nicht in Dokumente kopiert.
Berichte dürfen nur allgemeingültige oder exemplarische Aussagen und für den
Nachweis erforderliche, redigierte Metadaten wie `origin`, `trust`,
`completeness`, `fallbackReason`, Hash-Präsenz oder das Vorhandensein eines
Source-Snapshots enthalten.

## Ziel und Nutzen

Dieses Vorhaben prüft die im AiNetLinter-MCP implementierte Unterstützung für
lokale `.dll`- und `.exe`-Dateien mit besonderem Fokus auf den dekompilierten
Fallback. Ziel ist eine belastbare, code- und MCP-verifizierte Aussage darüber,
ob die Assembly-Analyse fachlich korrekt, sicher, vollständig genug,
token-effizient und im laufenden MCP-Betrieb effizient nutzbar ist.

Das Audit ist ein reiner Analyse- und Befundtask. Es werden kein
Produktionscode, keine Tests und keine Konfiguration geändert; `dotnet build`
und Testausführung gehören nicht zum Task. Die Findings werden gegen den
aktuellen Quellcode, die registrierten MCP-Verträge und die vorhandenen
Nachweise geprüft. Es findet in diesem Task keine spätere Konsolidierung der
einzelnen Finding-Dateien statt.

Das gewünschte Ergebnis ist eine priorisierte Liste konkreter
Verbesserungsmöglichkeiten am AiNetLinter. Die Befundberichte müssen deshalb
zuerst klare, code- und MCP-verifizierte Bugs ausweisen, danach mess- oder
strukturell begründete Optimierungen und anschließend nachgewiesene Missing
Features. Eine reine Beschreibung des Ist-Zustands genügt nicht: Jeder
relevante Befund endet mit einer konkreten Verbesserungsempfehlung oder einer
begründeten Einstufung als akzeptierte Einschränkung. Nicht reproduzierbare
Vermutungen werden sichtbar als Unsicherheit gekennzeichnet und nicht als Bug
ausgegeben.

## Gewünschte Befundpriorisierung

Die Ergebnisreihenfolge ist fachlich verbindlich:

1. **Bug** — eine aktuelle Implementierung verletzt einen Vertrag, ein
   Sicherheitsinvariant, die dokumentierte Semantik oder eine reproduzierbare
   Erwartung.
2. **Optimierung** — das Verhalten ist grundsätzlich korrekt, verursacht aber
   vermeidbare Kosten bei Laufzeit, Speicher, Tokenbudget, Robustheit oder
   Agentennutzbarkeit; der Verbesserungsvorschlag nennt die zugrunde liegende
   Evidenz.
3. **Missing Feature** — eine für den Assembly-Analyse-Workflow erforderliche
   Fähigkeit fehlt; der Bericht beschreibt die konkrete Nutzungslücke und
   grenzt sie von einem bloßen Wunsch ab.

Innerhalb jeder Kategorie wird nach P0–P3 und anschließend nach
Vertrauensgrad sortiert. Jeder Bericht enthält auch dann eine explizite
Aussage, wenn in einer Kategorie kein belastbarer Befund gefunden wurde.

## Verbindlicher Scope

Der Scope umfasst die beiden Assembly-Only-Tools und alle gemeinsam genutzten
Pfade, die für dekompilierte Assembly-Abfragen relevant sind:

- `inspect_assembly` und `find_assembly_extensions` einschließlich Registrierung,
  Tool-Schema, Annotationen, Eingabevalidierung, Filterung und Payload-Projektion;
- Assembly-Target-Dispatch und die Abgrenzung zu projektgebundenen MCP-Tools;
- metadata-only Decompilation, Roslyn-Workspace-/Snapshot-Erzeugung,
  Body-/Syntax-Aufbereitung sowie source-backed versus dekompilierter Ursprung;
- Referenzauflösung, optionale bounded Referenz-Sessions, fehlende oder
  inkompatible Abhängigkeiten und die Semantik von `partial`, Diagnosen,
  Herkunft, Vertrauen und Vollständigkeit;
- Registry, Generationen, Fingerprints, Cache, Leases, Refresh, LRU/TTL,
  Eviction, Disposal, Cancellation, Parallelität und Ressourcenbudgets;
- Symbolnavigation auf dekompilierten Snapshots (`find_symbol`,
  `get_symbol_body`, `find_references`, `get_call_tree`,
  `get_type_hierarchy`, `dependency_graph`, `get_namespace_tree`,
  `get_file_skeleton`, `get_class_structure`, `metrics_lookup` und
  `metrics_tree`), soweit sie Assembly-Targets unterstützen;
- Response-Budgets, Trunkierung, Diagnose-Samples, Text-vs-
  `structuredContent`-Konsistenz und Progressive Disclosure;
- MCP-Server-Lebenszyklus, Health-/Session-Sichtbarkeit, Fehler- und
  Fallback-Verhalten, Pfad-/Dateisystem-Sicherheitsgrenzen sowie das
  Verhalten bei `.exe`, nativen PE-Dateien und beschädigten oder wechselnden
  Dateien;
- bestehende Dokumentation und vorhandene Fast-/Integration-Testverträge als
  Nachweise, ohne Tests auszuführen.

Der Begriff „360 Grad“ bedeutet in diesem Konzept eine Prüfung dieser
fachlichen, technischen, betrieblichen, Sicherheits-, Vertrags- und
Effizienzdimensionen. Er bedeutet nicht, dass beliebige unabhängige Features
des gesamten Linters ohne Bezug zur Assembly-Unterstützung untersucht werden.

## Explizite Non-Goals und Scope-Grenzen

- Keine Implementierung, kein Refactoring, kein Build und keine Testausführung.
- Keine automatische Behebung von Findings und keine Änderung an
  `Docs/`, `rules.json`, Agentenregeln oder Produktionscode.
- Keine Laufzeit-Ausführung, kein Laden der untersuchten Ziel-Assembly und
  keine Ausführung ihrer Methoden als Teil des Audits. Ein Source-Checkout
  darf ausschließlich über den bestehenden, konfigurierten External-Source-
  Provider erfolgen, niemals als manueller Clone; die Konzeptphase löst
  keinen Checkout aus.
- Keine Behauptung vollständiger semantischer Gleichheit zwischen
  Dekompilation und Originalquellcode; Grenzen und Unsicherheiten müssen als
  Befund-/Vertrauensinformation sichtbar bleiben.
- Keine nachgelagerte Zusammenführung, Priorisierung über die Einzelberichte
  hinaus oder gemeinsame Abschlussbewertung innerhalb dieses Tasks.
- Keine Prüfung von Regeln/Audits, die für Assembly-Targets ausdrücklich
  unsupported sind, außer wenn ihre Abgrenzung zum Assembly-Vertrag relevant
  ist.
- Keine Veröffentlichung oder dauerhafte Speicherung von aus externen
  Assemblies abgeleiteten Codeinhalten oder identifizierenden Symbolnamen.

## Aktueller Projektkontext und belastbare Ausgangslage

Die aktuelle MCP-Session war zum Analysezeitpunkt geladen (MCP-Version
`1.0.158`, Projektroot `C:\Daten\Entwicklung\Ralf\AiNetLinter`); es waren zu
diesem Zeitpunkt keine Assembly-Sessions resident. Das ist eine Momentaufnahme
für die Audit-Ausgangslage, kein dauerhaftes Sollverhalten.

Die Dokumentation beschreibt `targetType="assembly"` mit absolutem lokalem
DLL-/EXE-Pfad. Assembly-fähige Symbol- und Strukturabfragen verwenden eine
gemeinsame read-only Assembly-Session. Eine explizite Source-Zuordnung kann
source-backed verwendet werden; andernfalls ist eine dekompilierte Session
vorgesehen. Herkunft, Snapshot/Generation, Status, Vollständigkeit und
Diagnosen sollen dabei sichtbar sein. `includeReferences=false` soll bei
Assembly-Targets root-only bleiben; Referenzexpansion ist opt-in und bounded.

Die physische Struktur weist aktuell unter `src/AiNetLinter/Mcp/Assemblies`
97 C#-Dateien aus, davon 44 im Bereich `Analysis` und 52 im Bereich
`ExternalSource`; `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis` enthält 18
Dateien. Relevante zentrale Typen sind unter anderem:

- `AssemblyAnalysisRegistry` für Leasing, Generationen, Fingerprints,
  Resident Count, Eviction und Disposal;
- `AssemblyAnalysisSession` für Refresh, Cache, Generationen, Snapshots,
  Compilation-Validierung und Snapshot-Leases;
- `AssemblyDecompilationAdapter`, `AssemblyDecompilationCache` und
  `AssemblyReferenceResolver` für die dekompilierte Analyse;
- `AssemblyAnalysisContextFactory` für source-backed Kontext, Consumer-/Source-
  Auswahl und Fallback-Diagnosen;
- `AssemblyAnalysisResponseLimits` für Diagnose-, Referenz- und globales
  Response-Budget;
- `AssemblyAnalysisToolRegistrations` sowie die Dispatch-, Service- und
  Response-Builder-Typen für die beiden öffentlichen Tools.

Vorhandene Nachweise sind insbesondere Assembly-FastTests für Filter,
Response-Budget, managed `.exe`, native PE, fehlende Abhängigkeiten und
Referenzauflösung sowie Integrationstests für Health, Registrierung und
Structured-Content-Verträge. Diese Nachweise werden gelesen und bewertet,
aber in diesem Audit nicht erneut ausgeführt.

## Externe Prüffälle und Origin-Nachweis

Zusätzlich zur In-Repo-Evidenz werden fünf vom Nutzer bereitgestellte externe
Prüffälle in die Audit-Verifikation aufgenommen:

Die konkreten lokalen Referenzen für diese fünf Labels stehen ausschließlich in
der [gitignorierten lokalen Prüffall-Matrix](../../temp/decompiled-assembly-audit-examples.md).
Dieses Konzept verwendet nur die Labels und allgemeine Fallbeschreibungen.

- `GIT-01`: Ein Assembly-Eintrag aus der bereitgestellten externen
  `ExternalSources`-Konfiguration. Der Fall muss über die installierte
  Runtime-Konfiguration und den bestehenden Git-/Source-Provider verfolgt
  werden. Nachzuweisen sind die tatsächliche Materialisierung, ein sauberer
  Checkout, die geladene Revision, der Source-Snapshot und die anschließende
  MCP-Herkunft.
- `LOCAL-01` und `LOCAL-02`: Zwei lokal vorhandene externe DLL-Dateien als
  Vergleichsfälle. Für beide ist zu prüfen, ob die MCP-Antwort den
  dekompilierten Ursprung, keinen Source-Snapshot und den passenden
  Fallback-Grund ausweist.
- `LOCAL-03`: Eine lokal vorhandene managed `.exe` als zusätzlicher
  Decompilation-Fall. Zu prüfen sind dieselben Origin-/Fallback-Felder wie bei
  den DLL-Fällen sowie die Behandlung großer Diagnose- und API-Mengen unter
  dem Response-Budget.
- `FALSE-01`: Eine lokal vorhandene nicht-.NET-EXE als Negativ- bzw.
  False-Test. Erwartet wird eine sichere, strukturierte und recoverable
  Metadata-Diagnose ohne Prozessstart, ohne Assembly-Ausführung und ohne
  unkontrollierten Serverfehler.

Für `GIT-01` gilt der Nachweis nur dann als „Source aus Git“, wenn mehrere
unabhängige Signale zusammenpassen: externe Mapping-Auflösung, erfolgreicher
Provider-/Checkout-Status, vertrauenswürdiger Snapshot mit geladener Revision,
`origin=source-backed` sowie ein nicht-dekompilierter Content-Modus. Ein bloß
vorhandenes Mapping, ein vorhandener lokaler Dateipfad oder eine erfolgreiche
API-Auflistung genügt nicht.

Für `LOCAL-01`/`LOCAL-02` gilt der Nachweis als „dekompiliert“, wenn die
MCP-Antwort mindestens `origin=decompiled`, fehlenden Source-Pfad bzw.
fehlenden Snapshot und den sichtbaren Fallback-Grund ausweist. Die aktuell
ausgeführten read-only Assembly-Abfragen lieferten für beide lokalen
Vergleichsfälle bereits diesen dekompilierten Origin mit `sourcePath=none`,
`fallbackReason=mapping-not-found`, `confidence=medium`, `trust=untrusted` und
`completeness=partial`; diese Momentaufnahme wird im eigentlichen Audit ohne
externe Identitäten protokolliert.

Die aktuelle read-only-Abfrage für `LOCAL-03` bestätigte ebenfalls
`origin=decompiled`, `sourcePath=none`, `fallbackReason=mapping-not-found`,
`confidence=medium`, `trust=untrusted` und `completeness=partial`; zusätzlich
trat eine hohe Zahl synthetischer Compiler-/Referenzdiagnosen auf, die durch
die vorhandenen Ausgabegrenzen gekürzt wurde. Die Abfrage für `FALSE-01`
lieferte `isError=false`, `recoverable=true` und ein strukturiertes Payload mit
Fehlercode, Kontext und Hinweis. Ob diese beobachtete Form dem gewünschten
öffentlichen MCP-Vertrag entspricht, bleibt ein expliziter Auditpunkt.

Die externe Runtime-Konfiguration verweist auf eine Mapping-Datei und einen
separaten Source-Cache. Bei der read-only Prüfung war dieser erwartete
Source-Cache nicht vorhanden. Das beweist nicht, dass nie ein Checkout
stattgefunden hat; es begründet aber die Pflicht, den tatsächlichen Runtime-,
Cache- und Sessionpfad beim Git-Fall explizit nachzuweisen. Die aktuelle
Repository-MCP-Session verwendete dagegen die projektlokale Konfiguration und
ist für die beiden lokalen Vergleichsfälle nicht als Source-Mapping-Nachweis
geeignet.

## Audit-Epics und Ergebnisartefakte

Die Audit-Arbeit wird in fachlich abgegrenzte, möglichst unabhängige Epics
geteilt. Jedes Epic wird als eigener Befundbericht bearbeitet; pro Epic entsteht
eine separate Markdown-Datei im Task-Verzeichnis. Die gewünschte Ausführung ist
begrenzt parallel: Es dürfen höchstens vier Sub-Agenten gleichzeitig laufen.
Wenn mehr als vier Epics vorhanden sind, werden mehrere Wellen gebildet. Jede
Welle startet ausschließlich neue, unabhängige Sub-Agenten. Abgeschlossene oder
beendete Agenten werden entfernt beziehungsweise archiviert und weder
fortgesetzt noch wiederbelebt oder für eine spätere Welle wiederverwendet. Wenn
die technische Orchestrierung keine parallele Welle zulässt, werden die Epics
seriell in denselben Fresh-Agent-Wellen bearbeitet; die Obergrenze von vier
aktiven Sub-Agenten bleibt unverändert.

Vorgesehene Epic-Grenzen:

1. **Öffentliche MCP-Verträge und Discoverability** — Registrierung, Schemas,
   Annotations, Parameterdefaults, Capability-Matrix, Progressive Disclosure
   und Dokumentationskonsistenz.
2. **Decompilation und semantischer Snapshot** — metadata-only-Garantie,
   dekompilierte Dokumente, Syntax/Bodies, Generics/Attribute/Parameter,
   stabile IDs und Unterschiede zu source-backed Ergebnissen.
3. **Referenzen, Source Selection und Diagnosen** — Auflösung, externe Quellen,
   Consumer-Kontext, fehlende/inkompatible Referenzen, Herkunft, Trust,
   `partial` und Diagnoseprojektion.
4. **Session-, Cache- und Lebenszeitsemantik** — Fingerprints, Generationen,
   Cache-Reuse, Refresh bei Dateiänderung, Leases, Cancellation, Eviction,
   TTL, Disposal, Parallelität und Registry-Isolation.
5. **Navigation und fachliche Query-Korrektheit** — Assembly-Support der
   Symbolgraph- und Strukturtools, Root-vs-Referenz-Grenzen, Caller-/Calltree-
   Semantik, Extension-Anwendbarkeit sowie Trunkierungsgrenzen.
6. **Response-, Token- und Laufzeiteffizienz** — globale Budgets, Reihenfolge
   der Reduktion, Text-/JSON-Konsistenz, Diagnose-Sample-Auswahl,
   Referenzlimits, Worst-Case-Payloads und unnötige Arbeit.
7. **Betrieb, Sicherheit und Fehlerverhalten** — absolute Pfade, Dateitypen,
   native/beschädigte/wechselnde PE-Dateien, Nichtausführung, redigierte
   Fehlermeldungen, Health-/Observability-Sicht und Fail-Closed-Verhalten.
8. **Test- und Dokumentationsnachweis** — Abdeckung der kritischen Verträge,
   Lücken, irreführende Erwartungen und notwendige spätere Verifikation;
   ohne Testausführung und ohne Änderung der Dokumente.

Die Epic-Grenzen dürfen bei nachgewiesener Überschneidung präzisiert werden,
aber kein Epic darf seine Findings stillschweigend in einen anderen Bericht
verschieben. Cross-Epic-Bezüge werden als Verweis auf den jeweils anderen
Befundbericht notiert; eine Konsolidierungsdatei ist nicht Teil dieses Scopes.

## Befundformat und Bewertung

Jeder Bericht enthält ausschließlich nachvollziehbare Befunde und die zu ihrer
Bewertung erforderliche Evidenz. Ein Befund muss mindestens enthalten:

- eindeutige lokale Finding-ID innerhalb des Epic-Berichts;
- Kategorie `Bug`, `Optimierung` oder `Missing Feature`;
- Priorität, empfohlene Größenklasse und Vertrauensgrad;
- betroffene MCP-Tools, Typen/Symbole, Dateien und möglichst Zeilen;
- beobachtetes Verhalten beziehungsweise Soll-Ist-Abweichung;
- Code-/Dokumentations-/MCP-Evidenz mit verwendeten Parametern und sichtbarer
  Vollständigkeit/Trunkierung;
- Auswirkung für Korrektheit, Sicherheit, Tokenbudget, Laufzeit oder Agenten-
  Nutzbarkeit;
- konkrete Empfehlung zur Behebung oder weiteren Umsetzung;
- Abgrenzung, ob es sich um einen Fehler, eine Lücke, ein Risiko, eine
  Dokumentationsabweichung oder eine bewusst akzeptierte Einschränkung handelt.

Die Reports ordnen ihre Findings zuerst nach Kategorie in der Reihenfolge
`Bug`, `Optimierung`, `Missing Feature`; innerhalb der Kategorie gelten
Priorität und Vertrauensgrad. Bei einem Bug muss die aktuelle Abweichung
reproduzierbar oder unmittelbar aus der Implementierung und ihrem Vertrag
ableitbar sein. Eine Optimierung muss eine vermeidbare Belastung oder
Robustheitskosten belegen. Ein Missing Feature muss eine konkrete, im Scope
liegende Nutzungslücke beschreiben.

Empfohlene einheitliche Klassifizierung, sofern keine Nutzerentscheidung sie
ändert: P0 = Sicherheits-/Datenverlust- oder harter Vertragsbruch, P1 = hohe
Korrektheits-/Betriebsrelevanz, P2 = relevante Effizienz-/Robustheits-/UX-Lücke,
P3 = kleinere Verbesserung oder Dokumentationspräzisierung. Größenklassen:
S = lokal und klar begrenzt, M = mehrere eng gekoppelte Stellen, L = mehrere
Komponenten oder Verträge, XL = Architektur-/Migrationsumfang. Priorität und
Größe sind getrennt zu bewerten; ein großer Befund ist nicht automatisch
wichtiger.

## Betriebs- und Bedrohungsmodell

Das MCP-Tool erhält einen absoluten lokalen Pfad zu einer Assembly. Die Datei
kann von einem Agenten bereitgestellt, unvollständig, beschädigt, nativen
Formats oder während einer Session verändert sein. Abhängigkeiten können
fehlen, falsche Versionen haben oder nur teilweise auflösbar sein. Ein
Consumer-Projekt kann vorhanden oder nicht entscheidbar sein.

Als Sicherheitsinvariante gilt: Analyse ist metadata-only und read-only; die
Zielassembly und ihre Methoden werden nicht geladen oder ausgeführt. Pfade,
Fehlertexte, Logs und Diagnosen dürfen keine Credentials oder unredigierten
Geheimnisse aus externen Quellen offenlegen. Bei Unsicherheit muss das System
die Unsicherheit und den eingeschränkten Scope sichtbar machen, statt aus
einem dekompilierten Root-Snapshot eine globale Negativaussage abzuleiten.

Akzeptierte, aber zu prüfende Annahmen sind: Dekompilation kann ohne
Originalquellcode nur eine approximierte Semantik liefern; externe Referenzen
bleiben bounded; MCP-Antworten sind bewusst begrenzt; und source-backed und
dekompilierte Sessions können unterschiedliche Detail- und Diagnosequalität
haben. Das Audit bewertet, ob diese Annahmen korrekt surfacet werden.

## Muss-Kriterien

- Alle acht Audit-Epics liefern jeweils einen separaten Markdown-Befundbericht
  mit code- und MCP-verifizierten Findings.
- Die beiden Assembly-Tools und alle im Scope genannten Assembly-fähigen
  Folgeabfragen werden gegen die aktuelle Implementierung und ihre Verträge
  geprüft; bloße Behauptungen aus älteren Dokumenten reichen nicht.
- Der Auditbericht jedes Epics trennt belegtes Verhalten, Unsicherheit,
  akzeptierte Einschränkung und Empfehlung.
- Jeder Bericht liefert konkrete Verbesserungsbefunde in den Kategorien Bug,
  Optimierung und Missing Feature — oder eine begründete Aussage, dass in einer
  Kategorie kein belastbarer Befund gefunden wurde.
- Findings sind zuerst nach Kategorie, dann nach Priorität geordnet und
  zusätzlich nach Größe klassifiziert.
- Decompilation, Referenzauflösung, Fallback-/Diagnosepfade, Lebenszeit,
  Sicherheit, Response-Budget und Agenten-Nutzbarkeit sind ausdrücklich
  abgedeckt.
- `GIT-01` weist eindeutig nach, ob die analysierten Dokumente aus dem
  konfigurierten Git-Checkout oder aus Decompilation stammen; `LOCAL-01`,
  `LOCAL-02` und `LOCAL-03` weisen ihren dekompilierten Ursprung eindeutig
  nach; `FALSE-01` weist die sichere Ablehnung einer nicht-.NET-Datei nach.
- Kein versioniertes Ergebnisdokument und kein Commit-Vorschlag enthält externe
  Assembly-Identitäten oder charakteristische externe Fachbegriffe; die
  opaken Labels bleiben dort die einzige zulässige Fallreferenz. Die einzige
  Ausnahme ist die ausdrücklich lokale, gitignorierte Prüffall-Matrix unter
  `temp/`.
- Es werden keine Code-, Build-, Test- oder Dokumentationsänderungen
  vorgenommen; bestehende Tests und Dokumente dürfen nur read-only als
  Evidenz untersucht werden.
- Keine spätere Analyse oder Konsolidierung der Finding-Dateien wird als Teil
  dieses Tasks eingeplant.

## Überprüfbare Akzeptanzkriterien

- Für jedes Epic existiert im Task-Verzeichnis genau ein eigener Bericht, und
  jeder Bericht enthält mindestens einen Evidence-/Scope-Abschnitt, auch wenn
  dort kein reproduzierbarer Befund gefunden wird.
- Jede als Fehler oder Lücke bezeichnete Aussage verweist auf konkrete aktuelle
  Code-/Symbolstellen oder einen reproduzierbaren MCP-Vertrag; erwartete
  Trunkierung und `partial`-Ergebnisse werden nicht als Fehler gezählt, wenn
  sie dem dokumentierten Vertrag entsprechen.
- Der `GIT-01`-Bericht enthält einen redigierten Origin-Nachweis mit
  Mappingstatus, Provider-/Checkoutstatus, Snapshot-/Revisionspräsenz,
  Trust-/Completeness-Werten und MCP-Origin; der Bericht behauptet keine
  Source-Nutzung allein aus einer Konfigurationsdatei.
- Die `LOCAL-01`-/`LOCAL-02`-/`LOCAL-03`-Berichte enthalten den entsprechenden
  redigierten Decompilation-Nachweis, ohne externe Pfade oder Symbolnamen zu
  reproduzieren.
- Der `FALSE-01`-Bericht weist nach, dass eine nicht-.NET-EXE als erwarteter
  Negativfall mit einem strukturierten, recoverable Fehler behandelt wird und
  weder Prozessstart noch Assembly-Ausführung stattfindet.
- Jeder Bericht enthält eine sichtbare Liste offener Unsicherheiten und eine
  Empfehlung, wie sie in einem späteren Implementierungs-/Verifikationstask
  entschieden werden kann.
- Eine abschließende Redaktionsprüfung bestätigt, dass weder versionierte
  Ergebnisdokumente noch Commit-Vorschläge externe Identitäten oder
  charakteristische Begriffe enthalten. Die lokale Prüffall-Matrix wird nur
  auf Gitignore-Status und Nichtübernahme in Ergebnisse geprüft.
- Die Berichte machen explizit kenntlich, welche Nachweise nur gelesen und
  welche MCP-Abfragen tatsächlich ausgeführt wurden; es gibt keinen Anspruch
  auf grüne Builds oder Tests in diesem Task.
- Der konzeptionelle Übergabestatus ist erreicht: Scope, Epic-
  Wellen-/Agentenregeln, Befundschema und Prioritäts-/Größenrubrik sind
  festgelegt; verbleibende Punkte sind als spätere Abhängigkeiten beschrieben.

## Geplante Verifikation

Die Audit-Agenten verwenden für C#-Semantik zuerst die AiNetLinter-MCP-Tools
mit `targetType` und absolutem `targetPath`; `rg`/Dateilesen ergänzt nur
Dokumentations-, Konfigurations- und exakte Textarbeit. Relevante Abfragen
sind insbesondere `get_server_health`, `get_file_tree`, `find_symbol`,
`get_class_structure`, `get_symbol_body`, `find_references`,
`get_impact`, `get_test_context`, `inspect_assembly`,
`find_assembly_extensions` und die passenden Assembly-fähigen
Navigationstools. Trunkierung, Herkunft, Generation, Status und
`completeness` werden als Teil der Evidenz notiert.

Die Verifikation ist read-only hinsichtlich Quellcode und untersuchter
Assemblies. Es werden keine Testprozesse, Builds, Assembly-Ausführungen oder
manuelle Git-Operationen gestartet. Für `GIT-01` ist in der späteren
Audit-Ausführung ausschließlich der bestehende External-Source-Provider der
installierten Runtime als kontrollierter Materialisierungspfad zulässig; dieser
Cache-Seiteneffekt muss als solcher offengelegt und darf nicht in
Repository-Dateien verewigt werden. Bestehende Tests werden nur daraufhin
geprüft, welche Verträge sie abdecken und welche Regressionen fehlen. Eine
spätere Umsetzung muss die im Audit erkannten Verifikationslücken separat
planen.

## Notwendige spätere Änderungen

Dieses Audit ändert keine Dokumentation. Falls Findings später umgesetzt
werden, sind je nach Ergebnis insbesondere `Docs/agent-api.md`,
`Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`,
`README.md`, `rules.json` und gegebenenfalls Agentenregeln gegen den
geänderten Code zu aktualisieren. Ob eine konkrete Datei betroffen ist, wird
erst durch ein Finding beziehungsweise die nachgelagerte Umsetzung entschieden.

## Festgelegte Annahmen und spätere Abhängigkeiten

Die fünf externen Prüffälle sind verbindlicher Bestandteil des Audits: ein
konfigurierter Git-Fall (`GIT-01`), drei lokale Decompilation-Fälle
(`LOCAL-01`, `LOCAL-02`, `LOCAL-03`) und ein lokaler Nicht-.NET-Negativfall
(`FALSE-01`). Die konkreten Referenzen bleiben ausschließlich in der
gitignorierten lokalen Prüffall-Matrix. In-Repo-Fixtures und die aktuelle
Projektimplementierung bleiben die primäre Evidenzquelle für Verträge und Code;
die lokale Runtime-/Cache-Umgebung dient dem Origin-Nachweis und dem
Negativfall.

Für die spätere Audit-Ausführung gelten folgende verbindliche
Konzeptannahmen:

- Der 360-Grad-Scope umfasst die Assembly-relevante Transport- und
  Daemon-Semantik, aber kein unabhängiges Audit des gesamten MCP-Servers.
- Die Befundklassifizierung `P0`–`P3` und `S`/`M`/`L`/`XL` ist verbindlich.
- `GIT-01` darf ausschließlich über den bestehenden External-Source-Provider
  der installierten Runtime ausgeführt werden. Ein dabei materialisierter
  Cache ist als Seiteneffekt offenzulegen; manuelle Git-Kommandos bleiben
  ausgeschlossen.
- Höchstens vier Sub-Agenten laufen gleichzeitig. Weitere Epics werden in
  Wellen bearbeitet; jede Welle verwendet ausschließlich neue Agenten und
  abgeschlossene oder beendete Agenten werden nicht wiederverwendet.

Spätere Abhängigkeiten sind der tatsächliche Laufzeit-, Cache- und Sessionpfad
für `GIT-01`, die Verifikation der Worst-Case-Responsebudgets sowie die
nachgelagerte Umsetzung eventuell festgestellter Bugs, Optimierungen oder
Missing Features. Diese Punkte ändern den freigegebenen Analyse-Scope nicht.
