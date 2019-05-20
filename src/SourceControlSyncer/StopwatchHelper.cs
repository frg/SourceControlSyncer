using System;
using System.Diagnostics;

namespace SourceControlSyncer
{
    public class StopwatchHelper : IDisposable
    {
        private readonly Action<TimeSpan> _callback;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public StopwatchHelper()
        {
            _stopwatch.Start();
        }

        public StopwatchHelper(Action<TimeSpan> callback) : this()
        {
            _callback = callback;
        }

        public TimeSpan Result => _stopwatch.Elapsed;

        public void Dispose()
        {
            _stopwatch.Stop();
            _callback?.Invoke(Result);
        }

        public static StopwatchHelper Start(Action<TimeSpan> callback)
        {
            return new StopwatchHelper(callback);
        }
    }
}