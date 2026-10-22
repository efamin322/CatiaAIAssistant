using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Core.Commands
{
    /// <summary>Everything a handler needs to execute one plan step.</summary>
    public sealed class CadStepContext
    {
        public CadExecutionStep Step { get; init; } = null!;
        public string TargetPath { get; init; } = string.Empty;
        /// <summary>StepId → output file of already executed steps.</summary>
        public IReadOnlyDictionary<string, string> ExecutedFiles { get; init; } = new Dictionary<string, string>();

        public double Input(string key) =>
            Step.NumericInputs.TryGetValue(key, out var v) ? v
                : throw new KeyNotFoundException($"ورودی «{key}» در گام «{Step.StepId}» وجود ندارد.");

        public string OutputOf(string stepId) =>
            ExecutedFiles.TryGetValue(stepId, out var f) ? f
                : throw new KeyNotFoundException($"خروجی گام «{stepId}» برای گام «{Step.StepId}» در دسترس نیست.");
    }

    /// <summary>
    /// Extension point for CAD operations (ARCHITECTURE_CONTRACT §4). A new operation = a new handler + its descriptor;
    /// the executor is never edited. Implementations live in Infrastructure.* projects.
    /// </summary>
    public interface ICadOperationHandler
    {
        CadOperationDescriptor Descriptor { get; }
        CadOperationResult Execute(CadStepContext context);
    }
}
