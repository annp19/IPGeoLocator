
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace IPGeoLocator.NetworkTools.Views
{
    public partial class NetworkToolsView : UserControl
    {
        public NetworkToolsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
