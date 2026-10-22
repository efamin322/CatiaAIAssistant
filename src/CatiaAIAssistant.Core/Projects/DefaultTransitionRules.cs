namespace CatiaAIAssistant.Core.Projects
{
    /// <summary>Built-in guards that stop a project from reaching execution without the information it needs (Spec §03).</summary>
    public static class DefaultTransitionRules
    {
        public static IEnumerable<ITransitionRule> All() => new ITransitionRule[]
        {
            new Rule(WorkflowStage.Requirements, p => string.IsNullOrWhiteSpace(p.ProblemStatement) ? "شرح مسئله خالی است." : null),
            new Rule(WorkflowStage.Analysis, p => p.Requirements.Count == 0 && string.IsNullOrWhiteSpace(p.ProblemStatement) ? "هیچ نیازمندی‌ای ثبت نشده است." : null),
            new Rule(WorkflowStage.Design, p => p.Specification is null ? "تحلیل مهندسی انجام نشده است (Specification خالی است)." : null),
            new Rule(WorkflowStage.Design, p => p.Specification is { } s && s.ClarifyingQuestions.Count > 0
                ? $"{s.ClarifyingQuestions.Count} سؤال روشن‌سازی بی‌پاسخ مانده است." : null),
            new Rule(WorkflowStage.Validation, p => p.Specification is { } s && s.ExecutionPlan.Count == 0 ? "برنامهٔ ساخت CAD خالی است." : null),
            new Rule(WorkflowStage.Execution, p => p.Specification is { CanExecuteNow: false } ? "اعتبارسنجی مهندسی اجازهٔ اجرا نمی‌دهد." : null),
            new Rule(WorkflowStage.Execution, p => p.Specification is { } s && s.ContradictionsAndWarnings.Count > 0 && !p.ExecutionApproved
                ? "تناقض/هشدار حل‌نشده وجود دارد و کاربر اجرا را تأیید نکرده است." : null),
            new Rule(WorkflowStage.Execution, p => p.HasUnconfirmedAssumptions ? "فرضیات تأییدنشده وجود دارد." : null),
            new Rule(WorkflowStage.Execution, p => !p.ExecutionApproved ? "اجرا هنوز توسط کاربر تأیید نشده است." : null),
            new Rule(WorkflowStage.Review, p => !p.ExecutionSucceeded ? "اجرای CAD موفق گزارش نشده است." : null),
            new Rule(WorkflowStage.Completed, p => p.Errors.Count > 0 ? $"{p.Errors.Count} خطای باز وجود دارد." : null),
        };

        private sealed class Rule : ITransitionRule
        {
            private readonly WorkflowStage _target;
            private readonly Func<EngineeringProject, string?> _check;
            public Rule(WorkflowStage target, Func<EngineeringProject, string?> check) { _target = target; _check = check; }
            public string? Check(EngineeringProject project, WorkflowStage from, WorkflowStage to) => to == _target ? _check(project) : null;
        }
    }
}
