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

            // Configure dynamic hints for mathematical blocks
            if (target.Designator.StartsWith("XG", System.StringComparison.OrdinalIgnoreCase))
            {
                HintBlock.Text = "Tip: Gain block. Enter multiplier/gain factor as Value, e.g. '2.5' or '-10.0'.";
            }
            else if (target.Designator.StartsWith("XI", System.StringComparison.OrdinalIgnoreCase))
            {
                HintBlock.Text = "Tip: Integrator block. Enter initial condition (y(0)) as Value, e.g. '0.0' or '1.5'.";
            }
            else if (target.Designator.StartsWith("XSO", System.StringComparison.OrdinalIgnoreCase))
            {
                HintBlock.Text = "Tip: Source block. Format: 'Constant <val>', 'Sine <offset> <amp> <freq> <phase>', or 'Step <offset> <stepval> <steptime>'.";
            }
            else if (target.Designator.StartsWith("XS", System.StringComparison.OrdinalIgnoreCase))
            {
                HintBlock.Text = "Tip: Summing junction. Enter signs for inputs, e.g. '+-' (first positive, second negative) or '++'.";
            }
            else if (target.Designator.StartsWith("XTF", System.StringComparison.OrdinalIgnoreCase))
            {
                HintBlock.Text = "Tip: Laplace Transfer Function. Format: 'num / den' (coefficients low-to-high power), e.g. '1 / 1 1' for 1/(s+1) or '1 / 1 2 1' for 1/(s^2+2s+1).";
            }

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
