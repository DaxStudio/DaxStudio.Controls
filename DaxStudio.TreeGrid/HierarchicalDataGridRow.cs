using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DaxStudio.TreeGrid
{
    /// <summary>
    /// Represents a row in the hierarchical data grid
    /// </summary>
    public class HierarchicalDataGridRow : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public object Data { get; set; }
        public int Level { get; set; }
        public HierarchicalDataGridRow Parent { get; set; }
        public List<HierarchicalDataGridRow> Children { get; set; } = new List<HierarchicalDataGridRow>();
        private bool _hasChildren;
        public bool HasChildren
        {
            get => _hasChildren;
            set
            {
                _hasChildren = value;
                OnPropertyChanged(nameof(HasChildren));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnPropertyChanged<T>(System.Linq.Expressions.Expression<Func<T>> propertyExpression)
        {
            if (propertyExpression.Body is System.Linq.Expressions.MemberExpression memberExpression)
            {
                OnPropertyChanged(memberExpression.Member.Name);
            }
        }

        protected virtual void NotifyOfPropertyChange([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
