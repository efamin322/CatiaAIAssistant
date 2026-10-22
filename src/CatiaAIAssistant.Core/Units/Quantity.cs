using System.Globalization;

namespace CatiaAIAssistant.Core.Units
{
    /// <summary>Thrown when an operation combines quantities of incompatible dimensions (Spec §11.3).</summary>
    public sealed class DimensionMismatchException : InvalidOperationException
    {
        public Dimension Left { get; }
        public Dimension Right { get; }
        public DimensionMismatchException(string operation, Dimension left, Dimension right)
            : base($"ابعاد ناسازگار در عملیات «{operation}»: [{left}] و [{right}].")
        { Left = left; Right = right; }
    }

    /// <summary>
    /// Immutable physical quantity. Stores the value <em>exactly as entered</em> together with its unit
    /// (Spec §11.1); every computation uses full double precision in SI and rounding happens only when
    /// formatting (Spec §11.6/§11.7).
    /// </summary>
    public readonly struct Quantity : IEquatable<Quantity>, IComparable<Quantity>
    {
        public double Value { get; }
        public Unit Unit { get; }
        public Dimension Dimension => Unit.Dimension;
        public double SiValue => Unit.ToSi(Value);

        public Quantity(double value, Unit unit)
        {
            if (unit is null) throw new ArgumentNullException(nameof(unit));
            if (double.IsNaN(value)) throw new ArgumentException("Quantity value must not be NaN.", nameof(value));
            Value = value;
            Unit = unit;
        }

        public static Quantity Of(double value, Unit unit) => new(value, unit);

        /// <summary>Explicit conversion; throws if dimensions differ (absolute vs. difference temperature included).</summary>
        public Quantity To(Unit target)
        {
            if (target.Dimension != Dimension) throw new DimensionMismatchException("تبدیل واحد", Dimension, target.Dimension);
            if (ReferenceEquals(target, Unit) || target.Equals(Unit)) return this;
            if (Unit.Offset == 0 && target.Offset == 0)
                return new Quantity(Value * (Unit.Factor / target.Factor), target); // single multiplication, no SI round-trip
            return new Quantity(target.FromSi(SiValue), target);
        }

        public double ValueIn(Unit target) => To(target).Value;

        // ---------------- arithmetic ----------------
        public static Quantity operator +(Quantity a, Quantity b)
        {
            // absolute T + ΔT = absolute T ; ΔT + ΔT = ΔT ; absolute + absolute is meaningless.
            if (a.Dimension.IsAbsoluteTemperature && b.Dimension.IsAbsoluteTemperature)
                throw new DimensionMismatchException("جمع دو دمای مطلق", a.Dimension, b.Dimension);
            if (a.Dimension.IsAbsoluteTemperature && b.Dimension == Dimension.TemperatureDifference)
                return new Quantity(a.Value + b.SiValue / a.Unit.Factor, a.Unit);
            if (b.Dimension.IsAbsoluteTemperature && a.Dimension == Dimension.TemperatureDifference)
                return b + a;
            if (a.Dimension != b.Dimension) throw new DimensionMismatchException("جمع", a.Dimension, b.Dimension);
            return new Quantity(a.Value + b.ValueIn(a.Unit), a.Unit);
        }

        public static Quantity operator -(Quantity a, Quantity b)
        {
            if (a.Dimension.IsAbsoluteTemperature && b.Dimension.IsAbsoluteTemperature)
            {
                // T1 − T2 is a temperature DIFFERENCE expressed in the Δ-unit matching a's scale.
                double deltaK = a.SiValue - b.SiValue;
                Unit du = UnitRegistry.Default.DifferenceUnitFor(a.Unit);
                return new Quantity(deltaK / du.Factor, du);
            }
            if (a.Dimension.IsAbsoluteTemperature && b.Dimension == Dimension.TemperatureDifference)
                return new Quantity(a.Value - b.SiValue / a.Unit.Factor, a.Unit);
            if (a.Dimension != b.Dimension) throw new DimensionMismatchException("تفریق", a.Dimension, b.Dimension);
            return new Quantity(a.Value - b.ValueIn(a.Unit), a.Unit);
        }

        public static Quantity operator -(Quantity a) =>
            a.Dimension.IsAbsoluteTemperature
                ? throw new DimensionMismatchException("منفی کردن دمای مطلق", a.Dimension, a.Dimension)
                : new Quantity(-a.Value, a.Unit);

        public static Quantity operator *(Quantity a, double k) => Scale(a, k);
        public static Quantity operator *(double k, Quantity a) => Scale(a, k);
        public static Quantity operator /(Quantity a, double k) => Scale(a, 1.0 / k);

        private static Quantity Scale(Quantity a, double k) =>
            a.Dimension.IsAbsoluteTemperature
                ? throw new DimensionMismatchException("ضرب دمای مطلق در عدد", a.Dimension, Dimension.None)
                : new Quantity(a.Value * k, a.Unit);

        /// <summary>Product in coherent SI; the result dimension is derived from the operands.</summary>
        public static Quantity operator *(Quantity a, Quantity b)
        {
            GuardAbsoluteTemperature(a, b, "ضرب");
            var dim = a.Dimension * b.Dimension;
            return new Quantity(a.SiValue * b.SiValue, UnitRegistry.Default.SiUnitFor(dim));
        }

        public static Quantity operator /(Quantity a, Quantity b)
        {
            GuardAbsoluteTemperature(a, b, "تقسیم");
            if (b.SiValue == 0) throw new DivideByZeroException("تقسیم بر کمیت صفر.");
            var dim = a.Dimension / b.Dimension;
            return new Quantity(a.SiValue / b.SiValue, UnitRegistry.Default.SiUnitFor(dim));
        }

        private static void GuardAbsoluteTemperature(Quantity a, Quantity b, string op)
        {
            // Absolute temperatures may only enter products in Kelvin (offset-free), e.g. gas laws.
            if ((a.Dimension.IsAbsoluteTemperature && a.Unit.Offset != 0) || (b.Dimension.IsAbsoluteTemperature && b.Unit.Offset != 0))
                throw new DimensionMismatchException(op + " با دمای دارای مبدأ (°C/°F)؛ ابتدا به K تبدیل کنید", a.Dimension, b.Dimension);
        }

        /// <summary>
        /// Treats the radian as the coherent dimensionless unit it is in SI (e.g. P = T·ω, v = ω·r).
        /// Must be called explicitly so that "rad" never disappears silently.
        /// </summary>
        public Quantity DropAngle()
        {
            if (Dimension.A == 0) return this;
            var dim = Dimension with { A = 0 };
            return new Quantity(SiValue, UnitRegistry.Default.SiUnitFor(dim));
        }

        // ---------------- comparison ----------------
        public int CompareTo(Quantity other)
        {
            if (Dimension != other.Dimension) throw new DimensionMismatchException("مقایسه", Dimension, other.Dimension);
            return SiValue.CompareTo(other.SiValue);
        }

        public static bool operator <(Quantity a, Quantity b) => a.CompareTo(b) < 0;
        public static bool operator >(Quantity a, Quantity b) => a.CompareTo(b) > 0;
        public static bool operator <=(Quantity a, Quantity b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Quantity a, Quantity b) => a.CompareTo(b) >= 0;

        /// <summary>Tolerance-aware equality in SI (absolute tolerance expressed as a quantity of the same dimension).</summary>
        public bool ApproximatelyEquals(Quantity other, Quantity absoluteTolerance)
        {
            if (Dimension != other.Dimension) throw new DimensionMismatchException("مقایسه", Dimension, other.Dimension);
            if (absoluteTolerance.Dimension.AsDifference() != Dimension.AsDifference())
                throw new DimensionMismatchException("تلرانس مقایسه", Dimension, absoluteTolerance.Dimension);
            return Math.Abs(SiValue - other.SiValue) <= Math.Abs(absoluteTolerance.Value * absoluteTolerance.Unit.Factor);
        }

        public bool ApproximatelyEquals(Quantity other, double relativeTolerance = 1e-9)
        {
            if (Dimension != other.Dimension) throw new DimensionMismatchException("مقایسه", Dimension, other.Dimension);
            double a = SiValue, b = other.SiValue;
            return Math.Abs(a - b) <= relativeTolerance * Math.Max(1e-300, Math.Max(Math.Abs(a), Math.Abs(b)));
        }

        /// <summary>Exact equality: same dimension and identical SI value (no hidden rounding).</summary>
        public bool Equals(Quantity other) => Dimension == other.Dimension && SiValue.Equals(other.SiValue);
        public override bool Equals(object? obj) => obj is Quantity q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(Dimension, SiValue);
        public static bool operator ==(Quantity a, Quantity b) => a.Equals(b);
        public static bool operator !=(Quantity a, Quantity b) => !a.Equals(b);

        // ---------------- display (separate from computation precision) ----------------
        /// <summary>Formats for display only; the stored value is never rounded (Spec §11.6).</summary>
        public string Format(int significantDigits = 6)
        {
            if (significantDigits < 1) significantDigits = 1;
            string num = double.IsInfinity(Value) ? (Value > 0 ? "∞" : "-∞")
                : Value.ToString("G" + significantDigits, CultureInfo.InvariantCulture);
            return Unit.Symbol == "-" ? num : $"{num} {Unit.Symbol}";
        }

        public string Format(Unit displayUnit, int significantDigits = 6) => To(displayUnit).Format(significantDigits);

        public override string ToString() => Format(15);
    }
}
