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
    private string? source, original, previewPath;
    private ContentType type = new("Unknown", ".bin", PreviewKind.Unsupported);
    private int generation;
    private bool busy;
    public MainWindow()
    {
        InitializeComponent();
        try { if (File.Exists(settingsPath)) { var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath)); KeepCheck.IsChecked = settings?.KeepPreviews ?? false; } } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { MessageText.Text = "Settings could not be read; using defaults."; }
        Closed += (_, _) => { PdfPreview.Dispose(); previews.Dispose(); };
        Loaded += async (_, _) => { var args = Environment.GetCommandLineArgs(); if (args.Length == 2 && File.Exists(args[1])) await OpenFile(args[1]); };
    }
    private async void OpenClick(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Filter = "Signed containers|*.p7s;*.p7m;*.pkcs7;*.cms|All files|*.*" }; if (dialog.ShowDialog(this) == true) await OpenFile(dialog.FileName); }
    private async Task OpenFile(string path) { if (busy) return; source = path; original = null; await Inspect(); }
    private async void VerifyClick(object sender, RoutedEventArgs e) => await Inspect();
    private async void OriginalClick(object sender, RoutedEventArgs e)
    { var dialog = new OpenFileDialog { Title = "Select the exact original file for this detached signature" }; if (dialog.ShowDialog(this) == true) { original = dialog.FileName; await Inspect(); } }
    private async Task Inspect()
    {
        if (source is null || busy) return; busy = true; var current = ++generation;
        try {
            SaveButton.IsEnabled = ExtractButton.IsEnabled = ExternalButton.IsEnabled = VerifyButton.IsEnabled = OriginalButton.IsEnabled = false;
            IntegrityHeader.Text = "VERIFYING…"; MessageText.Text = "Inspecting container and checking signatures locally.";
            inspection = null; SignerList.ItemsSource = null; SignerStatusText.Text = ""; SignerDetailsText.Text = "";
            HidePreviews(); if (PdfPreview.CoreWebView2 is not null) PdfPreview.CoreWebView2.Navigate("about:blank"); previewPath = null; previews.Clear();
            inspection = await inspector.InspectFileAsync(source, original);
            if (current != generation) return;
            IntegrityHeader.Text = inspection.IntegrityLabel;
            IntegrityHeader.Foreground = inspection.Integrity == Integrity.Valid ? Brushes.LightCyan : inspection.Integrity == Integrity.Invalid ? Brushes.Salmon : Brushes.LightSteelBlue;
            TrustHeader.Text = inspection.TrustLabel; CountHeader.Text = $"{inspection.Signers.Count} signer(s)"; MessageText.Text = inspection.Message;
            FileNameText.Text = Path.GetFileName(source); FileInfoText.Text = $"Container: {new FileInfo(source).Length:N0} bytes\n{(inspection.Detached ? "Detached signature" : "Attached container")}";
            OriginalButton.Visibility = inspection.Detached ? Visibility.Visible : Visibility.Collapsed; OriginalButton.IsEnabled = true;
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
    { if (SignerList.SelectedItem is SignerResult signer) { SignerStatusText.Text = $"{signer.IntegrityLabel}\nTrust: {signer.Trust} · Dates: {signer.Dates}\nRevocation: {signer.Revocation}"; SignerDetailsText.Text = signer.CertificateDetail; } }
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
    private async void FileDrop(object sender, DragEventArgs e) { if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files) await OpenFile(files[0]); }
    private sealed record Settings(bool KeepPreviews);
}
