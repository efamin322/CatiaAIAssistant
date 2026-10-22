using System.Globalization;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Units;

namespace CatiaAIAssistant.Core.Language
{
    /// <summary>
    /// Deterministic, offline extraction of named, unit-bearing parameters from Persian/English requests
    /// (Spec §06–§07). Numbers are bound to the parameter name that precedes them ("ماژول 3", "torque = 50 N·m")
    /// or, when directly adjacent, follows them ("24 دندانه", "24 teeth"). Nothing is guessed silently:
    /// default units, unassigned numbers, unit/dimension mismatches and conflicting values are reported as issues.
    /// </summary>
    public sealed class ParameterExtractor
    {
        private static readonly Regex NumberRx = new(
            @"(?<![\p{L}\d.,])(?<sign>-)?(?<num>\d+(?:[.,]\d+)?(?:[eE][-+]?\d+)?)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex StandardRx = new(
            @"\b(?<std>(?:ISO|DIN|AGMA|ANSI|ASME|JIS|BS|EN|ISIRI)\s*[-]?\s*\d+(?:[-:]\d+)*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly (string Text, ValueQualifier Q)[] QualifierWords =
        {
            ("حداقل", ValueQualifier.AtLeast), ("دست کم", ValueQualifier.AtLeast), ("بیشتر از", ValueQualifier.AtLeast),
            ("at least", ValueQualifier.AtLeast), ("min", ValueQualifier.AtLeast), (">=", ValueQualifier.AtLeast), ("≥", ValueQualifier.AtLeast),
            ("حداکثر", ValueQualifier.AtMost), ("کمتر از", ValueQualifier.AtMost), ("at most", ValueQualifier.AtMost),
            ("max", ValueQualifier.AtMost), ("<=", ValueQualifier.AtMost), ("≤", ValueQualifier.AtMost),
            ("حدود", ValueQualifier.Approximately), ("تقریبا", ValueQualifier.Approximately), ("تقریباً", ValueQualifier.Approximately),
            ("about", ValueQualifier.Approximately), ("approximately", ValueQualifier.Approximately), ("approx", ValueQualifier.Approximately),
            ("around", ValueQualifier.Approximately), ("~", ValueQualifier.Approximately), ("≈", ValueQualifier.Approximately),
        };

        /// <summary>Max characters between a parameter name and its number ("ماژول برابر با 3").</summary>
        public const int MaxPrefixGap = 24;

        private readonly ParameterVocabulary _vocabulary;
        private readonly UnitRegistry _units;

        public ParameterExtractor(ParameterVocabulary? vocabulary = null, UnitRegistry? units = null)
        {
            _vocabulary = vocabulary ?? ParameterVocabulary.Default;
            _units = units ?? UnitRegistry.Default;
        }

        private sealed record NumberToken(int Start, int End, double Value, string Raw);
        private sealed record NameToken(int Start, int End, ParameterDefinition Def) { public bool Used { get; set; } }

        public ExtractionResult Extract(string? rawText)
        {
            string text = LanguageText.Normalize(rawText, preserveCase: true);
            string lower = text.ToLowerInvariant();
            var result = new ExtractionResult { RawText = rawText ?? string.Empty, NormalizedText = text };
            if (text.Length == 0) return result;

            var blocked = new bool[text.Length];
            foreach (Match m in StandardRx.Matches(text))
            {
                result.Standards.Add(Regex.Replace(m.Groups["std"].Value.ToUpperInvariant(), @"\s+", " "));
                for (int i = m.Index; i < m.Index + m.Length; i++) blocked[i] = true;
            }

            var names = FindNames(lower);
            var numbers = new List<NumberToken>();
            foreach (Match m in NumberRx.Matches(text))
            {
                if (blocked[m.Index]) continue;
                if (!double.TryParse(m.Groups["num"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) continue;
                bool negative = m.Groups["sign"].Success && (m.Index == 0 || char.IsWhiteSpace(text[m.Index - 1]) || "=:".Contains(text[m.Index - 1]));
                int start = negative ? m.Index : m.Groups["num"].Index;
                numbers.Add(new NumberToken(start, m.Index + m.Length, negative ? -v : v, m.Value));
            }

            foreach (var num in numbers)
            {
                var unitMatch = MatchUnitAfter(text, num.End);
                NameToken? name = FindPrefixName(names, numbers, num, lower);
                bool postfix = false;
                if (name is null)
                {
                    name = FindPostfixName(names, unitMatch?.End ?? num.End, lower);
                    postfix = name is not null;
                }

                if (name is null)
                {
                    var unassigned = new UnassignedValue
                    {
                        Number = num.Value, Unit = unitMatch?.Unit, SourceIndex = num.Start,
                        SourceText = text.Substring(num.Start, (unitMatch?.End ?? num.End) - num.Start)
                    };
                    result.Unassigned.Add(unassigned);
                    result.Issues.Add(new ExtractionIssue
                    {
                        Kind = unitMatch is null ? ExtractionIssueKind.UnassignedNumber : ExtractionIssueKind.UnassignedQuantity,
                        Message = $"مقدار «{unassigned.SourceText}» به هیچ پارامتر نام‌داری نسبت داده نشد.",
                        Blocking = false
                    });
                    continue;
                }

                name.Used = true;
                Bind(result, text, name, num, unitMatch, postfix, lower);
            }

            return result;
        }

        private void Bind(ExtractionResult result, string text, NameToken name, NumberToken num, (Unit Unit, int End)? unitMatch, bool postfix, string lower)
        {
            var def = name.Def;
            Unit unit = def.DefaultUnit;
            bool explicitUnit = false;
            int end = num.End;

            if (unitMatch is { } um)
            {
                if (um.Unit.Dimension == def.Dimension) { unit = um.Unit; explicitUnit = true; end = um.End; }
                else if (def.Dimension.IsDimensionless)
                {
                    // e.g. "24 in total" for a tooth count — the word was not a unit here; ignore it.
                }
                else
                {
                    result.Issues.Add(new ExtractionIssue
                    {
                        Kind = ExtractionIssueKind.UnitDimensionMismatch, ParameterKey = def.Key, Blocking = true,
                        Message = $"واحد «{um.Unit.Symbol}» برای «{def.PersianName}» مناسب نیست (انتظار: واحدی از نوع [{def.Dimension}])."
                    });
                    return;
                }
            }

            int srcStart = Math.Min(name.Start, num.Start);
            int srcEnd = Math.Max(name.End, end);
            var param = new ExtractedParameter
            {
                Key = def.Key, Definition = def, Value = new Quantity(num.Value, unit), UnitWasExplicit = explicitUnit,
                Qualifier = DetectQualifier(lower, postfix ? num.Start : name.End, num.Start),
                SourceText = text.Substring(srcStart, srcEnd - srcStart), SourceIndex = srcStart,
                Confidence = postfix ? 0.75 : (explicitUnit || def.Dimension.IsDimensionless ? 0.95 : 0.8)
            };

            if ((def.MustBePositive && num.Value <= 0) || (def.IsInteger && Math.Abs(num.Value - Math.Round(num.Value)) > 1e-9))
            {
                result.Issues.Add(new ExtractionIssue
                {
                    Kind = ExtractionIssueKind.InvalidValue, ParameterKey = def.Key, Blocking = true,
                    Message = def.IsInteger
                        ? $"«{def.PersianName}» باید عدد صحیح مثبت باشد (مقدار داده‌شده: {num.Raw})."
                        : $"«{def.PersianName}» باید بزرگ‌تر از صفر باشد (مقدار داده‌شده: {num.Value.ToString(CultureInfo.InvariantCulture)})."
                });
                return;
            }

            var existing = result.Get(def.Key);
            if (existing is not null)
            {
                if (!existing.Value.ApproximatelyEquals(param.Value, 1e-12))
                    result.Issues.Add(new ExtractionIssue
                    {
                        Kind = ExtractionIssueKind.ConflictingValues, ParameterKey = def.Key, Blocking = true,
                        Message = $"برای «{def.PersianName}» دو مقدار متفاوت داده شده است: {existing.Value.Format()} و {param.Value.Format()}."
                    });
                return;
            }

            if (!explicitUnit && !def.Dimension.IsDimensionless)
                result.Issues.Add(new ExtractionIssue
                {
                    Kind = ExtractionIssueKind.DefaultUnitAssumed, ParameterKey = def.Key, Blocking = false,
                    Message = $"واحد «{def.PersianName}» ذکر نشده بود؛ «{def.DefaultUnit.Symbol}» فرض شد و نیاز به تأیید دارد."
                });

            result.Parameters.Add(param);
        }

        /// <summary>Longest-match, non-overlapping occurrences of parameter names, on word boundaries.</summary>
        private List<NameToken> FindNames(string lower)
        {
            var taken = new bool[lower.Length];
            var found = new List<NameToken>();
            foreach (var (alias, def) in _vocabulary.Aliases.OrderByDescending(a => a.Alias.Length))
            {
                int idx = 0;
                while ((idx = lower.IndexOf(alias, idx, StringComparison.Ordinal)) >= 0)
                {
                    int end = idx + alias.Length;
                    bool boundary = (idx == 0 || !char.IsLetterOrDigit(lower[idx - 1])) && (end >= lower.Length || !char.IsLetterOrDigit(lower[end]) || IsPersianSuffix(lower, end));
                    if (boundary && !Enumerable.Range(idx, alias.Length).Any(i => taken[i]))
                    {
                        for (int i = idx; i < end; i++) taken[i] = true;
                        found.Add(new NameToken(idx, end, def));
                    }
                    idx = end;
                }
            }
            found.Sort((a, b) => a.Start.CompareTo(b.Start));
            return found;
        }

        /// <summary>Persian plural/ezafe suffixes that may follow a name ("دندانه‌ها", "قطرِ", "شفتی").</summary>
        private static bool IsPersianSuffix(string s, int end)
        {
            string rest = s.Substring(end);
            return rest.StartsWith("ها") || rest.StartsWith(" ها") || rest.StartsWith("ی ") || rest == "ی" || rest.StartsWith("ِ");
        }

        private static NameToken? FindPrefixName(List<NameToken> names, List<NumberToken> numbers, NumberToken num, string lower)
        {
            var candidate = names.LastOrDefault(n => !n.Used && n.End <= num.Start);
            if (candidate is null) return null;
            if (num.Start - candidate.End > MaxPrefixGap) return null;
            // another number or name between the name and this number breaks the binding
            if (numbers.Any(o => o.Start >= candidate.End && o.Start < num.Start)) return null;
            string gap = lower.Substring(candidate.End, num.Start - candidate.End);
            if (gap.Contains('،') || gap.Contains(',') || gap.Contains(';') || gap.Contains('.')) return null;
            return candidate;
        }

        private static NameToken? FindPostfixName(List<NameToken> names, int afterIndex, string lower)
        {
            var candidate = names.FirstOrDefault(n => !n.Used && n.Start >= afterIndex);
            if (candidate is null) return null;
            string gap = lower.Substring(afterIndex, candidate.Start - afterIndex);
            return gap.Trim().Length == 0 && gap.Length <= 2 ? candidate : null;
        }

        /// <summary>Longest unit alias (up to four tokens) starting right after a number.</summary>
        private (Unit Unit, int End)? MatchUnitAfter(string text, int index)
        {
            int i = index;
            while (i < text.Length && text[i] == ' ') i++;
            if (i >= text.Length) return null;
            if (i - index > 1) return null;

            var ends = new List<int>();
            int p = i;
            for (int tok = 0; tok < 4 && p < text.Length; tok++)
            {
                while (p < text.Length && !char.IsWhiteSpace(text[p])) p++;
                ends.Add(p);
                while (p < text.Length && text[p] == ' ') p++;
            }

            for (int k = ends.Count - 1; k >= 0; k--)
            {
                string cand = text.Substring(i, ends[k] - i).TrimEnd(',', '،', ';', ':', ')', '(', '.', '؛');
                if (cand.Length == 0) continue;
                if (_units.TryResolve(cand, out var unit)) return (unit, i + cand.Length);
            }
            return null;
        }

        private static ValueQualifier DetectQualifier(string lower, int from, int to)
        {
            int start = Math.Max(0, Math.Min(from, to) - 14);
            string window = lower.Substring(start, Math.Max(0, to - start));
            foreach (var (t, q) in QualifierWords)
            {
                int k = window.LastIndexOf(t, StringComparison.Ordinal);
                if (k < 0) continue;
                bool wordStart = k == 0 || !char.IsLetter(window[k - 1]) || !char.IsLetter(t[0]);
                bool wordEnd = k + t.Length >= window.Length || !char.IsLetter(window[k + t.Length]) || !char.IsLetter(t[^1]);
                if (wordStart && wordEnd) return q;
            }
            return ValueQualifier.Exact;
        }
    }
}
