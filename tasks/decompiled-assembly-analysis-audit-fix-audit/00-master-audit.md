# MCP-Vertragsaudit – Masterbericht

Datum: 2026-08-31
Status: abgeschlossen; Synthese der neun Live-Proben. Keine Produktions- oder
Testdateien geändert, kein Build, kein Testlauf, kein Code-Audit und kein Push.

## Ergebnis und Management-Zusammenfassung

Das Angebot bietet Agents gute, kleine Einstiegspfade: zielgebundene Health-
Sichten, Symbol-/Metrik-Lookups sowie klein begrenzte Struktur- und Graphabfragen
lieferten verwertbare Ergebnisse. Assembly-Ziele blieben in den Proben
metadata-only; es wurde weder Code ausgeführt noch Quelle geändert. Unzulässige
Ziele und Parameter wurden abgewiesen, und zwischen Zielklassen wurde keine
Datenvermischung beobachtet.

Für verlässliche Automatisierung bestehen jedoch fünf prioritäre
Verbesserungsfelder: ein einheitlicher, typisierter Fehlervertrag; harte
Gesamt-Antwortbudgets; konsistente StructuredContent-Projektionen; ein
verlässlicher Pfad-/Folge-ID-Vertrag; und nachvollziehbarer source-backed
Fallback. Keine künstliche P0-Einstufung ist gerechtfertigt. Die P1-Funde sind
Vertragsrisiken für Agents, nicht Sicherheitsvorfälle.

## Geltungsbereich und Evidenzstandard

Dieser Bericht verdichtet ausschließlich live beobachtete Befunde der neun
anonymisierten Einzelberichte. Eine Wiederholung früherer, nicht bestätigter
Altbefunde erfolgte nicht. Probe-IDs verweisen auf die Einzelberichte; sie sind
keine Rohantworten. Aussagen über Vollständigkeit dekompilierter Assembly-Sichten
stehen stets unter `partial`-/Diagnostik-/Trunkierungs-Vorbehalt.

## Tool-Coverage

