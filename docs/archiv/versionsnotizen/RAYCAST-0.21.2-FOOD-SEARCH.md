# 0.21.2: Lebensmittelprüfung

- Neue IPC-Methoden `food.search` (mindestens zwei Zeichen, höchstens 30 Ergebnisse) und `food.get` (Einzeldetails).
- Serverseitige SQLite-Suche über foods, bls_foods und off_foods; keine vollständige Katalogmaterialisierung.
- Raycast: 300 ms Debounce, alte Ergebnisse ignorieren, Details erst nach Auswahl laden.
- Die bisherige API `nutrition.foods.list` bleibt aus Kompatibilitätsgründen erhalten, wird von Raycast aber nicht mehr verwendet.
- Kein Windows-/Raycast-Laufzeittest in dieser Umgebung.
