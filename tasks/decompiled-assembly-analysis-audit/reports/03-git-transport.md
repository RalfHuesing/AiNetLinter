# Linse 03 — Git-Transport, Prozesse, Timeout, Cancellation und Redaction

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `725ccd1e`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: projektgebundene Abfragen mit `targetType=project`, `targetPath=<repo-root-redacted>`. Prozessargumente, Credentials, URLs und lokale Pfade werden nicht in diesem Report wiedergegeben.

## Abdeckung

Geprüft wurden `ExternalSourceGitProcessExecutor`, `ExternalSourceGitProcessLauncher`, der provider-spezifische Git-Transport, die Fehlerklassifikation und die Tests für echte lokale Child-Prozesse. Bewertet wurden Argumentübergabe, Shell-Isolation, Umgebungsvariablen, Output-Caps, Exitcodes, Timeout, Cancellation, Prozessbaum, Cleanup und sichere Fehlerprojektion.

## Befundlage

Es wurde kein bestätigter S0–S2-Defekt gefunden.

`ExternalSourceGitProcessExecutor.cs:15-20` definiert eine begrenzte Output-Capture-Größe und eine feste Cleanup-Grenze. `:39-65` koppelt Timeout- und Caller-Cancellation an einen gemeinsamen Ablauf; `:97-185` unterscheidet Cancellation, Timeout und Primärfehler und versucht jeweils die Prozessbereinigung. `:208-250` liest weiter bis zum Streamende, begrenzt beide Ausgaben und markiert Trunkierung. `:253-280` beendet Prozessbaum, Streams und Reader innerhalb einer separaten Cleanup-Grenze.

`ExternalSourceGitProcessExecutor.cs:382-405` startet ohne Shell, mit getrennten Standardstreams und `ArgumentList`; `:408-420` entfernt geerbte `GIT_*`-Variablen, bevor explizite Transportvariablen gesetzt werden. Der Git-Transport setzt Prompt-Deaktivierung, isolierte Git-Konfiguration und einen Child-Process-Credential-Helper, ohne Credentials in die Repository-URL zu schreiben. Revisionen werden in `GiteaGitRepositoryTransport.cs:448-466` auf exakt 40 oder 64 Hexzeichen begrenzt.

Die Fehlerpfade projizieren Transportausgaben über `ExternalSourceRepositoryFailurePolicy` in typisierte, generische Diagnosen. Die raw Prozessausgaben bleiben interne Transportdaten; sie werden nicht als Providerfehlertext an die MCP-Antwort weitergereicht.

Die Integrationstests `ExternalSourceGitProcessExecutorTests` decken realen StartInfo-Aufbau, Output-Begrenzung, Timeout mit Child/Grandchild, Cancellation mit Child/Grandchild, Startfehler, wiederaufgenommenen Parent-Prozess, sichtbaren Tree-Close-Fehler und nicht darstellbare Timeouts ab. Die Transporttests decken Erfolg, Head-Revision, Credential-Isolation, typed failures, Timeout, Cancellation, Secret-Nichtoffenlegung und Teilcheckout-Cleanup ab.

## Abdeckungsgrenze GIT-001

- Typ: externe Voraussetzung, kein bestätigter Produktdefekt
- Schweregrad: S3
- Umfang: U3 — echter Netzwerk-/Remote-Git-Transport
- Konfidenz: hoch
- Evidenz: Prozess- und Transportverträge sind durch lokale echte Child-Prozesse und Test-Doubles abgedeckt; ein echter Remote-Clone/Fetch wurde in diesem Audit nicht ausgelöst.
- Auswirkung: Verhalten gegenüber einem realen Server bei Authentifizierung, Netzwerktrennung und Remote-Fehlertexten ist nur durch die Klassifikations- und Transporttests, nicht durch einen Live-E2E-Lauf belegt.
- Reproduktion: In einer kontrollierten Umgebung einen erlaubten Test-Remote mit kurzlebigen Credentials und freigegebenem Staging-Root verwenden; Erfolg, Exitcodefehler, Timeout, Cancellation, Revisionsermittlung und Cleanup getrennt beobachten.
- Disposition: Abdeckungsgrenze dokumentiert; keine Netzwerkzugriffe und keine Credential-Nutzung im Audit erzwungen.

## Nebenbeobachtung

`find_magic_values` meldete einen niedrig-riskanten lokalisierten User-Message-Kandidaten im Process-Launcher. Das ist kein Transport- oder Secret-Befund und wurde wegen Audit-only nicht geändert.
