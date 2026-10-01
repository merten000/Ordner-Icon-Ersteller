using Xunit;

namespace OrdnerIconErsteller.Tests
{
    // Runde 2, Befund F-001: Entscheidung beim Schliessen des Hauptfensters waehrend einer
    // laufenden Operation. Vorher: pauschal e.Cancel = true, unabhaengig vom Grund.
    public class OperationGuardTests
    {
        [Theory]
        [InlineData(OperationPhase.Idle, true, CloseAction.Allow)]
        [InlineData(OperationPhase.Idle, false, CloseAction.Allow)]
        [InlineData(OperationPhase.Cancellable, true, CloseAction.AskCancelThenClose)]
        [InlineData(OperationPhase.Cancellable, false, CloseAction.CancelAndAllow)]
        [InlineData(OperationPhase.Committing, true, CloseAction.BlockCommitting)]
        [InlineData(OperationPhase.Committing, false, CloseAction.WaitForCommitAndAllow)]
        public void DecideClose_LiefertErwarteteAktion(OperationPhase phase, bool userInitiated, CloseAction expected)
        {
            Assert.Equal(expected, OperationGuard.DecideClose(phase, userInitiated));
        }

        [Theory]
        [InlineData(OperationPhase.Idle)]
        [InlineData(OperationPhase.Cancellable)]
        [InlineData(OperationPhase.Committing)]
        public void DecideClose_WindowsHerunterfahrenOderTaskManager_BlockiertNie(OperationPhase phase)
        {
            // Kernpunkt von F-001: kein Grund ausser dem Benutzer selbst darf das Schliessen
            // verweigern (Windows-Abmeldung/Herunterfahren, "Task beenden").
            CloseAction action = OperationGuard.DecideClose(phase, userInitiated: false);
            Assert.NotEqual(CloseAction.BlockCommitting, action);
            Assert.NotEqual(CloseAction.AskCancelThenClose, action);
        }
    }
}
