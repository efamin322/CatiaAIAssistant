using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Infrastructure.Catia;

namespace CatiaAIAssistant.Engineering
{
    public sealed class EngineeringIntentAndCalculationEngine
    {
        public EngineeringDesignSpecification AnalyzeUserRequest(string rawPrompt)
        {
            string prompt = CatiaBasicPartService.NormalizeDigits(rawPrompt?.Trim() ?? string.Empty);
            var spec = new EngineeringDesignSpecification { RawUserPrompt = rawPrompt ?? string.Empty };
            var nums = ExtractNumbers(prompt);

            if (ContainsAny(prompt, "چرخ دنده", "چرخ‌دنده", "گیربکس", "gear"))
                BuildGearSpec(prompt, nums, spec);
            else if (ContainsAny(prompt, "شفت", "محور", "shaft"))
                BuildShaftSpec(nums, spec);
            else if (ContainsAny(prompt, "کوپلینگ", "فلنج", "coupling"))
                BuildCouplingSpec(nums, spec);
            else if (ContainsAny(prompt, "پیستون", "میل لنگ", "میل‌لنگ", "شاتون", "piston"))
                BuildPistonSpec(nums, spec);
            else if (ContainsAny(prompt, "کوادکوپتر", "پهپاد", "quadcopter", "drone"))
                BuildQuadcopterSpec(nums, spec);
            else if (ContainsAny(prompt, "ماشین", "اسباب بازی", "اسباب‌بازی", "چرخ"))
                BuildToyCarSpec(nums, spec);
            else
                BuildBlockSpec(nums, spec);

            return spec;
        }

