using DaxStudio.Controls.Model;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DaxStudio.Controls.Utils
{
    // Helper class for deferring ObservableCollection notifications
    internal class DeferRefresh<T> : IDisposable
    {
        private readonly ObservableCollection<TreeGridRow<T>> _collection;
        private readonly PropertyChangedEventHandler _propertyChangedHandler;
        private readonly NotifyCollectionChangedEventHandler _collectionChangedHandler;

        public DeferRefresh(ObservableCollection<TreeGridRow<T>> collection)
        {

            _collection = collection;
            // Store original handlers and temporarily remove them
            var collectionChangedField = typeof(ObservableCollection<TreeGridRow<T>>)
                .GetField("CollectionChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var propertyChangedField = typeof(ObservableCollection<TreeGridRow<T>>)
                .GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            _collectionChangedHandler = (NotifyCollectionChangedEventHandler)collectionChangedField?.GetValue(_collection);
            _propertyChangedHandler = (PropertyChangedEventHandler)propertyChangedField?.GetValue(_collection);

            // Temporarily clear the handlers
            collectionChangedField?.SetValue(_collection, null);
            propertyChangedField?.SetValue(_collection, null);
        }

        public void Dispose()
        {
            // Restore handlers and fire a reset notification
            var collectionChangedField = _collection.GetType()
                .GetField("CollectionChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var propertyChangedField = _collection.GetType()
                .GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if(_collectionChangedHandler != null)
                collectionChangedField?.SetValue(_collection, _collectionChangedHandler);
            if (_propertyChangedHandler != null)
                propertyChangedField?.SetValue(_collection, _propertyChangedHandler);

            // Fire a reset notification
            _collectionChangedHandler?.Invoke(_collection, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
