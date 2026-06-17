using System.Globalization;

namespace DesktopTextBoard.Services;

public static class GridWeightService
{
    private const double MinWeight = 0.05;
    private const double PixelWeightThreshold = 20;

    private static readonly char[] Separators = { ',', '，', ';', '；', ':', '：', ' ', '\t', '\r', '\n' };

    public static List<double> Parse(string? text, int count, IReadOnlyList<double>? fallback = null)
    {
        count = Math.Clamp(count, 1, 12);
        var parsed = ParseParts(text);
        var source = parsed.Count > 0 ? parsed : fallback;
        return Fit(source, count);
    }

    public static List<double> Fit(IEnumerable<double>? values, int count)
    {
        count = Math.Clamp(count, 1, 12);
        var fitted = values?
            .Where(IsUsable)
            .Select(x => Math.Max(MinWeight, x))
            .ToList()
            ?? new List<double>();

        while (fitted.Count < count)
        {
            fitted.Add(1.0);
        }

        while (fitted.Count > count)
        {
            fitted.RemoveAt(fitted.Count - 1);
        }

        return Compact(fitted);
    }

    public static string Format(IReadOnlyList<double>? weights)
    {
        if (weights is null || weights.Count == 0)
        {
            return "1";
        }

        var fitted = Fit(weights, weights.Count);
        return string.Join(", ", fitted.Select(FormatWeight));
    }

    private static List<double> ParseParts(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<double>();
        }

        return text
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParsePart)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }

    private static double? ParsePart(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return IsUsable(value) ? value : null;
        }

        return null;
    }

    private static List<double> Compact(IReadOnlyList<double> weights)
    {
        if (weights.Count == 0)
        {
            return new List<double> { 1.0 };
        }

        var scale = 1.0;
        var max = weights.Max();
        if (max > PixelWeightThreshold)
        {
            scale = weights.Where(x => x > 0).DefaultIfEmpty(1.0).Min();
        }

        return weights
            .Select(x => Math.Max(MinWeight, x / scale))
            .Select(x => Math.Round(x, 3, MidpointRounding.AwayFromZero))
            .ToList();
    }

    private static string FormatWeight(double weight)
    {
        return Math.Abs(weight - Math.Round(weight)) < 0.001
            ? ((int)Math.Round(weight)).ToString(CultureInfo.InvariantCulture)
            : weight.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool IsUsable(double value)
    {
        return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
