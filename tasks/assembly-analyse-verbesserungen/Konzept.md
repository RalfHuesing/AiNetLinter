---
status: ready
---

# Konzept: Verlässliche Source-Backed-Analyse externer Assemblies

## Ziel und Nutzen

Die Analyse einer lokalen externen .NET-DLL soll nachweisbar auf dem zugeordneten Originalquellcode aus einem Git-Repository arbeiten können. Wenn der Download, der Checkout, die Integritätsprüfung oder das Source-Mapping nicht gelingt, darf keine Analyse als Originalquellcode ausgegeben werden.

Der gesamte Analysepfad soll für Agenten gut nutzbar, maschinenlesbar und token-sparsam sein. Häufig benötigte Informationen sollen mit wenigen, gezielten Aufrufen auffindbar sein. Antworten müssen ihren Umfang, ihre Vollständigkeit und ihre Herkunft so beschreiben, dass ein Agent weder unnötig große Ausgaben anfordern noch aus unvollständigen Ergebnissen falsche Schlüsse ziehen muss.

Zusätzlich muss der Checkout- und Cache-Lebenszyklus bei mehreren Agenten und mehreren Daemon-Instanzen deterministisch funktionieren. Gemeinsame Cache-Nutzung darf weder zu doppelten, konkurrierenden oder vermischten Arbeitskopien noch zu unklaren Folgeanalysen führen.

## Ausgangslage und bisherige Evidenz

Bei Analysen externer DLLs wurden folgende MCP-Funktionen verwendet:

- `get_server_health`;
- `inspect_assembly`;
- `find_symbol`;
- `get_symbol_body`;
- `get_call_tree`;
- `find_references`;
- `get_impact`;
- `get_class_structure`;
- `get_file_skeleton`;
- `search_pattern` als Negativtest für ein Assembly-Ziel.

Bei einer DLL ohne nutzbares Source-Mapping wurde auf Dekompilation zurückgefallen. Bei einer zweiten DLL, für die ein Git-Repository hinterlegt war, meldete der MCP-Server ebenfalls:

- `origin=decompiled`;
- `fallbackReason=provider-unavailable`;
- `external-source-repository-checkout-unverified`;
- `external-source-repository-cleanup-failed`.

Die nachgelagerte Symbolsuche und Body-Analyse verwiesen weiterhin auf generierte Cache-Dateien. Damit ist belegt, dass die erwartete Originalquellcode-Analyse in diesem Durchlauf nicht stattgefunden hat.

Der Nutzer beobachtete außerdem, dass unter dem gemeinsamen Cache-Stamm zweimal derselbe Quellcode lag, nachdem zwei Agenten mit demselben Serverpfad, aber getrennten Daemon-Instanzen, dieselbe Aufgabe nacheinander ausgeführt hatten. Ob dies ein Fehler, ein bewusstes Generation-/Checkout-Verhalten oder eine Folge fehlender Cache-Synchronisierung ist, muss durch Tests geklärt werden.

## Muss-Kriterien

1. Eine source-kritische Analyse muss den tatsächlich verwendeten Ursprung eindeutig melden: Originalquelle oder Dekompilation.
2. Bei konfiguriertem Git-Quellcode müssen Repository, Revision, Mapping und Verifikation vor Beginn der Roslyn-Analyse erfolgreich abgeschlossen sein.
3. Ein fehlgeschlagener Source-Checkout darf bei einer Anfrage mit verpflichtender Originalquelle nicht stillschweigend auf Dekompilation zurückfallen.
4. Jeder Folgeaufruf muss denselben Source-Status, dieselbe Revision und dieselbe Analyse-Generation weiterreichen.
5. Gemeinsame Cache-Nutzung durch mehrere Daemon-Instanzen muss entweder sicher unterstützt oder eindeutig als nicht unterstützt erkannt und mit klaren Handlungsanweisungen versehen werden.
6. Identische parallele oder sequenzielle Anfragen müssen ein deterministisches Ergebnis liefern und dürfen keine vermischten Arbeitskopien verwenden.
7. Fehlerhafte Checkouts müssen vollständig, wiederholbar und Windows-kompatibel bereinigt oder in einen kontrollierten Quarantänezustand überführt werden.
8. Die MCP-Antworten müssen ausreichend Diagnoseinformationen liefern, damit Download, Checkout, Revision, Mapping, Analysequelle und Bereinigung ohne Zugriff auf interne Logdateien nachvollzogen werden können.
9. Die bestehende read-only-Eigenschaft der Assembly-Analyse bleibt erhalten; externe DLLs und externe Repositories werden nicht verändert.
10. Jede begrenzte oder gekürzte Assembly-Antwort muss maschinenlesbar anzeigen, ob Ergebnisse fehlen, wie weiter paginiert werden kann und welcher Umfang tatsächlich geliefert wurde.
11. Die für Agenten relevanten Assembly-Ergebnisse müssen primär strukturiert und kompakt abrufbar sein; wiederholte Header, lange absolute Cache-Pfade und redundante Metadaten dürfen nicht unnötig Token verbrauchen.
12. Symbole, Überladungen, Call-Tree-Knoten und Referenzen müssen stabile, eindeutig auflösbare Identitäten und vollständige Signaturen besitzen.
13. Unterstützte und nicht unterstützte Operationen müssen je Zieltyp explizit erkennbar sein. Ein nicht passendes Werkzeug darf nicht nur mit einem allgemeinen oder irreführenden Fehler scheitern.
14. Agenten müssen eine kompakte, source-aware Einstiegsabfrage verwenden können, ohne für dieselbe Fragestellung mehrere nahezu identische Vollausgaben zusammenzusetzen.
15. Die Analyse muss für Assembly-Ziele eine geeignete Volltext-/Muster- oder Datenzugriffs-Suche anbieten oder den fehlenden Umfang mit einer nachvollziehbaren Alternative eindeutig ausweisen.
16. Wiederkehrende Kontextdaten wie Analysewurzel, Source-/Cache-Root und Herkunft dürfen innerhalb einer Antwort nur einmal im Header beziehungsweise Kontextblock stehen; Ergebniszeilen verwenden danach relative, stabile Pfade.
17. Referenzauflösung, Referenz-Sessions, Assembly-Dateibaum, Typ-Hierarchie, Memberfilter, Call-Tree-Filter und Impact müssen auch bei externen Assembly-Zielen ihren fachlichen Zweck eindeutig erfüllen oder ihre Einschränkung strukturiert melden.
18. Ein erfolgreicher Source-Backed-Auftrag muss als zusammenhängender, unveränderlicher Nachweis aus Clone, Verifikation, Mapping, Roslyn-Laden und verwendeter Analysequelle abrufbar sein.
19. Änderungen an strukturierten Antwortverträgen müssen wire-kompatibel oder versioniert erfolgen; bestehende CLR-/Test-Properties dürfen nicht ohne Alias- oder Migrationsstrategie umbenannt werden.

