using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>Connection to the CAD host. Implementations live in Infrastructure.* (ARCHITECTURE_CONTRACT §2/§3).</summary>
    public interface ICadConnectionService
    {
        /// <summary>When false (default) the application never starts the CAD host by itself.</summary>
        bool AllowAutoLaunchIfClosed { get; set; }
        CadConnectionInfo CheckConnection();
    }

    /// <summary>Result of a bulk close request; failures are never swallowed (audit A-08).</summary>
    public sealed class CloseDocumentsResult
    {
        public int RequestedCount { get; init; }
        public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
        public bool AllClosed => Failures.Count == 0;
    }

    /// <summary>Document lifecycle operations on the CAD host.</summary>
    public interface ICadDocumentService
    {
        CadOperationResult OpenOrActivateDocument(string filePath);
        string? CreateBackupIfFileExists(string targetFilePath);
        int GetOpenDocumentCount();
        CloseDocumentsResult CloseAllDocuments();
    }

    /// <summary>Primitive part creation (sketch + pad).</summary>
    public interface IBasicPartService
    {
        CadOperationResult CreateBlockPart(double x, double y, double z, string outputPath, bool verifyReopen = false);
        CadOperationResult CreateShapePart(string shapeType, double firstDimension, double secondDimension, int polygonSides,
                                           double padHeight, string outputPath, bool verifyReopen = false);
    }

    /// <summary>Executes a validated engineering specification as CAD operations and inspects documents.</summary>
    public interface IDesignExecutionEngine
    {
        CadOperationResult ExecuteDesignSpecification(EngineeringDesignSpecification spec, string outputDirectory);
        string InspectActiveOrSelectedDocument(string? filePath = null);
    }

    /// <summary>Turns a natural-language request into a structured, validated engineering specification.</summary>
    public interface IEngineeringIntentAnalyzer
    {
        EngineeringDesignSpecification AnalyzeUserRequest(string rawPrompt);
    }
}
