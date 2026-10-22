namespace CatiaAIAssistant.Core.Units
{
    /// <summary>
    /// A measurement unit: <c>SI = value × Factor + Offset</c>. SI base units are m, kg, s, K, rad.
    /// <see cref="Offset"/> is non-zero only for absolute temperature scales (°C, °F); temperature
    /// <em>difference</em> units (ΔC, ΔF) share the factor but never the offset (Spec §11.4).
    /// </summary>
    public sealed class Unit : IEquatable<Unit>
    {
        public string Symbol { get; }
        public string PersianName { get; }
        public Dimension Dimension { get; }
        public double Factor { get; }
        public double Offset { get; }

        public Unit(string symbol, Dimension dimension, double factor, double offset = 0.0, string persianName = "")
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Unit symbol is required.", nameof(symbol));
            if (!double.IsFinite(factor) || factor == 0) throw new ArgumentOutOfRangeException(nameof(factor));
            if (offset != 0 && !dimension.IsAbsoluteTemperature)
                throw new ArgumentException("Only absolute temperature units may have an offset.", nameof(offset));
            Symbol = symbol;
            Dimension = dimension;
            Factor = factor;
            Offset = offset;
            PersianName = persianName;
        }

        public double ToSi(double value) => value * Factor + Offset;
        public double FromSi(double si) => (si - Offset) / Factor;

        public bool Equals(Unit? other) =>
            other is not null && Symbol == other.Symbol && Dimension == other.Dimension && Factor == other.Factor && Offset == other.Offset;
        public override bool Equals(object? obj) => Equals(obj as Unit);
        public override int GetHashCode() => HashCode.Combine(Symbol, Dimension, Factor, Offset);
        public override string ToString() => Symbol;
    }
}