## Geplanter Lösungsumfang

### 1. Source-Policy und Analysevertrag

Der Assembly-Analysevertrag erhält einen expliziten Source-Modus, mindestens mit den Zuständen:

- `source_required`;
- `source_preferred`;
- `decompilation_allowed`.

Ein konfiguriertes Git-Mapping aktiviert grundsätzlich den Source-Pfad und macht Originalquelle zur bevorzugten und zu verifizierenden Analysebasis. Ein Fallback auf Dekompilation ist nur bei klassifizierten harten Source-Fehlern zulässig, etwa bei Nichterreichbarkeit oder einem nicht verwendbaren Repository. Dieser Fallback muss als eigener, maschinenlesbarer Zustand samt Ursache ausgegeben werden und darf nicht aus einem unvollständigen oder nur scheinbar verifizierten Checkout entstehen.

Für besonders source-kritische Aufrufe bleibt `source_required` als strikter Modus verfügbar: Dort führt jeder Source-Fehler zu einem kontrollierten Fehler ohne Dekompilation. Die Standardkonfiguration darf den pragmatischen Hard-Error-Fallback verwenden, muss aber dieselbe Herkunftstransparenz wie der strikte Modus liefern.

Eine vorab konfigurierte Branch- oder Commit-Revision ist für den Standardfall nicht erforderlich. Nach dem Clone wird der tatsächlich ausgecheckte `HEAD`-Commit lediglich als Diagnose- und Reproduzierbarkeitsinformation erfasst. „Unbestätigte Revision“ bedeutet hier nur, dass der Clone nicht erfolgreich abgeschlossen wurde, kein gültiger `HEAD` gelesen werden kann oder der Cacheinhalt nicht nachweisbar zu diesem Clone gehört. Ein fehlender vorab eingetragener Commit ist kein Fehler.

Die Source-Information wird als unveränderlicher Analysekontext an alle Folgewerkzeuge weitergegeben. `origin`, `contentMode`, `isReconstructed`, Decompiler-Version, PDB-/Source-Status, Vertrauensbewertung, Revision, Mappingstatus und Vollständigkeit müssen semantisch konsistent sein. Der Begriff `source` darf ausschließlich für nachweislich zugeordneten Originalquellcode verwendet werden.

### 2. Git-Checkout-Zustandsautomat

Der Git-Workflow wird als überprüfbarer Zustandsautomat modelliert:

1. Quelle und Zielschlüssel bestimmen;
2. Checkout-Arbeitsbereich reservieren;
3. Repository abrufen oder vorhandenen gültigen Checkout wiederverwenden;
4. Revision festlegen;
5. Git-Repository und Arbeitsbaum verifizieren;
6. Source-Mapping auflösen;
7. Roslyn-Workspace auf genau dieser Quelle öffnen;
8. Analysequelle und Revision für Folgeaufrufe sperren;
9. Nutzung beenden und Checkout freigeben, bereinigen oder quarantänisieren.

Jeder Übergang muss idempotent und bei Fehlern nachvollziehbar sein. Ein nicht verifizierter Checkout darf nicht als wiederverwendbarer gültiger Checkout markiert werden. Für den vorgesehenen einfachen Betriebsfall genügt ein vollständiger Clone auf den Repository-Stand; zusätzliche Branch-Auswahl ist kein Bestandteil dieses Konzepts.

Der Source-Backed-Auftrag muss nach außen wie eine atomare Analysevorbereitung wirken: Entweder liegt ein verifiziertes Source-/Analysepaket mit Repository-Identifier, tatsächlichem `HEAD`, Mapping, Source-Pfad und Generation vor, oder es liegt ein expliziter Fallback-/Fehlerzustand vor. Der Agent darf nicht mehrere lose Antworten selbst zu einem vermeintlichen Nachweis zusammensetzen müssen.

Die Windows-Git-Prozessausführung wird gezielt verifiziert: Arbeitsverzeichnis beziehungsweise `--git-dir`/`--work-tree` müssen eindeutig gesetzt sein, Sicherheitsprüfungen wie `safe.directory` dürfen durch eine ungeeignete globale Konfigurationssperre nicht unbeabsichtigt sabotiert werden, und harmlose `stderr`-Hinweise dürfen nicht pauschal als Checkout-Fehler gelten. Diese Punkte sind als überprüfbare Fehlerklassen zu behandeln, nicht als ungeprüfte Einzelursache.

Für `safe.directory` soll standardmäßig der kanonische konkrete Checkout-Pfad über einen prozesslokalen Git-CLI-Parameter freigegeben werden. `safe.directory=*` ist kein Default, weil es die Sicherheitsprüfung global für den Prozess aufhebt; es darf höchstens als bewusst dokumentierter, isolierter Fallback mit eigener Testabdeckung existieren. Der Statusparser muss Exit-Code, strukturierte Statusausgabe und echte Fehler von harmlosen Warnungen getrennt bewerten.

### 3. Cache- und Mehrdaemon-Semantik

Die Analyse muss ausdrücklich zwischen folgenden Betriebsvarianten unterscheiden:

- mehrere Agenten innerhalb eines Daemons;
- mehrere Daemon-Instanzen mit gemeinsamem Cache;
- mehrere Daemon-Instanzen mit getrennten Cache-Stämmen.

Für einen gemeinsamen Cache sind mindestens atomare Checkout-Schlüssel, Locking, Besitz-/Lease-Informationen, Generationen, Revisionsbindung und Bereinigungsschutz erforderlich. Ein zweiter Prozess darf einen laufenden oder unvollständigen Checkout weder übernehmen noch als gültige Quelle verwenden.

Wenn diese Semantik nicht sicher umgesetzt werden kann, muss der Server den gemeinsamen Cache-Betrieb erkennen und entweder prozesssichere Isolation herstellen oder dem Nutzer eine klare Empfehlung für getrennte Cache-Stämme geben. Die Entscheidung darf nicht von impliziten Pfad- oder Prozessannahmen abhängen.

#### Empfohlene Cache-Isolation über Daemon-Profile

Die nutzerfreundliche Standardlösung ist ein stabiler Daemon-Profilname, nicht eine zufällige Prozess- oder PID-Komponente:

