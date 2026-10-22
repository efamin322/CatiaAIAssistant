using CatiaAIAssistant.Engineering;

namespace CatiaAIAssistant.Tests.Engineering
{
    /// <summary>
    /// Characterization tests: they lock in the behaviour of the Phase-1 rule-based engine so that the
    /// M6/M9/M11 refactors (handler registry, named-parameter extraction) cannot silently change results.
    /// Reference values are hand-computed from the textbook formulas cited in each assertion.
    /// </summary>
    public class IntentEngineCharacterizationTests
    {
        private readonly EngineeringIntentAndCalculationEngine _engine = new();

        [Theory]
        [InlineData("یک چرخ‌دنده ساده با ماژول 3 و تعداد دندانه 24", EngineeringDomain.SpurGearTransmission)]
        [InlineData("design a spur gear module 2 teeth 30", EngineeringDomain.SpurGearTransmission)]
        [InlineData("یک شفت پله‌ای طراحی کن", EngineeringDomain.SteppedShaftAndBearing)]
        [InlineData("کوپلینگ فلنجی برای شفت 22", EngineeringDomain.SteppedShaftAndBearing)] // 'شفت' wins: documents current keyword precedence (A-06)
        [InlineData("یک کوپلینگ فلنجی 22 میلی‌متری", EngineeringDomain.FlangeCoupling)]
        [InlineData("مکانیزم میل‌لنگ و پیستون", EngineeringDomain.CrankSliderPiston)]
        [InlineData("یک کوادکوپتر 450", EngineeringDomain.QuadcopterSystem)]
        [InlineData("یک ماشین اسباب‌بازی", EngineeringDomain.ToyCarSystem)]
        [InlineData("یک بلوک ۴۰ در ۲۰ در ۱۰", EngineeringDomain.BasicBlockOrProfile)]
        public void DetectsDomainFromKeywords(string prompt, EngineeringDomain expected)
            => Assert.Equal(expected, _engine.AnalyzeUserRequest(prompt).Domain);

        [Theory]
        [InlineData("برای من یک هواپیمای مسافربری طراحی کن")]
        [InlineData("")]
        [InlineData("   ")]
        public void UnknownRequest_IsBlockedAndAsksForClarification(string prompt)
        {
            var spec = _engine.AnalyzeUserRequest(prompt);
            Assert.Equal(EngineeringDomain.Unknown, spec.Domain);
            Assert.False(spec.CanExecuteNow);
            Assert.Empty(spec.ExecutionPlan);
            Assert.NotEmpty(spec.ClarifyingQuestions);
        }

        [Fact]
        public void NullRequest_DoesNotThrow()
        {
            var spec = _engine.AnalyzeUserRequest(null!);
            Assert.Equal(EngineeringDomain.Unknown, spec.Domain);
        }

        [Fact]
        public void Gear_PitchDiameter_IsModuleTimesTeeth()
        {
            // d = m·z = 3 × 24 = 72 mm
            var spec = _engine.AnalyzeUserRequest("یک چرخ‌دنده با ماژول 3 و تعداد دندانه 24 و عرض 25");
            Assert.Equal(3.0, spec.Parameters["Module"].Value, 9);
            Assert.Equal(24.0, spec.Parameters["TeethZ1"].Value, 9);
            Assert.Equal(72.0, spec.Parameters["PitchDia1"].Value, 9);
            Assert.Equal(ParameterStatus.SpecifiedByUser, spec.Parameters["Module"].Status);
            Assert.Equal(ParameterStatus.Calculated, spec.Parameters["PitchDia1"].Status);
        }

        [Fact]
        public void GearPair_CenterDistance_FollowsStandardFormula()
        {
            // z2 = z1·i = 18 × 2 = 36 ; a = m(z1+z2)/2 = 3 × 54 / 2 = 81 mm
            var spec = _engine.AnalyzeUserRequest("یک مجموعه دو چرخ‌دنده با ماژول 3 و دندانه 18 و نسبت انتقال 2");
            Assert.Equal(36.0, spec.Parameters["TeethZ2"].Value, 9);
            Assert.Equal(81.0, spec.Parameters["CenterDistance"].Value, 9);
        }

