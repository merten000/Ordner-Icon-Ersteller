@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo  Ordner-Icon-Ersteller: Release bauen
echo ============================================
echo Schritte: 1) Publish-Ordner  2) Installer  3) Pruefsummen

set "PUBLISH_DIR=src\OrdnerIconErsteller\publish"
set "ISS=installer\OrdnerIconErsteller.iss"

rem --- 1) Publish: normales self-contained Publish in einen Ordner (kein Single-File)
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if exist "%PUBLISH_DIR%" goto fehler_publishdir
if exist "installer\output" rmdir /s /q "installer\output"

dotnet publish "src\OrdnerIconErsteller\OrdnerIconErsteller.csproj" -c Release -r win-x64 --self-contained true -o "%PUBLISH_DIR%"
if errorlevel 1 goto fehler_publish
if not exist "%PUBLISH_DIR%\OrdnerIconErsteller.exe" goto fehler_publish

if not exist "release-extras" goto fehler_extras
xcopy "release-extras\*" "%PUBLISH_DIR%\" /s /e /i /y >nul
if errorlevel 1 goto fehler_extras

rem --- 2) Installer mit Inno Setup 6 (feste Standardpfade, kein Raten)
set "ISCC="
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if "%ISCC%"=="" goto fehler_iscc

"%ISCC%" "%ISS%"
if errorlevel 1 goto fehler_installer

rem --- 3) SHA-256-Pruefsummen zum Festhalten des Ergebnisses
echo.
echo Pruefsummen:
certutil -hashfile "%PUBLISH_DIR%\OrdnerIconErsteller.exe" SHA256 | findstr /v /c:"hash" /c:"CertUtil"
for %%F in ("installer\output\OrdnerIconErsteller-Setup-*.exe") do (
    echo %%~nxF
    certutil -hashfile "%%F" SHA256 | findstr /v /c:"hash" /c:"CertUtil"
)

echo.
echo FERTIG. Installer liegt in: installer\output
pause
exit /b 0

:fehler_publishdir
echo FEHLER: Alter Publish-Ordner konnte nicht entfernt werden (Datei noch geoeffnet?).
goto ende_fehler
:fehler_publish
echo FEHLER: dotnet publish fehlgeschlagen oder OrdnerIconErsteller.exe fehlt - siehe oben.
goto ende_fehler
:fehler_extras
echo FEHLER: release-extras fehlt oder konnte nicht kopiert werden.
goto ende_fehler
:fehler_iscc
echo FEHLER: Inno Setup 6 (ISCC.exe) nicht in den Standardordnern gefunden.
goto ende_fehler
:fehler_installer
echo FEHLER: Inno Setup meldet einen Fehler - siehe oben. Exitcode steht in der Ausgabe.
:ende_fehler
echo.
pause
exit /b 1
