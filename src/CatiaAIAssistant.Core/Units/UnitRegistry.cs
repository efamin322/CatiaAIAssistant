namespace CatiaAIAssistant.Core.Units
{
    /// <summary>
    /// Central, extensible unit registry (Spec §11): resolves symbols and aliases (English and Persian),
    /// provides the coherent SI unit for any dimension and the temperature-difference unit of each scale.
    /// New units are registered here; callers never hard-code conversion factors (contract §4).
    /// </summary>
    public sealed class UnitRegistry
    {
        private readonly Dictionary<string, Unit> _byAlias = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Unit> _byAliasIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Dimension, Unit> _siByDimension = new();
        private readonly Dictionary<string, Unit> _differenceByAbsolute = new(StringComparer.Ordinal);
        private readonly List<Unit> _all = new();

        public static UnitRegistry Default { get; } = CreateDefault();

        public IReadOnlyList<Unit> All => _all;

        /// <summary>Registers a unit with optional aliases. Case-sensitive aliases win (mm ≠ Mm, mN ≠ MN).</summary>
        public UnitRegistry Register(Unit unit, params string[] aliases)
        {
            if (!_all.Contains(unit)) _all.Add(unit);
            AddAlias(unit.Symbol, unit);
            foreach (var a in aliases) AddAlias(a, unit);
            return this;
        }

        private void AddAlias(string alias, Unit unit)
        {
            string key = Normalize(alias);
            if (key.Length == 0) return;
            if (_byAlias.TryGetValue(key, out var existing) && !existing.Equals(unit))
                throw new InvalidOperationException($"Unit alias '{alias}' is already bound to '{existing.Symbol}'.");
            _byAlias[key] = unit;
            _byAliasIgnoreCase.TryAdd(key, unit);
        }

        public UnitRegistry RegisterSi(Unit unit)
        {
            if (unit.Factor != 1 || unit.Offset != 0) throw new ArgumentException("SI unit must have factor 1 and no offset.");
            _siByDimension[unit.Dimension] = unit;
            return this;
        }

        public UnitRegistry RegisterDifference(Unit absolute, Unit difference)
        {
            if (!absolute.Dimension.IsAbsoluteTemperature || difference.Dimension != Dimension.TemperatureDifference || absolute.Factor != difference.Factor)
                throw new ArgumentException("Difference unit must be the offset-free interval of the absolute scale.");
            _differenceByAbsolute[absolute.Symbol] = difference;
            return this;
        }

        public bool TryResolve(string text, out Unit unit)
        {
            string key = Normalize(text);
            if (_byAlias.TryGetValue(key, out unit!)) return true;
            // Case-insensitive fallback only for words (≥4 chars): short symbols are case-significant (mPa ≠ MPa, mm ≠ Mm).
            return key.Length >= 4 && _byAliasIgnoreCase.TryGetValue(key, out unit!);
        }

        public Unit Resolve(string text) =>
            TryResolve(text, out var u) ? u : throw new KeyNotFoundException($"واحد ناشناخته: «{text}»");

        /// <summary>The coherent SI unit for a dimension; synthesises one (factor 1) when none is registered.</summary>
        public Unit SiUnitFor(Dimension dimension)
        {
            if (_siByDimension.TryGetValue(dimension, out var u)) return u;
            var synth = new Unit("SI[" + dimension + "]", dimension, 1);
            _siByDimension[dimension] = synth;
            return synth;
        }

        public Unit DifferenceUnitFor(Unit absolute) =>
            _differenceByAbsolute.TryGetValue(absolute.Symbol, out var d) ? d : Units.DeltaKelvin;

        /// <summary>Unicode/spacing normalisation: Persian ZWNJ, middle dots, superscripts and Greek micro.</summary>
        internal static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var t = s.Trim()
                .Replace('\u200c', ' ')              // ZWNJ → space
                .Replace('\u00b5', 'µ').Replace('\u03bc', 'µ') // micro sign / Greek mu
                .Replace("⋅", "·").Replace("*", "·").Replace(".", "·")
                .Replace("^2", "²").Replace("^3", "³")
                .Replace("ي", "ی").Replace("ك", "ک");
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            return t.Replace(" · ", "·");
        }

        private static UnitRegistry CreateDefault()
        {
            var r = new UnitRegistry();
            r.Register(Units.Meter, "meter", "metre", "متر")
             .Register(Units.Millimeter, "millimeter", "millimetre", "میلی متر", "میلیمتر", "م م")
             .Register(Units.Centimeter, "centimeter", "سانتی متر", "سانتیمتر", "سانت")
             .Register(Units.Micrometer, "um", "micron", "میکرون", "میکرومتر")
             .Register(Units.Inch, "inch", "\"", "اینچ")
             .Register(Units.Foot, "foot", "feet", "فوت")
             .Register(Units.SquareMeter, "m2", "متر مربع")
             .Register(Units.SquareMillimeter, "mm2", "میلی متر مربع")
             .Register(Units.CubicMeter, "m3", "متر مکعب")
             .Register(Units.CubicMillimeter, "mm3", "میلی متر مکعب")
             .Register(Units.CubicCentimeter, "cm3", "cc", "سی سی", "سانتی متر مکعب")
             .Register(Units.Liter, "l", "liter", "litre", "لیتر")
             .Register(Units.Kilogram, "kilogram", "کیلوگرم", "کیلو")
             .Register(Units.Gram, "gram", "گرم")
             .Register(Units.Tonne, "tonne", "ton", "تن")
             .Register(Units.Pound, "lbm", "pound", "پوند")
             .Register(Units.Second, "sec", "second", "ثانیه")
             .Register(Units.Minute, "minute", "دقیقه")
             .Register(Units.Hour, "hr", "hour", "ساعت")
             .Register(Units.Newton, "newton", "نیوتن", "نیوتون")
             .Register(Units.Kilonewton, "kilonewton", "کیلونیوتن", "کیلو نیوتن")
             .Register(Units.PoundForce, "پوند نیرو")
             .Register(Units.KilogramForce, "کیلوگرم نیرو")
             .Register(Units.NewtonMeter, "Nm", "N m", "نیوتن متر", "نیوتن·متر")
             .Register(Units.NewtonMillimeter, "Nmm", "N mm", "نیوتن میلی متر")
             .Register(Units.KilonewtonMeter, "kNm", "کیلونیوتن متر")
             .Register(Units.Joule, "joule", "ژول")
             .Register(Units.Kilojoule, "کیلوژول")
             .Register(Units.WattHour, "وات ساعت")
             .Register(Units.Pascal, "pascal", "پاسکال")
             .Register(Units.Kilopascal, "کیلوپاسکال")
             .Register(Units.Megapascal, "مگاپاسکال", "مگا پاسکال")
             .Register(Units.Gigapascal, "گیگاپاسکال", "گیگا پاسکال")
             .Register(Units.Bar, "بار")
             .Register(Units.Psi, "PSI")
             .Register(Units.NewtonPerSquareMillimeter, "N/mm2", "نیوتن بر میلی متر مربع")
             .Register(Units.Unitless, "unitless")
             .Register(Units.Percent, "percent", "درصد")
             .Register(Units.Microstrain, "ue", "microstrain", "میکرواسترین")
             .Register(Units.MeterPerSecond, "متر بر ثانیه")
             .Register(Units.MillimeterPerSecond, "میلی متر بر ثانیه")
             .Register(Units.KilometerPerHour, "kmh", "kph", "کیلومتر بر ساعت")
             .Register(Units.MeterPerSecondSquared, "m/s2", "متر بر مجذور ثانیه")
             .Register(Units.StandardGravity, "g0", "gn")
             .Register(Units.RadianPerSecond, "rad/sec", "رادیان بر ثانیه")
             .Register(Units.Rpm, "RPM", "r/min", "دور بر دقیقه", "دور در دقیقه")
             .Register(Units.DegreePerSecond, "deg/s", "درجه بر ثانیه")
             .Register(Units.Watt, "watt", "وات")
             .Register(Units.Kilowatt, "کیلووات", "کیلو وات")
             .Register(Units.Horsepower, "HP", "اسب بخار")
             .Register(Units.Kelvin, "kelvin", "کلوین")
             .Register(Units.Celsius, "C", "degC", "celsius", "درجه سانتی گراد", "سانتی گراد", "درجه سلسیوس")
             .Register(Units.Fahrenheit, "F", "degF", "fahrenheit", "فارنهایت", "درجه فارنهایت")
             .Register(Units.DeltaKelvin, "delta K", "dK")
             .Register(Units.DeltaCelsius, "delta C", "dC", "Δ°C", "ΔC")
             .Register(Units.DeltaFahrenheit, "delta F", "dF", "ΔF")
             .Register(Units.Radian, "radian", "رادیان")
             .Register(Units.Degree, "deg", "degree", "درجه")
             .Register(Units.Revolution, "rev", "revolution", "دور")
             .Register(Units.NewtonPerMeter, "نیوتن بر متر")
             .Register(Units.NewtonPerMillimeter, "نیوتن بر میلی متر");

            foreach (var u in new[] { Units.Meter, Units.SquareMeter, Units.CubicMeter, Units.Kilogram, Units.Second,
                                      Units.Newton, Units.NewtonMeter, Units.Pascal, Units.Unitless, Units.MeterPerSecond,
                                      Units.MeterPerSecondSquared, Units.RadianPerSecond, Units.Watt, Units.Kelvin,
                                      Units.DeltaKelvin, Units.Radian, Units.NewtonPerMeter })
                r.RegisterSi(u);

            r.RegisterDifference(Units.Kelvin, Units.DeltaKelvin)
             .RegisterDifference(Units.Celsius, Units.DeltaCelsius)
             .RegisterDifference(Units.Fahrenheit, Units.DeltaFahrenheit);
            return r;
        }
    }
}
