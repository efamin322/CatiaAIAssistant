using System.Runtime.InteropServices;
using CatiaAIAssistant.Core.Errors;

namespace CatiaAIAssistant.Tests.Errors
{
    public class ErrorTranslatorTests
    {
        [Theory]
        [InlineData(unchecked((int)0x80010108), ErrorKind.CadDisconnected)]
        [InlineData(unchecked((int)0x800706BA), ErrorKind.CadDisconnected)]
        [InlineData(unchecked((int)0x800401E3), ErrorKind.CadNotRunning)]
        [InlineData(unchecked((int)0x80010001), ErrorKind.CadOperationRejected)]
        [InlineData(unchecked((int)0x8001010A), ErrorKind.CadOperationRejected)]
        [InlineData(unchecked((int)0x80020009), ErrorKind.CadOperationRejected)]
        [InlineData(unchecked((int)0x80004005), ErrorKind.Unknown)]
        public void ComHResults_AreClassified(int hr, ErrorKind expected)
        {
            var e = ErrorTranslator.Translate(new COMException("x", hr));
            Assert.Equal(expected, e.Kind);
            Assert.False(string.IsNullOrWhiteSpace(e.UserMessage));
            Assert.Contains("COMException", e.TechnicalDetails);
        }

        [Fact]
        public void FileErrors_AreClassified()
        {
            Assert.Equal(ErrorKind.FileNotFound, ErrorTranslator.Translate(new FileNotFoundException("f")).Kind);
            Assert.Equal(ErrorKind.FileAccessDenied, ErrorTranslator.Translate(new UnauthorizedAccessException()).Kind);
            Assert.Equal(ErrorKind.FileInUse, ErrorTranslator.Translate(new IOException("busy", unchecked((int)0x80070020))).Kind);
            Assert.Equal(ErrorKind.DiskFull, ErrorTranslator.Translate(new IOException("full", unchecked((int)0x80070070))).Kind);
        }

        [Fact]
        public void NetworkCancellationAndResources_AreClassified()
        {
            Assert.Equal(ErrorKind.Network, ErrorTranslator.Translate(new HttpRequestException("dns")).Kind);
            Assert.Equal(ErrorKind.Network, ErrorTranslator.Translate(new TimeoutException()).Kind);
            Assert.Equal(ErrorKind.Cancelled, ErrorTranslator.Translate(new OperationCanceledException()).Kind);
            var oom = ErrorTranslator.Translate(new OutOfMemoryException());
            Assert.Equal(ErrorKind.OutOfResources, oom.Kind);
            Assert.False(oom.IsRecoverable);
        }

        [Fact]
        public void WrappedExceptions_AreUnwrapped()
        {
            var wrapped = new AggregateException(new System.Reflection.TargetInvocationException(new COMException("gone", unchecked((int)0x80010108))));
            Assert.Equal(ErrorKind.CadDisconnected, ErrorTranslator.Translate(wrapped).Kind);
        }
    }
}
