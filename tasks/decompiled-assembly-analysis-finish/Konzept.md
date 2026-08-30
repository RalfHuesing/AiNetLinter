---
status: draft
project_kind: brownfield
estimated_scope: medium
---

# Konzept: Robuste und nutzbare Assembly-Analyse

## Ziel und Nutzen

Die Assembly-Analyse soll als verlässliche, statische Analysefähigkeit des
AiNetLinter nutzbar sein. Sie muss externe Quellstände sicher in eine
Roslyn-Analyse einbinden können, ohne Assemblies zu laden oder auszuführen,
und bei nicht belastbaren externen Quellen kontrolliert auf Decompilation
zurückfallen.

Der unmittelbare Nutzen dieses Vorhabens ist ein klarer, belastbarer Abschluss
der Trust- und Lebenszeitsemantik sowie ein eindeutiger Produktvertrag dafür,
welche Analysefähigkeiten für Assembly-Ziele tatsächlich angeboten werden.
Dadurch werden stille Vermischungen von Clean-, Dirty- und Unverified-Zuständen,
partielle Ressourcenlecks und nicht transparent unterstützte Tool-Aufrufe
vermieden.

## Aktueller Stand

Die bestehende Implementierung verfügt bereits über:

- eine statische Assembly-Analyse mit Decompilation, Fingerprint und begrenztem
  Analyse-Cache;
- direkte Metadaten-Referenzen und eine Roslyn-Workspace-Erzeugung ohne
  `AssemblyLoadContext`, Reflection-Loading oder Ausführung von Assembly-Code;
- externe Source-Auswahl über Mapping, Repository-Acquisition,
  Source-Snapshot, Cache-Reuse und Cache-Refresh;
- Ownership-, Attestation-, Checkout- und Materialization-Lifecycle;
- Source-backed-Analyse mit Decompilation als Fallback;
- fail-closed-Konfigurations- und Trust-Prüfungen sowie die Zustände
  `Clean`, `Dirty` und `Unverified`;
- spezialisierte Assembly-Tools. Allgemeine Tool-Aufrufe werden für
  Assembly-Ziele derzeit noch als nicht unterstützt zurückgewiesen.

Maßgebliche aktuelle Bereiche sind:

