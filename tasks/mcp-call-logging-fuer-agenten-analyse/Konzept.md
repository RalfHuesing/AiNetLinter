# mcp call logging

um analysieren zu können ob und wie gut der mcp server funktioniert brauchen wir logging.
log relativ zur .exe von ainetlinter (haben wir ja schon)
unterverzeichnis pro projekt in dem ainetlinter gestartet wurde
dann unterverzeichnis mit datum
beispiel: "log\<foo.bar.projekt.solution>\2026-08-05\ ..... "
wir müssen mindestens wissen: was wurde aufgerufen, was wurde zurückgeliefert

denke bitte mit was noch sinnvoll wäre

ziel: wir können später gezielt analysen machen. wir wissen welches lokale code projekt verwendet wurde, was der agent gefragt hat und was er geliefert bekommen hat.
der quellcode des code projektes kann in der zwischenzeit einen ganz anderen stand haben, wir müssen also loggen was für eine anaylse relevant ist.

verzeichnis / datei struktur sinnvoll wählen

vermutlich kommen da sehr viele log informationen bei raus

# allgemein logging verbessern

haben wir überhaupt logging integriert?

wenn nein: serilog relativ zur .exe "log" (oder logs? wie man es machen würde)

wir brauchen auch allgemeines projekt spezifisches logging .. tool geladen, kann nicht laden weil xyz usw. usf.
kann ich aktuell überhaupt nicht diagnostizieren