        private static void BuildGearSpec(string prompt, List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.SpurGearTransmission;
            bool isPair = ContainsAny(prompt, "مجموعه", "جفت", "نسبت", "دو");
            spec.ProjectTitle = isPair ? "مجموعه دو چرخ‌دنده ساده درگیر (Spur Gear Pair)" : "چرخ‌دنده ساده پارامتریک (Spur Gear)";
            spec.EngineeringGoal = "انتقال گشتاور و دور بین محورهای موازی همراه با سوراخ شفت و شیار خار";

            double m = nums.Count >= 1 && nums[0] <= 15 ? nums[0] : 3.0;
            double z1 = nums.Count >= 2 && nums[1] >= 8 ? Math.Round(nums[1]) : 20.0;
            double ratio = nums.Count >= 3 && nums[2] <= 10 ? nums[2] : 2.0;
            double width = nums.Count >= 4 ? nums[3] : m * 8.0;
            double bore = Math.Max(10.0, Math.Round(m * z1 * 0.28));

            double d1 = m * z1;
            double da1 = m * (z1 + 2.0);
            double df1 = m * (z1 - 2.5);

            AddParam(spec, "Module", "ماژول (m)", m, "mm", nums.Count >= 1 ? ParameterStatus.SpecifiedByUser : ParameterStatus.AssumedByEngineeringRule, "تعیین‌کننده گام و استحکام دندانه");
            AddParam(spec, "TeethZ1", "تعداد دندانه (Z1)", z1, "عدد", nums.Count >= 2 ? ParameterStatus.SpecifiedByUser : ParameterStatus.AssumedByEngineeringRule, "حداقل ۱۷ دندانه برای جلوگیری از زیربری");
            AddParam(spec, "PitchDia1", "قطر دایره گام (d1)", d1, "mm", ParameterStatus.Calculated, "d1 = m × Z1");
            AddParam(spec, "FaceWidth", "عرض دندانه (b)", width, "mm", ParameterStatus.AssumedByEngineeringRule, "پهنای درگیری دندانه");

            spec.Calculations.Add(new EngineeringCalculationRecord
            {
                Title = "محاسبه هندسه چرخ‌دنده استاندارد",
                Formula = "d = m·Z , da = m·(Z+2) , df = m·(Z-2.5)",
                InputsSummary = $"m = {m} mm, Z1 = {z1}",
                ResultValue = d1,
                Unit = "mm",
                AssumptionsAndLimits = $"قطر خارجی da = {da1} mm و قطر ریشه df = {df1} mm"
            });

            var s1 = new CadExecutionStep
            {
                StepId = "STEP_GEAR_1",
                TargetDocumentName = $"SpurGear_M{m}_Z{z1}.CATPart",
                OperationType = "CreateSpurGear",
                Description = $"ساخت چرخ‌دنده با {z1} دندانه، ماژول {m}، سوراخ شفت {bore}mm و شیار خار",
                SuccessCriteria = "وجود پروفیل دندانه، سوراخ شفت و شیار خار در درخت طراحی"
            };
            s1.NumericInputs["Module"] = m;
            s1.NumericInputs["Teeth"] = z1;
            s1.NumericInputs["FaceWidth"] = width;
            s1.NumericInputs["BoreDiameter"] = bore;
            spec.ExecutionPlan.Add(s1);

            if (isPair)
            {
                double z2 = Math.Round(z1 * ratio);
                double centerDist = m * (z1 + z2) / 2.0;
                AddParam(spec, "TeethZ2", "تعداد دندانه چرخ دوم (Z2)", z2, "عدد", ParameterStatus.Calculated, "Z2 = Z1 × i");
                AddParam(spec, "CenterDistance", "فاصله محورها (a)", centerDist, "mm", ParameterStatus.Calculated, "a = m(Z1+Z2)/2");

                spec.Calculations.Add(new EngineeringCalculationRecord
                {
                    Title = "فاصله محورها و نسبت انتقال",
                    Formula = "a = m·(Z1 + Z2) / 2",
                    InputsSummary = $"Z1 = {z1}, Z2 = {z2}, m = {m}",
                    ResultValue = centerDist,
                    Unit = "mm",
                    AssumptionsAndLimits = $"نسبت انتقال واقعی i = {z2 / z1:F2}"
                });

                var s2 = new CadExecutionStep
                {
                    StepId = "STEP_GEAR_2",
                    TargetDocumentName = $"SpurGear_M{m}_Z{z2}.CATPart",
                    OperationType = "CreateSpurGear",
                    Description = $"ساخت چرخ‌دنده دوم با {z2} دندانه",
                    SuccessCriteria = "ذخیره موفق چرخ‌دنده دوم"
                };
                s2.NumericInputs["Module"] = m;
                s2.NumericInputs["Teeth"] = z2;
                s2.NumericInputs["FaceWidth"] = width;
                s2.NumericInputs["BoreDiameter"] = bore;
                spec.ExecutionPlan.Add(s2);

                var s3 = new CadExecutionStep
                {
                    StepId = "STEP_GEAR_ASM",
                    TargetDocumentName = "GearPair_Assembly.CATProduct",
                    OperationType = "AssembleGearPair",
                    Description = $"مونتاژ دو چرخ‌دنده در CATProduct با فاصله محور a = {centerDist} mm",
                    SuccessCriteria = "مونتاژ کامل هر دو چرخ‌دنده در درخت محصول"
                };
                s3.PrerequisiteStepIds.Add("STEP_GEAR_1");
                s3.PrerequisiteStepIds.Add("STEP_GEAR_2");
                s3.NumericInputs["CenterDistance"] = centerDist;
                spec.ExecutionPlan.Add(s3);
            }
        }

        private static void BuildShaftSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.SteppedShaftAndBearing;
            spec.ProjectTitle = "شفت پله‌ای سه‌مقطعی انتقال قدرت";
            spec.EngineeringGoal = "تحمل گشتاور پیچشی و ایجاد نشیمنگاه استاندارد برای یاتاقان و چرخ‌دنده";

            double d1 = nums.Count >= 1 ? nums[0] : 20.0;
            double l1 = nums.Count >= 2 ? nums[1] : 35.0;
            double d2 = nums.Count >= 3 ? nums[2] : 28.0;
            double l2 = nums.Count >= 4 ? nums[3] : 60.0;
            double d3 = nums.Count >= 5 ? nums[4] : 20.0;
            double l3 = nums.Count >= 6 ? nums[5] : 35.0;

