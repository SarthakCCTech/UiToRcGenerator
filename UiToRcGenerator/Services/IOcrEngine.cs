using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using UiToRcGenerator.Models;

namespace UiToRcGenerator.Services
{
    public class OcrResult
    {
        public List<ExtractedTextItem> Items { get; set; } = new();
        public string RawOutput { get; set; } = string.Empty;
        public string EngineName { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
    }

    public interface IOcrEngine
    {
        string Name { get; }
        bool IsAvailable { get; }
        Task<OcrResult> RecognizeAsync(Bitmap image, double coordinateScaleRatio = 1.0);
    }
}
