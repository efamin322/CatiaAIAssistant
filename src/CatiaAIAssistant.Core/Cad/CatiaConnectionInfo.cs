namespace CatiaAIAssistant.Core.Cad
{
    /// <summary>
    /// Snapshot of the CAD session state. Lives in Core so that callers can depend on
    /// <see cref="ICadConnection"/> without referencing the COM integration layer.
    /// </summary>
    public sealed class CatiaConnectionInfo
    {
        public bool IsConnected { get; init; }
        public string ApplicationName { get; init; } = string.Empty;
        public int OpenDocumentsCount { get; init; }
        public string ActiveDocumentName { get; init; } = "بدون سند فعال";
        public bool AttachedToExistingInstance { get; init; }
        public string StatusMessage { get; init; } = string.Empty;
        public string? ErrorDetails { get; init; }
    }
}
