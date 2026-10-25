using System;
using System.Threading;

namespace WhoSceneIt
{
    internal sealed class ReportActivity : IDisposable
    {
        private static int _active;
        public static int Active => Volatile.Read(ref _active);
        public ReportActivity() { Interlocked.Increment(ref _active); }
        public void Dispose() { Interlocked.Decrement(ref _active); }
    }
}
