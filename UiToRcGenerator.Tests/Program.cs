using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UiToRcGenerator.Models;
using UiToRcGenerator.Services;

namespace UiToRcGenerator.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine(" UI-to-RC Generator Automated Validation Suite (UIA & RC)");
            Console.WriteLine("==========================================================\n");

            int failedTests = 0;

            // -------------------------------------------------------------
            // TEST 1: Accessibility Insights Prefix Stripping
            // -------------------------------------------------------------
            Console.WriteLine("[1] Testing Accessibility Insights / Inspect Prefix Stripping:");
            var testCases = new (string Input, string Expected)[]
            {
                ("Name: \"Save As...\"", "Save As..."),
                ("Name: Cancel", "Cancel"),
                ("Text: \"Open File\"", "Open File"),
                ("Label: \"Submit Query\"", "Submit Query"),
                ("\"Zoom Level\"", "Zoom Level"),
                ("Text: OK", "OK")
            };

            foreach (var (input, expected) in testCases)
            {
                string cleaned = LiveWatcherService.CleanInsightPrefix(input);
                bool passed = string.Equals(cleaned, expected, StringComparison.Ordinal);
                Console.WriteLine($"    {(passed ? "PASS" : "FAIL")}: '{input}' -> '{cleaned}' (Expected: '{expected}')");
                if (!passed) failedTests++;
            }
            Console.WriteLine();

            // -------------------------------------------------------------
            // TEST 2: UIA ExtractedTextItem Modeling & Deduplication
            // -------------------------------------------------------------
            Console.WriteLine("[2] Testing ExtractedTextItem Modeling & Deduplication:");
            var capturedItems = new List<ExtractedTextItem>
            {
                new ExtractedTextItem { Order = 1, Id = "IDS_UI_1", Text = "Open File", ControlType = "MenuItem", Confidence = 100.0, Included = true },
                new ExtractedTextItem { Order = 2, Id = "IDS_UI_2", Text = "Save", ControlType = "Button", Confidence = 100.0, Included = true },
                new ExtractedTextItem { Order = 3, Id = "IDS_UI_3", Text = "Cancel", ControlType = "Button", Confidence = 100.0, Included = true },
                new ExtractedTextItem { Order = 4, Id = "IDS_UI_4", Text = "Save", ControlType = "Button", Confidence = 100.0, Included = true } // Duplicate
            };

            // Deduplication test
            var uniqueItems = capturedItems
                .GroupBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            bool dedupPassed = uniqueItems.Count == 3;
            Console.WriteLine($"    {(dedupPassed ? "PASS" : "FAIL")}: Deduplication reduced 4 items (with 1 duplicate) to {uniqueItems.Count} items.");
            if (!dedupPassed) failedTests++;
            Console.WriteLine();

            // -------------------------------------------------------------
            // TEST 3: Visual C++ .rc Standalone Generation
            // -------------------------------------------------------------
            Console.WriteLine("[3] Testing Visual C++ .rc Generation (Standalone):");
            var rcGenerator = new RcGeneratorService();
            var standaloneOptions = new RcGenerationOptions { UseReferenceTemplate = false, IdPrefix = "IDS_UI_" };
            string standaloneRc = rcGenerator.GenerateRc(uniqueItems, standaloneOptions, out var valResult);

            bool hasStringtable = standaloneRc.Contains("STRINGTABLE") && standaloneRc.Contains("BEGIN") && standaloneRc.Contains("END");
            bool hasItems = standaloneRc.Contains("IDS_UI_1") && standaloneRc.Contains("\"Open File\"");
            bool standalonePassed = valResult.IsValid && hasStringtable && hasItems;

            Console.WriteLine($"    {(standalonePassed ? "PASS" : "FAIL")}: Standalone RC output has valid STRINGTABLE structure.");
            if (!standalonePassed) failedTests++;
            Console.WriteLine();

            // -------------------------------------------------------------
            // TEST 4: Visual C++ .rc Syntax Validation
            // -------------------------------------------------------------
            Console.WriteLine("[4] Testing Visual C++ .rc Syntax Validation:");
            var syntaxResult = rcGenerator.ValidateRcSyntax(standaloneRc);
            Console.WriteLine($"    {(syntaxResult.IsValid ? "PASS" : "FAIL")}: Syntax validation reported IsValid={syntaxResult.IsValid}.");
            if (!syntaxResult.IsValid)
            {
                foreach (var err in syntaxResult.Errors)
                {
                    Console.WriteLine($"        Error: {err}");
                }
                failedTests++;
            }
            Console.WriteLine();

            // -------------------------------------------------------------
            // TEST 5: Template Merging into 2dTransRes.rc (if present)
            // -------------------------------------------------------------
            Console.WriteLine("[5] Testing Reference Template Merging (2dTransRes.rc):");
            string refPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "2dTransRes.rc"));
            if (File.Exists(refPath))
            {
                var templateOptions = new RcGenerationOptions { UseReferenceTemplate = true, ReferenceFilePath = refPath, IdPrefix = "IDS_UI_" };
                string mergedRc = rcGenerator.GenerateRc(uniqueItems, templateOptions, out var templateVal);
                bool mergeValid = templateVal.IsValid && !string.IsNullOrWhiteSpace(mergedRc) && mergedRc.Contains("STRINGTABLE") && mergedRc.Contains("IDS_UI_1");
                Console.WriteLine($"    {(mergeValid ? "PASS" : "FAIL")}: Merged successfully into reference template ({mergedRc.Length} chars).");
                if (!mergeValid) failedTests++;
            }
            else
            {
                Console.WriteLine($"    [INFO]: 2dTransRes.rc not found at {refPath}, skipping template merge test.");
            }
            Console.WriteLine();

            // -------------------------------------------------------------
            // Summary
            // -------------------------------------------------------------
            Console.WriteLine("==========================================================");
            if (failedTests == 0)
            {
                Console.WriteLine(" ALL TESTS PASSED! UI-to-RC Hover & UIA Pipeline is Healthy.");
                Console.WriteLine("==========================================================");
                return 0;
            }
            else
            {
                Console.WriteLine($" {failedTests} TEST(S) FAILED.");
                Console.WriteLine("==========================================================");
                return 1;
            }
        }
    }
}
