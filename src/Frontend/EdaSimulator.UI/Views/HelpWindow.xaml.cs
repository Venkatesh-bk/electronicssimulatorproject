using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EdaSimulator.UI.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }
    }

    /// <summary>
    /// Reusable inline UserControl for a keyboard shortcut row.
    /// Renders: [Key Badge]  [Action Description]
    /// </summary>
    public class ShortcutRow : UserControl
    {
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register(nameof(Key), typeof(string), typeof(ShortcutRow),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register(nameof(Action), typeof(string), typeof(ShortcutRow),
                new PropertyMetadata(string.Empty));

        public string Key
        {
            get => (string)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public string Action
        {
            get => (string)GetValue(ActionProperty);
            set => SetValue(ActionProperty, value);
        }

        public ShortcutRow()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 150)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 210, 255)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    FontWeight = FontWeights.Bold
                }
            };
            // Child is set directly below — no DependencyProperty needed for Border.Child

            // Bind text via loaded
            var keyText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(180, 210, 255)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            keyText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(Key)) { Source = this });
            keyBadge.Child = keyText;

            var actionText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            actionText.SetBinding(TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(Action)) { Source = this });

            Grid.SetColumn(keyBadge, 0);
            Grid.SetColumn(actionText, 1);
            grid.Children.Add(keyBadge);
            grid.Children.Add(actionText);
            Content = grid;
        }
    }
}
