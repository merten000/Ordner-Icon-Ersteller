namespace OrdnerIconErsteller
{
    /// <summary>
    /// Liest die im Ordner "legal" mitgelieferten rechtlichen Dokumente
    /// (Impressum, Datenschutz, Lizenz, Haftungsausschluss) ein, damit der
    /// "Rechtliches"-Bereich im Programm IMMER genau denselben Text zeigt wie die
    /// mitgelieferten Dateien - bewusst KEINE zweite, unabhängig gepflegte
    /// Textkopie im Code, um ein Auseinanderlaufen zwischen Programm und Dateien
    /// zu vermeiden (Merten-Feedback: "dieselben Dateien" sollen angezeigt werden).
    ///
    /// Die Dateien liegen sowohl im Entwicklungs-Build (per csproj als Inhalt mit
    /// CopyToOutputDirectory neben die EXE kopiert) als auch nach der Installation
    /// (per Installer-Skript in denselben relativen Unterordner "legal" neben die
    /// installierte EXE kopiert) im selben relativen Pfad - <see cref="AppContext.BaseDirectory"/>
    /// funktioniert deshalb zuverlässig in beiden Fällen.
    /// </summary>
    internal static class LegalDocumentService
    {
        internal enum Document
        {
            Impressum,
            Datenschutz,
            Lizenz,
            Haftungsausschluss,
            // Pruefbericht v2.7.1, Freigabeblocker 1: die Anwendung wird als
            // selbstenthaltene .NET-8-Windows-Anwendung veroeffentlicht und buendelt
            // dadurch Teile der .NET-Runtime mit aus. Nach dem offiziellen .NET Asset
            // Licensing Model muss jede .NET-Binaerdistribution (auch der Installer)
            // deren Lizenz und Drittanbieterhinweise mitfuehren. Beide Dateien liegen
            // unveraendert im Unterordner "legal\dotnet-runtime" (siehe csproj) und
            // stammen aus dem tatsaechlich verwendeten Runtime-Paket
            // Microsoft.NETCore.App.Runtime.win-x64 8.0.31.
            DotNetLizenz,
            Drittanbieterhinweise,
        }

        private const string LegalFolderName = "legal";
        private const string DotNetRuntimeSubfolderName = "dotnet-runtime";

        private static string[] RelativeSegmentsFor(Document document) => document switch
        {
            Document.Impressum => new[] { "IMPRESSUM.md" },
            Document.Datenschutz => new[] { "DATENSCHUTZ.md" },
            Document.Lizenz => new[] { "LIZENZ.md" },
            Document.Haftungsausschluss => new[] { "HAFTUNGSAUSSCHLUSS.md" },
            Document.DotNetLizenz => new[] { DotNetRuntimeSubfolderName, "LICENSE.TXT" },
            Document.Drittanbieterhinweise => new[] { DotNetRuntimeSubfolderName, "THIRD-PARTY-NOTICES.TXT" },
            _ => throw new ArgumentOutOfRangeException(nameof(document), document, "Unbekanntes Rechts-Dokument."),
        };

        private static string FileNameFor(Document document) => RelativeSegmentsFor(document)[^1];

        internal static string TitleFor(Document document) => document switch
        {
            Document.Impressum => "Impressum",
            Document.Datenschutz => "Datenschutzerklärung",
            Document.Lizenz => "Lizenz / Nutzungsbedingungen",
            Document.Haftungsausschluss => "Haftungsausschluss",
            Document.DotNetLizenz => ".NET-Lizenz (MIT)",
            Document.Drittanbieterhinweise => "Drittanbieterhinweise (.NET-Runtime)",
            _ => throw new ArgumentOutOfRangeException(nameof(document), document, "Unbekanntes Rechts-Dokument."),
        };

        /// <summary>
        /// Liest das angeforderte Dokument ein. Wirft eine gut lesbare
        /// <see cref="FileNotFoundException"/>, falls die Datei fehlt (z.B. bei einer
        /// unvollständigen Installation) - der Aufrufer (MainForm) zeigt diese
        /// Meldung dann statt eines unklaren Programmfehlers an.
        /// </summary>
        internal static string ReadText(Document document) => ReadText(document, AppContext.BaseDirectory);

        /// <summary>
        /// Überladung mit explizitem Basisverzeichnis statt <see cref="AppContext.BaseDirectory"/> -
        /// ausschließlich damit Tests eine fehlende Installation (leeres Verzeichnis)
        /// nachstellen können, ohne den echten Programmordner zu beeinflussen. Die
        /// eigentliche Anwendung ruft ausschließlich die Überladung ohne
        /// Basisverzeichnis-Parameter oben auf.
        /// </summary>
        internal static string ReadText(Document document, string baseDirectory)
        {
            string path = GetPath(document, baseDirectory);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Die Datei \"{FileNameFor(document)}\" wurde nicht gefunden. Die Installation dieses " +
                    "Programms scheint unvollständig zu sein - bitte das Programm neu installieren.", path);
            }

            return File.ReadAllText(path);
        }

        internal static string GetPath(Document document) => GetPath(document, AppContext.BaseDirectory);

        internal static string GetPath(Document document, string baseDirectory)
        {
            var segments = new List<string> { baseDirectory, LegalFolderName };
            segments.AddRange(RelativeSegmentsFor(document));
            return Path.Combine(segments.ToArray());
        }
    }
}
