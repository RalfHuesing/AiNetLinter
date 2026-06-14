Wie könnte man das prüfen schneller machen ohne features zu verlieren?

Konkret dauert ein run in einem größeren projekt mehrere minuten.

ich hatte folgendes überlegt:
wenn der agent was am code ändert fasst der ja maximal nur "eine handvoll" dateien an, und selbst wenn es 20 sind sind das nur ein bruchtel der gesamten dateien.
ich dachte es gibt eine cache oder so (checksum oder so) und wir merken uns was wir gemeldet haben (müsste man natürlich auch false-positves irgendwie rausbekommen, der könnte mehrere tests machen .. evtl. solange melden wie fehler da sind egal ob checksum gleich), jedenfalls: von 100% dateien "verwerfen" wir im speicher 99% und die prüfung müsste schneller gehen.
macht das sinn? bringt das wirklich was?
verlieren wir features? (datei übergreifende prüfungen?! haben wir sowas überhaupt?)

oder hast du andere ideen?