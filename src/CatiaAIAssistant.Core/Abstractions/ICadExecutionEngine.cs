using CatiaAIAssistant.Core.Domain;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>
    /// Executes an approved design plan as a sequence of whitelisted CAD operations.
    /// Implementations must never execute an operation that is not in the allowed catalogue
    /// (ARCHITECTURE_CONTRACT §7 / spec §42).
    /// </summary>
    public interface ICadExecutionEngine
    {
        CadOperationResult ExecuteDesignSpecification(EngineeringDesignSpecification spec, string outputDirectory);

        /// <summary>Human-readable inspection report of the active document (or of <paramref name="filePath"/>).</summary>
        string InspectActiveOrSelectedDocument(string? filePath = null);
    }
}
