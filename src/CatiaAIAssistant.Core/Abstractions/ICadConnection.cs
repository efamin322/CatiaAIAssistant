using CatiaAIAssistant.Core.Cad;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>Connection to the CAD host (CATIA V5). No COM types leak through this contract.</summary>
    public interface ICadConnection
    {
        /// <summary>Programmatic identifier of the CAD application (e.g. "CATIA.Application").</summary>
        string ProgId { get; }

        /// <summary>When false, a closed CAD session is reported as disconnected instead of being launched.</summary>
        bool AllowAutoLaunchIfClosed { get; set; }

        CatiaConnectionInfo CheckConnection();
    }
}
