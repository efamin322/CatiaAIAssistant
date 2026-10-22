using CatiaAIAssistant.Core.Language;
using CatiaAIAssistant.Core.Units;
using U = CatiaAIAssistant.Core.Units.Units;

namespace CatiaAIAssistant.Tests.Language
{
    public class ParameterExtractorTests
    {
        private readonly ParameterExtractor _x = new();

        [Fact]
        public void PersianGearRequest_NamedParameters()
        {
            var r = _x.Extract("یک چرخ‌دنده با ماژول ۳ و تعداد دندانه ۲۴ و عرض دندانه 25 mm");
            Assert.Equal(3, r.ValueIn("Module", U.Millimeter));
            Assert.Equal(24, r.ValueIn("Teeth", U.Unitless));
            Assert.Equal(25, r.ValueIn("FaceWidth", U.Millimeter));
            Assert.True(r.Get("FaceWidth")!.UnitWasExplicit);
            Assert.False(r.Get("Module")!.UnitWasExplicit);
            Assert.Contains(r.Issues, i => i.Kind == ExtractionIssueKind.DefaultUnitAssumed && i.ParameterKey == "Module");
            Assert.False(r.HasBlockingIssues);
        }

        [Fact]
        public void OrderIndependent_PostfixNames()
        {
            var r = _x.Extract("چرخ‌دنده با 24 دندانه و ماژول 2.5");
            Assert.Equal(24, r.ValueIn("Teeth", U.Unitless));
            Assert.Equal(2.5, r.ValueIn("Module", U.Millimeter));
        }

        [Fact]
        public void EnglishShaftRequest_ConvertsUnits()
        {
            var r = _x.Extract("Design a shaft: torque = 120 N·m, speed 1450 rpm, safety factor 2, shaft diameter 1.25 in");
            Assert.Equal(120, r.ValueIn("Torque", U.NewtonMeter));
            Assert.Equal(1450, r.ValueIn("Speed", U.Rpm));
            Assert.Equal(2, r.ValueIn("SafetyFactor", U.Unitless));
            Assert.Equal(31.75, r.ValueIn("ShaftDiameter", U.Millimeter)!.Value, 10);
            Assert.False(r.HasBlockingIssues);
            Assert.True(r.Confidence > 0.8);
        }

        [Fact]
        public void PersianUnits_AreRecognised()
        {
            var r = _x.Extract("گشتاور 50 نیوتن متر و سرعت دورانی 1500 دور بر دقیقه و توان 7.5 کیلووات");
            Assert.Equal(50, r.ValueIn("Torque", U.NewtonMeter));
            Assert.Equal(1500, r.ValueIn("Speed", U.Rpm));
            Assert.Equal(7.5, r.ValueIn("Power", U.Kilowatt));
            Assert.All(r.Parameters, p => Assert.True(p.UnitWasExplicit));
        }

        [Fact]
        public void UnitDimensionMismatch_IsBlocking()
        {
            var r = _x.Extract("گشتاور 50 mm");
            Assert.False(r.Has("Torque"));
            Assert.Contains(r.Issues, i => i.Kind == ExtractionIssueKind.UnitDimensionMismatch && i.Blocking);
        }

        [Fact]
        public void ConflictingValues_AreReported_NotOverwritten()
        {
            var r = _x.Extract("ماژول 3 ... بعداً: ماژول 4");
            Assert.Equal(3, r.ValueIn("Module", U.Millimeter));
            Assert.Contains(r.Issues, i => i.Kind == ExtractionIssueKind.ConflictingValues && i.Blocking);
        }

        [Fact]
        public void SameValueTwice_InDifferentUnits_IsNotAConflict()
        {
            var r = _x.Extract("طول 1 in و دوباره طول 25.4 mm");
            Assert.DoesNotContain(r.Issues, i => i.Kind == ExtractionIssueKind.ConflictingValues);
        }

        [Fact]
        public void NonIntegerTeeth_IsInvalid()
        {
            var r = _x.Extract("تعداد دندانه 20.5");
            Assert.False(r.Has("Teeth"));
            Assert.Contains(r.Issues, i => i.Kind == ExtractionIssueKind.InvalidValue && i.ParameterKey == "Teeth");
        }

