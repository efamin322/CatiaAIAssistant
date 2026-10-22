using CatiaAIAssistant.Core.Language;
using CatiaAIAssistant.Core.Units;

namespace CatiaAIAssistant.Engineering.Requirements
{
    public enum RequirementCategory
    {
        Functional, Geometric, Dimensional, OperatingCondition, Load, Material, Tolerance, Standard, Acceptance, Constraint
    }

    public enum RequirementSource { User, Assumption, Derived }

    /// <summary>A single, uniquely identified requirement (Spec §08: "REQ-001").</summary>
    public sealed class Requirement
    {
        public string Id { get; init; } = string.Empty;
        public RequirementCategory Category { get; init; }
        public RequirementSource Source { get; init; }
        public string ParameterKey { get; init; } = string.Empty;
        public string Statement { get; init; } = string.Empty;
        public Quantity? Value { get; init; }
        public ValueQualifier Qualifier { get; init; }
        public bool RequiresApproval { get; init; }
        public string SourceText { get; init; } = string.Empty;
    }

    public sealed class RequirementConflict
    {
        public IReadOnlyList<string> RequirementIds { get; init; } = Array.Empty<string>();
        public string Description { get; init; } = string.Empty;
    }

    /// <summary>
    /// Specification document built from a request (Spec §08): problem statement, inputs, constraints,
    /// standards, unknowns, assumptions, risks — every item traceable to an ID. Conflicts are never hidden
    /// and block <see cref="IsReadyForDesign"/>.
    /// </summary>
    public sealed class RequirementSpecification
    {
        public string ProblemType { get; init; } = string.Empty;
        public string ProblemStatement { get; init; } = string.Empty;
        public List<Requirement> Requirements { get; } = new();
        public List<RequirementConflict> Conflicts { get; } = new();
        public List<ClarificationQuestion> OpenQuestions { get; } = new();
        public List<string> Unknowns { get; } = new();
        public List<string> Risks { get; } = new();
        public List<string> Standards { get; } = new();
        public double ExtractionConfidence { get; set; }

        public IEnumerable<Requirement> Assumptions => Requirements.Where(r => r.Source == RequirementSource.Assumption);

        public bool IsReadyForDesign =>
            Conflicts.Count == 0 &&
            !OpenQuestions.Any(q => q.Need == InformationNeed.Required || q.SafetyCritical);

        public Requirement? Find(string parameterKey) => Requirements.FirstOrDefault(r => r.ParameterKey == parameterKey);
    }
}
