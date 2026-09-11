using System;
using Microsoft.Win32;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Trägt einen Eintrag ins Explorer-Rechtsklick-Menü für Ordner ein bzw.
    /// entfernt ihn wieder. Es wird ausschließlich der eigene Zweig des
    /// aktuellen Benutzers verwendet (HKEY_CURRENT_USER\Software\Classes),
    /// dafür sind KEINE Administratorrechte nötig. Der Zugriff erfolgt
    /// direkt über die .NET-Registry-API (Microsoft.Win32.Registry) -
    /// es wird zu keinem Zeitpunkt reg.exe, cmd.exe oder powershell.exe
    /// aufgerufen. Genau solche externen Aufrufe hatten die Vorversion
    /// beim Virenscanner auffällig gemacht.
    /// </summary>
    internal static class ContextMenuService
    {
        private const string VerbKeyPath = @"Software\Classes\Directory\shell\OrdnerIconErsteller";
        private const string VerbName = "Ordner-Icon ändern...";

        public static bool IsRegistered()
        {
            using var key = Registry.CurrentUser.OpenSubKey(VerbKeyPath);
            return key != null;
        }

        public static void Register()
        {
            string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Pfad der eigenen EXE konnte nicht ermittelt werden.");

            using var verbKey = Registry.CurrentUser.CreateSubKey(VerbKeyPath, writable: true)
                ?? throw new InvalidOperationException("Registry-Schlüssel konnte nicht angelegt werden.");

            verbKey.SetValue("", VerbName);
            verbKey.SetValue("Icon", exePath);

            using var commandKey = verbKey.CreateSubKey("command", writable: true)
                ?? throw new InvalidOperationException("Registry-Unterschlüssel 'command' konnte nicht angelegt werden.");

            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }

        public static void Unregister()
        {
            Registry.CurrentUser.DeleteSubKeyTree(VerbKeyPath, throwOnMissingSubKey: false);
        }
    }
}
