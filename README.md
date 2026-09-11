# Ordner-Icon-Ersteller (Version 2.0.1 – komplette Neuentwicklung)

## Änderungen nach der 1. ChatGPT-Prüfung (v2.0.0 → v2.0.1)

ChatGPT hat drei freigabeblockierende Fehler in v2.0.0 (Commit `f9a1f0e`) gefunden,
alle sind jetzt behoben:

1. **`desktop.ini` wurde nicht erkannt**: sie wurde als UTF-8 geschrieben, die
   klassische Windows-INI-API (`GetPrivateProfileString`) erkennt UTF-8 aber
   nicht zuverlässig. Jetzt wird Unicode (UTF-16LE mit BOM) verwendet.
2. **Vorhandene `desktop.ini`/`FolderIcon.ico`/Ordnerattribute wurden ohne
   Sicherung überschrieben**: `FolderIconService` sichert jetzt vor der
   allerersten Änderung an einem Ordner dessen Originalzustand (versteckte
   `.bak`-Dateien + ein kleines Marker-File) und stellt beim „Icon entfernen"
   genau diesen Originalzustand wieder her, statt einfach zu löschen.
3. **Kein Rollback bei Schreibfehlern**: Icon und `desktop.ini` werden jetzt
   erst vollständig in temporäre Dateien geschrieben und erst danach an ihren
   endgültigen Platz kopiert, damit bei einem Fehler mittendrin kein
   halbfertiger Zustand im Zielordner zurückbleibt.

Außerdem: die Registry-Statusabfrage (`ContextMenuService.IsRegistered`) kann
jetzt nicht mehr den Programmstart abbrechen (try/catch ergänzt), die zwei
unnötigen NuGet-Pakete (`System.Drawing.Common`, `Microsoft.Win32.Registry` –
beide bereits über das Windows-Desktop-Framework verfügbar) wurden entfernt,
und es gibt jetzt ein Testprojekt mit automatisierten Tests (siehe unten).

Windows-Tool, um einem Ordner per eigenem Bild ein individuelles Icon zu geben,
inklusive Eintrag im Explorer-Rechtsklick-Menü.

Diese Version ist ein **kompletter Neuanfang** (kein Code aus v1.0.0 wiederverwendet)
mit dem Ziel, die Antivirus-Fehlalarme der Vorversion nicht zu wiederholen.

## Warum die alte Version Ärger gemacht hat

Laut Notion-Eintrag hat die alte Setup.exe versucht, Sicherheitsfunktionen per
CMD zu umgehen – genau das Muster, das Virenscanner zuverlässig als verdächtig
einstufen (externe `cmd.exe`/`powershell.exe`/`reg.exe`-Aufrufe, Registry-Änderungen
über Subprozesse, ggf. Admin-Rechte-Anforderung).

## Was in dieser Version bewusst anders ist

- **Kein einziger externer Prozessaufruf.** Weder `cmd.exe` noch `powershell.exe`
  noch `reg.exe` werden irgendwo gestartet.
- **Keine Administratorrechte.** Der Rechtsklick-Menü-Eintrag wird ausschließlich
  unter `HKEY_CURRENT_USER\Software\Classes\...` angelegt (gilt nur für das
  eigene Benutzerkonto), direkt über die .NET-Registry-API
  (`Microsoft.Win32.Registry`), nicht über einen Registry-Editor-Aufruf.
- **Kein Internetzugriff, kein Nachladen von Code zur Laufzeit.** Alles läuft
  lokal und offline.
- **Standard-Windows-Mechanismus für Ordner-Icons** (`desktop.ini` +
  versteckte `.ico`-Datei im Zielordner) statt irgendwelcher Tricks.

## Funktionsumfang Version 1 (MVP)

