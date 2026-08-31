# MCP-Live-/Vertragsaudit: Administration und Observability

Datum: 2026-08-31
Status: abgeschlossen; keine Produktions-/Testcodeänderung, kein Build und kein Testlauf

## Scope und Sicherheitsgrenze

Geprüft wurden ausschließlich die Live-Verträge von `reload_config` und
`get_server_health` sowie der sichtbare Schema-/Dokumentationsvertrag von
`report_observability_feedback`. Ziele erscheinen nur als `project target` und
`installed vendor assembly A`; konkrete Pfade, Hersteller-, Produkt- und
Dateinamen werden nicht wiedergegeben.

Der Reload ohne Override ist nur read-only-nah: Er liest zwar dieselbe
Regeldatei neu ein, aktualisiert aber die residente Konfiguration. Ein
`configPath` wäre ein temporärer Hot-Swap-Override und ist deshalb nicht als
read-only annotiert. Die negative Probe verwendete einen vorab nicht
existierenden neutralen Pfad; vor und nach dem Aufruf war dieser nicht im
Dateisystem vorhanden.

`report_observability_feedback` wurde bewusst **nicht** ausgeführt. Der
sichtbare Katalog und die Referenz bestätigen eine persistente System-Log-
Nebenwirkung und eine nicht idempotente Annotation. Für diesen Audit war die
Schema- und Scope-Prüfung ausreichend; ein zusätzlicher Logeintrag hätte keine
neue Wire-Evidenz mit verhältnismäßiger Nebenwirkung erzeugt.

## Probe-Matrix

Antwortgrößen sind gerundete Zeichenmengen der Text- beziehungsweise
StructuredContent-Nutzlast, nicht Rohantworten. `n/a` bedeutet, dass der
jeweilige Status für diesen Verwaltungsvorgang nicht anwendbar ist.

