using System.Globalization;
using System.Text.RegularExpressions;

namespace CatiaAIAssistant.Core.Text
{
    /// <summary>
    /// Pure, UI/COM-independent text helpers shared by every layer.
    /// Moved from Infrastructure.Catia to Core (audit finding A-02) so that Engineering
    /// no longer depends on the CAD integration layer.
    /// </summary>
    public static class TextNormalizer
    {
        private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";
        private const string ArabicDigits = "٠١٢٣٤٥٦٧٨٩";

        /// <summary>Upper bound accepted for basic block dimensions (mm).</summary>
        public const double MaxBlockDimensionMm = 10000.0;

        /// <summary>Converts Persian and Arabic-Indic digits to ASCII digits; also maps the Persian decimal separator.</summary>
        public static string NormalizeDigits(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int p = PersianDigits.IndexOf(chars[i]);
                if (p >= 0) { chars[i] = (char)('0' + p); continue; }
                int a = ArabicDigits.IndexOf(chars[i]);
                if (a >= 0) { chars[i] = (char)('0' + a); continue; }
                if (chars[i] == '٫') chars[i] = '.';
            }
            return new string(chars);
        }

        /// <summary>
        /// Produces a CATIA/file-system safe identifier (ASCII letters, digits, '_' and '-').
        /// Common Persian shape names are transliterated.
        /// </summary>
        public static string SanitizeIdentifier(string? rawName, string prefix = "PART")
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            string normalized = NormalizeDigits(rawName.Trim())
                .Replace("مستطیل", "Rect")
                .Replace("مربع", "Square")
                .Replace("دایره", "Circle")
                .Replace("مثلث", "Triangle")
                .Replace("چندضلعی", "Polygon")
                .Replace("بلوک", "Block")
                .Replace("مکعب", "Block");

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                normalized = normalized.Replace(invalid, '_');
            }

            normalized = Regex.Replace(normalized, @"[^a-zA-Z0-9_\-]", "_");
            normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');

            return string.IsNullOrWhiteSpace(normalized) ? prefix : normalized;
        }

        /// <summary>Extracts every decimal number in the text (after digit normalisation).</summary>
        public static List<double> ExtractNumbers(string? text)
        {
            var result = new List<double>();
            string normalized = NormalizeDigits(text);
            foreach (Match m in Regex.Matches(normalized, @"\d+(?:[.,]\d+)?"))
            {
                if (double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    result.Add(v);
                }
            }
            return result;
        }

        /// <summary>Parses exactly three positive dimensions (L, W, H in mm) from free text.</summary>
        public static bool TryParseBlockDimensions(
            string? promptText,
            out double lengthX,
            out double widthY,
            out double heightZ,
            out string validationError)
        {
            lengthX = 0; widthY = 0; heightZ = 0; validationError = string.Empty;

            string normalized = NormalizeDigits(promptText?.Trim());
            MatchCollection matches = Regex.Matches(normalized, @"\d+(?:[.,]\d+)?");

            if (matches.Count != 3)
            {
                validationError = "دقیقاً سه اندازه (طول، عرض، ارتفاع) وارد کنید.";
                return false;
            }

            double[] dims = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!double.TryParse(matches[i].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out dims[i]))
                {
                    validationError = $"مقدار «{matches[i].Value}» قابل خواندن نیست.";
                    return false;
                }

                if (dims[i] <= 0 || dims[i] > MaxBlockDimensionMm)
                {
                    validationError = "ابعاد باید بین ۰ تا ۱۰,۰۰۰ میلی‌متر باشند.";
                    return false;
                }
            }

            lengthX = dims[0]; widthY = dims[1]; heightZ = dims[2];
            return true;
        }
    }
}
