using System.Drawing;
using System.Windows;

namespace UiToRcGenerator.Views
{
    public partial class WatchRegionOverlayWindow : Window
    {
        public WatchRegionOverlayWindow()
        {
            InitializeComponent();
        }

        public void SetRegion(Rectangle rect)
        {
            this.Left = rect.Left - 2;
            this.Top = rect.Top - 2;
            this.Width = rect.Width + 4;
            this.Height = rect.Height + 4;
        }
    }
}