            AddParam(spec, "D1", "قطر پله اول (D1)", d1, "mm", ParameterStatus.SpecifiedByUser, "نشیمنگاه یاتاقان اول");
            AddParam(spec, "L1", "طول پله اول (L1)", l1, "mm", ParameterStatus.SpecifiedByUser, "طول تکیه‌گاه اول");
            AddParam(spec, "D2", "قطر پله میانی (D2)", d2, "mm", ParameterStatus.SpecifiedByUser, "شانه تکیه‌گاهی میانی");
            AddParam(spec, "L2", "طول پله میانی (L2)", l2, "mm", ParameterStatus.SpecifiedByUser, "طول بخش میانی");
            AddParam(spec, "D3", "قطر پله سوم (D3)", d3, "mm", ParameterStatus.SpecifiedByUser, "نشیمنگاه یاتاقان دوم");
            AddParam(spec, "L3", "طول پله سوم (L3)", l3, "mm", ParameterStatus.SpecifiedByUser, "طول تکیه‌گاه انتهایی");

            double minD = Math.Min(d1, Math.Min(d2, d3));
            double maxTorque = (Math.PI * Math.Pow(minD / 1000.0, 3) * 45e6) / 16.0;
            spec.Calculations.Add(new EngineeringCalculationRecord
            {
                Title = "گشتاور پیچشی مجاز شفت (τ_allow = 45 MPa)",
                Formula = "T = (π · d_min³ · τ) / 16",
                InputsSummary = $"d_min = {minD} mm",
                ResultValue = Math.Round(maxTorque, 2),
                Unit = "N.m",
                AssumptionsAndLimits = $"طول کل شفت: {l1 + l2 + l3} میلی‌متر"
            });

            var step = new CadExecutionStep
            {
                StepId = "STEP_SHAFT",
                TargetDocumentName = $"SteppedShaft_L{l1 + l2 + l3}.CATPart",
                OperationType = "CreateSteppedShaft",
                Description = $"ساخت شفت پله‌ای هم‌محور به طول کل {l1 + l2 + l3}mm با سوراخ مرغک",
                SuccessCriteria = "ایجاد هر ۳ پله و سوراخ مرکزنگهدار در درخت طراحی"
            };
            step.NumericInputs["D1"] = d1; step.NumericInputs["L1"] = l1;
            step.NumericInputs["D2"] = d2; step.NumericInputs["L2"] = l2;
            step.NumericInputs["D3"] = d3; step.NumericInputs["L3"] = l3;
            spec.ExecutionPlan.Add(step);
        }

        private static void BuildCouplingSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.FlangeCoupling;
            spec.ProjectTitle = "کوپلینگ فلنجی صلب (Flange Coupling)";
            spec.EngineeringGoal = "اتصال دو شفت هم‌راستا با پیچ‌های محیطی انتقال گشتاور";

            double d = nums.Count >= 1 ? nums[0] : 22.0;
            AddParam(spec, "ShaftBore", "قطر شفت (d)", d, "mm", ParameterStatus.SpecifiedByUser, "قطر سوراخ مرکزی کوپلینگ");
            AddParam(spec, "FlangeDia", "قطر فلنج (D)", Math.Round(d * 4.0), "mm", ParameterStatus.Calculated, "D = 4d");

