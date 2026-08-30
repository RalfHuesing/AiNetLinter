# Analyse: Ablauf beim Speichern eines Beleges (DocumentEngine)

## 1. Übersicht
- **Ziel-Assembly:** `ThirdParty.ERP.DocumentEngine.dll`
- **Kern-Klasse:** `ThirdParty.ERP.DocumentEngine.Document`
- **Daten-Schicht:** `ThirdParty.ERP.DocumentEngine.DocumentData`
- **Haupteinstiegspunkt:** `Document.Save(bool abbruchBeiWarnung)` bzw. `Document.SaveIntern(bool)`

---

## 2. Detaillierter Ablauf des Speichervorgangs

Der Speichervorgang eines Beleges gliedert sich in **6 chronologische Phasen**:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Validierung & Regelprüfungen (Validate & Workflow)        │
├─────────────────────────────────────────────────────────────┤
│ 2. Speicherstatus-Ermittlung (SaveModeCommit & Events)      │
├─────────────────────────────────────────────────────────────┤
│ 3. Persistierung Kopf & Positionen (DocumentData.Insert*)   │
├─────────────────────────────────────────────────────────────┤
│ 4. Lagerbuchungen & Disposition (WarehouseJob & Verursacher)│
├─────────────────────────────────────────────────────────────┤
│ 5. Statistiken, Historie & Vorgangsfortschreibung           │
├─────────────────────────────────────────────────────────────┤
│ 6. Rechnungswesen-Übergabe & Transaktions-Commit            │
└─────────────────────────────────────────────────────────────┘
```

---

### Phase 1: Validierung & Vorprüfungen
1. **Struktur- & Pflichtfeldvalidierung (`Document.Validate()`, `ValidateObjects()`):**
   - Prüft Adressbeziehungen (A0 Rechnungsempfänger, A1 Lieferadresse, A2–A4 abweichende Adressen).
   - Validiert Besteuerungsart (`_besteuerung`), Währungskurse (`_fremdwaehrungskurs`), Belegjahr und Buchungsperiode.
   - Validiert alle untergeordneten Auflistungen:
     - Positionen (`DocumentPositionCollection`)
     - Stücklistenelemente (`DocumentStueckliste.ValidateObjects()`)
     - Zuschläge (`DocumentZuschlag.ValidateObjects()`)
     - Staffelrabatte (`DocumentStaffelrabatt.ValidateObjects()`)
     - Zahlungskonditionen (`CalcAndValidateZDKs()`)
2. **Customizing / Hook-Events (`DcmContextBelegValidate`, `DcmContextBelegBeforeSave`):**
   - Feuert Customizing-Ereignisse zur kundenindividuellen Vorvalidierung vor dem eigentlichen Schreibzugriff.
3. **Workflow- & Budgetprüfungen:**
   - Evaluierung von Workflow-Regeln (`WorkflowRule`, `WorkflowCondition`).
   - Kreditlimitprüfung (`_kreditlimitKonto`), Liefersperren (`_hatLiefersperreKonto`).
   - Im Einkauf: Budgetüberwachung (`DocumentBudget`, Event `QuestionBudget`). Bei Überschreitung automatisches Parken (`_ekIstGeparktWegenBudget`).

---

### Phase 2: Speicherstatus & SaveMode
1. **Ermittlung des Zielstatus (`BelegSpeicherstatus`):**
   - Unterscheidet zwischen regulärem Speichern, Parken (`IstWechselParkenNachSpeichern`), Storno oder Vorab-Erfassung.
2. **Event `SaveModeCommit` & `WorkFlowSaveModeCommit`:**
   - Benachrichtigt externe Komponenten und UI über den endgültigen Speicherstatus.
3. **Zustandssicherung (`PushSaveStatus()`):**
   - Sichert den aktuellen internen Speicherstatus auf einem Stack für ein evtl. Rollback.

---

### Phase 3: Persistierung der Belegdaten (Datenbank-Schicht `DocumentData`)
1. **Belegkopf (`SaveBelegkopf` -> `DocumentData.InsertBeleg`):**
   - Nummernvergabe/Aktualisierung (`_belegnummer`, `_handle`, `_vorgangsHandle`).
   - Speichert Nettobetrag, Steuerbetrag, Bruttobetrag, Skontofähigkeit, Rabatte (1–3), Kopftexte und Metadaten.
   - Projektbelege: Speichert Zusatzdaten via `InsertBelegProjekt` und `InsertProjektInfo`.
   - Schlussrechnungen: Speichert Rechnungsblöcke via `SaveSchlussrechnung` und `InsertRechnungsblock`.
2. **Positionen & Stücklisten (`SavePositionen` -> `DocumentData.InsertPosition`):**
   - Schreibt alle Belegpositionen (Mengen, Einzel-/Gesamtpreise, Rabatte, Erlöskonten).
   - Schreibt Stücklistenstrukturen (`DocumentData.InsertStuecklistenelement`).
   - Schreibt Lieferinformationen (`InsertLieferinformation`).
   - Schreibt Lagerplätze, Chargen- und Seriennummernzuordnungen (`InsertLagerplatz`, `InsertLagerplatzCharge`, `InsertLagerplatzSeriennummer`).
   - Verarbeitet Rahmenvertrags-Zuordnungen (`SavePositionRahmenvertragZuordnung`, `InsertRAVZuordnung`).
3. **Kalkulationstabellen:**
   - Schreibt Steuern (`SaveSteuern` -> `InsertSteuer`).
   - Schreibt Zuschläge (`SaveZuschlaege` -> `InsertZuschlaege`).
   - Schreibt Staffelrabatte (`SaveStaffelrabatte` -> `InsertStaffelrabatt`).
   - Schreibt Zahlungskonditionen (`SaveZKD` -> `InsertZKD`).
   - E-Rechnung: Verknüpft Import-IDs (`SaveEInvoiceImportID`).

---

### Phase 4: Lagerbuchung & Disposition
1. **Lagerjob-Ausführung (`_lagerJob`, `_lagerJobHandle`):**
   - Führt je nach Belegart die physikalische Lagerbewegung aus (Zugang/Abgang über `_lagerbewegungsartPositiveMenge` / `_lagerbewegungsartNegativeMenge`).
   - Erstellt Lagerbewegungsprotokolle (`_datumLagerbewegungsprotokoll`).
   - Prüft Sperrlager und Bestandsgrenzen.
2. **Dispositionsfortschreibung (`DocumentData.InsertUpdateDispositionsVerursacher`):**
   - Aktualisiert Bedarfsverursacher und Verursacherketten.
   - Erstellt/löscht Dispositionsartikel (`InsertDispoArtikel`, `DeleteDispoArtikel`).

---

### Phase 5: Statistiken, Historie & Vorgänge
1. **Statistik-Aktualisierung (`StatistikUpdate` / `_hatStatistikLauf`):**
   - Schreibt Hauptstatistik (`DocumentData.InsertStatistikMain`).
   - Schreibt Kunden-/Lieferantenumsätze (`InsertStatistikKonto`, `InsertStatistikKontoGruppe`).
   - Schreibt Vertreterprovisionen (`InsertStatistikVertreter`, `_vkProvision`).
2. **Vorgangs-Statusmengen:**
   - Aktualisiert gelieferte, berechnete und offene Mengen im übergeordneten Vorgang (`InsertVorgangPositionsReferenz`).
3. **Archivierung & Änderungshistorie:**
   - Erstellt Revisionsstände für Belegkopf, Positionen und Picklisten (`SaveArchiv`, `SavePicklistenArchiv`, `MaxAenderungsNummer`).

---

### Phase 6: Rechnungswesen & Abschluss
1. **FIBU / Offene Posten Übergabe (`DocumentReweInterface`, `BudgetueberwachungSaveRewe`):**
   - Bereitet Buchungssätze und OP-Einträge für das Rechnungswesen vor (`DcmContextBeforeReweUebergabe`, `DcmContextReweUebergabe`).
2. **Transaktionsabschluss (`DocumentData.CommitQry`):**
   - Führt den finalen Commit der temporär gepufferten Abfragen und Tabellenoperationen aus.
3. **Aufräumarbeiten & PopSaveStatus:**
   - Rücksetzen von temporären Handles und Aktualisieren der `_belegRowVersion`.
