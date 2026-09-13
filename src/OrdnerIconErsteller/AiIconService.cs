using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
    /// </summary>
    internal static class AiIconService
    {
        // Grosszuegiges Timeout, da Bildgenerierung bei beiden Anbietern durchaus
        // 20-60 Sekunden dauern kann.
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(120),
        };

        // Grosszuegige, aber endliche Obergrenze fuer eine einzelne Bildantwort,
        // um eine unplausibel grosse/verstuemmelte Antwort frueh abzulehnen statt
        // sie unbegrenzt in den Speicher zu laden (Pruefbericht Punkt 5).
        private const int MaxImageBytes = 30 * 1024 * 1024; // 30 MB

        /// <summary>
        /// Erzeugt ein Bild anhand des Prompts beim gewaehlten Anbieter und liefert
        /// die rohen Bilddaten (PNG) zurueck. Der Aufrufer speichert diese Bytes in
        /// eine temporaere Datei und uebergibt sie an die bestehende, unveraenderte
        /// Pipeline (IconWriter/FolderIconService) - genau wie bei einem manuell
        /// ausgewaehlten Bild. Die Bytes werden hier bereits vollstaendig als
        /// gueltiges Bild dekodiert/geprueft (Pruefbericht Punkt 5) - der Aufrufer
        /// muss sie also nicht selbst nochmal validieren, bevor er sie dauerhaft
        /// speichert.
        /// </summary>
        public static async Task<byte[]> GenerateIconImageAsync(AiImageProvider provider, string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                throw new ArgumentException("Bitte einen Text fuer das gewuenschte Icon eingeben.", nameof(prompt));
            }

            byte[] imageBytes = provider switch
            {
                AiImageProvider.OpenAi => await GenerateWithOpenAiAsync(prompt).ConfigureAwait(false),
                AiImageProvider.Google => await GenerateWithGoogleAsync(prompt).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unbekannter KI-Anbieter."),
            };

            ValidateDecodableImage(provider, imageBytes);
            return imageBytes;
        }

        /// <summary>
        /// Prueft, dass die vom Anbieter gelieferten Bytes tatsaechlich ein
        /// vollstaendig dekodierbares Bild sind - nicht nur syntaktisch gueltiges
        /// Base64. Eine kaputte/unvollstaendige Antwort fliegt damit sofort hier
        /// als kontrollierter Fehler auf, statt spaeter als beschaedigte
        /// "Icon-Original"-Datei im Zielordner zu landen (Pruefbericht Punkt 5).
        /// </summary>
        private static void ValidateDecodableImage(AiImageProvider provider, byte[] imageBytes)
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
                using var stream = new MemoryStream(imageBytes, writable: false);
                using var image = Image.FromStream(stream);
                // Zugriff auf eine Eigenschaft erzwingt, dass das Bild tatsaechlich
                // vollstaendig eingelesen wurde, statt nur den Header zu pruefen.
                _ = image.Width;
                _ = image.Height;
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
            {
                throw new InvalidOperationException(
                    $"{provider} hat Daten geliefert, die sich nicht als Bild öffnen lassen " +
                    "(unvollständige oder beschädigte Antwort).", ex);
            }
        }

        private static async Task<byte[]> GenerateWithOpenAiAsync(string prompt)
        {
            string? apiKey = ApiKeyStore.ReadOpenAiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Es ist noch kein OpenAI-API-Schluessel hinterlegt. Bitte zuerst unter " +
                    "\"KI-Einstellungen\" einen Schluessel eingeben.");
            }

            // "gpt-image-1" gilt laut aktueller OpenAI-Dokumentation als veraltet
            // (siehe https://developers.openai.com/api/docs/guides/image-generation).
            // "gpt-image-2.5-flare" ist das fuer schnelle Alltags-Bilderzeugung
            // empfohlene aktuelle Modell - fuer die hier erzeugten einfachen
            // Ordner-Icons passender als "sunburst" (auf praezise Bildbearbeitung
            // ausgelegt). GPT-Bildmodelle liefern die Bilddaten immer als Base64 -
            // ein "response_format"-Parameter (nur fuer DALL-E relevant) entfaellt
            // dafuer; "output_format" legt stattdessen das Dateiformat fest.
            var requestBody = new
            {
                model = "gpt-image-2.5-flare",
                prompt,
                size = "1024x1024",
                n = 1,
                output_format = "png",
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/images/generations")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"OpenAI hat die Anfrage abgelehnt (HTTP {(int)response.StatusCode}): {ExtractOpenAiError(responseText)}");
            }

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
                return Convert.FromBase64String(base64);
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

        private static async Task<byte[]> GenerateWithGoogleAsync(string prompt)
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
            (byte[]? imageBytes, string? textOnlyResponse) = await GenerateWithGoogleOnceAsync(apiKey, BuildGoogleImagePrompt(prompt))
                .ConfigureAwait(false);
            if (imageBytes is not null)
            {
                return imageBytes;
            }

            string retryPrompt = BuildGoogleImagePrompt(prompt) +
                " This is very important: your entire response must be a single generated image. " +
                "Do not write any words, do not ask questions, do not explain - respond with the image only.";
            (imageBytes, string? retryTextOnlyResponse) = await GenerateWithGoogleOnceAsync(apiKey, retryPrompt)
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
        /// Ein einzelner Aufruf der Google-Bild-API. Liefert die Bild-Bytes, oder -
        /// wenn NUR Text ohne Bild zurückkam - null zusammen mit dieser Text-Antwort
        /// (für die Fehlermeldung/den Retry). Ein abgelehnter HTTP-Status oder ein
        /// unerwartetes Antwortformat wirft weiterhin direkt eine Exception.
        /// (Ein "out"-Parameter wäre hier nicht zulässig - async-Methoden dürfen
        /// keine ref/in/out-Parameter haben - deshalb ein Tupel als Rückgabewert.)
        /// </summary>
        private static async Task<(byte[]? ImageBytes, string? TextOnlyResponse)> GenerateWithGoogleOnceAsync(string apiKey, string effectivePrompt)
        {
            var requestBody = new
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
                    new { parts = new object[] { new { text = effectivePrompt } } },
                },
                generationConfig = new
                {
                    responseModalities = new[] { "TEXT", "IMAGE" },
                },
            };

            const string url = "https://generativelanguage.googleapis.com/v1/models/gemini-3.1-flash-image:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            using HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Google hat die Anfrage abgelehnt (HTTP {(int)response.StatusCode}): {ExtractGoogleError(responseText)}");
            }

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
                    // Zwischen-/Denkbild statt des finalen Icons verwendet
                    // (Pruefbericht Punkt 6).
                    if (part.TryGetProperty("thought", out JsonElement thoughtElement)
                        && thoughtElement.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    if (part.TryGetProperty("inlineData", out JsonElement inlineData)
                        && inlineData.TryGetProperty("data", out JsonElement dataElement)
                        && dataElement.ValueKind == JsonValueKind.String)
                    {
                        // Nur ein unterstuetztes Bildformat akzeptieren, falls Google
                        // einen mimeType mitliefert (Pruefbericht Punkt 5).
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
                            return (Convert.FromBase64String(base64), null);
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
