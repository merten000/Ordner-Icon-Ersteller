namespace OrdnerIconErsteller
{
    /// <summary>
    /// In welcher Phase sich eine laufende, vom Hauptfenster gestartete Operation
    /// befindet (Runde 2, Befund F-001).
    /// </summary>
    public enum OperationPhase
    {
        /// <summary>Es laeuft nichts.</summary>
        Idle,

        /// <summary>
        /// Ordner-Scan, Zustimmung und Netzwerkanfrage an den KI-Anbieter. In dieser
        /// Phase ist noch nichts im Zielordner veraendert - ein Abbruch ist jederzeit
        /// gefahrlos moeglich.
        /// </summary>
        Cancellable,

        /// <summary>
        /// Dateischritt im Zielordner (Icon setzen/entfernen bzw. erzeugtes Bild
        /// schreiben und Icon setzen). Diese Transaktion hat ihr eigenes Rollback und
        /// wird bewusst NICHT mittendrin abgebrochen.
        /// </summary>
        Committing,
    }

    /// <summary>Was beim Schliessen des Fensters geschehen soll.</summary>
    public enum CloseAction
    {
        /// <summary>Normal schliessen.</summary>
        Allow,

        /// <summary>
        /// Der Benutzer hat geschlossen, waehrend noch abbrechbar gearbeitet wird:
        /// Fenster zunaechst offen lassen, den Benutzer fragen und bei "Ja" abbrechen
        /// und danach automatisch schliessen.
        /// </summary>
        AskCancelThenClose,

        /// <summary>
        /// Der Benutzer hat geschlossen, waehrend gerade in den Ordner geschrieben wird:
        /// Schliessen verweigern (nur wenige Sekunden), damit die Transaktion ihr
        /// Rollback sicher beenden kann.
        /// </summary>
        BlockCommitting,

        /// <summary>
        /// Windows faehrt herunter bzw. der Task-Manager beendet die Anwendung, waehrend
        /// noch abbrechbar gearbeitet wird: Abbruch anfordern und sofort schliessen.
        /// </summary>
        CancelAndAllow,

        /// <summary>
        /// Windows faehrt herunter bzw. der Task-Manager beendet die Anwendung, waehrend
        /// gerade in den Ordner geschrieben wird: kurz (begrenzt) auf das Ende der
        /// Transaktion warten und dann schliessen - niemals unbegrenzt blockieren.
        /// </summary>
        WaitForCommitAndAllow,
    }

    /// <summary>
    /// Reine Entscheidungslogik fuer das Schliessen des Hauptfensters (Runde 2,
    /// Befund F-001). Vorher setzte <c>OnFormClosing</c> waehrend einer KI-Operation
    /// immer <c>e.Cancel = true</c> - unabhaengig vom <c>CloseReason</c> und ohne
    /// Abbruchmoeglichkeit. Das blockierte auch Windows-Abmeldung/Herunterfahren und
    /// "Task beenden" und verleitete zum harten Beenden des Prozesses. Die Logik ist
    /// bewusst unabhaengig von WinForms, damit sie ohne Oberflaeche testbar ist.
    /// </summary>
    internal static class OperationGuard
    {
        /// <param name="phase">Aktuelle Phase der laufenden Operation.</param>
        /// <param name="userInitiated">
        /// true, wenn der Benutzer selbst das Fenster schliesst
        /// (<c>CloseReason.UserClosing</c>); false bei Windows-Herunterfahren,
        /// Task-Manager und allen anderen Gruenden.
        /// </param>
        public static CloseAction DecideClose(OperationPhase phase, bool userInitiated)
        {
            switch (phase)
            {
                case OperationPhase.Cancellable:
                    return userInitiated ? CloseAction.AskCancelThenClose : CloseAction.CancelAndAllow;
                case OperationPhase.Committing:
                    return userInitiated ? CloseAction.BlockCommitting : CloseAction.WaitForCommitAndAllow;
                default:
                    return CloseAction.Allow;
            }
        }
    }
}
