# Analyse: Ablauf beim Speichern eines Beleges (BelegEngine)

## 1. Übersicht
- **Ziel-Assembly:** `Sagede.OfficeLine.Wawi.BelegEngine.dll`
- **Kern-Klasse:** `Sagede.OfficeLine.Wawi.BelegEngine.Beleg`
- **Daten-Schicht:** `Sagede.OfficeLine.Wawi.BelegEngine.BelegData`
- **Haupteinstiegspunkt:** `Beleg.Save(bool abbruchBeiWarnung)` bzw. `Beleg.SaveIntern(bool)`

---

## 2. Detaillierter Ablauf des Speichervorgangs

Der Speichervorgang eines Beleges gliedert sich in **6 chronologische Phasen**:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Validierung & Regelprüfungen (Validate & Workflow)        │
├─────────────────────────────────────────────────────────────┤
│ 2. Speicherstatus-Ermittlung (SaveModeCommit & Events)      │
├─────────────────────────────────────────────────────────────┤
│ 3. Persistierung Kopf & Positionen (BelegData.Insert*)      │
├─────────────────────────────────────────────────────────────┤
│ 4. Lagerbuchungen & Disposition (LagerJob & Verursacher)    │
├─────────────────────────────────────────────────────────────┤
│ 5. Statistiken, Historie & Vorgangsfortschreibung           │
├─────────────────────────────────────────────────────────────┤
│ 6. Rechnungswesen-Übergabe & Transaktions-Commit            │
└─────────────────────────────────────────────────────────────┘
```

---

### Phase 1: Validierung & Vorprüfungen
1. **Struktur- & Pflichtfeldvalidierung (`Beleg.Validate()`, `ValidateObjects()`):**
   - Prüft Adressbeziehungen (A0 Rechnungsempfänger, A1 Lieferadresse, A2–A4 abweichende Adressen).
   - Validiert Besteuerungsart (`_besteuerung`), Währungskurse (`_fremdwaehrungskurs`), Belegjahr und Buchungsperiode.
   - Validiert alle untergeordneten Auflistungen:
     - Positionen (`BelegPositionCollection`)
     - Stücklistenelemente (`BelegStueckliste.ValidateObjects()`)
     - Zuschläge (`BelegZuschlag.ValidateObjects()`)
     - Staffelrabatte (`BelegStaffelrabatt.ValidateObjects()`)
     - Zahlungskonditionen (`CalcAndValidateZDKs()`)
2. **Customizing / DCM-Hooks (`DcmContextBelegValidate`, `DcmContextBelegBeforeSave`):**
   - Feuert DCM-Ereignisse zur kundenindividuellen Vorvalidierung vor dem eigentlichen Schreibzugriff.
3. **Workflow- & Budgetprüfungen:**
   - Evaluierung von Workflow-Regeln (`WorkflowRule`, `WorkflowCondition`).
   - Kreditlimitprüfung (`_kreditlimitKonto`), Liefersperren (`_hatLiefersperreKonto`).
   - Im Einkauf: Budgetüberwachung (`BelegBudget`, Event `QuestionBudget`). Bei Überschreitung automatisches Parken (`_ekIstGeparktWegenBudget`).

---

### Phase 2: Speicherstatus & SaveMode
1. **Ermittlung des Zielstatus (`BelegSpeicherstatus`):**
   - Unterscheidet zwischen regulärem Speichern, Parken (`IstWechselParkenNachSpeichern`), Storno oder Vorab-Erfassung.
2. **Event `SaveModeCommit` & `WorkFlowSaveModeCommit`:**
   - Benachrichtigt externe Komponenten und UI über den endgültigen Speicherstatus.
3. **Zustandssicherung (`PushSaveStatus()`):**
   - Sichert den aktuellen internen Speicherstatus auf einem Stack für ein evtl. Rollback.

---

### Phase 3: Persistierung der Belegdaten (Datenbank-Schicht `BelegData`)
1. **Belegkopf (`SaveBelegkopf` -> `BelegData.InsertBeleg`):**
   - Nummernvergabe/Aktualisierung (`_belegnummer`, `_handle`, `_vorgangsHandle`).
   - Speichert Nettobetrag, Steuerbetrag, Bruttobetrag, Skontofähigkeit, Rabatte (1–3), Kopftexte und Metadaten.
   - Projektbelege: Speichert Zusatzdaten via `InsertBelegProjekt` und `InsertProjektInfo`.
   - Schlussrechnungen: Speichert Rechnungsblöcke via `SaveSchlussrechnung` und `InsertRechnungsblock`.
2. **Positionen & Stücklisten (`SavePositionen` -> `BelegData.InsertPosition`):**
   - Schreibt alle Belegpositionen (Mengen, Einzel-/Gesamtpreise, Rabatte, Erlöskonten).
   - Schreibt Stücklistenstrukturen (`BelegData.InsertStuecklistenelement`).
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
2. **Dispositionsfortschreibung (`BelegData.InsertUpdateDispositionsVerursacher`):**
   - Aktualisiert Bedarfsverursacher und Verursacherketten.
   - Erstellt/löscht Dispositionsartikel (`InsertDispoArtikel`, `DeleteDispoArtikel`).

---

### Phase 5: Statistiken, Historie & Vorgänge
1. **Statistik-Aktualisierung (`StatistikUpdate` / `_hatStatistikLauf`):**
   - Schreibt Hauptstatistik (`BelegData.InsertStatistikMain`).
   - Schreibt Kunden-/Lieferantenumsätze (`InsertStatistikKonto`, `InsertStatistikKontoGruppe`).
   - Schreibt Vertreterprovisionen (`InsertStatistikVertreter`, `_vkProvision`).
2. **Vorgangs-Statusmengen:**
   - Aktualisiert gelieferte, berechnete und offene Mengen im übergeordneten Vorgang (`InsertVorgangPositionsReferenz`).
3. **Archivierung & Änderungshistorie:**
   - Erstellt Revisionsstände für Belegkopf, Positionen und Picklisten (`SaveArchiv`, `SavePicklistenArchiv`, `MaxAenderungsNummer`).

---

### Phase 6: Rechnungswesen & Abschluss
1. **FIBU / Offene Posten Übergabe (`BelegReweInterface`, `BudgetueberwachungSaveRewe`):**
   - Bereitet Buchungssätze und OP-Einträge für das Rechnungswesen vor (`DcmContextBeforeReweUebergabe`, `DcmContextReweUebergabe`).
2. **Transaktionsabschluss (`BelegData.CommitQry`):**
   - Führt den finalen Commit der temporär gepufferten Abfragen und Tabellenoperationen aus.
3. **Aufräumarbeiten & PopSaveStatus:**
   - Rücksetzen von temporären Handles und Aktualisieren der `_belegRowVersion`.