- `cache="..\\cache"` mit `daemon="codex"` verwendet deterministisch `..\\cache.codex`;
- identische Profilnamen dürfen denselben Profilcache verwenden, müssen aber durch die gemeinsame Lock-/Lease-Semantik sicher koordiniert werden;
- unterschiedliche Profilnamen erhalten getrennte Cache-Stämme und können sich nicht versehentlich über Generationen oder temporäre Checkouts vermischen;
- der Profilname wird für Dateisystempfade normalisiert und Kollisionen werden erkannt, nicht stillschweigend zusammengelegt;
- wenn eine strikt getrennte zweite Instanz benötigt wird, muss sie einen eigenen stabilen Instanz-/Profilnamen erhalten; eine zufällige PID-Suffixlösung ist nur für temporäre Staging-Verzeichnisse geeignet;
- ohne explizites Daemon-Profil bleibt der konfigurierte Cache-Stamm bestehen. In diesem Fall gilt der sichere gemeinsame Cache-Vertrag oder eine eindeutige Ablehnung mit Handlungsanweisung.

Damit ist `cache.<daemonname>` eine sinnvolle und verständliche Isolation, ersetzt aber nicht die Prozesssicherheit für zwei Prozesse mit demselben Namen. Genau diese Kombination muss durch Tests abgesichert werden.

### 4. Cleanup- und Quarantänesemantik

Die Bereinigung muss auch unter Windows bei schreibgeschützten Git-Dateien, offenen Handles, abgebrochenen Prozessen und parallelen Zugriffen definiert funktionieren. Ein fehlgeschlagener Cleanup darf nicht zu einem späteren gültigen Treffer im Cache führen.

Für nicht sicher löschbare Checkouts wird ein begrenzter Quarantänezustand benötigt. Dieser muss mit Ursache, Zeitpunkt, Besitzer und TTL sichtbar sein und darf den normalen Analysepfad nicht blockieren.

Die Löschroutine muss unter Windows ReadOnly-Attribute vor dem Löschen rekursiv zurücksetzen, beispielsweise über `File.SetAttributes(path, FileAttributes.Normal)` unmittelbar vor `File.Delete(path)`, oder eine gleichwertige robuste Löschstrategie verwenden. Nach einem fehlgeschlagenen Versuch darf kein halb gelöschter Zustand als gültiger Cache-Treffer erkannt werden.

### 5. Diagnose- und Health-Vertrag

`get_server_health` und zielgebundene Analysewerkzeuge sollen einen gemeinsamen Source-/Checkout-Bericht liefern:

- logischer Checkout-Schlüssel;
- Repository-Identifier ohne unnötige Geheimnisse;
- festgelegte Revision;
- Checkout- und Mappingstatus;
- konkrete Mapping-Regel und betroffene Source-Dateien;
- verwendeter Analyseursprung;
- Cache-Generation;
- Prozess-/Daemon-Zuordnung;
- Lock-/Lease-Status;
- Cleanup-/Quarantänestatus;
- Fehlercode, Phase und verkürzte technische Ursache.
- bei aktivem Fallback die wichtigsten `sourceDiagnostics` direkt im Ergebnis, einschließlich kurzer nächster Handlungsempfehlung;
- bei Bedarf getrennte Angaben für Root-Assembly-Session und transitive Referenz-Sessions: Anzahl, Status, Cache-Größe, letzte Nutzung, TTL, Bereinigungsstatus und harte Obergrenzen.

Die Diagnose muss zwischen „nicht versucht“, „in Bearbeitung“, „fehlgeschlagen“, „verifiziert“ und „bereinigt“ unterscheiden. Ein allgemeines `provider-unavailable` reicht für die Fehlerbehebung nicht aus.

### 5a. Referenzprofile und Referenz-Session-Lebenszyklus

Für externe Installationen muss die Referenzauflösung konfigurierbare, reproduzierbare Profile unterstützen:

- mehrere explizite Referenzverzeichnisse;
- Auswahlregeln für Versionsabweichungen;
- gezielte Aufnahme einzelner Referenzen;
- getrennte Zustände für gefunden, geladen, inkompatibel und nicht gefunden;
- begrenzte und vollständig zählbare Referenz-Sessions;
- klare Kennzeichnung, welche Symbole und Aufrufkanten durch eine fehlende Referenz eingeschränkt sind.

Health- und Analyseantworten müssen Root-Session und transitive Sessions getrennt ausweisen. Die Begrenzung und Freigabe transitiver Sessions braucht nachvollziehbare Limits, TTL und Bereinigungszustände, damit Speicher- und Cache-Wachstum für einen Agenten prüfbar bleiben.

### 6. Agentenfreundlicher Assembly-Analysevertrag

Die allgemeinen Assembly-Befunde werden als eigener Lösungsbereich behandelt und nicht auf das Git-Problem reduziert. Der Vertrag soll mindestens folgende Verbesserungen vorsehen:

