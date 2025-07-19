using Caliburn.Micro;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Windows.Controls.Primitives;

namespace DaxStudio.UI.ViewModels
{
    public class QueryPlanTreeExampleViewModel : PropertyChangedBase
    {
        public QueryPlanTreeExampleViewModel()
        {
            var filePath = @"c:\temp\QueryPlan.json";
            
            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    
                    var loadedItems = JsonConvert.DeserializeObject<QueryPlan>(json);
                   
                    if (loadedItems != null)
                    {
                        RootItems = LoadItemsRecursively(loadedItems.PhysicalQueryPlanRows);
                    }
                }
                catch (Exception ex)
                {
                    // Handle or log error as needed
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

        }

        private ObservableCollection<QPTreeItem> LoadItemsRecursively(List<QPTreeItem> loadedItems)
        {
            ObservableCollection<QPTreeItem> items = new ObservableCollection<QPTreeItem>();
            Stack<QPTreeItem> parents = new Stack<QPTreeItem>();
            var prevItem = default(QPTreeItem);
            foreach (var item in loadedItems)
            {
                {
                    if (item.Level == 0)
                        items.Add(item);
                    else if (item.Level == (prevItem?.Level??0))
                    {
                        parents.Peek().Children.Add(item);
                    }
                    else if (item.Level > (prevItem?.Level ?? 0))
                    {
                        parents.Push(prevItem);
                        prevItem.Children = new ObservableCollection<QPTreeItem>() { item };

                    }
                    else if (item.Level < (prevItem?.Level ?? 0))
                    {
                        while (parents.Count > 0 && parents.Peek().Level >= item.Level)
                        {
                            parents.Pop();
                        }
                        parents.Peek().Children.Add(item);
                    }
                }
                prevItem = item;
            }
            return items;
        }

        public ObservableCollection<QPTreeItem> RootItems { get; }
    }

    public class QueryPlan
    {
        public int FileFormatVersion { get; set; }
        public List<QPTreeItem> PhysicalQueryPlanRows { get; set; }
        public List<QPTreeItem> LogicalQueryPlanRows { get; set; }
        public string ActivityID { get; set; }
        public string CommandText { get; set; }
        public string Parameters { get; set; }
        public string StartDatetime { get; set; }
    }

    [DataContract]
    public class QPTreeItem : PropertyChangedBase
    {

        private bool _isVisible = true;
        private ObservableCollection<QPTreeItem> _children;
        [DataMember]
        public int? Records { get; set; }
        [DataMember]
        public string Operation { get; set; }
        public string IndentedOperation { get; set; }
        [DataMember]
        public int Level { get; set; }
        [DataMember]
        public int RowNumber { get; set; }
        [DataMember]
        public int NextSiblingRowNumber { get; set; }
        public bool HighlightRow { get; set; }



        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                NotifyOfPropertyChange();
            }
        }

        private QPTreeItem _parent;
        [JsonIgnore]
        public QPTreeItem Parent
        {
            get => _parent;
            set
            {
                _parent = value;
                NotifyOfPropertyChange();
            }
        }
        [JsonIgnore]
        public ObservableCollection<QPTreeItem> Children
        {
            get => _children;
            set
            {
                _children = value;
                NotifyOfPropertyChange();
            }
        }


    }
}