using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace UiToRcGenerator.Services
{
    /// <summary>
    /// Helper class for handling Windows High-DPI (Display Scaling like 125%, 150%, 200%).
    /// Without DPI awareness, screen coordinates can be misaligned on high-resolution laptop screens.
    /// </summary>
    public static class DpiHelper
    {
        #region Windows API Declarations

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        // System metrics indexes for measuring the entire multi-monitor desktop
        public const int SM_XVIRTUALSCREEN = 76; // Left coordinate of virtual screen
        public const int SM_YVIRTUALSCREEN = 77; // Top coordinate of virtual screen
        public const int SM_CXVIRTUALSCREEN = 78; // Width of entire virtual screen
        public const int SM_CYVIRTUALSCREEN = 79; // Height of entire virtual screen

        // -4 represents "Per-Monitor V2", the most accurate modern Windows DPI mode
        public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        #endregion

        /// <summary>
        /// Tells Windows that our app handles display scaling correctly so coordinates are pixel-accurate.
        /// </summary>
        public static void EnablePerMonitorDpi()
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch
            {
                // Fallback safely on older Windows versions where this API is not supported
            }
        }

        /// <summary>
        /// Calculates the total bounding rectangle across all connected monitors.
        /// </summary>
        public static Rectangle GetVirtualScreenBounds()
        {
            int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, 1920, 1080);
            }

            return new Rectangle(left, top, width, height);
        }
    }
}
