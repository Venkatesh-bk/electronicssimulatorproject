using System.Windows;
using EdaSimulator.Engines.Licensing;

namespace EdaSimulator.UI.Views
{
    public partial class ActivationWindow : Window
    {
        public ActivationWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentLicenseText.Text = $"Current Edition: {LicenseManager.CurrentLicense.Tier}\nRegistered To: {LicenseManager.CurrentLicense.RegisteredTo}";
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            string key = LicenseKeyInput.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show("Please enter a valid license key.", "Invalid Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LicenseManager.ActivateLicense(key))
            {
                MessageBox.Show($"Activation successful!\n\nWelcome to EDA Simulator {LicenseManager.CurrentLicense.Tier} Edition.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Restart required to apply changes to Title bar, etc.
                MessageBox.Show("Please restart the application to apply all professional features.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show("The license key entered is invalid or expired.", "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
