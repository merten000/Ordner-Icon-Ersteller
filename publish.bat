@echo off
cd /d "%~dp0"
echo ============================================
echo  Ordner-Icon-Ersteller wird gebaut (Release)
echo ============================================
dotnet publish "src\OrdnerIconErsteller\OrdnerIconErsteller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "src\OrdnerIconErsteller\publish"
echo.
if %ERRORLEVEL% NEQ 0 (
    echo FEHLER beim Bauen - siehe Meldungen oben.
) else (
    echo Fertig! Die fertige EXE liegt jetzt in:
    echo   src\OrdnerIconErsteller\publish
)
echo.
pause
