using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace UiToRcGenerator.Services
{
    /// <summary>
    /// Event arguments passed to MainWindow when a new UI string is captured via UIA hover.
    /// Contains the text, control type (e.g. Button), source, and screen position.
    /// </summary>
    public class LiveStringCapturedEventArgs : EventArgs
    {
        public string ExtractedText { get; set; } = string.Empty;
        public string RawText { get; set; } = string.Empty;
        public string ControlType { get; set; } = "Control";
        public string Source { get; set; } = "Direct UIA Hover";
        public Rectangle? Region { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// The live UI capture engine.
    /// Uses Windows UI Automation (UIA) to instantly read the name, control type,
    /// and properties of whatever control is currently hovered under the mouse cursor.
    /// </summary>
    public class LiveWatcherService : IDisposable
    {
        #region Windows API for Mouse Position

        // GetCursorPos asks Windows for the current (X, Y) pixel position of the mouse pointer.
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        #endregion

        // Cancellation token to stop background hover loop cleanly when requested
        private CancellationTokenSource? _uiaCancellationSource;

        // Memory variable to prevent spamming identical strings
        private string _lastUiaHoverText = string.Empty;

        /// <summary>
        /// True if the Direct UI Automation mouse-hover watcher loop is running.
        /// </summary>
        public bool IsUiaHoverActive => _uiaCancellationSource != null && !_uiaCancellationSource.IsCancellationRequested;

        /// <summary>
        /// If true, strips prefixes like "Name: " or "Text: " from strings captured from accessibility tools.
        /// </summary>
        public bool CleanAccessibilityInsightsPrefix { get; set; } = true;

        /// <summary>
        /// If true, ignores strings that were already added to the list.
        /// </summary>
        public bool Deduplicate { get; set; } = true;

        /// <summary>
        /// Event fired whenever a new string is detected and captured.
        /// </summary>
        public event EventHandler<LiveStringCapturedEventArgs>? StringCaptured;

        /// <summary>
        /// Event fired to update the status bar text in MainWindow.
        /// </summary>
        public event EventHandler<string>? StatusChanged;

        public LiveWatcherService()
        {
        }

        #region Direct UI Automation Hover Watcher

        /// <summary>
        /// Starts the background loop that monitors what UI button or element is under the mouse cursor.
        /// </summary>
        public void StartUiaHoverWatcher()
        {
            // Stop any existing session first
            StopUiaHoverWatcher();

            _uiaCancellationSource = new CancellationTokenSource();
            _lastUiaHoverText = string.Empty;

            StatusChanged?.Invoke(this, "Direct UIA Hover Capture active. Hover over any UI element to capture its Name.");
            
            // Run the monitoring loop on a background thread so the user interface never freezes
            Task.Run(() => UiaHoverWatcherLoopAsync(_uiaCancellationSource.Token), _uiaCancellationSource.Token);
        }

        /// <summary>
        /// Stops the mouse-hover capture loop.
        /// </summary>
        public void StopUiaHoverWatcher()
        {
            if (_uiaCancellationSource != null)
            {
                _uiaCancellationSource.Cancel();
                _uiaCancellationSource.Dispose();
                _uiaCancellationSource = null;
                StatusChanged?.Invoke(this, "Direct UIA Hover stopped.");
            }
        }

        /// <summary>
        /// Background loop for Direct UI Automation hover capture.
        /// Runs every 90 milliseconds to check the control under the mouse pointer.
        /// </summary>
        private async Task UiaHoverWatcherLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Step 1: Get the current mouse coordinates on the screen
                    if (GetCursorPos(out POINT currentPoint))
                    {
                        var mousePosition = new System.Windows.Point(currentPoint.X, currentPoint.Y);

                        // Step 2: Ask Windows UI Automation for the control located at this point
                        AutomationElement? element = null;
                        try
                        {
                            element = AutomationElement.FromPoint(mousePosition);
                        }
                        catch
                        {
                            // Some elevated or protected windows may temporarily throw an access exception
                        }

                        if (element != null)
                        {
                            string capturedName = string.Empty;
                            string controlTypeName = "Control";

                            try
                            {
                                // Step 3a: Read the primary element Name (e.g., "Save", "File", "Cancel")
                                capturedName = element.Current.Name?.Trim() ?? string.Empty;

                                // Step 3b: Read the control type (e.g., "Button", "MenuItem", "CheckBox")
                                controlTypeName = element.Current.ControlType.LocalizedControlType;
                                if (string.IsNullOrWhiteSpace(controlTypeName))
                                {
                                    controlTypeName = element.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
                                }

                                // Fallback 1: If element Name is empty, inspect child Text elements
                                if (string.IsNullOrWhiteSpace(capturedName))
                                {
                                    try
                                    {
                                        var textChild = element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                                        if (textChild != null)
                                        {
                                            capturedName = textChild.Current.Name?.Trim() ?? string.Empty;
                                        }
                                    }
                                    catch { }
                                }

                                // Fallback 2: Check Tooltip or HelpText
                                if (string.IsNullOrWhiteSpace(capturedName))
                                {
                                    capturedName = element.Current.HelpText?.Trim() ?? string.Empty;
                                }

                                // Fallback 3: Check ValuePattern (used by text fields and combo boxes)
                                if (string.IsNullOrWhiteSpace(capturedName))
                                {
                                    if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObject) && patternObject is ValuePattern valuePattern)
                                    {
                                        capturedName = valuePattern.Current.Value?.Trim() ?? string.Empty;
                                    }
                                }
                            }
                            catch
                            {
                                // Element might have closed or changed while reading properties
                            }

                            // Step 4: Validate and filter out background desktop noise
                            bool isValidString = !string.IsNullOrWhiteSpace(capturedName) &&
                                                 capturedName.Length >= 2 &&
                                                 !capturedName.Equals("Desktop", StringComparison.OrdinalIgnoreCase);

                            if (isValidString)
                            {
                                if (CleanAccessibilityInsightsPrefix)
                                {
                                    capturedName = CleanInsightPrefix(capturedName);
                                }

                                // Step 5: Check if this is different from what we just captured
                                if (!string.Equals(capturedName, _lastUiaHoverText, StringComparison.OrdinalIgnoreCase))
                                {
                                    _lastUiaHoverText = capturedName;

                                    // Get element bounding box rectangle on screen
                                    var bounding = element.Current.BoundingRectangle;
                                    Rectangle? elementRectangle = null;
                                    if (!bounding.IsEmpty && bounding.Width > 0 && bounding.Height > 0)
                                    {
                                        elementRectangle = new Rectangle((int)bounding.X, (int)bounding.Y, (int)bounding.Width, (int)bounding.Height);
                                    }

                                    // Step 6: Notify listeners (MainWindow) about the captured string
                                    StringCaptured?.Invoke(this, new LiveStringCapturedEventArgs
                                    {
                                        ExtractedText = capturedName,
                                        ControlType = controlTypeName,
                                        RawText = $"{controlTypeName}: {capturedName}",
                                        Source = "Direct UIA Hover",
                                        Region = elementRectangle
                                    });
                                }
                            }
                        }
                    }

                    // Check every 90 ms: fast enough to feel instant, gentle enough for zero CPU load
                    await Task.Delay(90, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Clean exit when capture is stopped
                    break;
                }
                catch
                {
                    // Delay slightly on unexpected error before retrying
                    await Task.Delay(150, cancellationToken);
                }
            }
        }

        #endregion

        /// <summary>
        /// Cleans prefixes often produced by Accessibility Insights or Inspect tool.
        /// For example: 'Name: "Save As..."' is cleaned into 'Save As...'.
        /// </summary>
        public static string CleanInsightPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Pattern: Name: "Text" or Text: "Text"
            var nameMatch = Regex.Match(text, @"(?:Name|Text)\s*[:=]\s*""?([^""\r\n]+)""?", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                return nameMatch.Groups[1].Value.Trim();
            }

            // Remove leading standalone "Name:", "Text:", or "Label:"
            var cleaned = Regex.Replace(text, @"^(?:Name|Text|Label)\s*[:=]\s*", "", RegexOptions.IgnoreCase).Trim();

            // Trim surrounding quotation marks if present
            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length > 2)
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2).Trim();
            }

            return cleaned;
        }

        /// <summary>
        /// Clean up and stop background watcher threads when the service is disposed.
        /// </summary>
        public void Dispose()
        {
            StopUiaHoverWatcher();
        }
    }
}