| Werkzeug | Status | Kurzbewertung / Evidenz |
| :-- | :-- | :-- |
| `get_server_health` | erfolgreich getestet | Global, project target und Assemblyziele; globale Payload kann mit Sessions stark wachsen. [01] |
| `get_file_tree` | erfolgreich und negativ getestet | Summary/Tree, Tiefen, Limits und ungültige Varianten; `treeDepth` unwirksam. [01] |
| `get_index_scope` | erfolgreich und unsupported getestet | Project target positiv; Assemblyziel nachvollziehbar unsupported, aber nur Text. [01, 06] |
| `inspect_assembly` | erfolgreich und negativ getestet | Filter, Limits, Wiederholung, metadata-only sowie nicht analysierbares Ziel. [02] |
| `find_assembly_extensions` | erfolgreich getestet | Name/Namespace, Limits und Receiver-Proben; Receiverfilter unwirksam. [05] |
| `find_symbol` | erfolgreich getestet | Root und bounded Referenzen, Filter, Leerresultat und Wiederholung. [03] |
| `find_references` | erfolgreich und negativ getestet | Root/bounded, Tiefe, Leermenge, Mehrdeutigkeit und stale Form. [03] |
| `get_call_tree` | erfolgreich getestet | Richtungen, Formate, Tiefe und bounded Referenzen; Root-only textuell. [03] |
| `get_type_hierarchy` | erfolgreich getestet | Kleine Typ-Slices; erfolgreiche Antworten nur textuell. [03] |
| `get_file_skeleton` | erfolgreich und negativ getestet | Einzel-/Batchdatei, Pfadformen, leere/ungültige Anfrage. [04] |
| `get_class_structure` | erfolgreich und negativ getestet | Limits, Sortierung, Filter und stale ID; StructuredContent-Count-Drift. [04] |
| `get_symbol_body` | erfolgreich und negativ getestet | IDs, Body-Limits, Batch und stale ID. [04] |
| `get_feature_context` | erfolgreich und unsupported getestet | Assemblyziel unsupported, project target positiv. [04] |
| `get_namespace_tree` | erfolgreich und negativ getestet | Project-/Assembly-Drilldown, Limits und ungültiger Kindfilter. [04, 08] |
| `metrics_lookup` | erfolgreich und negativ getestet | Project-/Assembly-Lookup, Batch und fehlende Symbole. [04, 08] |
| `metrics_tree` | erfolgreich und negativ getestet | Modi, Tiefe, Top-N, Filter und ungültige Parameter; nur Text. [04, 08] |
| `dependency_graph` | erfolgreich und negativ getestet | Project-/Assembly-Graph, Richtungen, Grenzen und Dateipfade. [06] |
| `get_impact` | erfolgreich und unsupported getestet | Project-Default und Symbolscope positiv; Assemblyziel unsupported. [06] |
| `get_test_context` | erfolgreich und unsupported getestet | Project target positiv; Assemblyziel unsupported. [06] |
| `search_pattern` | erfolgreich und unsupported getestet | Kleine Slices und optionale Anreicherung; Assemblyziel unsupported. [06] |
| `get_hotspots` | erfolgreich und unsupported getestet | Project target und enger Scope positiv; Assemblyziel unsupported. [08] |
| `pattern_detect` | erfolgreich und negativ getestet | Pattern-Batches, Limits, ungültiges Pattern und Assembly-Unsupported. [08] |
| `get_violations` | erfolgreich und unsupported getestet | Leere Project-Slices positiv; Snippetwirkung mangels Treffer nicht positiv getestet. [08] |
| `reload_config` | erfolgreich und negativ getestet | Erfolgs-, fehlender Override- und Assembly-Unsupported-Pfad; nur Text. [09] |
| `report_observability_feedback` | bewusst nicht ausgeführt | Persistente, nicht idempotente Log-Nebenwirkung; nur Schema/Dokumentation geprüft. [09] |
| `find_duplicates` | bewusst nicht ausgeführt | User-Vorgabe: kein Code-Audit. [08] |
| `find_dead_code` | bewusst nicht ausgeführt | User-Vorgabe: kein Code-Audit. [08] |
| `find_magic_values` | bewusst nicht ausgeführt | User-Vorgabe: kein Code-Audit. [08] |
| `safeguard` | bewusst nicht ausgeführt | User-Vorgabe: kein Code-Audit. [08] |

## Priorisiertes Befundregister

