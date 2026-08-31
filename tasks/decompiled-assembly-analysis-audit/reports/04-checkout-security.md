# Linse 04 — Checkout-Attestation, Pfadgrenzen, Reparse-Schutz und Cleanup

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `bda1e165`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: projektgebundene Abfragen mit `targetType=project`, `targetPath=<repo-root-redacted>`. Checkout-, Cache- und Markerpfade werden redigiert.

## Abdeckung

Geprüft wurden `ExternalSourceRepositoryPathGuard`, `ExternalSourceCheckoutOwnership`, `ExternalSourceCheckoutAttestation`, Acquisition-/Reservation-Pfade und die sichere Cache-Materialisierung. Im Zentrum standen Ownership-Marker, Descendant-Prüfungen, Reparse-Points, erwartete Revision, File-Inventory/Hash, atomare Publikation und die Frage, ob fremde Pfade gelöscht werden können.

## Befundlage

Es wurde kein bestätigter S0–S2-Sicherheitsdefekt gefunden.

`ExternalSourceRepositoryPathGuard.cs:11-76` trennt Descendant-Prüfung von Reparse-Prüfungen auf dem Pfad und im Baum. `:141-162` verlangt für Owned-Checkout Staging-Grenze, sichere Pfade und gültigen Ownership-Marker, bevor Cleanup zugelassen wird. Der Löschpfad arbeitet marker-/ownership-gebunden und behandelt Reparse-Einträge nicht als normale rekursive Verzeichnisse.

`ExternalSourceCheckoutAttestation.cs:27-99` prüft erwarteten Checkout-Pfad, erwartete geladene Revision und Ownership erneut; `:101-200` bildet Transport- und Cache-Attestationen aus Clean/Dirty/Unverified-Zuständen. Der Acquirer prüft vor Transport, nach Transport und vor der Solution-Nutzung Pfadgrenzen, Reparse-Status, reguläre Solution-Datei und sichere Revision (`ExternalSourceRepositoryAcquirer.cs:350-470`).

Die vorhandenen Tests prüfen tatsächliche Reparse-Einträge, fremde/ersetzte Checkouts, verloren gegangene Ownership, idempotentes Dispose, ungültige Solutionpfade, fehlende oder falsche Attestation, Manifest-/Hash-/Dateisatz-Manipulation und fehlgeschlagene atomare Publikation. Damit ist der zentrale Fail-closed-Vertrag für die geprüften lokalen Zustände belegt.

## Abdeckungsgrenze CHK-001

- Typ: verbleibende Laufzeitabdeckung, kein bestätigter Produktdefekt
- Schweregrad: S3
- Umfang: U3 — adversariale Dateisystem-Races außerhalb der Testharness
- Konfidenz: mittel
- Evidenz: Statische Prüfungen und gezielte Reparse-/Ownership-Tests sind vorhanden. Ein unabhängiger Prozess, der zwischen Prüfung und Lösch-/Move-Operation gezielt einen Pfad umhängt, wurde in diesem Audit nicht gestartet.
- Auswirkung: Ein vollständiger OS-/Filesystem-Race-Nachweis kann aus Codelektüre und den vorhandenen Tests nicht abgeleitet werden. Das ist eine verbleibende Sicherheits-Assurance-Grenze, kein reproduzierter Fehler.
- Reproduktion: In einer isolierten Testumgebung zwischen Ownership-/Reparse-Prüfung und Cleanup eine kontrollierte Umbenennung bzw. Reparse-Mutation versuchen; erwartetes Ergebnis ist Abbruch ohne Zugriff außerhalb des Staging-Roots.
- Disposition: Dokumentiert; keine Änderung am Sicherheitscode im Audit-only-Scope.

## Verifikation

Die Kombination aus `ExternalSourceRepositoryAcquirerTests`, `ExternalSourceRepositoryCheckoutAttestationTests`, Cache-Writer-/Readback-Tests, `GiteaGitRepositoryCheckoutStatusTests` und Snapshot-Materializer-Tests deckt positive, negative und Cleanup-Pfade ab. Keine Zugangsdaten, externen URLs oder lokalen Pfade wurden in den Report übernommen.
