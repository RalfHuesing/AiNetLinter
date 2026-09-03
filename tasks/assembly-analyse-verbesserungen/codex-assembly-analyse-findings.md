# Codex-Findings zur Analyse externer Assemblies

Dieses Dokument enthält ausschließlich Verbesserungsbedarf, der bei der read-only Analyse einer lokalen externen .NET-DLL mit den AiNetLinter-MCP-Werkzeugen aufgefallen ist. Namen, Pfade und fachliche Bezeichner aus der untersuchten Drittanbieter-DLL werden bewusst nicht genannt.

## 1. Herkunft der dekompilierten Inhalte eindeutig ausweisen

### Was versucht wurde

Mit `inspect_assembly` wurden Metadaten und mit `get_symbol_body` mehrere Methodenrümpfe aus einer externen DLL gelesen.

### Ergebnis

Die Assembly wurde im Analysekontext als `decompiled`, `partial`, `medium confidence` und `untrusted` ausgewiesen. Im Ergebnis von `get_symbol_body` erschien der Inhalt gleichzeitig als `contentMode: source`. Dadurch bleibt unklar, ob der angezeigte Code Originalquelle oder rekonstruierter Dekompilationscode ist.

### Was besser sein soll

Alle Werkzeuge sollten eine einheitliche Provenienz liefern. Für jeden Codeausschnitt sollten mindestens `origin`, `contentMode`, `isReconstructed`, Decompiler-Version, PDB-/Source-Status und eine lokale Vertrauensbewertung vorhanden sein. Dekompilierter Code darf nicht wie Originalquelle gekennzeichnet werden.

## 2. Vollständigkeit und Trunkierung einheitlich modellieren

### Was versucht wurde

Mit begrenzten und anschließend erhöhten Ergebnislimits wurden Assemblytypen, Member, Symbole, Referenzen und Diagnosen abgefragt.

### Ergebnis

Mehrere Antworten enthielten nur einen Teil der vorhandenen Daten. Bei `inspect_assembly` wurden Typen und Member wegen `responseBudget`, `maxResults` oder `maxMembers` gekürzt. `find_symbol` lieferte bei einem Trefferlimit genau die angeforderte Anzahl, ohne im `structuredContent` eine Gesamtzahl, Trunkierungskennzeichnung oder Fortsetzungsmöglichkeit auszuweisen.

### Was besser sein soll

Alle MCP-Ergebnisse sollten ein gemeinsames Format mit `totalCount`, `returnedCount`, `isTruncated`, `truncatedBy` und `continuationToken` verwenden. Ein gesetztes Limit darf niemals wie ein vollständiges Ergebnis aussehen. Die Trunkierung sollte je Ergebnisart und nicht nur global für die gesamte Antwort sichtbar sein.

## 3. Paging für große Methodenrümpfe ergänzen

### Was versucht wurde

Mit `get_symbol_body` wurden längere Methoden gelesen. Das Zeilenlimit wurde dafür mehrfach vergrößert.

### Ergebnis

Das Werkzeug bietet keinen Offset oder Seitenmechanismus. Bei langen Methoden muss ein Agent das Limit auf Verdacht erhöhen. Das erzeugt entweder unnötig große Antworten oder verhindert das gezielte Nachladen eines Folgeabschnitts.

### Was besser sein soll

`get_symbol_body` sollte `startLine` und `lineCount` oder ein gleichwertiges Paging unterstützen. Die Antwort sollte zusätzlich die tatsächliche Start-/Endzeile und einen Fortsetzungstoken liefern.

## 4. Referenzauflösung für externe Installationen konfigurierbar machen

### Was versucht wurde

Die Assembly wurde einmal ohne und einmal mit `includeReferences=true` untersucht. Danach wurden Health-Informationen und Diagnosen abgefragt.

### Ergebnis

Ein großer Teil der Referenzen konnte nicht identitätsgleich oder überhaupt nicht aufgelöst werden. Die Analyse blieb partiell und erzeugte zahlreiche Decompiler- und Referenzdiagnosen. Mit `includeReferences=true` wurden sehr viele Referenz-Sessions betrachtet, aber nur ein kleiner Teil davon im Ergebnis sichtbar gemacht. Für den Agenten ist nicht ausreichend erkennbar, welche Folgeanalyse durch welche fehlende Abhängigkeit eingeschränkt ist.

### Was besser sein soll

Der Server sollte für Assembly-Ziele konfigurierbare Referenzprofile unterstützen:

