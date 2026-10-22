using System;
using System.Drawing;
using System.Drawing.Imaging;
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
        /// Tells Windows that our app handles display scaling correctly so screenshots are crisp and pixel-accurate.
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

            // If metrics returned 0, fallback to a standard 1080p single screen resolution
            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, 1920, 1080);
            }

            return new Rectangle(left, top, width, height);
        }
    }

    /// <summary>
    /// Service responsible for capturing screenshots of the desktop or a specific rectangular region.
    /// Used by the OCR engine to read text from pixels.
    /// </summary>
    public class ScreenCaptureService
    {
        /// <summary>
        /// Takes a screenshot of a specific rectangular area on the screen.
        /// </summary>
        /// <param name="screenRegion">The screen coordinates and size to photograph.</param>
        /// <returns>A standard GDI+ Bitmap containing the captured pixels.</returns>
        public Bitmap CaptureRegion(Rectangle screenRegion)
        {
            if (screenRegion.Width <= 0 || screenRegion.Height <= 0)
            {
                throw new ArgumentException("Capture region must have positive width and height.");
            }

            // Create a blank image of the requested size
            var bitmap = new Bitmap(screenRegion.Width, screenRegion.Height, PixelFormat.Format32bppArgb);

            // Copy pixels directly from the screen into the blank image
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    screenRegion.Left, 
                    screenRegion.Top, 
                    0, 
                    0, 
                    screenRegion.Size, 
                    CopyPixelOperation.SourceCopy
                );
            }

            return bitmap;
        }

        /// <summary>
        /// Takes a full screenshot covering all connected monitors.
        /// </summary>
        public Bitmap CaptureAllScreens()
        {
            var bounds = DpiHelper.GetVirtualScreenBounds();
            return CaptureRegion(bounds);
        }
    }
}
