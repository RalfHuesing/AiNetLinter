# 05 – MCP-Fehler eindeutig kennzeichnen

**Status:** beobachteter Vertragswiderspruch. **Schweregrad:** mittel für automatisierte Clients, niedrig für manuelles Lesen.

## Evidenz

KnowHowToAI `find_references({targetPath:"C:\\Daten\\Entwicklung\\Ralf\\KnowHowToAI\\KnowHowToAI.slnx",symbolIdentifier:"h:ciNc"})` antwortete mit `[ERROR]: SYMBOL_NOT_FOUND` und `Status: operation=error`, das MCP-Resultat hatte aber `isError=false`. Der Text war verständlich; ein Client, der nur `isError` prüft, erkennt den Fehlschlag nicht. [Protokoll](KnowHowToAI.md)

Bei der erneuten Strukturierungsprüfung während des Solution-Ladens lieferten mehrere semantische Tools `[INFO]: Server laedt die Solution noch. Bitte in wenigen Sekunden erneut versuchen.` ebenfalls bei `isError=false`. Das ist möglicherweise ein legitimer Retry-Zustand, aber kein nutzbares semantisches Ergebnis.

## Grundlage für ein mögliches Umsetzungskonzept

- Fehlerklassen (`operation=error`) und Retry-Zustände (`loading`) als getrennte Content-Status definieren.
- Für echte Fehler `isError=true` prüfen; das Projekt verlangt weiterhin genau einen nichtleeren Text-Content-Block und keinen `structuredContent`-Vertrag.
- Ein Client muss ohne Heuristik zwischen Ergebnis, erneut versuchen und endgültigem Fehler unterscheiden können.

**Abnahmekriterium:** Ein `SYMBOL_NOT_FOUND`-Aufruf ist sowohl im Text als auch über `isError` als Fehler erkennbar. Ein Loading-Retry hat einen expliziten Status und keinen semantischen Trefferinhalt.
