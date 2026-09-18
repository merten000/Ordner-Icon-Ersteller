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
    /// Enthält der Ordner bereits ein gültiges Bild, wird es hier bereits selbst über
    /// AiIconService.LoadReferenceImage vollständig geladen/validiert und als
    /// "ReferenceImage" (plus "ReferenceImagePath" für Anzeige/Zustimmungsdialog)
    /// zurückgegeben - der Aufrufer (MainForm) übergibt es nach ausdrücklicher
    /// Zustimmung an AiIconService, damit der Google-Anbieter (der - anders als die
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

        internal sealed class ScanResult
        {
            public string ImplicitPrompt { get; }
            public AiIconService.ReferenceImage? ReferenceImage { get; }
            public string? ReferenceImagePath { get; }

            public ScanResult(string implicitPrompt, AiIconService.ReferenceImage? referenceImage, string? referenceImagePath)
            {
                ImplicitPrompt = implicitPrompt;
                ReferenceImage = referenceImage;
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
        /// Referenzbild verwenden kann.
        /// </param>
        /// <remarks>
        /// Pruefbericht v2.2.1, weiterer Fehler 3: vorher wurde allein anhand von
        /// Endung und vorab gelesener Groesse der ERSTE Bildkandidat gewaehlt und
        /// sofort der Referenz-Satz in den Prompt uebernommen - scheiterte das
        /// anschliessende Einlesen (z.B. beschaedigtes Bild), blieb der bereits
        /// gebaute Prompt trotzdem stehen, und ein evtl. vorhandenes zweites,
        /// gueltiges Bild wurde nie gesucht. Jetzt werden ALLE Bildkandidaten
        /// gesammelt und der Reihe nach ueber AiIconService.LoadReferenceImage
        /// tatsaechlich geladen UND vollstaendig validiert, bis der erste
        /// tatsaechlich gueltige gefunden wird. Das <see cref="ScanResult.ImplicitPrompt"/>
        /// erwaehnt ein Referenzbild deshalb bewusst NICHT mehr selbst - das
        /// entscheidet erst der Aufrufer (MainForm), NACHDEM der Benutzer der
        /// konkreten Uebertragung dieses einen Bildes ausdruecklich zugestimmt hat
        /// (siehe AiConsentService.ConfirmReferenceImage, Pruefbericht v2.2.1,
        /// weiterer Fehler 4) - erst dann wird <see cref="WithReferenceImageMention"/>
        /// aufgerufen.
        /// </remarks>
        public static ScanResult Scan(string folderPath, bool allowReferenceImage = true)
        {
            var relevantEntries = new List<string>();
            bool hasMoreThanListed = false;
            AiIconService.ReferenceImage? referenceImage = null;
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
                // weiter (bis das erste tatsaechlich gueltige Bild gefunden ist).
                if (relevantEntries.Count < MaxListedFileNames)
                {
                    relevantEntries.Add(isDirectory ? name + "/" : name);
                }
                else
                {
                    hasMoreThanListed = true;
                }

                // Pruefbericht v2.7.1, weiterer Fehler 7: vorher wurden ERST alle
                // Bildpfade des gesamten Ordners gesammelt und danach geladen/geprueft
                // - bei sehr grossen Bildordnern konnte das unnoetig viel Speicher
                // belegen. Jetzt wird jeder Kandidat sofort waehrend der Enumeration
                // geladen/validiert; sobald ein gueltiges Bild gefunden ist, werden
                // keine weiteren Bildpfade mehr gesammelt oder geladen (die
                // Namens-Enumeration fuer die 25-Eintraege-/"und weitere"-Logik laeuft
                // trotzdem unveraendert weiter).
                if (allowReferenceImage && referenceImage is null && !isDirectory && IsImageFile(name))
                {
                    try
                    {
                        referenceImage = AiIconService.LoadReferenceImage(path);
                        referenceImagePath = path;
                    }
                    catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or IOException)
                    {
                        // Ungueltiger/beschaedigter/gerade nicht lesbarer Kandidat -
                        // naechsten versuchen statt das ganze Scannen abzubrechen.
                    }
                }
            }

            string folderName = new DirectoryInfo(folderPath).Name;
            string prompt = BuildImplicitPrompt(folderName, relevantEntries, hasMoreThanListed);

            return new ScanResult(prompt, referenceImage, referenceImagePath);
        }

        /// <summary>
        /// Haengt nachtraeglich den Hinweis auf ein mitgeliefertes Referenzbild an
        /// einen bereits gebauten Prompt an - wird von MainForm erst NACH der
        /// ausdruecklichen Zustimmung zu genau diesem Bild aufgerufen (siehe Kommentar
        /// bei <see cref="Scan"/>).
        /// </summary>
        internal static string WithReferenceImageMention(string basePrompt) =>
            basePrompt + " Es wird zusätzlich ein Beispielbild aus dem Ordner mitgeliefert - orientiere dich " +
            "an dessen Motiv/Stil für das neue Icon.";

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

        internal static string BuildImplicitPrompt(string folderName, List<string> entries, bool hasMoreThanListed)
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

            sb.Append(" Das Ergebnis soll sich klar als kleines Ordner-Icon eignen: ein einzelnes, klar " +
                      "erkennbares Motiv vor einfachem Hintergrund, keine zusätzliche Schrift.");
            return sb.ToString();
        }
    }
}
