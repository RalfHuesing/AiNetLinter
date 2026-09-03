---
status: ready
---

# Konzept: Robuste und fokussierte Analyse fremder .NET-Assemblies

## Ziel und Nutzen

Die Assembly-Analyse des MCP soll sich für Agenten wie ein belastbares read-only Analysewerkzeug verhalten: Sie verwendet bei einer erfolgreich beschafften, attestierten und zur DLL gemappten Git-Quelle diese Quelle tatsächlich als Analysegrundlage, liefert zunächst eine kompakte Sicht auf die angegebene Ziel-Assembly und bleibt bei parallelen Aufrufen beliebiger Agenten- oder Thin-Client-Ausführungsmodelle mit einem gemeinsamen Cache vorhersagbar und ressourcenschonend.

Das Konzept verbindet zwei Ebenen, die fachlich zusammengehören, aber getrennt lieferbar sein müssen:

1. **Betriebssichere Artefakt-Erzeugung:** Decompilationen und Source-Checkouts dürfen pro Artefakt nicht mehrfach parallel entstehen oder Cache-Verzeichnisse unkontrolliert vermehren.
2. **Verständliche Analyseführung:** Die Analyse bleibt im Root-Scope der angeforderten Assembly, kennzeichnet Herkunft und Vollständigkeit und erweitert Referenzen nur auf ausdrückliche Navigation.

Die zweite Ebene soll nicht von einer perfekten Cache-Lösung abhängen. Die Cache-Stabilisierung ist jedoch Voraussetzung dafür, dass die Analyse bei parallelen Aufrufen mit einem gemeinsamen Cache wirtschaftlich nutzbar bleibt.

## Ausgangslage und belegter Kontext

- In einer früheren Beobachtung bestanden 136 gleichartige Assembly-Artefakte unter `cache\\asm` und 17 Checkouts desselben Repositorys unter `cache\\checkouts`. Die Beobachtung entstand in einer Codex-Session mit einem Chat und einem gestarteten Daemon. Sie beweist Parallelität oder wiederholte Erzeugung auf gemeinsamer Cache-Ebene, nicht aber mehrere eigenständige Daemon-Prozesse; mögliche interne Thin-Clients oder weitere Ausführungswege sind unbestätigt. Fehlgeschlagene Bereinigungen wurden zusätzlich beobachtet.
- Der aktuelle fachliche Defekt lautet: Ein Git-Checkout wird für eine DLL beschafft, aber die Analyse verwendet anschließend die Decompilation der DLL statt den vorliegenden Quellstand. Ein Checkout ohne source-backed Analyse ist kein akzeptabler erfolgreicher Pfad.
- Der aktuelle Code besitzt bereits einen persistenten `AssemblyDecompilationCache`, Generationen/Manifeste, Locking- und Pointer-Publishing-Teile sowie einen Repository-Cache mit Checkout-Reservation, Materialization-Lease und Cleanup. Diese Mechanismen dürfen weder doppelt erfunden noch vorschnell als vollständig wirksam angenommen werden; ihr Mehrprozess-Verhalten ist gegen den konkreten Befund zu prüfen.
- Die aktuelle Assembly-API kennt Root-Snapshots, optionale `includeReferences`-Navigation, source-backed und dekompilierte Projekt-Snapshots sowie `bodyAvailability` und `contentMode`. Die dekompilierte Ansicht bleibt eine abgeleitete Darstellung, auch wenn sie als echte Roslyn-Dokumente vorliegt.
- Der Code versucht bereits source-backed Analyse vor der Decompilation. Sie fällt jedoch auf Decompilation zurück, wenn keine nutzbare Source-Selection, kein zum Mapping gehörendes Projekt im Snapshot, keine Assembly-Identität oder keine Projekt-Compilation zustande kommt. Die Umsetzung muss daher die konkrete Fallback-Ursache pro Aufruf sichtbar machen und den beobachteten End-to-End-Fall reproduzieren, statt nur die vorhandene Prioritätslogik umzubauen.
- Ein gemessener Refactoring-Befund ist Teil des Umfangs: die zentrale Klasse `AssemblyDecompilationCache` liegt aktuell mit 502 Codezeilen knapp über dem Projektlimit von 500. Die Stabilisierung darf diese Drift nicht vergrößern; eine sichere, fachlich passende Aufteilung gehört zum Paket.

## Produktverhalten

### A. Gemeinsame Artefakte über Prozessgrenzen

