using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Commands;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Tests.Commands
{
    public class CadPlanExecutorTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "catia_ai_exec_" + Guid.NewGuid().ToString("N"));
        private readonly List<string> _calls = new();

        private ICadOperationHandler Fake(string type, string[]? inputs = null, string[]? prereq = null, bool fail = false,
                                          OperationRisk risk = OperationRisk.Low, OperationOutputKind output = OperationOutputKind.Part) =>
            new DelegateCadOperationHandler(new CadOperationDescriptor
            {
                OperationType = type, RequiredInputs = inputs ?? Array.Empty<string>(), RequiredPrerequisiteOutputs = prereq ?? Array.Empty<string>(),
                Risk = risk, Output = output
            }, c =>
            {
                _calls.Add($"{type}:{Path.GetExtension(c.TargetPath)}:{string.Join(",", c.ExecutedFiles.Keys)}");
                return fail ? CadOperationResult.Failure(type, "boom")
                            : new CadOperationResult { OperationName = type, FinalState = OperationState.Succeeded, OutputFilePath = c.TargetPath };
            });

        private CadPlanExecutor Executor(params ICadOperationHandler[] handlers)
        {
            var reg = new CadOperationRegistry();
            foreach (var h in handlers) reg.Register(h);
            return new CadPlanExecutor(reg, new OperationLogger(_root), () => new DateTime(2026, 1, 2, 3, 4, 5));
        }

        private static EngineeringDesignSpecification Plan(params CadExecutionStep[] steps)
        {
            var s = new EngineeringDesignSpecification { CanExecuteNow = true };
            s.ExecutionPlan.AddRange(steps);
            return s;
        }

        private static CadExecutionStep Step(string id, string op, string doc = "P.CATPart", params (string k, double v)[] inputs)
        {
            var st = new CadExecutionStep { StepId = id, OperationType = op, TargetDocumentName = doc };
            foreach (var (k, v) in inputs) st.NumericInputs[k] = v;
            return st;
        }

        [Fact]
        public void UnknownOperation_IsRejected_BeforeAnyCall()
        {
            var ex = Executor(Fake("A"));
            var report = ex.Execute(Plan(Step("S1", "A"), Step("S2", "DeleteEverything")), _root);
            Assert.False(report.Succeeded);
            Assert.Contains("مجاز نیست", report.Summary);
            Assert.Empty(_calls);
        }

        [Fact]
        public void OperationLookup_IsCaseSensitive()
        {
            Assert.False(new CadOperationRegistry().Register(Fake("CreateBlock")).IsAllowed("createblock"));
        }

        [Fact]
        public void DuplicateRegistration_Throws()
        {
            var reg = new CadOperationRegistry().Register(Fake("A"));
            Assert.Throws<InvalidOperationException>(() => reg.Register(Fake("A")));
        }

        [Fact]
        public void MissingRequiredInput_IsRejected()
        {
            var report = Executor(Fake("A", new[] { "X" })).Execute(Plan(Step("S1", "A")), _root);
            Assert.False(report.Succeeded);
            Assert.Contains("X", report.Summary);
            Assert.Empty(_calls);
        }

        [Fact]
        public void NonPositiveInput_IsRejected()
        {
            var report = Executor(Fake("A", new[] { "X" })).Execute(Plan(Step("S1", "A", "P.CATPart", ("X", 0))), _root);
            Assert.False(report.Succeeded);
        }

        [Fact]
        public void AssemblyBeforeItsParts_IsRejected()
        {
            var ex = Executor(Fake("Part"), Fake("Asm", prereq: new[] { "S1" }, output: OperationOutputKind.Product));
            var report = ex.Execute(Plan(Step("S2", "Asm"), Step("S1", "Part")), _root);
            Assert.False(report.Succeeded);
            Assert.Empty(_calls);
        }

        [Fact]
        public void HighRiskOperation_IsNeverAutoExecuted()
        {
            var report = Executor(Fake("Modify", risk: OperationRisk.High)).Execute(Plan(Step("S1", "Modify")), _root);
            Assert.False(report.Succeeded);
            Assert.Contains("پرخطر", report.Summary);
        }

        [Fact]
        public void DuplicateStepIds_AreRejected()
        {
            var report = Executor(Fake("A")).Execute(Plan(Step("S1", "A"), Step("S1", "A")), _root);
            Assert.False(report.Succeeded);
        }

        [Fact]
        public void HappyPath_ExecutesInOrder_PassesOutputs_AndUsesProductExtension()
        {
            var ex = Executor(Fake("Part"), Fake("Asm", prereq: new[] { "S1", "S2" }, output: OperationOutputKind.Product));
            var report = ex.Execute(Plan(Step("S1", "Part"), Step("S2", "Part"), Step("S3", "Asm", "A.CATProduct")), _root);

            Assert.True(report.Succeeded, report.Summary);
            Assert.Equal(new[] { "Part:.CATPart:", "Part:.CATPart:S1", "Asm:.CATProduct:S1,S2" }, _calls);
            Assert.All(report.Commands, c => Assert.Equal(OperationState.Succeeded, c.State));
            Assert.Equal(3, report.Commands.Select(c => c.CommandId).Distinct().Count());
            Assert.EndsWith("_20260102_030405.CATProduct", report.OutputFiles["S3"]);
        }

        [Fact]
        public void FailingStep_StopsExecution_AndMarksRestSkipped()
        {
            var ex = Executor(Fake("Ok"), Fake("Bad", fail: true));
            var spec = Plan(Step("S1", "Ok"), Step("S2", "Bad"), Step("S3", "Ok"));
            var report = ex.Execute(spec, _root);

            Assert.False(report.Succeeded);
            Assert.Equal(2, _calls.Count);
            Assert.Equal(OperationState.Failed, report.Commands[1].State);
            Assert.Equal(OperationState.Skipped, report.Commands[2].State);
            Assert.Equal(OperationState.Skipped, spec.ExecutionPlan[2].State);
        }

        [Fact]
        public void HandlerException_IsConvertedToFailure_NotPropagated()
        {
            var reg = new CadOperationRegistry().Register(new DelegateCadOperationHandler(
                new CadOperationDescriptor { OperationType = "Throw" }, _ => throw new InvalidOperationException("COM died")));
            var report = new CadPlanExecutor(reg, new OperationLogger(_root)).Execute(Plan(Step("S1", "Throw")), _root);
            Assert.False(report.Succeeded);
            Assert.Contains("COM died", report.LastResult!.ErrorDetails);
        }

        [Fact]
        public void Cancellation_StopsBetweenSteps_AndKeepsCompletedOutputs()
        {
            using var cts = new CancellationTokenSource();
            var reg = new CadOperationRegistry()
                .Register(new DelegateCadOperationHandler(new CadOperationDescriptor { OperationType = "A" }, c =>
                {
                    _calls.Add(c.Step.StepId);
                    cts.Cancel(); // user presses Cancel while step 1 runs
                    return new CadOperationResult { OperationName = "A", FinalState = OperationState.Succeeded, OutputFilePath = c.TargetPath };
                }));
            var spec = Plan(Step("S1", "A"), Step("S2", "A"), Step("S3", "A"));
            var report = new CadPlanExecutor(reg, new OperationLogger(_root)).Execute(spec, _root, cts.Token);

            Assert.True(report.Cancelled);
            Assert.False(report.Succeeded);
            Assert.Equal(new[] { "S1" }, _calls);
            Assert.True(report.OutputFiles.ContainsKey("S1"));
            Assert.Equal(OperationState.Succeeded, spec.ExecutionPlan[0].State);
            Assert.Equal(OperationState.Skipped, spec.ExecutionPlan[1].State);
            Assert.Equal(OperationState.Skipped, spec.ExecutionPlan[2].State);
        }

        [Fact]
        public void AlreadyCancelled_ExecutesNothing()
        {
            var spec = Plan(Step("S1", "A"));
            var report = Executor(Fake("A")).Execute(spec, _root, new CancellationToken(true));
            Assert.True(report.Cancelled);
            Assert.Empty(_calls);
            Assert.Equal(OperationState.Skipped, spec.ExecutionPlan[0].State);
        }

        [Fact]
        public void ComDisconnectInHandler_IsReportedAsCadDisconnected()
        {
            var reg = new CadOperationRegistry().Register(new DelegateCadOperationHandler(
                new CadOperationDescriptor { OperationType = "A" }, _ => throw new System.Runtime.InteropServices.COMException("x", unchecked((int)0x80010108))));
            var report = new CadPlanExecutor(reg, new OperationLogger(_root)).Execute(Plan(Step("S1", "A")), _root);
            Assert.Equal(CatiaAIAssistant.Core.Errors.ErrorKind.CadDisconnected, report.ErrorKind);
        }

        /// <summary>Every operation the engineering layer can plan must exist in the CATIA allowlist (source-level check; CATIA project is Windows-only).</summary>
        [Fact]
        public void EveryPlannedOperation_IsRegisteredInCatiaCatalog()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CatiaAIAssistant.sln"))) dir = dir.Parent;
            string catalog = File.ReadAllText(Path.Combine(dir!.FullName, "src", "CatiaAIAssistant.Infrastructure.Catia", "CatiaOperationHandlers.cs"));
            string engine = File.ReadAllText(Path.Combine(dir.FullName, "src", "CatiaAIAssistant.Engineering", "EngineeringIntentAndCalculationEngine.cs"));

            var registered = Regex.Matches(catalog, @"(?:Part|Assembly)\(""([A-Za-z]+)""").Select(m => m.Groups[1].Value).ToHashSet();
            var planned = Regex.Matches(engine, @"OperationType\s*=\s*""([A-Za-z]+)""").Select(m => m.Groups[1].Value).ToHashSet();

            Assert.NotEmpty(planned);
            Assert.Empty(planned.Except(registered));
        }

        public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    }
}
