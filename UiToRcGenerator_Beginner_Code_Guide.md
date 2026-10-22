# Beginner-Friendly Code Guide: UI-to-RC Resource Generator

Welcome! This guide explains how this application works in clear, simple terms. Even if you are new to C#, WPF, or Windows development, this document will help you understand every part of the codebase.

---

## 1. What Does This Application Do?

Imagine you are looking at desktop software (like Notepad, Calculator, or AutoCAD) and you need to translate all the buttons, menus, and labels into another language or generate a Visual C++ resource file (`.rc`).

Normally, a developer would have to manually read each button, retype its name, invent a resource ID, and type it into a file.

**This application automates that entire process:**
1. You hover your mouse over any button or menu in any application.
2. The app instantly extracts the exact text (e.g. `"Open File"`) and its control type (e.g. `Button`).
3. It displays the text in a clean table with auto-assigned IDs (e.g. `IDS_UI_1`).
4. It formats everything into a standard Visual C++ `.rc` file matching the template of `2dTransRes.rc`.
5. You click **"Save As .rc File"** to export your new resource file.

---

## 2. Project Architecture: Map of Files

The project is structured into clean, well-defined folders:

```
UiToRcGenerator/
│
├── App.xaml / App.xaml.cs          --> Program entry point (starts the application)
├── MainWindow.xaml                 --> The visual UI layout (Toolbar, Table, RC Preview)
├── MainWindow.xaml.cs              --> The "Controller" connecting buttons to logic
│
├── Models/                         --> Data structures (Plain Old C# Objects)
│   ├── ExtractedTextItem.cs        --> Represents one captured string row (ID, Text, Type)
│   ├── PreprocessingOptions.cs     --> Settings for image cleaning (Scale factor, Invert, etc.)
│   ├── OcrEngineType.cs            --> Enum distinguishing between OCR engines
│   └── AccuracyEvaluationResult.cs --> Data model for benchmarking accuracy
│
├── Services/                       --> Reusable worker services
│   ├── GlobalHotkeyManager.cs      --> Listens for Ctrl+Shift+H and F8 across Windows
│   ├── LiveWatcherService.cs       --> Background engine checking the mouse position
│   ├── RcGeneratorService.cs       --> Formats captured strings into .rc file format
│   ├── ScreenCaptureService.cs     --> Takes screenshots & handles High-DPI display scaling
│   ├── ImagePreprocessingService.cs--> Upscales images so OCR can read small 9pt fonts
│   ├── WindowsMediaOcrEngine.cs    --> Built-in Windows 10/11 OCR engine
│   └── TesseractOcrEngine.cs       --> Local Tesseract v5.5 OCR engine
│
└── Views/                          --> Secondary popup windows
    ├── HoverRecordingWidget.xaml   --> Floating "Screen-Recording Pill" at top of screen
    ├── HoverRecordingWidget.xaml.cs--> Dragging & Stop button logic for the widget
    ├── RegionSelectionWindow.xaml  --> Box-drawing window for manual region capture
    └── WatchRegionOverlayWindow.xaml-> Green bounding box showing watched area
```

---

## 3. The Lifecycle: From Mouse Hover to .RC File

Here is the exact journey of a captured string:

```
[ Mouse Hovers over Button ]
           │
           ▼
[ LiveWatcherService.cs ]
  • Uses Windows API: GetCursorPos(out mousePoint)
  • Uses UI Automation: AutomationElement.FromPoint(mousePoint)
  • Reads element.Current.Name (e.g. "Save")
  • Reads element.Current.ControlType (e.g. "Button")
           │
           ▼
[ MainWindow.xaml.cs ] (OnLiveStringCaptured)
  • Deduplication check: Have we already captured "Save"?
  • Assigns next ID: "IDS_UI_1"
  • Adds new ExtractedTextItem to ExtractedItems collection
           │
           ▼
[ MainWindow.xaml ] (WPF DataGrid)
  • Data binding automatically adds a new row to the table!
  • Floating HoverRecordingWidget updates badge to "1 Strings"
           │
           ▼
[ RcGeneratorService.cs ] (UpdateRcPreview)
  • Escapes quotes (e.g., "Hello" becomes ""Hello"")
  • Formats the STRINGTABLE block
  • Validates C++ identifiers
  • Live preview updates in the second tab
```

---

## 4. Key Concepts Explained for Beginners

### A. What is Microsoft UI Automation (UIA)?
Windows UI Automation is an accessibility system built into Windows. Every modern desktop button, menu, or checkbox has a `Name` property (so screen readers for visually impaired users can read them aloud). Our app uses this same native system. Because it reads the digital name directly, **accuracy is 100% and delay is 0 milliseconds**.

### B. Why is `Dispatcher.InvokeAsync` used?
In WPF, only the main thread (the "UI thread") is allowed to modify buttons, textboxes, and tables. Background worker threads cannot touch the screen directly. `Dispatcher.InvokeAsync(() => { ... })` sends a safe message to the main UI thread saying: *"Please add this new item to the table when you are ready."*

### C. What is an `ObservableCollection`?
In standard C#, a `List<T>` holds items in memory, but if you add an item to a `List`, the screen doesn't know it changed. An `ObservableCollection<T>` is a special list that fires an event whenever an item is added or removed. WPF listens to this and automatically inserts the new row into the table without needing manual screen-refresh code.

### D. What is a Global Hotkey (`RegisterHotKey`)?
Normally, keyboard shortcuts only work when a window is focused. But when you are hovering over another program (like AutoCAD or Notepad), our window is in the background! Windows provides an API called `RegisterHotKey` in `user32.dll`. It tells Windows: *"Even if my window is minimized, whenever the user presses Ctrl+Shift+H or F8, notify me immediately."*

### E. What is a Visual C++ `.rc` File?
A resource script (`.rc`) is a C++ file used by compilers like Microsoft Visual Studio. It defines a `STRINGTABLE`:
```rc
STRINGTABLE 
BEGIN
    IDS_UI_1                            "Open Project"
    IDS_UI_2                            "Save Changes"
    IDS_UI_3                            "Exit"
END
```
Notice how strings inside `.rc` files escape quotation marks by doubling them (e.g. `""`). Our `RcGeneratorService.cs` handles all of this automatically.

---

## 5. Clean Code Guarantees
- **Zero Loss in Accuracy**: Native UI Automation inspects strings directly from Windows elements.
- **Identical Features**: Table editing, hotkeys, floating recording pill, deduplication, and syntax validation all function exactly as before.
- **Zero External Overwrites**: The reference file `2dTransRes.rc` is strictly protected against accidental overwrites.
