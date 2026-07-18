using System.Text.RegularExpressions;

namespace ExcelToSql.Services;

public static class DataCleaner
{
    private static readonly Regex Quoted = new("\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex Brackets = new(@"\[(.*?)\]", RegexOptions.Compiled);

    public static string Apply(string value, string? pattern)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(pattern))
            return value;

        var (chars, tokens) = Parse(pattern);
        var result = value;
        foreach (var token in tokens)
            result = result.Replace(token, string.Empty, StringComparison.Ordinal);

        if (chars.Count == 0)
            return result;

        return new string(result.Where(c => !chars.Contains(c)).ToArray());
    }

    public static (HashSet<char> Chars, List<string> Tokens) Parse(string pattern)
    {
        var chars = new HashSet<char>();
        var tokens = new List<string>();
        var work = pattern;

        foreach (Match match in Quoted.Matches(work))
        {
            var token = match.Groups[1].Value;
            if (token.Length > 0)
                tokens.Add(token);
        }

        work = Quoted.Replace(work, " ");
        var brackets = Brackets.Matches(work);
        if (brackets.Count > 0)
        {
            foreach (Match match in brackets)
            {
                chars.Add('[');
                chars.Add(']');
                foreach (var c in match.Groups[1].Value)
                    chars.Add(c);
            }

            return (chars, tokens);
        }

        foreach (var c in work)
            chars.Add(c);

        return (chars, tokens);
    }
}
