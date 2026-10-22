using System.Runtime.InteropServices;

namespace CatiaAIAssistant.Infrastructure.Catia
{
    /// <summary>
    /// Collects COM RCWs created during one CAD operation and releases them deterministically in reverse order
    /// (Spec §05.2). Usage: <c>using var com = new ComObjectTracker(); dynamic docs = com.Track(catia.Documents);</c>
    /// Release failures are counted, never thrown, so cleanup cannot mask the original error.
    /// </summary>
    public sealed class ComObjectTracker : IDisposable
    {
        private readonly Stack<object> _objects = new();
        private bool _disposed;

        public int TrackedCount => _objects.Count;
        public int ReleaseFailures { get; private set; }

        public T Track<T>(T comObject)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (comObject is not null && OperatingSystem.IsWindows() && Marshal.IsComObject(comObject))
                _objects.Push(comObject);
            return comObject;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            while (_objects.Count > 0)
            {
                var o = _objects.Pop();
                try { if (OperatingSystem.IsWindows()) Marshal.ReleaseComObject(o); }
                catch (Exception) { ReleaseFailures++; }
            }
        }
    }
}
