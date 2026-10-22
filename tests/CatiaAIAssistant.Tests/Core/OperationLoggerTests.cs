using CatiaAIAssistant.Core.Abstractions;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Tests.Core
{
    public class OperationLoggerTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "catia_ai_tests_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Logger_CreatesWorkspace_WritesSessionFile_AndRaisesEvent()
        {
            IOperationLogger logger = new OperationLogger(_root);
            OperationLogEntry? received = null;
            logger.EntryAdded += e => received = e;

            logger.Failed("Op", "پیام خطا", "details");

            Assert.True(Directory.Exists(logger.PartsOutputDirectory));
            Assert.True(Directory.Exists(logger.BackupsDirectory));
            Assert.NotNull(received);
            Assert.Equal(OperationState.Failed, received!.State);
            Assert.Single(logger.GetEntries());
            string file = File.ReadAllText(logger.SessionLogFilePath);
            Assert.Contains("پیام خطا", file);
            Assert.Contains("details", file);
        }

        [Fact]
        public void Logger_RejectsEmptyWorkspace()
        {
            Assert.Throws<ArgumentException>(() => new OperationLogger(" "));
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }
    }
}