| Master-ID | P | Umfang | Disposition | Verdichteter Befund | Original-IDs / Evidenz |
| :-- | :--: | :-- | :-- | :-- | :-- |
| MF-001 | P1 | systemisch/Server | bestätigt, Fix erforderlich | Regel-, Validierungs- und Unsupported-Pfade verwenden uneinheitlich `isError` und liefern überwiegend keinen strukturierten Fehlerpayload. Erfolg, Leermenge und Clientfehler sind ohne Textparsing nicht robust trennbar. | F-HEALTH-004, CT-ERR-001, RMT-001, AOM-001; [01, 04, 06, 08, 09] |
| MF-002 | P1 | systemisch/Server | bestätigt, Fix erforderlich | Teil-/Gesamtzähler, Arrays und StructuredContent driften: große Extension-Slices und eine Klassenstruktur verlieren strukturierte Einträge, während Text bzw. Counts Vollständigkeit signalisieren. | EXT-001, SC-STRUCT-002; [04, 05] |
| MF-003 | P1 | mehrere Tools | bestätigt, Fix erforderlich | Sichtbare Slice-Limits sind kein hartes Gesamtbudget; Diagnose-, Referenz-, Namespace- und Sessionmetadaten lassen Antworten trotz kleiner Slices oder großer Limits stark anwachsen. | IA-004, SN-003, EXT-002, SB-002; [02, 03, 05, 07] |
| MF-004 | P1 | lokal (ein Tool) | bestätigt, Fix erforderlich | `receiverType` in `find_assembly_extensions` reduzierte weder passende noch absichtlich unmögliche Treffer. | EXT-003; [05] |
| MF-005 | P1 | mehrere Tools | bestätigt, Fix erforderlich | Pfad- und Folgeketten sind nicht durchgehend stabil: relative dekompilierte Dateipfade sind targetabhängig nicht wiederverwendbar; `find_symbol` gibt keine direkt verwendbare generationsgebundene Folge-ID aus. | SC-STRUCT-001, CT-DG-001, SN-001; [03, 04, 06] |
| MF-006 | P1 | systemisch/Server | bestätigt, Fix erforderlich | Vorhandene konfigurierte Source-Zuordnung führte in allen geprüften Projektionen weiterhin zu decompiled fallback; der Fallback-Grund ist nicht strukturiert sichtbar. | SB-001; [07] |
| MF-007 | P1 | mehrere Tools | bestätigt, Fix erforderlich | Strukturierte Parameterdaten fehlen in Member-/Extension-Resultaten; Signaturen sind nur Text-Fallback. | IA-006, EXT-004; [02, 05] |
| MF-008 | P1 | mehrere Tools | bestätigt, Fix erforderlich | `get_call_tree` Root-only und `get_type_hierarchy` liefern keine konsistente strukturierte Root-/Completeness-/Truncation-Projektion. | SN-002; [03] |
| MF-009 | P1 | lokal (ein Tool) | bestätigt, Fix erforderlich | `treeDepth` wird im Dateibaum wire-seitig ignoriert; wirksam war stattdessen ein anders benannter Tiefenparameter. | F-HEALTH-001; [01] |
| MF-010 | P2 | systemisch/Server | Tech-Debt | Parameterloser globaler Health expandiert residenten Sessiondetailbestand und kann ohne harte Begrenzung stark wachsen. | F-HEALTH-003; [01] |
| MF-011 | P2 | lokal (ein Tool) | Tech-Debt | Root-Summary des Dateibaums überträgt breite Verzeichnisdaten und ignoriert `maxResults`. | F-HEALTH-002; [01] |
| MF-012 | P2 | systemisch/Server | Tech-Debt | Reparse-ähnliche Schreibweise desselben zulässigen Assemblyziels erzeugte eine zweite Sessiongeneration. Keine Datenvermischung beobachtet. | CT-SES-001; [06] |
| MF-013 | P2 | lokale Tools/Dokumentation | Dokumentation, danach Design | `metrics_tree` ist erfolgreich, projektiert Assembly-Origin/Partialität aber nur im Text; `inspect_assembly` weist metadata-only/Load-State nur indirekt aus. | RMT-002, IA-007; [02, 08] |
| MF-014 | P2 | Dokumentation | Dokumentation | Eine Health-Beschreibung widerspricht der übrigen Dokumentation zur zulässigen Assemblyziel-Abfrage. | AOM-002; [09] |
| MF-015 | P3 | Dokumentation | Dokumentation | Für breite Listen-/Baumwerkzeuge fehlt eine explizite Kleinlimit-Strategie. | RMT-003; [08] |
| MF-016 | P2 | systemisch/Server | widerlegt/kein Befund | Cross-target-Datenvermischung wurde beim Wechsel zwischen project target und Assemblyzielen nicht beobachtet. | SB-003; [06, 07] |
| MF-017 | P2 | systemisch/Server | offen/needs-user-decision | Source-backed Lifecycle, Akquisition und Snapshot-/Lease-Verhalten konnten mangels positiver source-backed Sitzung nicht geprüft werden. | SB-004; [07] |

## Wesentliche Vertragsbefunde

- **Unwirksames `treeDepth`:** identische Tiefen trotz Werten 0 bis 3; ein
  anders benannter Tiefenparameter wirkte. Positiv und reproduzierbar getestet.
  [01/F-HEALTH-001]
