using CatiaAIAssistant.Core.Language;
using CatiaAIAssistant.Engineering.Requirements;

namespace CatiaAIAssistant.Tests.Requirements
{
    public class RequirementBuilderTests
    {
        private readonly ParameterExtractor _x = new();
        private readonly RequirementBuilder _b = new();

        private RequirementSpecification Build(ProblemTemplate t, string text) => _b.Build(t, _x.Extract(text));

        [Fact]
        public void ShaftWithoutTorque_AsksRequiredQuestionsFirst_InPriorityOrder()
        {
            var spec = Build(ProblemTemplates.Shaft, "یک محور برای انتقال گشتاور طراحی کن");
            Assert.False(spec.IsReadyForDesign);
            Assert.Equal("Torque", spec.OpenQuestions[0].ParameterKey);
            Assert.Equal("Speed", spec.OpenQuestions[1].ParameterKey);
            Assert.True(spec.OpenQuestions.Select(q => q.Priority).SequenceEqual(spec.OpenQuestions.Select(q => q.Priority).OrderBy(p => p)));
            Assert.All(spec.OpenQuestions, q => Assert.False(string.IsNullOrWhiteSpace(q.DesignEffect)));
            Assert.Contains("Torque", spec.Unknowns);
        }

        [Fact]
        public void ProvidedData_IsNeverAskedAgain()
        {
            var spec = Build(ProblemTemplates.Shaft, "شفت با گشتاور 120 N·m و سرعت دورانی 1450 rpm");
            Assert.DoesNotContain(spec.OpenQuestions, q => q.ParameterKey is "Torque" or "Speed");
        }

        [Fact]
        public void PowerSatisfiesTorqueNeed()
        {
            var spec = Build(ProblemTemplates.Shaft, "شفت با توان 5 kW و سرعت دورانی 1450 rpm");
            Assert.DoesNotContain(spec.OpenQuestions, q => q.ParameterKey == "Torque");
        }

        [Fact]
        public void SafetyCriticalAssumptions_AreRecorded_ButStillNeedApproval()
        {
            var spec = Build(ProblemTemplates.Shaft, "شفت با گشتاور 120 N·m و سرعت دورانی 1450 rpm");
            var bending = spec.Assumptions.Single(a => a.ParameterKey == "BendingMoment");
            Assert.True(bending.RequiresApproval);
            Assert.Contains(spec.OpenQuestions, q => q.ParameterKey == "BendingMoment" && q.SafetyCritical);
            Assert.False(spec.IsReadyForDesign);   // not until safety assumptions are confirmed
            var material = spec.Assumptions.Single(a => a.ParameterKey == "Material");
            Assert.False(material.RequiresApproval);
            Assert.DoesNotContain(spec.OpenQuestions, q => q.ParameterKey == "Material");
        }

        [Fact]
        public void FullyProvidedShaft_IsReady()
        {
            var spec = Build(ProblemTemplates.Shaft, "شفت با گشتاور 120 N·m و سرعت دورانی 1450 rpm و گشتاور خمشی 80 N·m و ضریب اطمینان 2.5");
            Assert.True(spec.IsReadyForDesign, string.Join(" | ", spec.OpenQuestions.Select(q => q.ParameterKey).Concat(spec.Conflicts.Select(c => c.Description))));
        }

