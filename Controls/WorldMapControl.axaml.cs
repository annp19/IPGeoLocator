using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ScottPlot;
using ScottPlot.Avalonia;
using IPGeoLocator.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IPGeoLocator.Controls
{
    public partial class WorldMapControl : UserControl
    {
        private AvaPlot? _plot;
        
        public WorldMapControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _plot = this.FindControl<AvaPlot>("Plot");
            
            SetupPlot();
        }
        
        private void SetupPlot()
        {
            var plt = _plot.Plot;
            
            // Configure the plot for world map visualization
            plt.Title("IP Geolocation Map");
            plt.XLabel("Longitude");
            plt.YLabel("Latitude");
            
            // Set the axis limits to world coordinates
            plt.Axes.SetLimitsX(-180, 180);
            plt.Axes.SetLimitsY(-90, 90);
            
            // Draw a basic outline of the world using longitude/latitude lines
            DrawWorldGrid();
            
            _plot.Refresh();
        }
        
        public void UpdateMap(List<MapPoint> points, bool showOnlyThreats = false, double threatThreshold = 70)
        {
            if (_plot?.Plot == null) return;
            
            var plt = _plot.Plot;
            
            // Clear previous scatter plots (but keep the grid)
            plt.Clear();
            DrawWorldGrid();
            
            if (points != null && points.Any())
            {
                var filteredPoints = showOnlyThreats 
                    ? points.Where(p => p.IsMalicious).ToList()
                    : points;
                
                if (filteredPoints.Any())
                {
                    var lats = filteredPoints.Select(p => p.Latitude).ToArray();
                    var lons = filteredPoints.Select(p => p.Longitude).ToArray();
                    
                    // Create a scatter plot
                    var scatter = plt.Add.Scatter(lons, lats);
                    
                    // Color the points based on threat level
                    var colors = new List<ScottPlot.Color>();
                    foreach (var point in filteredPoints)
                    {
                        colors.Add(point.IsMalicious 
                            ? ScottPlot.Color.FromHex("#FF0000") // Red for malicious
                            : ScottPlot.Color.FromHex("#00FF00")); // Green for clean
                    }
                    
                    // Remove this part as we'll handle markers differently
                    
                    // Add separate scatter plots with different colors based on threat level
                    var maliciousPoints = filteredPoints.Where(p => p.IsMalicious).ToList();
                    var cleanPoints = filteredPoints.Where(p => !p.IsMalicious).ToList();
                    
                    // Plot malicious points in red
                    if (maliciousPoints.Any())
                    {
                        var maliciousLons = maliciousPoints.Select(p => p.Longitude).ToArray();
                        var maliciousLats = maliciousPoints.Select(p => p.Latitude).ToArray();
                        var maliciousScatter = plt.Add.Scatter(maliciousLons, maliciousLats);
                        maliciousScatter.Color = ScottPlot.Color.FromHex("#FF0000"); // Red
                        maliciousScatter.MarkerSize = 8;
                    }
                    
                    // Plot clean points in green
                    if (cleanPoints.Any())
                    {
                        var cleanLons = cleanPoints.Select(p => p.Longitude).ToArray();
                        var cleanLats = cleanPoints.Select(p => p.Latitude).ToArray();
                        var cleanScatter = plt.Add.Scatter(cleanLons, cleanLats);
                        cleanScatter.Color = ScottPlot.Color.FromHex("#00FF00"); // Green
                        cleanScatter.MarkerSize = 6;
                    }
                }
            }
            
            _plot.Refresh();
        }
        
        private void DrawWorldGrid()
        {
            var plt = _plot.Plot;
            
            // Draw longitude lines
            for (int lon = -180; lon <= 180; lon += 30)
            {
                var line = plt.Add.Line(
                    x1: lon, 
                    x2: lon, 
                    y1: -90, 
                    y2: 90);
                line.LinePattern = ScottPlot.LinePattern.Solid;
                line.Color = ScottPlot.Color.FromHex("#D3D3D3").WithAlpha(0.5f);
                line.LineWidth = 0.5f;
            }
            
            // Draw latitude lines
            for (int lat = -90; lat <= 90; lat += 30)
            {
                var line = plt.Add.Line(
                    x1: -180, 
                    x2: 180, 
                    y1: lat, 
                    y2: lat);
                line.LinePattern = ScottPlot.LinePattern.Solid;
                line.Color = ScottPlot.Color.FromHex("#D3D3D3").WithAlpha(0.5f);
                line.LineWidth = 0.5f;
            }
            
            // Set title
            plt.Title("IP Geolocations");
        }
    }
}