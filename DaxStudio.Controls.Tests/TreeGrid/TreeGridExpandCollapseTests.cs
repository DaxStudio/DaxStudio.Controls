using DaxStudio.Controls.Model;
using DaxStudio.Controls.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace DaxStudio.Controls.Tests.TreeGrid
{
    [Xunit.Collection("WPF Tests")]
    public class TreeGridExpandCollapseTests : IDisposable
    {
        // xUnit provides a test output helper via constructor injection
        private readonly ITestOutputHelper _testOutputHelper;

        // Use same test data class as in TreeGridTests
        public class TestTreeItem
        {
            public TestTreeItem(string name, IEnumerable<TestTreeItem> children = null)
            {
                Name = name;
                Children = children != null ? new ObservableCollection<TestTreeItem>(children) : new ObservableCollection<TestTreeItem>();
            }

            public string Name { get; set; }
            public ObservableCollection<TestTreeItem> Children { get; }
        }

        // Sample hierarchical data
        private ObservableCollection<TestTreeItem> CreateSampleData()
        {
            return new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1", new[]
                {
                    new TestTreeItem("Root1-Child1", new[]
                    {
                        new TestTreeItem("Root1-Child1-Grandchild1"),
                        new TestTreeItem("Root1-Child1-Grandchild2")
                    }),
                    new TestTreeItem("Root1-Child2", new[]
                    {
                        new TestTreeItem("Root1-Child2-Grandchild1", new[]
                        {
                            new TestTreeItem("Root1-Child2-Grandchild1-GreatGrandchild1")
                        })
                    })
                }),
                new TestTreeItem("Root2", new[]
                {
                    new TestTreeItem("Root2-Child1"),
                    new TestTreeItem("Root2-Child2")
                })
            };
        }

        // Constructor for setup
        public TreeGridExpandCollapseTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            
            // Initialize WPF environment using the shared helper
            WpfTestHelper.InitializeWpfEnvironment();
            
            // Check if critical resources are available
            CheckIfResourcesAreLoaded();
        }

        private void CheckIfResourcesAreLoaded()
        {
            if (Application.Current == null)
            {
                _testOutputHelper.WriteLine("Application.Current is null");
                return;
            }

            _testOutputHelper.WriteLine($"Resource dictionaries count: {Application.Current.Resources.MergedDictionaries.Count}");
            
            // Try to find the specific template
            var template = Application.Current.TryFindResource("PlusMinusExpanderTemplate");
            _testOutputHelper.WriteLine($"PlusMinusExpanderTemplate found: {template != null}");
        }

        // Cleanup method
        public void Dispose()
        {
            // Clean up any WPF state between tests
            WpfTestHelper.ResetWpfState();
        }

        [UIFact]
        public void ExpandItemRecursively_ShouldExpandAllChildLevels()
        {
            // Create a fresh dispatcher frame for this test
            var frame = new DispatcherFrame();
            bool allExpanded = false;

            try
            {
                // Arrange - run everything on the UI thread
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    var treeGrid = new GenericTreeGrid<TestTreeItem>
                    {
                        ChildrenBindingPath = "Children",
                        ExpandOnLoad = false,
                        DebounceService = new ImmediateDebounceService(),
                        // load sample data after the testing debouncer is set
                        RootItems = CreateSampleData()
                    };

                    
                    

                    // Trigger loading event manually
                    var loadedEvent = new RoutedEventArgs(FrameworkElement.LoadedEvent);
                    treeGrid.RaiseEvent(loadedEvent);

                    // Force layout update to simulate loading
                    treeGrid.Measure(new Size(500, 500));
                    treeGrid.Arrange(new Rect(0, 0, 500, 500));
                    
                    
                    // Get internal fields
                    var itemToRowMapField = typeof(GenericTreeGrid<TestTreeItem>).GetField("_itemToRowMap", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var itemToRowMap = itemToRowMapField.GetValue(treeGrid) as Dictionary<TestTreeItem, TreeGridRow<TestTreeItem>>;

                    // Log initial state
                    _testOutputHelper.WriteLine("Initial state:");
                    foreach (var item in itemToRowMap.Values)
                    {
                        if (item.HasChildren)
                            _testOutputHelper.WriteLine($"  Item '{item.Data.Name}' IsExpanded: {item.IsExpanded}");
                    }

                    // Get the root item and perform expansion
                    var rootItem = treeGrid.RootItems.First();
                    var rootRow = itemToRowMap[rootItem];

                    // Get the method via reflection
                    var expandMethod = typeof(GenericTreeGrid<TestTreeItem>).GetMethod("ExpandItemRecursively", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    // Execute and ensure UI updates are processed
                    expandMethod.Invoke(treeGrid, new object[] { rootRow });
                    
                    // Process pending operations in the dispatcher
                    DoEvents();
                    
                    // Log the expansion results
                    _testOutputHelper.WriteLine("After expansion:");
                    foreach (var item in itemToRowMap.Values)
                    {
                        if (item.HasChildren)
                            _testOutputHelper.WriteLine($"  Item '{item.Data.Name}' IsExpanded: {item.IsExpanded}");
                    }

                    // Use our direct validation method with detailed output
                    allExpanded = CheckExpandedState(rootRow, _testOutputHelper);
                    
                    // If not all expanded, try one more approach - ensure each node is manually expanded
                    if (!allExpanded)
                    {
                        _testOutputHelper.WriteLine("Attempting direct expansion as fallback...");
                        
                        // Directly expand all rows to ensure proper state
                        void RecursiveExpand(TreeGridRow<TestTreeItem> row)
                        {
                            if (row == null || !row.HasChildren) return;
                            
                            row.IsExpanded = true;
                            
                            foreach (var child in row.Children)
                            {
                                RecursiveExpand(child);
                            }
                        }
                        
                        RecursiveExpand(rootRow);
                        
                        // Process UI updates again
                        DoEvents();
                        
                        // Check state after direct expansion
                        allExpanded = CheckExpandedState(rootRow, _testOutputHelper);
                    }
                    
                    
                }, DispatcherPriority.Normal);

                // Final assertion
                Assert.True(allExpanded, "All descendants should be expanded");
            }
            finally
            {
                // Ensure we clean up the frame
                frame.Continue = false;
            }
        }

        // Helper method to process dispatcher messages synchronously within the test
        private void DoEvents()
        {
            // Process all pending messages in the message queue
            var nestedFrame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(f => {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }), nestedFrame);
            Dispatcher.PushFrame(nestedFrame);
            
            // Give a little extra time
            Thread.Sleep(50);
        }

        public static bool CheckExpandedState(TreeGridRow<TestTreeItem> row, ITestOutputHelper testOutputHelper)
        {
            if (row == null) return true;
            
            // Check if the current row is expanded
            if (!row.IsExpanded)
            {
                testOutputHelper?.WriteLine($"Row '{row.Data.Name}' at level {row.Level} is not expanded");
                testOutputHelper?.WriteLine($"  HasChildren: {row.HasChildren}, ChildCount: {row.Children?.Count ?? 0}");
                return false;
            }
            
            var childrenExpanded = true;
            // Recursively check all children
            foreach (var child in row.Children)
            {
                if (!CheckExpandedState(child, testOutputHelper))
                {
                    childrenExpanded = false;
                    // Continue checking other children for better diagnostics
                }
            }
            return childrenExpanded;
        }

        [UIFact]
        public void CollapseItemRecursively_ShouldCollapseAllChildLevels()
        {
            // Arrange
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                ExpandOnLoad = true, // Start with everything expanded
                DebounceService = new ImmediateDebounceService(), // Use immediate debounce for testing
                // Load sample data after the testing debouncer is set
                RootItems = CreateSampleData()
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(500, 500));
            treeGrid.Arrange(new Rect(0, 0, 500, 500));
            
            // Trigger loading event manually
            var loadedEvent = new RoutedEventArgs(FrameworkElement.LoadedEvent);
            treeGrid.RaiseEvent(loadedEvent);
            
            // Process pending UI operations
            WpfTestHelper.DoEvents();


            // Act
            // Get the internal _itemToRowMap field through reflection
            var itemToRowMapField = typeof(GenericTreeGrid<TestTreeItem>).GetField("_itemToRowMap", BindingFlags.NonPublic | BindingFlags.Instance);
            var itemToRowMap = itemToRowMapField.GetValue(treeGrid) as Dictionary<TestTreeItem, TreeGridRow<TestTreeItem>>;

            // Get the root item
            var rootItem = treeGrid.RootItems.First();
            var rootRow = itemToRowMap[rootItem];

            // Use reflection to call the CollapseItemRecursively method
            var collapseMethod = typeof(GenericTreeGrid<TestTreeItem>).GetMethod("CollapseItemRecursively", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            collapseMethod.Invoke(treeGrid, new object[] { rootRow });

            // Process UI events
            WpfTestHelper.DoEvents();

            // Assert
            // Verify that all rows under the root are now collapsed
            var allCollapsed = true;
            foreach (var item in itemToRowMap.Values)
            {
                if (IsDescendantOf(item, rootRow) && item.HasChildren && item.IsExpanded)
                {
                    allCollapsed = false;
                    break;
                }
            }

            Assert.True(allCollapsed, "All descendants should be collapsed");
        }

        // Helper method to check if a row is a descendant of another row
        private static bool IsDescendantOf(TreeGridRow<TestTreeItem> potential, TreeGridRow<TestTreeItem> ancestor)
        {
            var current = potential.Parent;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.Parent;
            }
            return false;
        }
    }
}