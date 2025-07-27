using System;
using System.Collections.ObjectModel;
using Caliburn.Micro;

namespace DaxStudio.UI.ViewModels
{
    public class TreeGridExampleViewModel : PropertyChangedBase
    {
        public TreeGridExampleViewModel()
        {
            RootItems = GetTreeItems();
        }

        public void Reset() {
            RootItems.Clear();
            var newItems = GetTreeItems();
            foreach (var item in newItems)
            {
                RootItems.Add(item);
            }
            //RootItems.Add(newItems[1]);
        }

        public void Clear()
        {
            RootItems.Clear();

        }

        public void AddChild()
        {
            RootItems[0].Children.Add(new TreeItem { 
                Name = "Dynamic Table",
                Type = "Table",
                Description = "Dynamic Table Desc",
                Children = new ObservableCollection<TreeItem>
                            {
                                new TreeItem { Name = "DynamicID", Type = "Column", Description = "Unique identifier" },
                                new TreeItem { Name = "DynamicName", Type = "Column", Description = "Customer name" }
                            }
            });
        }

        private ObservableCollection<TreeItem> GetTreeItems()
        {
            return new ObservableCollection<TreeItem>
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