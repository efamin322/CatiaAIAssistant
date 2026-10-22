using CatiaAIAssistant.Core.Units;

namespace CatiaAIAssistant.Core.Language
{
    public enum ValueQualifier { Exact, Approximately, AtLeast, AtMost }

    public enum ExtractionIssueKind
    {
        /// <summary>No unit given; the parameter's default unit was applied and must be confirmed.</summary>
        DefaultUnitAssumed,
        /// <summary>The unit does not match the parameter dimension (e.g. "torque 50 mm").</summary>
        UnitDimensionMismatch,
        /// <summary>The same parameter was given more than once with different values.</summary>
        ConflictingValues,
        /// <summary>Value outside the allowed domain (non-positive, non-integer tooth count, …).</summary>
        InvalidValue,
        /// <summary>A number with a unit that is not attached to any named parameter.</summary>
        UnassignedQuantity,
        /// <summary>A bare number not attached to any named parameter.</summary>
        UnassignedNumber
    }

    public sealed class ExtractionIssue
    {
        public ExtractionIssueKind Kind { get; init; }
        public string ParameterKey { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        /// <summary>True when the issue must be resolved by the user before design proceeds.</summary>
        public bool Blocking { get; init; }
    }

    public sealed class ExtractedParameter
    {
        public string Key { get; init; } = string.Empty;
        public ParameterDefinition Definition { get; init; } = new();
        public Quantity Value { get; init; }
        public bool UnitWasExplicit { get; init; }
        public ValueQualifier Qualifier { get; init; }
        /// <summary>The exact source fragment, for traceability (Spec §10).</summary>
        public string SourceText { get; init; } = string.Empty;
        public int SourceIndex { get; init; }
        /// <summary>0..1 confidence of this individual extraction.</summary>
        public double Confidence { get; init; }
    }

    public sealed class UnassignedValue
    {
        public double Number { get; init; }
        public Unit? Unit { get; init; }
        public string SourceText { get; init; } = string.Empty;
        public int SourceIndex { get; init; }
    }

    /// <summary>Structured result of parsing one user request (Spec §06).</summary>
    public sealed class ExtractionResult
    {
        public string RawText { get; init; } = string.Empty;
        public string NormalizedText { get; init; } = string.Empty;
        public List<ExtractedParameter> Parameters { get; } = new();
        public List<UnassignedValue> Unassigned { get; } = new();
        public List<string> Standards { get; } = new();
        public List<ExtractionIssue> Issues { get; } = new();

        public bool Has(string key) => Parameters.Any(p => p.Key == key);
        public ExtractedParameter? Get(string key) => Parameters.FirstOrDefault(p => p.Key == key);

        /// <summary>Value of a parameter in a requested unit, or null when absent.</summary>
        public double? ValueIn(string key, Unit unit) => Get(key)?.Value.ValueIn(unit);

        public bool HasBlockingIssues => Issues.Any(i => i.Blocking);

        /// <summary>Overall extraction confidence: mean of parameter confidences, penalised by issues.</summary>
        public double Confidence
        {
            get
            {
                if (Parameters.Count == 0) return 0;
                double c = Parameters.Average(p => p.Confidence);
                c -= 0.15 * Issues.Count(i => i.Blocking);
                c -= 0.05 * Issues.Count(i => !i.Blocking);
                return Math.Clamp(c, 0, 1);
            }
        }
    }
}
