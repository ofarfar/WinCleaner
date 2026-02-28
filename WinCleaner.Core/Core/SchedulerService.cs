using System;
using System.Threading;
using System.Threading.Tasks;
using WinCleaner.Utils;

namespace WinCleaner.Core
{
    public class SchedulerService : IDisposable
    {
        private System.Threading.Timer? _timer;
        private bool _disposed;

        public bool IsEnabled { get; private set; }
        public TimeSpan Interval { get; private set; }

        /// <summary>Raised when a scheduled clean is triggered.</summary>
        public event EventHandler? CleanTriggered;

        /// <summary>Start the scheduler with the given interval.</summary>
        public void Start(TimeSpan interval)
        {
            Stop();
            Interval = interval;
            IsEnabled = true;

            _timer = new System.Threading.Timer(
                callback: _ => OnCleanTriggered(),
                state: null,
                dueTime: interval,
                period: interval);

            AppLogger.Info($"Scheduler started (interval: {interval.TotalHours:F1} hours)");
        }

        /// <summary>Stop the scheduler.</summary>
        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            IsEnabled = false;
        }

        private void OnCleanTriggered()
        {
            AppLogger.Info("Scheduled clean triggered.");
            CleanTriggered?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
