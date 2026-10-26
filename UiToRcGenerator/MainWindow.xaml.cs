using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using UiToRcGenerator.Models;
using UiToRcGenerator.Services;
using UiToRcGenerator.Views;

namespace UiToRcGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// This is the primary window and controller of the application.
    /// </summary>
    public partial class MainWindow : Window
    {
        // -------------------------------------------------------------
        // Underlying Services
        // -------------------------------------------------------------
        private readonly RcGeneratorService _rcGenerator = new();

        // The background hover watcher and global hotkey manager
        private readonly LiveWatcherService _liveWatcher = new();
        private readonly GlobalHotkeyManager _hotkeyManager = new();

        // Floating widget shown at the top of the screen during live capture
        private HoverRecordingWidget? _recordingWidget;

        /// <summary>
        /// ObservableCollection of extracted strings.
        /// An ObservableCollection automatically refreshes the WPF DataGrid whenever items are added or removed.
        /// </summary>
        public ObservableCollection<ExtractedTextItem> ExtractedItems { get; set; } = new();

        // =============================================================
        // 1. APPLICATION INITIALIZATION & LIFECYCLE
        // =============================================================

        public MainWindow()
        {
            InitializeComponent();

            // Enable high-DPI scaling so coordinates and text remain sharp on 4K / Laptop displays
            DpiHelper.EnablePerMonitorDpi();
            
            // Subscribe to live events fired by the watcher
            _liveWatcher.StringCaptured += OnLiveStringCaptured;
            _liveWatcher.StatusChanged += (sender, message) =>
            {
                // Dispatcher.InvokeAsync is required to update UI controls from a background thread
                Dispatcher.InvokeAsync(() => TxtStatus.Text = message);
            };

            // Bind the table (DataGrid) to our ObservableCollection
            DgExtractedItems.ItemsSource = ExtractedItems;

            // Automatically find and set the default path to 2dTransRes.rc in the workspace
            string defaultReferencePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "2dTransRes.rc")
            );

            if (File.Exists(defaultReferencePath))
            {
                TxtReferencePath.Text = defaultReferencePath;
            }
            else
            {
                TxtReferencePath.Text = "2dTransRes.rc";
            }

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Register system-wide global hotkeys (Ctrl + Shift + H and F8)
            bool isHotkeyRegistered = _hotkeyManager.Register(this);
            _hotkeyManager.HotkeyPressed += ToggleCapture;

            if (isHotkeyRegistered)
            {
                TxtStatus.Text = "Ready. Press Ctrl+Shift+H or F8 from any window to start/stop hover capture.";
            }
            else
            {
                TxtStatus.Text = "Ready. (Global hotkey registration failed; please use the toolbar button).";
            }

            // Generate initial .rc preview
            UpdateRcPreview();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Clean up resources when the window is closed
            _hotkeyManager.Dispose();
            _liveWatcher.Dispose();

            if (_recordingWidget != null)
            {
                _recordingWidget.Close();
                _recordingWidget = null;
            }
        }

        // =============================================================
        // 2. LIVE HOVER CAPTURE & RECORDING CONTROLLER
        // =============================================================

        private void BtnToggleCapture_Click(object sender, RoutedEventArgs e)
        {
            ToggleCapture();
        }

        /// <summary>
        /// Starts capture if currently stopped, or stops capture if currently running.
        /// </summary>
        private void ToggleCapture()
        {
            if (_liveWatcher.IsUiaHoverActive)
            {
                StopCapture();
            }
            else
            {
                StartCapture();
            }
        }

        /// <summary>
        /// Starts live mouse-hover capture and shows the floating recording pill widget.
        /// </summary>
        private void StartCapture()
        {
            _liveWatcher.CleanAccessibilityInsightsPrefix = true;
            _liveWatcher.Deduplicate = ChkAutoDeduplicate.IsChecked == true;
            _liveWatcher.StartUiaHoverWatcher();

            // Change button appearance to Red "Stop" state
            BtnToggleCapture.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
            BtnToggleCapture.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
            TxtToggleBtnIcon.Text = "⏹ ";
            TxtToggleBtnText.Text = "Stop Hover Capture";

            // Create and show the floating screen-recording widget if not already visible
            if (_recordingWidget == null)
            {
                _recordingWidget = new HoverRecordingWidget();
                _recordingWidget.StopRequested += StopCapture;
            }

            string latestString = ExtractedItems.LastOrDefault()?.Text ?? "Hover over any button...";
            _recordingWidget.UpdateStatus(ExtractedItems.Count, latestString);
            _recordingWidget.Show();

            TxtStatus.Text = "🔴 LIVE UIA HOVER CAPTURE ACTIVE. Hover over any button or menu in your app. Press Ctrl+Shift+H to stop.";
        }

        /// <summary>
        /// Stops live mouse-hover capture and hides the floating widget.
        /// </summary>
        private void StopCapture()
        {
            _liveWatcher.StopUiaHoverWatcher();

            // Change button appearance back to Blue "Start" state
            BtnToggleCapture.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
            BtnToggleCapture.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 78, 216));
            TxtToggleBtnIcon.Text = "▶ ";
            TxtToggleBtnText.Text = "Start Direct UIA Hover";

            // Hide the floating widget
            if (_recordingWidget != null)
            {
                _recordingWidget.Hide();
            }

            TxtStatus.Text = $"Hover capture stopped. Recorded {ExtractedItems.Count} unique UI strings.";
        }

        /// <summary>
        /// Called when the live watcher captures a new string from a UI element under the mouse.
        /// </summary>
        private void OnLiveStringCaptured(object? sender, LiveStringCapturedEventArgs e)
        {
            // Since this event is triggered from a background thread, we use Dispatcher to safely update the UI
            Dispatcher.InvokeAsync(() =>
            {
                string text = e.ExtractedText.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                // Check for duplicates if Auto-Deduplicate is enabled
                if (ChkAutoDeduplicate.IsChecked == true)
                {
                    bool alreadyExists = ExtractedItems.Any(item => 
                        string.Equals(item.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)
                    );

                    if (alreadyExists) return; // Skip duplicate
                }

                // Determine next sequential Resource ID
                string prefix = string.IsNullOrWhiteSpace(TxtIdPrefix.Text) ? "IDS_UI_" : TxtIdPrefix.Text.Trim();
                int nextIndex = ExtractedItems.Count + 1;

                var newItem = new ExtractedTextItem
                {
                    Order = nextIndex,
                    Id = $"{prefix}{nextIndex}",
                    Text = text,
                    ControlType = e.ControlType,
                    Confidence = 100.0,
                    X = e.Region?.X ?? 0,
                    Y = e.Region?.Y ?? 0,
                    Width = e.Region?.Width ?? 100,
                    Height = e.Region?.Height ?? 24,
                    Included = true
                };

                // Add to list and scroll table to the new item
                ExtractedItems.Add(newItem);
                DgExtractedItems.SelectedItem = newItem;
                DgExtractedItems.ScrollIntoView(newItem);

                // Update count badge
                TxtItemCountBadge.Text = $"{ExtractedItems.Count} Strings";

                // Update the floating widget
                _recordingWidget?.UpdateStatus(ExtractedItems.Count, text);

                // Re-generate the .rc code preview in real-time
                UpdateRcPreview();

                TxtStatus.Text = $"⚡ Captured ({e.ControlType}): \"{text}\" -> {newItem.Id}";
            });
        }

        // =============================================================
        // 3. TABLE ACTIONS (ADD, DELETE, MERGE, SPLIT, MOVE, RENUMBER)
        // =============================================================

        /// <summary>
        /// Re-assigns clean sequential IDs (e.g. IDS_UI_1, IDS_UI_2, etc.) to all items in the list.
        /// </summary>
        private void BtnReassignIds_Click(object sender, RoutedEventArgs e)
        {
            string prefix = string.IsNullOrWhiteSpace(TxtIdPrefix.Text) ? "IDS_UI_" : TxtIdPrefix.Text.Trim();
            int index = 1;

            foreach (var item in ExtractedItems)
            {
                item.Order = index;
                item.Id = $"{prefix}{index}";
                index++;
            }

            UpdateRcPreview();
        }

        /// <summary>
        /// Manually adds a blank new row into the table.
        /// </summary>
        private void BtnAddString_Click(object sender, RoutedEventArgs e)
        {
            int nextIndex = ExtractedItems.Count + 1;
            string prefix = string.IsNullOrWhiteSpace(TxtIdPrefix.Text) ? "IDS_UI_" : TxtIdPrefix.Text.Trim();

            var newItem = new ExtractedTextItem
            {
                Order = nextIndex,
                Id = $"{prefix}{nextIndex}",
                Text = "New Visible String",
                ControlType = "Custom",
                Confidence = 100.0,
                Included = true
            };

            ExtractedItems.Add(newItem);
            DgExtractedItems.SelectedItem = newItem;
            TxtItemCountBadge.Text = $"{ExtractedItems.Count} Strings";
            UpdateRcPreview();
        }

        /// <summary>
        /// Deletes the currently selected row from the table.
        /// </summary>
        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (DgExtractedItems.SelectedItem is ExtractedTextItem selectedItem)
            {
                ExtractedItems.Remove(selectedItem);
                RenumberItems();
                TxtItemCountBadge.Text = $"{ExtractedItems.Count} Strings";
                UpdateRcPreview();
            }
        }

        /// <summary>
        /// Clears all captured items after asking for user confirmation.
        /// </summary>
        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (ExtractedItems.Count == 0) return;

            var confirmation = MessageBox.Show(
                "Are you sure you want to clear all extracted strings?", 
                "Clear All", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question
            );

            if (confirmation == MessageBoxResult.Yes)
            {
                ExtractedItems.Clear();
                TxtItemCountBadge.Text = "0 Strings";
                UpdateRcPreview();
                TxtStatus.Text = "All strings cleared.";
            }
        }

        /// <summary>
        /// Merges the currently selected row's text with the row immediately below it.
        /// </summary>
        private void BtnMergeNext_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = DgExtractedItems.SelectedIndex;

            if (selectedIndex >= 0 && selectedIndex < ExtractedItems.Count - 1)
            {
                var currentItem = ExtractedItems[selectedIndex];
                var nextItem = ExtractedItems[selectedIndex + 1];

                // Combine both texts with a space
                currentItem.Text = $"{currentItem.Text} {nextItem.Text}".Trim();

                // Remove the merged second row
                ExtractedItems.RemoveAt(selectedIndex + 1);

                RenumberItems();
                TxtItemCountBadge.Text = $"{ExtractedItems.Count} Strings";
                UpdateRcPreview();
            }
            else
            {
                MessageBox.Show(
                    "Please select an item that has a subsequent item below it to merge with.", 
                    "Merge", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information
                );
            }
        }

        /// <summary>
        /// Splits a multi-word row into two separate items.
        /// </summary>
        private void BtnSplitLine_Click(object sender, RoutedEventArgs e)
        {
            if (DgExtractedItems.SelectedItem is ExtractedTextItem selectedItem)
            {
                var words = selectedItem.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (words.Length > 1)
                {
                    int midpoint = words.Length / 2;
                    string firstHalf = string.Join(" ", words.Take(midpoint));
                    string secondHalf = string.Join(" ", words.Skip(midpoint));

                    selectedItem.Text = firstHalf;
                    int currentIndex = ExtractedItems.IndexOf(selectedItem);

                    var newSecondItem = new ExtractedTextItem
                    {
                        Text = secondHalf,
                        ControlType = selectedItem.ControlType,
                        Confidence = selectedItem.Confidence,
                        Included = true
                    };

                    ExtractedItems.Insert(currentIndex + 1, newSecondItem);
                    RenumberItems();
                    TxtItemCountBadge.Text = $"{ExtractedItems.Count} Strings";
                    UpdateRcPreview();
                }
                else
                {
                    MessageBox.Show(
                        "Selected item must contain at least 2 words to split.", 
                        "Split Line", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );
                }
            }
        }

        /// <summary>
        /// Moves the selected row up by one position in the table.
        /// </summary>
        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = DgExtractedItems.SelectedIndex;
            if (selectedIndex > 0)
            {
                ExtractedItems.Move(selectedIndex, selectedIndex - 1);
                RenumberItems();
                DgExtractedItems.SelectedIndex = selectedIndex - 1;
                UpdateRcPreview();
            }
        }

        /// <summary>
        /// Moves the selected row down by one position in the table.
        /// </summary>
        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = DgExtractedItems.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < ExtractedItems.Count - 1)
            {
                ExtractedItems.Move(selectedIndex, selectedIndex + 1);
                RenumberItems();
                DgExtractedItems.SelectedIndex = selectedIndex + 1;
                UpdateRcPreview();
            }
        }

        /// <summary>
        /// Helper to re-sequence the Order number (1, 2, 3...) for all items.
        /// </summary>
        private void RenumberItems()
        {
            for (int i = 0; i < ExtractedItems.Count; i++)
            {
                ExtractedItems[i].Order = i + 1;
            }
        }

        /// <summary>
        /// Triggered whenever the user finishes directly editing a cell in the table.
        /// Automatically refreshes the .rc preview.
        /// </summary>
        private void DgExtractedItems_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                UpdateRcPreview();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        // =============================================================
        // 4. VISUAL C++ .RC GENERATION & SYNTAX VALIDATION
        // =============================================================

        /// <summary>
        /// Generates the .rc text from the extracted items and updates the preview text box and validation badge.
        /// </summary>
        private void UpdateRcPreview()
        {
            if (TxtRcPreview == null || TxtIdPrefix == null || TxtReferencePath == null || RbPreserveReference == null)
            {
                return;
            }

            var options = new RcGenerationOptions
            {
                IdPrefix = string.IsNullOrWhiteSpace(TxtIdPrefix.Text) ? "IDS_UI_" : TxtIdPrefix.Text.Trim(),
                UseReferenceTemplate = RbPreserveReference.IsChecked == true,
                ReferenceFilePath = TxtReferencePath.Text.Trim(),
                EscapeQuotesWithDoubleQuotes = true
            };

            string generatedRc = _rcGenerator.GenerateRc(ExtractedItems, options, out var validationResult);
            TxtRcPreview.Text = generatedRc;

            UpdateValidationBanner(validationResult);
        }

        /// <summary>
        /// Updates the green/red validation banner above the .rc code preview.
        /// </summary>
        private void UpdateValidationBanner(RcValidationResult validation)
        {
            if (ValidationBanner == null || TxtValidationStatus == null) return;

            if (validation.IsValid)
            {
                // Green valid state
                ValidationBanner.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));
                ValidationBanner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(134, 239, 172));
                TxtValidationStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52));
                TxtValidationStatus.Text = "✅ RC Syntax Valid. All IDs are unique and conform to Visual C++ standards.";
            }
            else
            {
                // Red error state
                ValidationBanner.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242));
                ValidationBanner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                TxtValidationStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
                TxtValidationStatus.Text = $"❌ Errors detected: {string.Join(" | ", validation.Errors.Take(2))}";
            }
        }

        private void BtnBrowseReference_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Reference RC File",
                Filter = "Resource Scripts (*.rc)|*.rc|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                TxtReferencePath.Text = openFileDialog.FileName;
                UpdateRcPreview();
            }
        }

        private void BtnValidateSyntax_Click(object sender, RoutedEventArgs e)
        {
            string content = TxtRcPreview.Text;
            var validation = _rcGenerator.ValidateRcSyntax(content);

            if (validation.IsValid)
            {
                MessageBox.Show(
                    "RC syntax is valid! BEGIN/END blocks, quotes, and STRINGTABLE tokens match specification.", 
                    "RC Validation", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information
                );
            }
            else
            {
                string errorList = string.Join("\n• ", validation.Errors);
                MessageBox.Show(
                    $"RC Validation issues:\n• {errorList}", 
                    "Validation Errors", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning
                );
            }

            UpdateValidationBanner(validation);
        }

        private void BtnRefreshRc_Click(object sender, RoutedEventArgs e)
        {
            UpdateRcPreview();
        }

        private void BtnCopyRc_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtRcPreview.Text))
            {
                Clipboard.SetText(TxtRcPreview.Text);
                TxtStatus.Text = "RC code copied to clipboard.";
            }
        }

        private void BtnSaveRc_Click(object sender, RoutedEventArgs e)
        {
            // CRITICAL REQUIREMENT: Never overwrite the original reference file 2dTransRes.rc!
            string referenceFileName = System.IO.Path.GetFileName(TxtReferencePath.Text);
            string defaultSaveName = "Generated_" + (string.IsNullOrWhiteSpace(referenceFileName) ? "2dTransRes.rc" : referenceFileName);

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save Generated RC File",
                Filter = "Resource Script (*.rc)|*.rc",
                FileName = defaultSaveName
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string targetDestination = saveFileDialog.FileName;
                string referenceFullPath = System.IO.Path.GetFullPath(TxtReferencePath.Text);

                // Safety guard check: prevent overwriting the source reference file
                if (string.Equals(System.IO.Path.GetFullPath(targetDestination), referenceFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "Cannot overwrite the reference file 2dTransRes.rc! Please choose a different file name.", 
                        "Protection Alert", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning
                    );
                    return;
                }

                // Write the file using Windows Default ANSI / Code Page 1252 encoding (standard for Visual C++ .rc files)
                File.WriteAllText(targetDestination, TxtRcPreview.Text, System.Text.Encoding.Default);
                TxtStatus.Text = $"Saved RC file to: {targetDestination}";
                MessageBox.Show($"Generated RC file saved successfully:\n{targetDestination}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void RcOption_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateRcPreview();
            }
        }
    }
}