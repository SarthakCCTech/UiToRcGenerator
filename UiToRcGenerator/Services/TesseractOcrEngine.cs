using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UiToRcGenerator.Models;

namespace UiToRcGenerator.Services
{
    public class TesseractOcrEngine : IOcrEngine
    {
        public string Name => "Tesseract OCR (v5.5 Local)";

        private string? _tesseractPath;
        public int PsmMode { get; set; } = 6; // Default to PSM 6 (Single uniform block) or 3 (Auto) or 11 (Sparse)

        public TesseractOcrEngine()
        {
            _tesseractPath = FindTesseractExecutable();
        }

        public bool IsAvailable => !string.IsNullOrEmpty(_tesseractPath) && File.Exists(_tesseractPath);

        private static string? FindTesseractExecutable()
        {
            string standardPath = @"C:\Program Files\Tesseract-OCR\tesseract.exe";
            if (File.Exists(standardPath)) return standardPath;

            string x86Path = @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe";
            if (File.Exists(x86Path)) return x86Path;

            // Search in PATH
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim(), "tesseract.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }

            return null;
        }

        public async Task<OcrResult> RecognizeAsync(Bitmap image, double coordinateScaleRatio = 1.0)
        {
            var sw = Stopwatch.StartNew();
            var result = new OcrResult { EngineName = Name };

            if (!IsAvailable || string.IsNullOrEmpty(_tesseractPath))
            {
                throw new InvalidOperationException("Tesseract executable not found at C:\\Program Files\\Tesseract-OCR\\tesseract.exe or in PATH.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "UiToRcOcr_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string inputImageFile = Path.Combine(tempDir, "input.png");
            string outputBase = Path.Combine(tempDir, "output");
            string outputTsv = outputBase + ".tsv";

            try
            {
                image.Save(inputImageFile, ImageFormat.Png);

                var psi = new ProcessStartInfo
                {
                    FileName = _tesseractPath,
                    Arguments = $"\"{inputImageFile}\" \"{outputBase}\" -l eng --psm {PsmMode} tsv",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                var stderrSb = new StringBuilder();
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrSb.AppendLine(e.Data); };

                process.Start();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (File.Exists(outputTsv))
                {
                    string tsvContent = await File.ReadAllTextAsync(outputTsv);
                    result.RawOutput = tsvContent;
                    result.Items = ParseTsv(tsvContent, coordinateScaleRatio);
                }
                else
                {
                    throw new InvalidOperationException($"Tesseract completed with code {process.ExitCode} but produced no TSV output: {stderrSb}");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }

            sw.Stop();
            result.ElapsedTime = sw.Elapsed;
            return result;
        }

        private List<ExtractedTextItem> ParseTsv(string tsvContent, double coordinateScaleRatio)
        {
            var items = new List<ExtractedTextItem>();
            if (string.IsNullOrWhiteSpace(tsvContent)) return items;

            double invScale = (coordinateScaleRatio > 0) ? (1.0 / coordinateScaleRatio) : 1.0;

            // Structure of TSV:
            // level	page_num	block_num	par_num	line_num	word_num	left	top	width	height	conf	text
            var lines = tsvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 1) return items;

            // Group words (level 5) by (block_num, par_num, line_num)
            var currentLineKey = string.Empty;
            var currentLineWords = new List<(string text, double conf, int left, int top, int width, int height)>();

            void FlushCurrentLine()
            {
                if (currentLineWords.Count == 0) return;

                string lineText = string.Join(" ", currentLineWords.Select(w => w.text)).Trim();
                if (string.IsNullOrWhiteSpace(lineText))
                {
                    currentLineWords.Clear();
                    return;
                }

                int minX = currentLineWords.Min(w => w.left);
                int minY = currentLineWords.Min(w => w.top);
                int maxX = currentLineWords.Max(w => w.left + w.width);
                int maxY = currentLineWords.Max(w => w.top + w.height);

                double avgConf = currentLineWords.Where(w => w.conf >= 0).Select(w => w.conf).DefaultIfEmpty(0).Average();

                int adjX = (int)Math.Max(0, Math.Round(minX * invScale));
                int adjY = (int)Math.Max(0, Math.Round(minY * invScale));
                int adjW = (int)Math.Max(1, Math.Round((maxX - minX) * invScale));
                int adjH = (int)Math.Max(1, Math.Round((maxY - minY) * invScale));

                int order = items.Count + 1;
                items.Add(new ExtractedTextItem
                {
                    Order = order,
                    Id = $"IDS_STRING_{order}",
                    Text = lineText,
                    Confidence = Math.Round(avgConf, 1),
                    X = adjX,
                    Y = adjY,
                    Width = adjW,
                    Height = adjH,
                    Included = true
                });

                currentLineWords.Clear();
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split('\t');
                if (parts.Length < 12) continue;

                if (!int.TryParse(parts[0], out int level)) continue;

                if (level == 5) // Word level
                {
                    string block = parts[2];
                    string par = parts[3];
                    string line = parts[4];
                    string key = $"{block}_{par}_{line}";

                    if (key != currentLineKey && !string.IsNullOrEmpty(currentLineKey))
                    {
                        FlushCurrentLine();
                    }
                    currentLineKey = key;

                    int left = int.TryParse(parts[6], out int l) ? l : 0;
                    int top = int.TryParse(parts[7], out int t) ? t : 0;
                    int width = int.TryParse(parts[8], out int w) ? w : 0;
                    int height = int.TryParse(parts[9], out int h) ? h : 0;
                    double conf = double.TryParse(parts[10], NumberStyles.Any, CultureInfo.InvariantCulture, out double c) ? c : 0;
                    string wordText = parts[11].Trim();

                    if (!string.IsNullOrEmpty(wordText))
                    {
                        currentLineWords.Add((wordText, conf, left, top, width, height));
                    }
                }
            }

            FlushCurrentLine();

            return items;
        }
    }
}
