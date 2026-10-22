using System;
using System.Windows;
using System.Windows.Input;

namespace UiToRcGenerator.Views
{
    /// <summary>
    /// Interaction logic for HoverRecordingWidget.xaml.
    /// This is the compact, floating "Screen-Recording Pill" that stays on top of all windows
    /// whenever the user starts live hover capture.
    /// </summary>
    public partial class HoverRecordingWidget : Window
    {
        /// <summary>
        /// Event fired when the user clicks the "⏹ Stop" button on the widget.
        /// </summary>
        public event Action? StopRequested;

        public HoverRecordingWidget()
        {
            InitializeComponent();

            Loaded += (sender, e) =>
            {
                // Position the widget near the top-right corner of the primary monitor
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                this.Left = screenWidth - this.Width - 30;
                this.Top = 25;
            };
        }

        /// <summary>
        /// Updates the badge counter and the preview text on the floating widget.
        /// </summary>
        /// <param name="capturedCount">Total number of unique strings captured so far.</param>
        /// <param name="latestString">The most recently captured UI string.</param>
        public void UpdateStatus(int capturedCount, string latestString)
        {
            TxtCount.Text = $"{capturedCount} Strings";

            if (!string.IsNullOrWhiteSpace(latestString))
            {
                TxtLastString.Text = $"\"{latestString}\"";
            }
        }

        /// <summary>
        /// Handler for the Stop button on the widget.
        /// </summary>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopRequested?.Invoke();
        }

        /// <summary>
        /// Allows the user to click and drag the widget anywhere on their screen.
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
