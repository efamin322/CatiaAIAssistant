using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Commands
{
    /// <summary>Lightweight handler built from a descriptor and a delegate; keeps one-line operations declarative.</summary>
    public sealed class DelegateCadOperationHandler : ICadOperationHandler
    {
        private readonly Func<CadStepContext, CadOperationResult> _execute;

        public DelegateCadOperationHandler(CadOperationDescriptor descriptor, Func<CadStepContext, CadOperationResult> execute)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public CadOperationDescriptor Descriptor { get; }
        public CadOperationResult Execute(CadStepContext context) => _execute(context);
    }
}