Für jeden Artefaktschlüssel darf es gleichzeitig höchstens einen Erzeuger geben. Andere Aufrufe verwenden ein vollständig verifiziertes Ergebnis oder warten cancellierbar auf den aktiven Erzeuger. Dies gilt sowohl für die Decompilation einer Assembly als auch für die Beschaffung beziehungsweise Materialisierung eines Repository-Snapshots.

Ein erfolgreich beschaffter Repository-Snapshot wird nicht nur gecacht, sondern muss — falls Mapping, Attestierung und zugehöriges Quellprojekt valide sind — die source-backed Roslyn-Analyse erzeugen. Compile-Diagnosen machen diese Analyse `partial`, solange Roslyn lesbare Syntaxbäume hat; sie lösen keine Decompilation aus. Decompilation ist ausschließlich der sichtbare Fallback für fehlende, mehrdeutige, nicht vertrauenswürdige oder syntaktisch nicht lesbare Quelle. Ein erfolgreicher Fallback trägt immer den konkreten Grund.

Ein Schlüssel muss die fachliche Identität abbilden:

- Assembly: vollständiger Byte-Hash der DLL/EXE plus relevante Analyse-/Formatversion und Erzeugungstyp;
- Repository: normalisierte Repository-Identität, Revision, Solution-Pfad und relevante Mapping-Identität plus Erzeugungstyp.

Die Veröffentlichung erfolgt ausschließlich aus einem privaten temporären Arbeitsbereich in einen eindeutig fertigen Artefaktzustand. Leser akzeptieren nur Artefakte mit passendem, vollständigem Manifest und zugehöriger Identität. Ein abgebrochener oder fehlerhafter Lauf darf daher nie als Analysequelle erscheinen.

#### Koordinationsprimitive

Die ursprüngliche Marker-Idee bleibt im Konzept, aber nicht als alleiniger TTL-basierter Lock: Eine bloße `build.lock.json`, die nach Ablauf gelöscht und neu angelegt wird, kann einen noch laufenden Erzeuger neben einem neuen zulassen. Das widerspricht dem Kriterium „höchstens ein Erzeuger“.

Stattdessen hält der aktive Erzeuger eine pro Artefaktschlüssel angelegte Lock-Datei während der gesamten Erzeugung exklusiv geöffnet. Dieser Betriebssystem-Lock ist die Autorität für Exklusivität und wird bei Prozessabbruch automatisch freigegeben. Eine separate, atomar geschriebene Lease-/Markerdatei enthält Owner-ID, Schlüssel, Operation, Startzeit und Diagnosezustand; sie dient der Sichtbarkeit und Cleanup-Entscheidung, nicht dem Recht zum Schreiben. Das Completion-Manifest bleibt der alleinige Nachweis eines lesbaren Artefakts.

Nach dem konfigurierbaren Default von zehn Minuten wird ein noch gehaltener Lock als verdächtig/hängend gemeldet und wartende Aufrufe können abbrechen. Ein automatisches „Stehlen“ eines noch gehaltenen Locks ist ausdrücklich ausgeschlossen. Der Betreiber beendet in diesem Fall den hängenden Prozess; erst die dadurch freigegebene Lock-Datei erlaubt einem neuen Owner die Erzeugung. Damit bleiben Absturz-Recovery und Exklusivität gleichzeitig erhalten.

Ein kurzzeitig persistierter negativer Status verhindert wiederholte gleiche Checkout-Versuche bei einem nachweislich nicht erreichbaren oder nicht verifizierbaren Source-Mapping. Der Status bleibt diagnostisch sichtbar und läuft kontrolliert ab.

### B. Fokussierte Assembly-Navigation

Die angegebene Assembly ist standardmäßig der Root-Scope. Framework- und Referenzsymbole erscheinen nicht ungerichtet in Such- oder Navigationsantworten. Eine Referenz wird zunächst metadata-only mit Auflösungsstatus dargestellt; erst eine gezielte Navigation öffnet sie als Session.

Jedes relevante Ergebnis unterscheidet klar:

- Herkunft: Root oder explizit einbezogene Referenz;
- Inhalt: Originalquelle, source-backed, dekompiliert oder nicht verfügbar;
- Vollständigkeit: vollständig, partiell oder nicht verfügbar;
- Evidenz: direkt sichtbar, über einen verfolgten Pfad abgeleitet, metadata-only oder wegen einer Grenze nicht auflösbar.

Symbolsuche liefert neben einer lesbaren eindeutigen Signatur einen direkt wiederverwendbaren maschinenlesbaren Handle. Er ist generationsgebunden, unterscheidet Overloads und führt bei Veralten zu einer konkreten erneuten Auswahl statt zu einer rätselhaften Fehlermeldung.