        [Fact]
        public void NegativeLength_IsInvalid()
        {
            var r = _x.Extract("طول -20 mm");
            Assert.Contains(r.Issues, i => i.Kind == ExtractionIssueKind.InvalidValue && i.ParameterKey == "Length");
        }

        [Fact]
        public void UnassignedNumbers_AreReported()
        {
            var r = _x.Extract("یک بلوک 100 در 50 در 20");
            Assert.Equal(3, r.Unassigned.Count);
            Assert.All(r.Issues, i => Assert.Equal(ExtractionIssueKind.UnassignedNumber, i.Kind));
            Assert.Empty(r.Parameters);
            Assert.Equal(0, r.Confidence);
        }

        [Fact]
        public void Qualifiers_AreDetected()
        {
            var r = _x.Extract("ضریب اطمینان حداقل 2 و عمر هدف حدود 20000 h و دمای کاری حداکثر 80 °C");
            Assert.Equal(ValueQualifier.AtLeast, r.Get("SafetyFactor")!.Qualifier);
            Assert.Equal(ValueQualifier.Approximately, r.Get("Life")!.Qualifier);
            Assert.Equal(ValueQualifier.AtMost, r.Get("Temperature")!.Qualifier);
            Assert.Equal(Dimension.Temperature, r.Get("Temperature")!.Value.Dimension);
        }

        [Fact]
        public void Standards_AreCaptured_AndTheirNumbersIgnored()
        {
            var r = _x.Extract("چرخ‌دنده مطابق ISO 6336 و DIN 3990 با ماژول 2");
            Assert.Contains("ISO 6336", r.Standards);
            Assert.Contains("DIN 3990", r.Standards);
            Assert.Empty(r.Unassigned);
            Assert.Equal(2, r.ValueIn("Module", U.Millimeter));
        }

        [Fact]
        public void LongestNameWins()
        {
            var r = _x.Extract("قطر پیستون 65 و قطر شفت 20 و قطر 40");
            Assert.Equal(65, r.ValueIn("PistonDiameter", U.Millimeter));
            Assert.Equal(20, r.ValueIn("ShaftDiameter", U.Millimeter));
            Assert.Equal(40, r.ValueIn("Diameter", U.Millimeter));
        }

        [Fact]
        public void SliderCrank_PersianSentence()
        {
            var r = _x.Extract("مکانیزم میل‌لنگ و پیستون با شعاع لنگ 30 و شاتون 110 و قطر پیستون 65");
            Assert.Equal(30, r.ValueIn("CrankRadius", U.Millimeter));
            Assert.Equal(110, r.ValueIn("RodLength", U.Millimeter));
            Assert.Equal(65, r.ValueIn("PistonDiameter", U.Millimeter));
        }

        [Fact]
        public void SourceText_IsTraceable()
        {
            var r = _x.Extract("گشتاور برابر با 50 N·m");
            Assert.Equal("گشتاور برابر با 50 N·m", r.Get("Torque")!.SourceText);
        }

        [Fact]
        public void Vocabulary_IsExtensible()
        {
            var v = new ParameterVocabulary().Register(new ParameterDefinition
            {
                Key = "HelixAngle", PersianName = "زاویه مارپیچ", Dimension = Dimension.Angle, DefaultUnit = U.Degree,
                Aliases = new[] { "زاویه مارپیچ", "helix angle" }
            });
            var r = new ParameterExtractor(v).Extract("helix angle 15°");
            Assert.Equal(15, r.ValueIn("HelixAngle", U.Degree));
            Assert.Throws<InvalidOperationException>(() => v.Register(new ParameterDefinition
            {
                Key = "Other", Dimension = Dimension.None, DefaultUnit = U.Unitless, Aliases = new[] { "helix angle" }
            }));
        }

        [Fact]
        public void EmptyInput_GivesEmptyResult()
        {
            var r = _x.Extract(null);
            Assert.Empty(r.Parameters);
            Assert.Empty(r.Issues);
        }
    }
}
