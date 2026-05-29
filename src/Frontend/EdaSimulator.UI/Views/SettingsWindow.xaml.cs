using System.Windows;

namespace EdaSimulator.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void SaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            // ViewModel Save command already executed via Command binding;
            // this handler closes the dialog.
            DialogResult = true;
            Close();
        }
    }
}
