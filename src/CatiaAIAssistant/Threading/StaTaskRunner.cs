using System;
using System.Threading;
using System.Threading.Tasks;

namespace CatiaAIAssistant.Threading
{
    /// <summary>
    /// Runs long CAD work on a dedicated STA thread so the UI thread is never blocked (Spec §05.3, contract §5.3).
    /// STA is required because COM automation servers like CATIA expect apartment-threaded callers; each operation
    /// acquires its own CATIA session inside the worker, so no RCW crosses apartments.
    /// </summary>
    public static class StaTaskRunner
    {
        public static Task<T> Run<T>(Func<T> work)
        {
            ArgumentNullException.ThrowIfNull(work);
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                try { tcs.SetResult(work()); }
                catch (Exception ex) { tcs.SetException(ex); }
            })
            {
                IsBackground = true,
                Name = "CAD-Worker"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }
    }
}
