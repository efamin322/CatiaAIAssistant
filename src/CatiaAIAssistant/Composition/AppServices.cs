using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Diagnostics;
using CatiaAIAssistant.Engineering;
using CatiaAIAssistant.Infrastructure.Catia;

namespace CatiaAIAssistant.Composition
{
    /// <summary>
    /// Composition Root (ARCHITECTURE_CONTRACT §2/§3): the ONLY place where concrete implementations are
    /// instantiated and wired together. Every other class receives its dependencies through the constructor
    /// and depends on interfaces from <c>CatiaAIAssistant.Core.Abstractions</c>.
    /// Adding a new implementation here is the only wiring change a new feature needs.
    /// </summary>
    public sealed class AppServices
    {
        public AppServices(
            IOperationLog logger,
            ICadConnection connection,
            ICadDocumentService documents,
            ICadPartService parts,
            ICadExecutionEngine execution,
            EngineeringIntentAndCalculationEngine intent,
            Phase1SelfTestRunner selfTests)
        {
            Logger = logger;
            Connection = connection;
            Documents = documents;
            Parts = parts;
            Execution = execution;
            Intent = intent;
            SelfTests = selfTests;
        }

        public IOperationLog Logger { get; }
        public ICadConnection Connection { get; }
        public ICadDocumentService Documents { get; }
        public ICadPartService Parts { get; }
        public ICadExecutionEngine Execution { get; }
        public EngineeringIntentAndCalculationEngine Intent { get; }
        public Phase1SelfTestRunner SelfTests { get; }

        /// <summary>Wires the real CATIA/COM implementations used by the desktop application.</summary>
        public static AppServices CreateDefault()
        {
            IOperationLog logger = new OperationLogger();

            var connection = new CatiaConnectionService(logger);
            var documents = new CatiaDocumentManager(connection, logger);
            var parts = new CatiaBasicPartService(connection, documents, logger);
            var execution = new CatiaMechanicalEngine(connection, documents, parts, logger);
            var intent = new EngineeringIntentAndCalculationEngine();
            var selfTests = new Phase1SelfTestRunner(logger, connection, documents, parts);

            return new AppServices(logger, connection, documents, parts, execution, intent, selfTests);
        }
    }
}
