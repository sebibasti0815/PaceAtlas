# Pace Atlas: Versionsregel

Nach jedem abgeschlossenen Änderungsschritt am Projekt `python3 bump-version.py` einmal ausführen, bevor `PaceAtlas.zip` aktualisiert wird. Die gemeinsame Versionsnummer steht in `Directory.Build.props` und gilt für WinForms, WinUI und Core. Die dritte Stelle zählt von 0 bis 10; auf `0.1.10` folgt `0.2.0`. Die erste Stelle nur nach ausdrücklicher Anweisung des Nutzers ändern.

`Directory.Build.props` und `bump-version.py` beim Verpacken in `PaceAtlas.zip` mitnehmen.
