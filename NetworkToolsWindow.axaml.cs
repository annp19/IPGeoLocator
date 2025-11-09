using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using IPGeoLocator.Services;
using System;
using System.Threading.Tasks;

namespace IPGeoLocator
{
    public partial class NetworkToolsWindow : Window
    {
        private NetworkToolsService _networkToolsService;
        private TextBox? _targetTextBox;
        private TextBox? _resultsTextBox;
        private Button? _pingButton;
        private Button? _tracerouteButton;
        private Button? _whoisButton;
        private Button? _cancelButton;
        private TextBlock? _statusTextBlock;

        public NetworkToolsWindow()
        {
            _networkToolsService = new NetworkToolsService();
            InitializeComponent();
            SetupControls();
            SubscribeToEvents();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _targetTextBox = this.FindControl<TextBox>("TargetTextBox");
            _resultsTextBox = this.FindControl<TextBox>("ResultsTextBox");
            _pingButton = this.FindControl<Button>("PingButton");
            _tracerouteButton = this.FindControl<Button>("TracerouteButton");
            _whoisButton = this.FindControl<Button>("WhoisButton");
            _cancelButton = this.FindControl<Button>("CancelButton");
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
        }

        private void SetupControls()
        {
            // Set up initial state
            _statusTextBlock.Text = "Ready to perform network diagnostics";
        }

        private void SubscribeToEvents()
        {
            // Button click events
            if (_pingButton != null)
            {
                _pingButton.Click += async (sender, e) => await PingAsync();
            }
            
            if (_tracerouteButton != null)
            {
                _tracerouteButton.Click += async (sender, e) => await TracerouteAsync();
            }
            
            if (_whoisButton != null)
            {
                _whoisButton.Click += async (sender, e) => await WhoisAsync();
            }
            
            if (_cancelButton != null)
            {
                _cancelButton.Click += (sender, e) => this.Close();
            }
        }

        private async Task PingAsync()
        {
            var target = GetTarget();
            if (string.IsNullOrWhiteSpace(target))
            {
                SetStatus("Please enter a target IP or domain.", true);
                return;
            }

            SetStatus("Pinging...", false, true);
            _resultsTextBox.Text = "";

            try
            {
                var result = await _networkToolsService.PingAsync(target);
                
                _resultsTextBox.Text = result.Output;
                
                if (result.Success)
                {
                    SetStatus("Ping completed successfully!", true);
                }
                else
                {
                    SetStatus($"Ping failed (exit code: {result.ExitCode})", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Ping error: {ex.Message}", true);
            }
        }

        private async Task TracerouteAsync()
        {
            var target = GetTarget();
            if (string.IsNullOrWhiteSpace(target))
            {
                SetStatus("Please enter a target IP or domain.", true);
                return;
            }

            SetStatus("Tracing route...", false, true);
            _resultsTextBox.Text = "";

            try
            {
                var result = await _networkToolsService.TracerouteAsync(target);
                
                _resultsTextBox.Text = result.Output;
                
                if (result.Success)
                {
                    SetStatus("Traceroute completed successfully!", true);
                }
                else
                {
                    SetStatus($"Traceroute failed (exit code: {result.ExitCode})", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Traceroute error: {ex.Message}", true);
            }
        }

        private async Task WhoisAsync()
        {
            var target = GetTarget();
            if (string.IsNullOrWhiteSpace(target))
            {
                SetStatus("Please enter a target IP or domain.", true);
                return;
            }

            SetStatus("Getting WHOIS information...", false, true);
            _resultsTextBox.Text = "";

            try
            {
                var result = await _networkToolsService.WhoisAsync(target);
                
                _resultsTextBox.Text = result.Output;
                
                if (result.Success)
                {
                    SetStatus("WHOIS lookup completed successfully!", true);
                }
                else
                {
                    SetStatus($"WHOIS lookup failed (exit code: {result.ExitCode})", true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"WHOIS error: {ex.Message}", true);
            }
        }

        private string GetTarget()
        {
            return _targetTextBox?.Text?.Trim() ?? "";
        }

        private void SetStatus(string message, bool isError = false, bool isWorking = false)
        {
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = message;
                
                // Here we could change the color based on status, but for now we'll just set the text
                if (isError)
                {
                    // Could set text color to red in a more advanced implementation
                }
            }
        }
        
        // Method to prefill the target with an IP address
        public void SetTarget(string target)
        {
            if (_targetTextBox != null)
            {
                _targetTextBox.Text = target;
            }
        }
    }
}