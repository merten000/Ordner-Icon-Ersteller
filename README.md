# Ordner-Icon-Ersteller (Version 2.0.0 – komplette Neuentwicklung)

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
└── src/
    └── OrdnerIconErsteller/
        ├── OrdnerIconErsteller.csproj
        ├── Program.cs
        ├── MainForm.cs
        ├── FolderIconService.cs
        ├── IconWriter.cs
        └── ContextMenuService.cs
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

Fertige, startbare EXE erzeugen (einzelne Datei, keine separate .NET-Installation
beim Nutzer nötig):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Die EXE liegt danach unter `src\OrdnerIconErsteller\publish\OrdnerIconErsteller.exe`.

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
