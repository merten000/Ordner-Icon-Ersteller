using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Auswahl des KI-Anbieters für die Icon-Erzeugung.
    /// </summary>
    public enum AiImageProvider
    {
        OpenAi,
        Google,
    }

    /// <summary>
    /// Erzeugt ein Bild ueber die Bild-APIs von OpenAI oder Google (Gemini) anhand
    /// eines vom Benutzer eingegebenen Text-Prompts. Dies ist die EINZIGE Stelle im
    /// Programm, die absichtlich Daten ueber das Internet an einen externen Dienst
    /// sendet (der Prompt-Text) - im Gegensatz zum Rest des Programms, das
    /// vollstaendig offline arbeitet. Es werden ausschliesslich die eingebauten
    /// .NET-Klassen HttpClient und System.Text.Json verwendet - keine zusaetzlichen
    /// NuGet-Pakete, kein externer Prozessaufruf (siehe Sicherheitsregel des
    /// Projekts: niemals cmd.exe/powershell.exe o.ae. aufrufen).
    ///
    /// Die API-Schluessel werden ausschliesslich ueber <see cref="ApiKeyStore"/>
    /// aus dem Windows-Anmeldeinformations-Manager gelesen - nie aus einer
    /// Konfigurationsdatei im Klartext.
    ///
    /// Die reine Antwort-Parsing-/Validierungslogik ist bewusst in interne,
    /// von HTTP entkoppelte Methoden ausgelagert (Build...RequestBody,
    /// Parse...ImageResponse, DecodeAndReencodeAsPng) - dadurch koennen
    /// AiIconServiceTests diese Pfade (gueltige/beschaedigte/uebergrosse/falsch
    /// typisierte Antworten, Gemini-Thought-Bilder, der tatsaechlich erzeugte
    /// Request-Body) offline und ohne echte, kostenpflichtige Netzwerkaufrufe
    /// pruefen (siehe Pruefbericht-Punkt 7 zur KI-Icon-Gegenpruefung).
    /// </summary>
    internal static class AiIconService
    {
        // Grosszuegiges Timeout, da Bildgenerierung bei beiden Anbietern durchaus
        // 20-60 Sekunden dauern kann.
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(120),
        };

        // Grosszuegige, aber endliche Obergrenze fuer das dekodierte Bild.
        internal const int MaxImageBytes = 30 * 1024 * 1024; // 30 MB

        // Obergrenze fuer die rohe HTTP-Antwort (Base64 + JSON-Overhead ist ca.
        // 1.35x groesser als die dekodierten Bilddaten) - wird VOR dem Einlesen
        // des Response-Bodys geprueft (Content-Length) bzw. waehrend des Lesens
        // durchgesetzt, nicht erst nach vollstaendigem Einlesen/Dekodieren
        // (Pruefbericht Punkt 5).
        private const int MaxResponseBytes = 45 * 1024 * 1024; // 45 MB

        // Obergrenze fuer die Laenge des Base64-Strings selbst, geprueft BEVOR
        // Convert.FromBase64String das komplette Bytearray alloziert.
        private const int MaxBase64Chars = (MaxImageBytes / 3 + 1) * 4 + 4;

        // HttpCompletionOption.ResponseHeadersRead bedeutet, dass HttpClient.Timeout
        // (oben, 120 Sekunden) nur die Zeit bis zum Eintreffen der HTTP-Header abdeckt -
        // das anschliessende Einlesen des Antwort-Bodys in ReadResponseTextWithLimitAsync
        // ist dadurch NICHT mehr zeitlich begrenzt und koennte theoretisch unbegrenzt
        // haengen bleiben (z.B. bei einer sehr langsamen/stockenden Verbindung). Microsoft
        // dokumentiert dafuer ausdruecklich einen eigenen CancellationToken beim Lesen des
        // Streams (ChatGPT-Gegenpruefung von v2.1.2, Freigabeblocker 2).
        private static readonly TimeSpan ResponseBodyReadTimeout = TimeSpan.FromSeconds(120);

        // Sinnvolle Obergrenze fuer die Gesamtpixelzahl eines Icon-Bildes -
        // verhindert z.B. ein absichtlich winziges, aber extrem hochaufgeloestes
        // "Bomben"-Bild.
        private const long MaxPixelCount = 50_000_000; // 50 Megapixel

        /// <summary>
        /// Ein bereits im Zielordner vorhandenes Bild, das als visuelle Referenz an
        /// den KI-Anbieter mitgeschickt wird (siehe "Icon ohne Text" - Merten-Feedback
        /// nach v2.1.4, ermittelt von <see cref="FolderContentScanner"/>). Wird aktuell
        /// nur von Google (Gemini) tatsächlich als Bild-Eingabe unterstützt - die hier
        /// verwendete OpenAI-Bild-API (images/generations) nimmt keine Bild-Eingabe
        /// entgegen, dort wirkt sich ein Referenzbild nur über die Text-Beschreibung
        /// aus, die es in <see cref="FolderContentScanner"/> bereits erwähnt.
        /// </summary>
        internal readonly struct ReferenceImage
        {
            public byte[] Bytes { get; }
            public string MimeType { get; }

            public ReferenceImage(byte[] bytes, string mimeType)
            {
                Bytes = bytes;
                MimeType = mimeType;
            }
        }

        // Deckt sich bewusst mit FolderContentScanner.MaxReferenceImageBytes - hier
        // trotzdem eine eigene, unabhaengige Grenze (Pruefbericht v2.2.0, P1: "das
        // Einlesen besitzt keine eigene Groessenbegrenzung"), damit diese Methode
        // auch dann sicher bleibt, wenn sie kuenftig von woanders als nur dem
        // FolderContentScanner-Pfad aus aufgerufen wird.
        private const long MaxReferenceImageBytes = 15 * 1024 * 1024; // 15 MB

        /// <summary>
        /// Liest eine bereits im Zielordner vorhandene Bilddatei ein, damit sie als
        /// <see cref="ReferenceImage"/> an <see cref="GenerateIconImageAsync"/>
        /// übergeben werden kann.
        ///
        /// Pruefbericht v2.2.0 (P1: "Referenzbild wird nicht als Bild validiert"):
        /// vorher wurden Endung und Dateigroesse nur vom aufrufenden
        /// FolderContentScanner geprueft, hier selbst gar nicht - eine beliebige,
        /// nur passend benannte Datei waere unveraendert an Google geschickt
        /// worden. Jetzt wird die Groesse hier selbst nochmal begrenzt (siehe
        /// <see cref="MaxReferenceImageBytes"/>) und die Datei tatsaechlich als
        /// Bild dekodiert - der MIME-Typ kommt danach vom erkannten Bildformat,
        /// nicht mehr blind von der Dateiendung.
        ///
        /// Pruefbericht v2.2.1, Freigabeblocker 2: vorher wurde die Groesse per
        /// <see cref="FileInfo.Length"/> geprueft und die Datei ANSCHLIESSEND separat mit
        /// <see cref="File.ReadAllBytes(string)"/> eingelesen - zwischen beiden Zugriffen
        /// haette die Datei ausgetauscht/vergroessert werden koennen (TOCTOU), und
        /// <see cref="File.ReadAllBytes(string)"/> selbst besass keine eigene Obergrenze.
        /// Jetzt wird EIN einziges, geoeffnetes <see cref="FileStream"/>-Handle sowohl fuer
        /// die Laengenpruefung als auch fuer das Einlesen verwendet (ein Ersetzen der Datei
        /// unter demselben Pfad kann dieses Handle nicht mehr beeinflussen), und
        /// <see cref="ReadAllBytesWithLimit"/> bricht das Lesen selbst hart ab, sobald mehr
        /// als <see cref="MaxReferenceImageBytes"/> Byte ankommen - unabhaengig davon, was
        /// die anfangs gelesene Laenge behauptet hat.
        ///
        /// Zusaetzlich erzwingt diese Methode jetzt - genau wie
        /// <see cref="DecodeAndReencodeAsPng"/> fuer von der KI gelieferte Bilder - eine
        /// vollstaendige Dekodierung durch Neu-Encodieren als PNG. Praktisch nachgestellt
        /// im Pruefbericht: eine um 1.200 Byte abgeschnittene, sonst gueltige PNG wurde von
        /// <c>Image.FromStream(..., validateImageData:true)</c> plus Zugriff auf
        /// <c>RawFormat</c> alleine NICHT erkannt (GDI+ dekodiert dabei teils nur
        /// Header/Metadaten) - das erzwungene Neu-Encodieren verlangt dagegen wirklich
        /// jede Bildzeile und schlaegt bei einer abgeschnittenen Datei fehl. Zurueckgegeben
        /// werden deshalb bewusst die NEU encodierten PNG-Bytes (MIME-Typ immer
        /// "image/png"), nicht die urspruenglich eingelesenen Rohdaten.
        /// </summary>
        public static ReferenceImage LoadReferenceImage(string filePath)
        {
            byte[] bytes;
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long length = fileStream.Length;
                if (length <= 0 || length > MaxReferenceImageBytes)
                {
                    throw new InvalidOperationException(
                        $"Die Datei \"{filePath}\" ist leer oder größer als " +
                        $"{MaxReferenceImageBytes / 1024 / 1024} MB und wird deshalb nicht als Referenzbild verwendet.");
                }

                bytes = ReadAllBytesWithLimit(fileStream, MaxReferenceImageBytes);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"Die Datei \"{filePath}\" konnte nicht gelesen werden: {ex.Message}", ex);
            }

            try
            {
                using var inputStream = new MemoryStream(bytes, writable: false);
                using var image = Image.FromStream(inputStream, useEmbeddedColorManagement: false, validateImageData: true);

                if (image.Width <= 0 || image.Height <= 0
                    || (long)image.Width * image.Height > MaxPixelCount)
                {
                    throw new InvalidOperationException(
                        $"Die Datei \"{filePath}\" hat unplausible Abmessungen " +
                        $"({image.Width}x{image.Height}) und wird deshalb nicht als Referenzbild verwendet.");
                }

                using var outputStream = new MemoryStream();
                image.Save(outputStream, ImageFormat.Png);
                return new ReferenceImage(outputStream.ToArray(), "image/png");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                throw new InvalidOperationException(
                    $"Die Datei \"{filePath}\" ist kein gültiges Bild und wird deshalb nicht als " +
                    "Referenzbild verwendet.", ex);
            }
        }

        /// <summary>
        /// Liest einen Stream vollständig ein, bricht dabei aber SOFORT mit einer
        /// <see cref="InvalidOperationException"/> ab, sobald mehr als <paramref
        /// name="maxBytes"/> Byte gelesen wurden - unabhängig von einer vorher separat
        /// geprüften Länge (siehe Kommentar bei <see cref="LoadReferenceImage"/>).
        /// </summary>
        private static byte[] ReadAllBytesWithLimit(Stream stream, long maxBytes)
        {
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[81920];
            long total = 0;
            int read;
            while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw new InvalidOperationException(
                        $"Die Datei ist beim Einlesen größer als {maxBytes / 1024 / 1024} MB geworden und wird " +
                        "deshalb nicht als Referenzbild verwendet.");
                }
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }

        /// <summary>
        /// Erzeugt ein Bild anhand des Prompts beim gewaehlten Anbieter und liefert
        /// die rohen Bilddaten als kanonisches PNG zurueck. Der Aufrufer speichert
        /// diese Bytes in die Zieldatei und uebergibt sie an die bestehende
        /// Pipeline (FolderIconService) - genau wie bei einem manuell ausgewaehlten
        /// Bild. Die Bytes werden hier bereits vollstaendig dekodiert und neu als
        /// PNG encodiert (siehe <see cref="DecodeAndReencodeAsPng"/>) - der Aufrufer
        /// muss sie also nicht selbst nochmal validieren, bevor er sie dauerhaft
        /// speichert.
        ///
        /// <paramref name="referenceImage"/> ist optional und wird nur bei
        /// <see cref="AiImageProvider.Google"/> tatsächlich als Bild-Eingabe verwendet
        /// (siehe <see cref="ReferenceImage"/>).
        ///
        /// Runde 2 (Befund F-001): <paramref name="cancellationToken"/> bricht eine laufende
        /// Anfrage kontrolliert ab (Verbindungsaufbau, Warten auf Antwort, Lesen des
        /// Antwortkoerpers, auch zwischen dem ersten und einem zweiten Google-Versuch).
        /// Ein vom Aufrufer angeforderter Abbruch endet mit einer
        /// <see cref="OperationCanceledException"/>; die bestehenden Zeitueberschreitungs-
        /// Meldungen bleiben davon unberuehrt.
        /// </summary>
        public static async Task<byte[]> GenerateIconImageAsync(AiImageProvider provider, string prompt, ReferenceImage? referenceImage = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Es wurde kein Text fuer das gewuenschte Icon ermittelt.", nameof(prompt));
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] imageBytes = provider switch
            {
                AiImageProvider.OpenAi => await GenerateWithOpenAiAsync(prompt, cancellationToken).ConfigureAwait(false),
                AiImageProvider.Google => await GenerateWithGoogleAsync(prompt, referenceImage, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter KI-Anbieter."),
            };

            return DecodeAndReencodeAsPng(provider, imageBytes);
        }

        /// <summary>
        /// Prueft, dass die vom Anbieter gelieferten Bytes tatsaechlich ein
        /// vollstaendig dekodierbares Bild sind, und liefert eine neu encodierte,
        /// kanonische PNG-Datei zurueck - unabhaengig davon, welches Format/welchen
        /// MIME-Typ der Anbieter tatsaechlich geliefert hat.
        ///
        /// Wichtig (Pruefbericht Punkt 4): <see cref="Image.FromStream(Stream)"/>
        /// plus Zugriff auf Width/Height genuegt NICHT als Vollstaendigkeitspruefung
        /// - GDI+ dekodiert dabei im Wesentlichen nur Header/Metadaten. Bei der
        /// Gegenpruefung wurde eine um 1.200 Byte abgeschnittene, sonst gueltige PNG
        /// davon nicht erkannt. Das erzwungene Neu-Encodieren nach PNG (<see
        /// cref="Image.Save(Stream, ImageFormat)"/>) verlangt dagegen wirklich jede
        /// Bildzeile - eine abgeschnittene/beschaedigte Datei schlaegt dabei fehl.
        /// Gleichzeitig entsteht dadurch garantiert eine ECHTE PNG-Datei, selbst
        /// wenn ein Anbieter faelschlich einen anderen MIME-Typ mit "image/"-Praefix
        /// gemeldet haette.
        /// </summary>
        internal static byte[] DecodeAndReencodeAsPng(AiImageProvider provider, byte[] imageBytes)
        {
            if (imageBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{provider} hat leere Bilddaten geliefert.");
            }

            if (imageBytes.Length > MaxImageBytes)
            {
                throw new InvalidOperationException(
                    $"{provider} hat ein ungewöhnlich großes Bild geliefert " +
                    $"({imageBytes.Length / 1024 / 1024} MB) - das wird zur Sicherheit abgelehnt.");
            }

            try
            {
                using var inputStream = new MemoryStream(imageBytes, writable: false);
                using var image = Image.FromStream(inputStream, useEmbeddedColorManagement: false, validateImageData: true);

                if (image.Width <= 0 || image.Height <= 0
                    || (long)image.Width * image.Height > MaxPixelCount)
                {
                    throw new InvalidOperationException(
                        $"{provider} hat ein Bild mit unplausiblen Abmessungen geliefert " +
                        $"({image.Width}x{image.Height}).");
                }

                using var outputStream = new MemoryStream();
                image.Save(outputStream, ImageFormat.Png);
                return outputStream.ToArray();
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                throw new InvalidOperationException(
                    $"{provider} hat Daten geliefert, die sich nicht vollständig als Bild lesen/neu " +
                    "speichern lassen (unvollständige oder beschädigte Antwort).", ex);
            }
        }

        private static async Task<byte[]> GenerateWithOpenAiAsync(string prompt, CancellationToken cancellationToken)
        {
            string? apiKey = ApiKeyStore.ReadOpenAiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Es ist noch kein OpenAI-API-Schluessel hinterlegt. Bitte zuerst unter " +
                    "\"KI-Einstellungen\" einen Schluessel eingeben.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations")
            {
                Content = new StringContent(JsonSerializer.Serialize(BuildOpenAiRequestBody(prompt)), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            string responseText = await ReadResponseTextWithLimitAsync(response, ResponseBodyReadTimeout, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenAI hat die Anfrage abgelehnt (HTTP {(int)response.StatusCode}): {ExtractOpenAiError(responseText)}");
            }

            return ParseOpenAiImageResponse(responseText);
        }

        /// <summary>
        /// "gpt-image-1" gilt laut aktueller OpenAI-Dokumentation als veraltet
        /// (siehe https://developers.openai.com/api/docs/guides/image-generation).
        /// "gpt-image-2.5-flare" ist das fuer schnelle Alltags-Bilderzeugung
        /// empfohlene aktuelle Modell - fuer die hier erzeugten einfachen
        /// Ordner-Icons passender als "sunburst" (auf praezise Bildbearbeitung
        /// ausgelegt). GPT-Bildmodelle liefern die Bilddaten immer als Base64 -
        /// ein "response_format"-Parameter (nur fuer DALL-E relevant) entfaellt
        /// dafuer; "output_format" legt stattdessen das Dateiformat fest.
        /// Als eigene Methode extrahiert, damit ein Test den tatsaechlich
        /// erzeugten Request-Body pruefen kann, ohne eine echte Anfrage zu senden
        /// (Pruefbericht Punkt 7).
        /// </summary>
        internal static object BuildOpenAiRequestBody(string prompt) => new
        {
            model = "gpt-image-2.5-flare",
            prompt,
            size = "1024x1024",
            n = 1,
            output_format = "png",
        };

        /// <summary>
        /// Liest die JSON-Antwort von OpenAI's Bild-API defensiv (TryGetProperty
        /// statt GetProperty, explizite ValueKind-Pruefungen) und liefert die
        /// dekodierten Bild-Bytes. Von der HTTP-Anfrage entkoppelt, damit Tests
        /// gueltige, unvollstaendige und falsch typisierte Antworten offline
        /// pruefen koennen.
        /// </summary>
        internal static byte[] ParseOpenAiImageResponse(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                if (!doc.RootElement.TryGetProperty("data", out JsonElement data)
                    || data.ValueKind != JsonValueKind.Array
                    || data.GetArrayLength() == 0
                    || !data[0].TryGetProperty("b64_json", out JsonElement b64Element)
                    || b64Element.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException("OpenAI hat kein Bild in der Antwort geliefert.");
                }

                string base64 = b64Element.GetString()
                    ?? throw new InvalidOperationException("OpenAI hat kein Bild in der Antwort geliefert.");
                return DecodeBase64WithLimit(base64, "OpenAI");
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                throw new InvalidOperationException(
                    "Die Antwort von OpenAI hatte ein unerwartetes Format und konnte nicht gelesen werden.", ex);
            }
        }

        private static async Task<byte[]> GenerateWithGoogleAsync(string prompt, ReferenceImage? referenceImage, CancellationToken cancellationToken)
        {
            string? apiKey = ApiKeyStore.ReadGoogleKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Es ist noch kein Google-API-Schluessel hinterlegt. Bitte zuerst unter " +
                    "\"KI-Einstellungen\" einen Schluessel eingeben.");
            }

            // Gemini-Bildmodelle behandeln einen kurzen/mehrdeutigen Prompt (z. B. nur
            // "Katze") manchmal als Bitte um eine Rückfrage/Beschreibung statt als
            // Anweisung zur Bilderzeugung und liefern dann NUR Text statt eines Bildes
            // zurück (kein Fehler, HTTP 200 - siehe GenerateWithGoogleOnceAsync unten).
            // Google selbst empfiehlt in seinem Prompting-Leitfaden ausformulierte,
            // beschreibende Sätze statt bloßer Stichworte; zusätzlich wird über
            // "system_instruction" verlangt, ausschließlich mit einem Bild zu
            // antworten. Liefert der erste Versuch trotzdem nur Text, wird EINMAL
            // automatisch mit einer noch deutlicheren Formulierung wiederholt, bevor
            // ein Fehler (inkl. der Text-Antwort des Modells) angezeigt wird.
            (byte[]? imageBytes, string? textOnlyResponse) = await GenerateWithGoogleOnceAsync(apiKey, BuildGoogleImagePrompt(prompt), referenceImage, cancellationToken)
                .ConfigureAwait(false);
            if (imageBytes is not null)
            {
                return imageBytes;
            }

            cancellationToken.ThrowIfCancellationRequested();

            string retryPrompt = BuildGoogleImagePrompt(prompt) +
                " This is very important: your entire response must be a single generated image. " +
                "Do not write any words, do not ask questions, do not explain - respond with the image only.";
            (imageBytes, string? retryTextOnlyResponse) = await GenerateWithGoogleOnceAsync(apiKey, retryPrompt, referenceImage, cancellationToken)
                .ConfigureAwait(false);
            if (imageBytes is not null)
            {
                return imageBytes;
            }

            string modelReply = retryTextOnlyResponse ?? textOnlyResponse ?? string.Empty;
            throw new InvalidOperationException(
                "Google hat auch nach einem zweiten Versuch kein Bild geliefert, nur Text. " +
                "Versuch es mit einer ausführlicheren, beschreibenden Formulierung statt einzelner " +
                "Stichworte (z. B. \"ein einfaches, minimalistisches Symbol einer roten Katze\" statt nur \"Katze\")." +
                (string.IsNullOrWhiteSpace(modelReply) ? string.Empty : $"\n\nAntwort von Google: {modelReply}"));
        }

        /// <summary>
        /// Beschreibendes Prompt-Template nach Googles eigener Prompting-Empfehlung
        /// (ausformulierte Sätze statt Stichwortlisten) für zuverlässigere
        /// Bildantworten statt Text-Rückfragen.
        /// </summary>
        private static string BuildGoogleImagePrompt(string userPrompt) =>
            $"Generate a single icon-style image of: {userPrompt}. " +
            "Do not include any explanatory text in your response - output only the image.";

        /// <summary>
        /// Der tatsaechliche Request-Body fuer einen einzelnen Google-Aufruf, als
        /// eigene Methode extrahiert, damit ein Test Modellname/Systemanweisung/
        /// Prompt-Feld pruefen kann, ohne eine echte Anfrage zu senden
        /// (Pruefbericht Punkt 7).
        /// </summary>
        internal static object BuildGoogleRequestBody(string effectivePrompt, ReferenceImage? referenceImage = null)
        {
            var parts = new List<object>();
            if (referenceImage is ReferenceImage refImage)
            {
                // Das Referenzbild MUSS vor dem Text-Teil stehen, damit Gemini es
                // eindeutig als Grundlage für die Bearbeitung/Erzeugung erkennt statt
                // als bloßen Anhang (siehe Googles eigene Beispiele für "image editing").
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = refImage.MimeType,
                        data = Convert.ToBase64String(refImage.Bytes),
                    },
                });
            }
            parts.Add(new { text = effectivePrompt });

            return new
            {
                system_instruction = new
                {
                    parts = new object[]
                    {
                        new
                        {
                            text = "You are an image generator for desktop folder icons. Always respond with a " +
                                   "generated image and no descriptive text, even for short or ambiguous prompts. " +
                                   "Never ask clarifying questions.",
                        },
                    },
                },
                contents = new object[]
                {
                    new { parts = parts.ToArray() },
                },
                generationConfig = new
                {
                    responseModalities = new[] { "TEXT", "IMAGE" },
                },
            };
        }

        /// <summary>
        /// Ein einzelner Aufruf der Google-Bild-API. Liefert die Bild-Bytes, oder -
        /// wenn NUR Text ohne Bild zurückkam - null zusammen mit dieser Text-Antwort
        /// (für die Fehlermeldung/den Retry). Ein abgelehnter HTTP-Status oder ein
        /// unerwartetes Antwortformat wirft weiterhin direkt eine Exception.
        /// (Ein "out"-Parameter wäre hier nicht zulässig - async-Methoden dürfen
        /// keine ref/in/out-Parameter haben - deshalb ein Tupel als Rückgabewert.)
        /// </summary>
        private static async Task<(byte[]? ImageBytes, string? TextOnlyResponse)> GenerateWithGoogleOnceAsync(string apiKey, string effectivePrompt, ReferenceImage? referenceImage, CancellationToken cancellationToken)
        {
            const string url = "https://generativelanguage.googleapis.com/v1/models/gemini-3.1-flash-image:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(BuildGoogleRequestBody(effectivePrompt, referenceImage)), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            string responseText = await ReadResponseTextWithLimitAsync(response, ResponseBodyReadTimeout, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google hat die Anfrage abgelehnt (HTTP {(int)response.StatusCode}): {ExtractGoogleError(responseText)}");
            }

            return ParseGoogleImageResponse(responseText);
        }

        /// <summary>
        /// Liest die JSON-Antwort von Googles Bild-API defensiv und liefert entweder
        /// die dekodierten Bild-Bytes oder - falls nur Text zurueckkam - diesen Text.
        /// Von der HTTP-Anfrage entkoppelt, damit Tests gueltige Bilder,
        /// uebersprungene "Thinking"-Bildteile, reine Text-Antworten und falsch
        /// typisierte Antworten offline pruefen koennen (Pruefbericht Punkt 7).
        /// </summary>
        internal static (byte[]? ImageBytes, string? TextOnlyResponse) ParseGoogleImageResponse(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                if (!doc.RootElement.TryGetProperty("candidates", out JsonElement candidates)
                    || candidates.ValueKind != JsonValueKind.Array
                    || candidates.GetArrayLength() == 0
                    || !candidates[0].TryGetProperty("content", out JsonElement content)
                    || !content.TryGetProperty("parts", out JsonElement parts)
                    || parts.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("Die Antwort von Google hatte ein unerwartetes Format.");
                }

                var textParts = new List<string>();
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    // Gemini-3-Bildmodelle koennen "Thinking"-Bildteile mit
                    // "thought": true liefern, bevor das eigentliche, endgueltige
                    // Bild kommt. Googles eigenes Beispiel ueberspringt diese
                    // Thought-Teile ausdruecklich - sonst wuerde hier ggf. ein
                    // Zwischen-/Denkbild statt des finalen Icons verwendet.
                    if (part.TryGetProperty("thought", out JsonElement thoughtElement)
                        && thoughtElement.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    if (part.TryGetProperty("inlineData", out JsonElement inlineData)
                        && inlineData.TryGetProperty("data", out JsonElement dataElement)
                        && dataElement.ValueKind == JsonValueKind.String)
                    {
                        // Als grobes Vorfilter akzeptieren wir nur einen mit "image/"
                        // beginnenden mimeType, falls einer gemeldet wird. Das entscheidende
                        // Sicherheitsnetz ist aber ohnehin das erzwungene Neu-Encodieren als
                        // PNG in DecodeAndReencodeAsPng - dieses Vorfilter ersetzt das
                        // NICHT, sondern spart nur unnoetiges Base64-Dekodieren offensichtlich
                        // falscher Antworten.
                        if (inlineData.TryGetProperty("mimeType", out JsonElement mimeTypeElement)
                            && mimeTypeElement.ValueKind == JsonValueKind.String)
                        {
                            string? mimeType = mimeTypeElement.GetString();
                            if (mimeType is not null && !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        string? base64 = dataElement.GetString();
                        if (!string.IsNullOrEmpty(base64))
                        {
                            return (DecodeBase64WithLimit(base64, "Google"), null);
                        }
                    }

                    if (part.TryGetProperty("text", out JsonElement textElement)
                        && textElement.ValueKind == JsonValueKind.String)
                    {
                        string? textValue = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(textValue))
                        {
                            textParts.Add(textValue);
                        }
                    }
                }

                // Kein Bild gefunden - nur Text (oder gar nichts). Die Text-Antwort
                // wird an den Aufrufer weitergereicht, damit sie entweder für den
                // Retry-Prompt oder die endgültige Fehlermeldung genutzt werden kann.
                string? textOnlyResponse = textParts.Count > 0 ? string.Join(" ", textParts) : null;
                return (null, textOnlyResponse);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                throw new InvalidOperationException(
                    "Die Antwort von Google hatte ein unerwartetes Format und konnte nicht gelesen werden.", ex);
            }
        }

        /// <summary>
        /// Prueft die Laenge des Base64-Strings, BEVOR <see
        /// cref="Convert.FromBase64String"/> das komplette dekodierte Bytearray
        /// alloziert - verhindert, dass eine unplausibel grosse Antwort erst nach
        /// vollstaendiger Dekodierung abgelehnt wird (Pruefbericht Punkt 5).
        /// </summary>
        private static byte[] DecodeBase64WithLimit(string base64, string providerName)
        {
            if (base64.Length > MaxBase64Chars)
            {
                throw new InvalidOperationException(
                    $"{providerName} hat unplausibel viele Bilddaten geliefert - das wird zur Sicherheit " +
                    "abgelehnt, bevor es vollständig dekodiert wird.");
            }

            return Convert.FromBase64String(base64);
        }

        /// <summary>
        /// Liest den Antwort-Body mit einer harten Obergrenze, statt ihn per
        /// <see cref="HttpContent.ReadAsStringAsync()"/> bedingungslos vollständig
        /// in den Speicher zu laden. Ein per Content-Length bereits als zu groß
        /// gemeldeter Body wird sofort abgelehnt; fehlt der Header (z.B. bei
        /// Chunked Transfer Encoding), wird der Stream gestückelt gelesen und beim
        /// Überschreiten der Grenze sofort abgebrochen (Pruefbericht Punkt 5).
        ///
        /// Der Timeout ist ein Parameter, damit automatisierte Tests (3. ChatGPT-
        /// Gegenprüfung von v2.1.2, Regressionstest für den Timeout-Fix) den
        /// Timeout-Zweig auslösen können, ohne 120 echte Sekunden zu warten; die
        /// Anwendung übergibt <see cref="ResponseBodyReadTimeout"/>. Runde 2 (F-001):
        /// <paramref name="cancellationToken"/> erlaubt einen vom Benutzer
        /// angeforderten Abbruch.
        /// </summary>
        internal static async Task<string> ReadResponseTextWithLimitAsync(HttpResponseMessage response, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            if (response.Content.Headers.ContentLength is long declaredLength && declaredLength > MaxResponseBytes)
            {
                throw new InvalidOperationException(
                    $"Die Antwort ist mit {declaredLength / 1024 / 1024} MB unplausibel groß und wird zur " +
                    "Sicherheit abgelehnt, ohne sie vollständig zu laden.");
            }

            // Eigener CancellationToken fuer das Lesen des Bodys - siehe Kommentar bei
            // ResponseBodyReadTimeout. HttpClient.Timeout allein wuerde diesen Teil wegen
            // HttpCompletionOption.ResponseHeadersRead nicht mehr abdecken.
            //
            // Runde 2 (Befund F-001): zusaetzlich mit dem Abbruch-Token des Aufrufers
            // verknuepft, damit auch das Lesen des Antwortkoerpers abbrechbar ist.
            using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeoutCts.CancelAfter(timeout);
            try
            {
                await using Stream stream = await response.Content.ReadAsStreamAsync(readTimeoutCts.Token).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                byte[] chunk = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(chunk, readTimeoutCts.Token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxResponseBytes)
                    {
                        throw new InvalidOperationException(
                            $"Die Antwort überschreitet die zulässige Größe von {MaxResponseBytes / 1024 / 1024} MB " +
                            "und wurde zur Sicherheit abgebrochen, bevor sie vollständig geladen wurde.");
                    }
                    buffer.Write(chunk, 0, read);
                }

                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
            catch (OperationCanceledException) when (readTimeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Nur die eigene Zeitueberschreitung wird in die bekannte Meldung
                // uebersetzt; ein vom Benutzer angeforderter Abbruch wird unveraendert
                // als OperationCanceledException weitergereicht.
                throw new InvalidOperationException(
                    "Die Antwort wurde nicht innerhalb der zulässigen Zeit vollständig empfangen " +
                    $"(Zeitüberschreitung nach {timeout.TotalSeconds:0} Sekunden beim Lesen " +
                    "des Antwortkörpers).");
            }
        }

        private static string ExtractOpenAiError(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("error", out JsonElement error)
                    && error.TryGetProperty("message", out JsonElement message))
                {
                    return message.GetString() ?? responseText;
                }
            }
            catch (JsonException)
            {
                // Antwort war kein JSON - Rohtext unten zurueckgeben.
            }

            return responseText;
        }

        private static string ExtractGoogleError(string responseText)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseText);
                if (doc.RootElement.TryGetProperty("error", out JsonElement error)
                    && error.TryGetProperty("message", out JsonElement message))
                {
                    return message.GetString() ?? responseText;
                }
            }
            catch (JsonException)
            {
                // Antwort war kein JSON - Rohtext unten zurueckgeben.
            }

            return responseText;
        }
    }
}
