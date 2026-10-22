using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CatiaAIAssistant.Core.Logging
{
    public sealed class OperationLogger
    {
        private readonly List<OperationLogEntry> _entries = new();
        private readonly object _syncLock = new();
        private readonly string _workspaceRoot;
        private readonly string _logDirectory;
        private readonly string _sessionLogFilePath;

        public event Action<OperationLogEntry>? EntryAdded;

        public string WorkspaceRoot => _workspaceRoot;
        public string PartsOutputDirectory => Path.Combine(_workspaceRoot, "Parts");
        public string BackupsDirectory => Path.Combine(_workspaceRoot, "Backups");
        public string LogsDirectory => _logDirectory;
        public string SessionLogFilePath => _sessionLogFilePath;

        public OperationLogger()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _workspaceRoot = Path.Combine(documents, "CATIA_AI_Workspace");
            _logDirectory = Path.Combine(_workspaceRoot, "Logs");

            EnsureWorkspaceDirectories();
            _sessionLogFilePath = Path.Combine(_logDirectory, $"Session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        public void EnsureWorkspaceDirectories()
        {
            try
            {
                Directory.CreateDirectory(_workspaceRoot);
                Directory.CreateDirectory(PartsOutputDirectory);
                Directory.CreateDirectory(BackupsDirectory);
                Directory.CreateDirectory(_logDirectory);
            }
            catch
            {
            }
        }

        public IReadOnlyList<OperationLogEntry> GetEntries()
        {
            lock (_syncLock)
            {
                return _entries.ToArray();
            }
        }

        public void Log(
            string category,
            string operationName,
            OperationState state,
            string message,
            LogSeverity severity = LogSeverity.Info,
            string? technicalDetails = null,
            string? targetFilePath = null)
        {
            var entry = new OperationLogEntry
            {
                Category = category,
                OperationName = operationName,
                State = state,
                Severity = severity,
                Message = message,
                TechnicalDetails = technicalDetails,
                TargetFilePath = targetFilePath
            };

            lock (_syncLock)
            {
                _entries.Add(entry);
                WriteToDiskSafe(entry);
            }

            EntryAdded?.Invoke(entry);
        }

        public void Info(string message, string category = "System")
        {
            Log(category, category, OperationState.Succeeded, message, LogSeverity.Info);
        }

        public void Running(string operationName, string message, string category = "CAD")
        {
            Log(category, operationName, OperationState.Running, message, LogSeverity.Info);
        }

        public void Succeeded(string operationName, string message, string? filePath = null, string category = "CAD")
        {
            Log(category, operationName, OperationState.Succeeded, message, LogSeverity.Verification, null, filePath);
        }

        public void Failed(string operationName, string message, string? technicalDetails = null, string category = "CAD")
        {
            Log(category, operationName, OperationState.Failed, message, LogSeverity.Error, technicalDetails);
        }

        public void Skipped(string operationName, string message, string category = "CAD")
        {
            Log(category, operationName, OperationState.Skipped, message, LogSeverity.Warning);
        }

        private void WriteToDiskSafe(OperationLogEntry entry)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(entry.ToDisplayLine());
                if (!string.IsNullOrWhiteSpace(entry.TechnicalDetails))
                {
                    sb.AppendLine($"    جزئیات فنی: {entry.TechnicalDetails}");
                }
                if (!string.IsNullOrWhiteSpace(entry.TargetFilePath))
                {
                    sb.AppendLine($"    مسیر فایل: {entry.TargetFilePath}");
                }

                File.AppendAllText(_sessionLogFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}