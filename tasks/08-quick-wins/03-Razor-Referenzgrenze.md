# 03 – Razor/Markup außerhalb der C#-Referenzsuche

**Status:** Abdeckungslücke belegt. **Schweregrad:** mittel. **Geltung:** Blazor-Solutions und Markup-Bindungen.

## Evidenz

- SAN `find_references({targetPath:"C:\\Daten\\Entwicklung\\SAN\\San.smart.Planner.Platform\\San.smart.Planner.Platform.slnx",symbolIdentifier:"h:ciYd",scopeType:"all"})` meldete keine C#-Aufrufstellen für `DialogLayoutTestPage.OpenDialog()`. `search_pattern` in `**/*.razor` fand `DialogLayoutTestPage.razor:14` mit `OnClick="OpenDialog"`. `get_index_scope` zählt 111 Razor-Dateien außerhalb des C#-Symbolgraphen. [Protokoll](SAN.md)
- KnowHowToAI zählt 48 Razor-Dateien. Die gezielte Suche nach `CurrentUser`/`DashboardQuery` fand darin keine Treffer; das ist ein begrenzter Negativbefund, keine allgemeine Entwarnung. [Protokoll](KnowHowToAI.md)

## Auswirkung und Einordnung

„Keine Aufrufstellen“ aus `find_references` bedeutet bei Blazor nicht „keine Nutzung“. Der SAN-Fall ist ein Markup-Aufruf in einer Test-Layoutseite; `test_only` wird dadurch nicht automatisch falsch. Unkritische Fälle können durch die notwendige zweite Markup-Suche Zeit und Tokens kosten; kritische Fälle könnten als unbenutzt missverstanden werden.

## Grundlage für ein mögliches Umsetzungskonzept

Zwei mögliche Ausbaustufen sind zu entscheiden:

1. **Kleine Vertragsverbesserung:** Bei unvollständiger Abdeckung einen expliziten Hinweis `razor_markup_not_indexed` und einen konkreten Such-Folgeschritt ausgeben.
2. **Semantische Abdeckung:** Razor-Bindungen in Referenz- und Advisory-Ermittlung integrieren. Das braucht eine eigene Genauigkeitsprüfung für Namensgleichheit, Komponenten-Basen und generierten Code.

**Abnahmekriterium:** `OpenDialog` darf in einer Nutzerantwort nicht ohne Markup-Grenzhinweis als referenzlos erscheinen. Ob es als `test_only` gilt, muss anhand des Projekt-/Seitenscopes separat bewertet werden.
