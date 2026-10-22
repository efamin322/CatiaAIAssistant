using CatiaAIAssistant.Core.Units;
using U = CatiaAIAssistant.Core.Units.Units;

namespace CatiaAIAssistant.Core.Language
{
    /// <summary>Definition of a named engineering parameter the language layer can recognise (Spec §06).</summary>
    public sealed class ParameterDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string PersianName { get; init; } = string.Empty;
        public Dimension Dimension { get; init; } = Dimension.None;
        /// <summary>Unit applied when the user gives a bare number; recorded as an assumption, never silent.</summary>
        public Unit DefaultUnit { get; init; } = U.Unitless;
        public bool IsInteger { get; init; }
        public bool MustBePositive { get; init; } = true;
        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Extensible FA/EN vocabulary of parameter names. New parameters are added with <see cref="Register"/>
    /// (contract §4) — the extractor itself never changes.
    /// </summary>
    public sealed class ParameterVocabulary
    {
        private readonly Dictionary<string, ParameterDefinition> _byKey = new(StringComparer.Ordinal);
        private readonly List<(string Alias, ParameterDefinition Def)> _aliases = new();

        public static ParameterVocabulary Default { get; } = CreateDefault();

        public IReadOnlyCollection<ParameterDefinition> Definitions => _byKey.Values;
        internal IReadOnlyList<(string Alias, ParameterDefinition Def)> Aliases => _aliases;

        public ParameterVocabulary Register(ParameterDefinition def)
        {
            if (string.IsNullOrWhiteSpace(def.Key)) throw new ArgumentException("Parameter key is required.");
            if (def.DefaultUnit.Dimension != def.Dimension)
                throw new ArgumentException($"Default unit of '{def.Key}' does not match its dimension.");
            _byKey[def.Key] = def;
            foreach (var a in def.Aliases.Append(def.Key))
            {
                string n = LanguageText.Normalize(a);
                if (n.Length == 0) continue;
                var clash = _aliases.FirstOrDefault(x => x.Alias == n);
                if (clash.Def != null && clash.Def.Key != def.Key)
                    throw new InvalidOperationException($"Alias '{a}' is already used by parameter '{clash.Def.Key}'.");
                if (clash.Def == null) _aliases.Add((n, def));
            }
            return this;
        }

        public bool TryGet(string key, out ParameterDefinition def) => _byKey.TryGetValue(key, out def!);

        private static ParameterDefinition P(string key, string fa, Dimension dim, Unit unit, params string[] aliases) =>
            new() { Key = key, PersianName = fa, Dimension = dim, DefaultUnit = unit, Aliases = aliases };

