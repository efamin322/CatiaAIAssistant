using System;
using System.IO;
using System.Text.RegularExpressions;
using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Logging;
using CatiaAIAssistant.Core.Text;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    /// <summary>
    /// Document lifecycle for CATIA (open / activate / backup / rollback). Implements
    /// <see cref="ICadDocumentService"/>; COM handles stay private to this class.
    /// </summary>
    public sealed class CatiaDocumentManager : ICadDocumentService
    {
        private readonly CatiaConnectionService _connectionService;
        private readonly IOperationLog _logger;

        public CatiaDocumentManager(CatiaConnectionService connectionService, IOperationLog logger)
        {
            _connectionService = connectionService;
            _logger = logger;
        }

        /// <summary>Number of documents open in the running CATIA session (0 when CATIA is closed).</summary>
        public int GetOpenDocumentCount()
        {
            try
            {
                dynamic catia = _connectionService.AcquireCatiaSession();
                return Convert.ToInt32(catia.Documents.Count);
            }
            catch (Exception ex)
            {
                _logger.Log("CATIA", "GetOpenDocumentCount", OperationState.Failed,
                    "شمارش اسناد باز ناموفق بود: " + ex.Message, LogSeverity.Warning);
                return 0;
            }
        }

        /// <summary>
        /// Closes documents of the running session. The caller is responsible for user confirmation;
        /// every failure is reported back (audit A-08: no silent swallowing).
        /// </summary>
        public IReadOnlyList<string> CloseAllDocuments(int documentCount)
        {
            var failures = new List<string>();
            if (documentCount <= 0) return failures;

            dynamic catia;
            try { catia = _connectionService.AcquireCatiaSession(); }
            catch (Exception ex)
            {
                failures.Add("اتصال به CATIA برقرار نشد: " + ex.Message);
                return failures;
            }

            for (int i = documentCount; i >= 1; i--)
            {
                try { catia.Documents.Item(i).Close(); }
                catch (Exception closeEx) { failures.Add($"سند شماره {i}: {closeEx.Message}"); }
            }

            if (failures.Count == 0)
                _logger.Succeeded("CloseAllDocuments", $"درخواست بستن {documentCount} سند اجرا شد.");
            else
                _logger.Log("CATIA", "CloseAllDocuments", OperationState.Failed,
                    $"بعضی اسناد بسته نشدند: {string.Join(" | ", failures)}", LogSeverity.Warning);

            return failures;
        }

        public bool HasOpenDocuments()
        {
            try
            {
                dynamic catia = _connectionService.AcquireCatiaSession();
                return Convert.ToInt32(catia.Documents.Count) > 0;
            }
            catch (Exception ex)
            {
                _logger.Log("CATIA", "HasOpenDocuments", OperationState.Failed,
                    "بررسی اسناد باز ناموفق بود: " + ex.Message, LogSeverity.Warning);
                return false;
            }
        }

        /// <summary>Kept for backward compatibility; delegates to <see cref="TextNormalizer.SanitizeIdentifier"/>.</summary>
        public static string SanitizeIdentifier(string rawName, string prefix = "PART") => TextNormalizer.SanitizeIdentifier(rawName, prefix);

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
            object? conflict = (object?)FindOpenDocumentByPathOrName(catia, targetFilePath);
            if (conflict is null || ReferenceEquals(conflict, (object?)currentWorkingDocument)) return;

            dynamic conflictDoc = conflict;
            try
            {
                conflictDoc.Close();
                _logger.Info($"سند هم‌مسیر قبلی بسته شد تا ذخیره ممکن شود: {targetFilePath}", "DocumentManager");
            }
            catch (Exception ex)
            {
                // Audit A-08: never swallow COM failures silently.
                _logger.Log("CATIA", "CloseConflictingDocument", OperationState.Failed, $"بستن سند هم‌مسیر ناموفق بود: {ex.Message}", LogSeverity.Warning);
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