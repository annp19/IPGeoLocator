using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IPGeoLocator.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private int _selectedTabIndex;

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        // ViewModels for each tab
        public IpLookupViewModel IpLookup { get; }
        public HistoryViewModel History { get; }
        public NetworkToolsViewModel NetworkTools { get; }

        public MainViewModel()
        {
            IpLookup = new IpLookupViewModel();
            History = new HistoryViewModel();
            NetworkTools = new NetworkToolsViewModel();
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
}