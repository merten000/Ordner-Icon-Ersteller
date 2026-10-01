using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Wird geworfen, wenn eine neue abbrechbare Lesearbeit gestartet werden soll, obwohl eine
    /// fruehere, abgebrochene noch im Hintergrund haengt (Runde 3, Befund F-008).
    /// </summary>
    public sealed class AbandonedWorkStillRunningException : InvalidOperationException
    {
        public AbandonedWorkStillRunningException()
            : base("Ein früherer, abgebrochener Zugriff auf den Ordner hängt noch (das Dateisystem antwortet nicht).")
        {
        }
    }

    /// <summary>
    /// Runde 3 (Befund F-008): fuehrt eine rein LESENDE, synchrone Arbeit (z. B. Ordner-Scan) im
    /// Hintergrund aus und erlaubt trotzdem einen sofortigen Abbruch, auch wenn die Arbeit selbst
    /// in einem blockierenden Dateisystemzugriff haengt und ihren CancellationToken nie mehr
    /// erreicht (z. B. nicht erreichbares Netzlaufwerk).
    ///
    /// Bei Abbruch wird die Arbeit "zurueckgelassen": der Aufrufer bekommt sofort eine
    /// OperationCanceledException, die Arbeit laeuft im Thread-Pool bis zum Ende des
    /// blockierenden Aufrufs weiter. Damit das harmlos bleibt:
    ///  - Die Arbeit darf nur lesen und keinen Zustand des Fensters oder einer spaeteren
    ///    Operation beruehren; ihr Ergebnis wird nach dem Zurücklassen verworfen, ihre Ausnahme
    ///    nur eingesammelt. Der Helfer kennt weder Oberflaeche noch Hauptfenster.
    ///  - Es gibt hoechstens EINE zurueckgelassene Arbeit pro Gate: solange sie noch haengt,
    ///    wird eine neue Arbeit mit <see cref="AbandonedWorkStillRunningException"/> abgelehnt.
    ///    Wiederholtes Abbrechen kann so keine beliebig vielen haengenden Tasks ansammeln.
    ///  - Der Prozess wird dadurch nicht am Beenden gehindert (Thread-Pool-Threads sind
    ///    Hintergrund-Threads).
    /// Veraendernde Arbeit (Datei-Transaktionen) darf NICHT ueber dieses Gate laufen.
    /// </summary>
    public sealed class AbandonableWorkGate
    {
        private int _abandonedRunning; // 0 oder 1

        /// <summary>True, solange eine zurueckgelassene Arbeit noch nicht zu Ende ist.</summary>
        public bool HasAbandonedWork => Volatile.Read(ref _abandonedRunning) == 1;

        public async Task<T> RunAsync<T>(Func<CancellationToken, T> work, CancellationToken cancellationToken)
        {
            if (work is null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (HasAbandonedWork)
            {
                throw new AbandonedWorkStillRunningException();
            }

            // Eigene, mit dem Benutzer-Token verknuepfte Quelle: bleibt gueltig, bis die Arbeit
            // wirklich zu Ende ist, auch wenn der Aufrufer seinen Token laengst freigegeben hat.
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<T> task = Task.Run(() => work(linked.Token));

            var cancelRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelRequested.TrySetResult(true)))
            {
                await Task.WhenAny(task, cancelRequested.Task).ConfigureAwait(true);
            }

            if (task.IsCompleted)
            {
                linked.Dispose();
                return await task.ConfigureAwait(true); // Ergebnis bzw. Ausnahme der Arbeit weitergeben
            }

            // Abbruch angefordert, Arbeit haengt noch: zuruecklassen. Das Merken MUSS vor dem Werfen
            // passieren, damit ein sofortiger Neustart zuverlaessig abgelehnt wird.
            Volatile.Write(ref _abandonedRunning, 1);
            _ = task.ContinueWith(
                t =>
                {
                    _ = t.Exception; // Ausnahme einsammeln (keine unbeobachtete Task-Ausnahme)
                    linked.Dispose();
                    Volatile.Write(ref _abandonedRunning, 0);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            throw new OperationCanceledException(cancellationToken);
        }
    }
}
