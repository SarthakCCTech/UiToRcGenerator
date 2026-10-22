using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UiToRcGenerator.Services;

namespace UiToRcGenerator.Views
{
    public partial class RegionSelectionWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging = false;
        private System.Drawing.Rectangle _screenBounds;

        public System.Drawing.Rectangle? SelectedScreenRect { get; private set; }

        public RegionSelectionWindow()
        {
            InitializeComponent();

            _screenBounds = DpiHelper.GetVirtualScreenBounds();

            // Set window geometry to virtual screen
            this.Left = _screenBounds.Left;
            this.Top = _screenBounds.Top;
            this.Width = _screenBounds.Width;
            this.Height = _screenBounds.Height;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(this);

                Canvas.SetLeft(SelectionRect, _startPoint.X);
                Canvas.SetTop(SelectionRect, _startPoint.Y);
                SelectionRect.Width = 0;
                SelectionRect.Height = 0;
                SelectionRect.Visibility = Visibility.Visible;

                DimensionBadge.Visibility = Visibility.Visible;
                UpdateSelection(e.GetPosition(this));
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                CancelSelection();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateSelection(e.GetPosition(this));
            }
        }

        private void UpdateSelection(Point currentPoint)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(_startPoint.X - currentPoint.X);
            double height = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(SelectionRect, x);
            Canvas.SetTop(SelectionRect, y);
            SelectionRect.Width = width;
            SelectionRect.Height = height;

            DimensionText.Text = $"{(int)width} × {(int)height} px";
            Canvas.SetLeft(DimensionBadge, x);
            Canvas.SetTop(DimensionBadge, Math.Max(0, y - 26));
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && e.ChangedButton == MouseButton.Left)
            {
                _isDragging = false;
                Point endPoint = e.GetPosition(this);

                double relX = Math.Min(_startPoint.X, endPoint.X);
                double relY = Math.Min(_startPoint.Y, endPoint.Y);
                double width = Math.Abs(_startPoint.X - endPoint.X);
                double height = Math.Abs(_startPoint.Y - endPoint.Y);

                if (width > 8 && height > 8)
                {
                    // Convert WPF device-independent units to screen pixels
                    // With VirtualScreenBounds offset
                    int screenX = _screenBounds.Left + (int)Math.Round(relX);
                    int screenY = _screenBounds.Top + (int)Math.Round(relY);
                    int screenW = (int)Math.Round(width);
                    int screenH = (int)Math.Round(height);

                    SelectedScreenRect = new System.Drawing.Rectangle(screenX, screenY, screenW, screenH);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    CancelSelection();
                }
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelSelection();
            }
        }

        private void CancelSelection()
        {
            _isDragging = false;
            SelectedScreenRect = null;
            DialogResult = false;
            Close();
        }
    }
}
