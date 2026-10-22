using System;
using System.Collections.Generic;
using System.Linq;
using UiToRcGenerator.Models;

namespace UiToRcGenerator.Services
{
    public class AccuracyEvaluationService
    {
        public AccuracyEvaluationResult Evaluate(IEnumerable<string> expectedList, IEnumerable<ExtractedTextItem> actualList)
        {
            var result = new AccuracyEvaluationResult();

            var expected = expectedList
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            var actual = actualList
                .Where(i => i.Included && !string.IsNullOrWhiteSpace(i.Text))
                .Select(i => i.Text.Trim())
                .ToList();

            result.TotalGroundTruthStrings = expected.Count;
            result.TotalExtractedStrings = actual.Count;

            if (expected.Count == 0)
            {
                return result;
            }

            var unmatchedExpected = new HashSet<int>(Enumerable.Range(0, expected.Count));
            var matchedActualIndices = new HashSet<int>();
            var expectedMatchCount = new int[expected.Count];

            // 1. Exact matching pass
            for (int e = 0; e < expected.Count; e++)
            {
                string expStr = expected[e];
                for (int a = 0; a < actual.Count; a++)
                {
                    if (matchedActualIndices.Contains(a)) continue;

                    if (string.Equals(expStr, actual[a], StringComparison.OrdinalIgnoreCase))
                    {
                        matchedActualIndices.Add(a);
                        unmatchedExpected.Remove(e);
                        expectedMatchCount[e]++;

                        result.Comparisons.Add(new StringComparisonDetail
                        {
                            Expected = expStr,
                            Actual = actual[a],
                            Status = "Exact Match",
                            SimilarityPercentage = 100.0
                        });
                        break;
                    }
                }
            }

            result.ExactMatchCount = result.Comparisons.Count(c => c.Status == "Exact Match");

            // 2. Fuzzy / Partial matching pass for remaining expected strings
            foreach (int e in unmatchedExpected.ToList())
            {
                string expStr = expected[e];
                int bestActualIdx = -1;
                double bestSimilarity = 0.0;

                for (int a = 0; a < actual.Count; a++)
                {
                    if (matchedActualIndices.Contains(a)) continue;

                    double sim = CalculateSimilarity(expStr, actual[a]);
                    if (sim > bestSimilarity && sim >= 0.45) // At least 45% similarity to be considered a match attempt
                    {
                        bestSimilarity = sim;
                        bestActualIdx = a;
                    }
                }

                if (bestActualIdx >= 0)
                {
                    matchedActualIndices.Add(bestActualIdx);
                    unmatchedExpected.Remove(e);
                    expectedMatchCount[e]++;

                    result.Comparisons.Add(new StringComparisonDetail
                    {
                        Expected = expStr,
                        Actual = actual[bestActualIdx],
                        Status = "Partial Match (Typo/OCR error)",
                        SimilarityPercentage = Math.Round(bestSimilarity * 100.0, 1)
                    });
                }
            }

            // 3. Any expected strings that were never matched are missed
            foreach (int e in unmatchedExpected)
            {
                result.MissedStrings.Add(expected[e]);
                result.Comparisons.Add(new StringComparisonDetail
                {
                    Expected = expected[e],
                    Actual = "(NONE - MISSED)",
                    Status = "Missed",
                    SimilarityPercentage = 0.0
                });
            }

            // 4. Any remaining actual items not matched to an expected string are Extra / Duplicates
            for (int a = 0; a < actual.Count; a++)
            {
                if (!matchedActualIndices.Contains(a))
                {
                    string extraText = actual[a];
                    // Check if this extra text is an exact duplicate of an already matched string
                    bool isDuplicate = expected.Any(e => string.Equals(e, extraText, StringComparison.OrdinalIgnoreCase));
                    if (isDuplicate)
                    {
                        result.DuplicatedStrings.Add(extraText);
                    }

                    result.Comparisons.Add(new StringComparisonDetail
                    {
                        Expected = "(NONE)",
                        Actual = extraText,
                        Status = isDuplicate ? "Duplicate String" : "Extra / Noise",
                        SimilarityPercentage = 0.0
                    });
                }
            }

            // Also check if any expected string was matched multiple times
            for (int e = 0; e < expected.Count; e++)
            {
                if (expectedMatchCount[e] > 1)
                {
                    result.DuplicatedStrings.Add(expected[e]);
                }
            }

            // Calculate overall Character and Word accuracy
            int totalExpectedChars = expected.Sum(s => s.Length);
            int totalEditDistance = 0;

            foreach (var comp in result.Comparisons.Where(c => c.Status == "Exact Match" || c.Status.StartsWith("Partial")))
            {
                totalEditDistance += ComputeLevenshteinDistance(comp.Expected, comp.Actual);
            }
            foreach (var missed in result.MissedStrings)
            {
                totalEditDistance += missed.Length;
            }

            double charAcc = totalExpectedChars > 0 
                ? Math.Max(0, (1.0 - (double)totalEditDistance / Math.Max(totalExpectedChars, totalEditDistance)) * 100.0) 
                : 100.0;
            result.CharacterAccuracyPercentage = Math.Round(charAcc, 1);

            // Word accuracy
            int totalWords = expected.Sum(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            int correctWords = 0;
            foreach (var comp in result.Comparisons.Where(c => c.Status == "Exact Match"))
            {
                correctWords += comp.Expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            }
            result.WordAccuracyPercentage = totalWords > 0 
                ? Math.Round((double)correctWords / totalWords * 100.0, 1) 
                : 100.0;

            return result;
        }

        private static double CalculateSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2)) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int dist = ComputeLevenshteinDistance(s1.ToLowerInvariant(), s2.ToLowerInvariant());
            int maxLen = Math.Max(s1.Length, s2.Length);
            return 1.0 - ((double)dist / maxLen);
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            return d[n, m];
        }
    }
}
