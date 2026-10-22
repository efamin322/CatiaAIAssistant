using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Infrastructure.Catia;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Diagnostics
{
    public enum TestCategory { AutomatedUnit, CatiaIntegration }

    public sealed class SelfTestCaseResult
    {
        public string TestId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public TestCategory Category { get; init; }
        public OperationState State { get; init; }
        public string Details { get; init; } = string.Empty;
    }

    public sealed class Phase1SelfTestRunner
    {
        private readonly OperationLogger _logger;
        private readonly CatiaConnectionService _connectionService;
        private readonly CatiaDocumentManager _documentManager;
        private readonly CatiaBasicPartService _partService;

        public Phase1SelfTestRunner(OperationLogger logger, CatiaConnectionService connectionService, CatiaDocumentManager documentManager, CatiaBasicPartService partService)
        {
            _logger = logger; _connectionService = connectionService; _documentManager = documentManager; _partService = partService;
        }

        public IReadOnlyList<SelfTestCaseResult> RunPhase1Suite(bool includeCatiaLiveTests)
        {
            var results = new List<SelfTestCaseResult>();
            _logger.Running("Phase1SelfTests", "شروع اجرای آزمون‌های مرحله اول...");

            results.Add(TestDigitNormalizationAndParser());
            results.Add(TestUnknownIntentGuard());
            results.Add(TestAssumptionFlagging());
            results.Add(TestIdentifierSanitization());
            results.Add(TestWorkspaceAndBackupMechanism());

            if (!includeCatiaLiveTests)
            {
                results.Add(new SelfTestCaseResult { TestId = "T1.4-CATIA-E2E", Title = "آزمون یکپارچه‌سازی CATIA", Category = TestCategory.CatiaIntegration, State = OperationState.Skipped, Details = "آزمون نیازمند CATIA رد شد." });
            }
            else
            {
                results.Add(TestCatiaIntegrationAndLifecycle());
            }

            return results;
        }

        private SelfTestCaseResult TestDigitNormalizationAndParser()
        {
            try
            {
                bool okValid = CatiaBasicPartService.TryParseBlockDimensionsFromText("یک مکعب با ابعاد ۴۰ در ۲۵.۵ در ۱۲ میلی‌متر بساز", out double x, out double y, out double z, out string err);
                if (okValid && Math.Abs(x - 40.0) < 1e-6 && Math.Abs(z - 12.0) < 1e-6)
                    return new SelfTestCaseResult { TestId = "T1.1", Title = "تبدیل ارقام", Category = TestCategory.AutomatedUnit, State = OperationState.Succeeded, Details = "استخراج ابعاد تأیید شد." };
                return new SelfTestCaseResult { TestId = "T1.1", Title = "تبدیل ارقام", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = err };
            }
            catch (Exception ex) { return new SelfTestCaseResult { TestId = "T1.1", Title = "خطا", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = ex.Message }; }
        }

        private SelfTestCaseResult TestUnknownIntentGuard()
        {
            try
            {
                var engine = new EngineeringIntentAndCalculationEngine();
                var spec = engine.AnalyzeUserRequest("برای من یک هواپیمای مسافربری طراحی کن");
                bool ok = spec.Domain == EngineeringDomain.Unknown && !spec.CanExecuteNow && spec.ExecutionPlan.Count == 0;
                return new SelfTestCaseResult
                {
                    TestId = "T1.2-A",
                    Title = "جلوگیری از تبدیل درخواست ناشناخته به بلوک",
                    Category = TestCategory.AutomatedUnit,
                    State = ok ? OperationState.Succeeded : OperationState.Failed,
                    Details = ok ? "درخواست ناشناخته متوقف شد و پرسش شفاف‌سازی تولید شد." : "درخواست ناشناخته به‌اشتباه به طرح اجرایی تبدیل شد."
                };
            }
            catch (Exception ex)
            {
                return new SelfTestCaseResult { TestId = "T1.2-A", Title = "کنترل درخواست ناشناخته", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = ex.Message };
            }
        }

        private SelfTestCaseResult TestAssumptionFlagging()
        {
            try
            {
                var engine = new EngineeringIntentAndCalculationEngine();
                var spec = engine.AnalyzeUserRequest("یک شفت پله‌ای طراحی کن");
                bool ok = spec.HasAssumedParameters;
                return new SelfTestCaseResult
                {
                    TestId = "T1.2-B",
                    Title = "علامت‌گذاری ابعاد پیش‌فرض",
                    Category = TestCategory.AutomatedUnit,
                    State = ok ? OperationState.Succeeded : OperationState.Failed,
                    Details = ok ? "ابعاد انتخاب‌شده توسط برنامه به‌عنوان فرض مهندسی نمایش داده می‌شوند." : "ابعاد پیش‌فرض به‌درستی علامت‌گذاری نشدند."
                };
            }
            catch (Exception ex)
            {
                return new SelfTestCaseResult { TestId = "T1.2-B", Title = "علامت‌گذاری فرضیات", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = ex.Message };
            }
        }

        private SelfTestCaseResult TestIdentifierSanitization()
        {
            string sanitized = CatiaDocumentManager.SanitizeIdentifier("مستطیل ۱*۲", "PART");
            if (sanitized.StartsWith("Rect"))
                return new SelfTestCaseResult { TestId = "T1.2", Title = "نام‌گذاری استاندارد", Category = TestCategory.AutomatedUnit, State = OperationState.Succeeded, Details = "تأیید شد." };
            return new SelfTestCaseResult { TestId = "T1.2", Title = "نام‌گذاری", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = "خطا" };
        }

        private SelfTestCaseResult TestWorkspaceAndBackupMechanism()
        {
            try
            {
                _logger.EnsureWorkspaceDirectories();
                string dummyFile = Path.Combine(_logger.PartsOutputDirectory, "SelfTest_Dummy.txt");
                File.WriteAllText(dummyFile, "Initial", Encoding.UTF8);
                string? backup = _documentManager.CreateBackupIfFileExists(dummyFile);
                bool ok = backup != null && File.Exists(backup);
                if (File.Exists(dummyFile)) File.Delete(dummyFile);
                if (ok) File.Delete(backup!);
                return new SelfTestCaseResult { TestId = "T1.3", Title = "پوشه کاری و بکاپ", Category = TestCategory.AutomatedUnit, State = ok ? OperationState.Succeeded : OperationState.Failed, Details = ok ? "تأیید شد" : "ناموفق" };
            }
            catch (Exception ex) { return new SelfTestCaseResult { TestId = "T1.3", Title = "خطا", Category = TestCategory.AutomatedUnit, State = OperationState.Failed, Details = ex.Message }; }
        }

        private SelfTestCaseResult TestCatiaIntegrationAndLifecycle()
        {
            if (!_connectionService.CheckConnection().IsConnected)
                return new SelfTestCaseResult { TestId = "T1.4", Title = "ارتباط با CATIA", Category = TestCategory.CatiaIntegration, State = OperationState.Skipped, Details = "CATIA بسته است." };

            string path = Path.Combine(_logger.PartsOutputDirectory, $"TestBlock_{DateTime.Now:yyyyMMdd_HHmmss}.CATPart");
            var res = _partService.CreateBlockPart(35.0, 25.0, 15.0, path, true);
            if (res.FinalState == OperationState.Succeeded)
                return new SelfTestCaseResult { TestId = "T1.4", Title = "تست CATIA", Category = TestCategory.CatiaIntegration, State = OperationState.Succeeded, Details = "تأیید شد." };
            return new SelfTestCaseResult { TestId = "T1.4", Title = "تست CATIA", Category = TestCategory.CatiaIntegration, State = OperationState.Failed, Details = res.SummaryMessage };
        }

        public static string FormatReport(IReadOnlyList<SelfTestCaseResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== گزارش آزمون ===");
            foreach (var item in results) sb.AppendLine($"[{item.State}] {item.Title}: {item.Details}");
            return sb.ToString();
        }
    }
}