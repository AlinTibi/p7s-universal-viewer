using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using P7SUniversalViewer.Core;

namespace P7SUniversalViewer;
public partial class MainWindow : Window
{
    private readonly CmsInspector inspector = new();
    private readonly PreviewFiles previews = new();
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALMARFELD", "P7SUniversalViewer", "settings.json");
    private Inspection? inspection;
    private VerificationReport? report;
    private string? source, original, previewPath;
    private ContentType type = new("Unknown", ".bin", PreviewKind.Unsupported);
    private int generation;
    private bool busy;
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{typeof(MainWindow).Assembly.GetName().Version!.ToString(3)} · Local processing";
        try { if (File.Exists(settingsPath)) { var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)); KeepCheck.IsChecked = settings?.KeepPreviews ?? false; } } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { MessageText.Text = "Settings could not be read; using defaults."; }
        Closed += (_, _) => { PdfPreview.Dispose(); previews.Dispose(); };
        Loaded += async (_, _) => { var args = Environment.GetCommandLineArgs(); if (args.Length > 1) await OpenFiles(args[1..]); };
    }
    private async void OpenClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Signed containers|*.p7s;*.p7m;*.pkcs7;*.cms|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == true) await OpenFiles(dialog.FileNames);
    }

    private async Task OpenFiles(string[] paths)
    {
        if (busy) return;
        paths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length > 100)
        {
            MessageBox.Show(this, "Select at most 100 containers per batch.", "Batch verification",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (paths.Length == 1) await OpenFile(paths[0]);
        if (paths.Length > 1)
        {
            var batch = new BatchWindow(paths, inspector) { Owner = this };
            if (batch.ShowDialog() == true && batch.DetailedFile is { } file)
                await OpenFile(file.Path, file.Original);
        }
    }

    private async Task OpenFile(string path, string? originalPath = null)
    {
        if (busy) return;
        source = path;
        original = originalPath;
        await Inspect();
    }

    private async void VerifyClick(object sender, RoutedEventArgs e) => await Inspect();

    internal static OpenFileDialog OriginalDialog(string container) => new()
    {
        Title = "Choose the exact original document",
        InitialDirectory = Path.GetDirectoryName(container),
        FileName = Path.GetFileName(VerificationReport.SuggestOriginal(container)) ?? ""
    };

    private async void OriginalClick(object sender, RoutedEventArgs e)
    {
        if (busy || source is null) return;
        var dialog = OriginalDialog(source);
        if (dialog.ShowDialog(this) != true) return;
        original = dialog.FileName;
        await Inspect();
    }

    private async void ReportClick(object sender, RoutedEventArgs e)
    {
        if (report is { } result)
            await SaveReport(this, "p7s-verification-report.json", [result]);
    }

    internal static async Task SaveReport(Window owner, string suggestedName,
        IReadOnlyList<VerificationReport> results)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save verification report (includes filenames and certificate details)",
            FileName = suggestedName,
            Filter = "JSON report|*.json",
            DefaultExt = ".json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(owner) != true) return;
        try
        {
            await ContentSafety.SaveNewAsync(Path.GetDirectoryName(dialog.FileName)!,
                Path.GetFileName(dialog.FileName), VerificationReport.Export(results));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            MessageBox.Show(owner, error.Message, "Could not save verification report",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task Inspect()
    {
        if (source is null || busy) return; busy = true; var current = ++generation;
        try {
            ReportButton.IsEnabled = SaveButton.IsEnabled = ExtractButton.IsEnabled = ExternalButton.IsEnabled = VerifyButton.IsEnabled = OriginalButton.IsEnabled = false;
            IntegrityHeader.Text = "VERIFYING…"; MessageText.Text = "Inspecting container and checking signatures locally.";
            inspection = null; report = null; SignerList.ItemsSource = null; SignerStatusText.Text = ""; SignerDetailsText.Text = "";
            HidePreviews(); if (PdfPreview.CoreWebView2 is not null) PdfPreview.CoreWebView2.Navigate("about:blank"); previewPath = null; previews.Clear();
            inspection = await inspector.InspectFileAsync(source, original);
            if (current != generation) return;
            IntegrityHeader.Text = inspection.IntegrityLabel;
            IntegrityHeader.Foreground = inspection.Integrity == Integrity.Valid ? Brushes.LightCyan : inspection.Integrity == Integrity.Invalid ? Brushes.Salmon : Brushes.LightSteelBlue;
            TrustHeader.Text = inspection.TrustLabel; CountHeader.Text = $"{inspection.Signers.Count} signer(s)"; MessageText.Text = inspection.NextStep;
            FileNameText.Text = Path.GetFileName(source); FileInfoText.Text = inspection.Detached ? "Detached signature" : "CMS container";
            var result = inspection;
            report = await Task.Run(() => VerificationReport.Create(source, original, result));
            ReportButton.IsEnabled = true;
            OriginalButton.Visibility = inspection.Detached || original is not null ? Visibility.Visible : Visibility.Collapsed; OriginalButton.IsEnabled = true;
            SignerList.ItemsSource = inspection.Signers; if (inspection.Signers.Count > 0) SignerList.SelectedIndex = 0;
            VerifyButton.IsEnabled = true;
            if (inspection.Content is not null) {
                type = ContentSafety.Detect(inspection.Content);
                PayloadText.Text = $"{SuggestedName}\n{type.Label}\n{inspection.Content.Length:N0} bytes";
                SaveButton.IsEnabled = ExtractButton.IsEnabled = ExternalButton.IsEnabled = true;
                await Preview(inspection.Content);
            } else { PayloadText.Text = "No extractable content available."; PreviewNotice.Text = inspection.Message; }
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) {
            inspection = null; SaveButton.IsEnabled = ExtractButton.IsEnabled = ExternalButton.IsEnabled = false;
            IntegrityHeader.Text = "ERROR"; MessageText.Text = e.Message;
        } finally { busy = false; }
    }
    private string SuggestedName => ContentSafety.SafeFileName(Path.GetFileName(source ?? "content"), type.Extension);
    private void HidePreviews() { TextPreview.Visibility = ImagePreview.Visibility = PdfPreview.Visibility = Visibility.Collapsed; TextPreview.Clear(); ImagePreview.Source = null; PreviewNotice.Visibility = Visibility.Visible; }
    private async Task Preview(byte[] bytes)
    {
        PreviewNotice.Text = $"{type.Label}\n{bytes.Length:N0} bytes\nExtract or save to open in another application.";
        if (type.Preview == PreviewKind.Text) {
            TextPreview.Text = Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 512 * 1024))) + (bytes.Length > 512 * 1024 ? "\n[Preview limited to 512 KiB; extraction preserves all bytes.]" : "");
            TextPreview.Visibility = Visibility.Visible; PreviewNotice.Visibility = Visibility.Collapsed;
        } else if (type.Preview == PreviewKind.Image) {
            try { ImagePreview.Source = ImagePreviewLoader.Load(bytes); ImagePreview.Visibility = Visibility.Visible; PreviewNotice.Visibility = Visibility.Collapsed; }
            catch (Exception e) when (e is IOException or NotSupportedException or ArgumentException) { PreviewNotice.Text = "Image preview unavailable: " + e.Message; }
        } else if (type.Preview == PreviewKind.Pdf) {
            try {
                previewPath = previews.Write(bytes, ".pdf");
                var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ALMARFELD", "P7SUniversalViewer", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(null, data);
                await PdfPreview.EnsureCoreWebView2Async(environment);
                PdfPreview.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PdfPreview.CoreWebView2.Settings.IsScriptEnabled = false;
                PdfPreview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PdfPreview.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                PdfPreview.CoreWebView2.WebResourceRequested -= BlockRemoteRequest; PdfPreview.CoreWebView2.WebResourceRequested += BlockRemoteRequest;
                PdfPreview.CoreWebView2.NewWindowRequested -= BlockNewWindow; PdfPreview.CoreWebView2.NewWindowRequested += BlockNewWindow;
                PdfPreview.CoreWebView2.Navigate(new Uri(previewPath).AbsoluteUri); PdfPreview.Visibility = Visibility.Visible; PreviewNotice.Visibility = Visibility.Collapsed;
            } catch (Exception e) when (e is WebView2RuntimeNotFoundException or System.Runtime.InteropServices.COMException or InvalidOperationException or IOException) {
                PreviewNotice.Text = "PDF preview needs Microsoft Edge WebView2 Runtime. Nothing is installed automatically. Save the document or choose Open externally. Official download: https://developer.microsoft.com/microsoft-edge/webview2/\n" + e.Message;
            }
        }
    }
    private void BlockRemoteRequest(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    { if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") e.Response = PdfPreview.CoreWebView2.Environment.CreateWebResourceResponse(null,403,"Remote requests disabled",""); }
    private void BlockNewWindow(object? sender, CoreWebView2NewWindowRequestedEventArgs e) => e.Handled = true;
    private void SignerChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    { if (SignerList.SelectedItem is SignerResult signer) { SignerStatusText.Text = $"{signer.IntegrityLabel}\nTrust: {signer.Trust} · Dates: {signer.Dates}\nRevocation: {signer.Revocation}"; SignerDetailsText.Text = signer.CertificateDetail;
        if (signer.ChainStatus.Count > 0)
            SignerDetailsText.Text += "\n\nChain status: " + string.Join(", ", signer.ChainStatus);
        if (signer.Chain.Count > 0)
            SignerDetailsText.Text += "\n\nCERTIFICATE CHAIN\n\n" +
                string.Join("\n\n", signer.Chain.Select(certificate => certificate.Description)); } }
    private async void ExtractClick(object sender, RoutedEventArgs e)
    { var dialog = new OpenFolderDialog { Title = "Choose output folder — existing files are never overwritten" }; if (dialog.ShowDialog(this) == true) await Save(dialog.FolderName, SuggestedName); }
    private async void SaveClick(object sender, RoutedEventArgs e)
    { var dialog = new SaveFileDialog { FileName = SuggestedName, Filter = "Extracted content|*" + type.Extension, OverwritePrompt = true }; if (dialog.ShowDialog(this) == true) await Save(Path.GetDirectoryName(dialog.FileName)!, Path.GetFileName(dialog.FileName)); }
    private async Task Save(string directory, string name)
    {
        if (inspection?.Content is null) return;
        try { await ContentSafety.SaveNewAsync(directory, name, inspection.Content); MessageText.Text = $"Saved {inspection.Content.Length:N0} bytes as {name}."; }
        catch (IOException e) { MessageBox.Show(this, "Nothing was overwritten. Choose a new filename.\n" + e.Message, "Save content", MessageBoxButton.OK, MessageBoxImage.Warning); }
        catch (Exception e) when (e is UnauthorizedAccessException or InvalidDataException or ArgumentException) { MessageBox.Show(this,e.Message,"Cannot save",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    private void ExternalClick(object sender, RoutedEventArgs e)
    { if (inspection?.Content is null) return; if (MessageBox.Show(this,"This opens the extracted document in another application. External applications may access the network. Continue?","Open externally",MessageBoxButton.YesNo,MessageBoxImage.Question) != MessageBoxResult.Yes) return; try { previewPath ??= previews.Write(inspection.Content,type.Extension); Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true }); } catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { MessageText.Text = error.Message; } }
    private void KeepChanged(object sender, RoutedEventArgs e)
    { if (KeepCheck is null) return; previews.Keep = KeepCheck.IsChecked == true; try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath,JsonSerializer.Serialize(new Settings(previews.Keep))); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { MessageText.Text = "Could not save preview preference."; } }
    private void FileDragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void FileDrop(object sender, DragEventArgs e) { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] files) await OpenFiles(files); }
    private sealed record Settings(bool KeepPreviews);
}
