using System;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    internal static class Program
    {
        // Pruefbericht v2.7.3 (Gesamtdurchlauf), kleinerer Punkt 5: der Deinstaller
        // entfernte bisher nur den Explorer-Kontextmenue-Registryzweig - die im Windows-
        // Anmeldeinformations-Manager gespeicherten API-Schluessel (OpenAI/Google) sowie
        // der Zustimmungs-Registrywert blieben nach einer Deinstallation zurueck, ohne
        // dass das dokumentiert oder eine Option dafuer angeboten wurde. Der Installer
        // (installer\OrdnerIconErsteller.iss, [UninstallRun]) ruft die EXE deshalb jetzt
        // mit genau diesem Argument auf, BEVOR die Dateien entfernt werden - das laeuft
        // ohne sichtbares Fenster (WinExe ohne Application.Run) und ohne cmd.exe/
        // powershell.exe/reg.exe, genau wie der Rest des Programms.
        private const string UninstallCleanupArg = "--uninstall-cleanup";

        /// <summary>
        /// Haupteinstiegspunkt.
        /// Wird das Programm über das Explorer-Rechtsklick-Menü aufgerufen,
        /// übergibt Windows den angeklickten Ordner als erstes Argument (%1).
        /// Es wird bewusst NICHTS über cmd.exe, powershell.exe oder reg.exe
        /// ausgeführt - alle Aktionen laufen direkt im eigenen Prozess.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], UninstallCleanupArg, StringComparison.Ordinal))
            {
                RunUninstallCleanup();
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string? startFolder = null;
            if (args.Length > 0 && System.IO.Directory.Exists(args[0]))
            {
                startFolder = args[0];
            }

            Application.Run(new MainForm(startFolder));
        }

        /// <summary>
        /// Entfernt die vom Programm angelegten, ueber die normale Dateideinstallation
        /// hinausgehenden Spuren: die beiden API-Schluessel im Windows-Anmeldeinformations-
        /// Manager und den Zustimmungs-Registrywert (inkl. seines Registry-Schluessels,
        /// falls dieser dadurch leer wird). Bewusst robust einzeln abgefangen - ein
        /// bereits geloeschter/nicht vorhandener Eintrag ist kein Fehler, und ein
        /// fehlschlagender Schritt darf die anderen nicht verhindern, insbesondere nicht
        /// den eigentlichen Deinstallationsvorgang selbst aufhalten.
        /// </summary>
        private static void RunUninstallCleanup()
        {
            TrySilently(ApiKeyStore.DeleteOpenAiKey);
            TrySilently(ApiKeyStore.DeleteGoogleKey);
            TrySilently(AiConsentService.DeleteStoredConsent);
        }

        private static void TrySilently(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Deinstallations-Aufraeumung darf die eigentliche Deinstallation nie
                // verhindern oder mit einem Fehlerdialog unterbrechen - ohne sichtbares
                // Fenster gaebe es ohnehin niemanden, der einen Dialog sehen koennte.
            }
        }
    }
}
