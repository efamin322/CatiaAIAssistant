using System;
using System.Collections.Generic;

namespace CatiaAIAssistant.Core.Logging
{
    public enum OperationState
    {
        Pending,
        Running,
        Succeeded,
        Failed,
        Skipped
    }

    public enum LogSeverity
    {
        Info,
        Warning,
        Error,
        Verification
    }

    public sealed class OperationLogEntry
    {
        public Guid OperationId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string Category { get; init; } = "General";
        public string OperationName { get; init; } = string.Empty;
        public OperationState State { get; init; } = OperationState.Pending;
        public LogSeverity Severity { get; init; } = LogSeverity.Info;
        public string Message { get; init; } = string.Empty;
        public string? TechnicalDetails { get; init; }
        public string? TargetFilePath { get; init; }

        public string ToDisplayLine()
        {
            string stateTag = State switch
            {
                OperationState.Pending => "PENDING",
                OperationState.Running => "RUNNING",
                OperationState.Succeeded => "SUCCEEDED",
                OperationState.Failed => "FAILED",
                OperationState.Skipped => "SKIPPED",
                _ => "INFO"
            };

            return $"[{Timestamp:HH:mm:ss}] [{stateTag}] [{Category}] {Message}";
        }
    }

    public sealed class CadOperationVerification
    {
        public bool DocumentCreated { get; set; }
        public bool FeatureExistsInTree { get; set; }
        public bool GeometryUpdatedCleanly { get; set; }
        public bool FileSavedOnDisk { get; set; }
        public bool FileReopenVerified { get; set; }
        public long SavedFileSizeBytes { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public List<string> CreatedFeatures { get; } = new();
        public List<string> VerificationNotes { get; } = new();

        public bool IsFullyVerified =>
            DocumentCreated &&
            FeatureExistsInTree &&
            GeometryUpdatedCleanly &&
            FileSavedOnDisk &&
            SavedFileSizeBytes > 0;
    }

    public sealed class CadOperationResult
    {
        public Guid OperationId { get; init; } = Guid.NewGuid();
        public string OperationName { get; init; } = string.Empty;
        public OperationState FinalState { get; set; } = OperationState.Pending;
        public string SummaryMessage { get; set; } = string.Empty;
        public string? ErrorDetails { get; set; }
        public string? OutputFilePath { get; set; }
        public string? BackupFilePath { get; set; }
        public CadOperationVerification Verification { get; } = new();

        public static CadOperationResult Failure(string operationName, string summary, string? technicalDetails = null)
        {
            return new CadOperationResult
            {
                OperationName = operationName,
                FinalState = OperationState.Failed,
                SummaryMessage = summary,
                ErrorDetails = technicalDetails
            };
        }
    }
}