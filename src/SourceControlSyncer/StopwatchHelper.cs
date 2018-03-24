using System;
using System.Diagnostics;

namespace SourceControlSyncer
{
    public class StopwatchHelper : IDisposable
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Action<TimeSpan> _callback;

        public StopwatchHelper()
        {
            _stopwatch.Start();
        }

        public StopwatchHelper(Action<TimeSpan> callback) : this()
        {
            _callback = callback;
        }

        public static StopwatchHelper Start(Action<TimeSpan> callback)
        {
            return new StopwatchHelper(callback);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _callback?.Invoke(Result);
        }

        public TimeSpan Result => _stopwatch.Elapsed;
    }
}
