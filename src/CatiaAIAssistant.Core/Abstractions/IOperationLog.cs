using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>
    /// Logging + workspace contract (ARCHITECTURE_CONTRACT §3). Consumers depend on this,
    /// never on the concrete <see cref="OperationLogger"/>.
    /// </summary>
    public interface IOperationLog
    {
        event Action<OperationLogEntry>? EntryAdded;

        string WorkspaceRoot { get; }
        string PartsOutputDirectory { get; }
        string BackupsDirectory { get; }
        string LogsDirectory { get; }
        string SessionLogFilePath { get; }

        void EnsureWorkspaceDirectories();
        IReadOnlyList<OperationLogEntry> GetEntries();

        void Log(
            string category,
            string operationName,
            OperationState state,
            string message,
            LogSeverity severity = LogSeverity.Info,
            string? technicalDetails = null,
            string? targetFilePath = null);

        void Info(string message, string category = "System");
        void Running(string operationName, string message, string category = "CAD");
        void Succeeded(string operationName, string message, string? filePath = null, string category = "CAD");
        void Failed(string operationName, string message, string? technicalDetails = null, string category = "CAD");
        void Skipped(string operationName, string message, string category = "CAD");
    }
}
