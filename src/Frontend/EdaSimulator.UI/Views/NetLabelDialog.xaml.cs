using System;
using System.Linq;
using System.Windows;

namespace EdaSimulator.UI.Views
{
    /// <summary>
    /// Dialog to prompt the user for a new net label name.
    /// Performs basic validation to prevent invalid SPICE tokens.
    /// </summary>
    public partial class NetLabelDialog : Window
    {
        public string NetName { get; private set; } = string.Empty;

        public NetLabelDialog(string currentNetName)
        {
            InitializeComponent();
            NetNameBox.Text = currentNetName;
            NetNameBox.Focus();
            NetNameBox.SelectAll();
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            string newName = NetNameBox.Text.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Net name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NetNameBox.Focus();
                return;
            }

            if (newName.Any(char.IsWhiteSpace))
            {
                MessageBox.Show("Net name cannot contain whitespace, which would break SPICE matrix tokenization.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                NetNameBox.Focus();
                return;
            }

            NetName = newName;
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
