using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Controls.Model
{
    public interface ITreeGridRow
    {
        int Level { get; set; }
        List<bool> Ancestors { get; set; } 
        ObservableCollection<bool> SelectedLineLevels { get; set; } 
        bool IsExpanded { get; set; }
        bool HasChildren { get; }
        bool IsLastChild { get; }
    }
}
