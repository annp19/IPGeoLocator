using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.TickGenerators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IPGeoLocator.Controls
{
    public class ThreatDataPoint
    {
        public string ServiceName { get; set; } = "";
        public int ThreatScore { get; set; }
        public string Category { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public partial class ThreatVisualizationControl : UserControl
    {
        private AvaPlot? _threatPlot;

        public ThreatVisualizationControl()
        {
            InitializeComponent();
            _threatPlot = this.FindControl<AvaPlot>("ThreatPlot");
            SetupPlot();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetupPlot()
        {
            if (_threatPlot?.Plot == null) return;

            var plt = _threatPlot.Plot;
            plt.Title("Threat Intelligence Overview");
            plt.XLabel("Threat Services");
            plt.YLabel("Threat Score");

            // Set axis limits for threat scores (0-100)
            plt.Axes.SetLimitsY(-5, 105);

            _threatPlot.Refresh();
        }

        public void UpdateThreatVisualization(List<ThreatDataPoint> threatData)
        {
            if (_threatPlot?.Plot == null) return;

            var plt = _threatPlot.Plot;
            
            // Clear previous plots
            plt.Clear();

            if (threatData == null || !threatData.Any())
            {
                plt.Title("No threat data available");
                _threatPlot.Refresh();
                return;
            }

            // Prepare data for visualization
            var serviceNames = threatData.Select(td => td.ServiceName).ToArray();
            var threatScores = threatData.Select(td => (double)td.ThreatScore).ToArray();
            
            // Create scatter plot for better visualization of threat levels (compatibility with ScottPlot version)
            var xs = Enumerable.Range(0, threatScores.Length).Select(x => (double)x).ToArray();
            var scatterPlot = plt.Add.Scatter(xs, threatScores);
            scatterPlot.MarkerSize = 15;
            scatterPlot.LineWidth = 0;
            
            // Color code points based on threat level
            for (int i = 0; i < threatScores.Length; i++)
            {
                var color = threatScores[i] < 30 ? ScottPlot.Color.FromHex("#00AA00") :  // Green (Low Risk)
                           threatScores[i] < 70 ? ScottPlot.Color.FromHex("#FFAA00") :  // Orange (Medium Risk)
                           ScottPlot.Color.FromHex("#FF0000");                        // Red (High Risk)
                scatterPlot.Color = color;
            }
            
            // Add labels on the bars
            for (int i = 0; i < serviceNames.Length; i++)
            {
                // Add the threat score value on top of each bar
                var text = plt.Add.Text(threatScores[i].ToString("F0"), i, threatScores[i] + 2);
                text.Label.FontSize = 12;
            }

            // Add risk level thresholds as horizontal lines - using Scatter with horizontal lines as compatible workaround
            // Add horizontal lines at risk thresholds using compatible API
            plt.Add.HorizontalLine(30); // Default color
            plt.Add.HorizontalLine(70); // Default color
            plt.Add.HorizontalLine(90); // Default color

            // Set appropriate axis limits
            plt.Axes.SetLimitsY(0, 105);
            plt.Axes.SetLimitsX(-0.5, threatScores.Length - 0.5);
            
            // Add legend for risk thresholds - handled by individual label annotations
            // plt.ShowLegend();
            
            plt.Title("Threat Intelligence Scores");
            plt.YLabel("Threat Score (0-100)");
            
            // Add service names as custom X-axis labels - for compatibility
            plt.XLabel("Threat Services");
            plt.YLabel("Threat Score (0-100)");

            _threatPlot.Refresh();
        }
    }
}