using System;
using System.Diagnostics;
using System.Windows;

namespace P7SExtractor
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            LanguageManager.LanguageChanged += ApplyLanguage;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            lblTitle.Text = Localization.T("Despre aplicație", "About");
            lblAppName.Text = "P7S Universal Viewer";
            lblVersion.Text = Localization.T("Versiune: 1.0.0", "Version: 1.0.0");

            txtDescription.Text = Localization.T(
                "P7S Universal Viewer este o aplicație desktop creată pentru deschiderea, extragerea și previzualizarea fișierelor .p7s într-un mod simplu și rapid.",
                "P7S Universal Viewer is a desktop application for opening, extracting, and previewing .p7s files quickly and easily.");

            feat1.Text = Localization.T("- deschide fișiere .p7s", "- opens .p7s files");
            feat2.Text = Localization.T("- extrage conținutul real din interior", "- extracts the original content");
            feat3.Text = Localization.T("- detectează automat tipul fișierului", "- automatically detects the file type");
            feat4.Text = Localization.T("- previzualizează PDF, text, imagini și alte tipuri suportate", "- previews PDF, text, images and other supported file types");
            feat5.Text = Localization.T("- oferă suport pentru procesare rapidă și utilizare practică", "- offers fast processing and practical usability");

            lblUsageTitle.Text = Localization.T("Utilizare:", "Usage:");
            txtUsage.Text = Localization.T(
                "Această aplicație este oferită gratuit. Dacă îți este utilă, poți susține dezvoltarea printr-o donație.",
                "This application is offered free of charge. If you find it useful, you can support its development with a donation.");

            btnDonateAbout.Content = Localization.T("Donează ❤️", "Donate ❤️");
            btnCloseAbout.Content = Localization.T("Închide", "Close");
        }

        private void BtnDonateAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wnd = new DonationWindow() { Owner = this };
                wnd.ShowDialog();
            }
            catch
            {
            }
        }

        private void BtnCloseAbout_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
