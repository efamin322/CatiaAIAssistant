namespace CatiaAIAssistant.Core.Commands
{
    /// <summary>
    /// Allowlist of executable CAD operations (Spec §42). Lookup is case-sensitive and exact:
    /// anything not registered is rejected before any CAD call is made.
    /// </summary>
    public sealed class CadOperationRegistry
    {
        private readonly Dictionary<string, ICadOperationHandler> _handlers = new(StringComparer.Ordinal);

        public CadOperationRegistry Register(ICadOperationHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            string type = handler.Descriptor.OperationType;
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Handler descriptor has no OperationType.", nameof(handler));
            if (!_handlers.TryAdd(type, handler)) throw new InvalidOperationException($"Operation '{type}' is already registered.");
            return this;
        }

        public bool TryGet(string operationType, out ICadOperationHandler handler) =>
            _handlers.TryGetValue(operationType ?? string.Empty, out handler!);

        public bool IsAllowed(string operationType) => _handlers.ContainsKey(operationType ?? string.Empty);

        public IReadOnlyCollection<CadOperationDescriptor> Catalog => _handlers.Values.Select(h => h.Descriptor).ToList();
    }
}
