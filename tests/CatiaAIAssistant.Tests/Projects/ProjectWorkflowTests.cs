using CatiaAIAssistant.Core.Projects;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Tests.Projects
{
    public class ProjectWorkflowTests
    {
        private readonly ProjectWorkflow _wf = ProjectWorkflow.CreateDefault();
        private readonly EngineeringIntentAndCalculationEngine _engine = new();

        private EngineeringProject ReadyForDesign(string prompt = "یک چرخ‌دنده با ماژول 3 و تعداد دندانه 24 و پهنای 20 و سوراخ 15")
        {
            var p = new EngineeringProject { Title = "Test", ProblemStatement = prompt };
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Requirements).Allowed);
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Analysis).Allowed);
            p.Specification = _engine.AnalyzeUserRequest(prompt);
            return p;
        }

        [Fact]
        public void NewProject_StartsInDraft()
        {
            Assert.Equal(WorkflowStage.Draft, new EngineeringProject().Stage);
        }

        [Fact]
        public void EmptyProblemStatement_BlocksRequirements()
        {
            var p = new EngineeringProject();
            var r = _wf.TryMoveTo(p, WorkflowStage.Requirements);
            Assert.False(r.Allowed);
            Assert.Equal(WorkflowStage.Draft, p.Stage);
            Assert.Contains(p.History, h => h.Action == "TransitionBlocked");
        }

        [Fact]
        public void SkippingStages_IsRejected()
        {
            var p = new EngineeringProject { ProblemStatement = "x" };
            var r = _wf.TryMoveTo(p, WorkflowStage.Execution);
            Assert.False(r.Allowed);
            Assert.Equal(WorkflowStage.Draft, p.Stage);
        }

        [Fact]
        public void Design_RequiresSpecification()
        {
            var p = new EngineeringProject { ProblemStatement = "x" };
            _wf.TryMoveTo(p, WorkflowStage.Requirements);
            _wf.TryMoveTo(p, WorkflowStage.Analysis);
            Assert.False(_wf.TryMoveTo(p, WorkflowStage.Design).Allowed);
        }

        [Fact]
        public void UnknownRequest_CannotReachDesign_BecauseOfClarifyingQuestions()
        {
            var p = ReadyForDesign("یک چیز جالب بساز");
            var r = _wf.TryMoveTo(p, WorkflowStage.Design);
            if (p.Specification!.ClarifyingQuestions.Count > 0) Assert.False(r.Allowed);
            else Assert.False(_wf.TryMoveTo(p, WorkflowStage.Validation).Allowed); // empty plan
        }

        [Fact]
        public void Execution_RequiresUserApproval_AndConfirmedAssumptions()
        {
            var p = ReadyForDesign();
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Design).Allowed);
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Validation).Allowed);

            p.Assumptions.Add(new ProjectAssumption { Text = "steel" });
            var blocked = _wf.TryMoveTo(p, WorkflowStage.Execution);
            Assert.False(blocked.Allowed);
            Assert.Equal(2, blocked.Reasons.Count(r => r.Contains("تأیید")));

            p.Assumptions[0].Approval = AssumptionApproval.Confirmed;
            p.ExecutionApproved = true;
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Execution).Allowed);
        }

        [Fact]
        public void FullHappyPath_ReachesCompleted_OnlyAfterSuccessfulExecution()
        {
            var p = ReadyForDesign();
            _wf.TryMoveTo(p, WorkflowStage.Design);
            _wf.TryMoveTo(p, WorkflowStage.Validation);
            p.ExecutionApproved = true;
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Execution).Allowed);
            Assert.False(_wf.TryMoveTo(p, WorkflowStage.Review).Allowed);
            p.ExecutionSucceeded = true;
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Review).Allowed);
            p.Errors.Add("open issue");
            Assert.False(_wf.TryMoveTo(p, WorkflowStage.Completed).Allowed);
            p.Errors.Clear();
            Assert.True(_wf.TryMoveTo(p, WorkflowStage.Completed).Allowed);
            Assert.Equal(WorkflowStage.Completed, p.Stage);
        }

        [Fact]
        public void Rework_GoingBack_ResetsApprovals_AndBumpsVersion()
        {
            var p = ReadyForDesign();
            _wf.TryMoveTo(p, WorkflowStage.Design);
            _wf.TryMoveTo(p, WorkflowStage.Validation);
            p.ExecutionApproved = true;
            _wf.TryMoveTo(p, WorkflowStage.Execution);

            var r = _wf.TryMoveTo(p, WorkflowStage.Design);
            Assert.True(r.Allowed);
            Assert.False(p.ExecutionApproved);
            Assert.Equal(2, p.Version);
        }

        [Fact]
        public void CustomRule_CanBeAddedWithoutChangingTheEngine()
        {
            var rules = DefaultTransitionRules.All().Append(new DenyRequirementsRule());
            var wf = new ProjectWorkflow(rules);
            var p = new EngineeringProject { ProblemStatement = "x" };
            var r = wf.TryMoveTo(p, WorkflowStage.Requirements);
            Assert.False(r.Allowed);
            Assert.Contains("custom", r.Reasons);
        }

        private sealed class DenyRequirementsRule : ITransitionRule
        {
            public string? Check(EngineeringProject project, WorkflowStage from, WorkflowStage to) => to == WorkflowStage.Requirements ? "custom" : null;
        }
    }
}
