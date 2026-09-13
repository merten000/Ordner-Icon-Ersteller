using System.Windows.Forms;
using Microsoft.Win32;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Zeigt den Kosten-/Datenuebertragungs-Hinweis fuer die KI-Icon-Funktion
    /// EINMALIG an (beim allerersten jemals gespeicherten Schluessel, egal ob
    /// OpenAI oder Google) und merkt sich die Bestaetigung dauerhaft unter HKCU -
    /// genau wie beim Rechtsklick-Menue-Eintrag (siehe ContextMenuService) direkt
    /// ueber Microsoft.Win32.Registry, ohne Administratorrechte und ohne
    /// externen Prozessaufruf. Der Wert folgt der im Projekt bereits etablierten
    /// strikten "0"/"1"-Konvention (siehe FolderIconService.ParseStrictBool).
    /// </summary>
    internal static class AiConsentService
    {
        private const string SettingsKeyPath = @"Software\OrdnerIconErsteller";
        private const string ConsentValueName = "KiHinweisBestaetigt";

        /// <summary>
        /// Gibt true zurueck, wenn gespeichert werden darf (Hinweis bereits
        /// bestaetigt ODER der Benutzer bestaetigt ihn jetzt in diesem Aufruf).
        /// </summary>
        public static bool EnsureGiven(IWin32Window owner)
        {
            if (IsGiven())
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                owner,
                "Die KI-Icon-Funktion sendet deinen eingegebenen Text (Prompt) über das Internet " +
                "an den von dir gewählten Anbieter (OpenAI oder Google), damit dort ein Bild erzeugt " +
                "wird. Das ist die einzige Stelle in diesem Programm, die Daten an einen externen " +
                "Dienst überträgt - alle anderen Funktionen arbeiten vollständig offline.\n\n" +
                "Je nach Anbieter und Tarif können dabei Kosten entstehen, die von deinem eigenen " +
                "API-Konto abgerechnet werden. Dieses Programm hat darauf keinen Einfluss und zeigt " +
                "keine Kosten an. Bei Google kann eine einzelne Icon-Erzeugung automatisch bis zu " +
                "ZWEI kostenpflichtige Anfragen auslösen, falls die erste Antwort kein Bild enthält " +
                "und automatisch ein zweiter Versuch gestartet wird.\n\n" +
                "Möchtest du fortfahren und einen API-Schlüssel hinterlegen?",
                "Hinweis: Internetübertragung und mögliche Kosten",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return false;
            }

            SetGiven();
            return true;
        }

        private static bool IsGiven()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
                string? raw = key?.GetValue(ConsentValueName) as string;
                return raw == "1";
            }
            catch
            {
                // Im Zweifel den Hinweis lieber (erneut) anzeigen, statt ihn zu
                // unterschlagen.
                return false;
            }
        }

        private static void SetGiven()
        {
            using var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath, writable: true)
                ?? throw new InvalidOperationException(
                    $"Der Registry-Schlüssel \"HKCU\\{SettingsKeyPath}\" konnte nicht angelegt/geöffnet werden. " +
                    "Die Zustimmung konnte deshalb nicht gespeichert werden.");
            key.SetValue(ConsentValueName, "1");
        }
    }
}
