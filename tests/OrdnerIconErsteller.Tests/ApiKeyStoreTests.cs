using System;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    /// <summary>
    /// Prüft das Speichern/Lesen/Löschen über den Windows-Anmeldeinformations-
    /// Manager im Rundlauf. Läuft nur sinnvoll unter Windows (wie der Rest des
    /// Programms), da ApiKeyStore direkt advapi32.dll über P/Invoke aufruft.
    ///
    /// WICHTIG: Diese Tests verwenden AUSSCHLIESSLICH die internen
    /// "...ForTesting"-Methoden von <see cref="ApiKeyStore"/> mit einem pro
    /// Testlauf per GUID eindeutigen Target-Namen - niemals die öffentlichen
    /// Save/Read/Has/DeleteOpenAiKey()/...GoogleKey()-Methoden mit den echten
    /// Produkt-Zielen "OrdnerIconErsteller:OpenAI"/"OrdnerIconErsteller:Google".
    /// Eine frühere Fassung dieser Tests rief genau diese Produkt-Methoden auf
    /// und konnte dadurch einen zuvor vom Benutzer/Entwickler echt gespeicherten
    /// API-Schlüssel unwiederbringlich löschen (siehe Prüfbericht KI-Icon,
    /// Commit 6cff972, Punkt 3). Der eindeutige Target-Name pro Testlauf stellt
    /// zusätzlich sicher, dass parallel laufende Tests sich nicht gegenseitig
    /// stören.
    ///
    /// Die eigentlichen KI-API-Aufrufe (AiIconService) sind bewusst NICHT
    /// automatisiert testbar, da sie echte, kostenpflichtige Netzwerkaufrufe an
    /// OpenAI/Google wären - das wird hier offen benannt statt stillschweigend
    /// übersprungen.
    /// </summary>
    public class ApiKeyStoreTests
    {
        private static string NewTestTargetName() => $"OrdnerIconErsteller:UnitTest:{Guid.NewGuid():N}";

        [Fact]
        public void Key_SpeichernLesenLoeschen_Rundlauf()
        {
            string targetName = NewTestTargetName();

            // Ein frischer, per GUID eindeutiger Target-Name kann per Definition
            // noch nicht existieren - trotzdem zur Sicherheit vorab aufräumen,
            // falls (theoretisch) doch schon etwas unter diesem Namen läge.
            ApiKeyStore.DeleteKeyForTesting(targetName);
            Assert.False(ApiKeyStore.HasKeyForTesting(targetName));
            Assert.Null(ApiKeyStore.ReadKeyForTesting(targetName));

            const string testKey = "sk-test-nur-fuer-diesen-unittest-1234567890";
            ApiKeyStore.SaveKeyForTesting(targetName, testKey);

            try
            {
                Assert.True(ApiKeyStore.HasKeyForTesting(targetName));
                Assert.Equal(testKey, ApiKeyStore.ReadKeyForTesting(targetName));
            }
            finally
            {
                ApiKeyStore.DeleteKeyForTesting(targetName);
            }

            Assert.False(ApiKeyStore.HasKeyForTesting(targetName));
            Assert.Null(ApiKeyStore.ReadKeyForTesting(targetName));
        }

        [Fact]
        public void DeleteKey_WennNichtVorhanden_WirftKeinenFehler()
        {
            string targetName = NewTestTargetName();

            ApiKeyStore.DeleteKeyForTesting(targetName);
            // Zweites Löschen desselben, bereits nicht mehr vorhandenen Schlüssels
            // muss ERROR_NOT_FOUND (1168) still abfangen statt zu werfen.
            var exception = Record.Exception(() => ApiKeyStore.DeleteKeyForTesting(targetName));
            Assert.Null(exception);
        }

        [Fact]
        public void SaveKey_ZuLangerSchluessel_WirftArgumentException()
        {
            string targetName = NewTestTargetName();
            // Deutlich über der 2560-Byte-Grenze des Credential-Manager-Blobs
            // (bei UTF-16 also über ca. 1280 Zeichen).
            string tooLong = new string('x', 5000);

            try
            {
                Assert.Throws<ArgumentException>(() => ApiKeyStore.SaveKeyForTesting(targetName, tooLong));
                Assert.False(ApiKeyStore.HasKeyForTesting(targetName));
            }
            finally
            {
                ApiKeyStore.DeleteKeyForTesting(targetName);
            }
        }
    }
}
