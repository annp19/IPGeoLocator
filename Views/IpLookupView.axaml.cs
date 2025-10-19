
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace IPGeoLocator.Views
{
    public partial class IpLookupView : UserControl
    {
        public IpLookupView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
