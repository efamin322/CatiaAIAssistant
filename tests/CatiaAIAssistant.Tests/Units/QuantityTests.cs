using CatiaAIAssistant.Core.Units;
using U = CatiaAIAssistant.Core.Units.Units;

namespace CatiaAIAssistant.Tests.Units
{
    public class QuantityTests
    {
        private static void Near(double expected, double actual, double rel = 1e-12) =>
            Assert.True(Math.Abs(expected - actual) <= rel * Math.Max(1, Math.Abs(expected)), $"expected {expected:R}, got {actual:R}");

        [Fact]
        public void StoresEnteredValueAndUnit()
        {
            var q = Quantity.Of(30, U.Millimeter);
            Assert.Equal(30, q.Value);
            Assert.Same(U.Millimeter, q.Unit);
            Near(0.03, q.SiValue);
        }

        [Theory]
        [InlineData(1, "in", 25.4, "mm")]
        [InlineData(1, "ft", 304.8, "mm")]
        [InlineData(1, "MPa", 1, "N/mm²")]
        [InlineData(1, "bar", 100, "kPa")]
        [InlineData(1, "psi", 6.894757293168, "kPa")]
        [InlineData(1, "kN·m", 1e6, "N·mm")]
        [InlineData(1, "hp", 0.74569987158227022, "kW")]
        [InlineData(1, "lbf", 4.4482216152605, "N")]
        [InlineData(1, "kgf", 9.80665, "N")]
        [InlineData(36, "km/h", 10, "m/s")]
        [InlineData(1, "Wh", 3.6, "kJ")]
        [InlineData(180, "°", Math.PI, "rad")]
        [InlineData(1, "rev", 360, "°")]
        [InlineData(1, "L", 1000, "cm³")]
        [InlineData(1, "N/mm", 1000, "N/m")]
        [InlineData(5, "%", 0.05, "-")]
        public void Conversions_AreExact(double v, string from, double expected, string to)
        {
            var r = UnitRegistry.Default;
            Near(expected, Quantity.Of(v, r.Resolve(from)).ValueIn(r.Resolve(to)));
        }

        [Fact]
        public void RpmAndRadPerSecond()
        {
            Near(2 * Math.PI * 1500 / 60, Quantity.Of(1500, U.Rpm).ValueIn(U.RadianPerSecond));
            Near(60.0 / (2 * Math.PI), Quantity.Of(1, U.RadianPerSecond).ValueIn(U.Rpm));
            Near(6, Quantity.Of(1, U.Rpm).ValueIn(U.DegreePerSecond));
        }

        [Fact]
        public void AbsoluteTemperatureConversions()
        {
            Near(293.15, Quantity.Of(20, U.Celsius).ValueIn(U.Kelvin));
            Near(212, Quantity.Of(100, U.Celsius).ValueIn(U.Fahrenheit));
            Near(-40, Quantity.Of(-40, U.Fahrenheit).ValueIn(U.Celsius));
            Near(0, Quantity.Of(32, U.Fahrenheit).ValueIn(U.Celsius), 1e-12);
        }

        [Fact]
        public void TemperatureDifference_HasNoOffset()
        {
            Near(18, Quantity.Of(10, U.DeltaCelsius).ValueIn(U.DeltaFahrenheit));
            Near(10, Quantity.Of(10, U.DeltaCelsius).ValueIn(U.DeltaKelvin));
            // absolute and difference are different dimensions: explicit conversion is refused
            Assert.Throws<DimensionMismatchException>(() => Quantity.Of(10, U.Celsius).To(U.DeltaCelsius));
        }

        [Fact]
        public void TemperatureArithmetic_FollowsPointIntervalRules()
        {
            var t1 = Quantity.Of(80, U.Celsius);
            var t2 = Quantity.Of(20, U.Celsius);
            var dt = t1 - t2;
            Assert.Equal(Dimension.TemperatureDifference, dt.Dimension);
            Assert.Same(U.DeltaCelsius, dt.Unit);
            Near(60, dt.Value);

            var t3 = t2 + Quantity.Of(18, U.DeltaFahrenheit);   // +10 K
            Assert.Same(U.Celsius, t3.Unit);
            Near(30, t3.Value);
            Near(70, (t1 - Quantity.Of(10, U.DeltaKelvin)).Value);

            Assert.Throws<DimensionMismatchException>(() => t1 + t2);
            Assert.Throws<DimensionMismatchException>(() => t1 * 2);
            Assert.Throws<DimensionMismatchException>(() => t1 * Quantity.Of(1, U.Meter));
            Assert.Equal(Dimension.TemperatureDifference, (dt * 2).Dimension);
        }

