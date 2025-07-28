# DAX Studio.Controls Documentation

The DaxStudio.Controls library provides a set of controls and utilities for building user interfaces in DAX Studio. This documentation covers the main components, their usage, and some best practices.

Currently this libarary contains the following main components:

* DaxStudio.Controls.TreeGrid: A hierarchical data grid control that supports expandable rows and custom rendering.

## TreeGrid Control

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
| RootItems | IEnumerable | null | The root-level items to display in the hierarchy |
| ChildrenBindingPath | string | "" | The path to the property containing child items (e.g., "Children") |
| IndentWidth | double | 20.0 | The width in pixels for each level of indentation |
| ExpandOnLoad | bool | false | Whether to expand all nodes when the control loads |
| EnableLazyLoading | bool | false | Enables lazy loading of child nodes for better performance |
| ShowDefaultContextMenu | bool | true | Shows/hides the built-in context menu with expand/collapse options |

### TreeGridTreeColumn Properties

| Property | Type | Default | Description |
|---|---|---|---|
| TextPath | string | null | Path to the property for text content (e.g., "Data.Name") |
| Text | string | null | Static text content for the tree cell |
| Icon | ImageSource | null | Icon to display in the tree cell |
| IndentWidth | double | 16.0 | The width in pixels for each level of indentation |
| ShowTreeLines | bool | true | Controls whether to display the tree lines |
| LineStroke | Brush | Gray (#AAAAAA) | Sets the color of the brush used to draw the tree lines |
| LineThickness | double | 1.0 | Sets the thickness of the tree lines |
| SelectedLineStroke | Brush | Gray (#AAAAAA) | Sets the color for the tree line to the children of the currently selected row |
| TextTemplate | DataTemplate | null | Custom template for displaying text content |
| IconTemplate | DataTemplate | null | Custom template for displaying icon content |
| ExpanderTemplate | ControlTemplate | null | Custom template for the expand/collapse button |