- eine kompakte, source-aware Einstiegsfunktion oder ein gleichwertiges zusammengesetztes Ergebnis mit Assembly-Identität, Source-Status, Signatur, relevanten Metriken und gezielten Verweisen auf Body, Referenzen, Call Tree und Impact;
- strukturierte Ergebnisse (`structuredContent`) für Bodies, Skeletons, Call Trees und Suchresultate, damit Folgeaufrufe ohne Textparsing möglich sind;
- eine Assembly-Datei-/Ordnerübersicht über `get_file_tree` oder ein gleichwertiges Assembly-Werkzeug mit `root`, `fileFilter` und kompakten Ansichten wie `tree`, `summary` und `files`;
- begrenzte Standardantworten mit explizitem `truncated`, `total`, `returned` und Cursor-/Weiterladeinformation;
- konfigurierbare Antwortbudgets beziehungsweise Detailstufen, mit kompaktem Agenten-Default und gezieltem Vollmodus;
- stabile kurze IDs und relative oder logisch gekürzte Pfade, während absolute Pfade nur bei Bedarf und eindeutig markiert ausgegeben werden;
- vollständige Signaturen und eindeutige IDs für überladene Methoden sowie stabile IDs und Scope-Informationen für Call-Tree-Knoten;
- bei Überladungen entweder gebündelte Ergebnisse oder verständliche Kurzsignaturen mit Parametern/Arity, damit kein zusätzlicher Aufruf nur zur Auflösung einer Mehrdeutigkeit nötig ist; alternativ müssen Parameterfilter harmonisch über dieselben Symbol-Identifier-Konventionen funktionieren;
- Call-Tree-Knoten mit Deklarationsposition, konkreter Aufrufposition, Aufrufart und Auflösungsstatus; verkürzte Namen bleiben nur Zusatzdarstellung;
- explizite Auflösung des angefragten Scopes bei Abhängigkeiten, Referenzen und Impact, insbesondere wenn ein Methodenbezeichner auf Datei- oder Typ-Ebene erweitert wird;
- `get_type_hierarchy` muss die Symbolauflösung auf Typen begrenzen und gleichnamige Properties/Methoden ignorieren;
- `get_call_tree` muss triviale Properties/Getter über einen klar benannten Filter wie `excludeProperties` oder `kindFilter` ausblendbar machen und relevante Methoden bei `topN` priorisieren;
- `inspect_assembly` muss Typ- und Membertruncation fachlich priorisieren, ausgeblendete Sichtbarkeiten sichtbar melden und über `exactTypeName` beziehungsweise eine gleichwertige Exaktsuche Substring-Rauschen reduzieren;
- `get_symbol_body` muss über `startLine`/`lineCount` oder einen gleichwertigen Offset-/Limit-Vertrag gezieltes Nachladen erlauben;
- `dependency_graph` muss Pfade aller Kanten korrekt relativ zur Analysewurzel beziehungsweise logisch stabil ausgeben und darf Kindpfade nicht an eine falsche Serverbasis hängen;
- `get_impact` muss bei Assembly-Zielen einen schnellen, diff-freien Referenzpfad verwenden oder klar auf `find_references` als passende, schnellere Operation verweisen;
- Assembly-kompatible Volltext-/Muster-Suche einschließlich einer gezielten Suche nach Datenzugriffen, Persistenzaufrufen und relevanten externen Aufrufen;
- ein gebündelter read-only Analysepfad für Kontrollfluss und Datenzugriff mit Eingangsparametern, Transaktionsgrenzen, externen Aufrufen, Fehlerpfaden, fehlenden Abhängigkeiten und Confidence je Befund;
- strukturierte Body- und Skeleton-Felder für Zeilenspanne, Sprache, Provenienz, Syntax-/Semantikdiagnosen, Memberfilter, `maxMembers`, `maxResponseBytes` und Paging;
- sinnvolle Priorisierung und Filterung, damit triviale Accessor-Knoten nicht die fachlich relevanten Aufrufpfade verdrängen;
- einheitliche Zustände für `not_applicable`, `unresolved`, `partial`, `truncated`, `complete` und Vertrauens-/Konfidenzangaben.

Die API soll dabei keine fachlichen Antworten vorwegnehmen. Sie muss nur die Analyseprimitive so liefern, dass ein Agent die Fragestellung mit wenigen, belastbaren und nachvollziehbaren Schritten bearbeiten kann.

#### Assembly-spezifischer Composite-Einstieg

Zusätzlich zu den verbesserten Einzelwerkzeugen wird ein `get_assembly_context` vorgesehen. Der Einstieg verwendet dieselben öffentlichen Begriffe und dieselbe Parameterlogik wie die bestehenden Composite- und Assembly-Werkzeuge:

- Routing mit `targetPath` und `targetType`;
- primäre Symbolauflösung über `symbolIdentifier`, mit `symbol` nur als kompatiblem Alias;
- dieselben Include-Namen (`includeMetrics`, `includeReferences`, `includeCallers`, `includeImpact`, `includeBody` und optional `includeClassStructure`);
- dieselben Grenzwerte und Traversierungsbegriffe wie in den Einzelwerkzeugen (`maxResults`, `maxBodyLines`, `maxCallers`, `depth`, `topN`);
- dieselben Antwortbudget- und Paging-Begriffe (`maxResponseBytes`, Detailstufe und Cursor), statt eines separaten Assembly-Vokabulars.

Die Reihenfolge der Parametergruppen folgt dem bestehenden Muster: Ziel, Symbol, optionale Teilbereiche, Limits/Traversal und zuletzt Budget/Paging. Die Assembly-spezifischen Felder werden ergänzt, ohne vorhandene Namen für dieselbe Bedeutung neu zu erfinden. Der Composite-Einstieg liefert standardmäßig kompakte Identität, Source-Kontext, Signatur, Metriken und gezielte Verweise; Bodies, Caller/Callee, Referenzen und Impact werden über dieselben Include- und Limitparameter kontrolliert. Jeder Teilbereich muss im strukturierten Ergebnis mit Status und Umfang sichtbar sein.

Projektbezogene Dimensionen wie Tests oder projektgebundene Linter-Verstöße werden bei einem Assembly-Ziel als `not_applicable` ausgewiesen und blockieren nicht die übrigen Dimensionen.

Dabei wird vorhandene Teilunterstützung für Assembly-Capabilities, Antwortbudgets und Truncation als Ausgangsbasis genutzt. Das Ziel ist ein einheitlicher, tatsächlich durch die MCP-Schnittstelle verifizierter Vertrag; bereits vorhandene Einzelmechanismen dürfen nicht als Beleg gelten, solange andere Werkzeuge weiterhin abweichende Formate, unklare Herkunft oder unnötig große Ausgaben liefern.

#### Empfohlene Antwortbudget-Semantik

Das Standardbudget wird als serialisierte Antwortgröße in Bytes gemessen, nicht als Tokenzahl, weil Tokenisierung vom Modell und Inhalt abhängt. Als Startwert wird ein Default von 16 KiB pro MCP-Antwort empfohlen. Das entspricht typischerweise einem kompakten, gut weiterverarbeitbaren Kontext und verhindert, dass ein einzelner Call-Tree oder Body den Agenten-Kontext dominiert.

Das Budget soll in `appsettings` konfigurierbar sein und zusätzlich pro Aufruf über explizite Parameter überschrieben werden können. Vorgeschlagen werden die Detailstufen `compact` (Default), `standard` und `full` sowie Cursor-/Paging-Parameter. `full` bedeutet nicht „ungegrenzt in einer Antwort“: Auch dort bleiben ein technischer Maximalwert und Paging erhalten. Alle ausgeblendeten Informationen müssen über Cursor, Detailaufruf oder expliziten Vollmodus erreichbar sein und mit Grund und Umfang ausgewiesen werden.

Für alle begrenzten Ergebnislisten wird ein gemeinsames Envelope mit kanonischen Feldern angestrebt: `totalCount`, `returnedCount`, `isTruncated`, `truncatedBy` und `continuationToken`. Werkzeug-spezifische Zusatzfelder sind zulässig, aber dieselbe Bedeutung darf nicht je Tool unter wechselnden Feldnamen erscheinen.

