# Source-backed-Regressionsprobe

Datum: 2026-08-31
Status: abgeschlossen; ausschließlich Live-MCP-Proben, keine Produktions- oder
Testcodeänderung, kein Build und kein Testlauf.

## Rahmen

Die lokale externe Source-Konfiguration enthält eine Zuordnung für die
`repo-provided assembly` sowie einen zweiten deklarierten Output. Die zugehörigen
lokalen Quelldatei- und Checkout-Artefakte waren vor den Proben vorhanden. Namen,
Pfade, Repository- und Provider-Identifikatoren wurden nur lokal zur
Target-Auflösung verwendet und erscheinen hier nicht.

Alle Assembly-Aufrufe verwendeten `targetType=assembly`, einen absoluten lokalen
Pfad und metadata-only-Limits `maxResults=1`, `maxMembers=1`. Antwortgrößen sind
gerundete Textzeichen; gekürzte Hashes sind nur Sitzungslabels.

## Probe-Matrix

| Probe | Tool / anonymisierte Parameter | Status, Herkunft und Sitzungsmarker | Completeness, Diagnostics, Truncation | Größe / agentische Nutzbarkeit / Evidenz | Ergebnis |
|---|---|---|---|---|---|
| SB-P01 | `inspect_assembly`; `repo-provided assembly`, kleine metadata-only-Probe | Erfolg; `origin=decompiled`, `sourcePath=none`, `snapshot=none`, `generation=2`, `hash-1`, `confidence=medium`, `trust=untrusted`, `status=partial` | `partial`; 167 Diagnosen, 1 Probe gezeigt, root/transitiv 88/79, alle Diagnose-/Referenzlisten gekürzt | ca. 18,7k; Fallback und Unsicherheit klar erkennbar, aber für eine kleine Probe nicht kompakt | Konfigurierte Source-Zuordnung führte nicht zu source-backed. |
| SB-P02 | `find_symbol`; `repo-provided assembly`, `Type_1`, Klasse, Limit 1, keine Referenzen | Erfolg; erneut `decompiled`, `sourcePath=none`, `snapshot=none`, `generation=2`, `status=partial` | `partial`; kein zusätzliches Truncation-/Diagnosefeld im kleinen Symbolslice | ca. 0,6k; die Fallback-Herkunft wird auch für Navigation durchgereicht | Kein source-backed Gegenbeleg. |
| SB-P03 | `get_file_skeleton`; `repo-provided assembly`, aus SB-P01 abgeleitetes `File_1` | Erfolg; Wire-Text verweist auf dekompilierte Session, `sourcePath=none`, `snapshot=none` | keine strukturierte Completeness-/Diagnoseprojektion | ca. 2,9k; Skeleton ist lesbar, aber die Source-/Snapshot-Lebensdauer nicht maschinenlesbar | Kein source-backed Gegenbeleg. |
| SB-P04 | `get_server_health`; `repo-provided assembly`, `includeDiagnostics=true`, Limit 1 | Erfolg; eine Assembly-Session, `loadState=partial`, `origin=decompiled`, kein `sourcePath`, `generation=2`, `hash-1`, `confidence=medium`, `trust=untrusted` | `partial`; 167/1 Diagnosen, Truncation sichtbar | ca. 1,0k; Fallback, Zustand und begrenzte Diagnosen klar; kein Provider-/Lease-/Akquisitionszustand | Kein source-backed Gegenbeleg. |
| SB-P05 | identische kleine `inspect_assembly`-Probe nach kurzer Folge | Erfolg; unverändert `decompiled`, `sourcePath=none`, `snapshot=none`, `generation=2`, `hash-1`, `partial` | gleiche 167 Diagnosen und Kürzungen | ca. 18,7k; Wiederholung innerhalb der Session stabil | Kein späteres Source-Upgrade beobachtet. |
| SB-P06 | `inspect_assembly`; `installed vendor assembly A`, kleine metadata-only-Probe | Erfolg; `decompiled`, `sourcePath=none`, `snapshot=none`, `generation=1`, `hash-2`, `partial` | `partial`; 105/1 Diagnosen, gekürzt | ca. 5,6k; erwartbarer externer Fallback | Referenzzustand ohne Source-Leak. |
| SB-P07 | `inspect_assembly`; `installed vendor assembly B`, identische kleine Probe | Erfolg; `decompiled`, `sourcePath=none`, `snapshot=none`, `generation=2`, `hash-3`, `partial` | `partial`; 7/1 Diagnosen, gekürzt | ca. 5,3k; erwartbarer externer Fallback | Referenzzustand ohne Source-Leak. |
| SB-P08 | `inspect_assembly` und `get_server_health`; Rückwechsel zu `repo-provided assembly`, identische kleine Parameter | Erfolg; wieder `decompiled`, `sourcePath=none`, `snapshot=none`, `generation=2`, `hash-1`, `loadState=partial` | unverändert 167/1 Diagnosen, Truncation sichtbar | ca. 18,7k bzw. 1,0k; Hash, Generation und Herkunft entsprechen SB-P01/P04 | Kein Cache-/Session-Leak oder stale Herkunftszustand beobachtet. |

## Register

