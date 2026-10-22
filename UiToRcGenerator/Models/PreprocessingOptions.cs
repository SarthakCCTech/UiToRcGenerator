using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UiToRcGenerator.Models
{
    /// <summary>
    /// Configuration options for image cleaning and enhancement before running OCR.
    /// Implements INotifyPropertyChanged so UI sliders and checkboxes can bind to it.
    /// </summary>
    public class PreprocessingOptions : INotifyPropertyChanged
    {
        private double _scaleFactor = 1.0;
        private bool _grayscale = false;
        private bool _invertColors = false;
        private bool _contrastEnhance = false;
        private bool _binarize = false;
        private int _thresholdValue = 128;
        private bool _autoOtsu = true;

        /// <summary>
        /// Upscaling multiplier (e.g., 2.0x scales a 100x100 region to 200x200).
        /// Enlarging small UI text gives OCR engines much cleaner letter shapes to recognize.
        /// </summary>
        public double ScaleFactor
        {
            get => _scaleFactor;
            set
            {
                if (_scaleFactor != value)
                {
                    _scaleFactor = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Converts the color image to shades of gray.
        /// </summary>
        public bool Grayscale
        {
            get => _grayscale;
            set
            {
                if (_grayscale != value)
                {
                    _grayscale = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Inverts all colors (turns black to white, and white to black).
        /// Crucial for Dark Mode applications where text is light-colored on a dark background.
        /// </summary>
        public bool InvertColors
        {
            get => _invertColors;
            set
            {
                if (_invertColors != value)
                {
                    _invertColors = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Stretches brightness so that faint text becomes sharp and easily readable.
        /// </summary>
        public bool ContrastEnhance
        {
            get => _contrastEnhance;
            set
            {
                if (_contrastEnhance != value)
                {
                    _contrastEnhance = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Converts the image strictly into pure black pixels and pure white pixels.
        /// </summary>
        public bool Binarize
        {
            get => _binarize;
            set
            {
                if (_binarize != value)
                {
                    _binarize = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Manual brightness threshold level (0-255) when Binarize is enabled without Auto-Otsu.
        /// </summary>
        public int ThresholdValue
        {
            get => _thresholdValue;
            set
            {
                if (_thresholdValue != value)
                {
                    _thresholdValue = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Uses Otsu's mathematical algorithm to automatically choose the best threshold value.
        /// </summary>
        public bool AutoOtsu
        {
            get => _autoOtsu;
            set
            {
                if (_autoOtsu != value)
                {
                    _autoOtsu = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public PreprocessingOptions Clone()
        {
            return new PreprocessingOptions
            {
                ScaleFactor = this.ScaleFactor,
                Grayscale = this.Grayscale,
                InvertColors = this.InvertColors,
                ContrastEnhance = this.ContrastEnhance,
                Binarize = this.Binarize,
                ThresholdValue = this.ThresholdValue,
                AutoOtsu = this.AutoOtsu
            };
        }
    }
}
