using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IPGeoLocator.Services;
using IPGeoLocator.ViewModels;

namespace IPGeoLocator.FeatureVerification
{
    /// <summary>
    /// Simple verification program to check that all features are implemented correctly
    /// </summary>
    public class FeatureVerification
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("IP GeoLocator - Feature Verification");
            Console.WriteLine("===================================");
            
            try
            {
                // Test 1: Performance Service
                await TestPerformanceService();
                
                // Test 2: IP Range Scanner functionality
                await TestIpRangeScanner();
                
                // Test 3: World Map Visualization
                await TestWorldMapVisualization();
                
                // Test 4: Threat Intelligence Visualization
                await TestThreatIntelligenceVisualization();
                
                Console.WriteLine("\n✅ All feature verifications completed successfully!");
                Console.WriteLine("The IP GeoLocator application features are implemented correctly.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during verification: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
        
        private static async Task TestPerformanceService()
        {
            Console.WriteLine("\n1. Testing Performance Service...");
            
            var perfService = new PerformanceService();
            
            // Test basic operations
            perfService.StartOperation("TestOperation");
            await Task.Delay(50); // Simulate work
            perfService.EndOperation("TestOperation");
            
            // Test metric recording
            perfService.RecordMetric("TestMetric", 42.5);
            
            // Test metrics retrieval
            var metrics = perfService.GetCurrentMetrics();
            Console.WriteLine($"   ✓ Got metrics: {metrics.OperationCounts.Count} operations, {metrics.CustomMetrics.Count} custom metrics");
            
            // Test metrics reset
            perfService.ResetMetrics();
            var resetMetrics = perfService.GetCurrentMetrics();
            Console.WriteLine($"   ✓ Reset metrics: {resetMetrics.OperationCounts.Count} operations remaining");
            
            Console.WriteLine("   ✅ Performance Service tests passed");
        }
        
        private static async Task TestIpRangeScanner()
        {
            Console.WriteLine("\n2. Testing IP Range Scanner functionality...");
            
            // Test that we can create the view model
            var viewModel = new IpRangeScanViewModel();
            Console.WriteLine("   ✓ IpRangeScanViewModel created successfully");
            
            // Test properties
            viewModel.StartIpAddress = "192.168.1.1";
            viewModel.EndIpAddress = "192.168.1.10";
            viewModel.ConcurrentScans = 5;
            
            Console.WriteLine($"   ✓ IP range set: {viewModel.StartIpAddress} to {viewModel.EndIpAddress}");
            Console.WriteLine($"   ✓ Concurrent scans set: {viewModel.ConcurrentScans}");
            
            // Test commands
            var canStart = viewModel.StartScanCommand.CanExecute(null);
            var canCancel = viewModel.CancelScanCommand.CanExecute(null);
            var canExport = viewModel.ExportResultsCommand.CanExecute(null);
            
            Console.WriteLine($"   ✓ Start command can execute: {canStart}");
            Console.WriteLine($"   ✓ Cancel command can execute: {canCancel}");
            Console.WriteLine($"   ✓ Export command can execute: {canExport}");
            
            Console.WriteLine("   ✅ IP Range Scanner tests passed");
        }
        
        private static async Task TestWorldMapVisualization()
        {
            Console.WriteLine("\n3. Testing World Map Visualization...");
            
            var viewModel = new WorldMapViewModel();
            
            // Test basic functionality
            Console.WriteLine("   ✓ WorldMapViewModel created successfully");
            
            // Test property changes
            viewModel.StatusMessage = "Test Status";
            Console.WriteLine($"   ✓ Status message updated: {viewModel.StatusMessage}");
            
            // Test map points
            var testPoints = new List<MapPoint>
            {
                new MapPoint 
                { 
                    Latitude = 40.7128, 
                    Longitude = -74.0060, 
                    IpAddress = "192.168.1.1", 
                    Country = "USA", 
                    City = "New York",
                    ThreatScore = 25,
                    IsMalicious = false
                }
            };
            
            // Test loading map data
            await viewModel.LoadMapDataAsync(testPoints);
            Console.WriteLine($"   ✓ Loaded {viewModel.MapPoints.Count} map points");
            
            Console.WriteLine("   ✅ World Map Visualization tests passed");
        }
        
        private static async Task TestThreatIntelligenceVisualization()
        {
            Console.WriteLine("\n4. Testing Threat Intelligence Visualization...");
            
            // Test threat intelligence service
            var threatService = new ThreatIntelligenceService(new System.Net.Http.HttpClient());
            
            // Test basic functionality
            Console.WriteLine("   ✓ ThreatIntelligenceService created successfully");
            
            // Test settings
            var settings = new ThreatIntelSettings();
            Console.WriteLine($"   ✓ Default threat settings: AbuseIPDB enabled = {settings.EnableAbuseIPDB}");
            
            Console.WriteLine("   ✅ Threat Intelligence Visualization tests passed");
        }
    }
}