Eine spätere, gezielte Seiteneffekt- beziehungsweise Persistenzanalyse darf nicht auf Methodennamen vertrauen. Sie klassifiziert Syntax-, Semantik- und bekannte API-Senken für Datenbank, ORM/Repository, Datei, Serialisierung und Netzwerk sowie erreichbare Aufrufe in geöffneten Referenz-Assemblies.

## Muss-Kriterien

1. Parallele Aufrufe mit derselben installierten Binary und demselben lokalen Cache sind sicher; dies schließt mehrere Daemons ein, verlangt aber nicht, dass diese die beobachtete Ursache waren.
2. Pro identischem Artefaktschlüssel ist zu jedem Zeitpunkt höchstens ein Erzeuger aktiv; wartende Aufrufe können abbrechen.
3. Nur vollständig verifizierte und veröffentlichte Artefakte sind lesbar; veröffentlichte Artefakte werden unveränderlich behandelt.
4. Ein konfigurierbarer Zehn-Minuten-Default meldet verdächtig lange Erzeugungen; ein Prozessabbruch gibt den Lock frei. Eine automatische Übernahme eines noch gehaltenen Locks ist ausgeschlossen, damit kein zweiter Erzeuger parallel läuft.
5. Wiederholte erfolgreiche Aufrufe vermehren weder Assembly-Artefakte noch Checkouts desselben fachlichen Artefakts.
6. Ein verfügbarer, attestierter Checkout mit eindeutig gemapptem Quellprojekt erzeugt eine source-backed Analyse; der Test weist nach, dass keine Decompilation als Ergebnisquelle gewählt wird.
7. Jeder Decompilation-Fallback weist strukturiert einen konkreten Grund aus (Mapping, Projektwahl, Attestierung, Assembly-Identität oder syntaktisch nicht lesbare Quelle), statt einen vorhandenen Checkout stillschweigend zu ignorieren.
8. Negative Source-Ergebnisse werden innerhalb eines begrenzten TTL nicht bei jedem Aufruf erneut materialisiert.
9. Cleanup arbeitet nur innerhalb kontrollierter Cache-Wurzeln, löscht keine unsicher zuordenbaren Einträge und macht Fehlschläge sichtbar.
10. Nach dem Beenden der Daemons kann ein Betreiber die erzeugten Cache-Einträge ohne offene Handles entfernen.
11. Standardantworten zur Assembly-Suche und Navigation bleiben im Root-Scope; Referenzexpansion ist explizit und begrenzt.
12. Methodenkörper und Navigationsbefunde weisen Herkunft und konkrete Vollständigkeit aus; dekompilierter Inhalt wird nicht als Originalquelle bezeichnet.
13. Ein Symboltreffer ist für Body- und Navigations-Folgeaufrufe ohne manuelle Bearbeitung wiederverwendbar.
14. Jede neue oder erweiterte MCP-Antwort hält Text und `structuredContent` fachlich synchron und begrenzt große Resultate sichtbar.

## Nicht-Ziele und Scope-Grenzen

- Keine Ausführung, kein dynamisches Laden und keine Veränderung fremder Assemblies.
- Keine verteilte Lock-Infrastruktur über Rechner- oder Netzgrenzen hinweg; angenommen wird ein gemeinsames lokales Dateisystem.
- Keine automatische Löschung fremder oder nicht eindeutig kontrollierter Verzeichnisse.
- Keine pauschale Expansion eines vollständigen Referenzgraphen.
- Keine vollständige Neugestaltung der Roslyn-Analyse außerhalb von Artefakt-Lebensdauer, Root-Scope und gezielter Navigation.
- Eine allgemeine Persistenzanalyse ist ein eigenständiges späteres Paket; sie blockiert nicht die Cache-Stabilisierung und die Kernnavigation.

## Betriebs-, Fehler- und Sicherheitsmodell

Ein Agent, Thin-Client oder Erzeugungsvorgang kann hängen, abstürzen, gecancelt werden oder ein I/O-/Netzwerkproblem erhalten. Die konkrete Topologie ist absichtlich keine Voraussetzung: Ein Host darf einen oder mehrere Clients starten, und mehrere unterschiedliche Agentensysteme dürfen denselben lokalen Cache verwenden. Ein Timeout beweist nicht, dass ein früherer Erzeuger tot ist; deshalb wird ein noch gehaltener Lock nicht übernommen. Betriebssystem-Lock, Lease-Metadaten, Manifestprüfung und atomare Veröffentlichung verhindern falsche Wiederverwendung. Nach Prozessabbruch ist ein erneuter Aufbau möglich; parallele Duplikate und unbegrenzt wachsende Rückstände sind es nicht.

