using System;

namespace P7SExtractor
{
    public static class DonationManager
    {
        // Replace these placeholders with the user's actual Revolut payment links
        // Generic Revolut link or template. You can include a placeholder {amount} which will be replaced
        // by the numeric amount. Example: "https://revolut.com/pay/yourid?amount={amount}".
        public static string RevolutCustom => "https://revolut.com/pay/your-revolut-generic-link";

        public static string GetCustomLink(decimal amount)
        {
            if (string.IsNullOrWhiteSpace(RevolutCustom))
                return string.Empty;

            // If the template contains {amount} replace it, otherwise append as query param
            if (RevolutCustom.Contains("{amount}"))
                return RevolutCustom.Replace("{amount}", amount.ToString("0.##"));

            // Append amount query parameter (some providers may accept it)
            var sep = RevolutCustom.Contains("?") ? "&" : "?";
            return RevolutCustom + sep + "amount=" + Uri.EscapeDataString(amount.ToString("0.##"));
        }
    }
}
