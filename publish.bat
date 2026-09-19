@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo  Ordner-Icon-Ersteller wird gebaut (Release)
echo ============================================
dotnet publish "src\OrdnerIconErsteller\OrdnerIconErsteller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "src\OrdnerIconErsteller\publish"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo FEHLER beim Bauen - siehe Meldungen oben.
    pause
    exit /b 1
)

rem Pruefbericht v2.7.4 (ChatGPT-Gegenpruefung zu Commit 7a37a28), weiterer Punkt 2:
rem das Skript fuehrte bisher nur "dotnet publish" aus - die neu angelegten
rem release-extras (LIESMICH.md, rechtliche PDFs) wurden weder kopiert noch
rem anderweitig referenziert. Werden hier nach dem Publish zusaetzlich in
rem denselben Ordner kopiert, damit ein aus diesem Ordner erstelltes Download-
rem Paket sie automatisch enthaelt.
rem
rem Pruefbericht v2.7.4 (ChatGPT-Gegenpruefung zu Commit 85d1912), Freigabeblocker 1:
rem die vorherige Fassung pruefte "%ERRORLEVEL%" INNERHALB des "if exist (...)"-
rem Blocks - CMD liest einen ganzen Klammerblock beim Erreichen der aeusseren
rem if-Zeile auf einmal ein und ersetzt %ERRORLEVEL% dabei bereits mit seinem
rem Wert ZU DIESEM ZEITPUNKT (hier: noch der Erfolgswert von "dotnet publish"
rem oben, 0) - nicht mit dem erst danach durch xcopy gesetzten Wert. Ein
rem fehlgeschlagener xcopy wurde dadurch trotzdem als Erfolg gemeldet (per
rem Test mit einem absichtlich fehlschlagenden xcopy bestaetigt). "if errorlevel 1"
rem ist dagegen eine eigene CMD-Syntax, die den Wert erst zur Laufzeit der
rem Pruefung selbst liest, nicht beim Einlesen des Blocks - deshalb hier statt
rem "if %ERRORLEVEL% NEQ 0" verwendet. Ausserdem bricht ein fehlender
rem "release-extras"-Ordner jetzt ebenfalls mit exit /b 1 ab statt nur uebersprungen
rem zu werden, da das ausgelieferte Paket diese Dateien zwingend enthalten soll.
echo.
if not exist "release-extras" (
    echo.
    echo FEHLER: Ordner "release-extras" wurde nicht gefunden - das ausgelieferte
    echo Paket soll LIESMICH.md und die rechtlichen PDFs zwingend enthalten.
    pause
    exit /b 1
)
echo Kopiere release-extras (LIESMICH.md, rechtliche PDFs) in den Publish-Ordner...
xcopy "release-extras\*" "src\OrdnerIconErsteller\publish\" /s /e /i /y >nul
if errorlevel 1 (
    echo.
    echo FEHLER beim Kopieren von release-extras - siehe Meldungen oben.
    pause
    exit /b 1
)

echo.
echo Fertig! Die fertige EXE und alle Zusatzdateien liegen jetzt in:
echo   src\OrdnerIconErsteller\publish
echo.
pause
exit /b 0
