using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>
    /// Structured operation log + workspace locations (ARCHITECTURE_CONTRACT §3).
    /// Every module depends on this abstraction, never on the concrete <see cref="OperationLogger"/>.
    /// </summary>
    public interface IOperationLogger
    {
        event Action<OperationLogEntry>? EntryAdded;

        string WorkspaceRoot { get; }
        string PartsOutputDirectory { get; }
        string BackupsDirectory { get; }
        string LogsDirectory { get; }
        string SessionLogFilePath { get; }

        void EnsureWorkspaceDirectories();
        IReadOnlyList<OperationLogEntry> GetEntries();

        void Log(string category, string operationName, OperationState state, string message,
                 LogSeverity severity = LogSeverity.Info, string? technicalDetails = null, string? targetFilePath = null);

        void Info(string message, string category = "System");
        void Running(string operationName, string message, string category = "CAD");
        void Succeeded(string operationName, string message, string? filePath = null, string category = "CAD");
        void Failed(string operationName, string message, string? technicalDetails = null, string category = "CAD");
        void Skipped(string operationName, string message, string category = "CAD");
    }
}
