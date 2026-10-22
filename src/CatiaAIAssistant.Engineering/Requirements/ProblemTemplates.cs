using CatiaAIAssistant.Core.Language;

namespace CatiaAIAssistant.Engineering.Requirements
{
    /// <summary>
    /// Information profiles per problem type (Spec §07 example: shaft needs torque, speed, supports,
    /// bending loads, material, life, operating conditions). Registered in <see cref="ProblemTemplateCatalog"/>.
    /// </summary>
    public static class ProblemTemplates
    {
        private static InformationItem Req(string key, int prio, string q, string effect, bool safety = false, string? how = null, params string[] alt) =>
            new() { ParameterKey = key, Need = InformationNeed.Required, Priority = prio, Question = q, DesignEffect = effect, SafetyCritical = safety, HowToObtain = how, SatisfiedBy = alt };

        private static InformationItem Asm(string key, int prio, string q, string effect, string assumption, bool safety = false, string? how = null) =>
            new() { ParameterKey = key, Need = InformationNeed.Assumable, Priority = prio, Question = q, DesignEffect = effect, DefaultAssumption = assumption, SafetyCritical = safety, HowToObtain = how };

        private static InformationItem Opt(string key, int prio, string q, string effect) =>
            new() { ParameterKey = key, Need = InformationNeed.Optional, Priority = prio, Question = q, DesignEffect = effect };

        public static readonly ProblemTemplate Shaft = new()
        {
            ProblemType = "Shaft",
            PersianTitle = "طراحی شفت",
            Items = new[]
            {
                Req("Torque", 1, "گشتاور انتقالی شفت چقدر است؟ (یا توان و دور)", "قطر حداقل از تنش پیچشی تعیین می‌شود (d ∝ T^(1/3)).", true,
                    "از پلاک موتور: T = P / ω ؛ یا با گشتاورسنج اندازه‌گیری کنید.", "Power"),
                Req("Speed", 2, "سرعت دورانی شفت چند دور بر دقیقه است؟", "برای تبدیل توان به گشتاور، سرعت بحرانی و عمر یاتاقان لازم است.", false,
                    "از پلاک موتور یا با دورسنج اندازه‌گیری کنید."),
                Asm("BendingMoment", 3, "آیا بار خمشی (نیروی چرخ‌دنده/تسمه) روی شفت وجود دارد؟", "خمش و پیچش ترکیبی معمولاً قطر را تعیین می‌کند.",
                    "بدون بار خمشی (فقط پیچش خالص)", safety: true, how: "از نیروی مماسی چرخ‌دنده Ft = 2T/d یا کشش تسمه برآورد کنید."),
                Asm("SafetyFactor", 4, "ضریب اطمینان موردنظر چقدر است؟", "تنش مجاز = استحکام / ضریب اطمینان.", "ضریب اطمینان 2.0", safety: true),
                Asm("Material", 5, "جنس شفت چیست؟", "استحکام تسلیم و خستگی را تعیین می‌کند.", "فولاد C45 نرمال‌شده (Sy≈340 MPa)"),
                Asm("Supports", 6, "نحوهٔ تکیه‌گاه‌گذاری (فاصلهٔ یاتاقان‌ها) چگونه است؟", "گشتاور خمشی و خیز به فاصله تکیه‌گاه‌ها وابسته است.", "دو یاتاقان ساده در دو انتها"),
                Opt("Life", 7, "عمر هدف چند ساعت است؟", "برای بررسی خستگی و انتخاب یاتاقان."),
                Opt("Temperature", 8, "دمای کاری چقدر است؟", "بر خواص ماده و انطباق‌ها اثر دارد."),
            }
        };

        public static readonly ProblemTemplate SpurGear = new()
        {
            ProblemType = "SpurGear",
            PersianTitle = "طراحی چرخ‌دنده ساده",
            Items = new[]
            {
                Req("Module", 1, "ماژول چرخ‌دنده چند میلی‌متر است؟", "اندازه دندانه و استحکام خمشی دندانه را تعیین می‌کند.", false,
                    "ماژول را می‌توان از قطر خارجی چرخ موجود حساب کرد: m = da / (z + 2)."),
                Req("Teeth", 2, "تعداد دندانه چند است؟", "قطر گام d = m·z و خطر زیربری (z < 17) را تعیین می‌کند.", false, "دندانه‌های چرخ موجود را بشمارید."),
                Asm("PressureAngle", 3, "زاویه فشار چند درجه است؟", "شکل اینولوت و حداقل تعداد دندانه.", "زاویه فشار 20° (استاندارد ISO 53)"),
                Asm("FaceWidth", 4, "عرض دندانه چقدر است؟", "ظرفیت بار با عرض متناسب است.", "عرض = 8 × ماژول"),
                Asm("BoreDiameter", 5, "قطر سوراخ شفت چقدر است؟", "باید با قطر شفت واقعی منطبق باشد.", "برآورد اولیه از ابعاد چرخ (نیازمند تطبیق با شفت)"),
                Opt("Torque", 6, "گشتاور انتقالی چقدر است؟", "برای بررسی استحکام دندانه (در صورت وجود داده کافی)."),
            }
        };

