using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    public sealed class CatiaBasicPartService
    {
        private readonly CatiaConnectionService _connectionService;
        private readonly CatiaDocumentManager _documentManager;
        private readonly OperationLogger _logger;

        public CatiaBasicPartService(
            CatiaConnectionService connectionService,
            CatiaDocumentManager documentManager,
            OperationLogger logger)
        {
            _connectionService = connectionService;
            _documentManager = documentManager;
            _logger = logger;
        }

        public static string NormalizeDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            const string persianDigits = "۰۱۲۳۴۵۶۷۸۹";
            const string arabicDigits = "٠١٢٣٤٥٦٧٨٩";
            for (int i = 0; i < 10; i++)
            {
                value = value.Replace(persianDigits[i], (char)('0' + i));
                value = value.Replace(arabicDigits[i], (char)('0' + i));
            }
            return value;
        }

        public static bool TryParseBlockDimensionsFromText(
            string promptText,
            out double lengthX,
            out double widthY,
            out double heightZ,
            out string validationError)
        {
            lengthX = 0; widthY = 0; heightZ = 0; validationError = string.Empty;

            string normalized = NormalizeDigits(promptText.Trim());
            MatchCollection matches = Regex.Matches(normalized, @"\d+(?:[.,]\d+)?");

            if (matches.Count != 3)
            {
                validationError = "دقیقاً سه اندازه (طول، عرض، ارتفاع) وارد کنید.";
                return false;
            }

            double[] dims = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (!double.TryParse(matches[i].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out dims[i]))
                {
                    validationError = $"مقدار «{matches[i].Value}» قابل خواندن نیست.";
                    return false;
                }

                if (dims[i] <= 0 || dims[i] > 10000)
                {
                    validationError = $"ابعاد باید بین ۰ تا ۱۰,۰۰۰ میلی‌متر باشند.";
                    return false;
                }
            }

            lengthX = dims[0]; widthY = dims[1]; heightZ = dims[2];
            return true;
        }

        public CadOperationResult CreateBlockPart(double x, double y, double z, string outputPath, bool verifyReopen = false)
        {
            return CreateShapePart("مستطیل", x, y, 4, z, outputPath, verifyReopen);
        }

        public CadOperationResult CreateShapePart(
            string shapeType, double firstDimension, double secondDimension, int polygonSides,
            double padHeight, string outputPath, bool verifyReopen = false)
        {
            string opName = $"Create_{CatiaDocumentManager.SanitizeIdentifier(shapeType, "Shape")}";
            var result = new CadOperationResult { OperationName = opName };

            _logger.Running(opName, $"شروع ساخت {shapeType}...");

            dynamic? catia = null;
            dynamic? document = null;

            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                result.Verification.DocumentCreated = true;

                dynamic part = document.Part;
                string cleanPartNumber = CatiaDocumentManager.SanitizeIdentifier(Path.GetFileNameWithoutExtension(outputPath), "CATIA_Part");

                try { part.Name = cleanPartNumber; } catch { }
                try { document.Product.PartNumber = cleanPartNumber; } catch { }

                result.Verification.PartNumber = cleanPartNumber;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic plane = part.OriginElements.PlaneXY;
                dynamic sketch = body.Sketches.Add(plane);

                string sketchName = $"Sketch_{CatiaDocumentManager.SanitizeIdentifier(shapeType, "Profile")}";
                try { sketch.Name = sketchName; } catch { }

                dynamic factory2D = sketch.OpenEdition();

                switch (shapeType)
                {
                    case "مستطیل":
                        DrawClosedLineLoop(factory2D, new (double X, double Y)[] {
                            (0.0, 0.0), (firstDimension, 0.0), (firstDimension, secondDimension), (0.0, secondDimension)
                        });
                        break;
                    case "دایره":
                        factory2D.CreateClosedCircle(0.0, 0.0, firstDimension);
                        break;
                    case "مثلث":
                        DrawClosedLineLoop(factory2D, new (double X, double Y)[] {
                            (0.0, 0.0), (firstDimension, 0.0), (firstDimension / 2.0, secondDimension)
                        });
                        break;
                    case "چندضلعی":
                        var points = new (double X, double Y)[polygonSides];
                        for (int i = 0; i < polygonSides; i++)
                        {
                            double angle = (2.0 * Math.PI * i / polygonSides) - (Math.PI / 2.0);
                            points[i] = (firstDimension * Math.Cos(angle), firstDimension * Math.Sin(angle));
                        }
                        DrawClosedLineLoop(factory2D, points);
                        break;
                    default:
                        throw new NotSupportedException("شکل پشتیبانی نمی‌شود.");
                }

                sketch.CloseEdition();
                part.Update();

                if (Convert.ToInt32(body.Sketches.Count) < 1) throw new InvalidOperationException("Sketch ساخته نشد.");

                result.Verification.CreatedFeatures.Add(sketchName);

                if (padHeight > 0)
                {
                    dynamic shapeFactory = part.ShapeFactory;
                    dynamic pad = shapeFactory.AddNewPad(sketch, padHeight);
                    string padName = $"Pad_{CatiaDocumentManager.SanitizeIdentifier(shapeType, "Body")}";
                    try { pad.Name = padName; } catch { }

                    part.Update();
                    if (Convert.ToInt32(body.Shapes.Count) < 1) throw new InvalidOperationException("Pad ساخته نشد.");

                    result.Verification.CreatedFeatures.Add(padName);
                }

                result.Verification.FeatureExistsInTree = true;
                result.Verification.GeometryUpdatedCleanly = true;

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result, verifyReopen);

                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"ساخت {shapeType} با موفقیت کامل شد.";

                _logger.Succeeded(opName, result.SummaryMessage, outputPath);
                return result;
            }
            catch (Exception ex)
            {
                _documentManager.SafeRollbackDocument(document, opName);
                string errMsg = $"ساخت ناموفق بود: {ex.Message}";
                _logger.Failed(opName, errMsg, ex.ToString());
                return CadOperationResult.Failure(opName, errMsg, ex.ToString());
            }
        }

        private static void DrawClosedLineLoop(dynamic factory2D, (double X, double Y)[] vertices)
        {
            int count = vertices.Length;
            var points2D = new dynamic[count];

            for (int i = 0; i < count; i++)
                points2D[i] = factory2D.CreatePoint(vertices[i].X, vertices[i].Y);

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                dynamic line = factory2D.CreateLine(vertices[i].X, vertices[i].Y, vertices[next].X, vertices[next].Y);
                try
                {
                    line.StartPoint = points2D[i];
                    line.EndPoint = points2D[next];
                }
                catch { }
            }
        }
    }
}