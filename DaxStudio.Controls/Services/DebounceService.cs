using System;
using System.Windows.Threading;

namespace DaxStudio.Controls.Services
{
    internal class DebounceService : IDebounceService
    {
        private DispatcherTimer _refreshTimer;
        public void Debounce(Action action, TimeSpan delay)
        {
            if (_refreshTimer == null)
            {
                _refreshTimer = new DispatcherTimer
                {
                    Interval = delay
                };
                _refreshTimer.Tick += (s, e) =>
                {
                    _refreshTimer.Stop();
                    action();
                };
            }

            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
            }
            else
            {
                // Reset the timer to debounce rapid calls
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }
        }
    }
}
