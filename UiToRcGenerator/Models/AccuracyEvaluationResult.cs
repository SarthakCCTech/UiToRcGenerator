using System.Collections.Generic;

namespace UiToRcGenerator.Models
{
    public class AccuracyEvaluationResult
    {
        public int TotalGroundTruthStrings { get; set; }
        public int TotalExtractedStrings { get; set; }
        public int ExactMatchCount { get; set; }
        public double ExactMatchPercentage => TotalGroundTruthStrings > 0 
            ? (double)ExactMatchCount / TotalGroundTruthStrings * 100.0 
            : 0;

        public double CharacterAccuracyPercentage { get; set; }
        public double WordAccuracyPercentage { get; set; }

        public List<string> MissedStrings { get; set; } = new();
        public List<string> DuplicatedStrings { get; set; } = new();
        public List<StringComparisonDetail> Comparisons { get; set; } = new();
    }

    public class StringComparisonDetail
    {
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Exact Match", "Partial Match", "Missed", "Extra/Duplicate"
        public double SimilarityPercentage { get; set; }
    }
}
