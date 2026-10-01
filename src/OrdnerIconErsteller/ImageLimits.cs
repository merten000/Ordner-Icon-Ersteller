namespace OrdnerIconErsteller
{
    /// <summary>
    /// Gemeinsame Groessengrenzen fuer Bilder, die zu einem Icon verarbeitet werden
    /// (Runde 2, Befund F-003). Die Werte entsprechen den bereits fuer KI-Bilder
    /// geltenden Grenzen in <see cref="AiIconService"/> (30 MB, 50 Megapixel); fuer
    /// manuell ausgewaehlte Bilder gab es vorher gar keine Grenze.
    /// Die Pruefung ist bewusst von System.Drawing getrennt und damit ohne
    /// Bilddaten testbar.
    /// </summary>
    internal static class ImageLimits
    {
        public const long MaxFileBytes = 30L * 1024 * 1024; // 30 MB
        public const long MaxPixelCount = 50_000_000;       // 50 Megapixel

        /// <summary>
        /// Prueft Dateigroesse und Abmessungen. Liefert null, wenn alles innerhalb der
        /// Grenzen liegt, sonst eine verstaendliche deutsche Fehlermeldung (ohne Pfad).
        /// <paramref name="width"/> und <paramref name="height"/> duerfen -1 sein, wenn
        /// sie noch nicht bekannt sind - dann wird nur die Dateigroesse geprueft.
        /// </summary>
        public static string? Check(long fileBytes, int width, int height)
        {
            if (fileBytes > MaxFileBytes)
            {
                return $"Das Bild ist mit {fileBytes / 1024 / 1024} MB zu groß für ein Icon " +
                       $"(erlaubt sind höchstens {MaxFileBytes / 1024 / 1024} MB). Bitte ein kleineres Bild wählen.";
            }

            if (width == -1 && height == -1)
            {
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                return "Das Bild hat unplausible Abmessungen und kann nicht verwendet werden.";
            }

            if ((long)width * height > MaxPixelCount)
            {
                return $"Das Bild hat mit {width} × {height} Pixeln zu viele Bildpunkte für ein Icon " +
                       $"(erlaubt sind höchstens {MaxPixelCount / 1_000_000} Megapixel). Bitte ein kleineres Bild wählen.";
            }

            return null;
        }
    }
}