Health/Logging müssen mindestens Cache-Hits, aktive Erzeuger, Wartende, Übernahmen, negative Cache-Hits, verwaiste Kandidaten und Cleanup-Fehler zählen. Diagnoseausgaben dürfen lokale Pfade nur dort ausgeben, wo dies für den Betreiber zulässig ist; aggregierte MCP-Antworten bleiben kompakt.

## Betroffene Bereiche und Dokumentation

- Assembly-Session, Registry, Generationen, Cache-Locking und Publishing;
- Decompilation sowie Lifecycle und Cleanup der Artefakte;
- External-Source-Provider, Repository-Akquisition, Checkout-Reservation und Snapshot-/Resource-Leases;
- MCP-Verträge für Assembly-Inspection, Symbolsuche, Bodies, Referenznavigation und Server-Health;
- Fast- und Integrationstests für Cache, Daemons und Assembly-Verträge;
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` und bei sichtbaren Meilensteinen `Docs/ROADMAP.md`.

## Geplante Verifikation

Je Umsetzungspaket sind passende xUnit-v3-Tests mit `TestTempDirectory` erforderlich. Der Gesamtabschluss umfasst:

- Fast-Tests für exklusive Lock-Anlage, Warten/Cancellation, Abbruch-Freigabe, Stall-Diagnose ohne Übernahme, Manifestvalidierung, negative TTL und Cleanup-Grenzen;
- einen End-to-End-Test mit gültigem Mapping und geklontem Repository, der `source-backed` als Herkunft belegt und die Decompilation als nicht gewählt nachweist;
- Gegenproben für jeden zulässigen Fallback-Grund, die dessen strukturierte Ursache und die korrekte dekompilierte Herkunft belegen;
- Integrationstests für parallele Aufrufe derselben DLL sowie desselben Repositorys/derselben Revision; wo die Testinfrastruktur es zuverlässig abbildet, zusätzlich prozessübergreifend mit getrennten Daemon-Hosts;
- Abbruchtests zwischen Erzeugung und Veröffentlichung und danach erfolgreiche Wiederherstellung;
- Wiederholungsprüfungen, die die Zahl der Artefakt- und Checkout-Verzeichnisse verifizieren;
- Contract-Tests für Root-Scope, Referenzstatus, Provenienz, generationsgebundene Symbol-Handles, Vollständigkeit und sichtbare Trunkierung;
- gezielte MCP-Impact-/Violation-Prüfungen nach Codeänderungen sowie zum Abschluss `dotnet build`, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
- abschließender scope-naher DRY-/Dead-Code-/Magic-Value-Audit.

## Umsetzungsrahmen und bewusste Entscheidungen

Das Vorhaben wird als ein Epic mit drei fachlich getrennten, jeweils verifizierbaren Paketen umgesetzt:

1. **Source-first und gemeinsame Artefakte:** Quellprojekt auch bei Compile-Diagnosen nutzen, pro Schlüssel exklusiv erzeugen, unveränderlich veröffentlichen, aufräumen und den End-to-End-Befund absichern.
2. **Fokussierte Navigation:** Root-Scope, Referenzübersicht, Provenienz, Vollständigkeit und generationsgebundene Symbol-Handles vertraglich vereinheitlichen.
3. **Spätere Erweiterung:** Persistenz- und Seiteneffektanalyse als eigenes Folgepaket; sie ist kein Startblocker für die ersten beiden Pakete.

Ein Repository-Artefakt wird je Identität und Revision genau einmal als unveränderliche Cache-Generation veröffentlicht. Gleichzeitige Leser erhalten unabhängige logische Lese-Leases, aber keine eigenständigen Klone oder veränderbaren Materialisierungen. Die konkrete In-Memory-Workspace-Lebenszeit bleibt pro Host gekapselt und darf keine Disk-Kopie erzwingen.

Die Stall-Recovery ist entschieden: Nach zehn Minuten wird ein noch gehaltener Lock sichtbar gemeldet; ein Betreiber beendet den blockierenden Prozess. Ein noch gehaltener Lock wird nie automatisch übernommen.

### Commit-Vorschlag

`docs: Konzept zur robusten Assembly-Analyse konsolidieren`
