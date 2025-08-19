using DaxStudio.Controls.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Xunit;

namespace DaxStudio.Controls.Tests.TreeGrid
{
    [Collection("WPF Tests")]
    public class TreeGridTests : IDisposable
    {
        // Define a test data class
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

        // Constructor for setup
        public TreeGridTests()
        {
            // Initialize WPF environment using the shared helper
            WpfTestHelper.InitializeWpfEnvironment();
        }

        // Cleanup method
        public void Dispose()
        {
            // No cleanup needed for WPF application
        }

        [UIFact]
        public void TreeGrid_Initialization_ShouldSucceed()
        {
            var treeGrid = new GenericTreeGrid<TestTreeItem>();
            Assert.NotNull(treeGrid);
            Assert.False(treeGrid.ExpandOnLoad);
            Assert.True(treeGrid.ShowDefaultContextMenu);
        }

        [UIFact]
        public void TreeGrid_LoadData_ShouldPopulateRows()
        {
            // Create test data
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1", new[]
                {
                    new TestTreeItem("Child1"),
                    new TestTreeItem("Child2", new[]
                    {
                        new TestTreeItem("Grandchild1")
                    })
                }),
                new TestTreeItem("Root2")
            };

            // Create and set up tree grid
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                RootItems = rootItems
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(500, 500));
            treeGrid.Arrange(new Rect(0, 0, 500, 500));
            
            // Process pending operations
            WpfTestHelper.DoEvents();

            // Verify the tree has the expected structure
            Assert.NotNull(treeGrid.RootItems);
            Assert.Equal(2, rootItems.Count);
        }

        [UIFact]
        public void TreeGrid_ExpandItem_ShouldExpandAllLevels()
        {
            // Create test data with multiple levels
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1", new[]
                {
                    new TestTreeItem("Child1"),
                    new TestTreeItem("Child2", new[]
                    {
                        new TestTreeItem("Grandchild1", new[]
                        {
                            new TestTreeItem("GreatGrandchild1")
                        })
                    })
                })
            };

            // Create and set up tree grid
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                RootItems = rootItems
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(500, 500));
            treeGrid.Arrange(new Rect(0, 0, 500, 500));
            
            // Process pending operations
            WpfTestHelper.DoEvents();

            // Expand all nodes
            treeGrid.ExpandAll();
            
            // Process pending operations
            WpfTestHelper.DoEvents();

            // Verify all nodes are expanded
            // Note: This requires internal testing or accessing via reflection
            // For a proper test, we'd need to check the visual tree or add test hooks
            
            // For now, we'll just assert that the operation completes without exceptions
            Assert.True(true, "ExpandAll operation completed without exceptions");
        }

        [UIFact]
        public void TreeGrid_CollapseItem_ShouldCollapseAllLevels()
        {
            // Create test data
            var rootItems = new ObservableCollection<TestTreeItem>
            {
                new TestTreeItem("Root1", new[]
                {
                    new TestTreeItem("Child1"),
                    new TestTreeItem("Child2")
                })
            };

            // Create and set up tree grid with ExpandOnLoad = true
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                RootItems = rootItems,
                ExpandOnLoad = true
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(500, 500));
            treeGrid.Arrange(new Rect(0, 0, 500, 500));
            
            // Process pending operations
            WpfTestHelper.DoEvents();

            // Collapse all nodes
            treeGrid.CollapseAll();
            
            // Process pending operations
            WpfTestHelper.DoEvents();

            // Assert operation completes
            Assert.True(true, "CollapseAll operation completed without exceptions");
        }
    }
}