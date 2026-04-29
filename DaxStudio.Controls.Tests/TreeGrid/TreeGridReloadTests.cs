using DaxStudio.Controls.Services;
using DaxStudio.Controls.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Xunit;

namespace DaxStudio.Controls.Tests.TreeGrid
{
    /// <summary>
    /// Regression tests covering the Unloaded -> mutate source -> Loaded cycle.
    ///
    /// When a <see cref="GenericTreeGrid{T}"/> is hosted inside a control that
    /// physically detaches it from the visual tree (e.g. an AvalonDock
    /// LayoutAnchorable in auto-hide mode) the Unloaded handler tears down all
    /// CollectionChanged subscriptions via Cleanup(). Without a matching
    /// re-subscription in OnLoaded any subsequent mutation of the bound
    /// RootItems collection (or any child collection) is silently ignored, so
    /// the grid keeps showing stale content and operations such as Clear appear
    /// to be no-ops.
    /// </summary>
    [Collection("WPF Tests")]
    public class TreeGridReloadTests : IDisposable
    {
        public class TestTreeItem
        {
            public TestTreeItem(string name, IEnumerable<TestTreeItem> children = null)
            {
                Name = name;
                Children = children != null
                    ? new ObservableCollection<TestTreeItem>(children)
                    : new ObservableCollection<TestTreeItem>();
            }

            public string Name { get; set; }
            public ObservableCollection<TestTreeItem> Children { get; }
        }

        public TreeGridReloadTests()
        {
            WpfTestHelper.InitializeWpfEnvironment();
        }

        public void Dispose()
        {
            // No cleanup needed
        }

        /// <summary>
        /// Synchronous debounce service so tests don't have to wait on
        /// <see cref="System.Windows.Threading.DispatcherTimer"/>.
        /// </summary>
        private sealed class ImmediateDebounceService : IDebounceService
        {
            public void Debounce(Action action, TimeSpan delay) => action?.Invoke();
        }

        private static GenericTreeGrid<TestTreeItem> CreateGrid(ObservableCollection<TestTreeItem> rootItems)
        {
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                DebounceService = new ImmediateDebounceService(),
                ChildrenBindingPath = nameof(TestTreeItem.Children),
                ExpandOnLoad = true,
                RootItems = rootItems
            };

            treeGrid.Measure(new Size(500, 500));
            treeGrid.Arrange(new Rect(0, 0, 500, 500));
            WpfTestHelper.DoEvents();

            return treeGrid;
        }

        private static void RaiseLoaded(FrameworkElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, element));
            WpfTestHelper.DoEvents();
        }

        private static void RaiseUnloaded(FrameworkElement element)
        {
            element.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, element));
            WpfTestHelper.DoEvents();
        }

        [UIFact]
        public void RootItems_AddedAfterReload_AreReflectedInTheGrid()
        {
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1")
            };

            var treeGrid = CreateGrid(rootItems);
            RaiseLoaded(treeGrid);
            Assert.Single(treeGrid.Items);

            // Simulate the host (e.g. AvalonDock auto-hide) detaching the control
            RaiseUnloaded(treeGrid);

            // Mutate the source while the grid is "unloaded"
            rootItems.Add(new TestTreeItem("Root2"));
            rootItems.Add(new TestTreeItem("Root3"));

            // Re-attach the control
            RaiseLoaded(treeGrid);

            Assert.Equal(3, treeGrid.Items.Count);
        }

        [UIFact]
        public void RootItems_ClearedAfterReload_AreReflectedInTheGrid()
        {
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1"),
                new TestTreeItem("Root2")
            };

            var treeGrid = CreateGrid(rootItems);
            RaiseLoaded(treeGrid);
            Assert.Equal(2, treeGrid.Items.Count);

            RaiseUnloaded(treeGrid);

            // The Clear button on a host view-model would do exactly this
            rootItems.Clear();

            RaiseLoaded(treeGrid);

            Assert.Empty(treeGrid.Items);
        }

        [UIFact]
        public void ChildItems_AddedAfterReload_AreReflectedInTheGrid()
        {
            var root = new TestTreeItem("Root1");
            var rootItems = new ObservableCollection<TestTreeItem> { root };

            var treeGrid = CreateGrid(rootItems);
            RaiseLoaded(treeGrid);

            // Initially: just the single root, expanded with no children
            Assert.Single(treeGrid.Items);

            RaiseUnloaded(treeGrid);

            // Mutate a child collection while the grid is detached
            root.Children.Add(new TestTreeItem("Child1"));
            root.Children.Add(new TestTreeItem("Child2"));

            RaiseLoaded(treeGrid);

            // Root + 2 children should now be visible (ExpandOnLoad = true)
            Assert.Equal(3, treeGrid.Items.Count);
        }

        [UIFact]
        public void MultipleReloadCycles_KeepGridInSyncWithSource()
        {
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1")
            };

            var treeGrid = CreateGrid(rootItems);
            RaiseLoaded(treeGrid);
            Assert.Single(treeGrid.Items);

            for (int i = 2; i <= 4; i++)
            {
                RaiseUnloaded(treeGrid);
                rootItems.Add(new TestTreeItem($"Root{i}"));
                RaiseLoaded(treeGrid);
                Assert.Equal(i, treeGrid.Items.Count);
            }
        }
    }
}
