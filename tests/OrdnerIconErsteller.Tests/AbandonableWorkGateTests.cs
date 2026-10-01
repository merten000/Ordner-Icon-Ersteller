using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    // Runde 3, Befund F-008: ein Abbruch muss auch wirken, wenn die Arbeit selbst in einem
    // blockierenden Dateisystemzugriff haengt und ihren Token nie mehr erreicht. Der haengende
    // Zugriff wird hier durch eine Arbeit nachgestellt, die auf ein Ereignis wartet und den
    // Token bewusst ignoriert.
    public class AbandonableWorkGateTests
    {
        private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    return false;
                }
                await Task.Delay(10);
            }
            return true;
        }

        [Fact]
        public async Task Fertige_Arbeit_LiefertErgebnis()
        {
            var gate = new AbandonableWorkGate();
            int result = await gate.RunAsync(_ => 42, CancellationToken.None);
            Assert.Equal(42, result);
            Assert.False(gate.HasAbandonedWork);
        }

        [Fact]
        public async Task Ausnahme_Der_Arbeit_WirdWeitergegeben()
        {
            var gate = new AbandonableWorkGate();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => gate.RunAsync<int>(_ => throw new InvalidOperationException("x"), CancellationToken.None));
            Assert.False(gate.HasAbandonedWork);
        }

        [Fact]
        public async Task Haengende_Arbeit_Abbruch_KehrtSofortZurueck()
        {
            var gate = new AbandonableWorkGate();
            using var release = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();

            Task<int> run = gate.RunAsync(_ => { started.Set(); release.Wait(); return 1; }, cts.Token);
            Assert.True(started.Wait(5000));

            var sw = Stopwatch.StartNew();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
            Assert.True(gate.HasAbandonedWork);

            release.Set();
            Assert.True(await WaitUntilAsync(() => !gate.HasAbandonedWork));
        }

        [Fact]
        public async Task Solange_Zurueckgelassen_WirdNeueArbeit_Abgelehnt_UndKeineZweiteAngesammelt()
        {
            var gate = new AbandonableWorkGate();
            using var release = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();

            Task<int> run = gate.RunAsync(_ => { started.Set(); release.Wait(); return 1; }, cts.Token);
            Assert.True(started.Wait(5000));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

            int started2 = 0;
            for (int i = 0; i < 5; i++)
            {
                await Assert.ThrowsAsync<AbandonedWorkStillRunningException>(
                    () => gate.RunAsync(_ => { Interlocked.Increment(ref started2); return 2; }, CancellationToken.None));
            }
            Assert.Equal(0, Volatile.Read(ref started2));

            release.Set();
            Assert.True(await WaitUntilAsync(() => !gate.HasAbandonedWork));
        }

        [Fact]
        public async Task SpaeterEnde_Der_ZurueckgelassenenArbeit_BeeinflusstNeueArbeitNicht()
        {
            var gate = new AbandonableWorkGate();
            using var release = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();
            int lateSideEffect = 0;

            Task<int> run = gate.RunAsync(_ => { started.Set(); release.Wait(); Interlocked.Increment(ref lateSideEffect); return 111; }, cts.Token);
            Assert.True(started.Wait(5000));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

            release.Set();
            Assert.True(await WaitUntilAsync(() => !gate.HasAbandonedWork));

            // Neue Arbeit bekommt genau ihr eigenes Ergebnis, nicht das der zurueckgelassenen.
            int fresh = await gate.RunAsync(_ => 222, CancellationToken.None);
            Assert.Equal(222, fresh);
            Assert.Equal(1, lateSideEffect); // die alte Arbeit lief zu Ende, ihr Ergebnis ging aber nirgends ein
        }

        [Fact]
        public async Task Ausnahme_Der_ZurueckgelassenenArbeit_Wird_Eingesammelt()
        {
            var gate = new AbandonableWorkGate();
            using var release = new ManualResetEventSlim(false);
            using var started = new ManualResetEventSlim(false);
            using var cts = new CancellationTokenSource();

            Task<int> run = gate.RunAsync<int>(_ => { started.Set(); release.Wait(); throw new System.IO.IOException("Laufwerk weg"); }, cts.Token);
            Assert.True(started.Wait(5000));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

            release.Set();
            Assert.True(await WaitUntilAsync(() => !gate.HasAbandonedWork));
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [Fact]
        public async Task Bereits_Abgebrochener_Token_StartetKeineArbeit()
        {
            var gate = new AbandonableWorkGate();
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            int ran = 0;
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => gate.RunAsync(_ => { Interlocked.Increment(ref ran); return 1; }, cts.Token));
            Assert.Equal(0, ran);
            Assert.False(gate.HasAbandonedWork);
        }

        [Fact]
        public async Task Kooperative_Arbeit_Wird_Ueber_Verknuepften_Token_Beendet()
        {
            var gate = new AbandonableWorkGate();
            using var cts = new CancellationTokenSource();
            Task<int> run = gate.RunAsync<int>(token =>
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    Thread.Sleep(5);
                }
            }, cts.Token);
            cts.CancelAfter(50);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.True(await WaitUntilAsync(() => !gate.HasAbandonedWork));
        }
    }
}
