using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using P7SUniversalViewer.Core;

namespace P7SUniversalViewer;

public partial class BatchWindow : Window
{
    private readonly CmsInspector inspector;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ObservableCollection<Row> rows = [];
    private bool busy;

    public BatchWindow(string[] paths, CmsInspector inspector)
    {
        InitializeComponent();
        this.inspector = inspector;
        Results.ItemsSource = rows;
        Loaded += async (_, _) =>
        {
            busy = true;
            try
            {
                foreach (var path in paths)
                {
                    StatusText.Text = $"Verifying {rows.Count + 1} of {paths.Length} locally…";
                    rows.Add(await Inspect(path, null));
                }
                Results.SelectedIndex = 0;
            }
            catch (OperationCanceledException) { }
            finally
            {
                busy = false;
                UpdateControls();
            }
        };
        Closed += (_, _) => cancellation.Cancel();
    }

    internal (string Path, string? Original)? DetailedFile { get; private set; }

    private async Task<Row> Inspect(string path, string? original)
    {
        var token = cancellation.Token;
        return await Task.Run(async () =>
        {
            var inspection = await inspector.InspectFileAsync(path, original, token);
            return new Row(path, original, VerificationReport.Create(path, original, inspection));
        }, token);
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateControls();

    private void UpdateControls()
    {
        var row = Results.SelectedItem as Row;
        OriginalButton.IsEnabled = !busy && (row?.Report.Detached == true || row?.Original is not null);
        OpenButton.IsEnabled = !busy && row is not null;
        ReportButton.IsEnabled = !busy && rows.Count > 0;
        Guidance.Text = row?.Report.Guidance ?? "";
        if (!busy)
        {
            int valid = rows.Count(r => r.Report.Integrity == Integrity.Valid);
            int invalid = rows.Count(r => r.Report.Integrity == Integrity.Invalid);
            int missing = rows.Count(r => r.Report.State == ContainerState.DetachedContentRequired);
            StatusText.Text = $"{rows.Count} files · {valid} valid · {invalid} failed · {missing} need original · " +
                $"{rows.Count - valid - invalid - missing} other";
        }
    }

    private async void ChooseOriginal(object sender, RoutedEventArgs e)
    {
        if (busy || Results.SelectedItem is not Row row) return;
        var dialog = MainWindow.OriginalDialog(row.Path);
        if (dialog.ShowDialog(this) != true) return;
        int index = Results.SelectedIndex;
        busy = true;
        UpdateControls();
        StatusText.Text = "Verifying the selected original locally…";
        try
        {
            rows[index] = await Inspect(row.Path, dialog.FileName);
            Results.SelectedIndex = index;
        }
        catch (OperationCanceledException) { }
        finally
        {
            busy = false;
            UpdateControls();
        }
    }

    private void OpenDetails(object sender, RoutedEventArgs e)
    {
        if (busy || Results.SelectedItem is not Row row) return;
        DetailedFile = (row.Path, row.Original);
        DialogResult = true;
    }

    private async void SaveReport(object sender, RoutedEventArgs e) =>
        await MainWindow.SaveReport(this, "p7s-batch-report.json", rows.Select(row => row.Report).ToArray());

    private sealed record Row(string Path, string? Original, VerificationReport Report)
    {
        public string AccessibleName => $"{Report.Container}: {Report.Summary}";
    }
}
