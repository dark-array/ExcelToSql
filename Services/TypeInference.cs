using System.Globalization;
using ExcelToSql.Models;

namespace ExcelToSql.Services;

public static class TypeInference
{
    private static readonly string[] TrueValues = ["true", "t", "yes", "y", "si", "sí", "1"];
    private static readonly string[] FalseValues = ["false", "f", "no", "n", "0"];

    public static ColumnDefinition Infer(string originalName, string sqlName, IEnumerable<string> values)
    {
        var samples = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        var column = new ColumnDefinition
        {
            OriginalName = originalName,
            SqlName = sqlName,
            DataType = LogicalType.Text,
            Length = 255
        };

        if (samples.Count == 0)
            return column;

        var maxLen = samples.Max(v => v.Length);
        column.Length = NextVarcharLength(maxLen);

        if (samples.All(IsBoolean))
        {
            column.DataType = LogicalType.Boolean;
            return column;
        }

        if (samples.All(IsInteger))
        {
            var tooBig = samples.Any(v =>
                !int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
                !int.TryParse(v, NumberStyles.Integer, CultureInfo.CurrentCulture, out _));
            column.DataType = tooBig ? LogicalType.BigInteger : LogicalType.Integer;
            return column;
        }

        if (samples.All(IsDecimal))
        {
            var (precision, scale) = MeasureDecimal(samples);
            column.DataType = LogicalType.Decimal;
            column.Precision = precision;
            column.Scale = scale;
            return column;
        }

        if (samples.All(IsDateTime))
        {
            column.DataType = samples.All(IsDateOnly) ? LogicalType.Date : LogicalType.DateTime;
            return column;
        }

        column.DataType = maxLen > 4000 ? LogicalType.LongText : LogicalType.Text;
        return column;
    }

    public static int NextVarcharLength(int maxLen)
    {
        if (maxLen <= 50) return 50;
        if (maxLen <= 100) return 100;
        if (maxLen <= 255) return 255;
        if (maxLen <= 500) return 500;
        if (maxLen <= 1000) return 1000;
        if (maxLen <= 2000) return 2000;
        if (maxLen <= 4000) return 4000;
        return maxLen;
    }

    public static bool IsBoolean(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return TrueValues.Contains(v) || FalseValues.Contains(v);
    }

    public static bool? ParseBoolean(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        if (TrueValues.Contains(v)) return true;
        if (FalseValues.Contains(v)) return false;
        return null;
    }

    public static bool IsInteger(string value)
    {
        var v = value.Trim();
        return long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
               || long.TryParse(v, NumberStyles.Integer, CultureInfo.CurrentCulture, out _);
    }

    public static bool IsDecimal(string value)
    {
        var v = value.Trim().Replace(" ", string.Empty);
        return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
               || decimal.TryParse(v, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CR"), out _)
               || decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, out _);
    }

    public static bool TryParseDecimal(string value, out decimal number)
    {
        var v = value.Trim().Replace(" ", string.Empty);
        return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
               || decimal.TryParse(v, NumberStyles.Number, CultureInfo.GetCultureInfo("es-CR"), out number)
               || decimal.TryParse(v, NumberStyles.Number, CultureInfo.CurrentCulture, out number);
    }

    public static bool TryParseDateTime(string value, out DateTime date)
    {
        var v = value.Trim();
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy"
        };

        return DateTime.TryParseExact(v, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
               || DateTime.TryParse(v, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    public static bool IsDateTime(string value) => TryParseDateTime(value, out _);

    public static bool IsDateOnly(string value)
        => TryParseDateTime(value, out var date) && date.TimeOfDay == TimeSpan.Zero;

    private static (int Precision, int Scale) MeasureDecimal(List<string> samples)
    {
        var maxScale = 0;
        var maxPrecision = 1;

        foreach (var sample in samples)
        {
            if (!TryParseDecimal(sample, out var number))
                continue;

            var text = Math.Abs(number).ToString(CultureInfo.InvariantCulture);
            var parts = text.Split('.');
            var intDigits = parts[0].TrimStart('0').Length;
            if (intDigits == 0) intDigits = 1;
            var scale = parts.Length > 1 ? parts[1].TrimEnd('0').Length : 0;
            maxScale = Math.Max(maxScale, scale);
            maxPrecision = Math.Max(maxPrecision, intDigits + scale);
        }

        maxScale = Math.Clamp(maxScale, 0, 10);
        maxPrecision = Math.Clamp(Math.Max(maxPrecision, maxScale + 1), 1, 38);
        return (maxPrecision, maxScale);
    }
}
