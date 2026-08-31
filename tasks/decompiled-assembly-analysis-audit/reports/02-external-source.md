# Audit-Report 02 – External-Source-Konfiguration und Credential-Semantik

## Linse, Scope und Revision

- **Linse:** 2 – External-Source-Konfiguration, Mapping-Auswahl und Mehrdeutigkeiten, Provider-Auswahl/-Verfügbarkeit sowie Credential-Semantik.
- **Rolle:** unabhängiger, read-only Reviewer; keine Produkt-, Test-, Konfigurations- oder Dokumentationsänderung.
- **Geprüfter Scope:**
  - `src/AiNetLinter/Configuration/` für External-Source-Settings, Mapping-Schema, URL-/Pfadvalidierung und strukturierte Diagnosen;
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/` für Host-Komposition, Provider-Auswahl und Mapping-Auflösung;
  - `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` für Provider-Vertrag, Credential-Auflösung, Repository-Akquisition und Laufzeit-URL-Policy;
  - zugehörige Fast- und Integration-Tests sowie die Konfigurationsdokumentation.
- **Verwendete Revision:** `8a9fbddaeba6fff26c4c6f8d3ab2d3f87e7c2193`.
- **Working Tree:** vor Erstellung dieses Reports sauber; es waren keine relevanten Source-/Teständerungen vorhanden.
- **Code-Map:** Für diese Linse unverändert korrekt. Die dort genannten Analyse-, External-Source-, Konfigurations- und Testbereiche waren die tatsächlichen Navigationspunkte. Die offenen Formulierungen „gegen Working Tree und MCP zu verifizieren“ sind nach dieser Prüfung weiterhin sachgerecht; konkrete veraltete Navigationsfakten wurden nicht gefunden. `code-map.md` blieb unverändert.

## Nicht geprüfte Bereiche und Abdeckungsgrenzen

- Kein Zugriff auf eine echte externe Repository-Quelle, keinen Netzwerk-/Credential-Test und keine Prüfung eines geschützten Dienstkontos.
- Keine Live-Ausführung des MCP-/Daemon-Pfads gegen eine externe Quelle.
- Keine Assembly-Inspektion: Für diese Linse lag keine neutrale, lokale Assembly als sicherer Live-Testgegenstand vor; die statische Mapping-/Provider-Semantik wurde dennoch vollständig im Projektindex geprüft.
- Keine Prüfung von Prozessbaum-, Checkout-, Snapshot-, Reparse-Point- oder MCP-Token-Details außer dort, wo sie für Credential- und Provider-Grenzen unmittelbar relevant waren. Diese Themen gehören primär zu anderen Linsen.
- Die Abwesenheit einer produktiven Credential-Resolver-Implementierung wurde semantisch und ergänzend textuell geprüft; ein negatives End-to-End-Ergebnis gegen einen geschützten Remote ist daher eine Abdeckungsgrenze, kein zusätzlich behaupteter Live-Befund.

## Executive Summary

### Befunde

1. **EXTSRC-01 – Konfigurationsvalidator und Laufzeit-URL-Policy sind nicht deckungsgleich.** Der Loader akzeptiert URL-Varianten, die die Akquisitionsschicht später unmittelbar als ungültig verwirft. Dadurch wird eine semantisch unbrauchbare Mapping-Konfiguration zunächst als erfolgreich geladen dargestellt.
2. **EXTSRC-02 – Im produktiven MCP-/Daemon-Einstieg ist kein Credential-Resolver angeschlossen.** Die interne Resolver-Schnittstelle ist vorhanden und der Transport kann Credentials sicher verwenden, aber die realen Einstiegspunkte übergeben keinen Resolver. Bei einer geschützten Quelle bleibt deshalb nur der prompt-freie, credential-lose Pfad.

Beide Befunde sind **S2**, nicht S0/S1: Die Fehlerpfade sind fail-closed und redigieren sensible Werte, aber relevante External-Source-Szenarien bleiben inkonsistent bzw. nicht nutzbar.

### Bestätigte Erwartungen

- Das Mapping-Schema ist streng: unbekannte oder doppelte Felder führen zu strukturierten Fehlern; Repository-, Solution- und Assembly-Angaben werden vor Verwendung validiert.
- Assembly-Aliase werden trim-/suffix-normalisiert und für Duplikat- bzw. Mapping-Prüfungen ohne Beachtung der Groß-/Kleinschreibung verglichen.
- Die Laufzeit-URL-Policy verwirft Userinfo, Query, Fragment, fehlenden Host und Nicht-HTTP(S) und normalisiert die verbleibende URL vor Transport-/Cache-Verwendung.
- Die Akquisition verwirft ungültige URL-Mappings vor dem Transport. Provider- und Transportfehler werden in typisierte, sichere Diagnosen überführt.
- Der Transport deaktiviert interaktive Prompts standardmäßig; Credentials werden nicht in Transportargumente geschrieben und in den geprüften Fehlerpfaden nicht in Diagnosen projiziert.
- Die Standard-Komposition wählt deterministisch einen statischen Provider-Aufbau. Ein testbarer „unavailable“-Provider kann einen sicheren typisierten Fehler liefern; eine dynamische, benutzerkonfigurierbare Mehrprovider-Auswahl wurde nicht als Vertragsbestandteil belegt.

### Abdeckungsgrenzen

Die URL-Divergenz wurde aus direkt sichtbaren Validator-/Runtime-Pfaden und vorhandenen Tests abgeleitet; die drei Varianten Userinfo, Query und Fragment sind nicht jeweils als Loader-Test abgedeckt. Die Credential-Lücke ist strukturell hoch sicher, ihr Verhalten gegen einen real geschützten Remote wurde mangels zulässiger externer Zugangsdaten nicht live reproduziert. Ein fehlender Provider bzw. eine fehlende externe Prozessinstallation wurde nicht als eigener Befund bewertet, weil die konkrete Fehlerklassifikation in die VCS-/Prozesslinse fällt.

## Befund EXTSRC-01

### Metadaten

- **Stabile ID:** `EXTSRC-01`
- **Titel:** Konfigurationsvalidator und Laufzeit-URL-Policy sind nicht deckungsgleich
- **Komponente:** External-Source-Mapping-Loader, URL-Validierung und Repository-Akquisition
- **Schweregrad:** S2 – relevante Fehlkonfiguration wird zu spät und in einem anderen Zustandsmodell sichtbar; kein nachgewiesener Secret-Leak.
- **Umfang:** U2 – begrenzte, aber pipelineübergreifende External-Source-Konfiguration/Akquisition.
- **Beweissicherheit:** hoch für die Codepfad-Divergenz; mittel für die konkrete Nutzerwirkung im vollständigen Orchestratorpfad.
- **Umgebungsabhängigkeit:** keine Netzwerkabhängigkeit für die strukturelle Reproduktion; ausgelöst nur durch URL-Formen mit Userinfo, Query oder Fragment.

### Erwartetes Verhalten

Eine Mapping-Konfiguration sollte an der Ladegrenze genau dieselbe kanonische URL-Regel anwenden wie die Akquisition. Eine URL, die der Laufzeitpfad sicherheits- oder transportbedingt ablehnt, sollte nicht vorher als erfolgreich geladene Konfiguration erscheinen. Insbesondere sollten Credentials nicht als URL-Bestandteil akzeptiert werden.

### Beobachtetes Verhalten

- `ExternalSourceMappingValidator.NormalizeUrl` prüft nur absolute URL, vorhandenen Host und HTTP(S); die Originalzeichenfolge wird zurückgegeben (`src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:204-219`). Userinfo, Query und Fragment werden dort nicht abgelehnt.
- `ExternalSourceRepositoryUrlPolicy.TryNormalize` lehnt dieselben Felder ausdrücklich ab und gibt nur die kanonische `AbsoluteUri` zurück (`src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryFailurePolicy.cs:406-428`).
- `ExternalSourceRepositoryAcquirer.TryValidateMapping` ruft diese Laufzeit-Policy erst beim Akquisitionsversuch auf und erzeugt dann `RepositoryMappingInvalid`, bevor der Transport aufgerufen wird (`src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs:299-311`).

Damit kann der Loader eine Mapping-Datei als erfolgreich laden, während die erste tatsächliche Source-Auflösung dasselbe Mapping als ungültig und nicht verwendbar behandelt. Das ist eine bestätigte Konsistenzlücke; eine externe Verbindung ist dafür nicht erforderlich.

### Auswirkung

Die Konfigurationsdiagnose ist zeitlich und semantisch verspätet: Ein Nutzer kann zunächst einen erfolgreichen Settings-Ladevorgang sehen und erst bei der Assembly-Auflösung einen Provider-/Fallback-Fehler erhalten. Bei URL-Userinfo bleibt ein sensibler Wert zudem länger im Mapping-Objekt erhalten, obwohl er später vor dem Transport verworfen wird. Die geprüften Projektionen redigieren den Wert; ein direkter Secret-Leak wurde nicht festgestellt.

### Konkrete Reproduktion

1. In der bestehenden Loader-Testform eine gültige Mapping-Datei mit genau einem Repository-Eintrag, gültigem relativem Solution-Pfad und einem Assembly-Alias erstellen.
2. Für `url` nacheinander eine redigierte HTTPS-URL mit (a) Query, (b) Fragment oder (c) Userinfo-Muster `<user>:<secret>@<host>` verwenden.
3. `ExternalSourceConfigurationLoader.Load(...)` aufrufen. **Beobachtet:** Die URL-Prüfung in `NormalizeUrl` erzeugt für diese Formen keinen Fehler; bei ansonsten gültigen Feldern ist das Load-Ergebnis erfolgreich.
4. Das geladene Mapping an `ExternalSourceRepositoryAcquirer.AcquireAsync(...)` übergeben. **Beobachtet:** Ergebnis nicht erfolgreich, Diagnosecode `RepositoryMappingInvalid`, Transport-Aufrufzahl `0`.

Die Laufzeit-Hälfte ist durch den bestehenden Test `<ExternalSourceRepositoryAcquirerTests>.AcquireAsync_RejectsNonCanonicalRepositoryUrlBeforeTransport` abgedeckt. Die bestehende Loader-Prüfung `<ExternalSourceConfigurationLoaderTests>.Load_UngueltigeUrlOderSolutionPath_LiefertStabileDiagnose` deckt allgemeine ungültige URL-/Pfadfälle ab, aber nicht diese drei akzeptierten URL-Varianten.

### Belege

| Beleg | Redigierte Parameter/Felder | Ergebnis und Begründung |
|---|---|---|
| AiNetLinter-MCP `get_symbol_body` für `ExternalSourceMappingValidator.NormalizeUrl` | `targetType="project"`, `targetPath="<project-root>"`, `symbolIdentifier="<mapping-validator>.NormalizeUrl"` | Direkter Methodenkörper zeigt die unvollständige Konfigurationsprüfung; Rückgabe ist der Originalwert. |
| AiNetLinter-MCP `get_feature_context` für `ExternalSourceRepositoryUrlPolicy.TryNormalize` | `targetType="project"`, `targetPath="<project-root>"`, `includeCallers=true`, `includeTests=true` | MCP meldet Deklaration an `...ExternalSourceRepositoryFailurePolicy.cs:406-428` und direkte Aufrufer in Transport, Akquisition und Cache. Die Policy lehnt Userinfo/Query/Fragment ab. |
| AiNetLinter-MCP `get_symbol_body` für `ExternalSourceRepositoryAcquirer.AcquireAsync` und `TryValidateMapping` | `targetType="project"`, `targetPath="<project-root>"`, `maxBodyLines=<bounded>` | Die Validierung erfolgt vor Reservation und Transport; die Diagnose ist ein Akquisitionsfehler, nicht ein Loader-Fehler. |
| Bestehende Fast-Tests | `<ExternalSourceRepositoryAcquirerTests>.AcquireAsync_RejectsMappingWithCredentialsWithoutExposingUrl()`; `<ExternalSourceRepositoryAcquirerTests>.AcquireAsync_RejectsNonCanonicalRepositoryUrlBeforeTransport()` | Bestätigen: credential-/nicht-kanonische URL wird vor Transport abgewiesen und nicht in Diagnosen exponiert. |
| Lokale Gegenprüfung | `dotnet test src/AiNetLinter.FastTests --no-restore --filter "FullyQualifiedName~ExternalSourceConfigurationLoaderTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~<provider-tests>"` | Der entsprechende redigierte External-Source-Testfilter war grün: 124 bestanden, 1 übersprungen. Der Loader-Testbestand enthält keine gezielte Abdeckung für Query/Fragment/Userinfo. |

### Nicht umgesetzte Remediation-Hypothese

Die URL-Prüfung sollte auf eine einzige, von Loader und Laufzeit gemeinsam verwendete Policy reduziert werden. Die Konfigurationsgrenze sollte dabei früh einen strukturierten Fehler liefern; Credentials sollten ausschließlich über einen sicheren Resolver und nie über Mapping-URLs transportiert werden. Dies ist nur eine Hypothese, keine im Audit umgesetzte Änderung.

## Befund EXTSRC-02

### Metadaten

- **Stabile ID:** `EXTSRC-02`
- **Titel:** Kein produktiver Credential-Resolver im tatsächlichen MCP-/Daemon-Einstieg
- **Komponente:** Host-Komposition, Standard-Provider-Fabrik, Credential-Resolver-Vertrag und VCS-Transport
- **Schweregrad:** S2 – geschützte External-Source-Szenarien sind über die realen Einstiegspunkte nicht anschließbar; öffentliche Quellen bleiben funktionsfähig.
- **Umfang:** U3 – betrifft beide produktiven Einstiegspunkte und den gesamten Standard-Providerpfad.
- **Beweissicherheit:** hoch für die fehlende Verdrahtung; mittel für das End-to-End-Verhalten an einem real geschützten Remote.
- **Umgebungsabhängigkeit:** nur bei einer Quelle mit Authentifizierungsanforderung relevant; kein Netzwerk- oder Credential-Zugriff für die strukturelle Reproduktion erforderlich.

### Erwartetes Verhalten

Wenn geschützte externe Quellen zur beabsichtigten Funktion gehören, muss der produktive Einstieg einen sicheren Credential-Resolver bereitstellen oder die Authentifizierung explizit als nicht unterstützt und früh erkennbar ausweisen. Ein vorhandener interner Resolver-Vertrag allein genügt nicht, wenn die realen MCP-/Daemon-Einstiege ihn nie befüllen.

### Beobachtetes Verhalten

- `IExternalSourceCredentialResolver` ist eine interne Schnittstelle; `ExternalSourceCredential` validiert und verwaltet Username/Secret und löscht das Secret beim Dispose (`src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/IExternalSourceCredentialResolver.cs:10-49`).
- `AssemblyAnalysisHostComposition.Create` akzeptiert den Resolver optional (`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs:73-82`). Die Standardfabrik reicht ihn nur weiter, wenn ein Aufrufer ihn liefert, und erstellt danach den statischen Standard-Provider (`.../AssemblyAnalysisHostComposition.cs:192-196`, `:222-242`).
- Der produktive MCP-Einstieg ruft `AssemblyAnalysisHostComposition.Create` nur mit Ressourcenüberschreibungen auf und übergibt keinen Resolver (`src/AiNetLinter/Commands/McpServerCommand.cs:57-63`). Der Daemon-Einstieg zeigt dasselbe Muster (`src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs:42-48`).
- Der Transport erhält dadurch standardmäßig `null`; seine Resolver-Auflösung liefert dann `null`. Die Prozessumgebung deaktiviert interaktive Prompts und setzt ohne Credential keine Credential-Variablen (`<provider-transport>.cs:227-237`, `:409-427`).
- Die MCP-Referenzabfrage für `IExternalSourceCredentialResolver` ergab sechs Verwendungen: drei in der Host-/Transport-Implementierung und drei in Testcode; eine produktive Resolver-Implementierung oder Konfigurationsquelle war nicht darunter. Ergänzend bestätigt die textuelle Suche nach `IExternalSourceCredentialResolver`/`ExternalSourceCredential(`, dass unter `src/AiNetLinter` kein konkreter Resolver bereitgestellt wird.

Der sichere Fehlerpfad ist vorhanden: Ein Authentifizierungsfehler ohne Credential wird typisiert als `AuthenticationRequired`; es wird nicht versucht, interaktiv nachzufragen. Das verhindert Hängen und Secret-Leaks, löst aber die fehlende produktive Credential-Anbindung nicht.

### Auswirkung

Eine geschützte Quelle kann über den realen MCP-/Daemon-Standardpfad nicht mit Credentials akquiriert werden. Das Ergebnis ist ein sicherer Provider-/Akquisitionsfehler und anschließend der vorgesehene Fallback, nicht die erwartete Source-Nutzung. Öffentliche Quellen und bereits verfügbare, gültige Cache-Inhalte sind davon nicht zwingend betroffen. Ob geschützte Quellen laut finalem Produktvertrag zwingend unterstützt werden sollen, ist in der Konfiguration/Dokumentation nicht explizit festgelegt; deshalb S2 statt S1.

### Konkrete Reproduktion

1. Den produktiven MCP- oder Daemon-Einstieg mit einer ansonsten gültigen External-Source-Konfiguration starten; keine Zugangsdaten in die Mapping-Datei eintragen.
2. Die Standard-Komposition verwenden. Nach dem gezeigten Call-Site-Code ist `credentialResolver == null`.
3. Eine Quelle verwenden, die eine Authentifizierung verlangt, oder in einem bestehenden Transport-Fake den Authentifizierungsfehler ohne Credential zurückgeben.
4. **Beobachtet/aus Code abgeleitet:** Resolver-Auflösung liefert `null`, der VCS-Prompt ist deaktiviert, der Fehler wird als `AuthenticationRequired` projiziert und es entsteht kein credential-behafteter Transportaufruf.

Der Fake-/Transportanteil ist ohne Netzwerk durch vorhandene Tests reproduzierbar. Die produktiven Call-Sites selbst wurden statisch per MCP und ergänzend per PowerShell verifiziert; ein echter geschützter Remote wurde wegen fehlender zulässiger Zugangsdaten nicht verwendet.

### Belege

| Beleg | Redigierte Parameter/Felder | Ergebnis und Begründung |
|---|---|---|
| AiNetLinter-MCP `find_references` für `IExternalSourceCredentialResolver` | `targetType="project"`, `targetPath="<project-root>"`, `symbolIdentifier="<credential-resolver-interface>"`, `maxResults=100` | Vollständiger Scope: sechs Fundstellen; keine konkrete produktive Resolver-Implementierung. Testverwendungen bleiben sichtbar und werden nicht als Produktverdrahtung gewertet. |
| AiNetLinter-MCP `get_feature_context` für `AssemblyAnalysisHostComposition.Create` | `targetType="project"`, `targetPath="<project-root>"`, `includeCallers=true`, `includeTests=true` | MCP listet die produktiven Aufrufer in MCP- und Daemon-Command sowie die optionale Resolver-Parameterposition. |
| Quelltext | `AssemblyAnalysisHostComposition.cs:192-196`, `:222-242`; `McpServerCommand.cs:57-63`; `DaemonHostCommand.cs:42-48` | Standardfabrik baut den Provider aus dem optionalen Resolver; beide produktiven Call-Sites lassen ihn leer. |
| Quelltext, redigierter Provider-Transport | `<provider-transport>.cs:227-237`, `:409-427` | `null`-Resolver führt zu keiner Credential-Auflösung; Prompt und geerbte Credential-Mechanismen sind deaktiviert. |
| Bestehende Fast-Tests | `<ProviderTransportTests>.CloneDefaultBranchAsync_WithoutResolverLeavesPublicClonePromptFree()`; `<ProviderTransportTests>.CloneDefaultBranchAsync_MapsExitOutputToTypedFailure()` | Bestätigen den prompt-freien Null-Resolver-Vertrag und die typisierte Authentifizierungsdiagnose ohne Credential. |
| Lokale Gegenprüfung | `dotnet test src/AiNetLinter.FastTests --no-restore --filter "FullyQualifiedName~ExternalSourceProviderContractTests|FullyQualifiedName~<provider-transport-tests>|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests"` | Relevanter Fast-Testfilter war grün; zusätzlich liefen gezielte Integrationstests für Prozess-/Snapshot-Grenzen grün. Keine externe Quelle wurde kontaktiert. |

### Nicht umgesetzte Remediation-Hypothese

Entweder muss ein sicherer Resolver am tatsächlichen MCP-/Daemon-Einstieg injizierbar und dokumentiert werden, oder die öffentliche Konfiguration sollte geschützte Quellen ausdrücklich als nicht unterstützt melden. In beiden Fällen dürfen Credentials nicht in Mapping-URLs, CLI-Argumenten, Logs oder Diagnosen gelangen.

## Provider-Auswahl, Verfügbarkeit und Mapping-Mehrdeutigkeiten

Die geprüfte Standard-Komposition erstellt genau einen statischen Provider-Aufbau; eine URL-basierte oder konfigurierbare Provider-Auswahl ist nicht sichtbar. Das ist als aktuelle Architekturbeobachtung bestätigt, aber mangels explizitem Mehrprovider-Akzeptanzkriterium kein eigenständiger Befund. Der Test-Provider für „unavailable“ liefert einen typisierten Warnzustand ohne Snapshot; die Produktionsauswahl dieses Testdoubles wurde nicht gefunden. Eine fehlende externe Prozessinstallation bzw. deren genaue Diagnoseklassifikation wurde nicht erneut als Provider-Befund aufgenommen, um keine Überschneidung mit der VCS-/Prozesslinse zu erzeugen.

Die Mapping-Auswahl selbst vergleicht Assembly-Aliase case-insensitiv. Der Konfigurationsvalidator führt ein Repository-übergreifendes Owner-Set und erzeugt bei doppelten Aliasen eine Ambiguous-Diagnose. Der Resolver wählt anschließend nur bei genau einem Mapping; bei null oder mehreren Treffern bleibt er im Fallback. Für die JSON-Konfiguration wurde kein bestätigter Mehrdeutigkeitsfehler gefunden.

## Cross-Lens-Überschneidungen

| Überschneidung | Abgrenzung dieses Reports |
|---|---|
| Statische Assembly-Analyse und Source-Fallback | Dieser Report bewertet nur, warum ein Mapping/Provider vor der Source-Nutzung nicht verfügbar wird; die Qualität der Decompilation und der eigentliche Fallback gehören zur Assembly-Linse. |
| VCS-Prozess, Prompt, Timeout und Exit-Code | EXTSRC-02 nutzt nur den belegten Null-Resolver-/Prompt-Vertrag. Prozessbaum, Timeout-Rennen und vollständige Exit-Code-Matrix bleiben der Prozesslinse. |
| Checkout, Cache und Snapshot-Trust | Cache-Key- und Cleanup-Semantik wurden nur insoweit berücksichtigt, wie URL-Normalisierung und Provider-Verfügbarkeit betroffen sind. |
| Tool-Antworten und Diagnose-Redaktion | Die sichere Projektion ohne Secret wurde als bestätigte Erwartung aufgenommen; Antwortbudget und Wire-Text sind nicht Gegenstand dieses Reports. |
| Dokumentation und Konfigurationsvertrag | Das Fehlen eines dokumentierten Credential-Feldes bzw. Resolver-Einstiegs unterstützt EXTSRC-02; eine eigenständige Dokumentationsbewertung bleibt der Dokumentationslinse vorbehalten. |

## Coverage-/Limitations-Tabelle

| Bereich | Nachweis | Status | Grenze |
|---|---|---|---|
| Settings-/Mapping-Schema | MCP-Symbolkörper, Konfigurations-Tests, strukturierte Diagnosen | hoch abgedeckt | Keine neuen Tests angelegt; nur bestehende Testfälle ausgeführt. |
| URL-Konfiguration vs. Laufzeitpolicy | MCP-Feature-Kontext und direkte Methodenkörper | Befund EXTSRC-01 | Query/Fragment/Userinfo nicht jeweils im Loader-Testbestand; keine Remoteverbindung. |
| Alias-Normalisierung und Mehrdeutigkeit | Mapping-Validator und Source-Match-Resolver | bestätigt, kein Befund | Keine absichtlich programmatisch injizierte Mehrfachkonfiguration im Live-Orchestrator. |
| Provider-Auswahl/-Verfügbarkeit | Host-Fabrik, Provider-Vertrag und Testdouble | teilweise abgedeckt | Kein expliziter Mehrprovider-Vertrag; keine fehlende Installation/Remoteverfügbarkeit live geprüft. |
| Credential-Semantik | MCP-Referenzen, Host-Call-Sites, Transporttests | Befund EXTSRC-02 | Keine produktive Resolver-Implementierung gefunden; geschützter Remote nicht live getestet. |
| Redigierung und Prompt-Verhalten | Bestehende Transport-/Provider-Tests | bestätigt | Vollständiger MCP-Wire-Text außerhalb dieser Linse. |
| Code-Map-Navigation | `get_file_tree`, `get_index_scope`, MCP-Symbolabfragen | korrekt, unverändert | Keine konkrete veraltete Zeile gefunden; daher keine Änderung an `code-map.md`. |

## Review-Verdikt

**approved mit zwei S2-Befunden; kein S0/S1-Befund.** Die geprüften Schutz- und Fail-Closed-Verträge sind überwiegend vorhanden. Die URL-Policy sollte an der Konfigurationsgrenze konsistent werden, und die Credential-Unterstützung benötigt entweder eine reale sichere Verdrahtung oder eine explizite, früh sichtbare Public-Only-Grenze.