- mehrere explizite Referenzverzeichnisse;
- Auswahlregeln für Versionsabweichungen;
- gezielte Aufnahme einzelner Referenzen;
- getrennte Zustände für gefunden, geladen, inkompatibel und nicht gefunden;
- begrenzte, aber vollständig zählbare Referenz-Sessions;
- klare Kennzeichnung betroffener Symbole und Aufrufkanten.

## 5. Referenz-Sessions sichtbar und begrenzt verwalten

### Was versucht wurde

Nach einer Referenzanalyse wurde `get_server_health` mit Session- und Diagnoseinformationen abgefragt.

### Ergebnis

Der Health-Status zeigte die Root-Assembly, aber nicht die tatsächliche Menge und den Lebenszyklus aller transitiven Referenz-Sessions in gleicher Detailtiefe. Dadurch sind Speicherverbrauch, Cache-Wachstum und Bereinigungszustand nicht ausreichend überprüfbar.

### Was besser sein soll

Die Health-Antwort sollte Root-Sessions und transitive Referenz-Sessions getrennt ausweisen, jeweils mit Anzahl, Status, Cache-Größe, letzter Nutzung, TTL und Bereinigungsstatus. Für transitive Sessions sollten harte Obergrenzen und eine kontrollierte Freigabe existieren.

## 6. Überladene Symbole in Aufrufbäumen eindeutig darstellen

### Was versucht wurde

Mit `get_call_tree`, `find_references` und `get_impact` wurde die Navigation von Methoden mit gleichen Namen, aber unterschiedlichen Signaturen geprüft.

### Ergebnis

Im Aufrufbaum wurden mehrere Überladungen nur mit dem verkürzten Namen dargestellt. Dadurch waren unterschiedliche Methoden nicht zuverlässig anhand des Knotennamens unterscheidbar. Die Darstellung enthielt außerdem nicht an jedem Knoten eine direkt weiterverwendbare Symbol-ID.

### Was besser sein soll

Jeder Call-Tree-Knoten sollte die vollständige Signatur, stabile Symbol-ID, Deklarationsposition, konkrete Aufrufposition, Aufrufart sowie den Auflösungsstatus enthalten. Verkürzte Namen dürfen nur als zusätzliche Anzeige verwendet werden.

## 7. Scope-Auflösung im Abhängigkeitsgraphen explizit machen

### Was versucht wurde

`dependency_graph` wurde sowohl mit Datei-/Typbezug als auch mit einem Methodensymbol aufgerufen.

### Ergebnis

Bei der Verwendung eines Methodensymbols wurde die Ausgabe effektiv auf den enthaltenden Typ beziehungsweise die Datei bezogen. Dieser Wechsel des Analyse-Scope war in der Antwort nicht eindeutig als Fallback sichtbar.

### Was besser sein soll

Das Werkzeug sollte Methodensymbole entweder ablehnen oder den Scope explizit ausweisen, zum Beispiel mit `requestedScope: method` und `resolvedScope: containingType`. Zusätzlich sollte eine echte methodengenaue Abhängigkeitsanalyse angeboten werden, wenn sie technisch möglich ist.

## 8. Volltextsuche für dekompilierte Assemblys anbieten

### Was versucht wurde

Mit `search_pattern` wurde nach generischen SQL- und Datenzugriffsmerkmalen in den dekompilierten Dateien gesucht.

### Ergebnis

Das Werkzeug antwortete mit `ASSEMBLY_TARGET_UNSUPPORTED`. Damit fehlt eine MCP-interne Möglichkeit, dekompilierte Inhalte assemblyweit nach Texten, regulären Ausdrücken oder Datenzugriffsoperationen zu durchsuchen.

### Was besser sein soll

`search_pattern` sollte Assembly-Ziele read-only unterstützen und dabei die dekompilierte Workspace-Struktur durchsuchen. Alternativ sollte ein spezialisiertes Werkzeug wie `find_data_access` angeboten werden, das Operationsart, Datei, Methode, Zeile, erkannte Ressource und Confidence strukturiert ausgibt.

## 9. Datenzugriffs- und Kontrollflussanalyse bündeln

### Was versucht wurde

Die Analyse wurde manuell aus Symbolsuche, Methodenrumpf, Call Tree, Referenzen und Abhängigkeitsgraph zusammengesetzt.

### Ergebnis

Die Werkzeuge liefern einzelne Bausteine, aber keine zusammenhängende Antwort für eine Frage nach einem Persistenz- oder Verarbeitungspfad. Ein Agent muss relevante Methoden zunächst über Namensheuristiken finden und anschließend viele Einzelabfragen koordinieren. Bei fehlenden Referenzen bleibt außerdem unklar, ob ein Pfad nur nicht gefunden oder tatsächlich nicht vorhanden ist.

