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
echo.
echo Kopiere release-extras (LIESMICH.md, rechtliche PDFs) in den Publish-Ordner...
if exist "release-extras" (
    xcopy "release-extras\*" "src\OrdnerIconErsteller\publish\" /s /e /i /y >nul
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo FEHLER beim Kopieren von release-extras - siehe Meldungen oben.
        pause
        exit /b 1
    )
) else (
    echo Hinweis: Ordner "release-extras" wurde nicht gefunden und uebersprungen.
)

echo.
echo Fertig! Die fertige EXE und alle Zusatzdateien liegen jetzt in:
echo   src\OrdnerIconErsteller\publish
echo.
pause
exit /b 0
