namespace CatiaAIAssistant.Core.Commands
{
    public enum OperationRisk
    {
        /// <summary>Creates new files only; nothing existing is touched.</summary>
        Low,
        /// <summary>Creates documents that reference other generated documents (assemblies).</summary>
        Medium,
        /// <summary>Modifies or deletes existing user data. Requires explicit confirmation and backup.</summary>
        High
    }

    public enum OperationOutputKind { Part, Product }

    /// <summary>
    /// Declarative description of an allowed CAD operation (Spec §04 / §42). Only operations present in the
    /// <see cref="CadOperationCatalog"/> may ever be executed — AI or parser output cannot invent new ones.
    /// </summary>
    public sealed class CadOperationDescriptor
    {
        public string OperationType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public IReadOnlyList<string> RequiredInputs { get; init; } = Array.Empty<string>();
        /// <summary>StepIds whose output files this operation consumes (e.g. assemblies).</summary>
        public IReadOnlyList<string> RequiredPrerequisiteOutputs { get; init; } = Array.Empty<string>();
        public OperationOutputKind Output { get; init; } = OperationOutputKind.Part;
        public OperationRisk Risk { get; init; } = OperationRisk.Low;
        /// <summary>True when the effect can be undone by deleting the produced file (no existing data modified).</summary>
        public bool Undoable { get; init; } = true;
        public string FailureConditions { get; init; } = "COM error, update error, or save/verification failure.";
        public string VerificationMethod { get; init; } = "Document created, feature present, clean update, file saved with size > 0.";
    }
}