        [Fact]
        public void IncompatibleDimensions_CannotBeAddedOrCompared()
        {
            var len = Quantity.Of(10, U.Millimeter);
            var force = Quantity.Of(10, U.Newton);
            Assert.Throws<DimensionMismatchException>(() => len + force);
            Assert.Throws<DimensionMismatchException>(() => len - force);
            Assert.Throws<DimensionMismatchException>(() => len < force);
            Assert.Throws<DimensionMismatchException>(() => Quantity.Of(1, U.NewtonMeter).To(U.Rpm));
            Assert.Throws<DimensionMismatchException>(() => len.To(U.Newton));
        }

        [Fact]
        public void AddingMixedUnits_KeepsLeftUnit()
        {
            var sum = Quantity.Of(1, U.Meter) + Quantity.Of(250, U.Millimeter);
            Assert.Same(U.Meter, sum.Unit);
            Near(1.25, sum.Value);
            Assert.True(Quantity.Of(1, U.Inch) > Quantity.Of(25, U.Millimeter));
            Assert.True(Quantity.Of(1, U.Inch) == Quantity.Of(25.4, U.Millimeter) || Quantity.Of(1, U.Inch).ApproximatelyEquals(Quantity.Of(25.4, U.Millimeter)));
        }

        [Fact]
        public void DerivedQuantities_HaveCorrectDimensions()
        {
            var torque = Quantity.Of(100, U.Newton) * Quantity.Of(0.5, U.Meter);
            Assert.Equal(Dimension.Torque, torque.Dimension);
            Near(50, torque.ValueIn(U.NewtonMeter));

            var stress = Quantity.Of(1000, U.Newton) / Quantity.Of(10, U.SquareMillimeter);
            Assert.Equal(Dimension.Stress, stress.Dimension);
            Near(100, stress.ValueIn(U.Megapascal));

            // P = T·ω requires explicitly treating rad as dimensionless
            var power = (Quantity.Of(100, U.NewtonMeter) * Quantity.Of(1500, U.Rpm)).DropAngle();
            Assert.Equal(Dimension.Power, power.Dimension);
            Near(100 * 1500 * 2 * Math.PI / 60 / 1000, power.ValueIn(U.Kilowatt));

            var area = Quantity.Of(10, U.Millimeter) * Quantity.Of(20, U.Millimeter);
            Near(200, area.ValueIn(U.SquareMillimeter));
            Assert.Throws<DivideByZeroException>(() => stress / Quantity.Of(0, U.Pascal));
        }

        [Fact]
        public void DisplayPrecision_IsSeparateFromStoredValue()
        {
            var q = Quantity.Of(1, U.Inch).To(U.Millimeter);
            Assert.Equal("25.4 mm", q.Format(4));
            var third = Quantity.Of(1.0 / 3.0, U.Meter);
            Assert.Equal("0.333 m", third.Format(3));
            Assert.Equal(1.0 / 3.0, third.Value);  // unchanged after formatting
            Assert.Equal("333.33 mm", third.Format(U.Millimeter, 5));
        }

        [Fact]
        public void RoundTripConversion_DoesNotAccumulateError()
        {
            var q = Quantity.Of(123.456789, U.Millimeter);
            var back = q.To(U.Inch).To(U.Foot).To(U.Meter).To(U.Millimeter);
            Assert.True(back.ApproximatelyEquals(q, 1e-14));
        }

        [Fact]
        public void ApproximateEquality_UsesExplicitTolerance()
        {
            var a = Quantity.Of(10.000, U.Millimeter);
            var b = Quantity.Of(10.004, U.Millimeter);
            Assert.True(a.ApproximatelyEquals(b, Quantity.Of(0.005, U.Millimeter)));
            Assert.False(a.ApproximatelyEquals(b, Quantity.Of(0.001, U.Millimeter)));
            Assert.False(a == b); // never silently equal by rounding
        }

        [Fact]
        public void NaN_IsRejected() => Assert.Throws<ArgumentException>(() => Quantity.Of(double.NaN, U.Meter));
    }
}