        public static readonly ProblemTemplate GearPair = new()
        {
            ProblemType = "GearPair",
            PersianTitle = "طراحی جفت چرخ‌دنده",
            Items = SpurGear.Items.Concat(new[]
            {
                Req("GearRatio", 2, "نسبت انتقال موردنظر چقدر است؟ (یا تعداد دندانهٔ چرخ دوم)", "تعداد دندانهٔ چرخ دوم و فاصلهٔ محورها را تعیین می‌کند."),
                Opt("CenterDistance", 3, "آیا فاصلهٔ محورها از قبل ثابت است؟", "اگر ثابت باشد، ماژول و تعداد دندانه‌ها باید با آن سازگار شوند."),
            }).ToArray()
        };

        public static readonly ProblemTemplate SliderCrank = new()
        {
            ProblemType = "SliderCrank",
            PersianTitle = "مکانیزم لنگ و لغزنده",
            Items = new[]
            {
                Req("CrankRadius", 1, "شعاع لنگ (نصف کورس) چقدر است؟", "کورس پیستون S = 2r.", false, "کورس موردنیاز را دو قسمت کنید.", "Stroke"),
                Req("RodLength", 2, "طول شاتون (فاصله مرکز تا مرکز) چقدر است؟", "باید L > r باشد؛ L/r معمولاً 3 تا 4.", false),
                Asm("PistonDiameter", 3, "قطر پیستون چقدر است؟", "سطح پیستون و نیروی گاز.", "قطر پیستون ≈ 2.2 × شعاع لنگ"),
                Opt("Speed", 4, "دور میل‌لنگ چقدر است؟", "برای سرعت و شتاب پیستون."),
            }
        };

        public static readonly ProblemTemplate Bearing = new()
        {
            ProblemType = "Bearing",
            PersianTitle = "انتخاب یاتاقان غلتشی",
            Items = new[]
            {
                Req("RadialLoad", 1, "بار شعاعی روی یاتاقان چقدر است؟", "بار معادل P و عمر L10 = (C/P)^p.", true),
                Req("Speed", 2, "سرعت دورانی چقدر است؟", "تبدیل عمر به ساعت و حد سرعت یاتاقان.", false),
                Asm("AxialLoad", 3, "آیا بار محوری وجود دارد؟", "نوع یاتاقان و بار معادل.", "بدون بار محوری", safety: true),
                Asm("Life", 4, "عمر هدف چند ساعت است؟", "ظرفیت دینامیکی لازم C.", "عمر L10h = 20000 h (ماشین‌آلات صنعتی یک‌شیفت)"),
                Req("ShaftDiameter", 5, "قطر شفت در محل یاتاقان چقدر است؟", "قطر داخلی یاتاقان.", false, "قطر نشیمنگاه را با کولیس اندازه بگیرید.", "BoreDiameter"),
                Opt("Temperature", 6, "دمای کاری و نوع روانکاری چیست؟", "ضریب a_ISO و انتخاب گریس/روغن."),
            }
        };

        public static readonly ProblemTemplate CompressionSpring = new()
        {
            ProblemType = "CompressionSpring",
            PersianTitle = "طراحی فنر فشاری",
            Items = new[]
            {
                Req("Force", 1, "نیروی کاری فنر چقدر است؟", "تنش برشی سیم τ = 8FDK/(πd³).", true),
                Req("Deflection", 2, "تغییر شکل در نیروی کاری چقدر است؟ (یا سختی فنر)", "سختی k = F/δ و تعداد حلقه‌ها.", false, null, "Stiffness"),
                Asm("Diameter", 3, "قطر متوسط فنر چقدر است؟", "اندیس فنر C = D/d (معمولاً 4 تا 12).", "قطر متوسط بر اساس فضای نصب (نیازمند تأیید)"),
                Asm("Material", 4, "جنس سیم چیست؟", "مدول برشی G و استحکام کششی.", "سیم فنر موزیک ASTM A228 (G = 81.7 GPa)"),
            }
        };

        public static readonly ProblemTemplate Block = new()
        {
            ProblemType = "Block",
            PersianTitle = "بلوک مکعب‌مستطیل",
            Items = new[]
            {
                Req("Length", 1, "طول بلوک چقدر است؟", "ابعاد اصلی قطعه."),
                Req("Width", 2, "عرض بلوک چقدر است؟", "ابعاد اصلی قطعه."),
                Req("Height", 3, "ارتفاع بلوک چقدر است؟", "ابعاد اصلی قطعه.", false, null, "Thickness"),
            }
        };
    }

    /// <summary>Extensible registry of problem templates (contract §4).</summary>
    public sealed class ProblemTemplateCatalog
    {
        private readonly Dictionary<string, ProblemTemplate> _templates = new(StringComparer.Ordinal);

        public static ProblemTemplateCatalog Default { get; } = new ProblemTemplateCatalog()
            .Register(ProblemTemplates.Shaft).Register(ProblemTemplates.SpurGear).Register(ProblemTemplates.GearPair)
            .Register(ProblemTemplates.SliderCrank).Register(ProblemTemplates.Bearing)
            .Register(ProblemTemplates.CompressionSpring).Register(ProblemTemplates.Block);

        public ProblemTemplateCatalog Register(ProblemTemplate t)
        {
            _templates[t.ProblemType] = t;
            return this;
        }

        public bool TryGet(string problemType, out ProblemTemplate template) => _templates.TryGetValue(problemType, out template!);
        public IReadOnlyCollection<ProblemTemplate> All => _templates.Values;
    }
}
