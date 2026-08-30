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

Das Betriebsmodell ist ein einzelner lokaler MCP-Daemon. Er verwaltet maximal
vier gleichzeitig residente Benutzer-Repositories beziehungsweise
Projektkontexte. Externe Quellen sind davon getrennt: Sie können in beliebiger
Anzahl logisch für Analysen hinzugenommen werden, ohne einen der vier
Benutzer-Kontexte zu ersetzen. Eine externe Quelle ist entweder eine DLL, die
statisch decompiliert wird, oder eine konfigurierte Git-/Gitea-Quelle, aus der
bei ausreichendem Trust ein Source-backed-Snapshot entsteht.

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
   Assembly-Tool-Fähigkeiten werden verbindlich festgelegt. Der einzelne
   lokale MCP-Daemon muss konfigurierte Git-/Gitea-Quellen selbstständig über
   den produktiven Provider anfordern können. Öffentliche Quellen benötigen
   dabei keine persistierten Credentials; private Quellen dürfen nur über eine
   ausdrücklich definierte Credential-Auflösungsgrenze ergänzt werden.

4. Für Assembly-Ziele wird die empfohlene begrenzte Capability-Grenze
   verwendet: statische Struktur-, Symbol-, Referenz-, Aufruf- und
   Metrikabfragen bilden die Kernmenge. Regel-, Diff-, Test-, Dead-Code-,
   Duplicate- und Pattern-Fähigkeiten werden nicht pauschal freigeschaltet.
   Die Grenze muss zwischen unterstützten statischen Kernfähigkeiten,
   teilweise verfügbaren Ergebnissen und bewusst nicht unterstützten Tools
   unterscheiden.

5. Die daraus entstehenden Benutzer- und Integrationsverträge werden in den
   vorhandenen Dokumentations- und Tool-Beschreibungsstellen nachgezogen.

6. Die Trennung zwischen maximal vier residenten Benutzer-Kontexten und einer
   beliebigen Anzahl externer Assembly-Quellen wird im Host-, Registry- und
   Lifecycle-Vertrag sichtbar gemacht. Die Vierergrenze darf externe Quellen
   nicht versehentlich begrenzen; für externe Quellen gelten weiterhin
   explizite Ressourcen-, Ownership- und Lebenszeitregeln.

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
- Ein einzelner lokaler MCP-Daemon verwaltet höchstens vier residente
  Benutzer-Repositories beziehungsweise Projektkontexte.
- Externe DLL- und Git-/Gitea-Quellen sind unabhängig von dieser Vierergrenze
  in beliebiger Anzahl logisch nutzbar. Eine künstliche Begrenzung auf vier
  externe Quellen ist nicht zulässig; konkrete Ressourcenlimits müssen separat
  und sichtbar behandelt werden.
- Der produktive Host-Vertrag benennt eindeutig, wie konfigurierte externe
  Git-/Gitea-Quellen standardmäßig erreichbar sind. Öffentliche Quellen werden
  vom lokalen Default-Host direkt unterstützt. Private Quellen benötigen eine
  getrennte, injizierbare Credential-Auflösung; Credentials bleiben außerhalb
  von Konzept, Diagnosen und normaler Konfiguration.
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
- Der einzelne lokale MCP-Daemon verwaltet höchstens vier residente
  Benutzer-Kontexte; die Lebenszeit- oder Kapazitätsentscheidung für einen
  fünften Kontext ist eindeutig diagnostiziert.
- Mindestens eine DLL-Quelle und mehrere konfigurierte Git-/Gitea-Quellen
  können unabhängig von der Vierergrenze als externe Analysequellen behandelt
  werden. Ein Ressourcenmangel führt zu einer sichtbaren, kontrollierten
  Nichtverfügbarkeit und nicht zu einer stillen Überschreibung eines aktiven
  Kontextes.
