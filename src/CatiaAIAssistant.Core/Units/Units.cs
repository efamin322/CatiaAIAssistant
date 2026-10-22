namespace CatiaAIAssistant.Core.Units
{
    /// <summary>
    /// Built-in unit catalog (Spec §11). Factors are exact definitions where one exists
    /// (inch = 25.4 mm, lbf = 4.4482216152605 N, psi = 6894.757293168 Pa, rpm = 2π/60 rad/s).
    /// New units are added through <see cref="UnitRegistry.Register"/>, not by editing callers (contract §4).
    /// </summary>
    public static class Units
    {
        private const double Pi = Math.PI;

        // Length
        public static readonly Unit Meter = new("m", Dimension.Length, 1, persianName: "متر");
        public static readonly Unit Millimeter = new("mm", Dimension.Length, 1e-3, persianName: "میلی‌متر");
        public static readonly Unit Centimeter = new("cm", Dimension.Length, 1e-2, persianName: "سانتی‌متر");
        public static readonly Unit Micrometer = new("µm", Dimension.Length, 1e-6, persianName: "میکرومتر");
        public static readonly Unit Inch = new("in", Dimension.Length, 0.0254, persianName: "اینچ");
        public static readonly Unit Foot = new("ft", Dimension.Length, 0.3048, persianName: "فوت");

        // Area / volume
        public static readonly Unit SquareMeter = new("m²", Dimension.Area, 1, persianName: "متر مربع");
        public static readonly Unit SquareMillimeter = new("mm²", Dimension.Area, 1e-6, persianName: "میلی‌متر مربع");
        public static readonly Unit CubicMeter = new("m³", Dimension.Volume, 1, persianName: "متر مکعب");
        public static readonly Unit CubicMillimeter = new("mm³", Dimension.Volume, 1e-9, persianName: "میلی‌متر مکعب");
        public static readonly Unit CubicCentimeter = new("cm³", Dimension.Volume, 1e-6, persianName: "سانتی‌متر مکعب");
        public static readonly Unit Liter = new("L", Dimension.Volume, 1e-3, persianName: "لیتر");

        // Mass
        public static readonly Unit Kilogram = new("kg", Dimension.Mass, 1, persianName: "کیلوگرم");
        public static readonly Unit Gram = new("g", Dimension.Mass, 1e-3, persianName: "گرم");
        public static readonly Unit Tonne = new("t", Dimension.Mass, 1e3, persianName: "تن");
        public static readonly Unit Pound = new("lb", Dimension.Mass, 0.45359237, persianName: "پوند");

        // Time
        public static readonly Unit Second = new("s", Dimension.Time, 1, persianName: "ثانیه");
        public static readonly Unit Minute = new("min", Dimension.Time, 60, persianName: "دقیقه");
        public static readonly Unit Hour = new("h", Dimension.Time, 3600, persianName: "ساعت");

        // Force
        public static readonly Unit Newton = new("N", Dimension.Force, 1, persianName: "نیوتن");
        public static readonly Unit Kilonewton = new("kN", Dimension.Force, 1e3, persianName: "کیلونیوتن");
        public static readonly Unit PoundForce = new("lbf", Dimension.Force, 4.4482216152605, persianName: "پوند نیرو");
        public static readonly Unit KilogramForce = new("kgf", Dimension.Force, 9.80665, persianName: "کیلوگرم نیرو");

        // Torque
        public static readonly Unit NewtonMeter = new("N·m", Dimension.Torque, 1, persianName: "نیوتن‌متر");
        public static readonly Unit NewtonMillimeter = new("N·mm", Dimension.Torque, 1e-3, persianName: "نیوتن‌میلی‌متر");
        public static readonly Unit KilonewtonMeter = new("kN·m", Dimension.Torque, 1e3, persianName: "کیلونیوتن‌متر");

        // Energy
        public static readonly Unit Joule = new("J", Dimension.Energy, 1, persianName: "ژول");
        public static readonly Unit Kilojoule = new("kJ", Dimension.Energy, 1e3, persianName: "کیلوژول");
        public static readonly Unit WattHour = new("Wh", Dimension.Energy, 3600, persianName: "وات‌ساعت");

        // Pressure / stress
        public static readonly Unit Pascal = new("Pa", Dimension.Pressure, 1, persianName: "پاسکال");
        public static readonly Unit Kilopascal = new("kPa", Dimension.Pressure, 1e3, persianName: "کیلوپاسکال");
        public static readonly Unit Megapascal = new("MPa", Dimension.Pressure, 1e6, persianName: "مگاپاسکال");
        public static readonly Unit Gigapascal = new("GPa", Dimension.Pressure, 1e9, persianName: "گیگاپاسکال");
        public static readonly Unit Bar = new("bar", Dimension.Pressure, 1e5, persianName: "بار");
        public static readonly Unit Psi = new("psi", Dimension.Pressure, 6894.757293168, persianName: "psi");
        public static readonly Unit NewtonPerSquareMillimeter = new("N/mm²", Dimension.Pressure, 1e6, persianName: "نیوتن بر میلی‌متر مربع");

        // Strain (dimensionless)
        public static readonly Unit Unitless = new("-", Dimension.None, 1, persianName: "بدون بعد");
        public static readonly Unit Percent = new("%", Dimension.None, 1e-2, persianName: "درصد");
        public static readonly Unit Microstrain = new("µε", Dimension.None, 1e-6, persianName: "میکرواسترین");

        // Linear velocity / acceleration
        public static readonly Unit MeterPerSecond = new("m/s", Dimension.Velocity, 1, persianName: "متر بر ثانیه");
        public static readonly Unit MillimeterPerSecond = new("mm/s", Dimension.Velocity, 1e-3, persianName: "میلی‌متر بر ثانیه");
        public static readonly Unit KilometerPerHour = new("km/h", Dimension.Velocity, 1000.0 / 3600.0, persianName: "کیلومتر بر ساعت");
        public static readonly Unit MeterPerSecondSquared = new("m/s²", Dimension.Acceleration, 1, persianName: "متر بر مجذور ثانیه");
        public static readonly Unit StandardGravity = new("g₀", Dimension.Acceleration, 9.80665, persianName: "شتاب گرانش استاندارد");

        // Angular velocity
        public static readonly Unit RadianPerSecond = new("rad/s", Dimension.AngularVelocity, 1, persianName: "رادیان بر ثانیه");
        public static readonly Unit Rpm = new("rpm", Dimension.AngularVelocity, 2 * Pi / 60.0, persianName: "دور بر دقیقه");
        public static readonly Unit DegreePerSecond = new("°/s", Dimension.AngularVelocity, Pi / 180.0, persianName: "درجه بر ثانیه");

        // Power
        public static readonly Unit Watt = new("W", Dimension.Power, 1, persianName: "وات");
        public static readonly Unit Kilowatt = new("kW", Dimension.Power, 1e3, persianName: "کیلووات");
        public static readonly Unit Horsepower = new("hp", Dimension.Power, 745.69987158227022, persianName: "اسب بخار");

        // Temperature (absolute)
        public static readonly Unit Kelvin = new("K", Dimension.Temperature, 1, persianName: "کلوین");
        public static readonly Unit Celsius = new("°C", Dimension.Temperature, 1, 273.15, persianName: "درجه سانتی‌گراد");
        public static readonly Unit Fahrenheit = new("°F", Dimension.Temperature, 5.0 / 9.0, 273.15 - 32 * 5.0 / 9.0, persianName: "درجه فارنهایت");

        // Temperature difference
        public static readonly Unit DeltaKelvin = new("ΔK", Dimension.TemperatureDifference, 1, persianName: "اختلاف کلوین");
        public static readonly Unit DeltaCelsius = new("Δ°C", Dimension.TemperatureDifference, 1, persianName: "اختلاف درجه سانتی‌گراد");
        public static readonly Unit DeltaFahrenheit = new("Δ°F", Dimension.TemperatureDifference, 5.0 / 9.0, persianName: "اختلاف درجه فارنهایت");

        // Angle
        public static readonly Unit Radian = new("rad", Dimension.Angle, 1, persianName: "رادیان");
        public static readonly Unit Degree = new("°", Dimension.Angle, Pi / 180.0, persianName: "درجه");
        public static readonly Unit Revolution = new("rev", Dimension.Angle, 2 * Pi, persianName: "دور");

        // Stiffness
        public static readonly Unit NewtonPerMeter = new("N/m", Dimension.Stiffness, 1, persianName: "نیوتن بر متر");
        public static readonly Unit NewtonPerMillimeter = new("N/mm", Dimension.Stiffness, 1e3, persianName: "نیوتن بر میلی‌متر");
    }
}