1. Ordner auswählen (oder per Rechtsklick-Menü direkt übergeben)
2. Bild auswählen (PNG, JPG, BMP, ICO)
3. „Icon setzen" → erzeugt eine mehrstufige `.ico`-Datei (16/32/48/256 px) aus
   dem Bild und hinterlegt sie im Zielordner
4. „Icon entfernen" → macht die Änderung rückgängig
5. „Ins Rechtsklick-Menü integrieren" / „entfernen" → fügt einen Eintrag
   „Ordner-Icon ändern..." zum Rechtsklick-Menü von Ordnern hinzu (nur für den
   aktuellen Benutzer)

**Noch nicht enthalten** (laut deiner Auswahl bewusst auf später verschoben):
KI-generierte Icons, automatischer Icon-Vorschlag, fertiger Installer/Setup.exe.

## Projektstruktur

```
OrdnerIconErsteller/
├── src/
│   └── OrdnerIconErsteller/
│       ├── OrdnerIconErsteller.csproj
│       ├── Program.cs
│       ├── MainForm.cs
│       ├── FolderIconService.cs
│       ├── IconWriter.cs
│       └── ContextMenuService.cs
└── tests/
    └── OrdnerIconErsteller.Tests/
        ├── OrdnerIconErsteller.Tests.csproj
        ├── IconWriterTests.cs
        └── FolderIconServiceTests.cs
```

## Bauen (auf deinem Windows-PC, .NET 8 SDK vorausgesetzt)

Wichtiger Hinweis: Ich konnte dieses Projekt in meiner Cloud-Umgebung **nicht
kompilieren/testen**, weil mein Sandbox-Netzwerk keinen Zugriff auf
Microsoft-/NuGet-/Ubuntu-Paketserver hat (alle Downloadversuche wurden vom
Sicherheits-Proxy mit 403 blockiert). Der Code ist sorgfältig geschrieben und
sollte sich normal bauen lassen – bitte einmal `dotnet build` laufen lassen und
mir eventuelle Fehlermeldungen schicken, falls etwas nicht sofort durchläuft.

```powershell
cd src\OrdnerIconErsteller
dotnet build
```

(Es gibt jetzt auch `OrdnerIconErsteller.sln` im Hauptordner - `dotnet build`
direkt dort baut Haupt- und Testprojekt zusammen.)

Fertige, startbare EXE erzeugen (einzelne Datei, keine separate .NET-Installation
beim Nutzer nötig):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Die EXE liegt danach unter `src\OrdnerIconErsteller\publish\OrdnerIconErsteller.exe`.

## Automatisierte Tests ausführen

Vom Projekt-Hauptordner aus (nicht aus `src\OrdnerIconErsteller`):

```powershell
dotnet test
```

Das baut Haupt- und Testprojekt und führt die xUnit-Tests aus (u. a. der in
Punkt 2 oben beschriebene Sicherung/Wiederherstellung-Rundlauf).

## Test-Checkliste vor der ChatGPT-Prüfung

- [ ] `dotnet build` läuft ohne Fehler durch
- [ ] Programm startet ohne Argumente (leeres Formular)
- [ ] Ordner + Bild auswählen, „Icon setzen" → Explorer zeigt neues Icon (ggf. F5)
- [ ] „Icon entfernen" macht es rückgängig
- [ ] „Ins Rechtsklick-Menü integrieren" → Rechtsklick auf einen beliebigen
      Ordner zeigt „Ordner-Icon ändern...", Klick öffnet das Programm mit
      bereits eingetragenem Ordner
- [ ] „Aus Rechtsklick-Menü entfernen" entfernt den Eintrag wieder
- [ ] Kein UAC-Admin-Dialog erscheint an irgendeiner Stelle

## Nächste Schritte (nach deiner Freigabe)

- ChatGPT-Prüfung dieser Version
- danach ggf. Installer (Inno Setup o.ä., ohne CMD-Tricks) für die
  Veröffentlichung
- danach optional: KI-Icon-Erzeugung, automatischer Vorschlag
