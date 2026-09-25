using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteReportRedactor
{
    internal static string Clean(string text, IEnumerable<string> privateValues, int limit = 80000)
    {
        text = text.Substring(0, Math.Min(text.Length, 160000));
        foreach (string value in privateValues.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderByDescending(v => v.Length))
            text = text.Replace(value, "[PRIVATE]");
        // Never let diagnostics block gameplay on pathological input. If masking fails, omit it all.
        try
        {
            foreach (string pattern in new[] {
                @"(?:[a-z]:[\\/]|\\\\)[^\r\n\""<>|]*", @"(?<!\d)\d{17}(?!\d)",
                @"(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?", @"\b(?:[0-9a-f]{1,4}:){2,}[0-9a-f:]{0,39}\b",
                @"[\w.+-]+@[\w.-]+\.[a-z]{2,}", @"\b(?:token|password|secret|authorization|api[_-]?key)\s*[:=]\s*[^\s,;]+",
                @"\b(?:gh[pousr]_[a-z0-9_]+|github_pat_[a-z0-9_]+)\b", @"/(?:home|Users)/[^/\s]+[^\r\n]*" })
                text = Regex.Replace(text, pattern, "[REDACTED]", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
            text = Regex.Replace(text, @"[\x00-\x08\x0b\x0c\x0e-\x1f]", "", RegexOptions.None, TimeSpan.FromMilliseconds(200));
        }
        catch (RegexMatchTimeoutException) { return "[OMITTED: privacy filter could not complete]"; }
        return text.Length <= limit ? text : text.Substring(0, limit) + "\n[TRUNCATED]";
    }
}
