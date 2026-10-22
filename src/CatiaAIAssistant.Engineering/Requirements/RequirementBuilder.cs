using CatiaAIAssistant.Core.Language;
using CatiaAIAssistant.Core.Units;
using U = CatiaAIAssistant.Core.Units.Units;

namespace CatiaAIAssistant.Engineering.Requirements
{
    /// <summary>A pluggable consistency rule that detects contradictory requirements (Spec §08).</summary>
    public interface IRequirementConsistencyRule
    {
        IEnumerable<RequirementConflict> Check(RequirementSpecification spec);
    }

    /// <summary>
    /// Builds a <see cref="RequirementSpecification"/> from extracted parameters + a problem template:
    /// user values → REQ entries, missing assumable info → explicit assumption REQs, missing required info →
    /// open questions, extraction issues → risks/conflicts, then runs consistency rules.
    /// </summary>
    public sealed class RequirementBuilder
    {
        private readonly ClarificationPlanner _planner = new();
        private readonly List<IRequirementConsistencyRule> _rules;

        public RequirementBuilder(IEnumerable<IRequirementConsistencyRule>? rules = null)
            => _rules = (rules ?? DefaultConsistencyRules.All).ToList();

        public RequirementSpecification Build(ProblemTemplate template, ExtractionResult extraction, IEnumerable<string>? alreadyAsked = null)
        {
            var spec = new RequirementSpecification
            {
                ProblemType = template.ProblemType,
                ProblemStatement = $"{template.PersianTitle}: {extraction.RawText}".Trim(),
                ExtractionConfidence = extraction.Confidence
            };
            spec.Standards.AddRange(extraction.Standards);
            int n = 0;
            string NextId() => $"REQ-{++n:000}";

            spec.Requirements.Add(new Requirement
            {
                Id = NextId(), Category = RequirementCategory.Functional, Source = RequirementSource.User,
                Statement = $"هدف: {template.PersianTitle}", SourceText = extraction.RawText
            });

            foreach (var p in extraction.Parameters)
            {
                string q = p.Qualifier switch
                {
                    ValueQualifier.AtLeast => "حداقل ",
                    ValueQualifier.AtMost => "حداکثر ",
                    ValueQualifier.Approximately => "حدود ",
                    _ => string.Empty
                };
                spec.Requirements.Add(new Requirement
                {
                    Id = NextId(), Category = CategoryOf(p.Definition), Source = RequirementSource.User, ParameterKey = p.Key,
                    Value = p.Value, Qualifier = p.Qualifier, SourceText = p.SourceText,
                    RequiresApproval = !p.UnitWasExplicit && !p.Definition.Dimension.IsDimensionless,
                    Statement = $"{p.Definition.PersianName} = {q}{p.Value.Format()}" + (p.UnitWasExplicit || p.Definition.Dimension.IsDimensionless ? "" : " (واحد فرض‌شده)")
                });
            }

            foreach (var std in extraction.Standards)
                spec.Requirements.Add(new Requirement { Id = NextId(), Category = RequirementCategory.Standard, Source = RequirementSource.User, Statement = $"مطابقت با {std}" });

            var plan = _planner.Plan(template, extraction.Parameters.Select(p => p.Key), alreadyAsked);
            foreach (var a in plan.Assumptions)
                spec.Requirements.Add(new Requirement
                {
                    Id = NextId(), Category = RequirementCategory.Constraint, Source = RequirementSource.Assumption,
                    ParameterKey = a.ParameterKey, Statement = "فرض: " + a.Statement, RequiresApproval = a.RequiresApproval
                });
            spec.OpenQuestions.AddRange(plan.Questions);
            spec.Unknowns.AddRange(plan.Questions.Where(q => q.Need == InformationNeed.Required).Select(q => q.ParameterKey));

            foreach (var issue in extraction.Issues)
            {
                if (issue.Kind == ExtractionIssueKind.ConflictingValues)
                    spec.Conflicts.Add(new RequirementConflict { Description = issue.Message, RequirementIds = IdsOf(spec, issue.ParameterKey) });
                else if (issue.Blocking)
                    spec.Conflicts.Add(new RequirementConflict { Description = issue.Message });
                else if (issue.Kind is ExtractionIssueKind.UnassignedNumber or ExtractionIssueKind.UnassignedQuantity)
                    spec.Risks.Add(issue.Message + " ممکن است بخشی از درخواست نادیده گرفته شده باشد.");
            }

            foreach (var rule in _rules)
                spec.Conflicts.AddRange(rule.Check(spec));

            if (spec.Assumptions.Any(a => a.RequiresApproval))
                spec.Risks.Add("فرض‌های مؤثر بر ایمنی بدون تأیید کاربر پذیرفته نمی‌شوند.");
            return spec;
        }

        private static IReadOnlyList<string> IdsOf(RequirementSpecification spec, string key) =>
            spec.Requirements.Where(r => r.ParameterKey == key).Select(r => r.Id).ToList();

