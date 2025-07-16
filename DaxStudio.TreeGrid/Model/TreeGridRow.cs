using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DaxStudio.Controls.Model
{
    /// <summary>
    /// Represents a row in the hierarchical data grid
    /// </summary>
    public class TreeGridRow<T> : INotifyPropertyChanged
    {
        private bool _isExpanded;

        public T Data { get; set; }
        public int Level { get; set; }
        public TreeGridRow<T> Parent { get; set; }
        public List<TreeGridRow<T>> Children { get; set; } = new List<TreeGridRow<T>>();
        
        /// <summary>
        /// Array indicating whether each ancestor level is the last child of its parent
        /// Used for drawing tree lines efficiently without runtime calculations
        /// </summary>
        public List<bool> Ancestors { get; set; } = new List<bool>();
        
        private bool _hasChildren;
        public bool HasChildren
        {
            get => Children.Any();

        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void OnPropertyChanged<TProperty>(System.Linq.Expressions.Expression<Func<TProperty>> propertyExpression)
        {
            if (propertyExpression.Body is System.Linq.Expressions.MemberExpression memberExpression)
            {
                OnPropertyChanged(memberExpression.Member.Name);
            }
        }

        protected void NotifyOfPropertyChange([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            OnPropertyChanged(propertyName);
        }
    }

    /// <summary>
    /// Non-generic version for backward compatibility
    /// </summary>
    public class HierarchicalDataGridRow : TreeGridRow<object>
    {
    }
}
