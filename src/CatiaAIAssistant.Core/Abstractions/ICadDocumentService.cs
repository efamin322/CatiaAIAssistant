using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>Document-level CAD operations (open / save / backup / rollback).</summary>
    public interface ICadDocumentService
    {
        /// <summary>Opens the file if it is not already open, otherwise activates it.</summary>
        CadOperationResult OpenOrActivateDocument(string filePath);

        /// <summary>Copies an existing file to the workspace backup folder; returns null when the file does not exist.</summary>
        string? CreateBackupIfFileExists(string targetFilePath);

        /// <summary>True when at least one document is open in the CAD session.</summary>
        bool HasOpenDocuments();
    }
}
