using System.Windows;
using EdaSimulator.UI.ViewModels.Canvas;

namespace EdaSimulator.UI.Views
{
    /// <summary>
    /// Double-click property editor for component Designator and Value.
    /// Opens modally over the main window with pre-filled current values.
    /// </summary>
    public partial class ComponentPropertyDialog : Window
    {
        private readonly ComponentNodeViewModel _target;

        public ComponentPropertyDialog(ComponentNodeViewModel target)
        {
            InitializeComponent();
            _target = target;

            // Pre-fill with current values
            DesignatorBox.Text = target.Designator;
            ValueBox.Text      = target.Value;

            // Select all text in the Value box so user can type immediately
            ValueBox.Focus();
            ValueBox.SelectAll();
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            string newDesignator = DesignatorBox.Text.Trim();
            string newValue      = ValueBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(newDesignator))
            {
                MessageBox.Show("Designator cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                DesignatorBox.Focus();
                return;
            }

            try
            {
                _target.Designator = newDesignator;
                _target.Value      = newValue;
                DialogResult = true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Invalid value: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
