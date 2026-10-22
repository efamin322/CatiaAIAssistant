using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Diagnostics;
using CatiaAIAssistant.Engineering;
using CatiaAIAssistant.Infrastructure.Catia;

namespace CatiaAIAssistant
{
    /// <summary>Resolved application services. The UI only receives this bundle of interfaces.</summary>
    public sealed class AppServices
    {
        public required IOperationLogger Logger { get; init; }
        public required ICadConnectionService Connection { get; init; }
        public required ICadDocumentService Documents { get; init; }
        public required IBasicPartService Parts { get; init; }
        public required IDesignExecutionEngine DesignEngine { get; init; }
        public required IEngineeringIntentAnalyzer IntentAnalyzer { get; init; }
        public required Phase1SelfTestRunner SelfTests { get; init; }
    }

    /// <summary>
    /// Composition Root (ARCHITECTURE_CONTRACT §2.4 / §3.3): the ONLY place where concrete implementations are
    /// chosen and wired together. Swapping the CAD host, the intent analyzer or the logger is done here only.
    /// </summary>
    public static class AppComposition
    {
        public static AppServices Build()
        {
            IOperationLogger logger = new OperationLogger();
            var catia = CatiaServices.Create(logger);
            IEngineeringIntentAnalyzer intent = new EngineeringIntentAndCalculationEngine();

            return new AppServices
            {
                Logger = logger,
                Connection = catia.Connection,
                Documents = catia.Documents,
                Parts = catia.Parts,
                DesignEngine = catia.DesignEngine,
                IntentAnalyzer = intent,
                SelfTests = new Phase1SelfTestRunner(logger, catia.Connection, catia.Documents, catia.Parts, intent)
            };
        }
    }
}