- **Globale Health-/Assembly-Payloads:** globale Health wuchs nach
  Session-Erwärmung stark; Assembly-Inspektion blieb auch bei kleinen
  Type-/Member-Limits metadata-lastig. Positiv getestet. [01/F-HEALTH-003;
  02/IA-004]
- **Text-/StructuredContent-Drift:** `inspect_assembly` begrenzte einen
  strukturierten Type-Slice zusätzlich; `get_call_tree` Root-only, große
  Extension-Slices und `metrics_tree` sind textlich bzw. strukturiert
  inkonsistent. Positiv getestet, aber nicht jede Toolvariante. [02/IA-004;
  03/SN-002; 05/EXT-001; 08/RMT-002]
- **Receiverfilter:** passender und unmöglicher `receiverType` zeigten
  dieselbe Menge. Positiv getestet. [05/EXT-003]
- **Parameterdaten:** geprüfte strukturierte Member-/Extensiondaten hatten
  keine separaten Parameterfelder. Positiv im geprüften Scope; kein Beweis für
  jedes Symbolkind. [02/IA-006; 05/EXT-004]
- **Relative Decomp-Dateipfade:** bei einem Ziel scheiterte die direkte relative
  Folgeform, bei anderen funktionierte sie; absolute Form war nutzbar. Positiv
  getestet. [04/SC-STRUCT-001; 06/CT-DG-001]
- **`isError`/StructuredContent:** reguläre Anwendungs- und Unsupported-Fehler
  waren lesbar, aber überwiegend `isError=false` und text-only. Positiv über
  mehrere Toolgruppen getestet. [01/F-HEALTH-004; 06/CT-ERR-001; 08/RMT-001]
- **Session-Key/Reparse:** gleicher Inhalt mit reparse-ähnlicher Schreibweise,
  aber andere Generation; keine Isolationverletzung. Positiv getestet.
  [06/CT-SES-001]
- **Konfigurierte Source-Zuordnung:** wiederholt decompiled fallback,
  `sourcePath=none`; positive source-backed Seite nicht getestet und bleibt
  offen. [07/SB-001, SB-004]
- **Reload-Textvertrag:** Erfolg, fehlender Override und Assembly-Unsupported
  sind nur textuell klassifiziert; die aktive Konfiguration blieb nach
  Fehlprobe sichtbar erhalten. Positiv getestet. [09/AOM-001]
- **Health-Dokumentation:** eine frühe Beschreibung schließt Assemblyziele
  aus, Capability-Matrix und Live-Vertrag erlauben sie. Dokumentationsabgleich,
  keine neue Wireprobe nötig. [09/AOM-002]

## Empfohlene nächste Verbesserungsrunde

| Maßnahme | Nutzen für Agents | Betroffene Werkzeuge | Risiko / Abhängigkeit | Art |
| :-- | :-- | :-- | :-- | :-- |
| 1. Einheitliches Fehler-DTO und `isError`-Policy einführen | Sichere Retry-, Fallback- und Unsupported-Entscheidung ohne Textparser | Fehler- und Unsupported-Pfade quer über das Angebot, Reload | Breite Kompatibilitätsentscheidung für Text/StructuredContent | Produktionscode + Design |
| 2. Harten Gesamt-Response-Budgetvertrag ergänzen | Vorhersehbarer Kontextverbrauch und sichere Progressive Disclosure | Health, Assembly-Inspektion, Navigation, Extensions, breite Bäume | Serverseitige Kürzungsreihenfolge, sichtbare Gründe und Tests | Produktionscode + Design |
| 3. StructuredContent atomar mit Text/Counts ausrichten | Kein stiller Datenverlust in automatisierten Agents | Extensions, Klassenstruktur, Call tree, Metrics tree, Assemblyinspektion | DTO-/Kompatibilitätsmigration; pro Tool Regressionstests | Produktionscode |
| 4. Assembly-Navigation normalisieren | Robuste Kette von Fund zu Datei, Skeleton und Body | Symbolsuche, Dateiskeleton, Dependency graph | Pfadmodell und generationsgebundene IDs festlegen | Produktionscode + Design |
| 5. Source-Mapping und Agentenreferenz nachschärfen | Erklärbarer source-backed/fallback Zustand und korrekte Zielwahl | Assembly-Sitzungen, Health, Inspektion; Dokumentation | Reproduzierbares source-backed Testziel erforderlich | Produktionscode + Dokumentation |

