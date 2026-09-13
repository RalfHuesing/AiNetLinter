---
status: ready
execution_mode: autonomous
open_questions: []
---

# Konzept: Opaque MCP-Handoff-Handles

## Ziel

MCP-Toolketten verwenden für navigierbare Symbole ausschließlich kurze opaque
Handles im Format `h:<Zaehler>`. Der Handle ist eine technische Adresse: Der
umgebende Content bleibt für Menschen lesbar und enthält weiterhin Symbolart,
Name oder Signatur sowie bei Bedarf Pfad und Position. Agenten geben den Handle
ohne Transformation an einen passenden Folgeaufruf weiter.

Direkte semantische Eingaben bleiben gültig, etwa DocCommentIds,
qualifizierte Namen und `Datei:Zeile[:Spalte]`. Der Handle ersetzt keine dieser
Eingabeformen und ändert keine Roslyn-Analyse.

## Architektur

`HandoffHandleRegistry` ist die einzige öffentliche Handoff-Grenze. Sie ordnet
die interne Resolveridentität einem Handle zu und restauriert ihn vor der
bestehenden Resolverlogik. Die Registry besitzt keine Symbol-, Target- oder
Snapshot-Semantik und hält nur die beiden ordinal verglichenen Zuordnungen:

```text
interne Resolveridentität -> h:<Zaehler> -> MCP-Content
MCP-Parameter h:<Zaehler> -> interne Resolveridentität -> bestehender Resolver
```

Ein Mapping ist während eines Hostlaufs bijektiv und stabil. Registry-Mappings
bleiben bis zum Host-Shutdown erhalten, auch wenn eine Solution- oder
Assembly-Session aus ihrem TTL-Cache entfernt wird. Nach einem Host-Neustart
sind frühere Handles nicht mehr auflösbar und liefern `HANDOFF_UNKNOWN`; der
Agent ermittelt das Symbol erneut über einen passenden Producer.

## Handleformat und Lebensdauer

Gültig ist ausschließlich `h:` gefolgt von mindestens einem Zeichen aus
`abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ`.
Groß-/Kleinschreibung ist signifikant. Der Zähler beginnt bei `h:a` und zählt
von rechts weiter (`h:z -> h:0`, `h:9 -> h:A`, `h:Z -> h:aa`). Ein
Windows-Pfad wie `h:\...` ist kein Handle.

Die globale High-Water-Mark wird atomar und prozessübergreifend unter
`LocalApplicationData/RalfHuesing/AiNetLinter/handoff-counter.json` gespeichert.
Sie enthält nur Formatversion und den zuletzt reservierten Zählerwert, keine
Symbole, Targets oder Pfade. Werte werden nie wiederverwendet. Kann der State
nicht sicher gelesen oder geschrieben werden, wird kein neuer Handle erzeugt
und `HANDOFF_COUNTER_UNAVAILABLE` zurückgegeben.

## Öffentlicher Vertrag

- Producer geben für jeden navigierbaren Eintrag `handoffId: h:...` aus und
  lassen fachlichen Begleittext unverändert.
- Consumer restaurieren ein syntaktisch gültiges Handle zentral, bevor sie die
  bestehende Fachlogik aufrufen.
- Fehlformatierte Handle-Eingaben erhalten `INVALID_HANDOFF`; unbekannte,
  syntaktisch gültige Handles erhalten `HANDOFF_UNKNOWN`.
- Fehler geben keine interne Resolveridentität aus. Fehler des bestehenden
  Resolvers wie `TARGET_MISMATCH` oder `STALE_SNAPSHOT` bleiben erhalten.
- Content-only und Zero-Transformation gelten für alle Toolketten.

## Abdeckung

Die Registry wird an den Ausgabe- und Eingabegrenzen der Symbol-, Struktur-,
Kontext-, Call-Tree-, Metrik-, Typ- und Assembly-Tools verwendet. Dazu zählen
unter anderem `find_symbol`, `get_file_skeleton`, `find_references`,
`get_call_tree`, `get_type_hierarchy`, `find_implementations`,
`get_class_structure`, `metrics_lookup`, `get_feature_context`,
`get_test_context`, `inspect_assembly`, `get_assembly_context` und
`find_duplicates`.

Nicht betroffen sind Paging-Tokens, Dateibaum- und Namespace-Parameter,
reine Metrik- und Hotspotabfragen sowie Positionsreferenzen außerhalb eines
Symbol-Handoff-Vertrags.

## Akzeptanzkriterien

- Nur `h:`-Handles werden öffentlich dokumentiert, ausgegeben und als Handle
  verarbeitet.
- Keine interne Resolveridentität gelangt in MCP-Content oder Fehlertexte.
- Ein Handle aus jedem fachlich passenden Producer funktioniert unverändert an
  jedem passenden Consumer, auch für Assembly-Symbole.
- Direkte semantische Eingaben funktionieren weiterhin.
- Unit- und Integrationstests decken Mapping, ungültige Eingaben, Renderer und
  repräsentative Source- sowie Assembly-Toolketten ab.
- Build, MCP-Verify und die Non-Stress-Testgates sind vor Release grün.

Freigabestatus: Der Umsetzungsscope ist freigegeben; offene Fragen bestehen
nicht.
