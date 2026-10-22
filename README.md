# CATIA AI Engineering Assistant

[![CI](https://github.com/efamin322/CatiaAIAssistant/actions/workflows/ci.yml/badge.svg)](https://github.com/efamin322/CatiaAIAssistant/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/efamin322/CatiaAIAssistant?include_prereleases)](https://github.com/efamin322/CatiaAIAssistant/releases)

دستیار مهندسی برای CATIA V5 (C# / .NET 8 WinForms، COM Automation): درخواست مهندسی به زبان طبیعی (فارسی/انگلیسی) ← مشخصات و محاسبات قابل بررسی ← مدل پارامتریک در CATIA.

> ⚖️ توسعهٔ این پروژه تابع **[قرارداد معماری](ARCHITECTURE_CONTRACT.md)** است: هیچ تغییری نباید ماژولار بودن و توسعه‌پذیری اپ را زیر سؤال ببرد.

## دانلود و نصب
از صفحهٔ **[Releases](https://github.com/efamin322/CatiaAIAssistant/releases)** یکی را دانلود کنید:
- `CatiaAIAssistant-Setup-<ver>-win-x64.exe` — نصب‌کننده (Start Menu + Uninstall)
- `CatiaAIAssistant-<ver>-win-x64-portable.zip` — نسخهٔ قابل‌حمل (بدون نصب؛ نیاز به .NET نصب‌شده ندارد)
- `CatiaAIAssistant_Complete_Engineering-<ver>.zip` — کد منبع کامل

پیش‌نیاز: Windows 10/11 x64. برای قابلیت‌های CAD: CATIA V5 نصب و در حال اجرا.

## Build از سورس
```bash
dotnet build CatiaAIAssistant.sln -c Release
dotnet test  CatiaAIAssistant.sln --filter "Category!=CatiaIntegration"
```

## انتشار نسخهٔ جدید
```bash
git tag v0.2.0 && git push origin v0.2.0   # workflow «Release» خودکار نصب‌کننده و ZIP می‌سازد
```

## ساختار
```
src/CatiaAIAssistant/                       App (WinForms، Composition Root)
src/CatiaAIAssistant.Core/                  مدل‌های دامنه، لاگ، ابزار متنی (net8.0)
src/CatiaAIAssistant.Engineering/           تحلیل درخواست و محاسبات (net8.0)
src/CatiaAIAssistant.Infrastructure.Catia/  اتوماسیون COM کتیا (net8.0-windows)
tests/CatiaAIAssistant.Tests/               آزمون‌های xUnit + آزمون قرارداد معماری
installer/               اسکریپت Inno Setup
docs/MASTER_PROMPT_V3.md مشخصات کامل توسعه
docs/reports/            گزارش‌های ممیزی و آزمون
ROADMAP.md               نقشهٔ راه و وضعیت هر مرحله
ARCHITECTURE_CONTRACT.md قرارداد الزام‌آور معماری
```

## وضعیت
نسخهٔ فعلی: **v0.3.0** — Interfaceها و Composition Root، گردش کار پروژه، فهرست مجاز عملیات CAD، مدیریت خطا/لغو + ۸۸ آزمون خودکار. جزئیات در [ROADMAP.md](ROADMAP.md) و [گزارش ممیزی](docs/reports/AUDIT_v0.1.0.md).