| ID | Status | Umfang | Dringlichkeit | Disposition |
|---|---|---|---:|---|
| SB-001 | bestätigt | systemisch: Source-Mapping, Assembly-Session und vier Toolprojektionen | P1 | Produktionsfix/gezielte Diagnose: die vorhandene konfigurierte Zuordnung muss für das adressierte Assemblyziel source-backed ergeben oder einen strukturierten, redigierten Fallback-Grund liefern. |
| SB-002 | bestätigt | systemisch: Antwortbudget über `inspect_assembly` | P2 | Response-Budget ergänzen oder klar als ungebunden deklarieren; kleine Type-/Member-Limits reichen nicht als Gesamtbudget. |
| SB-003 | widerlegt | mehrere Tools: Cross-Target-Sessionisolation | P2 | Keine Maßnahme; Wechsel A/B und Rückwechsel erzeugten keinen fremden Hash-, Origin-, Generation- oder Snapshotzustand. |
| SB-004 | offen | systemisch: source-backed Lebenszyklus, Akquisition und Lease/Snapshot | P2 | Erst mit einer tatsächlich source-backed Probe erneut prüfen; die aktuelle Umgebung liefert keinen positiven Zustand und keinen separaten Akquisitions-/Providerfehler. |

### SB-001 – Konfigurierte Source-Zuordnung wird live nicht source-backed

- **Evidenz:** Konfiguration und lokale Quell-/Checkout-Artefakte waren
  vorhanden. Dennoch lieferten `inspect_assembly`, `find_symbol`,
  `get_file_skeleton` und assemblygebundenes `get_server_health` ausschließlich
  `decompiled`, `sourcePath=none`, `snapshot=none`, `partial`, `medium` und
  `untrusted`. Nach der kurzen Folge und nach zwei Targetwechseln blieb derselbe
  Zustand bestehen.
- **Agentische Wirkung:** Fallback ist klar als dekompiliert und partiell
  erkennbar. Es ist jedoch nicht nachvollziehbar, warum die konfigurierte
  Zuordnung nicht genutzt wurde; ein Agent kann nicht zwischen fehlender
  Zuordnung, nicht verfügbarem Provider, nicht passendem Snapshot und
  Source-Akquisitionsfehler unterscheiden.
- **Empfehlung:** Mapping-Auflösung gegen Assemblyidentität und
  Konfigurationsladepfad prüfen. Bei Fallback eine stabile, redigierte Kategorie
  wie `mapping-not-found`, `source-unavailable`, `snapshot-mismatch` oder
  `provider-failed` in StructuredContent ausgeben. Erst danach eine positive
  source-backed Regression mit `sourcePath`/Snapshot-Label und Generation
  absichern.

### SB-002 – Kleine Ergebnislimits begrenzen die Fallback-Gesamtantwort nicht

- **Evidenz:** Bei identischen Limits von 1/1 hatte die repo-provided Probe
  ca. 18,7k Zeichen; die externen Gegenproben lagen bei ca. 5,3–5,6k. Die
  Diagnoseprobe war zwar serverseitig auf eine Stichprobe begrenzt und die
  Kürzung sichtbar, aber Referenz- und Sitzungsmetadaten dominierten weiter die
  Antwort.
- **Agentische Wirkung:** Diagnostics und Truncation sind interpretierbar;
  ein garantierter Gesamt-Response-Budgetvertrag ist daraus nicht ableitbar.
- **Empfehlung:** Zusätzlich zu Slice-Limits eine gesamte Byte-/Tokenobergrenze
  mit sichtbar ausgewiesenem Kürzungsgrund einführen.

## Vergleich mit den früheren anonymisierten Berichten

- **Widerlegt:** Die Behauptung, die vorhandene Zuordnung werde nicht nur über
  dekompilierten Fallback umgangen, ist in dieser Probe nicht belegbar: sie
  führte ausschließlich zu `decompiled`.
- **Teilweise bestätigt:** Der beobachtete Fallback ist eindeutig von einem
  erwarteten source-backed Zustand unterscheidbar (`origin`, fehlender
  `sourcePath`/Snapshot, Trust, Confidence, Completeness und Diagnostics).
  Die positive source-backed Seite des Vergleichs war in dieser Umgebung nicht
  erzeugbar.
- **Offen:** Snapshot-/Source-Lease- und Akquisitionsstatus sind nicht
  agentisch interpretierbar, weil kein source-backed Zustand und keine
  kategorisierte Fallback-Ursache ausgegeben wurden.
- **Teilweise bestätigt:** Fehlende Abhängigkeiten und daraus folgende
  Partialität sind durch `loadState`, Completeness, Diagnosezähler und
  Truncation nachvollziehbar. Ein echter Source-Fehler konnte nicht separat
  beobachtet werden; sein dekompilierter Fallback bleibt daher offen.
- **Teilweise widerlegt:** Diagnosestichproben bleiben begrenzt und ihre
  Kürzung sichtbar. Die Gesamtantwort bleibt dagegen auch bei kleinsten
  Type-/Member-Limits nicht nachweisbar begrenzt.

## Grenzen

Es wurden keine Daten verändert und kein neuer Checkout/Snapshot ausgelöst oder
als solcher beobachtet. Daher lässt die Umgebung keinen positiven Nachweis für
source-backed Analyse, Source-Akquisition, Snapshot-Wechsel oder Lease-Release
zu. Ältere Findings wurden nicht übernommen, sofern sie oben nicht durch diese
Live-Proben erneut bestätigt oder ausdrücklich als offen eingeordnet sind.

### Commit-Vorschlag

docs: dokumentiere Source-backed-Regressionsprobe
