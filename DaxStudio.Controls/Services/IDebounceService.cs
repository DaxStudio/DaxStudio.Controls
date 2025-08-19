using System;

namespace DaxStudio.Controls.Services
{
    public interface IDebounceService
    {
        void Debounce(Action action, TimeSpan delay);
    }
}