### Was besser sein soll

Ein read-only Analysemodus sollte einen Einstiegspunkt nehmen und daraus einen begrenzten Ablaufbericht erzeugen: Eingangsparameter, Kontrollfluss, Transaktionsgrenzen, externe Aufrufe, Datenzugriffsoperationen, Fehlerpfade, fehlende Abhängigkeiten und Unsicherheiten. Jeder Befund braucht eine Herkunft und eine Confidence.

## 10. Text- und Skeleton-Ergebnisse strukturiert zurückgeben

### Was versucht wurde

Mit `get_file_skeleton` wurde eine große dekompilierte Datei als kompakte Symbolübersicht angefordert. Die Struktur enthielt viele Member und Nutzungsbeziehungen.

### Ergebnis

Die Antwort wurde ausschließlich als Text geliefert. Für `get_symbol_body` war ebenfalls kein verwertbares `structuredContent` mit Body, Zeilenbereich und Provenienz vorhanden. Große Skeleton-Antworten können dadurch nicht zuverlässig maschinell gefiltert oder weiterverarbeitet werden.

### Was besser sein soll

Beide Werkzeuge sollten strukturierte Datenblöcke neben der formatierten Textansicht zurückgeben. Für Skeletons werden Memberfilter, `maxMembers`, `maxResponseBytes` und Paging benötigt. Für Bodies werden Zeilenspannen, Sprache, Dekompilationsstatus und Syntax-/Semantikdiagnosen benötigt.

## 11. Generierte Cache-Pfade von internen Pfaden trennen

### Was versucht wurde

Die Ergebnisse von Symbolsuche, Call Tree und Skeleton wurden auf die ausgegebenen Datei- und Cache-Pfade hin geprüft.

### Ergebnis

Viele Antworten wiederholen lange absolute Pfade des internen Dekompilierungs-Caches. Diese Pfade sind für die fachliche Navigation wenig hilfreich, verbrauchen aber viel Antwortbudget und können sich mit einer neuen Generation ändern.

### Was besser sein soll

Der Server sollte einen kurzen, stabilen Assembly-relativen Pfad als Standard liefern und den absoluten Cache-Pfad nur optional ausgeben. Alle Ergebnisse sollten zusätzlich eine generationsunabhängige logische Datei-ID oder einen stabilen relativen Pfad enthalten.

## 12. Fehlende Assembly-spezifische Toolgrenzen deutlicher dokumentieren

### Was versucht wurde

Mehrere projektbezogene Werkzeuge wurden testweise mit `targetType='assembly'` verwendet.

### Ergebnis

Einige Werkzeuge lehnten das Ziel korrekt ab, obwohl die allgemeine MCP-Navigation für Assemblys ähnliche Anwendungsfälle nahelegt. Dadurch entstehen zusätzliche Fehlerrunden, bevor der Agent die tatsächlich unterstützte Assembly-Capability-Matrix kennt.

### Was besser sein soll

Die Toolbeschreibung und ein maschinenlesbares Capability-Dokument sollten pro Werkzeug klar ausweisen:

- unterstützte Zieltypen;
- verfügbare Daten bei `origin=decompiled`;
- nicht anwendbare Projektfunktionen;
- mögliche Trunkierungs- und Referenzgrenzen;
- empfohlene Ersatzwerkzeuge.

## 13. Source-Backed-Analyse darf nicht unbemerkt auf Dekompilation zurückfallen

### Was versucht wurde

Für eine zweite externe DLL wurde die Analyse mit der Erwartung gestartet, dass der hinterlegte Git-Quellcode heruntergeladen, verifiziert und anschließend von `inspect_assembly`, `find_symbol` und `get_symbol_body` verwendet wird.

### Ergebnis

Der Repository-Checkout konnte laut MCP-Diagnose nicht sauber verifiziert werden. Zusätzlich schlug die Bereinigung des Checkouts fehl. Der Server wechselte deshalb auf `fallbackReason: provider-unavailable` und analysierte eine dekompilierte Ersatz-Compilation. Auch die nachgelagerte Symbolsuche und der Methodenrumpf verwiesen auf generierte Cache-Dateien statt auf den Original-Quellcode.

### Was besser sein soll