- Die Default-Komposition des lokalen Hosts verwendet für konfigurierte
  öffentliche Git-/Gitea-Quellen den produktiven Provider; ein separater Test
  deckt zusätzlich die injizierte Provider-Komposition ab.
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
- die Resident-Registry und ihre Grenze von höchstens vier
  Benutzer-Projektkontexten;
- `AssemblySourceSelectionOrchestrator`, Provider und Acquirer;
- `AnalysisToolCall`, `AnalysisTargetResolver` und die spezialisierten
  Assembly-Registrierungen;
- die Assembly-Analyse-Modelle für Origin, Partialität, Diagnose und
  Referenzauflösung.

Die externe Quellenverwaltung darf nicht an die Resident-Registry gekoppelt
werden. DLL-Decompilation und konfigurierte Git-/Gitea-Quellen erhalten eigene
request- beziehungsweise snapshotbezogene Lebenszyklen und können in
beliebiger Anzahl logisch adressiert werden.

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
- Die Vierergrenze gilt nur für residente Benutzer-Repositories. Externe
  Quellen werden über eigene, begrenzte Request-, Checkout- und Snapshot-
  Lebenszyklen verwaltet. Bei erschöpften lokalen Ressourcen wird der konkrete
  externe Vorgang kontrolliert abgewiesen oder degradiert; aktive Quellen
  werden nicht stillschweigend für einen anderen Vorgang überschrieben.

## Verifikation und Dokumentationspflichten

Die spätere Umsetzung verifiziert zuerst die betroffenen Parser-, Lease-,
Attestation- und Fallback-Pfade mit deterministischen Fakes und vorhandener
Testinfrastruktur. Danach folgen die vorgeschriebenen vollständigen Läufe der
Fast- und Integration-Tests ohne Stress-Kategorie, der fehler- und
warnungsfreie Build sowie die MCP-Safeguard-/Violation-Prüfungen. Die im
aktuellen Umfeld nicht verfügbare Windows-Reparse-Berechtigung darf dabei nicht
als stillschweigend bestandener Produktionsnachweis gelten.

Zusätzlich wird die Ein-Daemon-Komposition geprüft: vier residente
Benutzer-Kontexte bleiben innerhalb ihrer Grenze, während mehrere externe
DLL- und Git-/Gitea-Quellen unabhängig davon adressierbar sind. Die Prüfung
deckt auch die kontrollierte Reaktion auf erschöpfte Ressourcen und die
Freigabe nicht mehr verwendeter Quellen ab.

Bei Änderungen an öffentlichem Tool-Vertrag, Host-Konfiguration,
CLI-Optionen oder `rules.json` werden mindestens `README.md` und
`Docs/configuration.md` sowie die jeweils zuständigen Integrationshinweise
aktualisiert. Falls Regeln oder CLI-Verträge geändert werden, wird die
Agenten-Regelsynchronisation gemäß Projektregeln ausgeführt.

## Annahmen

- Der aktuelle Codebestand und die aktuellen Projektregeln sind die
  maßgebliche technische Grundlage.
- Das primäre Betriebsziel ist ein lokal beim Entwickler laufender
  einzelner MCP-Daemon mit höchstens vier residenten Benutzer-
  Projektkontexten.
- Der Startumfang soll klein genug bleiben, um Trust-Korrekturen und einen
  klaren Produktvertrag ohne parallele Großbaustellen abzuschließen.
- Externe DLL- und Git-/Gitea-Quellen sind logisch nicht auf vier Einträge
  begrenzt; ihre tatsächliche Nutzung bleibt durch explizite lokale
  Ressourcen- und Lebenszeitgrenzen bestimmt.
- Öffentliche Repository-Quellen können grundsätzlich ohne persistierte
  Credentials funktionieren. Private Quellen benötigen eine explizite,
  getrennte Credential-Resolver-Entscheidung.
- Deterministische Tests mit injizierten Transporten und Providern sind für
  die meisten Verträge ausreichend; reale externe Netzwerk- und privilegierte
  Umgebungen sind ergänzende Nachweise.
