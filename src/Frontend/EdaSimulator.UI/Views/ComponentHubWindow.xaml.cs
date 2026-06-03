using System.Windows;

namespace EdaSimulator.UI.Views
{
    public partial class ComponentHubWindow : Window
    {
        public ComponentHubWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is ViewModels.ComponentHubViewModel vm)
                {
                    vm.RequestClose += () => this.Close();
                }
            };
        }
    }
}