Die Harmonisierung erfolgt rückwärtskompatibel: Bestehende CLR-Properties und Testzugriffe wie `Results`, `RequestedCount`, `Navigation` oder bereits etablierte Diagnosefelder bleiben zunächst als kompatible Aliase erhalten. Im Wire-Format wird eine kanonische Completeness-/Paging-Struktur eingeführt; alte Felder werden nur solange zusätzlich serialisiert, wie dies für vorhandene Clients erforderlich ist, und dürfen das Standardbudget nicht durch redundante Kopien unverhältnismäßig vergrößern. Der Übergang braucht Contract-Tests für Serialisierung, Deserialisierung und Aliase.

Absolute Cache- und Checkout-Pfade werden im Default nicht wiederholt. Ein logischer Source-/Checkout-Identifier reicht für Folgeaufrufe; der vollständige Pfad ist gezielt anforderbar. So wird der Agent geschützt, ohne relevante Informationen dauerhaft zu verstecken.

#### Verbindliche Ausgabeeffizienz

Die Ausgabe wird nach einem gemeinsamen Kontext-Header-Prinzip gestaltet:

- Der Header enthält einmalig `contextId`, Analyseursprung, Source-/Cache-Root, Revision/Generation, Status und Vollständigkeit.
- Datei- und Symboltreffer enthalten danach nur relative Pfade oder stabile IDs; der vollständige absolute Pfad ist ausschließlich als gezielt angeforderte Diagnose verfügbar.
- Wiederholte Assembly-Identität, Source-Herkunft, Revision, Cache-Pfad, Signatur und identische Diagnosen werden nicht in jedem Treffer erneut ausgegeben.
- Textdarstellung und `structuredContent` dürfen nicht denselben langen Inhalt doppelt übertragen; die Textdarstellung bleibt eine kurze Zusammenfassung und die strukturierte Nutzlast ist die maschinenlesbare Quelle.
- Bodies, Call Trees und Skeletons werden nicht ungefragt vollständig eingebettet. Der Compact-Default liefert Signatur, Ausschnitt oder Verweis; vollständiger Inhalt wird über Detailmodus, Zeilenbereich oder Paging angefordert.
- Batch-Parameter und der Composite-Einstieg sollen mehrere logisch zusammengehörige Informationen in einem Aufruf liefern, ohne verschiedene nahezu identische Werkzeuge zwingend nacheinander aufzurufen.
- Fehlermeldungen enthalten einen kurzen stabilen Fehlercode, eine knappe Ursache und die nächste sinnvolle Aktion. Lange Prozessausgaben und wiederholte Stacktraces bleiben diagnostisch abrufbar, sind aber kein Default-Bestandteil der Agentenantwort.

## Betriebs- und Bedrohungsmodell

Unterstützt werden lokale Entwicklerumgebungen mit mehreren Agenten und potenziell mehreren Daemon-Prozessen auf demselben Rechner. Gemeinsame Cache-Nutzung ist ein ausdrücklich zu prüfender Betriebsfall.

Nicht erforderlich ist ein Schutz gegen einen böswilligen lokalen Administrator. Erforderlich bleiben jedoch Schutz gegen normale Prozessparallelität, Cancellation, Abstürze, stale Checkouts, fehlende Berechtigungen, schreibgeschützte Dateien, inkonsistente Cache-Marker und falsche Source-Zuordnung.

Git-Repositorys und externe DLLs werden nur gelesen. Credentials, Tokens und vollständige private Repository-URLs dürfen nicht in Toolantworten, Diagnosen oder Testartefakten landen.

## Test- und Verifikationskonzept

Die Umsetzung muss testgetrieben gegen reproduzierbare Test-Repositorys und kontrollierte DLL-/Source-Mappings abgesichert werden. Die Tests müssen die tatsächliche Quelle und den Cache-Zustand prüfen, nicht nur einen erfolgreichen Prozessabschluss.

### A. Source-Backed-Erfolg

- Git-Repository mit bekannter Revision bereitstellen;
- externe DLL und Source-Mapping laden;
- `inspect_assembly` ausführen;
- `find_symbol` und `get_symbol_body` ausführen;
- nachweisen, dass `origin=source`, die erwartete Revision und ein Source-Pfad verwendet werden;
- nachweisen, dass die Folgewerkzeuge dieselbe Generation und Quelle verwenden.

### A1. Konsistenter Agenten-Analysepfad

- dieselbe source-aware Einstiegsabfrage mit kleinem Standardbudget ausführen;
- strukturierte Body-, Skeleton-, Call-Tree-, Referenz- und Impact-Ergebnisse prüfen;
- nachweisen, dass kurze IDs, vollständige Signaturen und Scope-Informationen Folgeaufrufe eindeutig machen;
- nachweisen, dass keine unnötigen Wiederholungen großer Pfade oder Header enthalten sind;
- eine Assembly-Suche nach Textmustern und nach Datenzugriffs-/Persistenzmustern ausführen;
- dieselbe Fragestellung mit gezieltem Vollmodus wiederholen und die zusätzliche Ausgabe begründen können.

### A2. Antwortverträge und Token-Budgets

- `maxResults` absichtlich kleiner als die Trefferzahl wählen;
- `total`, `returned`, `truncated` und Weiterladeinformation verifizieren;
- große Bodies, Skeletons und Call Trees auf ein definiertes Standardbudget begrenzen;
- das konfigurierte Defaultbudget und einen per Anfrage erhöhten Detailmodus getrennt prüfen;
- Envelope-Migration mit vorhandenen DTO-Properties und FastTest-Deserialisierung prüfen, ohne unkontrollierte wire-seitige Feldduplikate einzuführen;
- strukturierte Daten gegen ein versioniertes Schema prüfen;
- wiederholte Antworten auf redundante Cache-Pfade, doppelte Header und unnötige Volltexte prüfen;
- prüfen, dass ein gemeinsamer Kontext-Header absolute Wurzeln nur einmal enthält und alle Treffer relative Pfade beziehungsweise IDs verwenden;
- prüfen, dass strukturierte Nutzlast und Textzusammenfassung keine langen Inhalte doppelt übertragen;
- sicherstellen, dass ein Agent bei gekürzten Ergebnissen nicht den Eindruck von Vollständigkeit erhält.

### A3. Einzelne Assembly-Funktionsverträge

