using System;
using System.Runtime.InteropServices;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    public sealed class CatiaConnectionInfo
    {
        public bool IsConnected { get; init; }
        public string ApplicationName { get; init; } = string.Empty;
        public int OpenDocumentsCount { get; init; }
        public string ActiveDocumentName { get; init; } = "بدون سند فعال";
        public bool AttachedToExistingInstance { get; init; }
        public string StatusMessage { get; init; } = string.Empty;
        public string? ErrorDetails { get; init; }
    }

    public sealed class CatiaConnectionService
    {
        public const string DefaultProgId = "CATIA.Application";
        private readonly OperationLogger _logger;

        public bool AllowAutoLaunchIfClosed { get; set; } = false;

        public CatiaConnectionService(OperationLogger logger)
        {
            _logger = logger;
        }

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

        public CatiaConnectionInfo CheckConnection()
        {
            _logger.Running("CheckCatiaConnection", "در حال بررسی اتصال COM به CATIA...", "COM");

            try
            {
                object? comObj = TryGetActiveCatiaInstance(out bool attachedExisting, out string? comError);

                if (comObj == null && AllowAutoLaunchIfClosed)
                {
                    Type? catiaType = Type.GetTypeFromProgID(DefaultProgId);
                    if (catiaType != null)
                    {
                        comObj = Activator.CreateInstance(catiaType);
                        attachedExisting = false;
                    }
                }

                if (comObj == null)
                {
                    string reason = comError ?? "نشست فعالی از CATIA V5 در حال اجرا یافت نشد.";
                    _logger.Failed("CheckCatiaConnection", "اتصال به CATIA برقرار نیست: " + reason, comError, "COM");
                    return new CatiaConnectionInfo
                    {
                        IsConnected = false,
                        StatusMessage = "CATIA بسته است یا نشست COM فعال نیست.",
                        ErrorDetails = reason
                    };
                }

                dynamic catia = comObj;
                string appName = Convert.ToString(catia.Name) ?? "CATIA";
                int docCount = Convert.ToInt32(catia.Documents.Count);
                string activeDocName = "بدون سند فعال";

                if (docCount > 0)
                {
                    try
                    {
                        dynamic activeDoc = catia.ActiveDocument;
                        if (activeDoc != null)
                        {
                            activeDocName = Convert.ToString(activeDoc.Name) ?? "نامشخص";
                        }
                    }
                    catch
                    {
                        activeDocName = "عدم دسترسی به سند فعال";
                    }
                }

                string msg = $"اتصال برقرار است | برنامه: {appName} | اسناد باز: {docCount} | سند فعال: {activeDocName}";
                _logger.Succeeded("CheckCatiaConnection", msg, null, "COM");

                return new CatiaConnectionInfo
                {
                    IsConnected = true,
                    ApplicationName = appName,
                    OpenDocumentsCount = docCount,
                    ActiveDocumentName = activeDocName,
                    AttachedToExistingInstance = attachedExisting,
                    StatusMessage = msg
                };
            }
            catch (Exception ex)
            {
                _logger.Failed("CheckCatiaConnection", "خطا در بررسی اتصال به CATIA: " + ex.Message, ex.ToString(), "COM");
                return new CatiaConnectionInfo
                {
                    IsConnected = false,
                    StatusMessage = "خطا در ارتباط COM با CATIA",
                    ErrorDetails = ex.Message
                };
            }
        }

        public dynamic AcquireCatiaSession()
        {
            object? comObj = TryGetActiveCatiaInstance(out _, out string? comError);
            if (comObj != null)
            {
                return comObj;
            }

            if (AllowAutoLaunchIfClosed)
            {
                Type? catiaType = Type.GetTypeFromProgID(DefaultProgId);
                if (catiaType == null)
                {
                    throw new InvalidOperationException("ثبت COM مربوط به CATIA.Application در رجیستری ویندوز یافت نشد.");
                }

                object? created = Activator.CreateInstance(catiaType);
                if (created != null)
                {
                    dynamic catia = created;
                    try { catia.Visible = true; } catch { }
                    return catia;
                }
            }

            throw new InvalidOperationException(
                "نشست فعالی از نرم‌افزار CATIA V5 یافت نشد. لطفاً ابتدا CATIA را باز کنید." +
                (string.IsNullOrWhiteSpace(comError) ? string.Empty : $" ({comError})"));
        }

        private static object? TryGetActiveCatiaInstance(out bool attachedExisting, out string? errorMessage)
        {
            attachedExisting = false;
            errorMessage = null;

            int hrClsid = CLSIDFromProgID(DefaultProgId, out Guid clsid);
            if (hrClsid < 0)
            {
                errorMessage = $"ProgID '{DefaultProgId}' در ویندوز ثبت نشده است (HRESULT: 0x{hrClsid:X8}).";
                return null;
            }

            int hrActive = GetActiveObject(ref clsid, IntPtr.Zero, out object? unk);
            if (hrActive >= 0 && unk != null)
            {
                attachedExisting = true;
                return unk;
            }

            errorMessage = $"نشست در حال اجرای CATIA یافت نشد (HRESULT: 0x{hrActive:X8}).";
            return null;
        }
    }
}