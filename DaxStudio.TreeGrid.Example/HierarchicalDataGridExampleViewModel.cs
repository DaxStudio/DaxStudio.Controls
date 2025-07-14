using System.Collections.ObjectModel;
using Caliburn.Micro;
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
                    Count = 5,
                    Children = new ObservableCollection<HierarchicalItem>
                    {
                        new HierarchicalItem
                        {
                            Name = "Customer",
                            Type = "Table",
                            Description = "Customer information",
                            Count = 100,
                            Children = new ObservableCollection<HierarchicalItem>
                            {
                                new HierarchicalItem { Name = "CustomerID", Type = "Column", Description = "Unique identifier", Count = 0 },
                                new HierarchicalItem { Name = "CustomerName", Type = "Column", Description = "Customer name", Count = 0 },
                                new HierarchicalItem { Name = "Revenue", Type = "Measure", Description = "Total revenue", Count = 0 }
                            }
                        },
                        new HierarchicalItem
                        {
                            Name = "Product",
                            Type = "Table",
                            Description = "Product catalog",
                            Count = 50,
                            Children = new ObservableCollection<HierarchicalItem>
                            {
                                new HierarchicalItem { Name = "ProductID", Type = "Column", Description = "Product identifier", Count = 0 },
                                new HierarchicalItem { Name = "ProductName", Type = "Column", Description = "Product name", Count = 0 }
                            }
                        }
                    }
                },
                new HierarchicalItem
                {
                    Name = "Measures",
                    Type = "Folder",
                    Description = "All measures in the model",
                    Count = 10,
                    Children = new ObservableCollection<HierarchicalItem>
                    {
                        new HierarchicalItem { Name = "Total Sales", Type = "Measure", Description = "Sum of all sales", Count = 0 },
                        new HierarchicalItem { Name = "Average Price", Type = "Measure", Description = "Average selling price", Count = 0 }
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
        private int _count;
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

        public int Count
        {
            get => _count;
            set
            {
                _count = value;
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
        public HierarchicalItem Parent { 
            get => _parent; 
            set {                 
                _parent = value;
                NotifyOfPropertyChange();
            }
        }

        public new ObservableCollection<HierarchicalItem> Children
        {
            get => _children;
            set
            {
                _children = value;
                Count = _children.Count;
                NotifyOfPropertyChange(nameof(Children));
                NotifyOfPropertyChange(nameof(HasChildren));
                NotifyOfPropertyChange(nameof(Children));
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