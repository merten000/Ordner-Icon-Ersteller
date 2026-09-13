using System;
using System.Runtime.InteropServices;
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

        public static void SaveOpenAiKey(string apiKey) => SaveKey(OpenAiTargetName, apiKey);
        public static void SaveGoogleKey(string apiKey) => SaveKey(GoogleTargetName, apiKey);
        public static string? ReadOpenAiKey() => ReadKey(OpenAiTargetName);
        public static string? ReadGoogleKey() => ReadKey(GoogleTargetName);
        public static bool HasOpenAiKey() => !string.IsNullOrEmpty(ReadOpenAiKey());
        public static bool HasGoogleKey() => !string.IsNullOrEmpty(ReadGoogleKey());
        public static void DeleteOpenAiKey() => DeleteKey(OpenAiTargetName);
        public static void DeleteGoogleKey() => DeleteKey(GoogleTargetName);

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

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CredFree(IntPtr cred);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, uint type, int flags);

        private static void SaveKey(string targetName, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("Der API-Schluessel darf nicht leer sein.", nameof(apiKey));
            }

            byte[] blobBytes = Encoding.Unicode.GetBytes(apiKey);
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
                // unmanaged Speicher entfernen, sobald CredWrite eine eigene Kopie
                // angelegt hat.
                Marshal.Copy(new byte[blobBytes.Length], 0, blobPtr, blobBytes.Length);
                Marshal.FreeHGlobal(blobPtr);
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

            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                {
                    return string.Empty;
                }

                byte[] bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, (int)credential.CredentialBlobSize);
                return Encoding.Unicode.GetString(bytes);
            }
            finally
            {
                CredFree(credPtr);
            }
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
