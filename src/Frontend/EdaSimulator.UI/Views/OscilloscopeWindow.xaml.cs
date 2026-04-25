using System.Windows;
using EdaSimulator.UI.ViewModels;

namespace EdaSimulator.UI.Views
{
    public partial class OscilloscopeWindow : Window
    {
        public OscilloscopeViewModel ViewModel { get; }

        public OscilloscopeWindow()
        {
            InitializeComponent();
            ViewModel = new OscilloscopeViewModel();
            DataContext = ViewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Just hide it instead of closing completely so math plots remain across instances
            this.Hide();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