Das Tool sollte für source-kritische Anfragen einen Modus wie `sourceRequired: true` anbieten. Wenn der Git-Download, die Integritätsprüfung oder das Mapping fehlschlägt, sollte die Anfrage kontrolliert mit einem eindeutigen Fehler enden, statt automatisch eine fachlich andersartige Datenquelle zu verwenden. Ein Fallback darf nur nach expliziter Zustimmung erfolgen.

## 14. Checkout-Verifikation und Bereinigung belastbar machen

### Was versucht wurde

Der Health-Status und die Assembly-Inspektion wurden nach dem Source-Checkout-Versuch abgefragt, um den Zustand des temporären Repositorys und die verwendete Quelle zu verifizieren.

### Ergebnis

Die Diagnose meldete sowohl `external-source-repository-checkout-unverified` als auch `external-source-repository-cleanup-failed`. Der Zustand des Checkouts blieb damit nicht vertrauenswürdig und die Lebensdauer beziehungsweise Bereinigung des temporären Arbeitsbereichs war nicht nachweisbar abgeschlossen.

### Was besser sein soll

Der Server sollte Checkout-Operationen mit einem expliziten Zustandsautomaten und nachvollziehbaren Ergebnissen versehen: Download gestartet, Integrität geprüft, Revision festgelegt, Mapping aktiviert, Bereinigung abgeschlossen oder Bereinigung fehlgeschlagen. Für die Bereinigung sollten Retry-/Quarantäne-Mechanismen, TTL und eine sichere Wiederaufnahme existieren. Verwaiste Arbeitsbereiche müssen sichtbar und automatisch begrenzt werden.

## 15. Source-Mapping als überprüfbaren Vertrag ausgeben

### Was versucht wurde

Nach der Analyse wurden Herkunft, Source-Status, Cache-Pfad und Diagnosezusammenfassung aus den MCP-Antworten verglichen.

### Ergebnis

Die Antwort enthielt keinen ausreichend detaillierten Nachweis darüber, welches Repository, welche Revision, welches Mapping und welche Verifikationsmethode für die erwartete Originalquelle verwendet werden sollten. Es war daher nicht möglich, aus dem Toolergebnis allein einen erfolgreichen Git-Download und eine aktivierte Source-Zuordnung nachzuweisen.

### Was besser sein soll

Jede source-backed Antwort sollte strukturiert folgende Angaben liefern:

- Source-Provider;
- Repository-Referenz als sicherer, nicht unnötig offengelegter Identifier;
- exakte Revision oder Commit-ID;
- Mapping-Regel und betroffene Dateien;
- Verifikationsstatus;
- verwendeter Source-Pfad;
- Fallback-Status und Fallback-Grund.

## 16. Provenienz über alle Folgewerkzeuge durchsetzen

### Was versucht wurde

Nach dem fehlgeschlagenen Checkout wurden Symbolsuche und Body-Abfrage auf einem gefundenen Einstiegssymbol ausgeführt.

### Ergebnis

Die Folgewerkzeuge übernahmen den dekompilierten Analysekontext. Der Assembly-Header meldete weiterhin `origin: decompiled`, während der Body zusätzlich als `contentMode: source` bezeichnet wurde. Damit kann ein nachgelagerter Agent die Quelle als Original interpretieren, obwohl die Source-Backed-Anforderung nicht erfüllt war.

### Was besser sein soll

Jedes Folgewerkzeug muss die Source-Policy erneut prüfen und Herkunft sowie Vollständigkeit unverändert weiterreichen. Bei `sourceRequired: true` müssen dekompilierte Inhalte blockiert werden. Die Bezeichnung `source` darf ausschließlich für nachweislich zugeordnete Originalquellen verwendet werden.

## 17. Git- und Analysephase als eine atomare Operation anbieten

### Was versucht wurde

Download-/Checkout-Diagnose und anschließende Roslyn-Abfragen wurden getrennt betrachtet.

### Ergebnis

Es gab keine einzelne MCP-Antwort, die beweist, dass zuerst eine bestimmte Revision erfolgreich ausgecheckt, anschließend geladen und danach tatsächlich als Quelle für alle Analysewerkzeuge verwendet wurde. Der Agent musste mehrere Antworten manuell korrelieren.

### Was besser sein soll

Für externe DLLs sollte ein expliziter Source-Backed-Analyseauftrag verfügbar sein. Dieser sollte Download, Verifikation, Mapping, Analyse und Bereinigung als zusammengehörige Operation ausführen und ein unveränderliches Analyseprotokoll mit Source-Status, Revision, Generation und Ergebnisquelle zurückgeben.