        private static ParameterVocabulary CreateDefault()
        {
            var v = new ParameterVocabulary();
            // gears
            v.Register(P("Module", "ماژول", Dimension.Length, U.Millimeter, "ماژول", "مدول", "module"));
            v.Register(new ParameterDefinition { Key = "Teeth", PersianName = "تعداد دندانه", Dimension = Dimension.None, DefaultUnit = U.Unitless, IsInteger = true,
                                                 Aliases = new[] { "تعداد دندانه", "دندانه", "دندانه‌ها", "teeth", "tooth count", "number of teeth" } });
            v.Register(P("GearRatio", "نسبت انتقال", Dimension.None, U.Unitless, "نسبت انتقال", "نسبت دنده", "نسبت", "gear ratio", "ratio", "transmission ratio"));
            v.Register(P("FaceWidth", "عرض دندانه", Dimension.Length, U.Millimeter, "عرض دندانه", "عرض چرخ دنده", "پهنای دندانه", "face width"));
            v.Register(P("PressureAngle", "زاویه فشار", Dimension.Angle, U.Degree, "زاویه فشار", "pressure angle"));
            v.Register(P("CenterDistance", "فاصله محورها", Dimension.Length, U.Millimeter, "فاصله محورها", "فاصله مرکزها", "فاصله مراکز", "center distance", "centre distance"));
            // shafts / bores
            v.Register(P("ShaftDiameter", "قطر شفت", Dimension.Length, U.Millimeter, "قطر شفت", "قطر محور", "shaft diameter"));
            v.Register(P("BoreDiameter", "قطر سوراخ", Dimension.Length, U.Millimeter, "قطر سوراخ", "سوراخ مرکزی", "سوراخ شفت", "bore diameter", "bore"));
            v.Register(P("Diameter", "قطر", Dimension.Length, U.Millimeter, "قطر", "diameter"));
            v.Register(P("Radius", "شعاع", Dimension.Length, U.Millimeter, "شعاع", "radius"));
            v.Register(P("Length", "طول", Dimension.Length, U.Millimeter, "طول", "length"));
            v.Register(P("Width", "عرض", Dimension.Length, U.Millimeter, "عرض", "پهنا", "width"));
            v.Register(P("Height", "ارتفاع", Dimension.Length, U.Millimeter, "ارتفاع", "height"));
            v.Register(P("Thickness", "ضخامت", Dimension.Length, U.Millimeter, "ضخامت", "thickness"));
            // slider-crank
            v.Register(P("CrankRadius", "شعاع لنگ", Dimension.Length, U.Millimeter, "شعاع لنگ", "شعاع میل لنگ", "crank radius"));
            v.Register(P("RodLength", "طول شاتون", Dimension.Length, U.Millimeter, "طول شاتون", "شاتون", "connecting rod length", "rod length", "connecting rod"));
            v.Register(P("PistonDiameter", "قطر پیستون", Dimension.Length, U.Millimeter, "قطر پیستون", "piston diameter"));
            v.Register(P("Stroke", "کورس", Dimension.Length, U.Millimeter, "کورس", "stroke"));
            // loads & operating conditions
            v.Register(P("Torque", "گشتاور", Dimension.Torque, U.NewtonMeter, "گشتاور", "torque"));
            v.Register(P("Speed", "سرعت دورانی", Dimension.AngularVelocity, U.Rpm, "سرعت دورانی", "سرعت چرخش", "دور موتور", "rotational speed", "speed"));
            v.Register(P("Power", "توان", Dimension.Power, U.Kilowatt, "توان", "قدرت", "power"));
            v.Register(P("Force", "نیرو", Dimension.Force, U.Newton, "نیرو", "force", "load"));
            v.Register(P("RadialLoad", "بار شعاعی", Dimension.Force, U.Newton, "بار شعاعی", "نیروی شعاعی", "radial load"));
            v.Register(P("AxialLoad", "بار محوری", Dimension.Force, U.Newton, "بار محوری", "نیروی محوری", "axial load"));
            v.Register(P("BendingMoment", "گشتاور خمشی", Dimension.Torque, U.NewtonMeter, "گشتاور خمشی", "ممان خمشی", "bending moment"));
            v.Register(P("Life", "عمر هدف", Dimension.Time, U.Hour, "عمر هدف", "عمر کاری", "عمر", "life", "target life"));
            v.Register(P("SafetyFactor", "ضریب اطمینان", Dimension.None, U.Unitless, "ضریب اطمینان", "ضریب ایمنی", "safety factor", "factor of safety"));
            v.Register(P("Temperature", "دما", Dimension.Temperature, U.Celsius, "دمای کاری", "دما", "temperature"));
            v.Register(P("Angle", "زاویه", Dimension.Angle, U.Degree, "زاویه", "angle"));
            v.Register(P("AllowableStress", "تنش مجاز", Dimension.Stress, U.Megapascal, "تنش مجاز", "allowable stress"));
            v.Register(P("Stiffness", "سختی فنر", Dimension.Stiffness, U.NewtonPerMillimeter, "سختی فنر", "ثابت فنر", "spring rate", "stiffness"));
            v.Register(P("Deflection", "تغییر شکل", Dimension.Length, U.Millimeter, "تغییر شکل", "خیز", "deflection"));
            v.Register(P("Mass", "جرم", Dimension.Mass, U.Kilogram, "جرم", "وزن", "mass", "weight"));
            return v;
        }
    }
}
