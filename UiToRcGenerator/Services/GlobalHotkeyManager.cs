using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UiToRcGenerator.Services
{
    /// <summary>
    /// Manages system-wide global hotkeys (like Ctrl+Shift+H and F8).
    /// "Global" means these hotkeys work anywhere in Windows, even when our app is
    /// in the background or minimized while you hover over other desktop software.
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        #region Windows API (P/Invoke) Declarations

        // user32.dll is a core Windows system library.
        // RegisterHotKey tells Windows: "Whenever the user presses this key combo anywhere, notify my window".
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        // UnregisterHotKey tells Windows to release the hotkey when our app closes.
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        #region Windows Constants

        // WM_HOTKEY is the message number Windows sends to our window when a registered hotkey is pressed.
        private const int WM_HOTKEY = 0x0312;

        // Modifier key flags:
        private const uint MOD_CONTROL = 0x0002;  // The Ctrl key
        private const uint MOD_SHIFT = 0x0004;    // The Shift key
        private const uint MOD_NOREPEAT = 0x4000; // Prevents spamming repeated events if held down

        // Virtual Key Codes for the keyboard keys:
        private const uint VK_H = 0x48;  // The 'H' key
        private const uint VK_F8 = 0x77; // The 'F8' key

        // Unique numeric identifiers we assign to our hotkeys:
        private const int HOTKEY_ID_CTRL_SHIFT_H = 9001;
        private const int HOTKEY_ID_F8 = 9002;

        #endregion

        // The Windows window handle (HWND) of our main application window
        private IntPtr _windowHandle = IntPtr.Zero;

        // The WPF source used to listen to raw Windows messages
        private HwndSource? _hwndSource;

        /// <summary>
        /// Event fired whenever the user presses Ctrl+Shift+H or F8.
        /// The MainWindow subscribes to this to toggle hover capture on and off.
        /// </summary>
        public event Action? HotkeyPressed;

        /// <summary>
        /// Registers both Ctrl+Shift+H and F8 as global hotkeys for the specified WPF window.
        /// Returns true if at least one hotkey registered successfully.
        /// </summary>
        public bool Register(Window window)
        {
            // Step 1: Get the native Windows window handle (HWND) for our WPF window
            var helper = new WindowInteropHelper(window);
            _windowHandle = helper.EnsureHandle();

            // Step 2: Hook into the Windows message pipeline so we can intercept WM_HOTKEY
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(HandleWindowsMessage);

            // Step 3: Register "Ctrl + Shift + H"
            uint ctrlShiftModifier = MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT;
            bool registeredCtrlShiftH = RegisterHotKey(_windowHandle, HOTKEY_ID_CTRL_SHIFT_H, ctrlShiftModifier, VK_H);

            // Step 4: Register "F8" (single key alternative for convenience)
            uint f8Modifier = MOD_NOREPEAT;
            bool registeredF8 = RegisterHotKey(_windowHandle, HOTKEY_ID_F8, f8Modifier, VK_F8);

            // Return true if either hotkey is active
            return registeredCtrlShiftH || registeredF8;
        }

        /// <summary>
        /// Unregisters the hotkeys and cleans up the Windows message listener.
        /// </summary>
        public void Unregister()
        {
            // Release hotkeys from Windows if they were registered
            if (_windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID_CTRL_SHIFT_H);
                UnregisterHotKey(_windowHandle, HOTKEY_ID_F8);
                _windowHandle = IntPtr.Zero;
            }

            // Remove our message hook
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HandleWindowsMessage);
                _hwndSource = null;
            }
        }

        /// <summary>
        /// This method is called by Windows for every message sent to our window.
        /// When msg == WM_HOTKEY, we check if it matches one of our hotkey IDs.
        /// </summary>
        private IntPtr HandleWindowsMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();

                // Check if the hotkey matches either Ctrl+Shift+H or F8
                if (hotkeyId == HOTKEY_ID_CTRL_SHIFT_H || hotkeyId == HOTKEY_ID_F8)
                {
                    // Fire our C# event to toggle capture!
                    HotkeyPressed?.Invoke();

                    // Tell Windows we handled this message so other apps don't process it
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Cleanup resources when the object is disposed or the application closes.
        /// </summary>
        public void Dispose()
        {
            Unregister();
        }
    }
}
