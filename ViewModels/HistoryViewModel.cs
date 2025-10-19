
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using IPGeoLocator.Data;
using IPGeoLocator.Models;
using IPGeoLocator.Services;

namespace IPGeoLocator.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        private readonly LookupHistoryService _historyService;
        private ObservableCollection<HistoryItemViewModel> _historyItems;
        private string _searchTerm = "";
        private string _resultsCountText = "";

        public ObservableCollection<HistoryItemViewModel> HistoryItems
        {
            get => _historyItems;
            set => SetProperty(ref _historyItems, value);
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set => SetProperty(ref _searchTerm, value);
        }

        public string ResultsCountText
        {
            get => _resultsCountText;
            set => SetProperty(ref _resultsCountText, value);
        }

        public HistoryViewModel()
        {
            var dbContext = new AppDbContext();
            _historyService = new LookupHistoryService(dbContext);
            _historyItems = new ObservableCollection<HistoryItemViewModel>();
            _ = LoadHistoryAsync();
        }

        public async Task LoadHistoryAsync()
        {
            try
            {
                var lookups = await _historyService.GetAllLookupsAsync();
                UpdateHistoryDisplay(lookups);
            }
            catch (Exception ex)
            {
                ResultsCountText = $"Error: {ex.Message}";
            }
        }

        public async Task SearchHistoryAsync()
        {
            try
            {
                var lookups = string.IsNullOrEmpty(SearchTerm)
                    ? await _historyService.GetAllLookupsAsync()
                    : await _historyService.SearchLookupsAsync(SearchTerm);
                UpdateHistoryDisplay(lookups);
            }
            catch (Exception ex)
            {
                ResultsCountText = $"Error: {ex.Message}";
            }
        }

        public async Task RefreshHistoryAsync()
        {
            await LoadHistoryAsync();
        }

        public async Task ClearHistoryAsync()
        {
            try
            {
                await _historyService.ClearAllLookupsAsync();
                HistoryItems.Clear();
                ResultsCountText = "No records found";
            }
            catch (Exception ex)
            {
                ResultsCountText = $"Error: {ex.Message}";
            }
        }

        private void UpdateHistoryDisplay(List<LookupHistory> lookups)
        {
            HistoryItems.Clear();
            foreach (var lookup in lookups)
            {
                HistoryItems.Add(new HistoryItemViewModel(lookup));
            }

            int count = HistoryItems.Count;
            ResultsCountText = count == 1
                ? $"{count} record found"
                : $"{count} records found";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class HistoryItemViewModel
    {
        private readonly LookupHistory _lookup;

        public HistoryItemViewModel(LookupHistory lookup)
        {
            _lookup = lookup;
        }

        public string IpAddress => _lookup.IpAddress ?? "";
        public string CityAndCountry => string.IsNullOrEmpty(_lookup.City)
            ? (_lookup.Country ?? "")
            : $"{_lookup.City}, {_lookup.Country}";
        public string Isp => _lookup.Isp ?? "N/A";
        public string ThreatScoreDisplay => _lookup.ThreatScore?.ToString() ?? "N/A";
        public string LookupTimeDisplay => _lookup.LookupTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string LookupDuration => _lookup.LookupDuration ?? "N/A";
    }
}
