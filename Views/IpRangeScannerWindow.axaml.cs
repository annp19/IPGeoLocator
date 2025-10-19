using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using IPGeoLocator.Models;
using IPGeoLocator.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.Views
{
    public partial class IpRangeScannerWindow : Window
    {
        private readonly IpRangeScanViewModel _viewModel;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public IpRangeScannerWindow()
        {
            InitializeComponent();
            _viewModel = new IpRangeScanViewModel();
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async Task<IpScanResult> ScanIpAddressAsync(string ipAddress)
        {
            // This would call the actual geolocation API from the main window
            // For now, we'll create a basic implementation
            try
            {
                // Simulate network request delay
                await Task.Delay(50, _cancellationTokenSource.Token);
                
                // In a real implementation, you would call the geolocation service here
                // For now, we'll return mock data to demonstrate functionality
                return new IpScanResult
                {
                    IpAddress = ipAddress,
                    Status = "Success",
                    Country = "United States",
                    City = "New York",
                    Isp = "Example ISP",
                    ThreatScore = new Random().Next(0, 100),
                    ScanTime = DateTime.UtcNow
                };
            }
            catch (OperationCanceledException)
            {
                return new IpScanResult
                {
                    IpAddress = ipAddress,
                    Status = "Cancelled",
                    ErrorMessage = "Scan cancelled"
                };
            }
            catch (Exception ex)
            {
                return new IpScanResult
                {
                    IpAddress = ipAddress,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }
        
        public async Task StartScanAsync()
        {
            if (_viewModel != null)
            {
                // Call the StartScanAsync method with the scan function
                await _viewModel.StartScanAsync(ScanIpAddressAsync, _cancellationTokenSource.Token);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            base.OnClosed(e);
        }
    }
}