# PaceAtlas 0.21.1 – Ernährung und Schnellnotizen

- Raycast: **Schnellnotiz**, **Lebensmittel prüfen**, **Mahlzeit erfassen**.
- IPC: `notes.create`, `nutrition.foods.list`, `nutrition.meals.list`, `nutrition.meal.create`.
- Lebensmittelregeln werden nach Priorität und Musterlänge wie in WinUI ausgewertet. GL ist pro 100 g ausgewiesen, nicht als Portionswert.
- Mahlzeit-Schnellerfassung unterstützt zunächst **ein Lebensmittel pro Eintrag**; für mehrere Zutaten bitte WinUI verwenden. Es wird direkt `Store.SaveMeal` verwendet.
- Schnellnotizen werden als Einträge vom Typ `Notiz` im bestehenden Entry-Store gespeichert und im IPC-Verlauf angezeigt. Der WinUI-Verlauf muss für diesen neuen Typ noch angepasst werden.
- Alle Änderungen verwenden die zentrale Datenhaltung. Kein separater Raycast-Datenspeicher.
- Nicht auf Windows kompiliert oder zur Laufzeit getestet.
