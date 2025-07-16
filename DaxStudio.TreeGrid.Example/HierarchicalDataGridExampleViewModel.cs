using System.Collections.Generic;
using System.Collections.ObjectModel;
using Caliburn.Micro;
using DaxStudio.TreeGrid;
using DaxStudio.UI.Controls;

namespace DaxStudio.UI.ViewModels
{
    public class HierarchicalDataGridExampleViewModel : PropertyChangedBase
    {
        public HierarchicalDataGridExampleViewModel()
        {
            RootItems = new ObservableCollection<HierarchicalItem>
            {
                new HierarchicalItem
                {
                    Name = "Tables",
                    Type = "Folder",
                    Description = "All tables in the model",
                    Children = new ObservableCollection<HierarchicalItem>
                    {
                        new HierarchicalItem
                        {
                            Name = "Customer",
                            Type = "Table",
                            Description = "Customer information",
                            Children = new ObservableCollection<HierarchicalItem>
                            {
                                new HierarchicalItem { Name = "CustomerID", Type = "Column", Description = "Unique identifier" },
                                new HierarchicalItem { Name = "CustomerName", Type = "Column", Description = "Customer name" },
                                new HierarchicalItem { Name = "Revenue", Type = "Measure", Description = "Total revenue" }
                            }
                        },
                        new HierarchicalItem
                        {
                            Name = "Product",
                            Type = "Table",
                            Description = "Product catalog",
                            Children = new ObservableCollection<HierarchicalItem>
                            {
                                new HierarchicalItem { Name = "ProductID", Type = "Column", Description = "Product identifier" },
                                new HierarchicalItem { Name = "ProductName", Type = "Column", Description = "Product name" }
                            }
                        }
                    }
                },
                new HierarchicalItem
                {
                    Name = "Measures",
                    Type = "Folder",
                    Description = "All measures in the model",
                    Children = new ObservableCollection<HierarchicalItem>
                    {
                        new HierarchicalItem { Name = "Total Sales", Type = "Measure", Description = "Sum of all sales" },
                        new HierarchicalItem { Name = "Average Price", Type = "Measure", Description = "Average selling price" }
                    }
                }
            };
        }

        public ObservableCollection<HierarchicalItem> RootItems { get; }
    }

    public class HierarchicalItem : PropertyChangedBase
    {
        private string _name;
        private string _type;
        private string _description;
        private bool _isVisible = true;
        private ObservableCollection<HierarchicalItem> _children;

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

        private HierarchicalItem _parent;
        public HierarchicalItem Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                NotifyOfPropertyChange();
            }
        }

        public ObservableCollection<HierarchicalItem> Children
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