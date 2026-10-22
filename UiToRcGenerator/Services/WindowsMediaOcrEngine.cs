using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using UiToRcGenerator.Models;

namespace UiToRcGenerator.Services
{
    public class WindowsMediaOcrEngine : IOcrEngine
    {
        public string Name => "Windows Media OCR (Built-in)";

        public bool IsAvailable
        {
            get
            {
                try
                {
                    return OcrEngine.IsLanguageSupported(new Language("en-US")) || 
                           OcrEngine.AvailableRecognizerLanguages.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task<OcrResult> RecognizeAsync(Bitmap image, double coordinateScaleRatio = 1.0)
        {
            var sw = Stopwatch.StartNew();
            var result = new OcrResult
            {
                EngineName = Name
            };

            if (image == null) return result;

            OcrEngine? engine = null;
            try
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch { }

            if (engine == null)
            {
                var enLang = new Language("en-US");
                if (OcrEngine.IsLanguageSupported(enLang))
                {
                    engine = OcrEngine.TryCreateFromLanguage(enLang);
                }
                else if (OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    engine = OcrEngine.TryCreateFromLanguage(OcrEngine.AvailableRecognizerLanguages[0]);
                }
            }

            if (engine == null)
            {
                throw new InvalidOperationException("No compatible Windows Media OCR language model found on this system.");
            }

            using var memoryStream = new MemoryStream();
            image.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Position = 0;

            using var randomAccessStream = memoryStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            var winOcrResult = await engine.RecognizeAsync(softwareBitmap);

            var rawSb = new StringBuilder();
            rawSb.AppendLine($"=== Windows Media OCR Output ({winOcrResult.Lines.Count} lines) ===");
            rawSb.AppendLine($"TextAngle: {winOcrResult.TextAngle}");
            rawSb.AppendLine();

            int order = 1;
            double invScale = (coordinateScaleRatio > 0) ? (1.0 / coordinateScaleRatio) : 1.0;

            foreach (var line in winOcrResult.Lines)
            {
                string text = line.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;

                rawSb.AppendLine($"[Line {order}]: {text}");

                // Compute bounding box from words
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (var word in line.Words)
                {
                    var rect = word.BoundingRect;
                    if (rect.X < minX) minX = rect.X;
                    if (rect.Y < minY) minY = rect.Y;
                    if (rect.X + rect.Width > maxX) maxX = rect.X + rect.Width;
                    if (rect.Y + rect.Height > maxY) maxY = rect.Y + rect.Height;

                    rawSb.AppendLine($"    Word: '{word.Text}' @ [{rect.X:F0}, {rect.Y:F0}, {rect.Width:F0}x{rect.Height:F0}]");
                }

                if (minX == double.MaxValue)
                {
                    minX = minY = 0;
                    maxX = maxY = 0;
                }

                int adjX = (int)Math.Max(0, Math.Round(minX * invScale));
                int adjY = (int)Math.Max(0, Math.Round(minY * invScale));
                int adjW = (int)Math.Max(1, Math.Round((maxX - minX) * invScale));
                int adjH = (int)Math.Max(1, Math.Round((maxY - minY) * invScale));

                var item = new ExtractedTextItem
                {
                    Order = order,
                    Id = $"IDS_STRING_{order}",
                    Text = text,
                    Confidence = 95.0, // Windows Media OCR default high confidence
                    X = adjX,
                    Y = adjY,
                    Width = adjW,
                    Height = adjH,
                    Included = true
                };

                result.Items.Add(item);
                order++;
            }

            sw.Stop();
            result.ElapsedTime = sw.Elapsed;
            result.RawOutput = rawSb.ToString();

            return result;
        }
    }
}