## Token-Effizienz und Query-Reihenfolge

Das Angebot hat gute kleine Einstiegspfade, aber eine Metadaten-Grundlast und
keine überall beobachtbare harte Gesamtbudgetgrenze. Empfohlene Reihenfolge:

1. `get_server_health` stets zielgebunden und ohne Diagnose-Samples; bei
   physischer Orientierung `get_file_tree` als Summary oder kleiner Tree-Slice.
2. Bekannte Entität: `find_symbol` oder `metrics_lookup` mit einem Identifier
   und Limit 1–2. Unbekannte Struktur: `get_namespace_tree` mit Tiefe 1 und
   Limit 1–2.
3. Für Assemblys zuerst `inspect_assembly` mit `maxResults=1` und
   `maxMembers=1`; Origin, Trust, Completeness, Diagnostics und Truncation
   prüfen. Erst danach nach `Type_1`/`Member_1` verfeinern.
4. Für Struktur `get_file_skeleton` mit einer Datei, danach
   `get_class_structure` oder `get_symbol_body` mit kleinen Grenzen.
5. Referenzsuche, Call tree, Extensions, Pattern und Violations erst nach
   klarer Fragestellung; jeweils `maxResults`/`topN` 1–2. Bounded
   Referenzsuche und große Assemblylimits nur bewusst nutzen.

## Security und Safety

- Metadata-only wurde für Assemblyziele beobachtet; keine Ausführung und keine
  Quellmodifikation wurden gesehen. Ein expliziter maschinenlesbarer
  metadata-only-/Load-Attest fehlt jedoch teilweise.
- Unzulässige Zielarten, relative oder nicht vorhandene Ziele und ungültige
  Identifier wurden abgewiesen; sie wurden nicht als zulässige Analyseziele
  verwendet.
- Reparse-ähnliche Pfade erzeugten zwar eine zweite Sessiongeneration, aber
  keine unerlaubte Zielöffnung oder Cross-target-Datenvermischung.
- `report_observability_feedback` blieb wegen persistenter,
  nicht-idempotenter Log-Nebenwirkung unaufgerufen.

## Ausgeführte Proben und nicht ausgeführte Gates

Die tatsächlichen Live-Proben sind in den folgenden Einzelberichten samt
Commit dokumentiert:

| Bericht | Commit |
| :-- | :-- |
| `01-session-health-contract.md` | `24c1a0ae` |
| `02-inspect-assembly.md` | `f2e96682` |
| `03-symbol-navigation.md` | `fcb319b2` |
| `04-structure-context.md` | `2b938397` |
| `05-extensions.md` | `a85fa80b` |
| `06-cross-target-errors.md` | `c134f867` |
| `07-source-backed-regression.md` | `827eaf80` |
| `08-remaining-tools-docs.md` | `d9adfd2f` |
| `09-admin-observability.md` | `8177eb10` |

Nicht ausgeführt: Builds, `dotnet test`, ein Code-Audit sowie
`find_duplicates`, `find_dead_code`, `find_magic_values` und `safeguard`
(jeweils gemäß User-Vorgabe „kein Code-Audit“). Auch
`report_observability_feedback` wurde wegen der persistenten Log-Nebenwirkung
nicht aufgerufen.

Der Commit dieses Masterberichts wird in der Übergabe nach dem Commit genannt:
eine Selbstreferenz im Dateiinhalt würde dessen Commit-ID verändern.

### Commit-Vorschlag

docs: verdichte MCP-Vertragsaudit