        [Fact]
        public void RequirementIds_AreUniqueAndSequential()
        {
            var spec = Build(ProblemTemplates.SpurGear, "چرخ‌دنده ماژول 2 و 24 دندانه مطابق ISO 6336");
            var ids = spec.Requirements.Select(r => r.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.Equal("REQ-001", ids[0]);
            Assert.All(ids, id => Assert.Matches(@"^REQ-\d{3}$", id));
            Assert.Contains(spec.Requirements, r => r.Category == RequirementCategory.Standard && r.Statement.Contains("ISO 6336"));
        }

        [Fact]
        public void DefaultUnit_RequiresApproval()
        {
            var spec = Build(ProblemTemplates.SpurGear, "ماژول 2 و تعداد دندانه 24");
            Assert.True(spec.Find("Module")!.RequiresApproval);
            Assert.False(spec.Find("Teeth")!.RequiresApproval);
        }

        [Fact]
        public void RodShorterThanCrank_IsAConflict()
        {
            var spec = Build(ProblemTemplates.SliderCrank, "شعاع لنگ 60 mm و طول شاتون 40 mm");
            Assert.Single(spec.Conflicts);
            Assert.Equal(2, spec.Conflicts[0].RequirementIds.Count);
            Assert.False(spec.IsReadyForDesign);
        }

        [Fact]
        public void StrokeVsCrankRadius_Conflict()
        {
            var spec = Build(ProblemTemplates.SliderCrank, "شعاع لنگ 30 mm و کورس 80 mm و طول شاتون 110 mm");
            Assert.Contains(spec.Conflicts, c => c.Description.Contains("کورس"));
        }

        [Fact]
        public void CenterDistanceMismatch_IsAConflict()
        {
            var spec = Build(ProblemTemplates.GearPair, "ماژول 2 mm و تعداد دندانه 20 و نسبت انتقال 2 و فاصله محورها 70 mm");
            Assert.Contains(spec.Conflicts, c => c.Description.Contains("فاصلهٔ محورها"));
            var ok = Build(ProblemTemplates.GearPair, "ماژول 2 mm و تعداد دندانه 20 و نسبت انتقال 2 و فاصله محورها 60 mm");
            Assert.DoesNotContain(ok.Conflicts, c => c.Description.Contains("فاصلهٔ محورها"));
        }

        [Fact]
        public void PowerTorqueSpeed_Inconsistent()
        {
            // 100 N·m at 1500 rpm = 15.7 kW, not 5 kW
            var spec = Build(ProblemTemplates.Shaft, "توان 5 kW و گشتاور 100 N·m و سرعت دورانی 1500 rpm");
            Assert.Contains(spec.Conflicts, c => c.Description.Contains("P = T·ω"));
            var ok = Build(ProblemTemplates.Shaft, "توان 15.708 kW و گشتاور 100 N·m و سرعت دورانی 1500 rpm");
            Assert.DoesNotContain(ok.Conflicts, c => c.Description.Contains("P = T·ω"));
        }

        [Fact]
        public void ConflictingValues_FromExtraction_BecomeConflicts()
        {
            var spec = Build(ProblemTemplates.SpurGear, "ماژول 2 و ماژول 3 و 24 دندانه");
            Assert.Contains(spec.Conflicts, c => c.Description.Contains("دو مقدار متفاوت"));
        }

        [Fact]
        public void UnassignedNumbers_BecomeRisks()
        {
            var spec = Build(ProblemTemplates.Block, "بلوک 100 در 50 در 20");
            Assert.Equal(3, spec.Risks.Count(r => r.Contains("نسبت داده نشد")));
            Assert.False(spec.IsReadyForDesign);
        }

        [Fact]
        public void AlreadyAsked_OptionalOrAssumable_IsNotRepeated_ButRequiredIs()
        {
            var planner = new ClarificationPlanner();
            var plan = planner.Plan(ProblemTemplates.Shaft, Array.Empty<string>(), new[] { "Torque", "Supports" });
            Assert.Contains(plan.Questions, q => q.ParameterKey == "Torque");
            Assert.DoesNotContain(plan.Questions, q => q.ParameterKey == "Supports");
        }

        [Fact]
        public void HowToObtain_IsOfferedForMeasurableData()
        {
            var spec = Build(ProblemTemplates.Shaft, "شفت");
            Assert.False(string.IsNullOrWhiteSpace(spec.OpenQuestions.Single(q => q.ParameterKey == "Torque").HowToObtain));
        }

        [Fact]
        public void CustomConsistencyRule_CanBePlugged()
        {
            var b = new RequirementBuilder(new[] { new AlwaysConflict() });
            var spec = b.Build(ProblemTemplates.Block, _x.Extract("طول 10 mm و عرض 10 mm و ارتفاع 10 mm"));
            Assert.Single(spec.Conflicts);
        }

        private sealed class AlwaysConflict : IRequirementConsistencyRule
        {
            public IEnumerable<RequirementConflict> Check(RequirementSpecification spec)
            { yield return new RequirementConflict { Description = "custom" }; }
        }

        [Fact]
        public void Catalog_ContainsAllTemplates_AndIsExtensible()
        {
            foreach (var t in new[] { "Shaft", "SpurGear", "GearPair", "SliderCrank", "Bearing", "CompressionSpring", "Block" })
                Assert.True(ProblemTemplateCatalog.Default.TryGet(t, out _), t);
            var c = new ProblemTemplateCatalog().Register(new ProblemTemplate { ProblemType = "X" });
            Assert.True(c.TryGet("X", out _));
        }

        [Fact]
        public void TemplateParameterKeys_ExistInVocabulary_OrAreQualitative()
        {
            var qualitative = new HashSet<string> { "Material", "Supports" };
            foreach (var t in ProblemTemplateCatalog.Default.All)
                foreach (var i in t.Items)
                    Assert.True(qualitative.Contains(i.ParameterKey) || ParameterVocabulary.Default.TryGet(i.ParameterKey, out _), $"{t.ProblemType}.{i.ParameterKey}");
        }
    }
}
