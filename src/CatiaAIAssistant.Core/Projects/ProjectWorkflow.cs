namespace CatiaAIAssistant.Core.Projects
{
    /// <summary>
    /// A guard that may block a specific stage transition. New rules are added by implementing this interface
    /// and registering it — the workflow engine itself is never edited (ARCHITECTURE_CONTRACT §4).
    /// </summary>
    public interface ITransitionRule
    {
        /// <summary>Returns null when the transition is allowed, otherwise a human-readable reason.</summary>
        string? Check(EngineeringProject project, WorkflowStage from, WorkflowStage to);
    }

    public sealed class TransitionResult
    {
        public bool Allowed => Reasons.Count == 0;
        public WorkflowStage From { get; init; }
        public WorkflowStage To { get; init; }
        public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Rule-based workflow state machine (Spec §03). Forward moves are one stage at a time and must satisfy all rules;
    /// moving back to an earlier stage (rework) is always allowed and resets downstream approvals.
    /// </summary>
    public sealed class ProjectWorkflow
    {
        private readonly IReadOnlyList<ITransitionRule> _rules;

        public ProjectWorkflow(IEnumerable<ITransitionRule> rules) => _rules = rules.ToList();

        /// <summary>Workflow with the built-in safety rules.</summary>
        public static ProjectWorkflow CreateDefault() => new(DefaultTransitionRules.All());

        public TransitionResult Evaluate(EngineeringProject project, WorkflowStage to)
        {
            ArgumentNullException.ThrowIfNull(project);
            var from = project.Stage;
            var reasons = new List<string>();

            if (to == from)
                reasons.Add($"پروژه هم‌اکنون در مرحلهٔ {to} است.");
            else if (to > from && to != from + 1)
                reasons.Add($"پرش از {from} به {to} مجاز نیست؛ مراحل باید به ترتیب طی شوند.");
            // Moving backwards (including reopening a Completed project) is rework and always allowed.

            if (reasons.Count == 0 && to > from)
                foreach (var rule in _rules)
                {
                    string? r = rule.Check(project, from, to);
                    if (!string.IsNullOrWhiteSpace(r)) reasons.Add(r);
                }

            return new TransitionResult { From = from, To = to, Reasons = reasons };
        }

        public TransitionResult TryMoveTo(EngineeringProject project, WorkflowStage to)
        {
            var result = Evaluate(project, to);
            if (!result.Allowed)
            {
                project.AddHistory("TransitionBlocked", $"{result.From} → {to}: {string.Join(" | ", result.Reasons)}");
                return result;
            }

            if (to < project.Stage)
            {
                // Rework: anything downstream must be re-earned.
                project.ExecutionApproved = false;
                if (to < WorkflowStage.Execution) project.ExecutionSucceeded = false;
                project.Version++;
            }

            project.Stage = to;
            project.AddHistory("StageChanged", $"{result.From} → {to}");
            return result;
        }
    }
}
