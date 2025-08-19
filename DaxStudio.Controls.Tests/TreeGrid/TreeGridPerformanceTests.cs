using DaxStudio.Controls.Model;
using DaxStudio.Controls.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;

using Xunit;


namespace DaxStudio.Controls.Tests.TreeGrid
{
    [Collection("WPF Tests")]
    public class TreeGridPerformanceTests : IDisposable
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

        // Constructor with output helper injection
        public TreeGridPerformanceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            
            // Initialize WPF environment using the shared helper
            WpfTestHelper.InitializeWpfEnvironment();
        }

        // Cleanup method
        public void Dispose()
        {
            // Any cleanup code goes here
        }

        [UIFact]
        public void PerformanceTest_ExpandLargeTree()
        {
            // Create a large tree with 5 levels and branching factor of 5
            var rootItems = CreateLargeHierarchy(5, 5);

            // Arrange
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                RootItems = rootItems,
                ExpandOnLoad = false
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(1000, 800));
            treeGrid.Arrange(new Rect(0, 0, 1000, 800));
            WpfTestHelper.DoEvents();

            // Act
            // Get internal map field
            var itemToRowMapField = typeof(GenericTreeGrid<TestTreeItem>).GetField("_itemToRowMap", BindingFlags.NonPublic | BindingFlags.Instance);
            var itemToRowMap = itemToRowMapField.GetValue(treeGrid) as Dictionary<TestTreeItem, TreeGridRow<TestTreeItem>>;

            // Get the root item
            var rootItem = treeGrid.RootItems.First();
            var rootRow = itemToRowMap[rootItem];

            // Measure the time to expand the entire tree
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Use reflection to call the ExpandItemRecursively method
            var expandMethod = typeof(GenericTreeGrid<TestTreeItem>).GetMethod("ExpandItemRecursively", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            expandMethod.Invoke(treeGrid, new object[] { rootRow });

            // Process UI events
            WpfTestHelper.DoEvents();

            stopwatch.Stop();
            var expandTime = stopwatch.ElapsedTicks;

            // Output the performance results
            _testOutputHelper.WriteLine($"Time to expand large tree: {expandTime} ticks");
            
            // No specific assertion - this is just a benchmark
            Assert.True(expandTime > 0, "Expansion time should be measurable");
        }

        [UITheory]
        [InlineData(3, 5, "Small tree - 3 levels, 5 branches")]
        [InlineData(4, 4, "Medium tree - 4 levels, 4 branches")]
        [InlineData(5, 3, "Large tree - 5 levels, 3 branches")]
        public void PerformanceTest_ExpandTreeVariousSizes(int depth, int branchingFactor, string description)
        {
            // Create a tree with the specified parameters
            var rootItems = CreateLargeHierarchy(depth, branchingFactor);

            // Arrange
            var treeGrid = new GenericTreeGrid<TestTreeItem>
            {
                ChildrenBindingPath = "Children",
                RootItems = rootItems,
                ExpandOnLoad = false
            };

            // Force layout update to simulate loading
            treeGrid.Measure(new Size(1000, 800));
            treeGrid.Arrange(new Rect(0, 0, 1000, 800));
            WpfTestHelper.DoEvents();

            // Act
            var itemToRowMapField = typeof(GenericTreeGrid<TestTreeItem>).GetField("_itemToRowMap", BindingFlags.NonPublic | BindingFlags.Instance);
            var itemToRowMap = itemToRowMapField.GetValue(treeGrid) as Dictionary<TestTreeItem, TreeGridRow<TestTreeItem>>;

            var rootItem = treeGrid.RootItems.First();
            var rootRow = itemToRowMap[rootItem];

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var expandMethod = typeof(GenericTreeGrid<TestTreeItem>).GetMethod("ExpandItemRecursively", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            expandMethod.Invoke(treeGrid, new object[] { rootRow });

            WpfTestHelper.DoEvents();

            stopwatch.Stop();
            var expandTime = stopwatch.ElapsedTicks;

            // Output the performance results with tree details
            int nodeCount = CountNodes(rootItems.First(), depth, branchingFactor);
            _testOutputHelper.WriteLine($"Test: {description}");
            _testOutputHelper.WriteLine($"Tree size: {nodeCount} nodes (depth={depth}, branches={branchingFactor})");
            _testOutputHelper.WriteLine($"Expansion time: {expandTime} ticks");
            _testOutputHelper.WriteLine($"Time per node: {(double)expandTime / nodeCount:F2} ticks");
            
            Assert.True(expandTime > 0, "Expansion time should be measurable");
        }

        private int CountNodes(TestTreeItem root, int depth, int branching)
        {
            if (depth <= 1)
                return 1;
                
            // Use the formula for a complete tree: (b^depth - 1) / (b - 1)
            return (int)((Math.Pow(branching, depth) - 1) / (branching - 1));
        }

        private ObservableCollection<TestTreeItem> CreateLargeHierarchy(int depth, int branchingFactor)
        {
            var rootItems = new ObservableCollection<TestTreeItem>();
            
            // Create a single root with many descendants for the performance test
            var root = new TestTreeItem("Root");
            rootItems.Add(root);
            
            // Create children recursively
            AddChildrenRecursively(root, depth, branchingFactor, 1);
            
            return rootItems;
        }

        private void AddChildrenRecursively(TestTreeItem parent, int maxDepth, int branchingFactor, int currentDepth)
        {
            if (currentDepth >= maxDepth)
                return;
                
            for (int i = 0; i < branchingFactor; i++)
            {
                var child = new TestTreeItem($"Item_Level{currentDepth}_Child{i}");
                parent.Children.Add(child);
                
                // Recursively add children to this child
                AddChildrenRecursively(child, maxDepth, branchingFactor, currentDepth + 1);
            }
        }
    }
}