using System;
using System.Collections.Generic;
using System.IO;
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

        /// <param name="folderPath">Der zu durchsuchende Zielordner.</param>
        /// <param name="allowReferenceImage">
        /// Nur wenn true wird ueberhaupt nach einem vorhandenen Bild gesucht und
        /// dessen Pfad zurueckgegeben. Pruefbericht v2.2.0 (weiterer Fehler: "bei
        /// OpenAI behauptet der Prompt, ein Referenzbild werde mitgesendet, obwohl
        /// OpenAI es vollstaendig verwirft"): das im Prompt-Text erwaehnte
        /// Referenzbild wird bisher unabhaengig vom gewaehlten Anbieter erwaehnt,
        /// obwohl nur Google es tatsaechlich als Bild-Eingabe erhaelt (siehe
        /// AiIconService.ReferenceImage). Der Aufrufer (MainForm) uebergibt hier
        /// deshalb explizit, ob der aktuell gewaehlte Anbieter ueberhaupt ein
        /// Referenzbild verwenden kann - nur dann taucht es im Prompt auf und wird
        /// als ReferenceImagePath zurueckgegeben.
        /// </param>
        public static ScanResult Scan(string folderPath, bool allowReferenceImage = true)
        {
            var relevantEntries = new List<string>();
            bool hasMoreThanListed = false;
            string? referenceImagePath = null;

            foreach (string path in Directory.EnumerateFileSystemEntries(folderPath))
            {
                string name = Path.GetFileName(path);
                if (IsIgnored(name))
                {
                    continue;
                }

                bool isDirectory = Directory.Exists(path);

                // Nur die ersten MaxListedFileNames Namen tatsaechlich behalten
                // (Pruefbericht v2.2.0, weiterer Fehler: "speichert trotz der
                // 25-Namen-Grenze zunaechst saemtliche Eintraege") - bei sehr
                // grossen Ordnern wuerde sonst unnoetig eine komplette, ggf. sehr
                // lange Liste im Speicher aufgebaut, obwohl spaeter ohnehin nur die
                // ersten 25 Namen in den Prompt uebernommen werden. Die Suche nach
                // einem Referenzbild laeuft unabhaengig davon ueber alle Eintraege
                // weiter, bis eines gefunden ist.
                if (relevantEntries.Count < MaxListedFileNames)
                {
                    relevantEntries.Add(isDirectory ? name + "/" : name);
                }
                else
                {
                    hasMoreThanListed = true;
                }

                if (allowReferenceImage && referenceImagePath is null && !isDirectory && IsImageFile(name))
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
            string prompt = BuildImplicitPrompt(folderName, relevantEntries, hasMoreThanListed, referenceImagePath is not null);

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

        internal static string BuildImplicitPrompt(string folderName, List<string> entries, bool hasMoreThanListed, bool hasReferenceImage)
        {
            var sb = new StringBuilder();
            sb.Append("Erstelle ein einfaches, minimalistisches Icon-Symbol, das gut zu einem Ordner mit dem Namen \"")
              .Append(folderName)
              .Append('"')
              .Append(" passt.");

            if (entries.Count > 0)
            {
                sb.Append(" Der Ordner enthält u.a. folgende Dateien/Unterordner: ")
                  .Append(string.Join(", ", entries));
                if (hasMoreThanListed)
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
