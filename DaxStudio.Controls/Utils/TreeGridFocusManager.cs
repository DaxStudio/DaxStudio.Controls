using DaxStudio.Controls.Model;
using System;
using System.Windows;
using System.Windows.Input;

namespace DaxStudio.Controls.Utils
{
    internal class TreeGridFocusManager<T> :IDisposable where T:class
    {
        private bool _disposed = false;
        private TreeGridRow<T> _focusedRow;
        private IInputElement _focusedElement;
        private GenericTreeGrid<T> _treeGrid;

        public TreeGridFocusManager(GenericTreeGrid<T> treeGrid)
        {
            _treeGrid = treeGrid ?? throw new ArgumentNullException(nameof(treeGrid));
            _focusedRow = treeGrid.SelectedItem as TreeGridRow<T>;
            _focusedElement = Keyboard.FocusedElement;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_focusedRow != null)
                {
                    _treeGrid.RestoreSelectionAfterRefresh(_focusedRow);
                }

                // Restore keyboard focus
                _treeGrid.RestoreKeyboardFocus(_focusedElement != null, _focusedElement);

                // Clean up resources here if necessary
                _disposed = true;
            }
        }
    }
}
