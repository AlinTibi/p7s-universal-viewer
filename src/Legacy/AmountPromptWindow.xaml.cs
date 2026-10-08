using System;
using System.Globalization;
using System.Windows;

namespace P7SExtractor
{
    public partial class AmountPromptWindow : Window
    {
        public decimal Amount { get; private set; }

        public AmountPromptWindow()
        {
            InitializeComponent();
            lblPrompt.Text = Localization.T("Introdu suma pe care vrei sa o donezi:", "Enter the amount you want to donate:");
            btnOkAmt.Content = Localization.T("OK", "OK");
            btnCancelAmt.Content = Localization.T("Anuleaza", "Cancel");
        }

        private void BtnOkAmt_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(txtAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v) && v > 0)
            {
                Amount = v;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show(this, Localization.T("Introdu o suma valida.", "Enter a valid amount."), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancelAmt_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
