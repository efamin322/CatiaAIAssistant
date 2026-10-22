using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Core.Projects
{
    public enum AssumptionApproval { Unconfirmed, Confirmed, Rejected }

    public sealed class ProjectAssumption
    {
        public string Text { get; init; } = string.Empty;
        public AssumptionApproval Approval { get; set; } = AssumptionApproval.Unconfirmed;
    }

    public sealed class ProjectRequirement
    {
        public string Id { get; init; } = string.Empty;
        public string Text { get; init; } = string.Empty;
        public bool IsConstraint { get; init; }
    }

    public sealed class ProjectFileReference
    {
        public string Path { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;   // e.g. Part, Assembly, Backup, Report
    }

    public sealed class ProjectHistoryEntry
    {
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public string Action { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
    }

    /// <summary>
    /// Engineering project aggregate (Spec §03). Pure data — no CAD, no UI. Stage changes go through
    /// <see cref="ProjectWorkflow"/> so transitions are rule-based, never cosmetic labels.
    /// </summary>
    public sealed class EngineeringProject
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string ProblemStatement { get; set; } = string.Empty;
        public string LengthUnit { get; set; } = "mm";
        public int Version { get; internal set; } = 1;
        public WorkflowStage Stage { get; internal set; } = WorkflowStage.Draft;

        public List<ProjectRequirement> Requirements { get; } = new();
        public List<ProjectAssumption> Assumptions { get; } = new();
        public List<ProjectFileReference> Files { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<ProjectHistoryEntry> History { get; } = new();
        public List<string> TestReports { get; } = new();

        /// <summary>The analysed specification (parameters, calculations, CAD plan). Null until Analysis.</summary>
        public EngineeringDesignSpecification? Specification { get; set; }

        /// <summary>Set by the user (or an approval step) once the design has been reviewed for execution.</summary>
        public bool ExecutionApproved { get; set; }

        /// <summary>Set by the execution layer once every CAD step reported success.</summary>
        public bool ExecutionSucceeded { get; set; }

        public bool HasUnconfirmedAssumptions => Assumptions.Any(a => a.Approval == AssumptionApproval.Unconfirmed);

        public void AddHistory(string action, string details = "") =>
            History.Add(new ProjectHistoryEntry { Action = action, Details = details });
    }
}