- Die beschlossene Assembly-Kernmenge umfasst statische Struktur-, Symbol-,
  Referenz-, Aufruf- und Metrikabfragen. Eine transitive
  Abhängigkeitsauflösung wird nur nach separater Scope-Entscheidung begonnen.

## Offene Fragen und spätere Teilbereichsentscheidungen

Für den Startumfang bestehen nach den getroffenen Entscheidungen keine
blockierenden Scope-Fragen mehr. Festgelegt ist, dass der einzelne lokale
MCP-Daemon konfigurierte öffentliche Git-/Gitea-Quellen selbstständig nutzen
kann. Private Quellen benötigen eine getrennte, injizierbare
Credential-Auflösung; Credentials werden nicht in normaler Konfiguration oder
Diagnosen gespeichert.

Die folgenden Fragen blockieren den Start nicht und werden nur im jeweils
betroffenen Teilbereich entschieden:

1. Welche konkreten Sicherheits- und Ressourcenlimits gelten pro externer
   Quelle bei vielen gleichzeitig angeforderten DLL- oder Git-/Gitea-Quellen,
   insbesondere für Cache, Disk, Parallelität und Idle-Lebenszeit?
2. Welche Form soll die spätere transitive Referenzauflösung mit ihren
   Grenzen für Tiefe, Größe und fehlende Abhängigkeiten annehmen?
3. Welche weiteren Tool-Familien sollen nach der Kernmenge einen eigenen
   Assembly-Capability-Vertrag erhalten?
4. Besteht ein nachweisbarer Kompatibilitätsnutzen für die Bereinigung des
   internen Origin-Alias oder für eine zentrale Origin-Textkonvention?

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

### Bestätigte Entscheidungen

- Die Assembly-Kernmenge umfasst statische Struktur-, Symbol-, Referenz-,
  Aufruf- und Metrikabfragen.
- Es gibt einen lokal beim Entwickler laufenden MCP-Daemon.
- Dieser Daemon verwaltet höchstens vier residente Benutzer-Repositories bzw.
  Projektkontexte.
- Externe Quellen sind davon unabhängig und können logisch in beliebiger
  Anzahl hinzukommen. Sie sind entweder DLLs für statische Decompilation oder
  konfigurierte Git-/Gitea-Quellen für Source-backed-Analyse.
- Konfigurierte öffentliche Git-/Gitea-Quellen sollen vom Default-Host
  selbstständig genutzt werden. Private Quellen bleiben an eine separate,
  injizierbare Credential-Auflösung gebunden.
- Das begrenzte lokale Bedrohungsmodell gilt; eine vollständige
  Namespace-Sperre gegen gleichberechtigte Prozesse ist kein Muss.

### Geprüfte Evidenz

- Die aktuelle Assembly-Analyse umfasst 68 Produktionsdateien im Bereich
  `Mcp/Assemblies`; der aktuelle MCP-Violation- und Dead-Code-Abgleich ergab
  dort keine Befunde.
- Die Resident-Registry ist auf höchstens vier residente Projekt-Keys
  ausgelegt. Diese Grenze darf nicht als Obergrenze für externe Quellen
  wiederverwendet werden.
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
- Umsetzung des bestätigten Default-Host-/Provider-Vertrags;
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

- konkrete Credential- und Ressourcenlimits für viele externe Quellen;
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

Die fachlichen Startentscheidungen sind getroffen: begrenzte
Assembly-Kernmenge, ein lokaler MCP-Daemon, höchstens vier residente
Benutzer-Kontexte und beliebig viele logisch getrennte externe Quellen. Der
Entwurf bleibt bis zur ausdrücklichen Nutzerfreigabe auf `status: draft`.
Vor der Freigabe wird nur noch geprüft, ob die Formulierungen den bestätigten
Scope widerspruchsfrei und selbstständig verständlich abbilden. Erst danach
kommt eine Umstellung auf `status: ready` in Betracht. Ein Orchestrator wird
nicht automatisch gestartet.
