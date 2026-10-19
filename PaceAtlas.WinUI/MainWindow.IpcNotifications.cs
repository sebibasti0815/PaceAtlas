using Microsoft.UI.Xaml;
using PaceAtlas.IPC;

namespace PaceAtlas.WinUI;

public sealed partial class MainWindow
{
    private DataChangeNotifier? ipcChanges;
    private DataChangeKind pendingIpcChanges;
    private readonly DispatcherTimer ipcRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };

    internal void AttachIpcNotifications(DataChangeNotifier notifier)
    {
        ipcChanges = notifier;
        notifier.Changed += OnIpcDataChanged;
        ipcRefreshTimer.Tick += (_, _) =>
        {
            ipcRefreshTimer.Stop();
            var changed = pendingIpcChanges;
            pendingIpcChanges = DataChangeKind.None;
            try
            {
                // Refresh list data, not form fields: unsaved edits must remain intact.
                if ((changed & DataChangeKind.Medications) != 0)
                {
                    ReloadMedicationData();
                    RenderIntakes();
                    RenderStock();
                    RefreshMedicationDueIndicators();
                }
                if ((changed & (DataChangeKind.Status | DataChangeKind.Activities | DataChangeKind.Medications | DataChangeKind.Measures)) != 0)
                {
                    LoadEntries();
                    RefreshTodaySummary();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IPC UI refresh failed: " + ex);
            }
        };
        Closed += (_, _) =>
        {
            if (ipcChanges is not null) ipcChanges.Changed -= OnIpcDataChanged;
            ipcRefreshTimer.Stop();
        };
    }

    private void OnIpcDataChanged(DataChangeKind kind)
    {
        // Pipe requests execute on background threads; only touch WinUI on its dispatcher.
        DispatcherQueue.TryEnqueue(() =>
        {
            pendingIpcChanges |= kind;
            ipcRefreshTimer.Stop();
            ipcRefreshTimer.Start();
        });
    }
}