- `src/AiNetLinter/Mcp/Assemblies/Analysis/`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Snapshots/`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
- `src/AiNetLinter/Mcp/Tools/AnalysisToolCall.cs`
- `src/AiNetLinter/Mcp/Tools/AnalysisTargetResolver.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysisHostComposition.cs`
- `src/AiNetLinter/Cli/McpServerCommand.cs`
- `src/AiNetLinter/Cli/DaemonHostCommand.cs`
- `Docs/configuration.md` und `README.md`

## Vorgeschlagener aktueller Scope

Der Scope bleibt auf ein zusammenhängendes Abschlussvorhaben begrenzt:

1. Die Status- und Trust-Semantik wird an den verbleibenden Grenzfällen
   gehärtet. Ein einzelnes nicht erlaubtes `CR`-Zeichen am Ende eines
   Statusdatensatzes darf nicht als gültiger sauberer Checkout gelten.

2. Die Materialization-Lease wird gegen partielle Ressourcenlecks abgesichert.
   Cancellation, ungültige Dateiinventare, Fehler beim Öffnen und Fehler beim
   Aufräumen müssen eine deterministische Freigabe bereits geöffneter Handles
   sicherstellen. Ein nicht vollständig attestierbarer Checkout bleibt
   unverified und darf nicht als Source-backed-Quelle weiterverwendet werden.

3. Der produktive Host-Vertrag für externe Quellen und die
   Assembly-Tool-Fähigkeiten werden verbindlich festgelegt. Die aktuelle
   Host-Komposition verwendet standardmäßig noch einen nicht verfügbaren
   Provider; die getestete Source-Acquisition ist daher nicht automatisch ein
   Beleg für eine produktiv erreichbare externe Quelle.

4. Für Assembly-Ziele wird die empfohlene begrenzte Capability-Grenze
   verwendet: statische Struktur-, Symbol-, Referenz-, Aufruf- und
   Metrikabfragen bilden die Kernmenge. Regel-, Diff-, Test-, Dead-Code-,
   Duplicate- und Pattern-Fähigkeiten werden nicht pauschal freigeschaltet.
   Die Grenze muss zwischen unterstützten statischen Kernfähigkeiten,
   teilweise verfügbaren Ergebnissen und bewusst nicht unterstützten Tools
   unterscheiden.

5. Die daraus entstehenden Benutzer- und Integrationsverträge werden in den
   vorhandenen Dokumentations- und Tool-Beschreibungsstellen nachgezogen.

Die folgenden Punkte gehören ausdrücklich nicht automatisch zum Startumfang:
transitive Abhängigkeitsauflösung, vollständige Unterstützung aller allgemeinen
Linter-Tools, eine neue Cache-Garbage-Collection sowie eine umfassende
Namespace-Sperre gegen konkurrierende gleichberechtigte Prozesse. Sie bleiben
als spätere Abhängigkeiten oder bewusste Grenzen dokumentiert.

## Muss-Kriterien

- Die Parser- und Lease-Grenzfälle sind fail-closed und deterministisch.
- Bereits erworbene Ressourcen werden bei jedem Fehlerpfad, einschließlich
  Cancellation und invaliden Eingabedaten, vollständig freigegeben.
- `Clean`, `Dirty` und `Unverified` behalten ihre Bedeutung über Transport,
  Acquisition, Cache, Provider und Snapshot hinweg. `Dirty` darf nicht in
  `Clean` umgedeutet werden.
- Eine Source-backed-Analyse darf nur mit einer erfolgreich attestierten,
  besessenen und lebenszeitlich noch gültigen Quelle entstehen.
- Es wird kein Assembly-Code geladen, reflektiert oder ausgeführt.
- Der produktive Host-Vertrag benennt eindeutig, ob und unter welchen
  Bedingungen externe Quellen standardmäßig erreichbar sind. Credentials
  bleiben außerhalb von Konzept, Diagnosen und normaler Konfiguration; falls
  Credentials unterstützt werden, erfolgt die Auflösung über eine getrennte,
  injizierbare Grenze.
- Assembly-Tools melden ihre tatsächliche Capability und Partialität
  maschinenlesbar oder in einer gleichwertig eindeutigen Diagnose. Ein nicht
  unterstützter Aufruf darf nicht wie ein leerer oder erfolgreicher Befund
  aussehen.
- Die Assembly-Kernmenge umfasst zunächst statische Struktur-, Symbol-,
  Referenz-, Aufruf- und Metrikabfragen. Weitere allgemeine Tools benötigen
  eine eigene Scope-Entscheidung.
- Source-Fallback, Fehlerzustand, Ownership und Snapshot-Lebenszeit sind für
  jeden unterstützten Pfad dokumentiert.
- Änderungen an CLI-, Regel- oder Konfigurationsverträgen führen zu den dafür
  geltenden Dokumentations- und Synchronisationsschritten.

## Akzeptanzkriterien

- Ein Statusdatensatz mit Lone-`CR` wird als ungültig behandelt; ein gültiger
  CRLF-Datensatz und ein gültiger sauberer Status bleiben unverändert gültig.
- Tests weisen nach, dass bei Cancellation, ungültigem Inventar und Fehlern
  beim Öffnen keine bereits erworbenen Materialization-Handles oder temporären
  Checkout-Ressourcen zurückbleiben.
- Tests weisen nach, dass Dirty- und Unverified-Zustände über Acquirer,
  Provider und Fallback-Diagnose erhalten bleiben und niemals als Clean
  attestiert werden.
- Ein Snapshot kann die Quelle nur innerhalb der vorgesehenen Lease-Lebenszeit
  verwenden; nach Freigabe der Lease ist keine Source-backed-Nutzung mehr
  möglich.
- Der endgültige Host-Entscheid ist durch einen deterministischen Test für
  Default-Komposition und durch einen Test für injizierte Provider abgedeckt.
- Für jede freigeschaltete Assembly-Capability existieren ein positiver Fall,
  ein nicht verfügbarer oder partieller Fall und ein klarer Fallback- oder
  Fehlerfall.
- Die Dokumentation beschreibt Zieltypen, unterstützte Tools, Herkunft des
  Analysemodells, keine Codeausführung, externe Quellen, Trust-Zustände,
  Fallbacks und bekannte Grenzen.
- Vor der Implementierungsfreigabe werden die projektweit vorgeschriebenen
  Build-, Fast-Test- und Integration-Test-Läufe ohne Stress-Kategorie sowie
  die passenden MCP-/Safeguard-Prüfungen ausgeführt. Diese Verifikation ist
  Bestandteil der späteren Umsetzung, nicht dieses Konzeptierungsdurchlaufs.

## Non-Goals und bewusste Grenzen

- Kein Laden oder Ausführen fremder Assemblies, keine Reflection-basierte
  Analyse und kein `AssemblyLoadContext`.
- Keine automatische Implementierung transitive Referenzauflösung in diesem
  Scope. Direkte Metadaten-Referenzen bleiben die belastbare Ausgangsbasis;
  die Erweiterung wird als eigenes Teilvorhaben behandelt.
- Keine pauschale Freischaltung der allgemeinen Analyse-, Regel-, Diff-, Test-,
  Dead-Code-, Duplicate- oder Pattern-Tools für Assembly-Ziele über die
  beschlossene Kernmenge hinaus.
- Keine Verteidigung gegen einen bösartigen lokalen Administrator und keine
  vollständige Synchronisationsgarantie gegen konkurrierende Prozesse mit
  denselben lokalen Rechten.
- Keine privilegierte Windows-Reparse-Umgebung als generelle Voraussetzung für
  den Start oder für jeden CI-Lauf. Capability-gesteuerte Tests bleiben
  möglich; fehlende Privilegien sind ein Umgebungsbefund.
- Keine neue externe Credential- oder Secret-Infrastruktur ohne ausdrücklich
  festgelegten Provider-Vertrag.
- Keine Änderung historischer Task-Dokumente, keine Roadmap, keine Step-Dateien
  und kein zusätzlicher Task-State als Bestandteil dieses Vorhabens.

## Betroffene Projektbereiche und Schnittstellen

Die Trust-Korrekturen betreffen insbesondere den Statusparser,
`ExternalSourceCheckoutMaterializationLease`, Attestation und die
Repository-/Snapshot-Lifecycle-Klassen.

Die Host- und Capability-Entscheidung betrifft insbesondere:

- `AssemblyAnalysisHostComposition` sowie die produktiven MCP- und Daemon-
  Einstiegspunkte;
- `AssemblySourceSelectionOrchestrator`, Provider und Acquirer;
- `AnalysisToolCall`, `AnalysisTargetResolver` und die spezialisierten
  Assembly-Registrierungen;
- die Assembly-Analyse-Modelle für Origin, Partialität, Diagnose und
  Referenzauflösung.

Die Implementierung muss die bestehenden Namespace- und Architekturgrenzen
beibehalten. Gemeinsame Pfad-, URL-, Status- und Trust-Regeln gehören an ihre
jeweilige zentrale Abstraktion und dürfen nicht parallel in einzelnen Tools
neu codiert werden.

## Betriebs- und Bedrohungsmodell

Der Dienst läuft grundsätzlich als lokaler MCP-/Daemon-Prozess beim Entwickler.
Eine spätere Nutzung in CI kann dieselben Verträge verwenden, ist aber kein
zusätzliches Bedrohungsmodell für den Start. Der Dienst verarbeitet lokale
Assembly-Dateien und optional Inhalte aus externen Repository-Quellen.
Remote-Inhalte und Repository-Zustände werden als untrusted behandelt. Lokale
Checkout-Verzeichnisse, Cache-Generationen, Ownership-Marker und Attestations
sind Integritätsgrenzen, keine Vertrauensbeweise allein durch ihre Existenz.

Das System schützt insbesondere vor:

- versehentlich oder absichtlich ungültigen Repository- und Statusdaten;
- unvollständigen, fremden oder nachträglich veränderten Checkouts;
- gefährlichen Pfaden, Reparse-/Link-Ausweichungen und unkontrollierten
  Schreibzielen;
- unbeabsichtigter Ausführung von Assembly-Code;
- Veröffentlichung eines nicht vollständig validierten Cache-Zustands;
- versehentlicher Preisgabe von Credentials in Diagnosen, Argumenten oder
  persistierten Konzept- und Statusdaten.

Nicht zugesichert wird vollständige Abwehr gegen einen bösartigen lokalen
Administrator oder gegen einen gleichberechtigten konkurrierenden Prozess,
sofern die explizite Attestation- und Ownership-Prüfung nicht verletzt wird.
Wenn sich aus einer Scope-Entscheidung doch ein stärkeres Namespace-Modell
ergibt, ist dieses als separates Sicherheitsvorhaben zu behandeln.

## Fehler-, Fallback- und Lebenszeitsemantik

- Ungültige oder widersprüchliche Konfiguration endet terminal mit einem
  Konfigurationsfehler; sie wird nicht durch Decompilation kaschiert.
- Keine Zuordnung, nicht erreichbare externe Quelle, fehlende optionale Quelle
  oder nicht unterstützte Capability führen zu einer eindeutigen Diagnose.
  Soweit das Zielmodell dies zulässt, wird statische Decompilation als
  Fallback verwendet; ein invalidierter Source-backed-Zustand wird niemals
  als gültiger Source-backed-Zustand weitergereicht.
- `Clean` ist die einzige Trust-Stufe für eine reguläre Source-backed-
  Materialisierung. `Dirty` bleibt sichtbar und darf höchstens einen
  ausdrücklich dokumentierten degradieren Pfad auslösen. `Unverified` bleibt
  unverified und darf nicht attestiert werden.
- Cancellation wird nach der best-effort-Bereinigung weitergegeben. Fehler
  beim Aufräumen werden diagnostisch sichtbar, dürfen aber keine bereits
  ungültige Quelle in einen Erfolg umwandeln.
- Ein Checkout-Handle besitzt seinen lokalen Arbeitsbereich. Eine
  Materialization-Lease hält die für Attestation, Kopie und Snapshot nötigen
  Ressourcen bis zum Ende des jeweiligen Lebenszyklus. Snapshot und Workspace
  dürfen die Lease nicht vorzeitig freigeben.
- Cache-Generationen werden nur nach vollständiger Validierung veröffentlicht.
  Ein fehlgeschlagener Refresh darf den letzten gültigen Zustand verwenden,
  sofern dessen Trust und Manifest weiterhin gültig sind; andernfalls wird der
  externe Pfad als nicht verfügbar behandelt.

## Verifikation und Dokumentationspflichten

Die spätere Umsetzung verifiziert zuerst die betroffenen Parser-, Lease-,
Attestation- und Fallback-Pfade mit deterministischen Fakes und vorhandener
Testinfrastruktur. Danach folgen die vorgeschriebenen vollständigen Läufe der
Fast- und Integration-Tests ohne Stress-Kategorie, der fehler- und
warnungsfreie Build sowie die MCP-Safeguard-/Violation-Prüfungen. Die im
aktuellen Umfeld nicht verfügbare Windows-Reparse-Berechtigung darf dabei nicht
als stillschweigend bestandener Produktionsnachweis gelten.

Bei Änderungen an öffentlichem Tool-Vertrag, Host-Konfiguration,
CLI-Optionen oder `rules.json` werden mindestens `README.md` und
`Docs/configuration.md` sowie die jeweils zuständigen Integrationshinweise
aktualisiert. Falls Regeln oder CLI-Verträge geändert werden, wird die
Agenten-Regelsynchronisation gemäß Projektregeln ausgeführt.

## Annahmen

- Der aktuelle Codebestand und die aktuellen Projektregeln sind die
  maßgebliche technische Grundlage.
- Das primäre Betriebsziel ist ein lokal beim Entwickler laufender
  MCP-Server.
- Der Startumfang soll klein genug bleiben, um Trust-Korrekturen und einen
  klaren Produktvertrag ohne parallele Großbaustellen abzuschließen.
- Öffentliche Repository-Quellen können grundsätzlich ohne persistierte
  Credentials funktionieren. Private Quellen benötigen eine explizite,
  getrennte Credential-Resolver-Entscheidung.
- Deterministische Tests mit injizierten Transporten und Providern sind für
  die meisten Verträge ausreichend; reale externe Netzwerk- und privilegierte
  Umgebungen sind ergänzende Nachweise.
- Die beschlossene Assembly-Kernmenge umfasst statische Struktur-, Symbol-,
  Referenz-, Aufruf- und Metrikabfragen. Eine transitive
  Abhängigkeitsauflösung wird nur nach separater Scope-Entscheidung begonnen.

## Offene Fragen, die den Start blockieren

1. Wenn ein Entwickler ein Assembly analysiert und dafür ein passendes
   externes Repository gemappt ist: Soll der normal gestartete lokale
   MCP-Server dieses Repository selbstständig abrufen und als Source-backed-
   Quelle verwenden, oder soll die externe Quelle weiterhin nur dann genutzt
   werden, wenn ein Provider ausdrücklich verdrahtet wurde? Die Empfehlung ist,
   öffentliche Repositories im normalen lokalen Host direkt zu unterstützen
   und private Repositories nur über eine optionale, injizierbare
   Credential-Auflösung zu ergänzen. Credentials sollen nicht in normaler
   Konfiguration oder Diagnosen gespeichert werden.

Die Capability-Auswahl und das begrenzte lokale Bedrohungsmodell sind damit
entschieden. Detailentscheidungen zu transitiven Referenzen, Origin-
Alias-Bereinigung, einer zentralen Origin-String-Konvention und Cache-
Retention können als nachgelagerte Teilbereichsentscheidungen behandelt
werden.

## Spätere Annahmen und Abhängigkeiten

- Ein späteres Abhängigkeitsvorhaben kann die direkte Referenzauflösung um eine
  begrenzte transitive Closure mit expliziten Größen-, Tiefe- und Fehlergrenzen
  erweitern.
- Ein späteres Capability-Epic kann weitere allgemeine Tools anhand desselben
  maschinenlesbaren Vertrags aufnehmen. Es darf die statische No-Execution-
  Grenze nicht aufweichen.
- Die Credential-Auflösung für private Quellen kann später über einen
  separaten, sicherheitsgeprüften Adapter erfolgen; der Assembly-Analyse-
  Vertrag soll davon unabhängig bleiben.
- Die interne Bereinigung ungenutzter Origin-Aliase und die Zentralisierung
  von Origin-Bezeichnungen werden nur bei nachgewiesenem Kompatibilitätsnutzen
  durchgeführt.
- Eine stärkere Namespace- oder Multi-Process-Sperre wäre ein eigenes
  Sicherheits-/Betriebs-Epic mit eigener Bedrohungsanalyse.

## Arbeitsgedächtnis (nur Draft)

### Geprüfte Evidenz

- Die aktuelle Assembly-Analyse umfasst 68 Produktionsdateien im Bereich
  `Mcp/Assemblies`; der aktuelle MCP-Violation- und Dead-Code-Abgleich ergab
  dort keine Befunde.
- `ExternalSourcePathRules.IsDriveQualified` wird gemeinsam verwendet; die
  frühere Duplikationsannahme ist daher erledigt.
- Der Origin-Alias `AssemblyOrigin.Kind` hat aktuell keine produktiven
  Referenzen. Eine Entfernung bleibt wegen möglicher interner
  Kompatibilitätsfragen außerhalb des Startumfangs.
- Der Statusparser behandelt einen Datensatz mit Lone-`CR` derzeit noch wie
  einen zulässigen Abschluss; das ist ein konkreter Korrekturbedarf.
- Die Lease-Erzeugung räumt bei Dateisystemfehlern geöffnete Handles auf, hat
  aber bei Cancellation, invaliden Eingaben und einzelnen Dispose-Fehlern noch
  keinen vollständig geschlossenen Fehlerpfad.
- Cache-Reuse und Cache-Refresh arbeiten mit validierten, veröffentlichten
  Generationen und request-eigenen Checkouts. Eine zusätzliche
  Namespace-Sperre ist unter dem begrenzten Bedrohungsmodell nicht als
  Startkriterium belegt.
- `AssemblyReferenceResolver` löst derzeit direkte Metadaten-Referenzen auf;
  eine transitive Closure ist nicht vorhanden.
- Die produktiven Host-Einstiegspunkte erzeugen standardmäßig eine
  `UnavailableExternalSourceProvider`-Komposition. Provider-Injektion ist in
  Tests und Kompositionspfaden möglich.
- Die vorhandene Dokumentation beschreibt statische Assembly-Tools,
  Decompilation, Mapping und Fallback bereits teilweise; eine abschließende
  Capability-, Trust- und Host-Vertragsbeschreibung fehlt noch.

### Vorläufige Bewertung der gefundenen Punkte

**Weiterhin relevant und für den Scope vorgesehen:**

- Statusparser-Grenzfall und vollständige Lease-Bereinigung;
- expliziter Default-Host-/Provider-Vertrag;
- die bestätigte begrenzte Assembly-Kernmenge und eindeutige
  Partialitätsdiagnose;
- abschließende Dokumentation und deterministische Verifikation.

**Bereits umgesetzt:**

- statische Analyse ohne Runtime-Loading;
- Decompilation, Fingerprint, Cache-Grundlage und direkte Referenzauflösung;
- Mapping, Source-Selection, Gitea-Transport, Acquisition, Cache-Reuse,
  Refresh, Attestation und Snapshot-Lebenszeit;
- fail-closed-Konfigurationspfad und Erhalt der Dirty-Trust-Semantik;
- zentrale Pfad-, URL- und Test-Snapshot-Hilfen.

**Veraltet:**

- alte Annahmen, nach denen Mapping, Source-Auswahl, Cache-Vertrag oder die
  grundlegende Attestation noch unentschieden wären;
- frühere Statusstände, kleinteilige Implementierungsabfolgen und historische
  Kritiker- oder Stop-Empfehlungen;
- die Annahme, dass die frühere Pfadprüfung noch doppelt implementiert sei.

**Ungeklärt und einem Teilbereich zuzuordnen:**

- Default-Verhalten und Credential-Grenze des produktiven Source-Providers;
- Umfang und Grenzen einer späteren transitiven Referenzauflösung;
- Kompatibilitätsbedarf des Origin-Alias und der Origin-Textkonvention.

**Overkill oder bewusst nicht übernehmen:**

- vollständige Namespace-Immutability gegen gleichberechtigte Prozesse;
- Runtime-/Reflection-Loading, privilegierte Pflichtumgebungen,
  externe Netzwerkpflichttests und pauschale Freischaltung aller Tools;
- automatische Entfernung interner Alias- oder Cache-Strukturen ohne
  nachgewiesenen öffentlichen Nutzen;
- eine neue Cache-Retention-/Garbage-Collection-Architektur in diesem Scope.

### Übergabestatus

Bestätigt sind die begrenzte Assembly-Kernmenge und das lokale
Entwickler-Betriebsmodell mit begrenztem Bedrohungsmodell. Der Entwurf ist
noch nicht startbereit. Nach Beantwortung der verbleibenden Provider-Frage
wird der Draft bereinigt, auf die bestätigten Muss-Kriterien
zugeschnitten und dem Nutzer ausdrücklich zur Freigabe vorgelegt. Erst danach
kommt eine Umstellung auf `status: ready` in Betracht. Ein Orchestrator wird
nicht automatisch gestartet.
