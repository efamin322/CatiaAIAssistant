# گزارش ممیزی پروژهٔ اولیه (حوزهٔ 01) — v0.1.0

تاریخ: 2026-10-22 · ابزار: .NET SDK 8.0.426 (Linux, `EnableWindowsTargeting`) · CATIA: در دسترس نبود.

## 1. ساختار
| مورد | مقدار |
|------|-------|
| Solution | `CatiaAIAssistant.sln` (در این ریپو ساخته شد؛ نسخهٔ اولیه Solution نداشت) |
| Project | `src/CatiaAIAssistant/CatiaAIAssistant.csproj` — WinExe, `net8.0-windows`, WinForms |
| C# | پیش‌فرض SDK 8 (C# 12)، `Nullable=enable`, `ImplicitUsings=enable` |
| NuGet | هیچ |
| COM | Late-binding با `dynamic` روی ProgID `CATIA.Application` (بدون Interop Assembly) + P/Invoke `oleaut32/ole32` |
| نقطهٔ شروع | `Program.Main` → `Form1` |
| خطوط کد | ~2,900 |

## 2. اجزا و مسئولیت‌ها
| کلاس | لایهٔ فعلی | مسئولیت |
|------|-----------|---------|
| `Form1` (833 خط) | UI | ساخت کل UI در کد، ناوبری، و **فراخوانی مستقیم سرویس‌ها و اشیای COM** |
| `ShapeParametersDialog` | UI | گرفتن ابعاد شکل پایه |
| `OperationLogger`, `OperationLogModels` | Core | لاگ عملیات، مسیرهای Workspace، مدل‌های نتیجه/تأیید CAD |
| `EngineeringIntentAndCalculationEngine` | Engineering | تشخیص دامنه با کلیدواژه، استخراج عدد، محاسبات اولیه، برنامهٔ اجرا، اعتبارسنجی |
| `EngineeringModels` | Domain | مشخصات طراحی، پارامتر، محاسبه، گام اجرایی |
| `CatiaConnectionService` | CAD | اتصال به نمونهٔ فعال CATIA (GetActiveObject) / اجرای اختیاری |
| `CatiaDocumentManager` | CAD | ساخت/ذخیره/پشتیبان/Rollback اسناد |
| `CatiaBasicPartService` | CAD | Sketch + Pad اشکال پایه؛ **همچنین نرمال‌سازی ارقام (منطق متنی)** |
| `CatiaMechanicalEngine` | CAD | اجرای برنامهٔ CAD (`switch` روی رشتهٔ OperationType)، بازرسی سند |
| `Phase1SelfTestRunner` | Diagnostics | آزمون‌های درون‌برنامه‌ای (نه یک فریم‌ورک آزمون) |

## 3. نتیجهٔ Build
- ✅ `dotnet build` موفق (Debug).
- ⚠️ 1 هشدار: `CS8602` در `CatiaDocumentManager.cs:163` (Dereference احتمالی null).
- `Form1.Designer.cs` خالی است (UI کاملاً در کد ساخته می‌شود) — مشکل کامپایلی ندارد.

## 4. یافته‌ها (اولویت‌دار)
| ID | شدت | یافته | Milestone |
|----|-----|-------|-----------|
| A-01 | بالا | یک پروژهٔ واحد؛ لایه‌ها فقط در حد پوشه جدا هستند و جهت وابستگی کنترل نمی‌شود. | M2 |
| A-02 | بالا | `Engineering` به `Infrastructure.Catia` وابسته است (`CatiaBasicPartService.NormalizeDigits`) — وابستگی وارونه. | M2 |
| A-03 | بالا | هیچ Interfaceای وجود ندارد؛ سرویس‌ها در `Form1` با `new` ساخته می‌شوند. | M3 |
| A-04 | بالا | `Form1` مستقیماً به `catia.Documents` (COM) دسترسی دارد (`CloseExtraOpenDocuments`). | M3/M6 |
| A-05 | بالا | `CatiaMechanicalEngine` با `switch` روی رشته OperationType — نقطهٔ توسعهٔ بسته (نقض Open/Closed). | M6 |
| A-06 | بالا | دامنه‌ها با `if/else` کلیدواژه‌ای در موتور تشخیص — افزودن قطعهٔ جدید نیازمند تغییر هسته. | M9/M11 |
| A-07 | متوسط | آزمون‌ها داخل برنامه‌اند؛ پروژهٔ آزمون خودکار (xUnit) و CI وجود ندارد. | M4 |
| A-08 | متوسط | `catch { }` خالی در چند نقطه (DocumentManager، BasicPartService، ConnectionService) — خطای بی‌صدا. | M7 |
| A-09 | متوسط | `CloseIfOpenWithSameTarget` سند هم‌نام را بدون تأیید کاربر می‌بندد (احتمال از دست رفتن تغییرات ذخیره‌نشده). | M7/M12 |
| A-10 | متوسط | عملیات COM روی UI Thread اجرا می‌شود (قفل شدن UI). | M16 |
| A-11 | متوسط | استخراج عدد بدون نام/واحد؛ عدد نامرتبط ممکن است برداشته شود. | M9 |
| A-12 | پایین | `Surface` صرفاً نمایشی است (Skipped) — طبق §57 باید یا پیاده‌سازی یا صریحاً غیرفعال اعلام شود. | M16 |
| A-13 | پایین | فایل‌های `Backup/`, `UpgradeLog*.htm`, `bin/`, `obj/`, `.vs/` در ZIP بودند — وارد ریپو نشدند. | M0 ✅ |
| A-14 | پایین | مسیر محلی ویندوز (`C:\Users\Amin\...`) در csproj — حذف شد. | M0 ✅ |

## 5. عملیات خطرناک روی اسناد CATIA
| محل | عملیات | کنترل فعلی |
|-----|--------|-----------|
| `Form1.CloseExtraOpenDocuments` | بستن همهٔ اسناد | تأیید کاربر ✅، گزارش خطا ✅ |
| `CatiaDocumentManager.CloseIfOpenWithSameTarget` | بستن سند هم‌مسیر | بدون تأیید ⚠️، خطا بلعیده می‌شود ⚠️ |
| `CatiaDocumentManager.SaveAndVerifyPartDocument` | `SaveAs` (بازنویسی) | پشتیبان قبل از بازنویسی ✅ |
| `CatiaDocumentManager.SafeRollbackDocument` | بستن سند ناقص | خطا بلعیده می‌شود ⚠️ |

## 6. قابلیت واقعی در برابر نمایشی
- واقعی (نیازمند CATIA، آزمون‌نشده در این محیط): اتصال، Sketch/Pad اشکال پایه، شفت، چرخ‌دنده (پروفیل تقریبی)، کوپلینگ، میل‌لنگ/شاتون/پیستون، کوادکوپتر، ماشین، ساخت CATProduct، بازرسی سند.
- نمایشی/غیرفعال: Surface Design، Drawing (فقط بازکردن فایل).
- AI: مبتنی بر قواعد محلی؛ ارائه‌دهندهٔ LLM وجود ندارد.