| Probe | Tool / Zielklasse | Parameterklasse | Status | StructuredContent / Text | Herkunft, Completeness, Truncation, Diagnostics | Größe | Nebenwirkung und Agentennutzen | Evidenz | Finding-ID | Umfang / Dringlichkeit / Disposition |
| :-- | :-- | :-- | :-- | :-- | :-- | --: | :-- | :-- | :-- | :-- |
| AO-01 | `reload_config` / `project target` | kein `configPath`; erneutes Einlesen der registrierten Regeldatei | `isError=false` | kein StructuredContent; kurze Vorher-/Nachher-Textzusammenfassung | Herkunft: bestehende Regeldatei; 17 aktivierte Regeln vor und nachher; Completeness/Truncation n/a; keine Diagnostics | ca. 0,2 k / 0 | keine Dateisystemänderung beobachtet; residenter Snapshot wird neu gesetzt. Für Menschen klar, für Clients nur Textanalyse | Regelanzahl und Konfigurationsreferenz unverändert | AOM-001 | lokal / P1 / Produktionsvertrag typisieren |
| AO-02 | `get_server_health` / `project target` | vollständiger Projekt-Target-Block direkt nach AO-01 | `isError=false` | StructuredContent mit Version, Projekt-, Daemon- und Assembly-Abschnitten; gleichwertiger Text | ein geladener Projekt-Key mit bisheriger Regeldatei; keine Assembly-Sessions; Diagnosemodus aus, daher keine Samples oder Truncation | ca. 0,7 k / 0,8 k | read-only und maschinenlesbar; bestätigt die stabile Projektsicht nach Reload | geladen, unveränderte Konfiguration und ein einzelner Projektzustand | kein Befund | lokal / P3 / keine Maßnahme |
| AO-03 | `reload_config` / `project target` | absoluter, absichtlich nicht existierender neutraler `configPath` | `isError=false` trotz recoverable Fehler | kein StructuredContent; Text enthält `CONFIG_NOT_FOUND`, Kontext, Hint und den Hinweis auf den erhaltenen Zustand | Herkunft: angefragter Override; Completeness/Truncation n/a; Fehlercode ausschließlich Text | ca. 0,4 k / 0 | keine Datei angelegt, geändert oder gelöscht; aktive Konfiguration soll erhalten bleiben, aber Fehlerklassifikation erfordert Textparsing | Existenzprüfung vor/nachher negativ; Fehlertext bestätigt State-Retention | AOM-001 | lokal / P1 / Produktionsvertrag typisieren |
| AO-04 | `get_server_health` / `project target` | vollständiger Projekt-Target-Block direkt nach AO-03 | `isError=false` | StructuredContent und Text wie AO-02 | weiterhin derselbe geladene Projekt-Key, dieselbe Regeldatei und keine Assembly-Sessions; keine Diagnostics/Truncation | ca. 0,7 k / 0,8 k | read-only Nachweis, dass der ungültige Override die sichtbare aktive Konfiguration nicht ersetzt hat | Health-Projektion unmittelbar nach der Fehlprobe | kein Befund | lokal / P3 / keine Maßnahme |
| AO-05 | `reload_config` / `installed vendor assembly A` | vorhandenes Assembly-Ziel, kein Override | `isError=false`, `capability=unsupported`, `status=unsupported` | kein StructuredContent; Text mit `ASSEMBLY_TARGET_UNSUPPORTED`, Kontext und Alternative | Herkunft: Assembly-Target; Completeness/Truncation n/a; Fehlercode nur Text | ca. 0,4 k / 0 | keine Session- oder Dateinebenwirkung beobachtet; Unsupported ist für Menschen klar, für Clients nicht typisiert | sichtbares Schema und Live-Antwort stimmen beim Projekt-only-Vertrag überein | AOM-001 | lokal / P1 / Produktionsvertrag typisieren |
| AO-06 | `report_observability_feedback` / unbound | nur Schema- und Dokumentationsprüfung, kein Call | nicht ausgeführt | sichtbarer Katalog benennt die fünf Kategorien; Referenz verspricht Bestätigung und typisiertes DTO | Herkunft/Completeness/Truncation/Diagnostics nicht live beobachtbar | 0 | System-Log-Schreibnebenwirkung und fehlende Idempotenz waren für diesen Audit nicht verhältnismäßig | Schema: `bug`, `false_positive`, `confusing_output`, `feature_request`, `performance`; Dokumentation der Annotationen | kein Live-Befund | systemisch / P3 / bei explizitem Log-Audit erneut prüfen |
| AO-07 | Dokumentation / kein Ziel | Abgleich von Integration, Konfiguration, Agent-API, README und sichtbarem Katalog | n/a | n/a | n/a | 0 | sichtbarer Katalog und die ausführliche Referenz sind überwiegend konsistent; eine frühere Agent-API-Passage schließt Assembly-Health aber textlich aus | vollständiger Dokumentabgleich auf die drei Verwaltungs-/Health-Verträge | AOM-002 | mehrere Dokumentationsabschnitte / P2 / Dokumentation konsolidieren |

## Findings

### AOM-001 – Reload-Erfolg und recoverable Fehler sind nur textuell klassifiziert

- Klassifikation: Wirevertragsbefund; Umfang: lokal bei `reload_config`; P1.
- Evidenz: Der erfolgreiche Reload und beide negativen Fälle (`CONFIG_NOT_FOUND`
  und `ASSEMBLY_TARGET_UNSUPPORTED`) hatten `isError=false` und kein
  StructuredContent. Die menschenlesbaren Texte enthalten zwar Code, Kontext
  und Hint, aber kein stabiles maschinenlesbares Ergebnis-, Fehler- oder
  Retention-Objekt.
- Agentische Wirkung: Ein Agent kann Erfolg, Wiederherstellbarkeit,
  unveränderten Snapshot und Unsupported nur durch sprach-/lokalisierungs-
  abhängiges Textparsing auseinanderhalten. Der anschließende Health-Call
  belegt die Retention, ist aber kein atomarer Ersatz für eine Reload-Antwort.
- Disposition: den Reloadvertrag mit typisiertem Status, Fehlercode,
  `configRetained`, Vorher-/Nachher-Regelanzahl und optionalem Capability-Status
  ergänzen; `isError`-Policy mit dem systemischen Befund F-HEALTH-004
  vereinheitlichen. Keine Produktionsänderung in diesem Scope.

### AOM-002 – Agent-API beschreibt Assembly-Health an zwei Stellen widersprüchlich

