using CatiaAIAssistant.Core.Abstractions;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    /// <summary>
    /// The CATIA V5 implementation set of the Core CAD abstractions.
    /// Created once by the application's Composition Root; consumers only see the interfaces.
    /// Supporting another CAD host means adding a sibling module that returns the same interfaces (ARCHITECTURE_CONTRACT §4).
    /// </summary>
    public sealed class CatiaServices
    {
        public ICadConnectionService Connection { get; }
        public ICadDocumentService Documents { get; }
        public IBasicPartService Parts { get; }
        public IDesignExecutionEngine DesignEngine { get; }

        private CatiaServices(ICadConnectionService connection, ICadDocumentService documents, IBasicPartService parts, IDesignExecutionEngine designEngine)
        {
            Connection = connection; Documents = documents; Parts = parts; DesignEngine = designEngine;
        }

        public static CatiaServices Create(IOperationLogger logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            var connection = new CatiaConnectionService(logger);
            var documents = new CatiaDocumentManager(connection, logger);
            var parts = new CatiaBasicPartService(connection, documents, logger);
            var engine = new CatiaMechanicalEngine(connection, documents, parts, logger);
            return new CatiaServices(connection, documents, parts, engine);
        }
    }
}
