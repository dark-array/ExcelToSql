using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ExcelToSql.Services;

public static class IdentifierSanitizer
{
    private static readonly Regex NonWord = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

    public static string Sanitize(string? name, int maxLength = 64)
    {
        var value = RemoveDiacritics(name ?? string.Empty).Trim();
        value = value.Replace(' ', '_');
        value = NonWord.Replace(value, "_");
        value = Regex.Replace(value, "_+", "_").Trim('_');

        if (value.Length == 0)
            value = "COL";

        if (char.IsDigit(value[0]))
            value = "C_" + value;

        if (value.Length > maxLength)
            value = value[..maxLength].TrimEnd('_');

        return value.Length == 0 ? "COL" : value;
    }

    public static string Unique(string name, ISet<string> used, int maxLength = 64)
    {
        var baseName = Sanitize(name, maxLength);
        var candidate = baseName;
        var i = 2;
        while (used.Contains(candidate.ToUpperInvariant()))
        {
            var suffix = "_" + i;
            var keep = Math.Max(1, maxLength - suffix.Length);
            candidate = (baseName.Length > keep ? baseName[..keep] : baseName) + suffix;
            i++;
        }

        used.Add(candidate.ToUpperInvariant());
        return candidate;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
