using System.Globalization;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Text;

namespace CatiaAIAssistant.Core.Units
{
    /// <summary>
    /// Parses "number + unit" text in Persian or English ("۳۰ میلی‌متر", "1500 rpm", "2.5kN", "20 °C").
    /// Never guesses: an unknown unit or a dimension that does not match the expected one is reported,
    /// not silently replaced (Spec §07/§11).
    /// </summary>
    public sealed class QuantityParser
    {
        private static readonly Regex NumberThenUnit = new(
            @"^\s*(?<num>[-+]?(?:\d+(?:[.,]\d+)?|[.,]\d+)(?:[eE][-+]?\d+)?)\s*(?<unit>.*?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly UnitRegistry _registry;

        public QuantityParser(UnitRegistry? registry = null) => _registry = registry ?? UnitRegistry.Default;

        public bool TryParse(string? text, out Quantity quantity, out string error, Dimension? expected = null, Unit? defaultUnit = null)
        {
            quantity = default;
            error = string.Empty;
            string s = TextNormalizer.NormalizeDigits(text ?? string.Empty).Replace('٬', ',');
            var m = NumberThenUnit.Match(s);
            if (!m.Success)
            {
                error = $"مقدار عددی در «{text}» پیدا نشد.";
                return false;
            }

            string numText = m.Groups["num"].Value.Replace(',', '.');
            if (!double.TryParse(numText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            {
                error = $"عدد نامعتبر: «{numText}».";
                return false;
            }

            string unitText = m.Groups["unit"].Value;
            Unit unit;
            if (unitText.Length == 0)
            {
                if (defaultUnit is null)
                {
                    error = $"واحد مقدار «{text}» مشخص نشده است.";
                    return false;
                }
                unit = defaultUnit;
            }
            else if (!_registry.TryResolve(unitText, out unit))
            {
                error = $"واحد ناشناخته: «{unitText}».";
                return false;
            }

            if (expected is { } exp && unit.Dimension != exp)
            {
                error = $"واحد «{unit.Symbol}» از نوع مورد انتظار نیست (انتظار: [{exp}]، دریافت: [{unit.Dimension}]).";
                return false;
            }

            quantity = new Quantity(value, unit);
            return true;
        }

        public Quantity Parse(string text, Dimension? expected = null, Unit? defaultUnit = null) =>
            TryParse(text, out var q, out var err, expected, defaultUnit) ? q : throw new FormatException(err);
    }
}
