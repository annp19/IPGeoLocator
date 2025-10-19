using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;

namespace IPGeoLocator.Controls
{
    public partial class ThreatIndicatorControl : UserControl
    {
        public static readonly StyledProperty<int> ThreatScoreProperty =
            AvaloniaProperty.Register<ThreatIndicatorControl, int>(nameof(ThreatScore), defaultValue: 0);

        public static readonly StyledProperty<string> ThreatLabelProperty =
            AvaloniaProperty.Register<ThreatIndicatorControl, string>(nameof(ThreatLabel), defaultValue: "Unknown");

        public int ThreatScore
        {
            get => GetValue(ThreatScoreProperty);
            set => SetValue(ThreatScoreProperty, value);
        }

        public string ThreatLabel
        {
            get => GetValue(ThreatLabelProperty);
            set => SetValue(ThreatLabelProperty, value);
        }

        private Ellipse? _threatLevelIndicator;
        private TextBlock? _threatLevelText;

        public ThreatIndicatorControl()
        {
            InitializeComponent();
            _threatLevelIndicator = this.FindControl<Ellipse>("ThreatLevelIndicator");
            _threatLevelText = this.FindControl<TextBlock>("ThreatLevelText");
            
            // Initialize with default values
            UpdateVisuals();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ThreatScoreProperty || change.Property == ThreatLabelProperty)
            {
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (_threatLevelIndicator == null || _threatLevelText == null) return;

            // Determine color based on threat score
            var (color, label) = GetThreatColorAndLabel(ThreatScore);
            
            _threatLevelIndicator.Fill = new SolidColorBrush(color);
            _threatLevelText.Text = string.IsNullOrEmpty(ThreatLabel) ? label : ThreatLabel;
        }

        private (Avalonia.Media.Color color, string label) GetThreatColorAndLabel(int score)
        {
            if (score < 0) return (Avalonia.Media.Colors.Gray, "Unknown");
            else if (score < 30) return (Avalonia.Media.Colors.Green, "Low Risk");
            else if (score < 70) return (Avalonia.Media.Colors.Orange, "Medium Risk");
            else if (score < 90) return (Avalonia.Media.Colors.Red, "High Risk");
            else return (Avalonia.Media.Colors.DarkRed, "Critical");
        }
    }
}