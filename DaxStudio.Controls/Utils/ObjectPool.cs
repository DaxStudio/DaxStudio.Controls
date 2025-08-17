using System;
using System.Collections.Concurrent;

namespace DaxStudio.Controls.Utils
{
    public class ObjectPool<T>
    {
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;

        public ObjectPool(Func<T> factory, Action<T> reset = null, int initialSize = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _reset = reset;

            for (int i = 0; i < initialSize; i++)
            {
                _queue.Enqueue(_factory());
            }
        }

        public T Get()
        {
            if (_queue.TryDequeue(out T item))
            {
                return item;
            }
            return _factory();
        }

        public void Return(T item)
        {
            if (item != null)
            {
                _reset?.Invoke(item);
                _queue.Enqueue(item);
            }
        }
    }
}