        private static RequirementCategory CategoryOf(ParameterDefinition d)
        {
            if (d.Dimension == Dimension.Length || d.Dimension == Dimension.Angle) return RequirementCategory.Dimensional;
            if (d.Dimension == Dimension.Force || d.Dimension == Dimension.Torque || d.Dimension == Dimension.Power) return RequirementCategory.Load;
            if (d.Dimension == Dimension.AngularVelocity || d.Dimension == Dimension.Temperature || d.Dimension == Dimension.Time) return RequirementCategory.OperatingCondition;
            if (d.Dimension == Dimension.Stress) return RequirementCategory.Material;
            return RequirementCategory.Constraint;
        }
    }

    /// <summary>Built-in consistency rules; add new ones by implementing <see cref="IRequirementConsistencyRule"/>.</summary>
    public static class DefaultConsistencyRules
    {
        public static IReadOnlyList<IRequirementConsistencyRule> All { get; } = new IRequirementConsistencyRule[]
        {
            new RodLongerThanCrank(), new BoreSmallerThanPitch(), new CenterDistanceMatchesGeometry(),
            new StrokeMatchesCrank(), new PowerTorqueSpeedConsistent()
        };

        private static double? V(RequirementSpecification s, string key, Unit unit) =>
            s.Requirements.FirstOrDefault(r => r.ParameterKey == key && r.Source == RequirementSource.User && r.Value.HasValue)?.Value!.Value.ValueIn(unit);

        private static string[] Ids(RequirementSpecification s, params string[] keys) =>
            s.Requirements.Where(r => keys.Contains(r.ParameterKey) && r.Source == RequirementSource.User).Select(r => r.Id).ToArray();

        private sealed class RodLongerThanCrank : IRequirementConsistencyRule
        {
            public IEnumerable<RequirementConflict> Check(RequirementSpecification s)
            {
                if (V(s, "CrankRadius", U.Millimeter) is double r && V(s, "RodLength", U.Millimeter) is double l && l <= r)
                    yield return new RequirementConflict { RequirementIds = Ids(s, "CrankRadius", "RodLength"), Description = $"طول شاتون ({l} mm) باید از شعاع لنگ ({r} mm) بزرگ‌تر باشد." };
            }
        }

        private sealed class BoreSmallerThanPitch : IRequirementConsistencyRule
        {
            public IEnumerable<RequirementConflict> Check(RequirementSpecification s)
            {
                if (V(s, "Module", U.Millimeter) is double m && V(s, "Teeth", U.Unitless) is double z && V(s, "BoreDiameter", U.Millimeter) is double b)
                {
                    double df = m * (z - 2.5);
                    if (b >= df)
                        yield return new RequirementConflict { RequirementIds = Ids(s, "Module", "Teeth", "BoreDiameter"), Description = $"قطر سوراخ ({b} mm) باید از قطر ریشهٔ دندانه ({df:0.###} mm) کوچک‌تر باشد." };
                }
            }
        }

        private sealed class CenterDistanceMatchesGeometry : IRequirementConsistencyRule
        {
            public IEnumerable<RequirementConflict> Check(RequirementSpecification s)
            {
                if (V(s, "Module", U.Millimeter) is double m && V(s, "Teeth", U.Unitless) is double z1 &&
                    V(s, "GearRatio", U.Unitless) is double i && V(s, "CenterDistance", U.Millimeter) is double a)
                {
                    double z2 = Math.Round(z1 * i);
                    double aCalc = m * (z1 + z2) / 2.0;
                    if (Math.Abs(aCalc - a) > 1e-6 * Math.Max(1, a))
                        yield return new RequirementConflict
                        {
                            RequirementIds = Ids(s, "Module", "Teeth", "GearRatio", "CenterDistance"),
                            Description = $"فاصلهٔ محورهای خواسته‌شده ({a} mm) با m(z1+z2)/2 = {aCalc:0.###} mm سازگار نیست (بدون اصلاح پروفیل)."
                        };
                }
            }
        }

        private sealed class StrokeMatchesCrank : IRequirementConsistencyRule
        {
            public IEnumerable<RequirementConflict> Check(RequirementSpecification s)
            {
                if (V(s, "CrankRadius", U.Millimeter) is double r && V(s, "Stroke", U.Millimeter) is double st && Math.Abs(2 * r - st) > 1e-9 * Math.Max(1, st))
                    yield return new RequirementConflict { RequirementIds = Ids(s, "CrankRadius", "Stroke"), Description = $"کورس ({st} mm) باید دو برابر شعاع لنگ ({r} mm) باشد." };
            }
        }

        private sealed class PowerTorqueSpeedConsistent : IRequirementConsistencyRule
        {
            /// <summary>Relative tolerance; P = T·ω given separately must agree within 2 %.</summary>
            public const double Tolerance = 0.02;

            public IEnumerable<RequirementConflict> Check(RequirementSpecification s)
            {
                if (V(s, "Power", U.Watt) is double p && V(s, "Torque", U.NewtonMeter) is double t && V(s, "Speed", U.RadianPerSecond) is double w)
                {
                    double pCalc = t * w;
                    if (Math.Abs(pCalc - p) > Tolerance * Math.Max(p, pCalc))
                        yield return new RequirementConflict
                        {
                            RequirementIds = Ids(s, "Power", "Torque", "Speed"),
                            Description = $"توان، گشتاور و دور با رابطهٔ P = T·ω سازگار نیستند: T·ω = {pCalc / 1000:0.###} kW اما توان {p / 1000:0.###} kW داده شده است."
                        };
                }
            }
        }
    }
}
