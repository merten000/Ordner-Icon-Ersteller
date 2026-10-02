# Release bauen, installieren, deinstallieren

Voraussetzung: .NET-8-SDK und Inno Setup 6 (Standardinstallationsordner).

1. Doppelklick auf `build-release.bat` (Repository-Hauptordner).
   Ergebnis: `installer\output\OrdnerIconErsteller-Setup-<Version>.exe` und
   SHA-256-Pruefsummen in der Konsole. Das Skript bricht bei jedem Fehler ab.
2. Installieren: Setup-EXE starten (nur fuer den aktuellen Benutzer, keine Adminrechte).
   Installationsort: `%LOCALAPPDATA%\Programs\Ordner-Icon-Ersteller`.
3. Starten: Startmenue "Ordner-Icon-Ersteller" (oder Haken am Installationsende).
4. Deinstallieren: Windows "Apps & Features" oder Startmenue-Eintrag. Die Deinstallation
   entfernt Dateien, Kontextmenue-Eintrag sowie gespeicherte API-Schluessel und
   Zustimmungswert (`--uninstall-cleanup`).

Aufbau: normales self-contained Publish in einen Ordner (kein Single-File, keine
selbstentpackende EXE); der Installer kopiert den ganzen Ordner. Version steht an zwei
Stellen: `OrdnerIconErsteller.csproj` (`Version`) und `installer\OrdnerIconErsteller.iss`
(`MyAppVersion`).
