using CatiaAIAssistant.Core.Text;

namespace CatiaAIAssistant.Core.Language
{
    /// <summary>Normalisation shared by vocabulary and extractor so that both sides compare the same text.</summary>
    public static class LanguageText
    {
        /// <summary>Digits → ASCII, Arabic ي/ك → Persian ی/ک, ZWNJ/tatweel → space, lower-case Latin, collapsed spaces.</summary>
        public static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = TextNormalizer.NormalizeDigits(s)
                .Replace('\u200c', ' ').Replace('\u0640', ' ').Replace('ي', 'ی').Replace('ك', 'ک')
                .Replace('\u00a0', ' ').ToLowerInvariant();
            var sb = new System.Text.StringBuilder(t.Length);
            bool space = false;
            foreach (char c in t)
            {
                if (char.IsWhiteSpace(c)) { if (!space && sb.Length > 0) sb.Append(' '); space = true; }
                else { sb.Append(c); space = false; }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
