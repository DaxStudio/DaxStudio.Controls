# DaxStudio.Controls

This project contains re-usable WPF controls that have been developed as part of the DAX Studio application.

Currently this project contains the following:

* TreeGrid

## TreeGrid

This control is a DataGrid with a hierarchical column that supports expand/collapse operations

* The tree column can be placed anywhere in the grid, it does not need to be the first column
* It has optional highlighting for the path to the selected nodes children
* It supports binding to POCO objects (no need to inherit from a specific base class)
* The TreeColumn supports templating
* Designed to work with MVVM
* The 
	* 

### Basic Use

1. Add a reference TreeGrid control to your WPF app
1. Add a TreeGrid to your page/window
1. Bind the RootItems property to a collection in your DataContext (your ViewModel if you are using an MVVM approach)
1. Set the ChildrenBindingPath to the name of the child items collection

### TreeColumn Configuration

| Property | Default | Description |
|---|---|---| 
|TextPath | | Set the text for the Tree Column by setting the TextPath to "Data.<property>" (eg 1TextPath="Data.Name"1) |
|IndentWidth | 16 | You can change the default indent size by setting this property |
| ShowTreeLines | True | Controls whether to display the tree lines |
| LineStroke | Sets the color of the brush used to draw the tree lines |
| LineThickness | 1 | Sets the thickness of the tree lines |
| SelectedLineStroke | Sets the color for the tree line to the children of the currently selected row |
