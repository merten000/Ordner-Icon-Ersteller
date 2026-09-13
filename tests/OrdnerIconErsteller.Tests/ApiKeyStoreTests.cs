using System;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    /// <summary>
    /// Prüft das Speichern/Lesen/Löschen über den Windows-Anmeldeinformations-
    /// Manager im Rundlauf. Läuft nur sinnvoll unter Windows (wie der Rest des
    /// Programms), da ApiKeyStore direkt advapi32.dll über P/Invoke aufruft.
    /// Die eigentlichen KI-API-Aufrufe (AiIconService) sind bewusst NICHT
    /// automatisiert testbar, da sie echte, kostenpflichtige Netzwerkaufrufe an
    /// OpenAI/Google wären - das wird hier offen benannt statt stillschweigend
    /// übersprungen.
    /// </summary>
    public class ApiKeyStoreTests
    {
        [Fact]
        public void OpenAiKey_SpeichernLesenLoeschen_Rundlauf()
        {
            // Aufräumen, falls von einem vorherigen fehlgeschlagenen Testlauf noch
            // ein Schlüssel übrig ist.
            ApiKeyStore.DeleteOpenAiKey();
            Assert.False(ApiKeyStore.HasOpenAiKey());
            Assert.Null(ApiKeyStore.ReadOpenAiKey());

            const string testKey = "sk-test-nur-fuer-diesen-unittest-1234567890";
            ApiKeyStore.SaveOpenAiKey(testKey);

            try
            {
                Assert.True(ApiKeyStore.HasOpenAiKey());
                Assert.Equal(testKey, ApiKeyStore.ReadOpenAiKey());
            }
            finally
            {
                ApiKeyStore.DeleteOpenAiKey();
            }

            Assert.False(ApiKeyStore.HasOpenAiKey());
            Assert.Null(ApiKeyStore.ReadOpenAiKey());
        }

        [Fact]
        public void GoogleKey_SpeichernLesenLoeschen_Rundlauf()
        {
            ApiKeyStore.DeleteGoogleKey();
            Assert.False(ApiKeyStore.HasGoogleKey());
            Assert.Null(ApiKeyStore.ReadGoogleKey());

            const string testKey = "AIza-test-nur-fuer-diesen-unittest-1234567890";
            ApiKeyStore.SaveGoogleKey(testKey);

            try
            {
                Assert.True(ApiKeyStore.HasGoogleKey());
                Assert.Equal(testKey, ApiKeyStore.ReadGoogleKey());
            }
            finally
            {
                ApiKeyStore.DeleteGoogleKey();
            }

            Assert.False(ApiKeyStore.HasGoogleKey());
            Assert.Null(ApiKeyStore.ReadGoogleKey());
        }

        [Fact]
        public void DeleteOpenAiKey_WennNichtVorhanden_WirftKeinenFehler()
        {
            ApiKeyStore.DeleteOpenAiKey();
            // Zweites Löschen desselben, bereits nicht mehr vorhandenen Schlüssels
            // muss ERROR_NOT_FOUND (1168) still abfangen statt zu werfen.
            var exception = Record.Exception(() => ApiKeyStore.DeleteOpenAiKey());
            Assert.Null(exception);
        }
    }
}
