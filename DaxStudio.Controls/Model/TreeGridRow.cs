using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private bool _hasChildren;
        private bool _hasChildrenCalculated = false;

        public T Data { get; set; }
        public int Level { get; set; }
        public TreeGridRow<T> Parent { get; set; }
        public List<TreeGridRow<T>> Children
        {
            get => _children;
            set
            {
                _children = value;
                _hasChildrenCalculated = false; // Invalidate cache
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasChildren));
            }
        }
        private List<TreeGridRow<T>> _children = new List<TreeGridRow<T>>();

        public bool IsCollapsing { get; set; }
        /// <summary>
        /// Array indicating whether each ancestor level is the last child of its parent
        /// Used for drawing tree lines efficiently without runtime calculations
        /// </summary>
        public List<bool> Ancestors { get; set; } = new List<bool>();
        public ObservableCollection<bool> SelectedLineLevels { get; set; } = new ObservableCollection<bool>();

        public bool HasChildren
        {
            get
            {
                // Cache the result to avoid repeated LINQ calls
                if (!_hasChildrenCalculated)
                {
                    _hasChildren = Children?.Count > 0;
                    _hasChildrenCalculated = true;
                }
                return _hasChildren;
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
                    if (_isExpanded) IsCollapsing = false;
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

        // Add method to add children efficiently
        public void AddChild(TreeGridRow<T> child)
        {
            Children.Add(child);
            if (!_hasChildren)
            {
                _hasChildren = true;
                _hasChildrenCalculated = true;
                NotifyOfPropertyChange(nameof(HasChildren));
            }
        }

        // Add this method to properly reset selection state
        public void ClearSelectionState()
        {
            IsCollapsing = false;
            if (SelectedLineLevels != null)
            {
                for (int i = 0; i < SelectedLineLevels.Count; i++)
                {
                    SelectedLineLevels[i] = false;
                }
            }
        }

        // Add this method to ensure consistent selection level initialization
        public void EnsureSelectionLevels(int requiredLevels)
        {
            if (SelectedLineLevels == null)
            {
                SelectedLineLevels = new ObservableCollection<bool>();
            }
            
            while (SelectedLineLevels.Count < requiredLevels)
            {
                SelectedLineLevels.Add(false);
            }
        }
    }

}
