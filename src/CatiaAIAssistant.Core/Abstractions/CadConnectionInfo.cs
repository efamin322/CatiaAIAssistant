namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>CAD-neutral snapshot of the connection state to the CAD host (e.g. CATIA V5).</summary>
    public sealed class CadConnectionInfo
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