            var step = new CadExecutionStep
            {
                StepId = "STEP_COUPLING",
                TargetDocumentName = $"FlangeCoupling_d{d}.CATPart",
                OperationType = "CreateFlangeCoupling",
                Description = $"ساخت فلنج، توپی، سوراخ شفت {d}mm و سوراخ پیچ‌های محیطی",
                SuccessCriteria = "ساخت کامل کوپلینگ در CATIA"
            };
            step.NumericInputs["ShaftBore"] = d;
            spec.ExecutionPlan.Add(step);
        }

        private static void BuildPistonSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.CrankSliderPiston;
            spec.ProjectTitle = "مکانیزم میل‌لنگ، شاتون و پیستون (Slider-Crank)";
            spec.EngineeringGoal = "تبدیل حرکت دورانی میل‌لنگ به حرکت خطی رفت‌وبرگشتی پیستون";

            double r = nums.Count >= 1 ? nums[0] : 30.0;
            double l = nums.Count >= 2 ? nums[1] : 110.0;
            double d = nums.Count >= 3 ? nums[2] : 65.0;
            if (l < r * 2.5) l = r * 3.5;

            AddParam(spec, "CrankRadius", "شعاع لنگ (r)", r, "mm", ParameterStatus.SpecifiedByUser, "نصف کورس پیستون");
            AddParam(spec, "RodLength", "طول شاتون (L)", l, "mm", ParameterStatus.SpecifiedByUser, "فاصله مرکز دو یاتاقان شاتون");
            AddParam(spec, "PistonDia", "قطر پیستون (D)", d, "mm", ParameterStatus.SpecifiedByUser, "قطر سیلندر");
            AddParam(spec, "Stroke", "کورس پیستون (S)", 2.0 * r, "mm", ParameterStatus.Calculated, "S = 2r");

            spec.Calculations.Add(new EngineeringCalculationRecord
            {
                Title = "کورس حرکت خطی و نسبت لنگ به شاتون",
                Formula = "S = 2r , λ = r / L",
                InputsSummary = $"r = {r} mm, L = {l} mm",
                ResultValue = 2.0 * r,
                Unit = "mm",
                AssumptionsAndLimits = $"نسبت سینماتیکی λ = {r / l:F3}"
            });

            var s1 = new CadExecutionStep { StepId = "STEP_CRANK", TargetDocumentName = "Crankshaft.CATPart", OperationType = "CreateCrankshaft", Description = "ساخت میل‌لنگ", SuccessCriteria = "ذخیره میل‌لنگ" };
            s1.NumericInputs["CrankRadius"] = r;
            var s2 = new CadExecutionStep { StepId = "STEP_ROD", TargetDocumentName = "ConnectingRod.CATPart", OperationType = "CreateConnectingRod", Description = "ساخت شاتون", SuccessCriteria = "ذخیره شاتون" };
            s2.NumericInputs["RodLength"] = l;
            var s3 = new CadExecutionStep { StepId = "STEP_PISTON", TargetDocumentName = "Piston.CATPart", OperationType = "CreatePiston", Description = "ساخت پیستون", SuccessCriteria = "ذخیره پیستون" };
            s3.NumericInputs["PistonDiameter"] = d;
            var s4 = new CadExecutionStep { StepId = "STEP_CRANK_ASM", TargetDocumentName = "CrankPiston_Assembly.CATProduct", OperationType = "AssembleCrankSlider", Description = "مونتاژ میل‌لنگ، شاتون و پیستون در CATProduct", SuccessCriteria = "مونتاژ هر ۳ قطعه" };
            s4.PrerequisiteStepIds.AddRange(new[] { "STEP_CRANK", "STEP_ROD", "STEP_PISTON" });
            s4.NumericInputs["CrankRadius"] = r; s4.NumericInputs["RodLength"] = l;

            spec.ExecutionPlan.AddRange(new[] { s1, s2, s3, s4 });
        }

        private static void BuildQuadcopterSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.QuadcopterSystem;
            spec.ProjectTitle = "کوادکوپتر چهارموتوره (X-Frame Quadcopter)";
            spec.EngineeringGoal = "طراحی شاسی مرکزی، ۴ بازو، ۴ موتور و بررسی عدم تداخل ملخ‌ها";

            double wb = nums.Count >= 1 && nums[0] >= 120 ? nums[0] : 450.0;
            double propIn = nums.Count >= 2 && nums[1] <= 30 ? nums[1] : 10.0;
            double mass = nums.Count >= 3 && nums[2] <= 25 ? nums[2] : 1.4;

            double propMm = propIn * 25.4;
            double clearance = wb * Math.Sin(Math.PI / 4.0) - propMm;
            double thrustPerMotorG = (mass * 1000.0 * 2.2) / 4.0;

            AddParam(spec, "Wheelbase", "فاصله قطری موتورها", wb, "mm", ParameterStatus.SpecifiedByUser, "قطر شاسی");
            AddParam(spec, "PropDiameter", "قطر ملخ", Math.Round(propMm, 1), "mm", ParameterStatus.Calculated, $"معادل {propIn} اینچ");
            AddParam(spec, "PropClearance", "فاصله آزاد نوک ملخ‌ها", Math.Round(clearance, 1), "mm", ParameterStatus.Calculated, "جلوگیری از برخورد ملخ‌ها");

            spec.Calculations.Add(new EngineeringCalculationRecord
            {
                Title = "محاسبه رانش هر موتور برای پرواز (T/W = 2.2)",
                Formula = "F_motor = (2.2 · m_total) / 4",
                InputsSummary = $"m = {mass} kg, Wheelbase = {wb} mm",
                ResultValue = Math.Round(thrustPerMotorG, 0),
                Unit = "gf",
                AssumptionsAndLimits = $"فاصله ایمنی نوک ملخ‌ها: {clearance:F1} میلی‌متر"
            });

            var s1 = new CadExecutionStep { StepId = "STEP_QUAD_FRAME", TargetDocumentName = $"QuadFrame_{wb}.CATPart", OperationType = "CreateQuadcopterFrame", Description = "ساخت شاسی چهاربازوی کوادکوپتر", SuccessCriteria = "ساخت شاسی" };
            s1.NumericInputs["Wheelbase"] = wb;
            var s2 = new CadExecutionStep { StepId = "STEP_QUAD_MOTOR", TargetDocumentName = "BrushlessMotor.CATPart", OperationType = "CreateMotorDummy", Description = "ساخت مدل موتور براشلس", SuccessCriteria = "ساخت موتور" };
            var s3 = new CadExecutionStep { StepId = "STEP_QUAD_ASM", TargetDocumentName = "Quadcopter_Assembly.CATProduct", OperationType = "AssembleQuadcopter", Description = "مونتاژ شاسی و ۴ موتور روی بازوها", SuccessCriteria = "مونتاژ ۵ قطعه در CATProduct" };
            s3.PrerequisiteStepIds.AddRange(new[] { "STEP_QUAD_FRAME", "STEP_QUAD_MOTOR" });
            s3.NumericInputs["Wheelbase"] = wb;

            spec.ExecutionPlan.AddRange(new[] { s1, s2, s3 });
        }

        private static void BuildToyCarSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.ToyCarSystem;
            spec.ProjectTitle = "ماشین چهارچرخ متحرک (Toy Car Assembly)";
            spec.EngineeringGoal = "ساخت شاسی و ۴ چرخ و مونتاژ در موقعیت محورهای جلو و عقب";

            double l = nums.Count >= 1 ? nums[0] : 160.0;
            double w = nums.Count >= 2 ? nums[1] : 90.0;
            double wd = nums.Count >= 3 ? nums[2] : 45.0;

            AddParam(spec, "Length", "طول شاسی", l, "mm", ParameterStatus.SpecifiedByUser, "طول بدنه");
            AddParam(spec, "Width", "عرض شاسی", w, "mm", ParameterStatus.SpecifiedByUser, "عرض بدنه");
            AddParam(spec, "WheelDia", "قطر چرخ", wd, "mm", ParameterStatus.SpecifiedByUser, "قطر چرخ‌ها");

            var s1 = new CadExecutionStep { StepId = "STEP_CAR_CHASSIS", TargetDocumentName = "CarChassis.CATPart", OperationType = "CreateCarChassis", Description = "ساخت شاسی ماشین", SuccessCriteria = "ساخت شاسی" };
            s1.NumericInputs["Length"] = l; s1.NumericInputs["Width"] = w;
            var s2 = new CadExecutionStep { StepId = "STEP_CAR_WHEEL", TargetDocumentName = "CarWheel.CATPart", OperationType = "CreateCarWheel", Description = "ساخت چرخ", SuccessCriteria = "ساخت چرخ" };
            s2.NumericInputs["WheelDiameter"] = wd;
            var s3 = new CadExecutionStep { StepId = "STEP_CAR_ASM", TargetDocumentName = "ToyCar_Assembly.CATProduct", OperationType = "AssembleToyCar", Description = "مونتاژ شاسی و ۴ چرخ", SuccessCriteria = "مونتاژ کامل" };
            s3.PrerequisiteStepIds.AddRange(new[] { "STEP_CAR_CHASSIS", "STEP_CAR_WHEEL" });
            s3.NumericInputs["Length"] = l; s3.NumericInputs["Width"] = w;

            spec.ExecutionPlan.AddRange(new[] { s1, s2, s3 });
        }

        private static void BuildBlockSpec(List<double> nums, EngineeringDesignSpecification spec)
        {
            spec.Domain = EngineeringDomain.BasicBlockOrProfile;
            spec.ProjectTitle = "بلوک مکعبی پارامتریک";
            spec.EngineeringGoal = "ساخت قطعه منشوری سه‌بعدی در Part Design";

            double x = nums.Count >= 1 ? nums[0] : 40.0;
            double y = nums.Count >= 2 ? nums[1] : 30.0;
            double z = nums.Count >= 3 ? nums[2] : 20.0;

            AddParam(spec, "X", "طول (X)", x, "mm", ParameterStatus.SpecifiedByUser, "طول");
            AddParam(spec, "Y", "عرض (Y)", y, "mm", ParameterStatus.SpecifiedByUser, "عرض");
            AddParam(spec, "Z", "ارتفاع (Z)", z, "mm", ParameterStatus.SpecifiedByUser, "ارتفاع");

            var step = new CadExecutionStep { StepId = "STEP_BLOCK", TargetDocumentName = $"Block_{x}x{y}x{z}.CATPart", OperationType = "CreateBlock", Description = $"ساخت بلوک {x}×{y}×{z} میلی‌متر", SuccessCriteria = "ذخیره فایل" };
            step.NumericInputs["X"] = x; step.NumericInputs["Y"] = y; step.NumericInputs["Z"] = z;
            spec.ExecutionPlan.Add(step);
        }

        private static void AddParam(EngineeringDesignSpecification spec, string key, string name, double val, string unit, ParameterStatus st, string effect)
        {
            spec.Parameters[key] = new EngineeringParameter { Key = key, DisplayName = name, Value = val, Unit = unit, Status = st, EngineeringEffect = effect };
        }

        private static List<double> ExtractNumbers(string text)
        {
            var list = new List<double>();
            foreach (Match m in Regex.Matches(text, @"\d+(?:[.,]\d+)?"))
            {
                if (double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0)
                    list.Add(v);
            }
            return list;
        }

        private static bool ContainsAny(string text, params string[] terms)
        {
            foreach (string t in terms)
                if (text.Contains(t, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static string FormatSpecificationReport(EngineeringDesignSpecification spec)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"■ طرح مهندسی: {spec.ProjectTitle}");
            sb.AppendLine($"■ هدف: {spec.EngineeringGoal}");
            sb.AppendLine();
            sb.AppendLine("📐 پارامترهای استخراج و محاسبه‌شده:");
            foreach (var p in spec.Parameters.Values)
                sb.AppendLine($"  • {p.DisplayName}: {p.Value} {p.Unit} [{p.StatusBadge}] — {p.EngineeringEffect}");
            sb.AppendLine();
            sb.AppendLine("🧮 محاسبات مهندسی:");
            foreach (var c in spec.Calculations)
                sb.AppendLine($"  • {c.Title}: {c.ResultValue} {c.Unit} ({c.Formula}) | {c.AssumptionsAndLimits}");
            sb.AppendLine();
            sb.AppendLine("🛠 برنامه اجرای گام‌به‌گام در CATIA:");
            foreach (var s in spec.ExecutionPlan)
                sb.AppendLine($"  [{s.StepId}] {s.OperationType} -> {s.TargetDocumentName} : {s.Description}");
            return sb.ToString();
        }
    }
}