- Klassifikation: Dokumentationsbefund; Umfang: mehrere Abschnitte derselben
  Agent-API; P2.
- Evidenz: Eine frühe Zielbeschreibung nennt für `get_server_health` nur den
  optionalen vollständigen Projekt-Target-Block. Capability-Matrix,
  Tooltabelle und sichtbarer Katalog erlauben dagegen ausdrücklich auch einen
  vollständigen Assembly-Target-Block; die Integrationsreferenz bestätigt
  ebenfalls beide Zielarten.
- Agentische Wirkung: Ein Agent, der die frühe Passage isoliert nutzt, kann
  die zulässige Assembly-Health-Abfrage fälschlich auslassen. Der aktive
  Toolkatalog ist eindeutig, doch die widersprüchliche Referenz erhöht das
  Fehlentscheidungsrisiko.
- Disposition: die frühe Passage auf den gemeinsamen Projekt-/Assembly-
  Vertrag angleichen und auf die Capability-Matrix verweisen. Keine
  Produktionsänderung in diesem Scope.

## Feedback-Semantik und vorhandene Befunde

Die sichtbaren zulässigen Kategorien sind `bug`, `false_positive`,
`confusing_output`, `feature_request` und `performance`. Die vorhandenen
Vertragsbefunde sind theoretisch meldbar: fehlerhafte Filter, inkonsistente
Counts, nicht wirksame Parameter und fehlende Source-Zuordnung als `bug`;
Text-only-Fehler, fehlende strukturierte Navigation und unklare Capability-
Signale als `confusing_output`; ungebundene Antwortgrößen als `performance`.
Ein Vorschlag für zusätzliche strukturierte Felder passt alternativ zu
`feature_request`. Ein `false_positive` wurde in den vorliegenden Befunden
nicht festgestellt.

Die Referenz verspricht für Feedback eine Bestätigung und ein typisiertes DTO,
der sichtbare Katalog macht `feedbackType` jedoch nur als String sichtbar und
liefert die erlaubten Werte beschreibend. Dadurch kann ein Agent die Kategorien
vorab aus dem Katalog semantisch wählen, aber Rückgabe und Fehlerpfad wegen der
absichtlich unterlassenen persistenten Probe nicht live verifizieren. Dies ist
kein zusätzlicher Produktionsbefund; ein explizit beauftragter Log-Audit könnte
die Bestätigungs- und Invalid-Category-Projektion nachholen.

## Dokumentationsabgleich

| Vertrag | Integration | Konfiguration | Agent-API | README | Sichtbarer Katalog | Ergebnis |
| :-- | :-- | :-- | :-- | :-- | :-- | :-- |
| `reload_config` | Annotation und closed-world beschrieben | keine abweichende Detailbehauptung | Projekt-only, Reload- und Retention-Semantik beschrieben | Annotation beschrieben | Projekt-only und Retention bei ungültigem Pfad beschrieben | Kein zusätzlicher Widerspruch; Text-only-Wirevertrag ist AOM-001. |
| `get_server_health` | Projekt/Assembly und Diagnosegrenze beschrieben | Default ohne Diagnose-Samples beschrieben | einmal nur Projekt, sonst Projekt/Assembly | Projekt/Assembly und Diagnosegrenze beschrieben | Projekt/Assembly beschrieben | AOM-002; im Übrigen konsistent. |
| `report_observability_feedback` | persistente, nicht idempotente Annotation beschrieben | keine abweichende Detailbehauptung | unbound, optionale Kontextangabe, DTO und Log-Schreiben beschrieben | persistente, nicht idempotente Annotation beschrieben | unbound, Kategorien und Pflichtfelder beschrieben | Kein Vertragswiderspruch; Live-Rückgabe absichtlich nicht geprüft. |

## Abschluss

- Keine Produktions- oder Testdatei geändert, kein Build, kein Testlauf und
  kein Push.
- Der positive und der negative Reload bewahrten die überprüfte Projekt-
  Konfiguration sichtbar; der negative Pfad erzeugte keine Datei.
- `report_observability_feedback` blieb aus Gründen der persistenten
  Nebenwirkung unaufgerufen.

### Commit-Vorschlag

docs: dokumentiere Admin- und Observability-Verträge
