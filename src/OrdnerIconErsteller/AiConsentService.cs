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

        // Versionsnummer des Hinweistexts (Pruefbericht v2.2.0, P1: eine bereits
        // gespeicherte Zustimmung zur alten, nur auf den Text-Prompt bezogenen
        // Fassung deckte nicht ab, dass seit "Icon ohne Text" bei leerem Prompt
        // zusaetzlich Ordner-/Dateinamen und bei Google ein im Ordner gefundenes
        // Referenzbild uebertragen werden. Ein bereits als "1" gespeicherter,
        // aelterer Wert zaehlt deshalb bewusst NICHT mehr als gueltige
        // Zustimmung fuer diese neue Fassung - beim naechsten Aufruf wird der
        // (aktualisierte) Hinweis einmalig erneut angezeigt. Bei einer
        // kuenftigen inhaltlichen Aenderung des Hinweistexts einfach wieder
        // hochzaehlen.
        private const string CurrentConsentVersion = "2";

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
                "Die KI-Icon-Funktion sendet Daten über das Internet an den von dir gewählten Anbieter " +
                "(OpenAI oder Google), damit dort ein Bild erzeugt wird. Das ist die einzige Stelle in " +
                "diesem Programm, die Daten an einen externen Dienst überträgt - alle anderen Funktionen " +
                "arbeiten vollständig offline.\n\n" +
                "Übertragen wird dein eingegebener Text (Prompt). Lässt du das Textfeld leer (\"Icon ohne " +
                "Text\"), durchsucht das Programm stattdessen den Zielordner selbst und überträgt Ordner- " +
                "und Dateinamen daraus als Ersatz-Beschreibung. Enthält der Zielordner bereits ein Bild und " +
                "hast du Google als Anbieter gewählt, wird zusätzlich dieses Bild als Vorlage an Google " +
                "übertragen (bei OpenAI nicht, dort wird kein Bild gesendet).\n\n" +
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

        /// <summary>
        /// Fragt VOR der eigentlichen Übertragung ausdrücklich nach, ob genau DIESES im
        /// Zielordner gefundene Bild als Referenz mitgesendet werden soll (Prüfbericht
        /// v2.2.1, weiterer Fehler 4: die allgemeine, versionierte Zustimmung oben deckt
        /// nur pauschal ab, DASS irgendein Bild übertragen werden könnte - sie nennt nie
        /// den konkreten Dateinamen und bietet keine Möglichkeit, im Einzelfall
        /// abzulehnen, obwohl bei mehreren Bildern im Ordner die Auswahl von der nicht
        /// garantierten Dateisystem-Reihenfolge abhängt). Diese Abfrage wird bewusst
        /// NICHT dauerhaft gespeichert - sie erscheint bei jeder Erzeugung erneut, bei
        /// der ein gültiges Referenzbild gefunden wurde, und ergänzt (ersetzt nicht) die
        /// allgemeine Zustimmung oben.
        /// </summary>
        public static bool ConfirmReferenceImage(IWin32Window owner, string imagePath)
        {
            DialogResult result = MessageBox.Show(
                owner,
                $"Im Zielordner wurde folgendes Bild gefunden:\n\n{imagePath}\n\n" +
                "Soll genau dieses Bild zusätzlich als Vorlage an Google übertragen werden?",
                "Referenzbild mitsenden?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            return result == DialogResult.Yes;
        }

        private static bool IsGiven()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath);
                string? raw = key?.GetValue(ConsentValueName) as string;
                return raw == CurrentConsentVersion;
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
            key.SetValue(ConsentValueName, CurrentConsentVersion);
        }
    }
}
