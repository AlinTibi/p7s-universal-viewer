using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace P7SExtractor
{
    public partial class MainWindow : Window
    {
        private enum PreviewTab
        {
            Pdf,
            Image,
            Word,
            Archive,
            Signature
        }

        private byte[]? ExtractInnerFileFromArchive(string entryKey)
        {
            if (archiveParentContent == null || archiveParentContent.Length == 0)
                return null;

            try
            {
                using var ms = new MemoryStream(archiveParentContent);
                using IArchive archive = ArchiveFactory.Open(ms);

                var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory && string.Equals(e.Key, entryKey, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    return null;

                using var outMs = new MemoryStream();
                entry.WriteTo(outMs);
                return outMs.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private void listArchive_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listArchive.SelectedItem == null)
            {
                txtStatus.Text = Localization.T("Selecteaza un fisier din arhiva.", "Select a file from the archive.");
                return;
            }

            string entry = listArchive.SelectedItem as string ?? listArchive.SelectedItem.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entry))
            {
                txtStatus.Text = Localization.T("Intrare invalida selectata.", "Invalid entry selected.");
                return;
            }

            var inner = ExtractInnerFileFromArchive(entry);
            if (inner == null || inner.Length == 0)
            {
                txtStatus.Text = Localization.T("Eroare la extragerea fisierului din arhiva.", "Error extracting file from archive.");
                return;
            }

            // Set current extracted content to the inner file and preview it
            currentInnerContent = inner;
            extractedContent = inner;

            // Try to guess type from entry name first
            string ext = Path.GetExtension(entry);
            if (string.IsNullOrWhiteSpace(ext))
            {
                DetectContentType(extractedContent, out ext, out string typeName);
            }
            else
            {
                DetectContentType(extractedContent, out _, out string typeName);
            }

            detectedExtension = string.IsNullOrWhiteSpace(ext) ? ".bin" : ext;
            DetectContentType(extractedContent, out string detectedExt2, out string detectedTypeName);
            detectedExtension = detectedExt2;
            detectedType = detectedTypeName;

            txtDetectedType.Text = detectedType;
            txtDetectedExtension.Text = detectedExtension;
            txtSize.Text = FormatSize(extractedContent.Length);

            btnView.IsEnabled = true;
            btnSave.IsEnabled = true;
            btnOpenExternal.IsEnabled = true;
            btnSaveTemp.IsEnabled = true;

            ShowPreview();
            txtStatus.Text = string.Format(Localization.T("Fisier extras din arhiva: {0}", "File extracted from archive: {0}"), entry);

            // After extracting an inner file we keep archiveParentContent so the user
            // can click the "Arhiva" tab to return to the listing. No back button needed.
        }

        // Back-to-archive button removed; users can click the "Arhiva" tab to return to the archive listing.


        private sealed class P7sFileRow
        {
            public string FileName { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string DetectedType { get; set; } = "-";
            public string DetectedExtension { get; set; } = "-";
            public long SizeBytes { get; set; }
            public string SizeDisplay { get; set; } = "-";
            public string SignatureStatus { get; set; } = Localization.T("Necunoscuta", "Unknown");
            public string PreviewAvailable { get; set; } = Localization.T("Nu", "No");

            public byte[] ExtractedContent { get; set; } = Array.Empty<byte>();
            public string SignatureInfo { get; set; } = string.Empty;

            public string Signer { get; set; } = "";
            public string Issuer { get; set; } = "";
            public DateTime? AnalysisDate { get; set; }
        }

        // Legacy/internal collection used by existing preview/save/batch logic.
        private readonly ObservableCollection<P7sFileRow> loadedFiles = new();

        private AppSettings appSettings = new();

        private string selectedFilePath = string.Empty;
        private byte[] extractedContent = Array.Empty<byte>();
        private string detectedExtension = ".bin";
        private string detectedType = Localization.T("Necunoscut", "Unknown");
        private string tempPreviewFile = string.Empty;
        // Archive state
        private List<string> currentArchiveEntries = new List<string>();
        private byte[] archiveParentContent = Array.Empty<byte>();
        private byte[] currentInnerContent = Array.Empty<byte>();

        public MainWindow()
        {
            InitializeComponent();
            appSettings = AppSettings.LoadSettings();
            // Default to Romanian if no language is configured so the UI is
            // consistent for Romanian-speaking users. Users can change this in settings.
            if (string.IsNullOrWhiteSpace(appSettings.Language))
            {
                appSettings.Language = "ro";
            }
            LanguageManager.SetLanguage(appSettings.Language);
            ApplyLanguage();
            SetInitialState();

            SetActivePreviewTab(PreviewTab.Pdf);

            // Show donation prompt once per day if enabled
            try
            {
                // Use LastDonationPromptDate from settings to show only once per day
                string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                if (!string.Equals(appSettings.LastDonationPromptDate, today, StringComparison.OrdinalIgnoreCase))
                {
                    var wnd = new DonationWindow() { Owner = this };
                    wnd.ShowDialog();
                    // Save date after showing
                    try
                    {
                        appSettings.LastDonationPromptDate = today;
                        appSettings.SaveSettings();
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void SetActivePreviewTab(PreviewTab tab)
        {
            previewPdfPanel.Visibility = tab == PreviewTab.Pdf ? Visibility.Visible : Visibility.Collapsed;
            previewImagePanel.Visibility = tab == PreviewTab.Image ? Visibility.Visible : Visibility.Collapsed;
            previewTextPanel.Visibility = (tab == PreviewTab.Word || tab == PreviewTab.Signature) ? Visibility.Visible : Visibility.Collapsed;
            previewArchivePanel.Visibility = tab == PreviewTab.Archive ? Visibility.Visible : Visibility.Collapsed;
            previewSignaturePanel.Visibility = tab == PreviewTab.Signature ? Visibility.Visible : Visibility.Collapsed;

            // Clear unrelated preview content when switching tabs to avoid showing stale
            // messages from a previously active panel.
            if (tab != PreviewTab.Pdf)
            {
                try { pdfViewer.Source = new Uri("about:blank"); } catch { }
            }

            if (tab != PreviewTab.Image)
            {
                try { imagePreview.Source = null; } catch { }
            }

            if (tab != PreviewTab.Word && tab != PreviewTab.Archive && tab != PreviewTab.Signature)
            {
                txtPreview.Text = string.Empty;
            }

            // Do not clear archiveParentContent when switching tabs. Keep the archive
            // bytes and entries in memory so the user can click the "Arhiva" tab and
            // return to the listing without needing a Back button. Only clear the
            // ListBox visual binding to avoid showing archive entries in non-archive
            // panels.
            if (tab != PreviewTab.Archive)
            {
                try { listArchive.ItemsSource = null; } catch { }
                currentInnerContent = Array.Empty<byte>();
            }

            if (tab != PreviewTab.Signature)
            {
                txtSignature.Text = string.Empty;
            }

            btnPreviewTabPdf.Tag = tab == PreviewTab.Pdf ? "Active" : null;
            btnPreviewTabImage.Tag = tab == PreviewTab.Image ? "Active" : null;
            btnPreviewTabWord.Tag = tab == PreviewTab.Word ? "Active" : null;
            btnPreviewTabArchive.Tag = tab == PreviewTab.Archive ? "Active" : null;
            btnPreviewTabSignature.Tag = tab == PreviewTab.Signature ? "Active" : null;
        }

        private bool IsPreviewContentAvailable(PreviewTab tab)
        {
            if (!HasContent())
                return false;

            return tab switch
            {
                PreviewTab.Pdf => IsPdf(detectedExtension),
                PreviewTab.Image => IsImage(detectedExtension),
                PreviewTab.Word => detectedExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase),
                PreviewTab.Archive => IsArchive(detectedExtension),
                PreviewTab.Signature => true,
                _ => false
            };
        }

        private void TryActivatePreviewTab(PreviewTab tab)
        {
            // Clear any stale preview content before activating the requested tab so
            // messages from previous actions don't remain visible in the new tab.
            try
            {
                txtPreview.Text = string.Empty;
                txtSignature.Text = string.Empty;
                gridPreview.ItemsSource = null;
                try { pdfViewer.Source = new Uri("about:blank"); } catch { }
                try { imagePreview.Source = null; } catch { }
            }
            catch { }

            SetActivePreviewTab(tab);
            if (!HasContent())
            {
                PreviewText(Localization.T("Nu exista continut incarcat.", "No content loaded."), activate: false);
                return;
            }

            switch (tab)
            {
                case PreviewTab.Pdf:
                    if (IsPdf(detectedExtension))
                        PreviewPdf(extractedContent, detectedExtension);
                    else
                    {
                        // Keep PDF tab selected but do not overlay the text panel with an error
                        // message. Clear the PDF viewer and show a status message instead.
                        SetActivePreviewTab(PreviewTab.Pdf);
                        try
                        {
                            pdfViewer.Source = new Uri("about:blank");
                        }
                        catch { }
                        txtStatus.Text = Localization.T("Nu este un fisier PDF.", "Not a PDF file.");
                    }
                    break;
                case PreviewTab.Image:
                    if (IsImage(detectedExtension))
                        PreviewImage(extractedContent);
                    else
                    {
                        // Keep Image tab selected but do not overlay the text panel.
                        imagePreview.Source = null;
                        txtStatus.Text = Localization.T("Nu este o imagine.", "Not an image file.");
                    }
                    break;
                case PreviewTab.Word:
                    if (detectedExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                        PreviewText(ExtractDocxText(extractedContent));
                    else
                        PreviewText(Localization.T("Nu este un document Word (DOCX).", "Not a Word (DOCX) document."), activate: true);
                    break;
                case PreviewTab.Archive:
                    // If we previously stored the parent archive bytes (user extracted an inner file),
                    // show the listing from that archive. Otherwise if the current content is an
                    // archive show its listing.
                    if (archiveParentContent != null && archiveParentContent.Length > 0)
                    {
                        PreviewArchive(BuildArchiveListing(archiveParentContent, ".zip"));
                    }
                    else if (IsArchive(detectedExtension))
                    {
                        PreviewArchive(BuildArchiveListing(extractedContent, detectedExtension));
                    }
                    else
                    {
                        SetActivePreviewTab(PreviewTab.Archive);
                        txtStatus.Text = Localization.T("Nu este o arhiva suportata.", "Not a supported archive.");
                    }
                    break;
                case PreviewTab.Signature:
                    txtPreview.Text = txtSignatureInfo?.Text ?? string.Empty;
                    break;
                default:
                    PreviewText(Localization.T("Nu exista continut preview pentru aceasta fila.", "No preview content available for this tab."), activate: false);
                    break;
            }
        }

        private void PreviewTabPdf_Click(object sender, RoutedEventArgs e) => TryActivatePreviewTab(PreviewTab.Pdf);
        private void PreviewTabImage_Click(object sender, RoutedEventArgs e) => TryActivatePreviewTab(PreviewTab.Image);
        private void PreviewTabWord_Click(object sender, RoutedEventArgs e) => TryActivatePreviewTab(PreviewTab.Word);
        private void PreviewTabArchive_Click(object sender, RoutedEventArgs e) => TryActivatePreviewTab(PreviewTab.Archive);
        private void PreviewTabSignature_Click(object sender, RoutedEventArgs e) => TryActivatePreviewTab(PreviewTab.Signature);

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wnd = new DonationWindow() { Owner = this };
                wnd.ShowDialog();
            }
            catch { }
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wnd = new AboutWindow() { Owner = this };
                wnd.ShowDialog();
            }
            catch { }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new SettingsWindow(appSettings)
            {
                Owner = this
            };

            if (wnd.ShowDialog() == true)
            {
                appSettings = AppSettings.LoadSettings();
                ApplyLanguage();
                txtStatus.Text = Localization.T("Setarile au fost salvate.", "Settings saved.");
                Logger.LogInfo("Settings_Saved", message: $"lang={appSettings.Language}; folder={appSettings.DefaultSaveFolder}; autosave={appSettings.AutoSaveAfterAnalyze}; autoopen={appSettings.AutoOpenAfterSave}; keeptemp={appSettings.KeepTempPreviewFiles}");
            }
        }

        private void ApplyLanguage()
        {
            lblAppTitle.Text = Localization.T("P7S Universal Viewer", "P7S Universal Viewer");

            btnBrowse.Content = Localization.T("Alege fisier", "Choose file");
            btnAnalyze.Content = Localization.T("Incarca si analizeaza", "Load and analyze");
            btnClear.Content = Localization.T("Reset", "Reset");
            btnSettings.Content = Localization.T("Setari", "Settings");
            try { btnAbout.Content = Localization.T("Despre", "About"); } catch { }
            // Localize donate button in header
            try { btnDonate.Content = Localization.T("Donează ❤️", "Donate ❤️"); } catch { }

            lblDetectedType.Text = Localization.T("Tip detectat:", "Detected type:");
            lblDetectedExt.Text = Localization.T("Extensie:", "Extension:");
            lblDetectedSize.Text = Localization.T("Dimensiune:", "Size:");

            lblLoadedFiles.Text = Localization.T("Fisiere incarcate (.p7s)", "Loaded files (.p7s)");
            btnLoadFiles.Content = Localization.T("Incarca fisiere", "Load files");
            btnAnalyzeAll.Content = Localization.T("Analizeaza toate", "Analyze all");
            btnPreviewSelected.Content = Localization.T("Preview selectat", "Preview selected");
            btnSaveSelected.Content = Localization.T("Salveaza selectat", "Save selected");
            btnExportCsv.Content = Localization.T("Export raport CSV", "Export CSV report");

            btnPreviewTabPdf.Content = Localization.T("PDF", "PDF");
            btnPreviewTabImage.Content = Localization.T("Imagine", "Image");
            btnPreviewTabWord.Content = Localization.T("Word", "Word");
            btnPreviewTabArchive.Content = Localization.T("Arhiva", "Archive");
            btnPreviewTabSignature.Content = Localization.T("Semnatura", "Signature");

            btnView.Content = Localization.T("Vizualizeaza", "View");
            btnSave.Content = Localization.T("Salveaza continutul", "Save content");
            btnOpenExternal.Content = Localization.T("Deschide extern", "Open external");
            btnSaveTemp.Content = Localization.T("Salveaza temporar", "Save temp");

            if (!HasContent())
            {
                txtStatus.Text = Localization.T(
                    "Selecteaza sau trage un fisier .p7s in fereastra.",
                    "Select or drag a .p7s file into the window.");
            }

            lblStatusTitle.Text = Localization.T("Status", "Status");

            // Section titles
            lblFileSectionTitle.Text = Localization.T("Fisier", "File");
            lblAnalysisDetailsTitle.Text = Localization.T("Detalii analiza", "Analysis details");
            lblArchiveContents.Text = Localization.T("Continut arhiva:", "Archive contents:");

            // Loaded files grid headers
            if (dgFiles?.Columns != null && dgFiles.Columns.Count >= 4)
            {
                dgFiles.Columns[0].Header = Localization.T("Nume", "Name");
                dgFiles.Columns[1].Header = Localization.T("Tip", "Type");
                dgFiles.Columns[2].Header = Localization.T("Dimensiune", "Size");
                dgFiles.Columns[3].Header = Localization.T("Semnatura", "Signature");
            }

            // Localize existing items in the UI grid and internal collection so values
            // reflect the newly selected language (e.g. "Valida" -> "Valid").
            try
            {
                // LoadedP7SFile items shown in dgFiles
                foreach (var item in dgFiles.Items.OfType<LoadedP7SFile>())
                {
                    if (!string.IsNullOrWhiteSpace(item.Signature))
                        item.Signature = LocalizeSignatureString(item.Signature);
                }

                // Internal loadedFiles rows
                foreach (var r in loadedFiles)
                {
                    if (!string.IsNullOrWhiteSpace(r.SignatureStatus))
                        r.SignatureStatus = LocalizeSignatureString(r.SignatureStatus);
                    if (!string.IsNullOrWhiteSpace(r.PreviewAvailable))
                        r.PreviewAvailable = LocalizePreviewString(r.PreviewAvailable);
                }

                dgFiles.Items.Refresh();
            }
            catch
            {
            }
        }

        private string LocalizeSignatureString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string up = value.ToUpperInvariant();
            if (up.Contains("VALIDA") || up.Contains("VALID") || up.Contains("SEMNATURA VALIDA") || up.Contains("SIGNATURE VALID"))
                return Localization.T("Valida", "Valid");
            if (up.Contains("INVALIDA") || up.Contains("INVALID") || up.Contains("SEMNATURA INVALIDA") || up.Contains("SIGNATURE INVALID"))
                return Localization.T("Invalida", "Invalid");
            if (up.Contains("NECUNOSCUTA") || up.Contains("UNKNOWN"))
                return Localization.T("Necunoscuta", "Unknown");

            return value;
        }

        private string LocalizePreviewString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string up = value.ToUpperInvariant();
            if (up.Contains("DA") || up.Contains("YES"))
                return Localization.T("Da", "Yes");
            if (up.Contains("NU") || up.Contains("NO"))
                return Localization.T("Nu", "No");

            return value;
        }

        private void SetInitialState()
        {
            txtDetectedType.Text = "-";
            txtDetectedExtension.Text = "-";
            txtSize.Text = "-";
            ResetPreviewOnly();
            txtSignatureInfo.Text = string.Empty;

            btnView.IsEnabled = false;
            btnSave.IsEnabled = false;
            btnOpenExternal.IsEnabled = false;
            btnSaveTemp.IsEnabled = false;

            btnAnalyzeAll.IsEnabled = false;
            btnPreviewSelected.IsEnabled = false;
            btnSaveSelected.IsEnabled = false;
        }

        private void ResetPreviewOnly()
        {
            txtPreview.Text = string.Empty;
            imagePreview.Source = null;
            gridPreview.ItemsSource = null;

            try
            {
                pdfViewer.Source = new Uri("about:blank");
            }
            catch
            {
                try
                {
                    pdfViewer.Source = null;
                }
                catch
                {
                }
            }


            SetActivePreviewTab(PreviewTab.Pdf);
        }

        private void ClearLoadedData()
        {
            if (!appSettings.KeepTempPreviewFiles)
            {
                TryDeleteTempPreviewFile();
            }

            extractedContent = Array.Empty<byte>();
            detectedExtension = ".bin";
            detectedType = "Necunoscut";
            tempPreviewFile = string.Empty;

            txtDetectedType.Text = "-";
            txtDetectedExtension.Text = "-";
            txtSize.Text = "-";

            ResetPreviewOnly();

            btnView.IsEnabled = false;
            btnSave.IsEnabled = false;
            btnOpenExternal.IsEnabled = false;
            btnSaveTemp.IsEnabled = false;

            loadedFiles.Clear();
            dgFiles.Items.Clear();
            // Clear archive-related state when resetting application
            try { listArchive.ItemsSource = null; } catch { }
            currentArchiveEntries.Clear();
            archiveParentContent = Array.Empty<byte>();
            currentInnerContent = Array.Empty<byte>();
            btnAnalyzeAll.IsEnabled = false;
            btnPreviewSelected.IsEnabled = false;
            btnSaveSelected.IsEnabled = false;
        }

        private void TryDeleteTempPreviewFile()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPreviewFile) && File.Exists(tempPreviewFile))
                    File.Delete(tempPreviewFile);
            }
            catch
            {
            }
        }

        private void LoadSelectedFile(string path)
        {
            selectedFilePath = path;
            txtFilePath.Text = selectedFilePath;
            txtStatus.Text = Localization.T("Fisier selectat. Apasa Analizeaza.", "File selected. Click Analyze.");
            Logger.LogInfo("SelectFile", selectedFilePath);

            extractedContent = Array.Empty<byte>();
            detectedExtension = ".bin";
            detectedType = "Necunoscut";
            tempPreviewFile = string.Empty;

            txtDetectedType.Text = "-";
            txtDetectedExtension.Text = "-";
            txtSize.Text = "-";
            ResetPreviewOnly();
            txtSignatureInfo.Text = string.Empty;

            btnView.IsEnabled = false;
            btnSave.IsEnabled = false;
            btnOpenExternal.IsEnabled = false;
            btnSaveTemp.IsEnabled = false;
        }

        private void btnLoadFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Localization.T("Selecteaza fisierele P7S", "Select P7S files"),
                Filter = Localization.T("P7S Files (*.p7s)|*.p7s|All Files (*.*)|*.*", "P7S Files (*.p7s)|*.p7s|All Files (*.*)|*.*"),
                Multiselect = true
            };

            if (dlg.ShowDialog() != true)
                return;

            loadedFiles.Clear();
            dgFiles.Items.Clear();

            foreach (string path in dlg.FileNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                loadedFiles.Add(new P7sFileRow
                {
                    FileName = System.IO.Path.GetFileName(path),
                    Path = path,
                    SignatureStatus = Localization.T("Necunoscuta", "Unknown"),
                    PreviewAvailable = Localization.T("Nu", "No")
                });

                dgFiles.Items.Add(new LoadedP7SFile
                {
                    Name = System.IO.Path.GetFileName(path),
                    Type = "-",
                    Size = "-",
                    Signature = Localization.T("Necunoscuta", "Unknown")
                });
            }

            btnAnalyzeAll.IsEnabled = loadedFiles.Count > 0;
            btnPreviewSelected.IsEnabled = loadedFiles.Count > 0;
            btnSaveSelected.IsEnabled = loadedFiles.Count > 0;

            txtStatus.Text = string.Format(Localization.T("Au fost incarcate {0} fisiere. Apasa 'Analizeaza toate'.", "Loaded {0} files. Click 'Analyze all'."), loadedFiles.Count);
            Logger.LogInfo("LoadFiles", message: $"count={loadedFiles.Count}");
        }

        private async void btnAnalyzeAll_Click(object sender, RoutedEventArgs e)
        {
            if (loadedFiles.Count == 0)
            {
                txtStatus.Text = Localization.T("Nu exista fisiere in lista.", "No files in the list.");
                Logger.LogWarning("AnalyzeAll_NoFiles");
                return;
            }

            txtStatus.Text = Localization.T("Analizez fisierele...", "Analyzing files...");
            Logger.LogInfo("AnalyzeAll_Start", message: $"count={loadedFiles.Count}");

            int ok = 0;
            int fail = 0;

            foreach (var row in loadedFiles)
            {
                if (!File.Exists(row.Path))
                {
                    row.DetectedType = Localization.T("Fisier lipsa", "File missing");
                    row.DetectedExtension = "-";
                    row.SizeBytes = 0;
                    row.SizeDisplay = "-";
                    row.SignatureStatus = Localization.T("Necunoscuta", "Unknown");
                    row.PreviewAvailable = Localization.T("Nu", "No");
                    fail++;
                    continue;
                }

                try
                {
                    byte[] p7sBytes = File.ReadAllBytes(row.Path);
                    row.SizeBytes = p7sBytes.Length;
                    row.SizeDisplay = FormatSize(p7sBytes.Length);

                    SignedCms signedCms = new SignedCms();
                    signedCms.Decode(p7sBytes);

                    row.SignatureInfo = BuildSignatureInfo(signedCms);
                    row.SignatureStatus = GetSignatureStatus(signedCms);
                    row.AnalysisDate = DateTime.Now;

                    var signerCert = GetSignerCertificate(signedCms);
                    row.Signer = signerCert?.Subject ?? string.Empty;
                    row.Issuer = signerCert?.Issuer ?? string.Empty;

                    byte[] content = signedCms.ContentInfo.Content;
                    row.ExtractedContent = content;

                    DetectContentType(content, out string ext, out string typeName);
                    row.DetectedExtension = ext;
                    row.DetectedType = typeName;
                    row.PreviewAvailable = IsPreviewAvailable(ext) ? Localization.T("Da", "Yes") : Localization.T("Nu", "No");

                    ok++;

                    Logger.LogInfo(
                        "AnalyzeFile",
                        row.Path,
                        row.DetectedType,
                        row.SignatureStatus,
                        message: $"ext={row.DetectedExtension}; preview={row.PreviewAvailable}");
                }
                catch (Exception ex)
                {
                    row.DetectedType = Localization.T("Eroare: ", "Error: ") + ex.Message;
                    row.DetectedExtension = "-";
                    row.SignatureStatus = Localization.T("Necunoscuta", "Unknown");
                    row.PreviewAvailable = Localization.T("Nu", "No");
                    row.ExtractedContent = Array.Empty<byte>();
                    row.SignatureInfo = string.Empty;
                    row.Signer = string.Empty;
                    row.Issuer = string.Empty;
                    row.AnalysisDate = DateTime.Now;
                    fail++;

                    Logger.LogError("AnalyzeFile_Failed", ex, row.Path);
                }
            }

            dgFiles.Items.Refresh();
            txtStatus.Text = Localization.T($"Analiza finalizata. OK: {ok}, erori: {fail}.", $"Analysis finished. OK: {ok}, errors: {fail}.");
            Logger.LogInfo("AnalyzeAll_End", message: $"ok={ok}; fail={fail}");

            if (appSettings.AutoSaveAfterAnalyze)
            {
                int saved = SaveAllBatchFiles();
                if (saved > 0)
                    txtStatus.Text += Localization.T($" Salvate automat: {saved}.", $" Auto-saved: {saved}.");
            }

            if (pdfViewer.CoreWebView2 == null)
            {
                try
                {
                    await pdfViewer.EnsureCoreWebView2Async();
                }
                catch
                {
                }
            }
        }

        private void btnPreviewSelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgFiles.SelectedItem == null)
            {
                txtStatus.Text = Localization.T("Selecteaza un fisier din lista.", "Select a file from the list.");
                return;
            }

            P7sFileRow? row = null;

            if (dgFiles.SelectedItem is P7sFileRow directRow)
            {
                row = directRow;
            }
            else if (dgFiles.SelectedItem is LoadedP7SFile uiRow)
            {
                row = loadedFiles.FirstOrDefault(r =>
                    r.FileName.Equals(uiRow.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    ?? loadedFiles.FirstOrDefault(r =>
                        Path.GetFileName(r.Path).Equals(uiRow.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            if (row == null)
            {
                txtStatus.Text = Localization.T("Nu am gasit datele interne pentru fisierul selectat.", "Internal data for the selected file was not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
            {
                txtStatus.Text = Localization.T("Fisierul selectat nu mai exista.", "The selected file no longer exists.");
                return;
            }

            if (row.ExtractedContent == null || row.ExtractedContent.Length == 0)
            {
                txtStatus.Text = Localization.T("Fisierul selectat nu a fost analizat inca. Apasa 'Analizeaza toate' sau analizeaza individual.", "The selected file has not been analyzed yet. Click 'Analyze all' or analyze individually.");
                return;
            }

            selectedFilePath = row.Path;
            txtFilePath.Text = selectedFilePath;

            extractedContent = row.ExtractedContent;
            detectedExtension = row.DetectedExtension;
            detectedType = row.DetectedType;
            txtDetectedType.Text = detectedType;
            txtDetectedExtension.Text = detectedExtension;
            txtSize.Text = FormatSize(extractedContent.Length);
            txtSignatureInfo.Text = row.SignatureInfo;

            btnView.IsEnabled = true;
            btnSave.IsEnabled = true;
            btnOpenExternal.IsEnabled = true;
            btnSaveTemp.IsEnabled = true;

            ShowPreview();
            txtStatus.Text = Localization.T("Preview incarcat pentru fisierul selectat.", "Preview loaded for selected file.");
        }

        private void btnSaveSelected_Click(object sender, RoutedEventArgs e)
        {
            if (dgFiles.SelectedItem == null)
            {
                txtStatus.Text = Localization.T("Selecteaza un fisier din lista.", "Select a file from the list.");
                return;
            }

            P7sFileRow? row = null;

            if (dgFiles.SelectedItem is P7sFileRow directRow)
            {
                row = directRow;
            }
            else if (dgFiles.SelectedItem is LoadedP7SFile uiRow)
            {
                row = loadedFiles.FirstOrDefault(r =>
                    r.FileName.Equals(uiRow.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    ?? loadedFiles.FirstOrDefault(r =>
                        Path.GetFileName(r.Path).Equals(uiRow.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            if (row == null)
            {
                txtStatus.Text = Localization.T("Nu am gasit datele interne pentru fisierul selectat.", "Internal data for the selected file was not found.");
                return;
            }

            if (row.ExtractedContent == null || row.ExtractedContent.Length == 0)
            {
                txtStatus.Text = Localization.T("Fisierul selectat nu a fost analizat inca. Apasa 'Analizeaza toate'.", "The selected file has not been analyzed yet. Click 'Analyze all'.");
                return;
            }

            selectedFilePath = row.Path;
            extractedContent = row.ExtractedContent;
            detectedExtension = row.DetectedExtension;
            detectedType = row.DetectedType;

            btnSave_Click(sender, e);
        }

        private void btnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (loadedFiles.Count == 0)
            {
                txtStatus.Text = Localization.T("Nu exista fisiere in lista.", "No files in the list.");
                Logger.LogWarning("ExportCsv_NoFiles");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Title = Localization.T("Export raport CSV", "Export CSV report"),
                FileName = "raport_p7s.csv",
                Filter = Localization.T("CSV (*.csv)|*.csv|All Files (*.*)|*.*", "CSV (*.csv)|*.csv|All Files (*.*)|*.*")
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                string separator = ",";

                var sb = new StringBuilder();
                    sb.AppendLine(string.Join(separator, new[]
                    {
                        Csv(Localization.T("Nume fisier","File name")),
                        Csv(Localization.T("Cale","Path")),
                        Csv(Localization.T("Tip detectat","Detected type")),
                        Csv(Localization.T("Extensie","Extension")),
                        Csv(Localization.T("Dimensiune","Size")),
                        Csv(Localization.T("Status semnatura","Signature status")),
                        Csv(Localization.T("Semnatar","Signer")),
                        Csv(Localization.T("Emitent","Issuer")),
                        Csv(Localization.T("Data analiza","Analysis date"))
                    }));

                foreach (var row in loadedFiles)
                {
                    sb.AppendLine(string.Join(separator, new[]
                    {
                        Csv(row.FileName),
                        Csv(row.Path),
                        Csv(row.DetectedType),
                        Csv(row.DetectedExtension),
                        Csv(row.SizeDisplay),
                        Csv(row.SignatureStatus),
                        Csv(row.Signer),
                        Csv(row.Issuer),
                        Csv(row.AnalysisDate?.ToString("u") ?? string.Empty)
                    }));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                txtStatus.Text = Localization.T("Raport exportat.", "Report exported.");
                Logger.LogInfo("ExportCsv", message: $"rows={loadedFiles.Count}; path={dlg.FileName}");
            }
            catch (Exception ex)
            {
                txtStatus.Text = Localization.T("Eroare la export. Vezi log-ul.", "Export error. See log.");
                Logger.LogError("ExportCsv_Failed", ex, message: dlg.FileName);
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Localization.T("Selecteaza fisierul P7S","Select P7S file"),
                Filter = Localization.T("P7S Files (*.p7s)|*.p7s|All Files (*.*)|*.*", "P7S Files (*.p7s)|*.p7s|All Files (*.*)|*.*")
            };

            if (dlg.ShowDialog() == true)
            {
                LoadSelectedFile(dlg.FileName);
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            selectedFilePath = string.Empty;
            txtFilePath.Text = string.Empty;
            txtStatus.Text = Localization.T("Aplicatia a fost resetata.", "Application has been reset.");
            ClearLoadedData();
        }

        private async void btnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(selectedFilePath) || !File.Exists(selectedFilePath))
            {
                txtStatus.Text = Localization.T("Selecteaza un fisier P7S valid.", "Select a valid P7S file.");
                Logger.LogWarning("Analyze_NoValidFile", selectedFilePath);
                return;
            }

            try
            {
                txtStatus.Text = Localization.T("Analizez fisierul...", "Analyzing file...");
                byte[] fileBytes = File.ReadAllBytes(selectedFilePath);

                SignedCms signedCms = new SignedCms();
                signedCms.Decode(fileBytes);

                txtSignatureInfo.Text = BuildSignatureInfo(signedCms);

                var signerCert = signedCms.SignerInfos.Count > 0 ? signedCms.SignerInfos[0].Certificate : null;

                extractedContent = signedCms.ContentInfo.Content;

                DetectContentType(extractedContent, out string extension, out string typeName);
                detectedExtension = extension;
                detectedType = typeName;

                txtDetectedType.Text = detectedType;
                txtDetectedExtension.Text = detectedExtension;
                txtSize.Text = FormatSize(extractedContent.Length);

                btnView.IsEnabled = true;
                btnSave.IsEnabled = true;
                btnOpenExternal.IsEnabled = true;
                btnSaveTemp.IsEnabled = true;

                // Do not block the analysis/UI update on WebView2 initialization.
                _ = Dispatcher.InvokeAsync(async () =>
                {
                    if (pdfViewer.CoreWebView2 != null)
                        return;

                    try
                    {
                        await pdfViewer.EnsureCoreWebView2Async();
                    }
                    catch
                    {
                    }
                });

                // Keep legacy sync for existing preview/save/batch logic, but do NOT select it in the grid
                // because the grid is bound to LoadedFiles.
                await Dispatcher.InvokeAsync(SyncCurrentFileToGridRow);

                // Preview is driven by the current analysis state.
                txtFilePath.Text = selectedFilePath;

                txtDetectedType.Text = detectedType;
                txtDetectedExtension.Text = detectedExtension;
                txtSize.Text = FormatSize(extractedContent.Length);

                dgFiles.Items.Clear();

                var row = new LoadedP7SFile
                {
                    Name = System.IO.Path.GetFileName(selectedFilePath),
                    Type = detectedType,
                    Size = FormatSize(extractedContent.Length),
                    Signature = Localization.T("Valida", "Valid")
                };

                dgFiles.Items.Add(row);
                dgFiles.SelectedItem = row;
                dgFiles.ScrollIntoView(row);
                dgFiles.UpdateLayout();

                await System.Windows.Threading.Dispatcher.
                    Yield(System.Windows.Threading.DispatcherPriority.Background);

                ShowPreview();

                txtStatus.Text = Localization.T("Analiza finalizata.", "Analysis finished.");

                // ... grid sync done above ...

                string signatureStatus = GetSignatureStatus(signedCms);
                Logger.LogInfo("Analyze", selectedFilePath, detectedType, signatureStatus, message: $"ext={detectedExtension}; size={extractedContent.Length}");

                if (appSettings.AutoSaveAfterAnalyze)
                {
                    if (SaveExtractedContent(selectedFilePath, extractedContent, detectedExtension, out string savedPath))
                    {
                        txtStatus.Text = Localization.T("Salvare automata reusita: ", "Auto-save succeeded: ") + savedPath;
                        Logger.LogInfo("AutoSave", selectedFilePath, detectedType, signatureStatus, message: savedPath);
                    }
                }
            }
            catch (Exception ex)
            {
                ClearLoadedData();
                txtStatus.Text = Localization.T("Eroare la analiza. Vezi log-ul.", "Analysis error. See log.");
                Logger.LogError("Analyze_Failed", ex, selectedFilePath);
            }
        }

        // NOTE: The user-requested simplified `LoadedP7SFile` model does not contain `FilePath`,
        // `Extension` or `PreviewAvailable`. Any upsert/update logic should be reintroduced only
        // after extending the model again.

        private string GetSafeOutputFilePath(string p7sPath, string detectedExt)
        {
            string outputFolder = appSettings.DefaultSaveFolder;
            if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
                outputFolder = Path.GetDirectoryName(p7sPath) ?? Environment.CurrentDirectory;

            string baseName = Path.GetFileNameWithoutExtension(p7sPath);
            string ext = string.IsNullOrWhiteSpace(detectedExt) ? ".bin" : detectedExt;

            string candidate = Path.Combine(outputFolder, baseName + ext);
            if (!File.Exists(candidate))
                return candidate;

            for (int i = 1; i < 10000; i++)
            {
                string alt = Path.Combine(outputFolder, baseName + $" ({i})" + ext);
                if (!File.Exists(alt))
                    return alt;
            }

            return Path.Combine(outputFolder, baseName + "_" + Guid.NewGuid().ToString("N") + ext);
        }

        private bool SaveExtractedContent(string p7sPath, byte[] content, string detectedExt, out string savedPath)
        {
            savedPath = string.Empty;

            if (content == null || content.Length == 0)
                return false;

            try
            {
                string path = GetSafeOutputFilePath(p7sPath, detectedExt);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, content);
                savedPath = path;

                if (appSettings.AutoOpenAfterSave)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private int SaveAllBatchFiles()
        {
            int saved = 0;

            foreach (var row in loadedFiles)
            {
                if (row.ExtractedContent == null || row.ExtractedContent.Length == 0)
                    continue;

                if (string.IsNullOrWhiteSpace(row.Path))
                    continue;

                if (SaveExtractedContent(row.Path, row.ExtractedContent, row.DetectedExtension, out _))
                    saved++;
            }

            return saved;
        }

        private void btnView_Click(object sender, RoutedEventArgs e)
        {
            if (!HasContent())
            {
                txtStatus.Text = Localization.T("Nu exista continut incarcat.", "No content loaded.");
                return;
            }

            // If legacy Office (DOC/XLS/PPT) prefer to open externally in the user's Office app
            if (detectedExtension.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
                detectedExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
                detectedExtension.Equals(".ppt", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string fileToOpen = WriteTempPreviewFile();
                    Process.Start(new ProcessStartInfo { FileName = fileToOpen, UseShellExecute = true });
                    txtStatus.Text = Localization.T("Fisier deschis extern in aplicatia implicita.", "File opened externally in default app.");
                }
                catch (Exception ex)
                {
                    txtStatus.Text = Localization.T("Eroare la deschidere externa. Vezi log-ul.", "External open error. See log.") + " " + ex.Message;
                }

                return;
            }

            try
            {
                ShowPreview();
                txtStatus.Text = Localization.T("Preview incarcat.", "Preview loaded.");
            }
            catch (Exception ex)
            {
                txtStatus.Text = Localization.T("Eroare la preview:\n", "Preview error:\n") + ex.Message;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!HasContent())
            {
                txtStatus.Text = Localization.T("Nu exista continut de salvat.", "No content to save.");
                Logger.LogWarning("Save_NoContent", selectedFilePath, detectedType);
                return;
            }

            try
            {
                // If a default save folder is configured, save there automatically without prompting.
                if (!string.IsNullOrWhiteSpace(appSettings.DefaultSaveFolder))
                {
                    string path = GetSafeOutputFilePath(selectedFilePath, detectedExtension);
                    File.WriteAllBytes(path, extractedContent);
                    txtStatus.Text = Localization.T("Fisier salvat.", "File saved.") + " " + path;
                    Logger.LogInfo("Save", selectedFilePath, detectedType, message: path);

                    if (appSettings.AutoOpenAfterSave)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = path,
                                UseShellExecute = true
                            });
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    SaveFileDialog dlg = new SaveFileDialog
                    {
                        Title = Localization.T("Salveaza continutul extras", "Save extracted content"),
                        FileName = Path.GetFileNameWithoutExtension(selectedFilePath) + detectedExtension,
                        Filter = BuildFilter(detectedExtension, detectedType),
                        InitialDirectory = string.Empty
                    };

                    if (dlg.ShowDialog() == true)
                    {
                        File.WriteAllBytes(dlg.FileName, extractedContent);
                        txtStatus.Text = Localization.T("Fisier salvat.", "File saved.");
                        Logger.LogInfo("Save", selectedFilePath, detectedType, message: dlg.FileName);

                        if (appSettings.AutoOpenAfterSave)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = dlg.FileName,
                                    UseShellExecute = true
                                });
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = Localization.T("Eroare la salvare. Vezi log-ul.", "Save error. See log.");
                Logger.LogError("Save_Failed", ex, selectedFilePath, detectedType);
            }
        }

        private void btnOpenExternal_Click(object sender, RoutedEventArgs e)
        {
            if (!HasContent())
            {
                txtStatus.Text = Localization.T("Nu exista continut incarcat.", "No content loaded.");
                Logger.LogWarning("OpenExternal_NoContent", selectedFilePath, detectedType);
                return;
            }

            try
            {
                string fileToOpen = WriteTempPreviewFile();
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileToOpen,
                    UseShellExecute = true
                });

                txtStatus.Text = Localization.T("Fisierul a fost deschis cu aplicatia implicita din Windows.", "File opened with the default application in Windows.");
                Logger.LogInfo("OpenExternal", selectedFilePath, detectedType, message: fileToOpen);
            }
            catch (Exception ex)
            {
                txtStatus.Text = Localization.T("Eroare la deschidere externa. Vezi log-ul.", "External open error. See log.");
                Logger.LogError("OpenExternal_Failed", ex, selectedFilePath, detectedType);
            }
        }

        private void btnSaveTemp_Click(object sender, RoutedEventArgs e)
        {
            if (!HasContent())
            {
                txtStatus.Text = Localization.T("Nu exista continut incarcat.", "No content loaded.");
                Logger.LogWarning("SaveTemp_NoContent", selectedFilePath, detectedType);
                return;
            }

            try
            {
                string tempFile = WriteTempPreviewFile();
                txtStatus.Text = Localization.T("Fisier temporar creat.", "Temporary file created.");
                Logger.LogInfo("SaveTemp", selectedFilePath, detectedType, message: tempFile);
            }
            catch (Exception ex)
            {
                txtStatus.Text = Localization.T("Eroare la fisier temporar. Vezi log-ul.", "Temp file error. See log.");
                Logger.LogError("SaveTemp_Failed", ex, selectedFilePath, detectedType);
            }
        }

        private bool HasContent()
        {
            return extractedContent != null && extractedContent.Length > 0;
        }

        private void ShowPreview()
        {
            ResetPreviewOnly();

            if (!HasContent())
                return;

            if (IsPdf(detectedExtension))
            {
                PreviewPdf(extractedContent, detectedExtension);
                return;
            }

            if (IsImage(detectedExtension))
            {
                PreviewImage(extractedContent);
                return;
            }

            if (IsTextLike(detectedExtension))
            {
                PreviewText(GetTextSafely(extractedContent));
                return;
            }

            if (detectedExtension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                string text = ExtractDocxText(extractedContent);
                PreviewText(text);
                txtStatus.Text = Localization.T("DOCX incarcat. Se afiseaza textul extras.", "DOCX loaded. Showing extracted text.");
                return;
            }

            if (detectedExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                DataTable dt = ExtractXlsxFirstSheet(extractedContent);
            if (dt != null && dt.Columns.Count > 0)
            {
                // Excel preview is not a tab anymore, fallback to Word tab for text preview
                SetActivePreviewTab(PreviewTab.Word);
                gridPreview.ItemsSource = dt.DefaultView;
                txtStatus.Text = Localization.T("XLSX incarcat. Se afiseaza primul sheet.", "XLSX loaded. Showing first sheet.");
            }
            else
            {
                SetActivePreviewTab(PreviewTab.Word);
                txtStatus.Text = Localization.T("XLSX detectat, dar nu s-a putut genera preview tabel.", "XLSX detected but could not generate table preview.");
            }
            return;
            }

            if (detectedExtension.Equals(".doc", StringComparison.OrdinalIgnoreCase) ||
                detectedExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
                detectedExtension.Equals(".ppt", StringComparison.OrdinalIgnoreCase))
            {
                SetActivePreviewTab(PreviewTab.Word);
                txtStatus.Text = Localization.T("Format Office vechi detectat. Preview intern nu este disponibil in aceasta versiune. Foloseste 'Deschide extern' sau 'Salveaza continutul'.", "Legacy Office format detected. Internal preview is not available in this version. Use 'Open external' or 'Save content'.");
                return;
            }

            SetActivePreviewTab(PreviewTab.Pdf);
            txtStatus.Text = Localization.T("Preview direct indisponibil pentru acest tip de fisier. Foloseste 'Deschide extern' sau 'Salveaza continutul'.", "Direct preview unavailable for this file type. Use 'Open external' or 'Save content'.");
        }

        private void PreviewPdf(byte[] bytes, string extension)
        {
            SetActivePreviewTab(PreviewTab.Pdf);
            string pdfPath = WriteTempPreviewFile(bytes, extension);
            pdfViewer.Source = new Uri(pdfPath);
        }

        private void PreviewImage(byte[] bytes)
        {
            SetActivePreviewTab(PreviewTab.Image);
            imagePreview.Source = LoadBitmapFromBytes(bytes);
        }

        private void PreviewArchive(string listing)
        {
            // Show archive listing under the dedicated Archive tab and ensure the
            // Archive tab is activated so the button appears selected.
            SetActivePreviewTab(PreviewTab.Archive);
            txtPreview.Text = string.Empty;
            previewTextPanel.Visibility = Visibility.Collapsed;
            previewArchivePanel.Visibility = Visibility.Visible;
                try
                {
                    // Populate the ListBox with entries (the BuildArchiveListing caller prepared currentArchiveEntries).
                    listArchive.ItemsSource = null;
                    listArchive.ItemsSource = currentArchiveEntries;
                    listArchive.UpdateLayout();
                }
                catch { }
            try
            {
                System.Windows.Controls.Panel.SetZIndex(previewArchivePanel, 1000);
                System.Windows.Controls.Panel.SetZIndex(previewPdfPanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewImagePanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewExcelPanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewSignaturePanel, 0);
            }
            catch { }
        }

        private void PreviewText(string text, bool activate = true)
        {
            // Show text in the text viewer. By default activate the Word/text tab.
            // If activate==false the caller only wants to display a short status
            // message without changing the visible preview panel — write to the
            // status bar instead to avoid leaving detached messages in the text box.
            if (!activate)
            {
                txtStatus.Text = text;
                return;
            }

            SetActivePreviewTab(PreviewTab.Word);
            txtPreview.Text = text;

            // Ensure the text panel is visible and brought to front so the user
            // clearly sees the textual preview or listing.
            previewTextPanel.Visibility = Visibility.Visible;
            try
            {
                System.Windows.Controls.Panel.SetZIndex(previewTextPanel, 1000);
                System.Windows.Controls.Panel.SetZIndex(previewPdfPanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewImagePanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewExcelPanel, 0);
                System.Windows.Controls.Panel.SetZIndex(previewSignaturePanel, 0);
            }
            catch
            {
            }
        }

        private string WriteTempPreviewFile()
        {
            return WriteTempPreviewFile(extractedContent, detectedExtension);
        }

        private string WriteTempPreviewFile(byte[] content, string extension)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "P7SExtractorPreview");
            Directory.CreateDirectory(tempFolder);

            tempPreviewFile = Path.Combine(
                tempFolder,
                Path.GetFileNameWithoutExtension(selectedFilePath) + "_" + Guid.NewGuid().ToString("N") + extension);

            File.WriteAllBytes(tempPreviewFile, content);
            return tempPreviewFile;
        }

        private void DetectContentType(byte[] data, out string extension, out string typeName)
        {
            extension = ".bin";
            typeName = Localization.T("Fisier binar necunoscut", "Unknown binary file");

            if (data == null || data.Length == 0)
            {
                extension = ".bin";
                typeName = Localization.T("Fisier gol", "Empty file");
                return;
            }

            if (LooksLikePdf(data))
            {
                extension = ".pdf";
                typeName = "PDF";
                return;
            }

            if (LooksLikePng(data))
            {
                extension = ".png";
                typeName = Localization.T("Imagine PNG", "PNG image");
                return;
            }

            if (LooksLikeJpeg(data))
            {
                extension = ".jpg";
                typeName = Localization.T("Imagine JPEG", "JPEG image");
                return;
            }

            if (LooksLikeGif(data))
            {
                extension = ".gif";
                typeName = Localization.T("Imagine GIF", "GIF image");
                return;
            }

            if (LooksLikeBmp(data))
            {
                extension = ".bmp";
                typeName = Localization.T("Imagine BMP", "BMP image");
                return;
            }

            if (LooksLikeRtf(data))
            {
                extension = ".rtf";
                typeName = Localization.T("Document RTF", "RTF document");
                return;
            }

            if (LooksLikeOleCompoundFile(data))
            {
                extension = GuessLegacyOfficeExtension(data);
                typeName = extension == ".xls"
                    ? Localization.T("Document Excel vechi (XLS)", "Legacy Excel document (XLS)")
                    : Localization.T("Document Office vechi (DOC/XLS/PPT)", "Legacy Office document (DOC/XLS/PPT)");
                return;
            }

            if (LooksLikeZip(data))
            {
                DetectZipBasedType(data, out extension, out typeName);
                return;
            }

            if (LooksLikeRar(data))
            {
                extension = ".rar";
                typeName = Localization.T("Arhiva RAR", "RAR archive");
                return;
            }

            if (LooksLike7z(data))
            {
                extension = ".7z";
                typeName = Localization.T("Arhiva 7z", "7z archive");
                return;
            }

            if (LooksLikeGzip(data))
            {
                extension = ".gz";
                typeName = Localization.T("Arhiva GZip", "GZip archive");
                return;
            }

            string sampleText = GetTextSafely(data, 4096);
            string trimmed = sampleText.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("<"))
                {
                    if (trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                    {
                        extension = ".html";
                        typeName = "HTML";
                        return;
                    }

                    extension = ".xml";
                    typeName = "XML";
                    return;
                }

                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    extension = ".json";
                    typeName = "JSON";
                    return;
                }

                if (LooksLikeText(trimmed))
                {
                    extension = ".txt";
                    typeName = Localization.T("Text", "Text");
                    return;
                }
            }
        }

        private void DetectZipBasedType(byte[] data, out string extension, out string typeName)
        {
            extension = ".zip";
            typeName = "Arhiva ZIP";

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Read, true))
                {
                    var names = archive.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();

                    if (names.Any(n => n.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) &&
                        names.Any(n => n.StartsWith("word/", StringComparison.OrdinalIgnoreCase)))
                    {
                        extension = ".docx";
                        typeName = Localization.T("Document Word DOCX", "Word document (DOCX)");
                        return;
                    }

                    if (names.Any(n => n.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) &&
                        names.Any(n => n.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)))
                    {
                        extension = ".xlsx";
                        typeName = Localization.T("Document Excel XLSX", "Excel document (XLSX)");
                        return;
                    }

                    if (names.Any(n => n.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) &&
                        names.Any(n => n.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase)))
                    {
                        extension = ".pptx";
                        typeName = Localization.T("Prezentare PowerPoint PPTX", "PowerPoint presentation (PPTX)");
                        return;
                    }

                    extension = ".zip";
                    typeName = Localization.T("Arhiva ZIP", "ZIP archive");
                }
            }
            catch
            {
                extension = ".zip";
                typeName = Localization.T("Arhiva ZIP", "ZIP archive");
            }
        }

        private string ExtractDocxText(byte[] content)
        {
            try
            {
                string tempDocx = WriteTempPreviewFile(content, ".docx");

                using (WordprocessingDocument doc = WordprocessingDocument.Open(tempDocx, false))
                {
                    return ExtractDocxParagraphs(doc);
                }
            }
            catch (Exception ex)
            {
                return Localization.T("Eroare la citirea DOCX:\n", "Error reading DOCX:\n") + ex.Message;
            }
        }

        private static string ExtractDocxParagraphs(WordprocessingDocument doc)
        {
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null)
                return Localization.T("Nu s-a putut extrage text din DOCX.", "Could not extract text from DOCX.");

            var sb = new StringBuilder();

            foreach (var p in body.Elements<Paragraph>())
            {
                string line = string.Concat(
                    p.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));

                line = line.Replace("\u00A0", " ");
                sb.AppendLine(line);
            }

            string result = sb.ToString().TrimEnd();
            if (string.IsNullOrWhiteSpace(result))
                return Localization.T("Nu s-a putut extrage text din DOCX.", "Could not extract text from DOCX.");

            return result;
        }

        private DataTable ExtractXlsxFirstSheet(byte[] content)
        {
            try
            {
                string tempXlsx = WriteTempPreviewFile(content, ".xlsx");

                using (SpreadsheetDocument doc = SpreadsheetDocument.Open(tempXlsx, false))
                {
                    return ExtractXlsxToDataTable(doc, maxRows: 200);
                }
            }
            catch
            {
                return new DataTable();
            }
        }

        private static DataTable ExtractXlsxToDataTable(SpreadsheetDocument doc, int maxRows)
        {
            var dt = new DataTable();

            WorkbookPart? workbookPart = doc.WorkbookPart;
            if (workbookPart?.Workbook?.Sheets == null)
                return dt;

            Sheet? firstSheet = workbookPart.Workbook.Sheets.Elements<Sheet>().FirstOrDefault();
            if (firstSheet?.Id == null)
                return dt;

            WorksheetPart worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id);
            SheetData? sheetData = worksheetPart.Worksheet.Elements<SheetData>().FirstOrDefault();
            if (sheetData == null)
                return dt;

            var rows = sheetData.Elements<Row>().Take(maxRows).ToList();
            if (rows.Count == 0)
                return dt;

            uint headerRowIndex = rows.First().RowIndex?.Value ?? 1;
            var headerCells = rows[0].Elements<Cell>().ToList();
            var headerMap = headerCells.ToDictionary(c => GetColumnIndexFromCellReference(c.CellReference?.Value), c => GetCellValue(doc, c));

            int maxColIndex = rows
                .SelectMany(r => r.Elements<Cell>())
                .Select(c => GetColumnIndexFromCellReference(c.CellReference?.Value))
                .DefaultIfEmpty(0)
                .Max();

            if (maxColIndex <= 0)
                return dt;

            bool hasHeader = headerMap.Values.Any(v => !string.IsNullOrWhiteSpace(v));

            for (int col = 1; col <= maxColIndex; col++)
            {
                string header = hasHeader ? (headerMap.TryGetValue(col, out string? h) ? h : string.Empty) : string.Empty;
                header = (header ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(header))
                    header = Localization.T("Col", "Col") + col;

                dt.Columns.Add(MakeUniqueColumnName(dt, header));
            }

            int startRow = hasHeader ? 1 : 0;
            foreach (var row in rows.Skip(startRow))
            {
                var values = new string[maxColIndex];
                foreach (var cell in row.Elements<Cell>())
                {
                    int colIndex = GetColumnIndexFromCellReference(cell.CellReference?.Value);
                    if (colIndex <= 0 || colIndex > maxColIndex)
                        continue;

                    values[colIndex - 1] = GetCellValue(doc, cell);
                }

                DataRow dr = dt.NewRow();
                for (int i = 0; i < maxColIndex; i++)
                    dr[i] = values[i] ?? string.Empty;

                dt.Rows.Add(dr);
            }

            return dt;
        }

        private static string MakeUniqueColumnName(DataTable dt, string name)
        {
            string baseName = name;
            string candidate = baseName;
            int i = 1;

            while (dt.Columns.Contains(candidate))
            {
                candidate = baseName + " (" + i + ")";
                i++;
            }

            return candidate;
        }

        private static int GetColumnIndexFromCellReference(string? cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
                return 0;

            int col = 0;
            foreach (char ch in cellReference)
            {
                if (ch < 'A' || ch > 'Z')
                    break;
                col = (col * 26) + (ch - 'A' + 1);
            }

            return col;
        }

        private static string GetCellValue(SpreadsheetDocument document, Cell cell)
        {
            if (cell == null)
                return string.Empty;

            string value = cell.InnerText ?? string.Empty;

            if (cell.DataType == null)
                return value;

            if (cell.DataType.Value == CellValues.SharedString)
            {
                SharedStringTablePart stringTable = document.WorkbookPart.SharedStringTablePart;
                if (stringTable != null && int.TryParse(value, out int index))
                {
                    return stringTable.SharedStringTable.ElementAt(index).InnerText;
                }
            }

            if (cell.DataType.Value == CellValues.Boolean)
            {
                return value == "1" ? "TRUE" : value == "0" ? "FALSE" : value;
            }

            return value;
        }

        private string GuessLegacyOfficeExtension(byte[] data)
        {
            string text = Encoding.ASCII.GetString(data.Take(Math.Min(data.Length, 4096)).ToArray());

            if (text.IndexOf("Workbook", StringComparison.OrdinalIgnoreCase) >= 0)
                return ".xls";

            return ".doc";
        }


        private BitmapImage LoadBitmapFromBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
        }

        private bool LooksLikePdf(byte[] data)
        {
            return data.Length >= 4 &&
                   data[0] == 0x25 &&
                   data[1] == 0x50 &&
                   data[2] == 0x44 &&
                   data[3] == 0x46;
        }

        private bool LooksLikeZip(byte[] data)
        {
            return data.Length >= 4 &&
                   data[0] == 0x50 &&
                   data[1] == 0x4B;
        }

        private bool LooksLikePng(byte[] data)
        {
            return data.Length >= 8 &&
                   data[0] == 0x89 &&
                   data[1] == 0x50 &&
                   data[2] == 0x4E &&
                   data[3] == 0x47 &&
                   data[4] == 0x0D &&
                   data[5] == 0x0A &&
                   data[6] == 0x1A &&
                   data[7] == 0x0A;
        }

        private bool LooksLikeJpeg(byte[] data)
        {
            return data.Length >= 3 &&
                   data[0] == 0xFF &&
                   data[1] == 0xD8 &&
                   data[2] == 0xFF;
        }

        private bool LooksLikeGif(byte[] data)
        {
            if (data.Length < 6) return false;
            string header = Encoding.ASCII.GetString(data, 0, 6);
            return header == "GIF87a" || header == "GIF89a";
        }

        private bool LooksLikeBmp(byte[] data)
        {
            return data.Length >= 2 &&
                   data[0] == 0x42 &&
                   data[1] == 0x4D;
        }

        private bool LooksLikeRtf(byte[] data)
        {
            if (data.Length < 5) return false;
            string header = Encoding.ASCII.GetString(data, 0, Math.Min(10, data.Length));
            return header.StartsWith(@"{\\rtf");
        }

        private bool LooksLikeOleCompoundFile(byte[] data)
        {
            byte[] sig = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            return data.Length >= 8 && sig.SequenceEqual(data.Take(8));
        }

        private bool LooksLikeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            int controlCount = 0;
            int lengthToCheck = Math.Min(value.Length, 500);

            for (int i = 0; i < lengthToCheck; i++)
            {
                char c = value[i];
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    controlCount++;
            }

            return controlCount < 8;
        }

        private string GetTextSafely(byte[] data, int maxBytes = -1)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            byte[] actualData;

            if (maxBytes > 0 && data.Length > maxBytes)
            {
                actualData = new byte[maxBytes];
                Array.Copy(data, actualData, maxBytes);
            }
            else
            {
                actualData = data;
            }

            try
            {
                return Encoding.UTF8.GetString(actualData);
            }
            catch
            {
                try
                {
                    return Encoding.Default.GetString(actualData);
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private bool IsPdf(string ext)
        {
            return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsImage(string ext)
        {
            string[] exts = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };
            return exts.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsTextLike(string ext)
        {
            string[] exts = { ".txt", ".xml", ".json", ".html", ".rtf" };
            return exts.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsArchive(string ext)
        {
            string[] exts = { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2" };
            return exts.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private string BuildArchiveListing(byte[] content, string extension)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Localization.T("ARHIVA", "ARCHIVE"));
            sb.AppendLine(new string('-', 55));

            try
            {
                using var ms = new MemoryStream(content);

                // Let SharpCompress auto-detect as much as possible.
                using IArchive archive = ArchiveFactory.Open(ms);

                var entries = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(500)
                    .ToList();

                if (entries.Count == 0)
                {
                    sb.AppendLine(Localization.T("(arhiva nu contine fisiere sau nu a putut fi citita)", "(archive contains no files or could not be read)"));
                    return sb.ToString();
                }

                int i = 1;
                currentArchiveEntries = entries.Select(e => e.Key).ToList();

                foreach (var e in entries)
                {
                    string size = e.Size >= 0 ? e.Size.ToString() : "?";
                    sb.AppendLine(string.Format(Localization.T("{0,3}. {1}  ({2} bytes)", "{0,3}. {1}  ({2} bytes)"), i, e.Key, size));
                    i++;
                }

                if (archive.Entries.Count() > entries.Count)
                    sb.AppendLine("...");

                // Store the archive raw bytes to allow extraction on demand
                archiveParentContent = content;

                return sb.ToString();
            }
            catch (Exception ex)
            {
                sb.AppendLine(Localization.T("Eroare la citirea arhivei:", "Error reading archive:"));
                sb.AppendLine(ex.Message);
                return sb.ToString();
            }
        }

        private string BuildFilter(string extension, string typeName)
        {
            switch (extension.ToLower())
            {
                case ".pdf":
                    return "PDF File (*.pdf)|*.pdf|All Files (*.*)|*.*";
                case ".xml":
                    return "XML File (*.xml)|*.xml|All Files (*.*)|*.*";
                case ".txt":
                    return "Text File (*.txt)|*.txt|All Files (*.*)|*.*";
                case ".json":
                    return "JSON File (*.json)|*.json|All Files (*.*)|*.*";
                case ".html":
                    return "HTML File (*.html)|*.html|All Files (*.*)|*.*";
                case ".rtf":
                    return "RTF File (*.rtf)|*.rtf|All Files (*.*)|*.*";
                case ".zip":
                    return "ZIP Archive (*.zip)|*.zip|All Files (*.*)|*.*";
                case ".docx":
                    return "Word Document (*.docx)|*.docx|All Files (*.*)|*.*";
                case ".xlsx":
                    return "Excel Document (*.xlsx)|*.xlsx|All Files (*.*)|*.*";
                case ".pptx":
                    return "PowerPoint Presentation (*.pptx)|*.pptx|All Files (*.*)|*.*";
                case ".doc":
                    return "Word 97-2003 (*.doc)|*.doc|All Files (*.*)|*.*";
                case ".xls":
                    return "Excel 97-2003 (*.xls)|*.xls|All Files (*.*)|*.*";
                case ".jpg":
                    return "JPEG Image (*.jpg)|*.jpg|All Files (*.*)|*.*";
                case ".png":
                    return "PNG Image (*.png)|*.png|All Files (*.*)|*.*";
                case ".gif":
                    return "GIF Image (*.gif)|*.gif|All Files (*.*)|*.*";
                case ".bmp":
                    return "Bitmap Image (*.bmp)|*.bmp|All Files (*.*)|*.*";
                default:
                    return $"{typeName} (*{extension})|*{extension}|All Files (*.*)|*.*";
            }
        }

        private bool LooksLikeRar(byte[] data)
        {
            // RAR4: 52 61 72 21 1A 07 00 ; RAR5: 52 61 72 21 1A 07 01 00
            if (data.Length < 7)
                return false;

            return data[0] == 0x52 && data[1] == 0x61 && data[2] == 0x72 && data[3] == 0x21 && data[4] == 0x1A && data[5] == 0x07 && (data[6] == 0x00 || data[6] == 0x01);
        }

        private bool LooksLike7z(byte[] data)
        {
            // 37 7A BC AF 27 1C
            if (data.Length < 6)
                return false;

            return data[0] == 0x37 && data[1] == 0x7A && data[2] == 0xBC && data[3] == 0xAF && data[4] == 0x27 && data[5] == 0x1C;
        }

        private bool LooksLikeGzip(byte[] data)
        {
            // 1F 8B
            return data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B;
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";

            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.00") + " KB";

            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.00") + " MB";

            double gb = mb / 1024.0;
            return gb.ToString("0.00") + " GB";
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && files[0].EndsWith(".p7s", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Copy;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
                return;

            string file = files[0];

            if (!file.EndsWith(".p7s", StringComparison.OrdinalIgnoreCase))
            {
                txtStatus.Text = Localization.T("Accept doar fisiere .p7s.", "Only .p7s files are supported.");
                Logger.LogWarning("Drop_Unsupported", file);
                return;
            }

            LoadSelectedFile(file);
        }

        private string BuildSignatureInfo(SignedCms signedCms)
        {
            if (signedCms == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine(Localization.T("Semnatura digitala (P7S)", "Digital signature (P7S)"));
            sb.AppendLine(new string('-', 55));

            bool signatureValid;
            string validationMessage;
            try
            {
                signedCms.CheckSignature(verifySignatureOnly: true);
                signatureValid = true;
                validationMessage = Localization.T("Semnatura valida.", "Signature valid.");
            }
            catch (Exception ex)
            {
                signatureValid = false;
                validationMessage = Localization.T("Semnatura invalida: ", "Signature invalid: ") + ex.Message;
            }

            sb.AppendLine(Localization.T("Validitate: ", "Validity: ") + (signatureValid ? Localization.T("VALIDA", "VALID") : Localization.T("INVALIDA", "INVALID")));
            sb.AppendLine(validationMessage);
            sb.AppendLine();

            if (signedCms.SignerInfos == null || signedCms.SignerInfos.Count == 0)
            {
                sb.AppendLine(Localization.T("Nu exista informatii despre semnatar in acest P7S.", "No signer information present in this P7S."));
                return sb.ToString();
            }

            if (signedCms.SignerInfos.Count > 1)
            {
                sb.AppendLine(string.Format(Localization.T("Semnatari: {0} (se afiseaza primul)", "Signers: {0} (showing first)"), signedCms.SignerInfos.Count));
                sb.AppendLine();
            }

            SignerInfo signer = signedCms.SignerInfos[0];
            X509Certificate2? cert = null;
            try
            {
                cert = signer.Certificate;
            }
            catch
            {
            }

            string? signingTime = null;
            try
            {
                var st = signer.SignedAttributes
                    .Cast<CryptographicAttributeObject>()
                    .FirstOrDefault(a => a.Oid?.Value == "1.2.840.113549.1.9.5");

                if (st != null)
                {
                    var asnData = new AsnEncodedData(st.Oid, st.Values[0].RawData);
                    signingTime = asnData.Format(multiLine: false);
                }
            }
            catch
            {
            }

            if (!string.IsNullOrWhiteSpace(signingTime))
                sb.AppendLine(Localization.T("Data semnarii: ", "Signing time: ") + signingTime);
            else
                sb.AppendLine(Localization.T("Data semnarii: (indisponibila)", "Signing time: (unavailable)"));

            sb.AppendLine();
            sb.AppendLine(Localization.T("Certificat semnatar", "Signer certificate"));
            sb.AppendLine(new string('-', 55));

            if (cert == null)
            {
                sb.AppendLine(Localization.T("Certificatul semnatarului nu este disponibil in P7S.", "Signer certificate not available in P7S."));
                return sb.ToString();
            }

            sb.AppendLine(Localization.T("Nume semnatar (Subject): ", "Signer name (Subject): ") + cert.Subject);
            sb.AppendLine(Localization.T("Emitent (Issuer): ", "Issuer: ") + cert.Issuer);
            sb.AppendLine(Localization.T("Serie: ", "Serial: ") + cert.SerialNumber);
            sb.AppendLine(Localization.T("Thumbprint: ", "Thumbprint: ") + cert.Thumbprint);
            sb.AppendLine(Localization.T("Valabil de la: ", "Valid from: ") + cert.NotBefore.ToString("u"));
            sb.AppendLine(Localization.T("Valabil pana la: ", "Valid until: ") + cert.NotAfter.ToString("u"));

            return sb.ToString();
        }

        private string GetSignatureStatus(SignedCms signedCms)
        {
            if (signedCms == null)
                return Localization.T("Necunoscuta", "Unknown");

            try
            {
                signedCms.CheckSignature(verifySignatureOnly: true);
                return Localization.T("Valida", "Valid");
            }
            catch
            {
                return Localization.T("Invalida", "Invalid");
            }
        }

        private bool IsPreviewAvailable(string extension)
        {
            if (IsPdf(extension) || IsImage(extension) || IsTextLike(extension))
                return true;

            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                return true;

            if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                return true;

            if (IsArchive(extension))
                return true;

            return false;
        }

        private X509Certificate2? GetSignerCertificate(SignedCms signedCms)
        {
            try
            {
                if (signedCms?.SignerInfos == null || signedCms.SignerInfos.Count == 0)
                    return null;

                return signedCms.SignerInfos[0].Certificate;
            }
            catch
            {
                return null;
            }
        }

        private static string Csv(string? value)
        {
            value ??= string.Empty;

            bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            if (value.Contains('"'))
                value = value.Replace("\"", "\"\"");

            return mustQuote ? "\"" + value + "\"" : value;
        }

        private void SyncCurrentFileToGridRow()
        {
            if (string.IsNullOrWhiteSpace(selectedFilePath))
                return;

            var row = loadedFiles.FirstOrDefault(r =>
                r.Path.Equals(selectedFilePath, StringComparison.OrdinalIgnoreCase));

            if (row == null)
            {
                row = new P7sFileRow
                {
                    FileName = System.IO.Path.GetFileName(selectedFilePath),
                    Path = selectedFilePath
                };

                loadedFiles.Add(row);
            }

            row.DetectedType = detectedType;
            row.DetectedExtension = detectedExtension;
            row.ExtractedContent = extractedContent;
            row.SizeBytes = extractedContent?.Length ?? 0;
            row.SizeDisplay = extractedContent != null && extractedContent.Length > 0 ? FormatSize(extractedContent.Length) : "-";
            row.SignatureInfo = txtSignatureInfo.Text;
            // SignatureInfo may contain localized tokens. Support both RO and EN forms.
            string sigInfoUpper = (row.SignatureInfo ?? string.Empty).ToUpperInvariant();
            bool isValidSig = sigInfoUpper.Contains("VALIDA") || sigInfoUpper.Contains("VALID") || sigInfoUpper.Contains("SEMNTURA VALIDA") || sigInfoUpper.Contains("SIGNATURE VALID") || sigInfoUpper.Contains("SEMNTURA VALIDA") || sigInfoUpper.Contains("SEMNATURA VALIDA");
            bool isInvalidSig = sigInfoUpper.Contains("INVALIDA") || sigInfoUpper.Contains("INVALID") || sigInfoUpper.Contains("SEMNTURA INVALIDA") || sigInfoUpper.Contains("SIGNATURE INVALID");

            row.SignatureStatus = isValidSig ? Localization.T("Valida", "Valid") : isInvalidSig ? Localization.T("Invalida", "Invalid") : Localization.T("Necunoscuta", "Unknown");
            row.PreviewAvailable = IsPreviewAvailable(detectedExtension) ? Localization.T("Da","Yes") : Localization.T("Nu","No");
            row.AnalysisDate ??= DateTime.Now;

            try
            {
                // Normalize newlines and split by single '\n' to avoid complex overloads
                // that can trigger unexpected behavior in some runtime environments.
                var sig = row.SignatureInfo ?? string.Empty;
                var normalized = sig.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');

                var signerLine = lines.FirstOrDefault(l => l.StartsWith(Localization.T("Nume semnatar (Subject): ", "Signer name (Subject): "), StringComparison.OrdinalIgnoreCase)
                                                           || l.StartsWith("Signer name (Subject): ", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(signerLine))
                    row.Signer = signerLine.Substring(signerLine.IndexOf(':') + 1).Trim();

                var issuerLine = lines.FirstOrDefault(l => l.StartsWith(Localization.T("Emitent (Issuer): ", "Issuer: "), StringComparison.OrdinalIgnoreCase)
                                                          || l.StartsWith("Issuer: ", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(issuerLine))
                    row.Issuer = issuerLine.Substring(issuerLine.IndexOf(':') + 1).Trim();
            }
            catch
            {
                // ignore parsing errors
            }

            btnAnalyzeAll.IsEnabled = loadedFiles.Count > 0;
            btnPreviewSelected.IsEnabled = loadedFiles.Count > 0;
            btnSaveSelected.IsEnabled = loadedFiles.Count > 0;

            dgFiles.Items.Refresh();
        }
    }
}
