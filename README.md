# DaxStudio.Controls

This project contains re-usable WPF controls that have been developed as part of the DAX Studio application.

Currently this project contains the following controls:

* TreeGrid

## TreeGrid

The TreeGrid control is a DataGrid with a hierarchical column that supports expand/collapse operations. It's designed to work seamlessly with hierarchical data and provides rich customization options for displaying tree structures.

### Key Features

* The tree column can be placed anywhere in the grid, it does not need to be the first column
* Optional highlighting for the path to the selected node's children
* Supports binding to POCO objects (no need to inherit from a specific base class)
* The TreeColumn supports templating for icons, text, and expanders
* Designed to work with MVVM pattern
* Built-in context menu with expand/collapse operations
* High-performance virtualization for large datasets
* Thread-safe operations

### Basic Usage

1. Add a reference to the TreeGrid control in your WPF app
2. Add a TreeGrid to your page/window
3. Bind the RootItems property to a collection in your DataContext
4. Set the ChildrenBindingPath to the name of the field for the child items collection

### TreeGrid Properties

| Property | Type | Default | Description |
|---|---|---|---|
| AddCustomMenusAtBottom | bool | true | Controls whether custom menu items appear at the bottom or top of context menu |
| ChildrenBindingPath | string | "" | The path to the property containing child items (e.g., "Children") |
| CustomDescendantFilter | Func<object,object,bool> | null | Custom filter predicate for tree filtering operations |
| ExpandOnLoad | bool | false | Whether to expand all nodes when the control loads |
| RootItems | IEnumerable | null | The root-level items to display in the hierarchy |
| ShowDefaultContextMenu | bool | true | Shows/hides the built-in context menu with expand/collapse options |

### TreeGrid Methods

| Method | Type | Default | Description |
|---|---|---|---|
| ExecuteCustomDescendantFilter | ICommand | - | Command that executes the custom filter functionality |

### TreeGridTreeColumn Properties

| Property | Type | Default | Description |
|---|---|---|---|
| ExpanderTemplate | ControlTemplate | null | Custom template for the expand/collapse button |
| Foreground | Brush | SystemColors.ControlText | Text color for the tree cell content | 
| Icon | ImageSource | null | Icon to display in the tree cell |
| IconTemplate | DataTemplate | null | Custom template for displaying icon content |
| IndentWidth | double | 16.0 | The width in pixels for each level of indentation |
| LineStroke | Brush | Gray (#AAAAAA) | Sets the color of the brush used to draw the tree lines |
| LineThickness | double | 1.0 | Sets the thickness of the tree lines |
| SelectedLineStroke | Brush | Gray (#AAAAAA) | Sets the color for the tree line to the children of the currently selected row |
| ShowExpander | bool | true | Controls the visibility of the expander control in the tree |
| ShowTreeLines | bool | true | Controls whether to display the tree lines |
| TextPath | string | null | Path to the property for text content (e.g., "Data.Name") |
| Text | string | null | Static text content for the tree cell |
| TextTemplate | DataTemplate | null | Custom template for displaying text content |


### Example Usage

#### XAML
```xml
<ctrl:TreeGrid ItemsSource="{Binding RootItems}" 
               ChildrenBindingPath="Children"
               IndentWidth="20"
               ExpandOnLoad="true"
               EnableLazyLoading="true"
               ShowDefaultContextMenu="true">
    <ctrl:TreeGrid.Columns>
        <!-- Tree Column -->
        <ctrl:TreeGridTreeColumn Header="Name" 
                                Width="300"
                                TextPath="Data.Name"
                                IndentWidth="16"
                                ShowTreeLines="true"
                                LineStroke="#AAAAAA"
                                SelectedLineStroke="Red"
                                LineThickness="1"/>

        <!-- Additional columns -->
        <DataGridTextColumn Header="Type" 
                           Binding="{Binding Data.Type}" 
                           Width="100"/>
    
        <DataGridTextColumn Header="Description" 
                           Binding="{Binding Data.Description}" 
                           Width="*"/>
    
        <DataGridTextColumn Header="Count" 
                           Binding="{Binding Data.Count, StringFormat='#,0'}" 
                           Width="80">
            <DataGridTextColumn.ElementStyle>
                <Style TargetType="TextBlock">
                    <Setter Property="HorizontalAlignment" Value="Right"/>
                </Style>
            </DataGridTextColumn.ElementStyle>
        </DataGridTextColumn>
    </ctrl:TreeGrid.Columns>
</ctrl:TreeGrid>

```

### Example ViewModel
```csharp
using System.Collections.ObjectModel;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class TreeGridExampleViewModel : PropertyChangedBase
    {
        public TreeGridExampleViewModel()
        {
            RootItems = new ObservableCollection<TreeItem>
            {
                new TreeItem
                {
                    Name = "Tables",
                    Type = "Folder",
                    Description = "All tables in the model",
                    Children = new ObservableCollection<TreeItem>
                    {
                        new TreeItem
                        {
                            Name = "Customer",
                            Type = "Table",
                            Description = "Customer information",
                            Children = new ObservableCollection<TreeItem>
                            {
                                new TreeItem { Name = "CustomerID", Type = "Column", Description = "Unique identifier" },
                                new TreeItem { Name = "CustomerName", Type = "Column", Description = "Customer name" },
                                new TreeItem { Name = "Revenue", Type = "Measure", Description = "Total revenue" }
                            }
                        },
                        new TreeItem
                        {
                            Name = "Product",
                            Type = "Table",
                            Description = "Product catalog",
                            Children = new ObservableCollection<TreeItem>
                            {
                                new TreeItem { Name = "ProductID", Type = "Column", Description = "Product identifier" },
                                new TreeItem { Name = "ProductName", Type = "Column", Description = "Product name" }
                            }
                        }
                    }
                },
                new TreeItem
                {
                    Name = "Measures",
                    Type = "Folder",
                    Description = "All measures in the model",
                    Children = new ObservableCollection<TreeItem>
                    {
                        new TreeItem { Name = "Total Sales", Type = "Measure", Description = "Sum of all sales" },
                        new TreeItem { Name = "Average Price", Type = "Measure", Description = "Average selling price" }
                    }
                }
            };
        }

        public ObservableCollection<TreeItem> RootItems { get; }
    }

    public class TreeItem : PropertyChangedBase
    {
        private string _name;
        private string _type;
        private string _description;
        private bool _isVisible = true;
        private ObservableCollection<TreeItem> _children;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                NotifyOfPropertyChange();
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                NotifyOfPropertyChange();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                NotifyOfPropertyChange();
            }
        }

        private TreeItem _parent;
        public TreeItem Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                NotifyOfPropertyChange();
            }
        }

        public ObservableCollection<TreeItem> Children
        {
            get => _children;
            set
            {
                _children = value;
                NotifyOfPropertyChange();
            }
        }

        public string Icon => Type switch
        {
            "Table" => "tableDrawingImage",
            "Column" => "columnDrawingImage",
            "Measure" => "measureDrawingImage",
            "Folder" => "folderDrawingImage",
            _ => null
        };
    }
}

```