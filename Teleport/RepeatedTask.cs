using System;
using System.Timers;

namespace Teleport
{
    internal class RepeatedTask : IDisposable
    {
        internal Timer _timer;

        internal RepeatedTask(double interval)
        {
            _timer = new Timer(interval);
        }

        internal void Start() => _timer.Start();

        internal void Stop() => _timer.Stop();

        internal void SetInterval(double interval) => _timer.Interval = interval;

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
