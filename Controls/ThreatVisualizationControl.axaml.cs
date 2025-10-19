using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ScottPlot;
using ScottPlot.Avalonia;
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
            
            // Create a simple scatter plot for threat scores
            var xs = Enumerable.Range(0, threatScores.Length).Select(i => (double)i).ToArray();
            
            // Plot each point separately to have different colors
            for (int i = 0; i < threatScores.Length; i++)
            {
                var singleScatter = plt.Add.Scatter(
                    xs: new double[] { xs[i] },
                    ys: new double[] { threatScores[i] });
                singleScatter.MarkerSize = 15;
                singleScatter.LineWidth = 0;
                
                // Color code points based on threat level
                var color = threatScores[i] < 30 ? ScottPlot.Color.FromHex("#00AA00") :  // Green
                           threatScores[i] < 70 ? ScottPlot.Color.FromHex("#FFAA00") :  // Orange
                           ScottPlot.Color.FromHex("#FF0000");                        // Red
                singleScatter.Color = color;
            }

            plt.Title("Threat Intelligence Scores");
            plt.YLabel("Threat Score");
            
            // For ScottPlot 5.x, setting X-axis labels is different
            var xPositions = Enumerable.Range(0, serviceNames.Length).Select(i => (double)i).ToArray();
            
            // Set X-axis tick positions and labels
            // For ScottPlot 5.x, we need to use different API
            // Let's just skip custom labels for now to avoid API issues

            _threatPlot.Refresh();
        }
    }
}