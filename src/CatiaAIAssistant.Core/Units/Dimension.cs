namespace CatiaAIAssistant.Core.Units
{
    /// <summary>
    /// Physical dimension expressed as integer exponents of the base quantities
    /// Length (L), Mass (M), Time (T), Temperature (Θ) and plane Angle (A).
    /// Angle is kept as an explicit base so that rpm ↔ rad/s and degree ↔ radian are checked conversions (Spec §11.5).
    /// <see cref="IsAbsoluteTemperature"/> separates an absolute temperature (a point on a scale) from a
    /// temperature difference (an interval) — Spec §11.4.
    /// </summary>
    public readonly record struct Dimension(int L, int M, int T, int Theta, int A, bool IsAbsoluteTemperature = false)
    {
        public static readonly Dimension None = new(0, 0, 0, 0, 0);
        public static readonly Dimension Length = new(1, 0, 0, 0, 0);
        public static readonly Dimension Area = new(2, 0, 0, 0, 0);
        public static readonly Dimension Volume = new(3, 0, 0, 0, 0);
        public static readonly Dimension Mass = new(0, 1, 0, 0, 0);
        public static readonly Dimension Time = new(0, 0, 1, 0, 0);
        public static readonly Dimension Force = new(1, 1, -2, 0, 0);
        public static readonly Dimension Torque = new(2, 1, -2, 0, 0);
        public static readonly Dimension Energy = Torque;
        public static readonly Dimension Pressure = new(-1, 1, -2, 0, 0);
        public static readonly Dimension Stress = Pressure;
        public static readonly Dimension Strain = None;
        public static readonly Dimension Velocity = new(1, 0, -1, 0, 0);
        public static readonly Dimension AngularVelocity = new(0, 0, -1, 0, 1);
        public static readonly Dimension Acceleration = new(1, 0, -2, 0, 0);
        public static readonly Dimension Power = new(2, 1, -3, 0, 0);
        public static readonly Dimension Angle = new(0, 0, 0, 0, 1);
        public static readonly Dimension Temperature = new(0, 0, 0, 1, 0, true);
        public static readonly Dimension TemperatureDifference = new(0, 0, 0, 1, 0);
        public static readonly Dimension Stiffness = new(0, 1, -2, 0, 0);

        public static Dimension operator *(Dimension a, Dimension b) =>
            new(a.L + b.L, a.M + b.M, a.T + b.T, a.Theta + b.Theta, a.A + b.A);

        public static Dimension operator /(Dimension a, Dimension b) =>
            new(a.L - b.L, a.M - b.M, a.T - b.T, a.Theta - b.Theta, a.A - b.A);

        public Dimension Pow(int n) => new(L * n, M * n, T * n, Theta * n, A * n);

        /// <summary>The same exponents viewed as an interval (drops the absolute-temperature marker).</summary>
        public Dimension AsDifference() => this with { IsAbsoluteTemperature = false };

        public bool IsDimensionless => L == 0 && M == 0 && T == 0 && Theta == 0 && A == 0;

        public override string ToString()
        {
            if (IsDimensionless) return "1";
            var parts = new List<string>();
            void Add(string s, int e) { if (e != 0) parts.Add(e == 1 ? s : $"{s}^{e}"); }
            Add("L", L); Add("M", M); Add("T", T); Add("Θ", Theta); Add("A", A);
            return (IsAbsoluteTemperature ? "abs " : "") + string.Join("·", parts);
        }
    }
}