- `get_file_tree` mit Assembly-Ziel und den vorgesehenen Root-/Dateifilter-/Ansichtsvarianten ausführen;
- `get_type_hierarchy` mit einem Typnamen testen, der zugleich als Membername vorkommt, und sicherstellen, dass nur Typen aufgelöst werden;
- `get_symbol_body` mit überladenen Methoden testen und entweder gebündelte Bodies oder eine eindeutige Kurzsignatur mit Parametern erhalten;
- `get_symbol_body` mit `startLine`/`lineCount` beziehungsweise dem vereinbarten Offset-/Limit-Vertrag testen und tatsächliche Start-/Endzeilen sowie den Fortsetzungstoken prüfen;
- `get_call_tree` mit Property-Filter und Priorisierung relevanter Methoden testen;
- `inspect_assembly` mit großen Typen, `publicOnly` und exaktem Typfilter testen und prüfen, dass Sichtbarkeits- und Truncation-Bias sichtbar bleiben;
- `dependency_graph` auf korrekte relative Kindpfade und expliziten angeforderten/aufgelösten Scope prüfen;
- `get_impact` für Assemblys gegen `find_references` hinsichtlich Ergebnisgleichheit, Laufzeit und unnötiger Diff-/Projektarbeit prüfen.

### A4. Referenz- und Provenienzverträge

- Referenzprofile mit mehreren Verzeichnissen, Versionsabweichungen und Einzelreferenzen prüfen;
- Zustände `found`, `loaded`, `incompatible` und `not_found` sowie betroffene Symbole/Aufrufkanten prüfen;
- Root- und transitive Referenz-Sessions mit Anzahl, Limit, TTL, Cachegröße und Cleanupstatus prüfen;
- Decompiler-Version, PDB-/Source-Status, lokale Vertrauensbewertung und `isReconstructed` über alle Folgewerkzeuge vergleichen;
- einen vollständigen Source-Backed-Lauf als eine korrelierbare MCP-Antwort beziehungsweise Analyse-Generation prüfen, nicht nur einzelne Statusmeldungen.

### B. Checkout- und Mapping-Fehler

- Repository nicht erreichbar;
- Clone-Prozess abgebrochen oder ohne lesbaren `HEAD` beendet;
- Mapping ohne Treffer;
- beschädigter Checkout;
- nicht sauberer Arbeitsbaum;
- fehlende oder inkompatible Referenzen.

Erwartung: Nichterreichbarkeit, abgebrochener/ungültiger Clone und ein nicht verwendbarer Repository-Inhalt gelten als Hard-Error-Fallback-Kandidaten. Ein fehlender Mapping-Treffer, ein beschädigter Checkout oder ein nicht lesbarer `HEAD` darf niemals als source-backed ausgegeben werden. Der Standardmodus darf in diesen Fällen explizit auf Dekompilation ausweichen; `source_required` schlägt kontrolliert fehl und liefert die Fehlerphase. Partielle Referenzen oder normale Roslyn-Diagnosen sollen dagegen die Source-Herkunft nicht in Dekompilation umwandeln.

### C. Cleanup-Fehler unter Windows

- schreibgeschützte Dateien;
- offene Datei-Handles;
- abgebrochener Checkout;
- Prozessabbruch zwischen Checkout und Mapping;
- wiederholte Bereinigung.

Erwartung: kein halb gültiger Cache-Eintrag, keine unkontrollierte Endlosschleife, sichtbarer Quarantänestatus und erfolgreiche spätere Wiederaufnahme.

Zusätzlich werden Git-Prozessumgebung, Arbeitsverzeichnis, `safe.directory`, harmlose `stderr`-Warnungen sowie ReadOnly-Attribute separat mit positiven und negativen Testfällen geprüft. Ein Cleanup-Test muss nachweisen, dass schreibgeschützte Dateien rekursiv behandelbar sind und ein fehlgeschlagener Versuch weder einen gültigen Marker noch einen unklaren Zwischenzustand hinterlässt.

### D. Zwei Daemon-Instanzen mit gemeinsamem Cache

- zwei getrennte Daemon-Prozesse mit identischem Cache-Stamm;
- identische Anfrage gleichzeitig;
- identische Anfrage nacheinander;
- ein Prozess wird während Checkout oder Cleanup beendet.

Zusätzlich sind die Cache-Profilregeln zu prüfen:

- aus demselben Basispfad und demselben Daemonprofil entsteht immer derselbe deterministische Cachepfad;
- zwei Prozesse mit demselben Profil verwenden den sicheren gemeinsamen Pfad ohne doppelte gültige Publikation;
- zwei unterschiedliche Profile erhalten unterschiedliche Pfade;
- ungültige oder kollidierende Profilnamen werden kontrolliert abgelehnt oder eindeutig normalisiert.

Zu prüfen sind insbesondere:

- Anzahl der Checkout-Verzeichnisse;
- Eindeutigkeit von Lock, Generation und Revision;
- Wiederverwendung eines gültigen Checkouts;
- Verhalten bei konkurrierenden Schreibern;
- Verhalten bei stale Locks;
- Analyseursprung beider Prozesse;
- keine Vermischung von Dateien oder Metadaten.

### E. Zwei Daemon-Instanzen mit getrennten Cache-Stämmen

Der gleiche Test wird mit bewusst getrennten Cache-Stämmen wiederholt. Das Ergebnis dient als Vergleichsbasis und zugleich als Fallback-Handlungsanweisung, falls ein gemeinsam genutzter Cache nicht unterstützt werden soll.

### F. Wiederholbarkeit und Regression

Jeder Test muss neben dem Ergebnis auch Source-Status, Revision, Generation, Checkout-Anzahl und Cleanup-Zustand auswerten. Die Tests gehören in die bestehende MCP-/Integration-Testinfrastruktur; Ad-hoc-Skripte im Task-Verzeichnis sind nicht vorgesehen.

### G. Capability- und Fehlermatrix

- jedes Assembly-Werkzeug gegen unterstützte, nicht unterstützte und nicht auflösbare Zieltypen prüfen;
- `not_applicable`, `unresolved`, `partial` und `truncated` voneinander unterscheiden;
- prüfen, dass Fehler eine konkrete nächste Aktion für einen Agenten ermöglichen;
- prüfen, dass die Toolauswahl für Assembly-Suche, Source-Suche und strukturierte Analyse aus der Capability-Information ableitbar ist.

## Sinnvolle Umsetzungspakete

Die Umsetzung wird bewusst in wenige zusammenhängende Pakete gegliedert:

