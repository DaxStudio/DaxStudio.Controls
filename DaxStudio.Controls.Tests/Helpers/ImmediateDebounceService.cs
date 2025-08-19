using DaxStudio.Controls.Services;
using System;

namespace DaxStudio.Controls.Tests.Helpers
{
    internal class ImmediateDebounceService : IDebounceService
    {
        public void Debounce(Action action, TimeSpan delay)
        {
            // ignore the delay and execute immediately
            action();
        }
    }
}
