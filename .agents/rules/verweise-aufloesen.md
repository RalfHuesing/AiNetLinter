---
rule: verweise-aufloesen
applies_to: "**/*.md"
---

# Verweise müssen auflösen — beim Entfernen wird ausgeschrieben

Die Dateien hier verweisen dicht aufeinander: `orchestrator.md` → `spec.md`
§X, `SKILL.md` → `../../spec.md` §Y, `templates/**` → `../spec.md` §Z. Ein
Agent liest genau eine dieser Dateien und folgt dem Verweis, um das Detail
zu finden. Zeigt der Verweis ins Leere oder auf den falschen Abschnitt,
merkt das niemand — der Agent arbeitet einfach ohne die Regel weiter.

## Regeln

1. **Jeder `§`-Verweis muss auf einen existierenden Abschnitt zeigen.**
   Verschiebst oder nummerierst du Abschnitte um, prüfe alle Verweise
   darauf im ganzen Repo, nicht nur in der bearbeiteten Datei.
2. **Ein Verweis, dessen Ziel du entfernst, wird ausgeschrieben.** Stand
   in Datei A „Ablauf identisch zu B §3", und B verschwindet, dann gehört
   der **tatsächliche Wortlaut aus B** nach A — nicht eine Kurzfassung aus
   dem Gedächtnis. Der eingebundene Text war Teil der Spezifikation, nicht
   nur ein Hinweis.
3. **Paraphrasieren ist kein Auflösen.** Wird aus vier Regeln ein
   zusammenfassender Halbsatz, ist die Regel weg, auch wenn die Datei
   danach „vollständig" aussieht. Besonders tückisch: ein Satz, der die
   Überschrift wiederholt, ohne den Mechanismus zu nennen
   („Vorab-Klassifikation: Vorab-Prüfung, ob …").
4. **Ein Verweis auf ein Detail bleibt nur gültig, solange das Detail
   existiert.** Kürzt du Abschnitt §9 zusammen, prüfe, wer „Details siehe
   §9" sagt — diese Stellen brauchen dann den Inhalt selbst.

## Warum

Verweise sind hier bewusst die Alternative zu Duplikation: eine Regel
steht an genau einer Stelle, alle anderen zeigen darauf. Das hält die
Dateien schlank, macht sie aber empfindlich — eine gebrochene Kante
entfernt stillschweigend eine Regel aus dem Workflow, ohne dass irgendwo
etwas fehlt oder rot wird.

## Prüfung vor dem Commit

```bash
grep -rhoE "§[0-9]+(\.[0-9]+)*" --include=*.md . | sort -u
```

Jede ausgegebene Nummer muss es als Abschnitt geben. Gegenprobe für eine
konkrete Datei:

```bash
grep -n "^#\{2,3\} " dev-loop/drift-loop/spec.md
```