1. **Analysevertrag und Agenten-UX:** einheitliche Herkunfts-/Vollständigkeitsfelder, strukturierte Antworten, kompakter Einstieg, stabile Identitäten, Scope-Transparenz, Paging und Antwortbudgets;
2. **Source, Cache und Mehrdaemon-Betrieb:** Hard-Error-Fallback, Git-Verifikation, daemonbasierte Cache-Isolation, prozesssichere Wiederverwendung, Cleanup und Quarantäne;
3. **Suche, E2E-Verifikation und Dokumentation:** Assembly-Volltext-/Datenzugriffs-Suche, echte MCP-End-to-End- und Mehrdaemon-Tests, Anwenderhinweise sowie Regression gegen alle vorherigen Pakete.

Die Pakete sind groß genug für zusammenhängende Reviews und klein genug, um nach jedem Paket belastbare Tests auszuführen. Kein Paket gilt als fertig, wenn nur Unit-Tests grün sind und der reale MCP-Aufrufpfad nicht verifiziert wurde.

## Akzeptanzkriterien

1. Ein Test mit gültigem Repository und gültigem Mapping weist eine Analyse auf Originalquellcode nach.
2. Ein Test mit absichtlich fehlerhaftem Checkout weist nach, dass der strikte `source_required`-Modus nicht auf Dekompilation zurückfällt und der Standardmodus nur bei einem klassifizierten Hard-Error explizit ausweicht.
3. Ein Test weist nach, dass `get_symbol_body`, `find_symbol`, `get_call_tree`, `find_references` und `get_impact` denselben Source-Kontext verwenden.
4. Der Mehrdaemon-Test mit gemeinsamem Cache ist deterministisch grün oder der Server verweigert diesen Modus mit einer eindeutigen, dokumentierten Handlungsanweisung.
5. Der Mehrdaemon-Test erzeugt keine unkontrollierten doppelten gültigen Checkouts für denselben logischen Quellschlüssel.
6. Ein abgebrochener oder nicht bereinigbarer Checkout wird weder als gültige Quelle wiederverwendet noch stillschweigend ignoriert.
7. Health- und Analyseantworten zeigen die tatsächliche Quelle, Revision, Generation, Fehlerphase und Cleanup-Semantik.
8. Die vorhandenen Assembly-Analysefunktionen behalten ihre read-only-Eigenschaft.
9. Bestehende Tests bleiben grün; neue Parallelitäts- und Lasttests werden passend als Integration oder Stress kategorisiert.
10. Die betroffenen MCP-Verträge und Anwenderdokumentationen beschreiben die Cache-/Daemon-Nutzung und die Source-Policy konsistent.
11. Ein Agent kann eine typische Assembly-Fragestellung mit einem kompakten Einstieg und wenigen Folgeaufrufen bearbeiten, ohne Volltextantworten parsen oder wiederholte Pfade deduplizieren zu müssen.
12. Begrenzte Ergebnisse weisen ihre Unvollständigkeit und die Fortsetzungsmöglichkeit zuverlässig aus.
13. Überladungen und Call-Tree-Knoten bleiben über mehrere Aufrufe eindeutig identifizierbar; der tatsächlich analysierte Scope ist sichtbar.
14. Bodies, Skeletons, Call Trees und Suchresultate sind strukturiert abrufbar und besitzen ein dokumentiertes, testbares Antwortschema.
15. Assembly-Volltext-/Muster- und Datenzugriffs-Suchen sind verfügbar oder die Capability-Antwort verweist eindeutig auf die unterstützte Ersatzoperation.
16. Die Standardausgaben halten ein festgelegtes Antwortbudget ein; ein größerer Umfang ist nur durch eine bewusste Detail- oder Paging-Anforderung möglich.
17. Das konfigurierte Standardbudget beträgt zunächst 16 KiB pro serialisierter MCP-Antwort und kann über Konfiguration sowie explizite Anfrageparameter angepasst werden.
18. Vollständige Informationen bleiben über Detailmodus oder Paging erreichbar; kein relevanter Inhalt wird ohne Diagnose und Fortsetzungsmöglichkeit still verborgen.
19. Ein explizites Daemon-Profil erzeugt einen deterministischen Cache-Suffix, während gleiche Profilnamen weiterhin sicher koordiniert und unterschiedliche Profilnamen isoliert werden.
20. Die drei Umsetzungspakete können jeweils mit Fast-, Integrations- und bei Bedarf Stress-Tests verifiziert werden; der Abschluss umfasst den vollständigen Nicht-Stress-Testlauf.
21. Ein Test weist nach, dass Analysewurzel, Source-/Cache-Root und Herkunft je Antwort nur einmal erscheinen und Treffer anschließend relative Pfade oder stabile IDs verwenden.
22. Ein Test weist nach, dass Textzusammenfassung und `structuredContent` keine redundanten langen Bodies, Pfade oder Diagnosen duplizieren.
23. `get_file_tree` ist für Assembly-Ziele verfügbar oder die Capability-Matrix nennt eine gleichwertige, getestete Ersatzfunktion.
24. Typ-Hierarchie, Überladungsauflösung, Call-Tree-Filter, Memberpriorisierung, exakte Typfilter und Dependency-Graph-Pfade sind jeweils durch Regressionstests abgesichert.
25. Referenzprofile und transitive Referenz-Sessions sind konfigurierbar, begrenzt, sichtbar und ihren Auswirkungen auf Symbole/Aufrufkanten zuordenbar.
26. Die Assembly-Variante von `get_impact` führt keine unnötige projekt- oder diffbezogene Arbeit aus und ist entweder performant oder verweist eindeutig auf das passende Referenzwerkzeug.
27. Der Source-Backed-Nachweis umfasst Clone, Windows-resiliente Verifikation, Mapping, tatsächlichen Roslyn-Workspace, Revision/Generation und Bereinigung in einem korrelierbaren Ergebnis.
28. Die Windows-Git-Tests unterscheiden konkrete `safe.directory`-Freigabe, bewusst nicht standardmäßige Wildcard-Freigabe, echte Fehler und harmlose `stderr`-Warnungen.
29. Der Cleanup-Test weist das Zurücksetzen von ReadOnly-Attributen vor dem Löschen nach.
30. Die Envelope-Harmonisierung bewahrt bestehende DTO-/FastTest-Zugriffe über kompatible Aliase oder eine dokumentierte Versionierung und erzeugt keine unnötigen doppelten Wire-Felder.

## Non-Goals und Scope-Grenzen

