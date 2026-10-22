using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    public sealed class CatiaMechanicalEngine
    {
        private readonly CatiaConnectionService _connectionService;
        private readonly CatiaDocumentManager _documentManager;
        private readonly CatiaBasicPartService _basicPartService;
        private readonly OperationLogger _logger;

        public CatiaMechanicalEngine(
            CatiaConnectionService connectionService,
            CatiaDocumentManager documentManager,
            CatiaBasicPartService basicPartService,
            OperationLogger logger)
        {
            _connectionService = connectionService;
            _documentManager = documentManager;
            _basicPartService = basicPartService;
            _logger = logger;
        }

        public CadOperationResult ExecuteDesignSpecification(EngineeringDesignSpecification spec, string outputDirectory)
        {
            string preflightError = ValidateExecutionPlan(spec);
            if (!string.IsNullOrWhiteSpace(preflightError))
                return CadOperationResult.Failure("PreflightValidation", preflightError);

            try
            {
            _logger.EnsureWorkspaceDirectories();
            Directory.CreateDirectory(outputDirectory);

            var executedFiles = new Dictionary<string, string>();
            CadOperationResult? lastResult = null;
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            foreach (var step in spec.ExecutionPlan)
            {
                string ext = step.TargetDocumentName.EndsWith(".CATProduct", StringComparison.OrdinalIgnoreCase) ? ".CATProduct" : ".CATPart";
                string baseName = CatiaDocumentManager.SanitizeIdentifier(Path.GetFileNameWithoutExtension(step.TargetDocumentName), "Part");
                string targetPath = Path.Combine(outputDirectory, $"{baseName}_{ts}{ext}");

                switch (step.OperationType)
                {
                    case "CreateBlock":
                        lastResult = _basicPartService.CreateBlockPart(step.NumericInputs["X"], step.NumericInputs["Y"], step.NumericInputs["Z"], targetPath);
                        break;
                    case "CreateSteppedShaft":
                        lastResult = CreateSteppedShaftPart(step.NumericInputs["D1"], step.NumericInputs["L1"], step.NumericInputs["D2"], step.NumericInputs["L2"], step.NumericInputs["D3"], step.NumericInputs["L3"], targetPath);
                        break;
                    case "CreateSpurGear":
                        lastResult = CreateSpurGearPart(step.NumericInputs["Module"], (int)step.NumericInputs["Teeth"], step.NumericInputs["FaceWidth"], step.NumericInputs["BoreDiameter"], targetPath);
                        break;
                    case "AssembleGearPair":
                        lastResult = CreateMultiPartAssembly("GearPair", new[]
                        {
                            (executedFiles["STEP_GEAR_1"], "Pinion_1", 0.0, 0.0, 0.0),
                            (executedFiles["STEP_GEAR_2"], "Gear_2", step.NumericInputs["CenterDistance"], 0.0, 0.0)
                        }, targetPath);
                        break;
                    case "CreateFlangeCoupling":
                        lastResult = CreateFlangeCouplingPart(step.NumericInputs["ShaftBore"], targetPath);
                        break;
                    case "CreateCrankshaft":
                        lastResult = CreateCrankshaftPart(step.NumericInputs["CrankRadius"], targetPath);
                        break;
                    case "CreateConnectingRod":
                        lastResult = CreateConnectingRodPart(step.NumericInputs["RodLength"], targetPath);
                        break;
                    case "CreatePiston":
                        lastResult = CreatePistonPart(step.NumericInputs["PistonDiameter"], targetPath);
                        break;
                    case "AssembleCrankSlider":
                        double r = step.NumericInputs["CrankRadius"];
                        double l = step.NumericInputs["RodLength"];
                        lastResult = CreateMultiPartAssembly("CrankSlider", new[]
                        {
                            (executedFiles["STEP_CRANK"], "Crank", 0.0, 0.0, 0.0),
                            (executedFiles["STEP_ROD"], "Rod", r, 0.0, 12.0),
                            (executedFiles["STEP_PISTON"], "Piston", r + l, 0.0, 12.0)
                        }, targetPath);
                        break;
                    case "CreateQuadcopterFrame":
                        lastResult = CreateQuadcopterFramePart(step.NumericInputs["Wheelbase"], targetPath);
                        break;
                    case "CreateMotorDummy":
                        lastResult = _basicPartService.CreateShapePart("دایره", 14.0, 0, 0, 26.0, targetPath);
                        break;
                    case "AssembleQuadcopter":
                        double off = (step.NumericInputs["Wheelbase"] / 2.0) * 0.7071;
                        string f = executedFiles["STEP_QUAD_FRAME"];
                        string m = executedFiles["STEP_QUAD_MOTOR"];
                        lastResult = CreateMultiPartAssembly("Quadcopter", new[]
                        {
                            (f, "MainFrame", 0.0, 0.0, 0.0),
                            (m, "Motor_1", off, off, 8.0),
                            (m, "Motor_2", -off, off, 8.0),
                            (m, "Motor_3", -off, -off, 8.0),
                            (m, "Motor_4", off, -off, 8.0)
                        }, targetPath);
                        break;
                    case "CreateCarChassis":
                        lastResult = _basicPartService.CreateBlockPart(step.NumericInputs["Length"], step.NumericInputs["Width"], 12.0, targetPath);
                        break;
                    case "CreateCarWheel":
                        lastResult = _basicPartService.CreateShapePart("دایره", step.NumericInputs["WheelDiameter"] / 2.0, 0, 0, 14.0, targetPath);
                        break;
                    case "AssembleToyCar":
                        double cl = step.NumericInputs["Length"];
                        double cw = step.NumericInputs["Width"];
                        string cf = executedFiles["STEP_CAR_CHASSIS"];
                        string wf = executedFiles["STEP_CAR_WHEEL"];
                        lastResult = CreateMultiPartAssembly("ToyCar", new[]
                        {
                            (cf, "Chassis", 0.0, 0.0, 0.0),
                            (wf, "Wheel_FL", cl * 0.2, -15.0, 5.0),
                            (wf, "Wheel_FR", cl * 0.2, cw + 2.0, 5.0),
                            (wf, "Wheel_RL", cl * 0.8, -15.0, 5.0),
                            (wf, "Wheel_RR", cl * 0.8, cw + 2.0, 5.0)
                        }, targetPath);
                        break;
                    default:
                        return CadOperationResult.Failure(step.OperationType, $"نوع عملیات «{step.OperationType}» در موتور CAD پیاده‌سازی نشده است.");
                }

                if (lastResult == null || lastResult.FinalState != OperationState.Succeeded)
                    return lastResult ?? CadOperationResult.Failure(step.StepId, "خطا در اجرای مرحله.");

                executedFiles[step.StepId] = targetPath;
            }

            return lastResult ?? CadOperationResult.Failure("ExecuteDesignSpecification", "هیچ عملیات CAD اجرا نشد.");
            }
            catch (Exception ex)
            {
                _logger.Failed("ExecuteDesignSpecification", "اجرای طرح متوقف شد.", ex.ToString(), "CAD");
                return CadOperationResult.Failure("ExecuteDesignSpecification", "اجرای طرح به‌دلیل خطای پیش‌بینی‌نشده متوقف شد. گزارش فنی در گزارش نشست ثبت شد.", ex.ToString());
            }
        }

        private static string ValidateExecutionPlan(EngineeringDesignSpecification? spec)
        {
            if (spec == null) return "طرح مهندسی خالی است.";
            if (!spec.CanExecuteNow) return "طرح هنوز آماده اجرا نیست. پرسش‌ها و هشدارهای گزارش تحلیل را برطرف کنید.";
            if (spec.ExecutionPlan.Count == 0) return "برنامه اجرایی خالی است؛ هیچ عملیات CAD برای اجرا تعریف نشده است.";

            var knownIds = new HashSet<string>(spec.ExecutionPlan.Select(s => s.StepId), StringComparer.OrdinalIgnoreCase);
            var completedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var step in spec.ExecutionPlan)
            {
                if (string.IsNullOrWhiteSpace(step.StepId)) return "یکی از گام‌های اجرایی شناسه ندارد.";
                foreach (var prerequisite in step.PrerequisiteStepIds)
                {
                    if (!knownIds.Contains(prerequisite)) return $"گام «{step.StepId}» به گام تعریف‌نشده «{prerequisite}» وابسته است.";
                    if (!completedIds.Contains(prerequisite)) return $"ترتیب برنامه نامعتبر است: پیش‌نیاز «{prerequisite}» پیش از «{step.StepId}» اجرا نشده است.";
                }
                foreach (var input in step.NumericInputs)
                    if (double.IsNaN(input.Value) || double.IsInfinity(input.Value) || input.Value <= 0)
                        return $"ورودی «{input.Key}» در گام «{step.StepId}» نامعتبر است؛ مقدار باید محدود و بزرگ‌تر از صفر باشد.";
                completedIds.Add(step.StepId);
            }
            return string.Empty;
        }

        public CadOperationResult CreateSteppedShaftPart(double d1, double l1, double d2, double l2, double d3, double l3, string outputPath)
        {
            const string opName = "CreateSteppedShaft";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeYZ = part.OriginElements.PlaneYZ;

                dynamic sk1 = body.Sketches.Add(planeYZ);
                dynamic f1 = sk1.OpenEdition(); f1.CreateClosedCircle(0.0, 0.0, d1 / 2.0); sk1.CloseEdition();
                sf.AddNewPad(sk1, l1 + l2 + l3);
                part.Update();

                dynamic sk2 = body.Sketches.Add(planeYZ);
                dynamic f2 = sk2.OpenEdition(); f2.CreateClosedCircle(0.0, 0.0, d2 / 2.0); sk2.CloseEdition();
                sf.AddNewPad(sk2, l1 + l2);
                part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"شفت پله‌ای به طول {l1 + l2 + l3} میلی‌متر ساخته و ذخیره شد.";
                _logger.Succeeded(opName, result.SummaryMessage, outputPath);
                return result;
            }
            catch (Exception ex)
            {
                _documentManager.SafeRollbackDocument(document, opName);
                return CadOperationResult.Failure(opName, ex.Message);
            }
        }

        public CadOperationResult CreateSpurGearPart(double module, int teeth, double width, double bore, string outputPath)
        {
            const string opName = "CreateSpurGear";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeXY = part.OriginElements.PlaneXY;

                double rOut = (module * (teeth + 2.0)) / 2.0;
                double rRoot = Math.Max(bore / 2.0 + 3.0, (module * (teeth - 2.5)) / 2.0);

                dynamic sk = body.Sketches.Add(planeXY);
                dynamic f2d = sk.OpenEdition();
                var pts = new (double X, double Y)[teeth * 4];
                double pitchAng = (2.0 * Math.PI) / teeth;

                for (int i = 0; i < teeth; i++)
                {
                    double a = i * pitchAng;
                    pts[i * 4 + 0] = (rRoot * Math.Cos(a - pitchAng * 0.28), rRoot * Math.Sin(a - pitchAng * 0.28));
                    pts[i * 4 + 1] = (rOut * Math.Cos(a - pitchAng * 0.14), rOut * Math.Sin(a - pitchAng * 0.14));
                    pts[i * 4 + 2] = (rOut * Math.Cos(a + pitchAng * 0.14), rOut * Math.Sin(a + pitchAng * 0.14));
                    pts[i * 4 + 3] = (rRoot * Math.Cos(a + pitchAng * 0.28), rRoot * Math.Sin(a + pitchAng * 0.28));
                }
                DrawPolygon(f2d, pts);
                sk.CloseEdition();
                sf.AddNewPad(sk, width);
                part.Update();

                dynamic skBore = body.Sketches.Add(planeXY);
                dynamic fb = skBore.OpenEdition(); fb.CreateClosedCircle(0.0, 0.0, bore / 2.0); skBore.CloseEdition();
                sf.AddNewPocket(skBore, width);
                part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"چرخ‌دنده (Z={teeth}, m={module}) ساخته و ذخیره شد.";
                _logger.Succeeded(opName, result.SummaryMessage, outputPath);
                return result;
            }
            catch (Exception ex)
            {
                _documentManager.SafeRollbackDocument(document, opName);
                return CadOperationResult.Failure(opName, ex.Message);
            }
        }

        public CadOperationResult CreateFlangeCouplingPart(double bore, string outputPath)
        {
            const string opName = "CreateFlangeCoupling";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeXY = part.OriginElements.PlaneXY;

                dynamic sk1 = body.Sketches.Add(planeXY);
                dynamic f1 = sk1.OpenEdition(); f1.CreateClosedCircle(0.0, 0.0, bore * 2.0); sk1.CloseEdition();
                sf.AddNewPad(sk1, bore * 0.6);
                part.Update();

                dynamic sk2 = body.Sketches.Add(planeXY);
                dynamic f2 = sk2.OpenEdition(); f2.CreateClosedCircle(0.0, 0.0, bore); sk2.CloseEdition();
                sf.AddNewPad(sk2, bore * 1.8);
                part.Update();

                dynamic sk3 = body.Sketches.Add(planeXY);
                dynamic f3 = sk3.OpenEdition(); f3.CreateClosedCircle(0.0, 0.0, bore / 2.0); sk3.CloseEdition();
                sf.AddNewPocket(sk3, bore * 1.8);
                part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"کوپلینگ فلنجی با قطر شفت {bore}mm ساخته شد.";
                _logger.Succeeded(opName, result.SummaryMessage, outputPath);
                return result;
            }
            catch (Exception ex)
            {
                _documentManager.SafeRollbackDocument(document, opName);
                return CadOperationResult.Failure(opName, ex.Message);
            }
        }

        public CadOperationResult CreateCrankshaftPart(double r, string outputPath)
        {
            const string opName = "CreateCrankshaft";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeXY = part.OriginElements.PlaneXY;

                dynamic sk1 = body.Sketches.Add(planeXY);
                dynamic f1 = sk1.OpenEdition(); f1.CreateClosedCircle(0.0, 0.0, r * 1.3); sk1.CloseEdition();
                sf.AddNewPad(sk1, 12.0); part.Update();

                dynamic sk2 = body.Sketches.Add(planeXY);
                dynamic f2 = sk2.OpenEdition(); f2.CreateClosedCircle(r, 0.0, r * 0.25); sk2.CloseEdition();
                sf.AddNewPad(sk2, 24.0); part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = "میل‌لنگ ساخته شد.";
                return result;
            }
            catch (Exception ex) { _documentManager.SafeRollbackDocument(document, opName); return CadOperationResult.Failure(opName, ex.Message); }
        }

        public CadOperationResult CreateConnectingRodPart(double l, string outputPath)
        {
            const string opName = "CreateConnectingRod";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeXY = part.OriginElements.PlaneXY;

                dynamic sk = body.Sketches.Add(planeXY);
                dynamic f = sk.OpenEdition();
                DrawPolygon(f, new (double X, double Y)[] { (-12, -8), (l + 12, -8), (l + 12, 8), (-12, 8) });
                sk.CloseEdition();
                sf.AddNewPad(sk, 10.0); part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = "شاتون ساخته شد.";
                return result;
            }
            catch (Exception ex) { _documentManager.SafeRollbackDocument(document, opName); return CadOperationResult.Failure(opName, ex.Message); }
        }

        public CadOperationResult CreatePistonPart(double d, string outputPath)
        {
            return _basicPartService.CreateShapePart("دایره", d / 2.0, 0, 0, d * 0.85, outputPath);
        }

        public CadOperationResult CreateQuadcopterFramePart(double wb, string outputPath)
        {
            const string opName = "CreateQuadcopterFrame";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? document = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                document = catia.Documents.Add("Part");
                dynamic part = document.Part;
                dynamic body = part.Bodies.Item("PartBody");
                dynamic sf = part.ShapeFactory;
                dynamic planeXY = part.OriginElements.PlaneXY;

                double reach = (wb / 2.0) * 0.7071;
                double w = 12.0;

                dynamic sk1 = body.Sketches.Add(planeXY);
                dynamic f1 = sk1.OpenEdition();
                DrawPolygon(f1, new (double X, double Y)[] { (-reach - w, -reach + w), (-reach + w, -reach - w), (reach + w, reach - w), (reach - w, reach + w) });
                sk1.CloseEdition();
                sf.AddNewPad(sk1, 8.0); part.Update();

                dynamic sk2 = body.Sketches.Add(planeXY);
                dynamic f2 = sk2.OpenEdition();
                DrawPolygon(f2, new (double X, double Y)[] { (-reach - w, reach - w), (-reach + w, reach + w), (reach + w, -reach + w), (reach - w, -reach - w) });
                sk2.CloseEdition();
                sf.AddNewPad(sk2, 8.0); part.Update();

                _documentManager.SaveAndVerifyPartDocument(catia, document, outputPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"شاسی کوادکوپتر ({wb}mm) ساخته شد.";
                return result;
            }
            catch (Exception ex) { _documentManager.SafeRollbackDocument(document, opName); return CadOperationResult.Failure(opName, ex.Message); }
        }

        public CadOperationResult CreateMultiPartAssembly(string title, (string FilePath, string InstanceName, double X, double Y, double Z)[] items, string outputProductPath)
        {
            const string opName = "CreateMultiPartAssembly";
            var result = new CadOperationResult { OperationName = opName };
            dynamic? catia = null; dynamic? prodDoc = null;
            try
            {
                catia = _connectionService.AcquireCatiaSession();
                prodDoc = catia.Documents.Add("Product");
                dynamic rootProd = prodDoc.Product;
                dynamic products = rootProd.Products;

                for (int i = 0; i < items.Length; i++)
                {
                    var it = items[i];
                    object[] arr = new object[] { it.FilePath };
                    products.AddComponentsFromFiles(arr, "All");
                    dynamic inst = products.Item(products.Count);
                    object[] mat = new object[] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0, it.X, it.Y, it.Z };
                    try { inst.Move.Apply(mat); } catch { }
                }

                try { rootProd.Update(); } catch { }
                _documentManager.SaveAndVerifyPartDocument(catia, prodDoc, outputProductPath, result);
                result.FinalState = OperationState.Succeeded;
                result.SummaryMessage = $"اسمبلی {title} با {items.Length} قطعه در CATIA مونتاژ و ذخیره شد.";
                _logger.Succeeded(opName, result.SummaryMessage, outputProductPath);
                return result;
            }
            catch (Exception ex)
            {
                _documentManager.SafeRollbackDocument(prodDoc, opName);
                return CadOperationResult.Failure(opName, ex.Message);
            }
        }

        public string InspectActiveOrSelectedDocument(string? filePath = null)
        {
            try
            {
                dynamic catia = _connectionService.AcquireCatiaSession();
                dynamic doc = !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath)
                    ? (_documentManager.FindOpenDocumentByPathOrName(catia, filePath) ?? catia.Documents.Open(filePath))
                    : catia.ActiveDocument;

                string name = Convert.ToString(doc.Name) ?? "نامشخص";
                var sb = new StringBuilder();
                sb.AppendLine($"نام سند: {name}");
                if (name.EndsWith(".CATPart", StringComparison.OrdinalIgnoreCase))
                {
                    dynamic body = doc.Part.Bodies.Item("PartBody");
                    sb.AppendLine($"تعداد Sketchها: {body.Sketches.Count} | تعداد ویژگی‌های حجمی: {body.Shapes.Count}");
                }
                else if (name.EndsWith(".CATProduct", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"تعداد قطعات مونتاژشده: {doc.Product.Products.Count}");
                }
                return sb.ToString();
            }
            catch (Exception ex) { return "خطا در بازرسی: " + ex.Message; }
        }

        private static void DrawPolygon(dynamic f2d, (double X, double Y)[] pts)
        {
            int n = pts.Length;
            var p = new dynamic[n];
            for (int i = 0; i < n; i++) p[i] = f2d.CreatePoint(pts[i].X, pts[i].Y);
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                dynamic l = f2d.CreateLine(pts[i].X, pts[i].Y, pts[next].X, pts[next].Y);
                try { l.StartPoint = p[i]; l.EndPoint = p[next]; } catch { }
            }
        }
    }
}