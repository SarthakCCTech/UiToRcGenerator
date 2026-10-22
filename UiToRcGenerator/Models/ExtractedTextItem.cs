using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UiToRcGenerator.Models
{
    /// <summary>
    /// Represents a single captured UI text item (for example, the text on a button, menu, or label).
    /// Implements INotifyPropertyChanged so that whenever any property changes (like editing text or ID),
    /// the WPF DataGrid user interface automatically updates in real-time.
    /// </summary>
    public class ExtractedTextItem : INotifyPropertyChanged
    {
        // Private backing fields that hold the actual data
        private int _order;
        private string _id = string.Empty;
        private string _text = string.Empty;
        private string _controlType = "Button";
        private double _confidence = 100.0;
        private int _x;
        private int _y;
        private int _width;
        private int _height;
        private bool _included = true;

        /// <summary>
        /// The sequence or row number of the item (e.g., 1, 2, 3...).
        /// </summary>
        public int Order
        {
            get => _order;
            set
            {
                if (_order != value)
                {
                    _order = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The Visual C++ Resource Identifier string (e.g., "IDS_UI_1", "IDS_BUTTON_SAVE").
        /// This is the symbol name written into the .rc file.
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The actual text extracted from the user interface (e.g., "Open File", "Cancel", "Save").
        /// </summary>
        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The type of UI control this text was captured from (e.g., "Button", "MenuItem", "TabItem", "Text").
        /// </summary>
        public string ControlType
        {
            get => _controlType;
            set
            {
                if (_controlType != value)
                {
                    _controlType = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Extraction confidence score as a percentage (100.0 for native UI Automation, or 0-100 for OCR).
        /// </summary>
        public double Confidence
        {
            get => _confidence;
            set
            {
                if (Math.Abs(_confidence - value) > 0.001)
                {
                    _confidence = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Screen X coordinate (pixels from the left edge of the screen).
        /// </summary>
        public int X
        {
            get => _x;
            set
            {
                if (_x != value)
                {
                    _x = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(BoundingBoxDisplay));
                }
            }
        }

        /// <summary>
        /// Screen Y coordinate (pixels from the top edge of the screen).
        /// </summary>
        public int Y
        {
            get => _y;
            set
            {
                if (_y != value)
                {
                    _y = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(BoundingBoxDisplay));
                }
            }
        }

        /// <summary>
        /// Width of the UI element in screen pixels.
        /// </summary>
        public int Width
        {
            get => _width;
            set
            {
                if (_width != value)
                {
                    _width = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(BoundingBoxDisplay));
                }
            }
        }

        /// <summary>
        /// Height of the UI element in screen pixels.
        /// </summary>
        public int Height
        {
            get => _height;
            set
            {
                if (_height != value)
                {
                    _height = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(BoundingBoxDisplay));
                }
            }
        }

        /// <summary>
        /// Whether this string should be included when generating the .rc file.
        /// Checkbox in the table allows the user to easily include/exclude items.
        /// </summary>
        public bool Included
        {
            get => _included;
            set
            {
                if (_included != value)
                {
                    _included = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Helper string that formats the bounding box coordinates for display in the table (e.g. "[100, 200, 80x24]").
        /// </summary>
        public string BoundingBoxDisplay => $"[{X}, {Y}, {Width}x{Height}]";

        /// <summary>
        /// Standard event used by WPF to know when a property was updated.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Helper method to raise the PropertyChanged event safely.
        /// The [CallerMemberName] attribute automatically passes the name of the property calling it.
        /// </summary>
        protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Creates a complete duplicate copy of this item.
        /// </summary>
        public ExtractedTextItem Clone()
        {
            return new ExtractedTextItem
            {
                Order = this.Order,
                Id = this.Id,
                Text = this.Text,
                ControlType = this.ControlType,
                Confidence = this.Confidence,
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
                Included = this.Included
            };
        }
    }
}