        [Fact]
        public void Gear_LowToothCount_RaisesUndercutWarning()
        {
            var spec = _engine.AnalyzeUserRequest("چرخ‌دنده ماژول 2 دندانه 12");
            Assert.Contains(spec.ContradictionsAndWarnings, w => w.Contains("۱۷"));
        }

        [Fact]
        public void Shaft_AllowableTorque_MatchesSolidShaftFormula()
        {
            // T = π d³ τ / 16 with d = 20 mm, τ = 45 MPa -> π·(0.02)³·45e6/16 = 70.6858 N·m
            var spec = _engine.AnalyzeUserRequest("یک شفت پله‌ای با ابعاد 20 در 35 و 28 در 60 و 20 در 35");
            var calc = Assert.Single(spec.Calculations, c => c.Unit == "N.m");
            Assert.Equal(70.69, calc.ResultValue, 2);
            Assert.True(double.IsFinite(calc.ResultValue));
        }

        [Fact]
        public void Shaft_WithoutNumbers_FlagsAssumptions()
        {
            var spec = _engine.AnalyzeUserRequest("یک شفت پله‌ای طراحی کن");
            Assert.True(spec.HasAssumedParameters);
            Assert.All(spec.Parameters.Values, p => Assert.Equal(ParameterStatus.AssumedByEngineeringRule, p.Status));
        }

        [Fact]
        public void CrankSlider_RodShorterThanCrank_IsBlocked()
        {
            var spec = _engine.AnalyzeUserRequest("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 60 و شاتون 40 و قطر پیستون 65");
            Assert.False(spec.CanExecuteNow);
        }

        [Fact]
        public void CrankSlider_ShortRodRatio_IsReplacedOnlyAsFlaggedAssumption()
        {
            // r = 30, L = 60 -> L/r = 2 < 2.5 -> proposed L = 3.5·30 = 105 mm, flagged
            var spec = _engine.AnalyzeUserRequest("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و شاتون 60 و قطر پیستون 65");
            Assert.Equal(105.0, spec.Parameters["RodLength"].Value, 9);
            Assert.Equal(ParameterStatus.AssumedByEngineeringRule, spec.Parameters["RodLength"].Status);
            Assert.True(spec.HasAssumedParameters);
            Assert.NotEmpty(spec.Assumptions);
        }

        [Fact]
        public void CrankSlider_Stroke_IsTwiceCrankRadius()
        {
            var spec = _engine.AnalyzeUserRequest("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و شاتون 110 و قطر پیستون 65");
            Assert.Equal(60.0, spec.Parameters["Stroke"].Value, 9);
            Assert.Equal(110.0, spec.Parameters["RodLength"].Value, 9);
            Assert.Equal(ParameterStatus.SpecifiedByUser, spec.Parameters["RodLength"].Status);
            Assert.True(spec.CanExecuteNow);
        }

        [Fact]
        public void EveryExecutablePlan_HasPositiveFiniteInputs()
        {
            string[] prompts =
            {
                "یک چرخ‌دنده ساده با ماژول 3 و تعداد دندانه 24 و عرض 25",
                "یک شفت پله‌ای طراحی کن",
                "یک کوادکوپتر با قطر 450 میلی‌متر",
                "یک ماشین اسباب‌بازی با طول 160 و عرض 90 و قطر چرخ 45",
            };
            foreach (var p in prompts)
            {
                var spec = _engine.AnalyzeUserRequest(p);
                Assert.NotEmpty(spec.ExecutionPlan);
                foreach (var step in spec.ExecutionPlan)
                {
                    Assert.False(string.IsNullOrWhiteSpace(step.OperationType));
                    Assert.All(step.NumericInputs.Values, v => Assert.True(double.IsFinite(v) && v > 0, $"{p}: {step.StepId}"));
                }
            }
        }

        [Fact]
        public void Report_ContainsTitleAndParameters()
        {
            var spec = _engine.AnalyzeUserRequest("یک چرخ‌دنده با ماژول 3 و تعداد دندانه 24");
            string report = EngineeringIntentAndCalculationEngine.FormatSpecificationReport(spec);
            Assert.Contains(spec.ProjectTitle, report);
            Assert.Contains("ماژول", report);
        }
    }
}
