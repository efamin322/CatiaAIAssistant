using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Core.Abstractions
{
    /// <summary>Creation of basic parametric shapes (sketch + pad).</summary>
    public interface ICadPartService
    {
        CadOperationResult CreateBlockPart(double x, double y, double z, string outputPath, bool verifyReopen = false);

        CadOperationResult CreateShapePart(
            string shapeType,
            double firstDimension,
            double secondDimension,
            int polygonSides,
            double padHeight,
            string outputPath,
            bool verifyReopen = false);
    }
}