- Keine fachliche Dokumentation der untersuchten Drittanbieter-DLL.
- Keine Änderung oder Ausführung externer DLLs.
- Keine Veröffentlichung oder Synchronisierung privater Quellcode-Repositorys.
- Kein Schutzmodell gegen böswillige lokale Administratoren.
- Kein allgemeiner Umbau des MCP-Servers außerhalb von Source-Provider, Checkout, Cache, Analyseursprung, Diagnose und den dafür notwendigen Tests.
- Keine fachliche Antwort- oder Domänenlogik, die dem Agenten die Interpretation der Assembly-Analyse abnimmt; verbessert werden nur Analyseprimitive, Verträge, Herkunft und Ausgabeform.
- Keine automatische Entscheidung, dass mehrere Daemons grundsätzlich verboten sind; diese Frage wird durch Tests und eine explizite Betriebssemantik geklärt.

## Betroffene Projektbereiche

Voraussichtlich betroffen sind:

- MCP-Assembly-Session und Source-Provider-Integration;
- Git-Checkout- und Repository-Verifikation;
- Cache-Schlüssel, Generationen, Locking, TTL und Cleanup;
- Herkunfts- und Vollständigkeitsmetadaten aller Assembly-Analysewerkzeuge;
- strukturierte Assembly-Antworten, Paging, Antwortbudgets, stabile Symbol-/Knoten-IDs und Capability-Verträge;
- Assembly-Volltext-/Muster- und Datenzugriffs-Suche oder die dokumentierte Ersatzfunktion;
- Health-/Diagnosemodell;
- MCP- und Integrationstests für externe Assemblies und Mehrdaemon-Betrieb;
- Dokumentation der Assembly-Ziele, Source-Policy und Cache-Nutzung.

Als aktuelle technische Anker wurden unter anderem die Assembly-Analyse- und Source-Selection-Komponenten, die Repository-Acquirer-/Cache-Implementierungen, die Assembly-Response-Limits, die Assembly-Navigation und die MCP-Registrierungen identifiziert. Als Testbasis existieren bereits getrennte Tests für Source-Provider, Checkout-Attestierung, Cache-Reuse/Refresh/Cleanup, Session- und Registry-Lebensdauer, Navigationstransparenz, Capabilitys und Response-Budgets. Diese Tests sind zu erweitern und um echte Mehrdaemon- sowie End-to-End-Verträge zu ergänzen, statt parallele Ad-hoc-Testpfade aufzubauen.

Die konkreten Dateien und Symbole werden erst nach der aktuellen Code-Recherche festgelegt und dürfen nicht aus den historischen Findings ungeprüft übernommen werden.

## Dokumentationsbedarf

Nach der Umsetzung müssen mindestens die fachlich betroffenen MCP-Verträge und Integrationsdokumente aktualisiert werden. Die Anwenderdokumentation muss insbesondere beantworten:

- Wann ist ein gemeinsamer Cache mit mehreren Daemon-Instanzen unterstützt?
- Wann müssen getrennte Cache-Stämme verwendet werden?
- Wie wird aus Basispfad und stabilem Daemonprofil der effektive Cachepfad abgeleitet, zum Beispiel `cache` plus `.codex`?
- Wie wird eine bewusst getrennte Instanz über einen anderen Profilnamen gestartet?
- Wie wird Originalquelle gegenüber Dekompilation erzwungen?
- Welche Diagnose zeigt eine fehlgeschlagene Verifikation oder Bereinigung?
- Wie werden stale Checkouts und Quarantänen behandelt?
- Wie kann ein Agent mit kleinem Standardbudget gezielt zu Body, Call Tree, Referenzen, Impact und Datenzugriffen weitergehen?
- Welche IDs, Scope-Angaben, Paging- und Truncation-Felder sind für automatisierte Folgeaufrufe verbindlich?
- Wie wird die Assembly-Capability eines Werkzeugs vor dem Aufruf maschinenlesbar abgefragt?
- Welche gemeinsamen Parameter verwendet `get_assembly_context`, und wie werden Detailmodus, Call-Tree-Richtung und Paging kombiniert?

## Getroffene Leitentscheidungen

1. Gemeinsame Cache-Nutzung bleibt grundsätzlich unterstützt, wird aber nicht als unkoordinierte Standardannahme behandelt. Ein explizites Daemon-Profil isoliert deterministisch über einen Suffix wie `cache.codex`; gleiche Profilnamen benötigen weiterhin prozesssichere Locks, unterschiedliche Profilnamen bleiben getrennt.
2. Ein konfiguriertes Git-Mapping aktiviert grundsätzlich den Source-Pfad. Der Server versucht und verifiziert Originalquelle zuerst. Nur klassifizierte harte Source-Fehler dürfen im pragmatischen Standardmodus zu einer explizit markierten Dekompilation führen; der strikte `source_required`-Modus verweigert diesen Fallback.
3. Das initiale Standardbudget beträgt 16 KiB pro serialisierter MCP-Antwort. Es wird konfigurierbar und pro Aufruf überschreibbar. Ein Vollmodus liefert weiterhin paginiert und innerhalb eines technischen Maximalbudgets.
4. Alle Findings werden in einem Konzept und später in wenigen zusammenhängenden Umsetzungspaketen verfolgt. Die Pakete sind keine Abwertung der übrigen Befunde zugunsten des Git-Themas.

5. Ein separates `daemonInstance`-Feld ist nicht erforderlich. Der vorhandene Daemonname wird als stabiler Cache-Profilname behandelt. Bewusst getrennte Instanzen erhalten unterschiedliche Profilnamen; gleiche Profilnamen werden sicher koordiniert.
6. `get_assembly_context` wird als Assembly-spezifischer Composite-Einstieg ergänzt. Seine Parametergruppen und Begriffe orientieren sich an den bestehenden Werkzeugen, damit Agenten kein zweites, inkompatibles API-Vokabular lernen müssen.

## Finale Betriebsentscheidungen

- Diagnoseantworten enthalten standardmäßig nur einen logischen Kontext-Identifier, Herkunft, Revision/Generation, Status, Fehlercode und eine kurze Handlungsempfehlung. Vollständige lokale Pfade, private Repository-URLs und lange Prozessdetails sind nur über einen expliziten Diagnosemodus abrufbar.
- Ein Hard-Error-Fallback enthält direkt eine kurze nächste Aktion, zusätzlich zum stabilen Fehlercode und zur technischen Fehlerphase.
- Die Umsetzung erfolgt in den drei beschriebenen zusammenhängenden Paketen. Jedes Paket erhält passende Tests; der finale Abschluss verlangt den vollständigen Nicht-Stress-Testlauf.
