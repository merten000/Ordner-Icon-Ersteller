using System;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    internal static class Program
    {
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
    }
}
