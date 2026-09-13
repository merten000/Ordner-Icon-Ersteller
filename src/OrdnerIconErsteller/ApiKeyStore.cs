using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Speichert und liest die API-Schluessel fuer die KI-Icon-Funktion ueber den
    /// Windows-Anmeldeinformations-Manager (Credential Manager) - NICHT als
    /// Klartextdatei im Programmordner. Es wird ausschliesslich die offizielle
    /// .NET-P/Invoke-Schnittstelle zu advapi32.dll verwendet (CredWrite/CredRead/
    /// CredDelete/CredFree) - zu keinem Zeitpunkt wird cmdkey.exe, powershell.exe
    /// oder ein anderer externer Prozess aufgerufen.
    ///
    /// CRED_PERSIST_LOCAL_MACHINE: die Schluessel bleiben nur auf diesem Rechner
    /// fuer den aktuellen Windows-Benutzer erhalten (kein Roaming/Synchronisieren
    /// zwischen Geraeten). Genau wie beim Rechtsklick-Menue-Eintrag (siehe
    /// ContextMenuService) sind dafuer keine Administratorrechte noetig.
    /// </summary>
    internal static class ApiKeyStore
    {
        private const string OpenAiTargetName = "OrdnerIconErsteller:OpenAI";
        private const string GoogleTargetName = "OrdnerIconErsteller:Google";

        private const uint CredTypeGeneric = 1;
        private const uint CredPersistLocalMachine = 2;
        private const int ErrorNotFound = 1168;

        // CRED_MAX_CREDENTIAL_BLOB_SIZE laut Microsoft-Dokumentation zur
        // CREDENTIAL-Struktur: 5 * 512 Byte = 2560 Byte fuer den Blob.
        private const int MaxCredentialBlobBytes = 2560;

        public static void SaveOpenAiKey(string apiKey) => SaveKey(OpenAiTargetName, apiKey);
        public static void SaveGoogleKey(string apiKey) => SaveKey(GoogleTargetName, apiKey);
        public static string? ReadOpenAiKey() => ReadKey(OpenAiTargetName);
        public static string? ReadGoogleKey() => ReadKey(GoogleTargetName);

        // Prueft nur, ob ein Schluessel hinterlegt ist - ohne den Klartext dafuer
        // in eine verwaltete string-Kopie zu lesen (siehe Pruefbericht Punkt 7).
        public static bool HasOpenAiKey() => KeyExists(OpenAiTargetName);
        public static bool HasGoogleKey() => KeyExists(GoogleTargetName);
        public static void DeleteOpenAiKey() => DeleteKey(OpenAiTargetName);
        public static void DeleteGoogleKey() => DeleteKey(GoogleTargetName);

        // Nur fuer automatisierte Tests: identische Logik wie die oeffentlichen
        // Methoden oben, aber mit einem vom Aufrufer gewaehlten Target-Namen -
        // damit Tests niemals die echten Produkt-Eintraege
        // "OrdnerIconErsteller:OpenAI"/"OrdnerIconErsteller:Google" beruehren
        // (siehe Pruefbericht Punkt 3). Zugriff nur ueber InternalsVisibleTo aus
        // OrdnerIconErsteller.Tests.
        internal static void SaveKeyForTesting(string targetName, string apiKey) => SaveKey(targetName, apiKey);
        internal static string? ReadKeyForTesting(string targetName) => ReadKey(targetName);
        internal static bool HasKeyForTesting(string targetName) => KeyExists(targetName);
        internal static void DeleteKeyForTesting(string targetName) => DeleteKey(targetName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

        // Die native Win32-Funktion CredFree gibt VOID zurueck (siehe MSDN) - die
        // vorherige Deklaration mit Rueckgabetyp "bool" und SetLastError war
        // falsch und wurde korrigiert (Pruefbericht Punkt 7).
        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr cred);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, uint type, int flags);

        private static void SaveKey(string targetName, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("Der API-Schluessel darf nicht leer sein.", nameof(apiKey));
            }

            byte[] blobBytes = Encoding.Unicode.GetBytes(apiKey);
            if (blobBytes.Length > MaxCredentialBlobBytes)
            {
                throw new ArgumentException(
                    $"Der API-Schluessel ist zu lang fuer den Windows-Anmeldeinformations-Manager " +
                    $"(maximal {MaxCredentialBlobBytes} Byte, dieser Schluessel benoetigt {blobBytes.Length} Byte).",
                    nameof(apiKey));
            }

            IntPtr blobPtr = Marshal.AllocHGlobal(blobBytes.Length);
            try
            {
                Marshal.Copy(blobBytes, 0, blobPtr, blobBytes.Length);

                var credential = new CREDENTIAL
                {
                    Type = CredTypeGeneric,
                    TargetName = targetName,
                    Comment = "Ordner-Icon-Ersteller: API-Schluessel fuer KI-Icon-Erzeugung",
                    CredentialBlobSize = (uint)blobBytes.Length,
                    CredentialBlob = blobPtr,
                    Persist = CredPersistLocalMachine,
                    UserName = "OrdnerIconErsteller",
                };

                if (!CredWrite(ref credential, 0))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        $"Der API-Schluessel konnte nicht im Windows-Anmeldeinformations-Manager gespeichert werden (Fehlercode {error}).");
                }
            }
            finally
            {
                // Aus Sicherheitsgruenden den Klartext-Schluessel umgehend aus dem
                // unmanaged UND dem verwalteten Speicher entfernen, statt auf die
                // Garbage Collection zu warten (Pruefbericht Punkt 7).
                Marshal.Copy(new byte[blobBytes.Length], 0, blobPtr, blobBytes.Length);
                Marshal.FreeHGlobal(blobPtr);
                CryptographicOperations.ZeroMemory(blobBytes);
            }
        }

        private static string? ReadKey(string targetName)
        {
            if (!CredRead(targetName, CredTypeGeneric, 0, out IntPtr credPtr))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                {
                    return null;
                }
                throw new InvalidOperationException(
                    $"Der API-Schluessel konnte nicht aus dem Windows-Anmeldeinformations-Manager gelesen werden (Fehlercode {error}).");
            }

            byte[]? bytes = null;
            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                {
                    return string.Empty;
                }

                bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, (int)credential.CredentialBlobSize);
                return Encoding.Unicode.GetString(bytes);
            }
            finally
            {
                CredFree(credPtr);
                if (bytes is not null)
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
        }

        /// <summary>
        /// Prueft nur, ob unter dem Target-Namen ein Eintrag existiert, ohne den
        /// Klartext-Schluessel in eine verwaltete string-Kopie zu lesen
        /// (Pruefbericht Punkt 7: "Has...Key() sollte nur die Existenz pruefen").
        /// </summary>
        private static bool KeyExists(string targetName)
        {
            if (!CredRead(targetName, CredTypeGeneric, 0, out IntPtr credPtr))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                {
                    return false;
                }
                throw new InvalidOperationException(
                    $"Der API-Schluessel konnte nicht aus dem Windows-Anmeldeinformations-Manager gelesen werden (Fehlercode {error}).");
            }

            CredFree(credPtr);
            return true;
        }

        private static void DeleteKey(string targetName)
        {
            if (!CredDelete(targetName, CredTypeGeneric, 0))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNotFound)
                {
                    return; // Bereits nicht vorhanden - kein Fehler.
                }
                throw new InvalidOperationException(
                    $"Der API-Schluessel konnte nicht aus dem Windows-Anmeldeinformations-Manager entfernt werden (Fehlercode {error}).");
            }
        }
    }
}
