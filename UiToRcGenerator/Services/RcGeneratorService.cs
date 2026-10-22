using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UiToRcGenerator.Models;

namespace UiToRcGenerator.Services
{
    /// <summary>
    /// Configuration options for generating the Visual C++ .rc file.
    /// </summary>
    public class RcGenerationOptions
    {
        /// <summary>
        /// Prefix for the generated resource IDs (default: "IDS_UI_").
        /// </summary>
        public string IdPrefix { get; set; } = "IDS_UI_";

        /// <summary>
        /// When true, injects the strings into the structure of an existing reference file (like 2dTransRes.rc).
        /// When false, creates a clean standalone .rc file.
        /// </summary>
        public bool UseReferenceTemplate { get; set; } = true;

        /// <summary>
        /// File path to the reference .rc file to use as a structural template.
        /// </summary>
        public string? ReferenceFilePath { get; set; }

        /// <summary>
        /// In standard Visual C++ .rc files, double quotes inside strings are escaped by writing two double quotes ("").
        /// </summary>
        public bool EscapeQuotesWithDoubleQuotes { get; set; } = true;
    }

    /// <summary>
    /// Holds the results of validating an .rc file or extracted items list.
    /// </summary>
    public class RcValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? CompilerOutput { get; set; }
    }

    /// <summary>
    /// Service that creates and validates Microsoft Visual C++ Resource Script (.rc) files.
    /// 
    /// What is an .rc file?
    /// An .rc file is a C++ resource file containing a STRINGTABLE block.
    /// Win32 applications compile this table into the executable to store user interface strings.
    /// 
    /// Example format:
    ///   STRINGTABLE
    ///   BEGIN
    ///       IDS_BUTTON_SAVE        "Save Document"
    ///       IDS_BUTTON_CANCEL      "Cancel"
    ///   END
    /// </summary>
    public class RcGeneratorService
    {
        // Regular expression to check if an ID is a valid C/C++ identifier (letters, digits, underscores, cannot start with digit)
        private static readonly Regex ValidIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// Main method that generates the complete .rc file text from the list of captured items.
        /// </summary>
        public string GenerateRc(IEnumerable<ExtractedTextItem> items, RcGenerationOptions options, out RcValidationResult validation)
        {
            // Filter only items that are checked (Included == true) and not empty
            var activeItems = items.Where(item => item.Included && !string.IsNullOrWhiteSpace(item.Text)).ToList();

            // Run validation to check for duplicate IDs, invalid characters, or empty strings
            validation = ValidateItems(activeItems);

            var builder = new StringBuilder();

            // Option 1: Use an existing reference template file (e.g., 2dTransRes.rc)
            bool hasValidTemplate = options.UseReferenceTemplate && 
                                    !string.IsNullOrEmpty(options.ReferenceFilePath) && 
                                    File.Exists(options.ReferenceFilePath);

            if (hasValidTemplate)
            {
                string mergedRc = GenerateFromReferenceTemplate(options.ReferenceFilePath!, activeItems, options);
                builder.Append(mergedRc);
            }
            // Option 2: Generate a clean standalone .rc file from scratch
            else
            {
                builder.AppendLine("// Microsoft Visual C++ generated resource script.");
                builder.AppendLine("//");
                builder.AppendLine("#include \"resource.h\"");
                builder.AppendLine();
                builder.AppendLine("#define APSTUDIO_READONLY_SYMBOLS");
                builder.AppendLine("/////////////////////////////////////////////////////////////////////////////");
                builder.AppendLine("//");
                builder.AppendLine("// Generated from the TEXTINCLUDE 2 resource.");
                builder.AppendLine("//");
                builder.AppendLine("#include <afxres.h>");
                builder.AppendLine();
                builder.AppendLine("/////////////////////////////////////////////////////////////////////////////");
                builder.AppendLine("#undef APSTUDIO_READONLY_SYMBOLS");
                builder.AppendLine();
                builder.AppendLine("/////////////////////////////////////////////////////////////////////////////");
                builder.AppendLine("// English (U.S.) resources");
                builder.AppendLine();
                builder.AppendLine("#if !defined(AFX_RESOURCE_DLL) || defined(AFX_TARG_ENU)");
                builder.AppendLine("#ifdef _WIN32");
                builder.AppendLine("LANGUAGE LANG_ENGLISH, SUBLANG_ENGLISH_US");
                builder.AppendLine("#pragma code_page(1252)");
                builder.AppendLine("#endif //_WIN32");
                builder.AppendLine();
                builder.AppendLine("/////////////////////////////////////////////////////////////////////////////");
                builder.AppendLine("//");
                builder.AppendLine("// String Table");
                builder.AppendLine("//");
                builder.AppendLine();
                builder.AppendLine("STRINGTABLE ");
                builder.AppendLine("BEGIN");

                // Write each active string into the table
                foreach (var item in activeItems)
                {
                    string safeId = string.IsNullOrWhiteSpace(item.Id) 
                        ? $"{options.IdPrefix}{item.Order}" 
                        : item.Id.Trim();

                    string safeString = EscapeRcString(item.Text, options.EscapeQuotesWithDoubleQuotes);

                    // Align the string value neatly in columns (standard Win32 convention)
                    int paddingSpaces = Math.Max(1, 36 - safeId.Length);
                    builder.AppendLine($"    {safeId}{new string(' ', paddingSpaces)}\"{safeString}\"");
                }

                builder.AppendLine("END");
                builder.AppendLine();
                builder.AppendLine("#endif    // English (U.S.) resources");
                builder.AppendLine("/////////////////////////////////////////////////////////////////////////////");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Escapes special characters for Visual C++ resource scripts.
        /// In .rc scripts, quotation marks inside a string are escaped by doubling them:
        /// For example: Hello "World" becomes "Hello ""World"""
        /// </summary>
        public string EscapeRcString(string text, bool useDoubleQuotes = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (char character in text)
            {
                switch (character)
                {
                    case '"':
                        // In Visual C++ .rc files, double quotes are escaped as ""
                        builder.Append(useDoubleQuotes ? "\"\"" : "\\\"");
                        break;

                    case '\\':
                        // Backslashes are escaped as \\
                        builder.Append("\\\\");
                        break;

                    case '\r':
                        // Carriage returns are ignored when paired with \n
                        break;

                    case '\n':
                        // Newlines become literal \n
                        builder.Append("\\n");
                        break;

                    case '\t':
                        // Tabs become literal \t
                        builder.Append("\\t");
                        break;

                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Reads an existing reference template file (like 2dTransRes.rc) and replaces its String Table
        /// section with our newly captured strings, preserving all existing headers and directives.
        /// </summary>
        private string GenerateFromReferenceTemplate(string referencePath, List<ExtractedTextItem> activeItems, RcGenerationOptions options)
        {
            string originalTemplateText = File.ReadAllText(referencePath);

            // Step 1: Build the new STRINGTABLE block
            var stringTableBuilder = new StringBuilder();
            stringTableBuilder.AppendLine("STRINGTABLE ");
            stringTableBuilder.AppendLine("BEGIN");

            foreach (var item in activeItems)
            {
                string safeId = string.IsNullOrWhiteSpace(item.Id) 
                    ? $"{options.IdPrefix}{item.Order}" 
                    : item.Id.Trim();

                string safeString = EscapeRcString(item.Text, options.EscapeQuotesWithDoubleQuotes);
                int paddingSpaces = Math.Max(1, 36 - safeId.Length);
                stringTableBuilder.AppendLine($"    {safeId}{new string(' ', paddingSpaces)}\"{safeString}\"");
            }

            stringTableBuilder.AppendLine("END");

            // Step 2: Locate the String Table section in the reference file
            var stringTableRegex = new Regex(@"(//\s*String Table\s*\r?\n//\r?\n\r?\n)([\s\S]*?)(#endif\s*//\s*English \(U\.S\.\) resources)", RegexOptions.IgnoreCase);
            var match = stringTableRegex.Match(originalTemplateText);

            if (match.Success)
            {
                string headerPart = match.Groups[1].Value;
                string footerPart = match.Groups[3].Value;

                // Replace the old string table body with our new one
                string newSection = headerPart + stringTableBuilder.ToString() + "\r\n" + footerPart;
                return originalTemplateText.Substring(0, match.Index) + newSection + originalTemplateText.Substring(match.Index + match.Length);
            }
            else
            {
                // Fallback: If pattern didn't match exactly, append before the last #endif
                int lastEndifIndex = originalTemplateText.LastIndexOf("#endif", StringComparison.OrdinalIgnoreCase);
                if (lastEndifIndex >= 0)
                {
                    return originalTemplateText.Insert(lastEndifIndex, "\r\n" + stringTableBuilder.ToString() + "\r\n");
                }

                return originalTemplateText + "\r\n" + stringTableBuilder.ToString();
            }
        }

        /// <summary>
        /// Validates that all items have non-empty, unique, and valid C++ identifiers.
        /// </summary>
        public RcValidationResult ValidateItems(List<ExtractedTextItem> items)
        {
            var result = new RcValidationResult { IsValid = true };
            var seenIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                // Check 1: Empty Resource ID
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    result.Errors.Add($"Item #{item.Order} ('{item.Text}') has an empty Resource ID.");
                    result.IsValid = false;
                }
                else
                {
                    string id = item.Id.Trim();

                    // Check 2: Valid C Identifier format (e.g., IDS_BUTTON_1)
                    if (!ValidIdentifierRegex.IsMatch(id))
                    {
                        result.Errors.Add($"Resource ID '{id}' is invalid. Must be a valid C identifier (letters, digits, underscores, cannot start with a digit).");
                        result.IsValid = false;
                    }

                    // Check 3: Duplicate IDs
                    if (seenIdentifiers.Contains(id))
                    {
                        result.Errors.Add($"Duplicate Resource ID detected: '{id}'. Resource IDs must be unique.");
                        result.IsValid = false;
                    }
                    else
                    {
                        seenIdentifiers.Add(id);
                    }
                }

                // Check 4: Empty string warning
                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    result.Warnings.Add($"Item #{item.Order} ({item.Id}) has empty string content.");
                }
            }

            return result;
        }

        /// <summary>
        /// Checks the generated .rc script for syntax errors like mismatched BEGIN / END blocks.
        /// If Microsoft rc.exe is installed, it also attempts test compilation.
        /// </summary>
        public RcValidationResult ValidateRcSyntax(string rcContent)
        {
            var result = new RcValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(rcContent))
            {
                result.Errors.Add("RC content is empty.");
                result.IsValid = false;
                return result;
            }

            // Check 1: Ensure BEGIN and END counts match
            int countBegin = Regex.Matches(rcContent, @"\bBEGIN\b").Count;
            int countEnd = Regex.Matches(rcContent, @"\bEND\b").Count;

            if (countBegin != countEnd)
            {
                result.Errors.Add($"Mismatched BEGIN/END blocks: Found {countBegin} BEGIN and {countEnd} END statements.");
                result.IsValid = false;
            }

            // Check 2: Ensure STRINGTABLE keyword exists
            if (!rcContent.Contains("STRINGTABLE", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add("No STRINGTABLE directive found in generated RC content.");
            }

            // Check 3: Check if the official Microsoft Resource Compiler (rc.exe) is on this machine
            string? rcExePath = FindRcExe();
            if (rcExePath != null)
            {
                result.CompilerOutput = RunRcCompiler(rcExePath, rcContent, result);
            }
            else
            {
                result.Warnings.Add("Microsoft Resource Compiler (rc.exe) not found on system PATH; static syntax validation applied.");
            }

            return result;
        }

        /// <summary>
        /// Searches the system PATH to locate rc.exe (Microsoft Resource Compiler).
        /// </summary>
        private static string? FindRcExe()
        {
            string? pathEnvironment = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnvironment))
            {
                string[] directories = pathEnvironment.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (string directory in directories)
                {
                    try
                    {
                        string candidatePath = Path.Combine(directory.Trim(), "rc.exe");
                        if (File.Exists(candidatePath))
                        {
                            return candidatePath;
                        }
                    }
                    catch
                    {
                        // Ignore permission/path errors while searching directories
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Invokes rc.exe in a temporary directory to verify that Microsoft Visual C++ compiles the script without errors.
        /// </summary>
        private static string RunRcCompiler(string rcExePath, string rcContent, RcValidationResult result)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "RcCompileCheck_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);
            string testRcFile = Path.Combine(tempFolder, "test.rc");
            string testResFile = Path.Combine(tempFolder, "test.res");

            try
            {
                File.WriteAllText(testRcFile, rcContent, Encoding.Default);

                var startInfo = new ProcessStartInfo
                {
                    FileName = rcExePath,
                    Arguments = $"/v /r /fo \"{testResFile}\" \"{testResFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string errorOutput = process.StandardError.ReadToEnd();
                    process.WaitForExit(10000);

                    string combinedOutput = (standardOutput + "\n" + errorOutput).Trim();
                    if (process.ExitCode != 0)
                    {
                        result.Errors.Add($"rc.exe reported errors (exit code {process.ExitCode}): {combinedOutput}");
                        result.IsValid = false;
                    }
                    return combinedOutput;
                }
            }
            catch (Exception exception)
            {
                result.Warnings.Add($"Failed to invoke rc.exe: {exception.Message}");
            }
            finally
            {
                // Clean up the temporary folder
                try { Directory.Delete(tempFolder, true); } catch { }
            }

            return string.Empty;
        }
    }
}
