using System;
using System.IO;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    /// <summary>
    /// Deckt ab, dass der "Rechtliches"-Bereich (Merten-Feedback: "möglichst direkt
    /// dieselben mitgelieferten Dateien anzeigen") die tatsächlich mit ausgelieferten
    /// Dateien einliest und bei einer fehlenden Datei eine verständliche Meldung
    /// liefert, statt abzustürzen.
    /// </summary>
    public class LegalDocumentServiceTests
    {
        // WICHTIG: Die Testmethoden-PARAMETER sind hier bewusst "string" statt direkt
        // LegalDocumentService.Document, obwohl LegalDocumentServiceTests per
        // InternalsVisibleTo (siehe OrdnerIconErsteller.csproj) den internen Typ
        // grundsätzlich verwenden darf. Ein "public" [Theory]-Testmethode (von xUnit
        // zur Testerkennung verlangt) DARF laut C#-Sprachregel trotzdem keinen weniger
        // zugreifbaren ("internal") Typ in ihrer Signatur haben (Fehler CS0051) - das
        // gilt unabhängig von InternalsVisibleTo, weil die Methode selbst öffentlich
        // bleibt. Deshalb wird der Dokumentname als reiner string übergeben und erst
        // im Testkörper (nicht mehr Teil der Signatur) per Enum.Parse in
        // LegalDocumentService.Document umgewandelt.
        [Theory]
        [InlineData("Impressum", "IMPRESSUM.md")]
        [InlineData("Datenschutz", "DATENSCHUTZ.md")]
        [InlineData("Lizenz", "LIZENZ.md")]
        [InlineData("Haftungsausschluss", "HAFTUNGSAUSSCHLUSS.md")]
        public void GetPath_ZeigtAufDenGleichnamigenUnterordnerLegalNebenDerExe(
            string documentName, string expectedFileName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);
            string path = LegalDocumentService.GetPath(document);

            Assert.Equal(expectedFileName, Path.GetFileName(path));
            Assert.Equal("legal", Path.GetFileName(Path.GetDirectoryName(path)));
        }

        [Theory]
        [InlineData("Impressum")]
        [InlineData("Datenschutz")]
        [InlineData("Lizenz")]
        [InlineData("Haftungsausschluss")]
        public void ReadText_MitgelieferteDateiVorhanden_LiefertNichtLeerenText(string documentName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);

            // Pruefbericht v2.7.1, Punkt 6: vorher wurde bei fehlender Datei per "return"
            // stillschweigend uebersprungen - genau der Fehler, den dieser Test laut Namen
            // erkennen soll ("liefert nicht leeren Text"), wurde damit als Erfolg gewertet.
            // Jetzt schlaegt der Test bewusst fehl, wenn die csproj die legal\*.md-Dateien
            // nicht (mehr) per CopyToOutputDirectory neben die Test-DLL kopiert.
            string path = LegalDocumentService.GetPath(document);
            Assert.True(File.Exists(path), $"Erwartete mitgelieferte Datei fehlt: {path}");

            string text = LegalDocumentService.ReadText(document);
            Assert.False(string.IsNullOrWhiteSpace(text));
        }

        // Pruefbericht v2.7.1/v2.7.2, Freigabeblocker 1: die mitgelieferten .NET-
        // Runtime-Rechtstexte liegen bewusst in einem eigenen Unterordner
        // "legal\dotnet-runtime" (nicht direkt in "legal") - eigene Tests dafuer statt
        // die obigen Theorien zu erweitern, weil hier der unmittelbare Elternordner
        // "dotnet-runtime" statt "legal" heisst.
        [Theory]
        [InlineData("DotNetLizenz", "LICENSE.TXT")]
        [InlineData("Drittanbieterhinweise", "THIRD-PARTY-NOTICES.TXT")]
        public void GetPath_DotNetRuntimeDokumente_ZeigenAufDenUnterordnerDotnetRuntime(
            string documentName, string expectedFileName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);
            string path = LegalDocumentService.GetPath(document);

            Assert.Equal(expectedFileName, Path.GetFileName(path));
            Assert.Equal("dotnet-runtime", Path.GetFileName(Path.GetDirectoryName(path)));
        }

        [Theory]
        [InlineData("DotNetLizenz")]
        [InlineData("Drittanbieterhinweise")]
        public void ReadText_DotNetRuntimeDokumenteVorhanden_LiefertNichtLeerenText(string documentName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);

            string path = LegalDocumentService.GetPath(document);
            Assert.True(File.Exists(path), $"Erwartete mitgelieferte Datei fehlt: {path}");

            string text = LegalDocumentService.ReadText(document);
            Assert.False(string.IsNullOrWhiteSpace(text));
        }

        [Fact]
        public void ReadText_DateiFehlt_WirftFileNotFoundExceptionMitVerstaendlicherMeldung()
        {
            // Simuliert eine unvollstaendige Installation: ein leeres Verzeichnis statt
            // des echten Programmordners, über die testbare Überladung mit explizitem
            // Basisverzeichnis (siehe Kommentar bei LegalDocumentService.ReadText).
            string emptyTempDirectory = Directory.CreateTempSubdirectory("OrdnerIconErsteller_LegalMissing_").FullName;
            try
            {
                var ex = Assert.Throws<FileNotFoundException>(
                    () => LegalDocumentService.ReadText(LegalDocumentService.Document.Impressum, emptyTempDirectory));
                Assert.Contains("IMPRESSUM.md", ex.Message);
                Assert.Contains("neu installieren", ex.Message);
            }
            finally
            {
                Directory.Delete(emptyTempDirectory, recursive: true);
            }
        }
    }
}
