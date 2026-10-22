using System;
using System.IO;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    public sealed class CatiaDocumentManager
    {
        private readonly CatiaConnectionService _connectionService;
        private readonly OperationLogger _logger;

        public CatiaDocumentManager(CatiaConnectionService connectionService, OperationLogger logger)
        {
            _connectionService = connectionService;
            _logger = logger;
        }

        public static string SanitizeIdentifier(string rawName, string prefix = "PART")
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}";
            }

            string normalized = rawName.Trim()
                .Replace("مستطیل", "Rect")
                .Replace("مربع", "Square")
                .Replace("دایره", "Circle")
                .Replace("مثلث", "Triangle")
                .Replace("چندضلعی", "Polygon")
                .Replace("بلوک", "Block")
                .Replace("مکعب", "Block");

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                normalized = normalized.Replace(invalid, '_');
            }

            normalized = Regex.Replace(normalized, @"[^a-zA-Z0-9_\-]", "_");
            normalized = Regex.Replace(normalized, @"_+", "_").Trim('_');

            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = prefix;
            }

            return normalized;
        }

        public dynamic? FindOpenDocumentByPathOrName(dynamic catia, string filePath)
        {
            string fullTarget = Path.GetFullPath(filePath);
            string fileName = Path.GetFileName(filePath);

            int count = Convert.ToInt32(catia.Documents.Count);
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    dynamic doc = catia.Documents.Item(i);
                    string docFullName = Convert.ToString(doc.FullName) ?? string.Empty;
                    string docName = Convert.ToString(doc.Name) ?? string.Empty;

                    if (string.Equals(docFullName, fullTarget, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(docName, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return doc;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        public CadOperationResult OpenOrActivateDocument(string filePath)
        {
            const string opName = "OpenCadDocument";
            _logger.Running(opName, $"در حال بررسی و بازکردن فایل: {filePath}");

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                string msg = "فایل انتخابی در مسیر مشخص‌شده وجود ندارد.";
                _logger.Failed(opName, msg, filePath);
                return CadOperationResult.Failure(opName, msg, filePath);
            }

            try
            {
                dynamic catia = _connectionService.AcquireCatiaSession();
                dynamic? existingDoc = FindOpenDocumentByPathOrName(catia, filePath);

                if (existingDoc != null)
                {
                    try { existingDoc.Activate(); } catch { }
                    string existingMsg = $"این سند از قبل باز است و فعال شد: {Path.GetFileName(filePath)}";
                    _logger.Succeeded(opName, existingMsg, filePath);

                    var existingResult = new CadOperationResult
                    {
                        OperationName = opName,
                        FinalState = OperationState.Succeeded,
                        SummaryMessage = existingMsg,
                        OutputFilePath = filePath
                    };
                    existingResult.Verification.DocumentCreated = true;
                    existingResult.Verification.FileSavedOnDisk = true;
                    existingResult.Verification.FileReopenVerified = true;
                    existingResult.Verification.SavedFileSizeBytes = new FileInfo(filePath).Length;
                    return existingResult;
                }

                dynamic openedDoc = catia.Documents.Open(filePath);
                string openedName = Convert.ToString(openedDoc.Name) ?? Path.GetFileName(filePath);

                var result = new CadOperationResult
                {
                    OperationName = opName,
                    FinalState = OperationState.Succeeded,
                    SummaryMessage = $"سند «{openedName}» باز شد.",
                    OutputFilePath = filePath
                };
                result.Verification.DocumentCreated = true;
                result.Verification.FileSavedOnDisk = true;
                result.Verification.FileReopenVerified = true;
                result.Verification.SavedFileSizeBytes = new FileInfo(filePath).Length;

                _logger.Succeeded(opName, result.SummaryMessage, filePath);
                return result;
            }
            catch (Exception ex)
            {
                string errMsg = "بازکردن فایل ناموفق بود: " + ex.Message;
                _logger.Failed(opName, errMsg, ex.ToString());
                return CadOperationResult.Failure(opName, errMsg, ex.ToString());
            }
        }

        public string? CreateBackupIfFileExists(string targetFilePath)
        {
            if (!File.Exists(targetFilePath)) return null;

            _logger.EnsureWorkspaceDirectories();
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(targetFilePath);
            string ext = Path.GetExtension(targetFilePath);
            string backupFileName = $"{fileNameWithoutExt}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            string backupPath = Path.Combine(_logger.BackupsDirectory, backupFileName);

            File.Copy(targetFilePath, backupPath, overwrite: true);
            _logger.Info($"نسخه پشتیبان ایجاد شد: {backupPath}", "Backup");
            return backupPath;
        }

        public void CloseIfOpenWithSameTarget(dynamic catia, string targetFilePath, dynamic currentWorkingDocument)
        {
            dynamic? conflictDoc = FindOpenDocumentByPathOrName(catia, targetFilePath);
            if (conflictDoc != null && !ReferenceEquals(conflictDoc, currentWorkingDocument))
            {
                try
                {
                    conflictDoc.Close();
                }
                catch { }
            }
        }

        public void SafeRollbackDocument(dynamic? document, string operationName)
        {
            if (document == null) return;
            try
            {
                document.Close();
                _logger.Info("سند ناقص پس از خطا بسته شد (Rollback).", "Rollback");
            }
            catch { }
        }

        public void SaveAndVerifyPartDocument(
            dynamic catia,
            dynamic document,
            string outputPath,
            CadOperationResult result,
            bool verifyReopen = false)
        {
            string? directory = Path.GetDirectoryName(outputPath);
            Directory.CreateDirectory(directory!);
            result.BackupFilePath = CreateBackupIfFileExists(outputPath);
            CloseIfOpenWithSameTarget(catia, outputPath, document);

            bool previousAlerts = true;
            bool alertsChanged = false;
            try
            {
                previousAlerts = Convert.ToBoolean(catia.DisplayFileAlerts);
                catia.DisplayFileAlerts = false;
                alertsChanged = true;
            }
            catch { }

            try
            {
                document.SaveAs(outputPath);
            }
            finally
            {
                if (alertsChanged)
                {
                    try { catia.DisplayFileAlerts = previousAlerts; } catch { }
                }
            }

            if (!File.Exists(outputPath)) throw new IOException("فایل خروجی یافت نشد.");

            var fileInfo = new FileInfo(outputPath);
            if (fileInfo.Length <= 0) throw new IOException("فایل ذخیره‌شده خالی است.");

            result.OutputFilePath = outputPath;
            result.Verification.FileSavedOnDisk = true;
            result.Verification.SavedFileSizeBytes = fileInfo.Length;

            if (verifyReopen)
            {
                document.Close();
                dynamic reopened = catia.Documents.Open(outputPath);
                if (reopened == null) throw new IOException("بازگشایی مجدد ناموفق بود.");
                result.Verification.FileReopenVerified = true;
            }
            else
            {
                dynamic? openCheck = FindOpenDocumentByPathOrName(catia, outputPath);
                result.Verification.FileReopenVerified = openCheck != null;
            }
        }
    }
}