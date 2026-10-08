using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace P7SExtractor
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            this.settings = settings;

            txtDefaultFolder.Text = settings.DefaultSaveFolder;
            chkAutoSave.IsChecked = settings.AutoSaveAfterAnalyze;
            chkAutoOpen.IsChecked = settings.AutoOpenAfterSave;
            chkKeepTemp.IsChecked = settings.KeepTempPreviewFiles;

            SelectLanguage(settings.Language);
            ApplyLanguage(settings.Language);
        }

        private void SelectLanguage(string language)
        {
            // set radio buttons according to saved language (default to ro)
            if (language == "en")
            {
                rbLangEn.IsChecked = true;
            }
            else
            {
                rbLangRo.IsChecked = true;
            }
        }

        private void btnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = Localization.T("Alege folderul implicit pentru salvare", "Choose default save folder"),
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Selecteaza folder"
            };

            if (Directory.Exists(txtDefaultFolder.Text))
                dlg.InitialDirectory = txtDefaultFolder.Text;

            if (dlg.ShowDialog(this) == true)
            {
                string? folder = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrWhiteSpace(folder))
                    txtDefaultFolder.Text = folder;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void rbLanguage_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string lang)
            {
                ApplyLanguage(lang);
            }
        }

        private void ApplyLanguage(string lang)
        {
            Localization.SetLanguage(lang);

            Title = Localization.T("Setari", "Settings");
            lblTitle.Text = Localization.T("Setari", "Settings");
            lblDefaultFolder.Text = Localization.T("Folder implicit pentru salvare:", "Default save folder:");
            btnBrowseFolder.Content = Localization.T("Browse", "Browse");

            chkAutoOpen.Content = Localization.T("Deschide automat fisierul dupa salvare", "Auto-open file after saving");
            chkAutoSave.Content = Localization.T("Salveaza automat dupa analiza", "Auto-save extracted file after analysis");
            chkKeepTemp.Content = Localization.T("Pastreaza fisierele temporare de preview", "Keep temporary preview files");

            lblLanguage.Text = Localization.T("Limba:", "Language:");

            rbLangRo.Content = Localization.T("Romana", "Romanian");
            rbLangEn.Content = Localization.T("English", "English");

            btnCancel.Content = Localization.T("Anuleaza", "Cancel");
            btnSave.Content = Localization.T("Salveaza", "Save");
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            settings.DefaultSaveFolder = txtDefaultFolder.Text?.Trim() ?? string.Empty;
            settings.AutoSaveAfterAnalyze = chkAutoSave.IsChecked == true;
            settings.AutoOpenAfterSave = chkAutoOpen.IsChecked == true;
            settings.KeepTempPreviewFiles = chkKeepTemp.IsChecked == true;

            // read language from radio buttons
            if (rbLangEn.IsChecked == true && rbLangEn.Tag is string enTag)
                settings.Language = enTag;
            else if (rbLangRo.IsChecked == true && rbLangRo.Tag is string roTag)
                settings.Language = roTag;

            Localization.SetLanguage(settings.Language);

            try
            {
                settings.SaveSettings();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Eroare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // removed signature preview; signature display is handled elsewhere in the app
    }
}
