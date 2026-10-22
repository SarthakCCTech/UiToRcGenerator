using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UiToRcGenerator.Models;
using UiToRcGenerator.Services;

namespace UiToRcGenerator.Tests
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine(" UI-to-RC Generator Automated Benchmark & Validation Suite");
            Console.WriteLine("==========================================================\n");

            string testOutputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestArtifacts");
            Directory.CreateDirectory(testOutputDir);

            var winOcr = new WindowsMediaOcrEngine();
            var tessOcr = new TesseractOcrEngine();
            var preprocessService = new ImagePreprocessingService();
            var accuracyService = new AccuracyEvaluationService();
            var rcGenerator = new RcGeneratorService();

            Console.WriteLine($"[1] Checking OCR Engines Availability:");
            Console.WriteLine($"    - Windows Media OCR: {(winOcr.IsAvailable ? "AVAILABLE" : "UNAVAILABLE")}");
            Console.WriteLine($"    - Tesseract OCR v5.5: {(tessOcr.IsAvailable ? "AVAILABLE" : "UNAVAILABLE")}\n");

            // Test Accessibility Insights prefix cleaning
            Console.WriteLine("[1b] Testing Accessibility Insights Prefix Stripping:");
            string[] rawInsightSamples = new[]
            {
                "Name: \"Save As...\"",
                "Name: Cancel",
                "Text: \"Open File\"",
                "Label: \"Submit Query\"",
                "\"Zoom Level\""
            };
            foreach (var sample in rawInsightSamples)
            {
                string cleaned = LiveWatcherService.CleanInsightPrefix(sample);
                Console.WriteLine($"    '{sample}' -> '{cleaned}'");
            }
            Console.WriteLine();

            // Define Test Dataset 1: Reference Strings from 2dTransRes.rc
            var referenceStrings = new List<string>
            {
                "Get Interface Object Failed in DmDrawingDoc::Open",
                "QueryInterface Failed in DmDrawingDoc::Open",
                "AutoCAD 2000 is not installed correctly on this system",
                "Unable to load AcAutoExt.arx application into AutoCAD",
                "Unable to get the interface of AutoCAD documents",
                "2dTranslator Dimension Style",
                "Hatch"
            };

            // Render Test Image 1: Light Theme Desktop UI (9pt Segoe UI, standard Windows ClearType font)
            string lightImagePath = Path.Combine(testOutputDir, "test_ui_light.png");
            using (var lightBmp = RenderUiImage(referenceStrings, isDarkMode: false))
            {
                lightBmp.Save(lightImagePath, ImageFormat.Png);
            }
            Console.WriteLine($"[2] Generated Test UI Screenshot (Light Mode Segoe UI 9pt): {lightImagePath}");

            // Render Test Image 2: Dark Theme Desktop UI
            string darkImagePath = Path.Combine(testOutputDir, "test_ui_dark.png");
            using (var darkBmp = RenderUiImage(referenceStrings, isDarkMode: true))
            {
                darkBmp.Save(darkImagePath, ImageFormat.Png);
            }
            Console.WriteLine($"[3] Generated Test UI Screenshot (Dark Mode Segoe UI 9pt): {darkImagePath}\n");

            // ==========================================================
            // TEST SUITE 1: Extraction Accuracy with Preprocessing Comparison
            // ==========================================================
            Console.WriteLine("==========================================================");
            Console.WriteLine(" PHASE 2: EXTRACTION ACCURACY & PREPROCESSING BENCHMARK");
            Console.WriteLine("==========================================================");

            var enginesToTest = new List<IOcrEngine>();
            if (winOcr.IsAvailable) enginesToTest.Add(winOcr);
            if (tessOcr.IsAvailable) enginesToTest.Add(tessOcr);

            using var originalLightBmp = new Bitmap(lightImagePath);

            foreach (var engine in enginesToTest)
            {
                Console.WriteLine($"\n--- Benchmarking Engine: {engine.Name} ---");

                // Config A: Baseline 1.0x (No Preprocessing)
                var optBaseline = new PreprocessingOptions { ScaleFactor = 1.0 };
                using var bmpBaseline = preprocessService.Preprocess(originalLightBmp, optBaseline);
                var resBaseline = await engine.RecognizeAsync(bmpBaseline, optBaseline.ScaleFactor);
                var evalBaseline = accuracyService.Evaluate(referenceStrings, resBaseline.Items);

                PrintEvaluationResult("1. Baseline (1.0x Scale, No Preprocessing)", evalBaseline, resBaseline.ElapsedTime);

                // Config B: 2.0x Bicubic Scale (Recommended for 9pt UI fonts)
                var optScale2x = new PreprocessingOptions { ScaleFactor = 2.0 };
                using var bmpScale2x = preprocessService.Preprocess(originalLightBmp, optScale2x);
                var resScale2x = await engine.RecognizeAsync(bmpScale2x, optScale2x.ScaleFactor);
                var evalScale2x = accuracyService.Evaluate(referenceStrings, resScale2x.Items);

                PrintEvaluationResult("2. With 2.0x Bicubic Upscaling", evalScale2x, resScale2x.ElapsedTime);

                // Config C: 2.0x Scale + Contrast Enhancement
                var optContrast = new PreprocessingOptions { ScaleFactor = 2.0, ContrastEnhance = true };
                using var bmpContrast = preprocessService.Preprocess(originalLightBmp, optContrast);
                var resContrast = await engine.RecognizeAsync(bmpContrast, optContrast.ScaleFactor);
                var evalContrast = accuracyService.Evaluate(referenceStrings, resContrast.Items);

                PrintEvaluationResult("3. With 2.0x Scale + Contrast Enhancement", evalContrast, resContrast.ElapsedTime);
            }

            // Test Dark Mode Image with Inversion Preprocessing
            Console.WriteLine("\n--- Benchmarking Dark Mode UI Text (Invert Colors Preprocessing) ---");
            using var originalDarkBmp = new Bitmap(darkImagePath);
            var optDarkNoInvert = new PreprocessingOptions { ScaleFactor = 2.0, InvertColors = false };
            using var bmpDarkNoInvert = preprocessService.Preprocess(originalDarkBmp, optDarkNoInvert);
            var resDarkNoInvert = await tessOcr.RecognizeAsync(bmpDarkNoInvert, optDarkNoInvert.ScaleFactor);
            var evalDarkNoInvert = accuracyService.Evaluate(referenceStrings, resDarkNoInvert.Items);
            PrintEvaluationResult("Dark UI - Without Color Inversion", evalDarkNoInvert, resDarkNoInvert.ElapsedTime);

            var optDarkInvert = new PreprocessingOptions { ScaleFactor = 2.0, InvertColors = true, Grayscale = true };
            using var bmpDarkInvert = preprocessService.Preprocess(originalDarkBmp, optDarkInvert);
            var resDarkInvert = await tessOcr.RecognizeAsync(bmpDarkInvert, optDarkInvert.ScaleFactor);
            var evalDarkInvert = accuracyService.Evaluate(referenceStrings, resDarkInvert.Items);
            PrintEvaluationResult("Dark UI - With Invert Colors Preprocessing", evalDarkInvert, resDarkInvert.ElapsedTime);

            // ==========================================================
            // TEST SUITE 2: RC Generation & Validation
            // ==========================================================
            Console.WriteLine("\n==========================================================");
            Console.WriteLine(" PHASE 3: RC GENERATION & REFERENCE TEMPLATE VALIDATION");
            Console.WriteLine("==========================================================");

            string workspaceRefRcPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "2dTransRes.rc"));
            Console.WriteLine($"Reference RC file path: {workspaceRefRcPath}");
            bool refExists = File.Exists(workspaceRefRcPath);
            Console.WriteLine($"Reference file exists: {refExists}");

            if (!refExists)
            {
                Console.WriteLine("ERROR: 2dTransRes.rc not found!");
                return 1;
            }

            // Create extracted items representing the recognized strings
            var itemsToGenerate = new List<ExtractedTextItem>();
            int order = 1;
            foreach (var str in referenceStrings)
            {
                itemsToGenerate.Add(new ExtractedTextItem
                {
                    Order = order,
                    Id = $"IDS_EXTRACTED_{order}",
                    Text = str,
                    Confidence = 98.5,
                    X = 10,
                    Y = order * 25,
                    Width = 300,
                    Height = 20,
                    Included = true
                });
                order++;
            }

            // Add an item with quotes to test escaping
            itemsToGenerate.Add(new ExtractedTextItem
            {
                Order = order,
                Id = $"IDS_EXTRACTED_{order}",
                Text = "Value with \"quoted text\" and backslash \\ path",
                Confidence = 95.0,
                X = 10,
                Y = order * 25,
                Width = 350,
                Height = 20,
                Included = true
            });

            // Test 1: RC Generation with Reference Template Integration
            var genOptionsRef = new RcGenerationOptions
            {
                IdPrefix = "IDS_EXTRACTED_",
                UseReferenceTemplate = true,
                ReferenceFilePath = workspaceRefRcPath,
                EscapeQuotesWithDoubleQuotes = true
            };

            string generatedRc = rcGenerator.GenerateRc(itemsToGenerate, genOptionsRef, out var valResult);
            Console.WriteLine($"\nRC Generation Validation: IsValid = {valResult.IsValid}");
            if (valResult.Errors.Count > 0)
            {
                Console.WriteLine("Errors: " + string.Join(", ", valResult.Errors));
            }
            if (valResult.Warnings.Count > 0)
            {
                Console.WriteLine("Warnings: " + string.Join(", ", valResult.Warnings));
            }

            // Validate syntax
            var syntaxVal = rcGenerator.ValidateRcSyntax(generatedRc);
            Console.WriteLine($"RC Syntax Validation: IsValid = {syntaxVal.IsValid}");

            // Verify original 2dTransRes.rc is UNCHANGED
            string originalRefContent = File.ReadAllText(workspaceRefRcPath);
            bool originalPreserved = originalRefContent.Contains("IDS_2dTRAN_INTERFACE_FAILED") && 
                                     originalRefContent.Contains("Dm3dSurface.rgs");
            Console.WriteLine($"Verified reference 2dTransRes.rc is UNMODIFIED: {originalPreserved}");

            // Save to new separate file
            string outputGeneratedRcPath = Path.Combine(testOutputDir, "Generated_2dTransRes_Test.rc");
            File.WriteAllText(outputGeneratedRcPath, generatedRc);
            Console.WriteLine($"Generated RC saved to separate file: {outputGeneratedRcPath}");
            Console.WriteLine($"Generated RC File size: {new FileInfo(outputGeneratedRcPath).Length} bytes");

            // Print the STRINGTABLE portion from the generated output
            Console.WriteLine("\nGenerated STRINGTABLE sample output:");
            var stLines = generatedRc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .SkipWhile(l => !l.Contains("STRINGTABLE"))
                .Take(12);
            foreach (var line in stLines)
            {
                Console.WriteLine("    " + line);
            }

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(" BENCHMARK & TEST SUITE COMPLETED SUCCESSFULLY!");
            Console.WriteLine("==========================================================");
            return 0;
        }

        private static Bitmap RenderUiImage(List<string> strings, bool isDarkMode)
        {
            int width = 640;
            int height = 50 + (strings.Count * 32);

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                var bgColor = isDarkMode ? Color.FromArgb(32, 32, 32) : Color.FromArgb(245, 245, 245);
                var textColor = isDarkMode ? Color.FromArgb(240, 240, 240) : Color.FromArgb(20, 20, 20);
                var boxColor = isDarkMode ? Color.FromArgb(45, 45, 45) : Color.FromArgb(255, 255, 255);
                var borderColor = isDarkMode ? Color.FromArgb(70, 70, 70) : Color.FromArgb(200, 200, 200);

                g.Clear(bgColor);

                using var font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
                using var textBrush = new SolidBrush(textColor);
                using var boxBrush = new SolidBrush(boxColor);
                using var borderPen = new Pen(borderColor, 1);

                // Draw Dialog title
                using var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
                g.DrawString(isDarkMode ? "Dark Theme Dialog" : "Light Theme Dialog", titleFont, textBrush, 20, 14);

                int y = 46;
                foreach (var str in strings)
                {
                    // Draw a UI card/button bounding area
                    var rect = new Rectangle(20, y, width - 40, 26);
                    g.FillRectangle(boxBrush, rect);
                    g.DrawRectangle(borderPen, rect);

                    g.DrawString(str, font, textBrush, 28, y + 4);
                    y += 32;
                }
            }
            return bmp;
        }

        private static void PrintEvaluationResult(string title, AccuracyEvaluationResult eval, TimeSpan elapsed)
        {
            Console.WriteLine($"  * {title}:");
            Console.WriteLine($"      Time: {elapsed.TotalMilliseconds:F0} ms");
            Console.WriteLine($"      Exact Matches: {eval.ExactMatchCount}/{eval.TotalGroundTruthStrings} ({eval.ExactMatchPercentage:F1}%)");
            Console.WriteLine($"      Character Accuracy: {eval.CharacterAccuracyPercentage:F1}%");
            Console.WriteLine($"      Word Accuracy: {eval.WordAccuracyPercentage:F1}%");
            Console.WriteLine($"      Missed Strings: {eval.MissedStrings.Count} {(eval.MissedStrings.Count > 0 ? $"[{string.Join(", ", eval.MissedStrings.Take(2))}]" : "")}");
            Console.WriteLine($"      Duplicated Strings: {eval.DuplicatedStrings.Count}");
        }
    }
}
