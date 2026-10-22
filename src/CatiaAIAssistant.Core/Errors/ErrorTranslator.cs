using System.Runtime.InteropServices;

namespace CatiaAIAssistant.Core.Errors
{
    public enum ErrorKind
    {
        Unknown,
        CadNotRunning,
        CadDisconnected,
        CadOperationRejected,
        FileNotFound,
        FileAccessDenied,
        FileInUse,
        DiskFull,
        Network,
        OutOfResources,
        Cancelled,
        InvalidInput
    }

    /// <summary>User-facing message + full technical details for diagnostics (Spec §05).</summary>
    public sealed class TranslatedError
    {
        public ErrorKind Kind { get; init; }
        public string UserMessage { get; init; } = string.Empty;
        public string TechnicalDetails { get; init; } = string.Empty;
        public bool IsRecoverable { get; init; } = true;
    }

    /// <summary>
    /// Classifies exceptions into actionable categories instead of hiding them behind a generic catch.
    /// Pure and platform-neutral: HRESULTs are compared numerically, no COM calls are made.
    /// </summary>
    public static class ErrorTranslator
    {
        // Well-known HRESULTs seen with out-of-process COM automation servers such as CATIA V5.
        public const int RPC_E_DISCONNECTED = unchecked((int)0x80010108);
        public const int RPC_S_SERVER_UNAVAILABLE = unchecked((int)0x800706BA);
        public const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        public const int RPC_E_SERVERCALL_RETRYLATER = unchecked((int)0x8001010A);
        public const int MK_E_UNAVAILABLE = unchecked((int)0x800401E3);
        public const int CO_E_CLASSSTRING = unchecked((int)0x800401F3);
        public const int DISP_E_EXCEPTION = unchecked((int)0x80020009);
        private const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
        private const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
        private const int ERROR_DISK_FULL = unchecked((int)0x80070070);
        private const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);

        public static TranslatedError Translate(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            var root = Unwrap(ex);
            string details = ex.ToString();

            return root switch
            {
                OperationCanceledException => Make(ErrorKind.Cancelled, "عملیات توسط کاربر لغو شد.", details),
                COMException com => FromHResult(com.HResult, com.Message, details),
                OutOfMemoryException or InsufficientExecutionStackException =>
                    Make(ErrorKind.OutOfResources, "حافظه یا منابع سیستم کافی نیست. برنامه‌های دیگر را ببندید و دوباره تلاش کنید.", details, false),
                FileNotFoundException or DirectoryNotFoundException =>
                    Make(ErrorKind.FileNotFound, "فایل یا پوشهٔ موردنظر پیدا نشد. مسیر را بررسی کنید.", details),
                UnauthorizedAccessException =>
                    Make(ErrorKind.FileAccessDenied, "اجازهٔ دسترسی به فایل یا پوشه وجود ندارد. مسیر دیگری انتخاب کنید یا مجوزها را بررسی کنید.", details),
                IOException io when io.HResult == ERROR_SHARING_VIOLATION || io.HResult == ERROR_LOCK_VIOLATION =>
                    Make(ErrorKind.FileInUse, "فایل توسط برنامهٔ دیگری (احتمالاً CATIA) باز است. آن را ببندید و دوباره تلاش کنید.", details),
                IOException io when io.HResult == ERROR_DISK_FULL || io.HResult == ERROR_HANDLE_DISK_FULL =>
                    Make(ErrorKind.DiskFull, "فضای دیسک کافی نیست.", details),
                HttpRequestException or System.Net.Sockets.SocketException or TimeoutException =>
                    Make(ErrorKind.Network, "ارتباط شبکه برقرار نشد. اتصال اینترنت یا تنظیمات پراکسی را بررسی کنید؛ قابلیت‌های محلی همچنان در دسترس‌اند.", details),
                ArgumentException or FormatException or KeyNotFoundException =>
                    Make(ErrorKind.InvalidInput, "ورودی نامعتبر است: " + root.Message, details),
                _ => Make(ErrorKind.Unknown, "خطای پیش‌بینی‌نشده رخ داد: " + root.Message, details)
            };
        }

        public static TranslatedError FromHResult(int hresult, string message, string details) => hresult switch
        {
            RPC_E_DISCONNECTED or RPC_S_SERVER_UNAVAILABLE =>
                Make(ErrorKind.CadDisconnected, "ارتباط با CATIA قطع شد (برنامه بسته شده یا از کار افتاده است). CATIA را دوباره باز کنید و اتصال را بررسی کنید.", details),
            MK_E_UNAVAILABLE or CO_E_CLASSSTRING =>
                Make(ErrorKind.CadNotRunning, "نشست فعالی از CATIA پیدا نشد یا CATIA ثبت نشده است. ابتدا CATIA را باز کنید.", details),
            RPC_E_CALL_REJECTED or RPC_E_SERVERCALL_RETRYLATER =>
                Make(ErrorKind.CadOperationRejected, "CATIA مشغول است (احتمالاً یک پنجرهٔ گفتگو باز است). پنجره‌های باز CATIA را ببندید و دوباره تلاش کنید.", details),
            DISP_E_EXCEPTION =>
                Make(ErrorKind.CadOperationRejected, "CATIA عملیات را رد کرد: " + message, details),
            _ => Make(ErrorKind.Unknown, $"خطای COM (0x{hresult:X8}): {message}", details)
        };

        private static Exception Unwrap(Exception ex)
        {
            while (ex is AggregateException { InnerExceptions.Count: 1 } agg) ex = agg.InnerException!;
            while (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie) ex = tie.InnerException;
            return ex;
        }

        private static TranslatedError Make(ErrorKind kind, string user, string details, bool recoverable = true) =>
            new() { Kind = kind, UserMessage = user, TechnicalDetails = details, IsRecoverable = recoverable };
    }
}
