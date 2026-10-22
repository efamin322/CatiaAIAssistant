using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Errors;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Core.Text;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Core.Commands
{
    /// <summary>Traceable record of one executed command (Spec §04: started / completed / failed are distinguishable).</summary>
    public sealed class CommandRecord
    {
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public string StepId { get; init; } = string.Empty;
        public string OperationType { get; init; } = string.Empty;
        public OperationRisk Risk { get; init; }
        public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
        public DateTime? FinishedUtc { get; set; }
        public OperationState State { get; set; } = OperationState.Running;
        public string? OutputFilePath { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class PlanExecutionReport
    {
        public bool Succeeded { get; set; }
        public bool Cancelled { get; set; }
        /// <summary>Classification of the exception that stopped execution, if any.</summary>
        public ErrorKind? ErrorKind { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<CommandRecord> Commands { get; } = new();
        public CadOperationResult? LastResult { get; set; }
        public IReadOnlyDictionary<string, string> OutputFiles { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Validates and executes an engineering CAD plan through the operation allowlist. Contains no CAD-specific code:
    /// every concrete operation is delegated to a registered <see cref="ICadOperationHandler"/>.
    /// </summary>
    public sealed class CadPlanExecutor
    {
        private readonly CadOperationRegistry _registry;

        public CadOperationRegistry Registry => _registry;
        private readonly IOperationLogger _logger;
        private readonly Func<DateTime> _clock;

        public CadPlanExecutor(CadOperationRegistry registry, IOperationLogger logger, Func<DateTime>? clock = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clock = clock ?? (() => DateTime.Now);
        }

        /// <summary>Returns an empty string when the plan is executable, otherwise the first blocking problem.</summary>
        public string Validate(EngineeringDesignSpecification? spec)
        {
            if (spec == null) return "طرح مهندسی خالی است.";
            if (!spec.CanExecuteNow) return "طرح هنوز آماده اجرا نیست. پرسش‌ها و هشدارهای گزارش تحلیل را برطرف کنید.";
            if (spec.ExecutionPlan.Count == 0) return "برنامه اجرایی خالی است؛ هیچ عملیات CAD برای اجرا تعریف نشده است.";

            var knownIds = new HashSet<string>(spec.ExecutionPlan.Select(s => s.StepId), StringComparer.OrdinalIgnoreCase);
            if (knownIds.Count != spec.ExecutionPlan.Count) return "شناسهٔ گام‌های برنامه تکراری است.";
            var completedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in spec.ExecutionPlan)
            {
                if (string.IsNullOrWhiteSpace(step.StepId)) return "یکی از گام‌های اجرایی شناسه ندارد.";
                if (!_registry.TryGet(step.OperationType, out var handler))
                    return $"عملیات «{step.OperationType}» در فهرست عملیات مجاز نیست (گام «{step.StepId}»).";

                foreach (var prerequisite in step.PrerequisiteStepIds)
                {
                    if (!knownIds.Contains(prerequisite)) return $"گام «{step.StepId}» به گام تعریف‌نشده «{prerequisite}» وابسته است.";
                    if (!completedIds.Contains(prerequisite)) return $"ترتیب برنامه نامعتبر است: پیش‌نیاز «{prerequisite}» پیش از «{step.StepId}» اجرا نشده است.";
                }
                foreach (var required in handler.Descriptor.RequiredInputs)
                    if (!step.NumericInputs.ContainsKey(required)) return $"ورودی الزامی «{required}» در گام «{step.StepId}» وجود ندارد.";
                foreach (var dep in handler.Descriptor.RequiredPrerequisiteOutputs)
                    if (!completedIds.Contains(dep)) return $"گام «{step.StepId}» به خروجی گام «{dep}» نیاز دارد که پیش از آن اجرا نمی‌شود.";
                foreach (var input in step.NumericInputs)
                    if (double.IsNaN(input.Value) || double.IsInfinity(input.Value) || input.Value <= 0)
                        return $"ورودی «{input.Key}» در گام «{step.StepId}» نامعتبر است؛ مقدار باید محدود و بزرگ‌تر از صفر باشد.";
                if (handler.Descriptor.Risk == OperationRisk.High)
                    return $"عملیات پرخطر «{step.OperationType}» بدون تأیید صریح و پشتیبان قابل اجرا نیست.";
                completedIds.Add(step.StepId);
            }
            return string.Empty;
        }

        public PlanExecutionReport Execute(EngineeringDesignSpecification spec, string outputDirectory, CancellationToken cancellationToken = default)
        {
            var report = new PlanExecutionReport();
            string problem = Validate(spec);
            if (problem.Length > 0)
            {
                report.Summary = problem;
                report.LastResult = CadOperationResult.Failure("PreflightValidation", problem);
                _logger.Failed("PreflightValidation", problem, category: "Plan");
                return report;
            }

            _logger.EnsureWorkspaceDirectories();
            Directory.CreateDirectory(outputDirectory);
            string ts = _clock().ToString("yyyyMMdd_HHmmss");
            var executed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            CadExecutionStep? previous = null;
            foreach (var step in spec.ExecutionPlan)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation is honoured only between steps so no CAD document is left half-built (Spec §05.4/§05.10).
                    report.Cancelled = true;
                    MarkRemainingSkipped(spec, previous, report, "به‌دلیل لغو عملیات توسط کاربر اجرا نشد.");
                    report.Summary = $"اجرا توسط کاربر لغو شد؛ {executed.Count} گام کامل شده و فایل‌های آن حفظ شده‌اند.";
                    report.OutputFiles = executed;
                    _logger.Skipped("ExecutePlan", report.Summary, "Command");
                    return report;
                }

                _registry.TryGet(step.OperationType, out var handler);
                string ext = handler.Descriptor.Output == OperationOutputKind.Product ? ".CATProduct" : ".CATPart";
                string baseName = TextNormalizer.SanitizeIdentifier(Path.GetFileNameWithoutExtension(step.TargetDocumentName), "Part");
                string targetPath = Path.Combine(outputDirectory, $"{baseName}_{ts}{ext}");

                var record = new CommandRecord { StepId = step.StepId, OperationType = step.OperationType, Risk = handler.Descriptor.Risk };
                report.Commands.Add(record);
                step.State = OperationState.Running;
                _logger.Running(step.OperationType, $"[{record.CommandId:N}] شروع گام {step.StepId}", "Command");

                CadOperationResult result;
                try
                {
                    result = handler.Execute(new CadStepContext { Step = step, TargetPath = targetPath, ExecutedFiles = executed });
                }
                catch (Exception ex)
                {
                    var err = ErrorTranslator.Translate(ex);
                    result = CadOperationResult.Failure(step.OperationType, $"گام «{step.StepId}» متوقف شد: {err.UserMessage}", err.TechnicalDetails);
                    report.ErrorKind = err.Kind;
                }

                record.FinishedUtc = DateTime.UtcNow;
                record.State = result.FinalState;
                record.Message = result.SummaryMessage;
                report.LastResult = result;
                step.State = result.FinalState;

                if (result.FinalState != OperationState.Succeeded)
                {
                    _logger.Failed(step.OperationType, $"[{record.CommandId:N}] {result.SummaryMessage}", result.ErrorDetails, "Command");
                    MarkRemainingSkipped(spec, step, report, "به‌دلیل شکست گام قبلی اجرا نشد.");
                    report.Summary = $"اجرا در گام «{step.StepId}» متوقف شد: {result.SummaryMessage}";
                    report.OutputFiles = executed;
                    return report;
                }

                string output = result.OutputFilePath ?? targetPath;
                record.OutputFilePath = output;
                step.OutputFilePath = output;
                executed[step.StepId] = output;
                previous = step;
            }

            report.Succeeded = true;
            report.OutputFiles = executed;
            report.Summary = $"{report.Commands.Count} گام CAD با موفقیت اجرا شد.";
            return report;
        }

        /// <summary>Marks every step after <paramref name="failed"/> (or all steps when null) as skipped.</summary>
        private static void MarkRemainingSkipped(EngineeringDesignSpecification spec, CadExecutionStep? failed, PlanExecutionReport report, string reason)
        {
            bool after = failed is null;
            foreach (var s in spec.ExecutionPlan)
            {
                if (after)
                {
                    s.State = OperationState.Skipped;
                    report.Commands.Add(new CommandRecord { StepId = s.StepId, OperationType = s.OperationType, State = OperationState.Skipped, FinishedUtc = DateTime.UtcNow, Message = reason });
                }
                if (ReferenceEquals(s, failed)) after = true;
            }
        }
    }
}
