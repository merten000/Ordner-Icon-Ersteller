using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Baut bei leerem Text-Prompt ("Icon ohne Text" - Merten-Feedback nach v2.1.4)
    /// aus dem Inhalt des Zielordners selbst eine Beschreibung für die KI-Bilderzeugung,
    /// statt zwingend eine Texteingabe zu verlangen. Rein lesend - verändert nie etwas
    /// im Zielordner.
    ///
    /// Enthält der Ordner bereits ein Bild, wird dessen Pfad zusätzlich als
    /// "ReferenceImagePath" zurückgegeben - der Aufrufer (MainForm) liest es ein und
    /// übergibt es an AiIconService, damit der Google-Anbieter (der - anders als die
    /// hier verwendete OpenAI-Anbindung - Bild-Eingaben in derselben Anfrage
    /// unterstützt) sich beim neuen Icon inhaltlich/stilistisch daran orientieren kann.
    /// </summary>
    internal static class FolderContentScanner
    {
        // Dateien/Muster, die dieses Programm selbst im Zielordner anlegt bzw. die
        // zum Windows-Ordnersystem gehören - beim Scannen ignoriert, damit weder
        // eigene Zustands-/Sicherungsdateien noch bereits früher erzeugte KI-Icons
        // die Beschreibung oder das Referenzbild verfälschen.
        private static readonly string[] IgnoredExactNames =
        {
            "desktop.ini",
            "FolderIcon.ico",
        };

        internal const string GeneratedImagePrefix = "Icon-Original (von KI erzeugt)";

        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp",
        };

        private const int MaxListedFileNames = 25;

        // Großzügige, aber endliche Obergrenze für ein als Referenz eingelesenes,
        // bereits im Ordner vorhandenes Bild.
        private const long MaxReferenceImageBytes = 15 * 1024 * 1024; // 15 MB

        internal sealed class ScanResult
        {
            public string ImplicitPrompt { get; }
            public string? ReferenceImagePath { get; }

            public ScanResult(string implicitPrompt, string? referenceImagePath)
            {
                ImplicitPrompt = implicitPrompt;
                ReferenceImagePath = referenceImagePath;
            }
        }

        public static ScanResult Scan(string folderPath)
        {
            var relevantEntries = new List<string>();
            string? referenceImagePath = null;

            foreach (string path in Directory.EnumerateFileSystemEntries(folderPath))
            {
                string name = Path.GetFileName(path);
                if (IsIgnored(name))
                {
                    continue;
                }

                bool isDirectory = Directory.Exists(path);
                relevantEntries.Add(isDirectory ? name + "/" : name);

                if (referenceImagePath is null && !isDirectory && IsImageFile(name))
                {
                    try
                    {
                        long length = new FileInfo(path).Length;
                        if (length > 0 && length <= MaxReferenceImageBytes)
                        {
                            referenceImagePath = path;
                        }
                    }
                    catch (IOException)
                    {
                        // Datei gerade nicht lesbar (z.B. kurzzeitig gesperrt) - einfach
                        // überspringen, das Scannen darf dadurch nicht insgesamt fehlschlagen.
                    }
                }
            }

            string folderName = new DirectoryInfo(folderPath).Name;
            string prompt = BuildImplicitPrompt(folderName, relevantEntries, referenceImagePath is not null);

            return new ScanResult(prompt, referenceImagePath);
        }

        private static bool IsIgnored(string name)
        {
            if (name.StartsWith(".", StringComparison.Ordinal))
            {
                return true; // eigene Marker-/Sperr-/Zwischendateien beginnen immer mit "."
            }
            if (name.EndsWith(".before-ordnericonersteller.bak", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (name.StartsWith(GeneratedImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true; // frühere, von diesem Programm selbst erzeugte KI-Icons
            }
            foreach (string exact in IgnoredExactNames)
            {
                if (string.Equals(name, exact, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsImageFile(string name)
        {
            string ext = Path.GetExtension(name);
            foreach (string imageExt in ImageExtensions)
            {
                if (string.Equals(ext, imageExt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string BuildImplicitPrompt(string folderName, List<string> entries, bool hasReferenceImage)
        {
            var sb = new StringBuilder();
            sb.Append("Erstelle ein einfaches, minimalistisches Icon-Symbol, das gut zu einem Ordner mit dem Namen \"")
              .Append(folderName)
              .Append('"')
              .Append(" passt.");

            if (entries.Count > 0)
            {
                IEnumerable<string> namesToList = entries.Count > MaxListedFileNames
                    ? entries.Take(MaxListedFileNames)
                    : entries;
                sb.Append(" Der Ordner enthält u.a. folgende Dateien/Unterordner: ")
                  .Append(string.Join(", ", namesToList));
                if (entries.Count > MaxListedFileNames)
                {
                    sb.Append(" und weitere");
                }
                sb.Append('.');
            }
            else
            {
                sb.Append(" Der Ordner ist aktuell leer - orientiere dich ausschließlich am Ordnernamen.");
            }

            if (hasReferenceImage)
            {
                sb.Append(" Es wird zusätzlich ein Beispielbild aus dem Ordner mitgeliefert - orientiere dich " +
                          "an dessen Motiv/Stil für das neue Icon.");
            }

            sb.Append(" Das Ergebnis soll sich klar als kleines Ordner-Icon eignen: ein einzelnes, klar " +
                      "erkennbares Motiv vor einfachem Hintergrund, keine zusätzliche Schrift.");
            return sb.ToString();
        }
    }
}
