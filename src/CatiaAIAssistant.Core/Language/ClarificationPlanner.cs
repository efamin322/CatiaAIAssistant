namespace CatiaAIAssistant.Core.Language
{
    public enum InformationNeed
    {
        /// <summary>Design cannot proceed without it.</summary>
        Required,
        /// <summary>A documented default may be assumed, but must be recorded and shown to the user.</summary>
        Assumable,
        /// <summary>Improves the result; never blocks.</summary>
        Optional
    }

    /// <summary>One piece of information a problem type needs (Spec §07).</summary>
    public sealed class InformationItem
    {
        public string ParameterKey { get; init; } = string.Empty;
        public string Question { get; init; } = string.Empty;
        public InformationNeed Need { get; init; }
        /// <summary>1 = highest engineering importance.</summary>
        public int Priority { get; init; } = 5;
        /// <summary>How the answer changes the design (Spec §07.4).</summary>
        public string DesignEffect { get; init; } = string.Empty;
        /// <summary>Safety-relevant items may never be assumed without explicit approval (Spec §07.7).</summary>
        public bool SafetyCritical { get; init; }
        /// <summary>Default used when Need == Assumable (human-readable, with unit).</summary>
        public string? DefaultAssumption { get; init; }
        /// <summary>How the user can measure or estimate the value if unknown (Spec §07.9).</summary>
        public string? HowToObtain { get; init; }
        /// <summary>Alternative parameter keys that satisfy the same need (e.g. Power instead of Torque).</summary>
        public IReadOnlyList<string> SatisfiedBy { get; init; } = Array.Empty<string>();
    }

    /// <summary>The information profile of one problem type (e.g. shaft design).</summary>
    public sealed class ProblemTemplate
    {
        public string ProblemType { get; init; } = string.Empty;
        public string PersianTitle { get; init; } = string.Empty;
        public IReadOnlyList<InformationItem> Items { get; init; } = Array.Empty<InformationItem>();
    }

    public sealed class ClarificationQuestion
    {
        public string ParameterKey { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public InformationNeed Need { get; init; }
        public int Priority { get; init; }
        public string DesignEffect { get; init; } = string.Empty;
        public bool SafetyCritical { get; init; }
        public string? HowToObtain { get; init; }
    }

    public sealed class RecordedAssumption
    {
        public string ParameterKey { get; init; } = string.Empty;
        public string Statement { get; init; } = string.Empty;
        /// <summary>Safety-critical assumptions stay unapproved until the user confirms them.</summary>
        public bool RequiresApproval { get; init; }
    }

    public sealed class ClarificationPlan
    {
        public List<ClarificationQuestion> Questions { get; } = new();
        public List<RecordedAssumption> Assumptions { get; } = new();
        public List<string> AlreadyProvided { get; } = new();
        /// <summary>True when no Required (or safety-critical) item is missing.</summary>
        public bool CanProceed => !Questions.Any(q => q.Need == InformationNeed.Required || q.SafetyCritical);
    }

    /// <summary>
    /// Decides what to ask (Spec §07): never re-asks provided data, never duplicates a question,
    /// orders by engineering importance, separates required from assumable information and refuses
    /// to silently assume safety-critical values.
    /// </summary>
    public sealed class ClarificationPlanner
    {
        public ClarificationPlan Plan(ProblemTemplate template, IEnumerable<string> providedKeys, IEnumerable<string>? alreadyAskedKeys = null)
        {
            var provided = new HashSet<string>(providedKeys, StringComparer.Ordinal);
            var asked = new HashSet<string>(alreadyAskedKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
            var plan = new ClarificationPlan();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in template.Items.OrderBy(i => i.Priority).ThenBy(i => i.Need))
            {
                if (!seen.Add(item.ParameterKey)) continue; // no duplicate questions
                bool satisfied = provided.Contains(item.ParameterKey) || item.SatisfiedBy.Any(provided.Contains);
                if (satisfied) { plan.AlreadyProvided.Add(item.ParameterKey); continue; }

                bool mayAssume = item.Need == InformationNeed.Assumable && item.DefaultAssumption is not null;
                if (mayAssume)
                {
                    plan.Assumptions.Add(new RecordedAssumption
                    {
                        ParameterKey = item.ParameterKey,
                        Statement = item.DefaultAssumption!,
                        RequiresApproval = item.SafetyCritical
                    });
                    if (!item.SafetyCritical) continue; // documented assumption is enough
                }
                else if (item.Need == InformationNeed.Optional)
                {
                    continue;
                }

                if (asked.Contains(item.ParameterKey) && !item.SafetyCritical && item.Need != InformationNeed.Required) continue;

                plan.Questions.Add(new ClarificationQuestion
                {
                    ParameterKey = item.ParameterKey,
                    Text = mayAssume ? $"{item.Question} (پیش‌فرض پیشنهادی: {item.DefaultAssumption} — نیازمند تأیید شما)" : item.Question,
                    Need = item.Need,
                    Priority = item.Priority,
                    DesignEffect = item.DesignEffect,
                    SafetyCritical = item.SafetyCritical,
                    HowToObtain = item.HowToObtain
                });
            }
            return plan;
        }
    }
}
