# UI-to-RC Resource Generator (Live Hover Capture)

A Windows desktop application built with **C# and WPF (.NET 10)** that captures visible UI strings by hovering over desktop application buttons/controls and generates Microsoft Visual C++ resource (`.rc`) files matching the reference structure of `2dTransRes.rc`.

---

## Key Features

1. **Direct UI Automation Hover Capture (Zero Latency, 100% Native)**
   - Move your mouse across buttons, menus, and controls in any Windows app.
   - Automatically inspects `element.Current.Name` and control type with 100% character accuracy and 0 ms delay.

2. **Global Hotkey: `Ctrl + Shift + H` (or `F8`)**
   - Toggle hover capture on or off from **any application window** without needing to switch back to this app.

3. **Floating "Screen-Recording" Widget**
   - When capture starts, a compact floating pill appears on the top of the screen:
     - 🔴 Pulsing recording dot (`REC`)
     - Real-time counter of captured strings (`X Strings`)
     - Live preview badge showing the last captured string
     - Quick `⏹ Stop` button
   - Draggable anywhere on the desktop.

4. **Clean, Minimal & Executive-Ready Workspace**
   - **Tab 1: Captured UI Strings**: Clean table showing `#`, `Resource ID`, `String Value`, and `Control Type` (Button, MenuItem, Tab, etc.) with actions to Edit, Delete, Merge, Split, and Re-assign IDs.
   - **Tab 2: Visual C++ .rc Generator**: Live preview matching `2dTransRes.rc` with 1-click **Validate Syntax** and **Save As .rc File**.
   - **Auto-Deduplication**: Automatically ignores duplicate strings when hovering repeatedly.

---

## How to Run in Visual Studio

1. Open `UiToRcGenerator.sln` in Visual Studio.
2. Ensure **`UiToRcGenerator`** is set as the Startup Project.
3. Press **`F5`** (or **`Ctrl + F5`**).

---

## How to Test

1. Launch `UiToRcGenerator`.
2. Open any desktop application (e.g. Notepad, Calculator, AutoCAD, or your CAD software).
3. Press **`Ctrl + Shift + H`** (or click **"▶ Start Direct UIA Hover"**).
   - The floating recording pill widget appears at the top of your screen.
4. Move your mouse across buttons, menus, and options in your application.
   - The floating pill pulses and shows the captured text in real time.
5. Press **`Ctrl + Shift + H`** again (or click `⏹ Stop`) when finished.
6. Switch to `UiToRcGenerator` to review the table and switch to the **"Visual C++ .rc Generator"** tab to save your new `.rc` file.
