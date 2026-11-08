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

        /// <summary>Number of documents currently open in the CAD session (0 when the CAD host is not running).</summary>
        int GetOpenDocumentCount();

        /// <summary>
        /// Attempts to close <paramref name="documentCount"/> documents of the running session.
        /// Returns one message per document that could not be closed (empty list = all closed).
        /// </summary>
        IReadOnlyList<string> CloseAllDocuments(int documentCount);
    }
}
