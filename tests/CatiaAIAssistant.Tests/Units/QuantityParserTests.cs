using CatiaAIAssistant.Core.Units;
using U = CatiaAIAssistant.Core.Units.Units;

namespace CatiaAIAssistant.Tests.Units
{
    public class QuantityParserTests
    {
        private readonly QuantityParser _p = new();

        [Theory]
        [InlineData("30 mm", 30, "mm")]
        [InlineData("۳۰ میلی‌متر", 30, "mm")]
        [InlineData("۲٫۵ کیلونیوتن", 2.5, "kN")]
        [InlineData("2.5kN", 2.5, "kN")]
        [InlineData("1500 rpm", 1500, "rpm")]
        [InlineData("1500 دور بر دقیقه", 1500, "rpm")]
        [InlineData("20 °C", 20, "°C")]
        [InlineData("20 درجه سانتی‌گراد", 20, "°C")]
        [InlineData("45 درجه", 45, "°")]
        [InlineData("50 N.m", 50, "N·m")]
        [InlineData("50 نیوتن متر", 50, "N·m")]
        [InlineData("210 GPa", 210, "GPa")]
        [InlineData("3,5 mm", 3.5, "mm")]
        [InlineData("1e-3 m", 0.001, "m")]
        [InlineData("10 mm^2", 10, "mm²")]
        [InlineData("7.5 kW", 7.5, "kW")]
        public void Parses(string text, double value, string symbol)
        {
            var q = _p.Parse(text);
            Assert.Equal(value, q.Value, 12);
            Assert.Equal(symbol, q.Unit.Symbol);
        }

        [Fact]
        public void UnknownUnit_IsReported_NotGuessed()
        {
            Assert.False(_p.TryParse("12 furlongs", out _, out var err));
            Assert.Contains("ناشناخته", err);
        }

        [Fact]
        public void MissingUnit_RequiresExplicitDefault()
        {
            Assert.False(_p.TryParse("12", out _, out var err));
            Assert.Contains("مشخص نشده", err);
            Assert.True(_p.TryParse("12", out var q, out _, defaultUnit: U.Millimeter));
            Assert.Same(U.Millimeter, q.Unit);
        }

        [Fact]
        public void ExpectedDimension_IsEnforced()
        {
            Assert.False(_p.TryParse("12 N", out _, out var err, expected: Dimension.Length));
            Assert.Contains("مورد انتظار", err);
            Assert.True(_p.TryParse("12 in", out _, out _, expected: Dimension.Length));
        }

        [Fact]
        public void ShortSymbols_AreCaseSensitive()
        {
            Assert.Same(U.Megapascal, UnitRegistry.Default.Resolve("MPa"));
            Assert.False(UnitRegistry.Default.TryResolve("mpa", out _));
            Assert.Same(U.Rpm, UnitRegistry.Default.Resolve("RPM"));
            Assert.Same(U.Newton, UnitRegistry.Default.Resolve("NEWTON"));
        }

        [Fact]
        public void Registry_IsExtensible_AndRejectsConflictingAliases()
        {
            var r = new UnitRegistry();
            var mil = new Unit("mil", Dimension.Length, 25.4e-6, persianName: "میل");
            r.Register(mil, "thou");
            Assert.Same(mil, r.Resolve("thou"));
            Assert.Throws<InvalidOperationException>(() => r.Register(U.Millimeter, "thou"));
        }

        [Fact]
        public void EveryCatalogUnit_IsResolvableBySymbol()
        {
            foreach (var u in UnitRegistry.Default.All)
                Assert.Same(u, UnitRegistry.Default.Resolve(u.Symbol));
        }

        [Fact]
        public void Spec11_AllRequiredQuantityKinds_HaveUnits()
        {
            var dims = UnitRegistry.Default.All.Select(u => u.Dimension).ToHashSet();
            foreach (var d in new[] { Dimension.Length, Dimension.Area, Dimension.Volume, Dimension.Mass, Dimension.Force,
                                      Dimension.Torque, Dimension.Pressure, Dimension.Strain, Dimension.Velocity,
                                      Dimension.AngularVelocity, Dimension.Acceleration, Dimension.Power, Dimension.Energy,
                                      Dimension.Temperature, Dimension.TemperatureDifference, Dimension.Angle })
                Assert.Contains(d, dims);
        }
    }
}
